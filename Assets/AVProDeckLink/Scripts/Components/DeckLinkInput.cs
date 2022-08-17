using UnityEngine;
using System.Collections;
using System;
using System.IO;
using System.Collections.Generic;

//-----------------------------------------------------------------------------
// Copyright 2014-2020 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProDeckLink
{
	[AddComponentMenu("AVPro DeckLink/DeckLinkInput")]
	public class DeckLinkInput : DeckLink
	{
		//video settings

		public bool _autoDeinterlace = true;

		public bool _autoDetectMode = true;

		[HideInInspector]
		[SerializeField]
		private bool _flipX = false;

		[HideInInspector]
		[SerializeField]
		private bool _flipY = false;

		//audio settings
		[Range(0f, 10f)]
		public float _audioVolume = 1f;

		public bool _muteAudio = false;

		public bool _bypassGamma = false;

		private AudioSource _audioSource;

		[HideInInspector]
		[SerializeField]
		[Range(1, 12)]
		[Tooltip("Total number of input buffers")]
		private int _inputBufferCount = 3;

		[HideInInspector]
		[SerializeField]
		[Range(0, 6)]
		[Tooltip("Number of input buffers to store before reading from the buffer.  0 = Least latency, but can cause buffer contention resulting in jittering.  2 = Ideal value for smooth input.  Must be less than _inputBufferCount.")]
		private int _inputBufferReadCount = 0;

		public ulong LastFrameTimestamp
		{
			get { return _device == null ? 0 : _device.OutputFrameNumber; }
		}

		public bool FlipX
		{
			get { return _flipX; }
			set
			{
				_flipX = value;
				if(_device != null)
				{
					_device.FlipInputX = _flipX;
				}
			}
		}

		public bool FlipY
		{
			get { return _flipY; }
			set
			{
				_flipY = value;
				if (_device != null)
				{
					_device.FlipInputY = _flipY;
				}
			}
		}

		public AudioSource InputAudioSource
		{
			get { return _audioSource; }
		}

		public Texture OutputTexture
		{
			get
			{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
				if (_device != null && _device.OutputTexture != null && _device.ReceivedSignal)
					return _device.OutputTexture;
#endif
				return null;

			}
		}

		public Texture RightOutputTexture
		{
			get
			{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
				if (_device != null && _device.RightOutputTexture != null && _device.ReceivedSignal)
					return _device.RightOutputTexture;
#endif
				return null;

			}
		}

		public bool UnsetInputReceivedFlag
		{
			get; set;
		}

		protected override void Process()
		{
			if (_device != null)
			{
				if(_device.BypassGammaCorrection != _bypassGamma)
				{
					_device.BypassGammaCorrection = _bypassGamma;
				}
				if(_device.IgnoreAlphaChannel != _ignoreAlphaChannel)
				{
					_device.IgnoreAlphaChannel = _ignoreAlphaChannel;
				}
				if(_device.InputColorspaceMode != _colorspaceMode)
				{
					_device.InputColorspaceMode = _colorspaceMode;
				}

				if(_enable3D != _device.Enable3DInput)
				{
					bool reset = _device.IsStreamingInput;

					if(reset)
					{
						StopInput();
					}

					//_device.Enable3DInput = _enable3D;

					Begin(true);
				}
			}
		}

		protected override bool IsInput()
		{
			return true;
		}

		public override void Awake()
		{
			base.Awake();
			_audioSource = gameObject.AddComponent<AudioSource>();
			_audioSource.hideFlags = HideFlags.HideInInspector;
			_audioSource.loop = true;
		}

		public void LateUpdate()
		{
			/*if(_device != null && UnsetInputReceivedFlag)
			{
				_device.SetInputFrameReceived(false);
			}*/
		}

		protected override void BeginDevice()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			DeckLinkPlugin.SetAutoDetectEnabled(_deviceIndex, _autoDetectMode);

			UnsetInputReceivedFlag = false;

			_device.AutoDeinterlace = _autoDeinterlace;

			if (_audioChannels > _device.MaxAudioChannels)
			{
				Debug.LogWarning("[AVProDeckLink] Output audio channel count too high, clamping to  " + _device.MaxAudioChannels);
				_audioChannels = _device.MaxAudioChannels;	
			}

			if (_modeIndex < 0)
			{ 
				if (_autoDetectMode)
				{ 
					_modeIndex = 0;
				}
				else 
				{
					Debug.LogWarning("[AVProDeckLink] Desired format not found - the resolution or pixel format may not be supported, consider enabling mode auto detection");
				}
			}

			_device.Enable3DInput = _enable3D;
			_device.EnableAncillaryDataInput = _enableAncillaryData;
			_device.EnableTimeCodeInput = _enableTimeCodeCapture;
#if FEATURE_SYNCGROUPS_WIP
			_device.UseInputSyncGroup = _useSyncGroup;
			_device.InputSyncGroup = _syncGroupIndex;
#endif
			_device.SetInputBufferSizes(_inputBufferCount, _inputBufferReadCount);

			if (!_device.StartInput(_modeIndex, new AudioSettings(_audioChannels, _audioBitDepth), false, _useHdr))
			{
				Debug.LogError("[AVProDeckLink] Failed to start input device");
				StopInput();
			}

			if(_device != null)
			{
				_enable3D = _device.Enable3DInput;
				_device.FlipInputX = _flipX;
				_device.FlipInputY = _flipY;
				DeckLinkPlugin.FlushAudioBuffer(_deviceIndex);
			}
#endif	
		}

		void OnAudioFilterRead(float[] data, int channels)
		{
			if (_audioChannels > 0)
			{
				if (!_muteAudio)
				{
					DeckLinkPlugin.GetAudioBuffer(_deviceIndex, data, data.Length, channels, _audioVolume);
				}
				else
				{
					DeckLinkPlugin.FlushAudioBuffer(_deviceIndex);
				}
			}
		}

		public bool StopInput()
		{
			_audioSource.Stop();
			if (_audioSource.clip != null)
			{
				AudioClip.Destroy(_audioSource.clip);
				_audioSource.clip = null;
			}

			if (_device == null)
			{
				return false;
			}

			_device.StopInput();
			_device = null;

			return true;
		}

		public void Pause()
		{
			if (_device != null)
			{
				_device.Pause();
			}
		}

		public void Unpause()
		{
			if (_device != null)
			{
				_device.Unpause();
			}
		}

#if UNITY_EDITOR
		[ContextMenu("Save Input PNG")]
		public override void SavePNG()
		{
			if (OutputTexture != null)
			{
				if (RightOutputTexture != null)
				{
					Helper.SavePNG("Image-Input-Left.png", (RenderTexture)OutputTexture);
					Helper.SavePNG("Image-Input-Right.png", (RenderTexture)RightOutputTexture);
				}
				else
				{
					Helper.SavePNG("Image-Input.png", (RenderTexture)OutputTexture);
				}
			}
		}

#if UNITY_5_6_OR_NEWER
		[ContextMenu("Save Input EXR")]
		public override void SaveEXR()
		{
			if (OutputTexture != null)
			{
				if (RightOutputTexture != null)
				{
					Helper.SaveEXR("Image-Input-Left.exr", (RenderTexture)OutputTexture);
					Helper.SaveEXR("Image-Input-Right.exr", (RenderTexture)RightOutputTexture);
				}
				else
				{
					Helper.SaveEXR("Image-Input.exr", (RenderTexture)OutputTexture);
				}
			}
		}
#endif
#endif
		protected override void Cleanup()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			DeckLinkPlugin.SetAutoDetectEnabled(_deviceIndex, false);

			_audioSource.Stop();
			if(_audioSource.clip != null)
			{
				AudioClip.Destroy(_audioSource.clip);
				_audioSource.clip = null;
			}
#endif
		}
	}
}
