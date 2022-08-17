//#define DeckLink_Output_MultiBlended

using UnityEngine;
using System.Collections;
using System;
using System.Runtime.InteropServices;

//-----------------------------------------------------------------------------
// Copyright 2014-2020 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProDeckLink
{
	[StructLayout(LayoutKind.Sequential, Size = 32)]
	public struct ComputeBufferParams
	{
		public uint width;
		public uint height;
		public uint bufferWidth;
		public uint bigEndian;
		public uint leading;
		public uint isLinear;
		public uint flipX;
		public uint flipY;

		public static int Size()
		{
			return 32;
		}
	}

	[AddComponentMenu("AVPro DeckLink/DeckLinkOutput")]
	public class DeckLinkOutput : DeckLink
	{
		private class PerEye
		{
			// Source texture override (if not rendering via camera)
			public Texture sourceTexture = null;

			public RenderTexture inputTexture = null;
			public RenderTexture[] capturedFrames = null;
			public RenderTexture convertedTexture;
			public byte[] outputBuffer = null;
			public RenderTexture blended = null;
			public IntPtr convertedPointer = IntPtr.Zero;

			public void Release()
			{
				if (inputTexture != null)
				{
					RenderTexture.ReleaseTemporary(inputTexture);
					inputTexture = null;
				}

				if(capturedFrames != null)
				{
					foreach(var frame in capturedFrames)
					{
						RenderTexture.ReleaseTemporary(frame);
					}
					capturedFrames = null;
				}

				if(blended != null)
				{
					RenderTexture.ReleaseTemporary(blended);
					blended = null;
				}

				if(convertedTexture != null)
				{
					RenderTexture.ReleaseTemporary(convertedTexture);
					convertedTexture = null;
					convertedPointer = IntPtr.Zero;
				}

				if (outputBuffer != null)
				{
					outputBuffer = null;
				}
			}
		}

		private PerEye[] _eye = new PerEye[2];
		private PerEye LeftEye { get { return _eye[0]; } }
		private PerEye RightEye { get { return _eye[1]; } }

		//Anti-Aliasing
		public enum AALevel
		{
			None = 1,
			Two = 2,
			Four = 4,
			Eight = 8
		}

		public DeckLinkInput _syncedToInput;

		public AALevel _antiAliasingLevel = AALevel.Two;

		//buffering & timing
		[Range(2, 9)]
		public int _bufferBalance = 2;

		public enum NoCameraMode
		{
			None,
			Colour,
			DefaultTexture
		}

		public NoCameraMode _noCameraMode;
		public Texture _defaultTexture;
		public Color _defaultColour;

		private int _outputFrameRate = -1;
		private int _currFrame = 0;
		private bool _canOutputFrame = false;
		private int _targetFrameRate = -1;
		private float prevFrameTime = 0.0f;
		private float currFrameTime = 0.0f;
		private float _timeSinceLastFrame = 0f;
		private bool _current3DEnabled;
		private bool _multiOutputCached;

		//pipeline textures/
		public Camera _camera;
		public Camera _rightEyeCamera;
		public bool _lowLatencyMode = false;

		private Shader _rgbaToYuv422Shader;
		private Shader _rgbaToYuv422Shader2;
		private Shader _rgbaToBgraShader;
		private Shader _rgbaToArgbShader;
		private Shader _interlaceShader;
		private Shader _blendShader;
		private ComputeShader _abgrTo10bitARGB;

		//left
//		private RenderTexture _inputTexture;
//		private RenderTexture[] _capturedFrames = null;
//		private RenderTexture _convertedTexture;
//		private byte[] _outputBuffer = null;
//		private RenderTexture _blended;

//		//right
//		private RenderTexture _rightInputTexture;
//		private RenderTexture[] _rightCapturedFrames = null;
//		private RenderTexture _rightConvertedTexture;
//		private byte[] _rightOutputBuffer = null;
//		private RenderTexture _rightBlended;
		
		private RenderTexture _interlacedTexture;

		private ComputeBuffer _convertedCompBuffer = null;
		private ComputeBuffer _parameters = null;
		
		private Material _interlaceMaterial;
		private Material _conversionMaterial;
		private Material _blendMat;

		private DeckLinkPlugin.PixelFormat _format = DeckLinkPlugin.PixelFormat.Unknown;
		private bool _interlaced;
		private InterlacePass _interlacePass = InterlacePass.First;
//		private IntPtr _convertedPointer = IntPtr.Zero;
//		private IntPtr _rightConvertedPointer = IntPtr.Zero;

		private byte _currCapturedFrame = 0;

		//Audio
		public AudioSource _outputAudioSource;
		public bool _muteOutputAudio = false;

		private DeckLinkAudioOutput _audioOutputManager = null;

		public bool _bypassGamma = false;

		//misc
		public int _genlockPixelOffset = 0;
		public SmpteLevel _smpteLevel = SmpteLevel.Unspecified;

		private static int refCount = 0;
		private static int prevRefCount;

		private const string ShaderKeyRec709 = "USE_REC709";
		private const string ShaderKeyRec2100 = "USE_REC2100";
		private const string ShaderKeyRec2020 = "USE_REC2020";
		private const string ShaderKeyIgnoreAlpha = "IGNORE_ALPHA";

		public RenderTexture InputTexture
		{
			get { return LeftEye.inputTexture; }
		}

		public RenderTexture RightInputTexture
		{
			get { return RightEye.inputTexture; }
		}

		public enum KeyerMode
		{
			None = 0,
			Internal,
			External,
		}

		public KeyerMode _keyerMode = KeyerMode.None;

		protected override void Init()
		{
			base.Init();

			_current3DEnabled = _enable3D;
		}

		private void FindShaders()
		{
			_rgbaToYuv422Shader = Shader.Find("AVProDeckLink/RGBA 4:4:4 to UYVY 4:2:2");
			_rgbaToYuv422Shader2 = Shader.Find("AVProDeckLink/RGBA 4:4:4 to UYVY 4:2:2 10-bit");
			_rgbaToBgraShader = Shader.Find("AVProDeckLink/RGBA 4:4:4 to BGRBA 4:4:4");
			_rgbaToArgbShader = Shader.Find("AVProDeckLink/RGBA 4:4:4 to ARGB 4:4:4");
			_interlaceShader = Shader.Find("AVProDeckLink/Interlacer");
			_blendShader = Shader.Find("AVProDeckLink/BlendFrames");
			_abgrTo10bitARGB = (ComputeShader)Resources.Load("Shaders/AVProDeckLink_RGBA_to_10RGBX");
		}

		private void InitializeAudioOutput()
		{
			DeckLinkAudioOutput[] audioOutputs = FindObjectsOfType<DeckLinkAudioOutput>();
			if (audioOutputs.Length > 1)
			{
				Debug.LogError("[AVProDeckLink] There should never be more than one DeckLinkAudioOutput object per scene");
			}
			else if (audioOutputs.Length == 1)
			{
				_audioOutputManager = audioOutputs[0];
			}
			else
			{
				if (_outputAudioSource == null)
				{
					AudioListener[] listeners = FindObjectsOfType<AudioListener>();

					GameObject listenerObject;

					if (listeners.Length == 0)
					{
						listenerObject = new GameObject("[AVProDeckLink]Listener");
						listenerObject.AddComponent<AudioListener>();
					}
					else
					{
						listenerObject = listeners[0].gameObject;
					}

					_audioOutputManager = listenerObject.AddComponent<DeckLinkAudioOutput>();

#if UNITY_5 && (UNITY_5_1 || UNITY_5_2)
					// TODO: comment why this is here?
					DeckLinkAudioOutput temp = listenerObject.AddComponent<DeckLinkAudioOutput>();
					Destroy(temp);
#endif
				}
				else
				{
					_audioOutputManager = _outputAudioSource.gameObject.AddComponent<DeckLinkAudioOutput>();
				}
			}
		}

		private void UpdateReferenceCounter()
		{
			if (refCount == 0)
			{
				prevRefCount = QualitySettings.vSyncCount;
				QualitySettings.vSyncCount = 0;
			}

			refCount++;
		}

		public override void Awake()
		{
			base.Awake();

			for (int i = 0; i < _eye.Length; i++)
			{
				_eye[i] = new PerEye();
			}

			UpdateReferenceCounter();
			FindShaders();
			InitializeAudioOutput();

			DeckLinkManager.Instance.AddOutputDevice(this);
		}

		private bool IsEyeUsed(int eyeIndex)
		{
			if (eyeIndex == 0) return true;
			return _current3DEnabled;
		}

		private void InitCaptureBlendResources(int width, int height)
		{
			RenderTextureFormat renderTextureFormat = RenderTextureFormat.ARGB32;
			if (_useHdr)
			{
				renderTextureFormat = RenderTextureFormat.ARGBFloat;
			}
			// RJT TODO: If attaching to a camera then match it's format?
/*			if (_camera && _camera.targetTexture)
			{
				renderTextureFormat = _camera.targetTexture.format;
			}*/

			if (_blendMat == null)
			{
				_blendMat = new Material(_blendShader);
			}

			for (int i = 0; i < 2; i ++)
			{
				PerEye eye = _eye[i];

				if(eye.capturedFrames != null)
				{
					foreach(var frame in eye.capturedFrames)
					{
						RenderTexture.ReleaseTemporary(frame);
					}
					eye.capturedFrames = null;
				}

				if(eye.blended != null && (!IsEyeUsed(i) || eye.blended.width != width || eye.blended.height != height || eye.blended.antiAliasing != (int)_antiAliasingLevel))
				{
					RenderTexture.ReleaseTemporary(eye.blended);
					eye.blended = null;
				}

#if DeckLink_Output_MultiBlended
				if (DeckLinkSettings.Instance._multiOutput)
				{
					if (IsEyeUsed(i))
					{
						eye.capturedFrames = new RenderTexture[2];
						for(int j = 0; j < 2; ++j)
						{
							eye.capturedFrames[j] = RenderTexture.GetTemporary(width, height, 0, renderTextureFormat, RenderTextureReadWrite.Default, (int)_antiAliasingLevel);
						}
					}
				}
#endif

				if(eye.blended == null && IsEyeUsed(i))
				{
					eye.blended = RenderTexture.GetTemporary(width, height, 0, renderTextureFormat, RenderTextureReadWrite.Default, (int)_antiAliasingLevel);
				}

				if (eye.inputTexture != null)
				{
					if (!IsEyeUsed(i) || eye.inputTexture.width != width || eye.inputTexture.height != height || eye.inputTexture.antiAliasing != (int)_antiAliasingLevel)
					{
						RenderTexture.ReleaseTemporary(eye.inputTexture);
						eye.inputTexture = null;
					}
				}

				if (eye.inputTexture == null && IsEyeUsed(i))
				{
					eye.inputTexture = RenderTexture.GetTemporary(width, height, 24, renderTextureFormat, RenderTextureReadWrite.Default, (int)_antiAliasingLevel);
				}

				if (eye.inputTexture != null && !eye.inputTexture.IsCreated())
				{
					eye.inputTexture.Create();
				}
			}
		}

		private void InitConversionResources(DeckLinkPlugin.PixelFormat format, int width, int height)
		{
			if (_conversionMaterial != null || _format != format)
			{
				Material.Destroy(_conversionMaterial);
				_conversionMaterial = null;
				_format = format;
			}

			int texWidth = -1;
			// If we are doing keying and a non-RGBA mode is used for output, then use an RGBA texture, 
			// as this conversion will be handled by the DeckLink hardware.
			if (_keyerMode != KeyerMode.None && !HasAlphaChannel(format))
			{
				_conversionMaterial = new Material(_rgbaToArgbShader);
				texWidth = width;
			}
			else
			{
				// Otherwise convert to the output format
				switch (format)
				{
					case DeckLinkPlugin.PixelFormat.YCbCr_8bpp_422:
						_conversionMaterial = new Material(_rgbaToYuv422Shader);
						texWidth = width / 2;
						break;
					case DeckLinkPlugin.PixelFormat.YCbCr_10bpp_422:
						_conversionMaterial = new Material(_rgbaToYuv422Shader2);
						texWidth = (width / 6) * 4;
						_conversionMaterial.SetFloat("_TextureWidth", texWidth);
						break;
					case DeckLinkPlugin.PixelFormat.BGRA_8bpp_444:
						_conversionMaterial = new Material(_rgbaToBgraShader);
						texWidth = width;
						break;
					case DeckLinkPlugin.PixelFormat.ARGB_8bpp_444:
						_conversionMaterial = new Material(_rgbaToArgbShader);
						texWidth = width;
						break;
					default:
						break;
				}
			}

			if (_parameters != null)
			{
				_parameters.Release();
				_parameters = null;
			}

			DeckLinkPlugin.SetOutputBufferPointer(_deviceIndex, null);
			DeckLinkPlugin.SetOutputTexturePointer(_deviceIndex, IntPtr.Zero);
			DeckLinkPlugin.SetRightOutputBufferPointer(_deviceIndex, null);
			DeckLinkPlugin.SetRightOutputTexturePointer(_deviceIndex, IntPtr.Zero);

			for (int i = 0; i < 2; i ++)
			{
				PerEye eye = _eye[i];
				if (eye.convertedTexture != null)
				{
					RenderTexture.ReleaseTemporary(eye.convertedTexture);
					eye.convertedTexture = null;
					eye.convertedPointer = IntPtr.Zero;
				}
			}

			LeftEye.outputBuffer = null;
			RightEye.outputBuffer = null;

			if (_convertedCompBuffer != null)
			{
				_convertedCompBuffer.Release();
				_convertedCompBuffer = null;
			}

			if (texWidth < 0)
			{
				//sets up compute buffers 
				if (_format == DeckLinkPlugin.PixelFormat.RGBX_10bpp_444 || 
					_format == DeckLinkPlugin.PixelFormat.RGBX_10bpp_444_LE ||
					_format == DeckLinkPlugin.PixelFormat.RGB_10bpp_444)
				{
					_parameters = new ComputeBuffer(1, ComputeBufferParams.Size());

					ComputeBufferParams[] parms = new ComputeBufferParams[1];
					parms[0].height = (uint)height;
					parms[0].width = (uint)width;
					parms[0].bufferWidth = (uint)(width + 63) / 64 * 64;
					parms[0].leading = _format == DeckLinkPlugin.PixelFormat.RGB_10bpp_444 ? 1U : 0U;
					bool formatBigEndian = _format != DeckLinkPlugin.PixelFormat.RGBX_10bpp_444_LE ? true : false;
					if(BitConverter.IsLittleEndian)
					{
						formatBigEndian = !formatBigEndian;
					}
					parms[0].bigEndian = formatBigEndian ? 1U : 0U;
					parms[0].isLinear = QualitySettings.activeColorSpace == ColorSpace.Linear ? 1U : 0U;

					LeftEye.outputBuffer = new byte[parms[0].bufferWidth * parms[0].height * 4];
					if (_current3DEnabled)
					{
						RightEye.outputBuffer = new byte[parms[0].bufferWidth * parms[0].height * 4];
					}

					_convertedCompBuffer = new ComputeBuffer((int)(parms[0].bufferWidth * parms[0].height), 4, ComputeBufferType.Raw);

					_parameters.SetData(parms);

					DeckLinkPlugin.SetOutputBufferPointer(_deviceIndex, LeftEye.outputBuffer);
					DeckLinkPlugin.SetRightOutputBufferPointer(_deviceIndex, RightEye.outputBuffer);
				}
				else
				{
					RenderTextureReadWrite readWriteMode = (QualitySettings.activeColorSpace == ColorSpace.Linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.Default);
					for (int i = 0; i < 2; i ++)
					{
						PerEye eye = _eye[i];
						if (IsEyeUsed(i))
						{
							eye.convertedTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, readWriteMode, 1);
							eye.convertedPointer = eye.convertedTexture.GetNativeTexturePtr();
							if (i == 0) { DeckLinkPlugin.SetOutputTexturePointer(_deviceIndex, eye.convertedPointer); }
							else { DeckLinkPlugin.SetRightOutputTexturePointer(_deviceIndex, eye.convertedPointer); }
						}

					}
				}
			}
			else
			{
				RenderTextureReadWrite readWriteMode = (QualitySettings.activeColorSpace == ColorSpace.Linear ? RenderTextureReadWrite.Linear : RenderTextureReadWrite.Default);

				RenderTextureFormat textureFormat = RenderTextureFormat.ARGB32;
				if (format == DeckLinkPlugin.PixelFormat.YCbCr_10bpp_422)
				{
					textureFormat = RenderTextureFormat.ARGB2101010;
				}

				for (int i = 0; i < 2; i ++)
				{
					PerEye eye = _eye[i];
					if (IsEyeUsed(i))
					{
						eye.convertedTexture = RenderTexture.GetTemporary(texWidth, height, 0, textureFormat, readWriteMode, 1);
						eye.convertedPointer = eye.convertedTexture.GetNativeTexturePtr();
						if (i == 0) { DeckLinkPlugin.SetOutputTexturePointer(_deviceIndex, eye.convertedPointer); }
						else { DeckLinkPlugin.SetRightOutputTexturePointer(_deviceIndex, eye.convertedPointer); }
					}
				}
			}
		}

		private void InitInterlaceResources(int width, int height)
		{
			if (_interlaceMaterial == null)
			{
				_interlaceMaterial = new Material(_interlaceShader);
			}

			_interlaceMaterial.SetFloat("_TextureHeight", height);

			if (_interlacedTexture != null)
			{
				if (_interlacedTexture.width != width || _interlacedTexture.height != height)
				{
					RenderTexture.ReleaseTemporary(_interlacedTexture);
					_interlacedTexture = null;
				}
			}

			if (_interlacedTexture == null)
			{
				_interlacedTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default);
				_interlacedTexture.filterMode = FilterMode.Point;

				if (!_interlacedTexture.IsCreated())
				{
					_interlacedTexture.Create();
				}
			}
		}

		private static bool HasAlphaChannel(DeckLinkPlugin.PixelFormat format)
		{
			bool result = false;
			switch (format)
			{
				case DeckLinkPlugin.PixelFormat.BGRA_8bpp_444:
				case DeckLinkPlugin.PixelFormat.ARGB_8bpp_444:
					result = true;
					break;
			}
			return result;
		}

		public static bool OutputFormatSupported(DeckLinkPlugin.PixelFormat format)
		{
			bool result = false;
			switch (format)
			{
				case DeckLinkPlugin.PixelFormat.YCbCr_10bpp_422:
				case DeckLinkPlugin.PixelFormat.YCbCr_8bpp_422:
				case DeckLinkPlugin.PixelFormat.BGRA_8bpp_444:
				case DeckLinkPlugin.PixelFormat.ARGB_8bpp_444:
				case DeckLinkPlugin.PixelFormat.RGBX_10bpp_444:
				case DeckLinkPlugin.PixelFormat.RGBX_10bpp_444_LE:
				case DeckLinkPlugin.PixelFormat.RGB_10bpp_444:
					result = true;
					break;
			}
			return result;
		}

		public int TargetFramerate
		{
			get { return _targetFrameRate; }
		}

		public int OutputFramerate
		{
			get
			{
				if (DeckLinkSettings.Instance._multiOutput)
				{
					return _outputFrameRate;
				}
				else
				{
					return Application.targetFrameRate;
				}
			}
		}

		public void SetSourceTexture(Texture textureLeft, Texture textureRight = null)
		{
			LeftEye.sourceTexture = textureLeft;
			RightEye.sourceTexture = textureRight;
		}

		public void SetCamera(Camera camera)
		{
			if (_camera != null)
			{
				_camera.targetTexture = null;
			}

			_camera = camera;
			AssignCameraTargetTexture(_camera, LeftEye);
		}

		public void SetRightEyeCamera(Camera camera)
		{
			if(_rightEyeCamera != null)
			{
				_rightEyeCamera.targetTexture = null;
			}

			_rightEyeCamera = camera;
			AssignCameraTargetTexture(_rightEyeCamera, RightEye);
		}

		private int _lastInputFrameCount = -1;

		public bool CanOutputFrame()
		{
			if (Time.frameCount != _currFrame)
			{
				_currFrame = Time.frameCount;

				if(_syncedToInput != null)
				{
					// RJT TODO: If syncing to say a 24hz input but we're running a 60hz output then this will
					// cause the output to drop frames as it's still trying to consume at 60 but only being fed at 24
					_canOutputFrame = _syncedToInput.Device != null && _syncedToInput.Device.IsStreamingInput && _syncedToInput.Device.InputFrameReceived();
					if (_canOutputFrame)
					{
						// Now check that we actually have a new frame, 
						// unless we're in the 2nd pass of interlaced mode, where we reuse the same input frame
						if (!_interlaced || _interlacePass == InterlacePass.First)
						{
							if (_syncedToInput.Device.InputFramesTotal == _lastInputFrameCount)
							{
								//Debug.Log("No new input frame available... last frame:" + _lastInputFrameCount);
								// In this case the latest texture in the input device has the same frame count as it did last time, so this frame will duplicate.
								// This usually happens when the input queue isn't being fulled fast enough, or Unity is running too quickly
								// TODO: should we Sleep() here to help give time for the input frames to arrive?
								_canOutputFrame = false;
							}
							else
							{
								//Debug.Log("Good frame..." + _lastInputFrameCount + " " + _syncedToInput.Device.FramesTotal);
								_lastInputFrameCount = _syncedToInput.Device.InputFramesTotal;
							}
						}
						else
						{
							_lastInputFrameCount = _syncedToInput.Device.InputFramesTotal;
						}
					}
				}
				else
				{
					float secondsPerFrame = 1f / (float)_outputFrameRate;
					float delta = Mathf.Min(secondsPerFrame, Time.unscaledDeltaTime);

					_timeSinceLastFrame += delta;

					if (_outputFrameRate < 0 || _timeSinceLastFrame >= secondsPerFrame)
					{
						if (secondsPerFrame > 0)
						{
							_timeSinceLastFrame = _timeSinceLastFrame % secondsPerFrame;
							_canOutputFrame = true;
						}
						else
						{
							_timeSinceLastFrame = 0f;
							_canOutputFrame = true;
						}
					}
					else
					{
						_canOutputFrame = false;
					}
				}
			}

			return _canOutputFrame;
		}

		private void RegisterAudioOutput()
		{
			if (_audioOutputManager != null)
			{
				_audioOutputManager.RegisterDevice(_device.DeviceIndex);
			}
		}

		private void AssignCameraTargetTexture(Camera camera, PerEye eye)
		{
			if(camera != null)
			{
				// RJT NOTE: Also check to make sure the existing texture isn't already the one we're trying to assign
				if ((camera.targetTexture != null) && (camera.targetTexture != eye.inputTexture))
				{
					Debug.LogWarning("[AVProDeckLink] DeckLinkOutput camera already has a targetTexture set, you may have to assign another camera");
				}
				camera.targetTexture = eye.inputTexture;
			}
		}

		private void AttachToCamera()
		{
			if (_camera == null)
			{
				_camera = gameObject.GetComponent<Camera>();
			}
			if (_camera != null)
			{
				AssignCameraTargetTexture(_camera, LeftEye);
				AssignCameraTargetTexture(_rightEyeCamera, RightEye);
			}
		}

		// Set the target update frame rate for Unity (dependent on multi-output flag)
		private void SetUpdateFrameRate(int targetFrameRate)
		{
			// RJT NOTE: I presume '_syncedToInput' is considered here so from initialisation Unity runs unbounded until we adjust to input rate
			// - I've removed for now as the very next update forced Unity to an adjusted '_targetFrameRate' anyhow and makes this function more generic
			if (!DeckLinkSettings.Instance._multiOutput)// && _syncedToInput == null)
			{
				Application.targetFrameRate = targetFrameRate;
#if !UNITY_2018_2_OR_NEWER
				Time.captureFramerate = targetFrameRate;
#endif
				_outputFrameRate = -1;

				// ADG? TODO: Perhaps when synced to input, the captureFrameRate should just be set to targetFrameRate?
/*				if (_syncedToInput != null)
				{
					Time.captureFramerate = _targetFrameRate;
				}*/
			}
			else
			{
				// RJT NOTE: Commented out as now handled at a higher level
//				Application.targetFrameRate = Time.captureFramerate = -1;
				_outputFrameRate = targetFrameRate;
			}
		}

		protected override void BeginDevice()
		{
			_currCapturedFrame = 0;
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			_device.GenlockOffset = _genlockPixelOffset;

			if (_current3DEnabled && !this.Device.OutputModes[_modeIndex].SupportStereo3D)
			{
				_current3DEnabled = false;
				Debug.LogWarning("[AVProDeckLink] Output mode does not support stereo mode " + _modeIndex + ", disabling");
			}

			if (_smpteLevel != SmpteLevel.Unspecified)
			{
				DeckLinkPlugin.SetOutputSmpteLevel(_device.DeviceIndex, (int)_smpteLevel);
			}
			DeckLinkPlugin.Set3DPlaybackEnabled(_device.DeviceIndex, _current3DEnabled);
			_device.LowLatencyMode = _lowLatencyMode;
			_device.EnableTimeCodeInput = _enableTimeCodeCapture;

			// Set keying mode before the output has started, as this can affect buffer allocation sizes
			if (_keyerMode != KeyerMode.None)
			{
				_device.CurrentKeyingMode = _keyerMode;
			}

			if (_audioChannels > _device.MaxAudioChannels)
			{
				Debug.LogWarning("[AVProDeckLink] Output audio channel count too high, clamping to  " + _device.MaxAudioChannels);
				_audioChannels = _device.MaxAudioChannels;	
			}

#if FEATURE_SYNCGROUPS_WIP
			_device.UseOutputSyncGroup = _useSyncGroup;
			_device.OutputSyncGroup = _syncGroupIndex;
#endif

			// Try starting output
			if (!_device.StartOutput(_modeIndex, new AudioSettings(_audioChannels, _audioBitDepth)))
			{
				Debug.LogError("[AVProDeckLink] Failed to start output device");
				StopOutput();
			}
			else
			{
				DeviceMode mode = _device.GetOutputMode(_modeIndex);

				RegisterAudioOutput();
				float framerate = mode.FrameRate;

				InitCaptureBlendResources(mode.Width, mode.Height);

				if (mode.InterlacedFieldMode)
				{
					// When in interlaced mode, we need to render 2 frames (top and bottom fields) to create a single frame
					// So for 60i, we need to run Unity at 60fps to generate the two frames, which are have their scanlines interleaved.
					// If there are input frames, they will come in at 30fps
					_interlaced = true;
					framerate *= 2f;
					InitInterlaceResources(mode.Width, mode.Height);
				}
				else
				{
					_interlaced = false;
				}

				InitConversionResources(mode.PixelFormat, mode.Width, mode.Height);

				_multiOutputCached = DeckLinkSettings.Instance._multiOutput;

				_targetFrameRate = Mathf.CeilToInt(framerate);
				SetUpdateFrameRate(_targetFrameRate);

				AttachToCamera();
			}
#endif
		}
		
		private void UnregisterAudioOutput()
		{
			if (_audioOutputManager != null)
			{
				if (_device != null)
				{
					_audioOutputManager.UnregisterDevice(_device.DeviceIndex);
				}
			}
		}

		public bool StopOutput()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			UnregisterAudioOutput();

			if (_device != null)
			{
				_device.StopOutput();
				_device = null;
			}

			_targetFrameRate = -1;
			if (DeckLinkManager.Instance != null)
			{
				_outputFrameRate = -1;
			}

			Application.targetFrameRate = Time.captureFramerate = -1;

			_interlaced = false;

			return true;
#else
			return false;
#endif
		}

		protected override void Cleanup()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			StopOutput();

			DeckLinkPlugin.SetOutputBufferPointer(_deviceIndex, null);
			DeckLinkPlugin.SetOutputTexturePointer(_deviceIndex, IntPtr.Zero);
			DeckLinkPlugin.SetRightOutputBufferPointer(_deviceIndex, null);
			DeckLinkPlugin.SetRightOutputTexturePointer(_deviceIndex, IntPtr.Zero);

			foreach (PerEye eye in _eye)
			{
				eye.Release();
			}

			if (_parameters != null)
			{
				_parameters.Release();
				_parameters = null;
			}

			if (_convertedCompBuffer != null)
			{
				_convertedCompBuffer.Release();
				_convertedCompBuffer = null;
			}

			if(_interlaceMaterial != null)
			{
				Destroy(_interlaceMaterial);
				_interlaceMaterial = null;
			}

			if(_conversionMaterial != null)
			{
				Destroy(_conversionMaterial);
				_conversionMaterial = null;
			}

			if(_blendMat != null)
			{
				Destroy(_blendMat);
				_blendMat = null;
			}
#endif
		}

		private void Convert(Texture inputTexture, RenderTexture convertedTexture, byte[] outputBuffer)
		{
			if (convertedTexture != null)
			{
				if (_conversionMaterial != null)
				{
					Graphics.Blit(inputTexture, convertedTexture, _conversionMaterial);
				}
				else
				{
					Graphics.Blit(inputTexture, convertedTexture);
				}
			}
			else if(_convertedCompBuffer != null)
			{
				if (_abgrTo10bitARGB == null)
				{
					Debug.LogError("[AVProDeckLink] Unable to find shader to covert ABGR to 10bit RGBA");
					return;
				}
				int kernelHandle = _abgrTo10bitARGB.FindKernel("RGBA_to_10RGBX");
				_abgrTo10bitARGB.SetTexture(kernelHandle, "input", inputTexture);
				_abgrTo10bitARGB.SetBuffer(kernelHandle, "result", _convertedCompBuffer);
				_abgrTo10bitARGB.SetBuffer(kernelHandle, "constBuffer", _parameters);
				_abgrTo10bitARGB.Dispatch(kernelHandle, inputTexture.width / 8, inputTexture.height / 8, 1);

				_convertedCompBuffer.GetData(outputBuffer);
			}
			else
			{
				Debug.Log("[AVPro DeckLink] Something really wrong happened, this path shouldn't be possible");
			}
		}

		private void RenderEyeWithoutCamera(PerEye eye)
		{
			if (eye.inputTexture != null)
			{
				if (eye.sourceTexture != null)
				{
					Graphics.Blit(eye.sourceTexture, eye.inputTexture);
				}
				else if(_noCameraMode == NoCameraMode.Colour)
				{
					var curr = RenderTexture.active;
					Graphics.SetRenderTarget(eye.inputTexture);
					GL.Clear(true, true, _defaultColour);
					Graphics.SetRenderTarget(curr);
				}
				else if(_noCameraMode == NoCameraMode.DefaultTexture)
				{
					Graphics.Blit(_defaultTexture != null ? _defaultTexture : Texture2D.blackTexture, eye.inputTexture);
				}
			}
		}

		private void CaptureFrame()
		{
			if(_camera == null)
			{
				RenderEyeWithoutCamera(LeftEye);
			}
			if (_rightEyeCamera == null)
			{
				RenderEyeWithoutCamera(RightEye);
			}

			if (LeftEye.capturedFrames != null)
			{
				for (int i = 0; i < 2; i ++)
				{
					PerEye eye = _eye[i];
					if (IsEyeUsed(i))
					{
						eye.capturedFrames[_currCapturedFrame].DiscardContents();
						Graphics.Blit(eye.inputTexture, eye.capturedFrames[_currCapturedFrame]);
					}
				}

				prevFrameTime = currFrameTime;
				currFrameTime = Time.unscaledTime;
				_currCapturedFrame = (byte)((_currCapturedFrame + 1) % 2);
			}
			else
			{
				for (int i = 0; i < 2; i ++)
				{
					PerEye eye = _eye[i];
					if (IsEyeUsed(i))
					{
						eye.blended.DiscardContents();
						Graphics.Blit(eye.inputTexture, eye.blended);
					}
				}
			}
		}

		private void ProcessAudio()
		{
			if (_audioOutputManager)
			{
				if (_muteOutputAudio)
				{
					_audioOutputManager.UnregisterDevice(_deviceIndex);
				}
				else
				{
					_audioOutputManager.RegisterDevice(_deviceIndex);
				}
			}
		}

		private void BlendCapturedFrames()
		{
			float timeSinceLastRenderedFrame = currFrameTime - prevFrameTime;

			float t = 1f - (timeSinceLastRenderedFrame == 0f ? 1f : _timeSinceLastFrame / timeSinceLastRenderedFrame);
			t = Mathf.Clamp01(t);

			_blendMat.SetFloat("_t", t);

			uint currTex = (_currCapturedFrame + 1U) % 2U;

			for (int i = 0; i < 2; i ++)
			{
				if (IsEyeUsed(i))
				{
					PerEye eye = _eye[i];
					_blendMat.SetTexture("_AfterTex", eye.capturedFrames[currTex]);
					Graphics.Blit(eye.capturedFrames[_currCapturedFrame], eye.blended, _blendMat);
				}
			}
		}

		private RenderTexture Interlace(RenderTexture inputTexture, bool isLastEyeRender)
		{
			if (_interlaced)
			{
				if(_interlacedTexture == null || _interlaceMaterial == null)
				{
					Debug.LogError("[AVPro DeckLink] Something went really wrong, I should not be here :(");
				}

				Graphics.Blit(inputTexture, _interlacedTexture, _interlaceMaterial, (int)_interlacePass);

				// Notify the plugin that the interlaced frame is complete now
				DeckLinkPlugin.SetInterlacedOutputFrameReady(_device.DeviceIndex, _interlacePass == InterlacePass.Second);

				// On the last eye render, switch to the alternative pass
				if (isLastEyeRender)
				{
					if (_interlacePass == InterlacePass.First)
					{
						_interlacePass = InterlacePass.Second;
						if (_syncedToInput != null && _syncedToInput.Device != null)
						{
							// Signal the next pass of the DeckLinkInput component not to update the texture
							_syncedToInput.Device.ForceNoInputFrameUpdate = true;
						}
					}
					else
					{
						_interlacePass = InterlacePass.First;
						if (_syncedToInput != null && _syncedToInput.Device != null)
						{
							_syncedToInput.Device.ForceNoInputFrameUpdate = false;
						}
					}
				}

				return _interlacedTexture;
			}

			return inputTexture;
		}

		// Dynamically adjust Unity's frame rate so that we're producing smooth output frames
		private void AdjustPlaybackFramerate()
		{	
			float delta = 1f;
			float deltaScale = 1f;
			float outputDelta = 0f;
			float inputDelta = 0f;

#if false	// Adjust based on buffered frames held by DeckLink
			{
				int numWaitingOutputFrames = DeckLinkPlugin.GetOutputBufferedFramesCount(_device.DeviceIndex);
				int minThreshold = Mathf.Max(1, _bufferBalance - 1);
				int maxThreshold = Mathf.Min(9, _bufferBalance + 1);
				if (numWaitingOutputFrames < minThreshold)
				{
					// If there are too few frames in the output buffer, we need to speed up Unity
					outputDelta += delta * (minThreshold - numWaitingOutputFrames) * deltaScale;
				}
				else if (numWaitingOutputFrames > maxThreshold)
				{
					// If there are too many frames in the output buffer, we need to slow down Unity
					outputDelta -= delta * (numWaitingOutputFrames - maxThreshold) * deltaScale;
				}
			}
#else	// Adjust based on output health (pre-roll length)
			// RJT NOTE: Health can also be reported back as > 1.0 which will also slow Unity down if sufficient
			outputDelta += ((1.0f - DeckLinkPlugin.GetHealthStatus(_device.DeviceIndex)) * _bufferBalance * deltaScale);
#endif
			// If we're syncing to input as well, also take into account this frame queue
			// NOTE: we don't run this in interlaced mode, as in this mode the render thread is running
			// at double the rate of the incoming frames, so the buffers should be fine, and adjusting
			// them causes more trouble.
			if (_syncedToInput != null && !_interlaced)
			{
				int totalBufferCount, readBufferCount, usedFrameCount, pendingFrameCount;
				if (Device.GetInputBufferStats(out totalBufferCount, out readBufferCount, out usedFrameCount, out pendingFrameCount))
				{
					int numWaitingInputFrames = pendingFrameCount;
					int bufferBalance = totalBufferCount / 2;
					int minThreshold = Mathf.Max(1, bufferBalance - 1);
					int maxThreshold = Mathf.Min(totalBufferCount - 1, bufferBalance + 1);
					if (numWaitingInputFrames < minThreshold)
					{
						// If we have too few input frames in the buffer, we need to slow down Unity
						inputDelta -= delta * (minThreshold - numWaitingInputFrames) * deltaScale;
					}
					else if (numWaitingInputFrames > maxThreshold)
					{
						// If we have too many input frames in the buffer, we need to speed up Unity
						inputDelta += delta * (numWaitingInputFrames - maxThreshold) * deltaScale;
					}
				}
			}

			float totalDelta = (inputDelta + outputDelta);
			int target = Mathf.CeilToInt(_targetFrameRate + totalDelta);
#if false
			if (!DeckLinkSettings.Instance._multiOutput)
			{
				Application.targetFrameRate = target;
	#if !UNITY_2018_2_OR_NEWER
				Time.captureFramerate = target;
	#endif

				// TODO: Perhaps when synced to input, the captureFrameRate should just be set to targetFrameRate?
/*				if (_syncedToInput != null)
				{
					Time.captureFramerate = _targetFrameRate;
				}*/
			}
			else
			{
				_outputFrameRate = target;
			}
#else
			SetUpdateFrameRate(target);
#endif
		}

		protected override void Process()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
/*			// RJT NOTE: Seems we're getting two 'Process()' calls (via 'DeckLink::Update()') before hitting 'WaitForEndOfFrame()'
			// - https://forum.unity.com/threads/yield-return-waitendofframe-will-wait-until-end-of-the-same-frame-or-the-next-frame.282213/
			// - So for now, skipping the first update..
			// - Remove as now covered eye 'convertedPointer' check below
			if( Time.frameCount <= 1 ) {  return; }*/

			if (_device == null)
			{
				return;
			}

			bool restartDevice = false;

			if(_current3DEnabled != _enable3D)
			{
				_current3DEnabled = _enable3D;
				restartDevice = true;
			}

			_device.LowLatencyMode = _lowLatencyMode;

			// If we're syncing to an input, then make sure we output using the same frame rate
			if (_syncedToInput != null && _syncedToInput.Device != null && _syncedToInput.Device.IsStreamingInput && _syncedToInput.ModeIndex >= 0)
			{
				// If we're using auto detect mode then we need to wait for frames to start coming in, otherwise
				// it may be still on the default mode awaiting auto detection
				if (!_syncedToInput._autoDetectMode || _syncedToInput.Device.InputFramesTotal > 1)
				{
					var inputMode = _syncedToInput.Device.CurrentMode;
					var outputMode = (Device == null) ? null : Device.CurrentOutputMode;

					// TODO: add better support for interlaced modes, 
					// currently it doesn't match the interlaced modes between input and output, and maybe it should?
					float inputFrameRate = inputMode.FrameRate;// * (inputMode.InterlacedFieldMode ? 2 : 1);

					if (outputMode == null || outputMode.FrameRate != inputFrameRate)
					{
						_filterModeByFPS = true;
						_filterModeByInterlacing = true;

						_modeFPS = inputFrameRate;
						_modeInterlacing = false;			// TODO: we're forcing progressive mode, this could be undesirable in some cases?

						restartDevice = true;
					}
				}
				else
				{
					return;
				}
			}

			if (restartDevice)
			{
				StopOutput();
				Begin(true);

				if(_device == null)
				{
					return;
				}
			}

			_enable3D = _current3DEnabled;

			if (_conversionMaterial != null)
			{
				//in this case, since we are dealing with non-srgb texture, need to do conversion from gamma to linear
				if (QualitySettings.activeColorSpace == ColorSpace.Linear && !_bypassGamma)
				{
					_conversionMaterial.EnableKeyword("APPLY_GAMMA");
				}
				else
				{
					_conversionMaterial.DisableKeyword("APPLY_GAMMA");
				}
				if (_ignoreAlphaChannel)
				{
					_conversionMaterial.EnableKeyword(ShaderKeyIgnoreAlpha);
				}
				else
				{
					_conversionMaterial.DisableKeyword(ShaderKeyIgnoreAlpha);
				}

				switch (_colorspaceMode)
				{
					case DeckLink.ColorspaceMode.Rec709:
						_conversionMaterial.DisableKeyword(ShaderKeyRec2020);
						_conversionMaterial.DisableKeyword(ShaderKeyRec2100);
						_conversionMaterial.EnableKeyword(ShaderKeyRec709);
						break;
					case DeckLink.ColorspaceMode.Rec2020:
						_conversionMaterial.DisableKeyword(ShaderKeyRec709);
						_conversionMaterial.DisableKeyword(ShaderKeyRec2100);
						_conversionMaterial.EnableKeyword(ShaderKeyRec2020);
						break;
					case DeckLink.ColorspaceMode.Rec2100:
						_conversionMaterial.DisableKeyword(ShaderKeyRec709);
						_conversionMaterial.DisableKeyword(ShaderKeyRec2020);
						_conversionMaterial.EnableKeyword(ShaderKeyRec2100);
						break;
				}
			}

			// Colour space changed?
			if (_device.OutputColorspaceMode != _colorspaceMode)
			{
				_device.OutputColorspaceMode = _colorspaceMode;
			}

			for (int i = 0; i < 2; i ++)
			{
				PerEye eye = _eye[i];
				if (eye.convertedTexture != null && eye.convertedPointer == IntPtr.Zero)
				{
					eye.convertedPointer = eye.convertedTexture.GetNativeTexturePtr();
					if (i == 0) { DeckLinkPlugin.SetOutputTexturePointer(_deviceIndex, eye.convertedPointer); }
					else { DeckLinkPlugin.SetRightOutputTexturePointer(_deviceIndex, eye.convertedPointer); }
				}
			}

			// Handle multi-output change
			if (_multiOutputCached != DeckLinkSettings.Instance._multiOutput)
			{
				_multiOutputCached = DeckLinkSettings.Instance._multiOutput;
				SetUpdateFrameRate(_targetFrameRate);
			}

			if (_device.IsStreamingOutput)
			{
				CaptureFrame();

				if (CanOutputFrame())
				{
					/*if(_syncedToInput)
					{
						_syncedToInput.UnsetInputReceivedFlag = true;
					}*/

					if (/*DeckLinkSettings.Instance._multiOutput*/(LeftEye.capturedFrames != null) || (RightEye.capturedFrames!=null))
					{
						BlendCapturedFrames();
					}

					RenderTexture input = LeftEye.blended;
					input = Interlace(input, !_current3DEnabled);
					if (!_interlaced || _interlacePass == InterlacePass.First)
					{
						Convert(input, LeftEye.convertedTexture, LeftEye.outputBuffer);

						if(_current3DEnabled)
						{
							input = RightEye.blended;
							input = Interlace(input, true);
							Convert(input, RightEye.convertedTexture, RightEye.outputBuffer);
						}

						AdjustPlaybackFramerate();

						// RJT NOTE: It takes a frame or two for our converted texture(s) to be created so
						// make sure they exist before allowing GPU reads to keep counters/timecodes in sync
						if (_eye[0].convertedPointer != IntPtr.Zero)
						{
							_device.UpdateOutputFPS(true);

	#if true	// Timecode output
							// Determine current timecode
							// RJT TODO: Option for user to specify their own timecode?
							// - https://github.com/RenderHeads/UnityPlugin-AVProDeckLink/issues/3
							// RJT NOTE: If synching to input then read its timecode instead
							// - This is working on the assumption Unity updates have also been synced to the input feed
							// - For completeness it would be good to have a separate timecode held by Unity which by
							//   the process of input/output synching would yield the same timecode as input, but it would
							//   need to remain separate as we can't override Unity's 'Time.realtimeSinceStartup' etc..
							FrameTimeCode _timecode = _device.GetFrameTimeCode();
							if(_syncedToInput && _syncedToInput.Device.EnableTimeCodeInput)
							{
								_timecode = _syncedToInput.Device.GetFrameTimeCode();
							}

							// Send to native to attach to next frame
							// RJT TODO: Ideally we'd pass this down as metadata in the Unity/D3D texture but not found a good way, yet..
							uint timecode = (((uint)_timecode.hours << 24) | ((uint)_timecode.minutes << 16) | ((uint)_timecode.seconds << 8) | (uint)_timecode.frames);
							DeckLinkPlugin.SetOutputTimeCode(_deviceIndex, timecode);
	#endif
							DeckLinkPlugin.SetDeviceOutputReady(_deviceIndex);
						}
					}
				}
			}

			ProcessAudio();
#endif
		}

		protected override bool IsInput()
		{
			return false;
		}

		public override void OnDestroy()
		{
			// RJT TODO: There are instances where 'DeckLinkManager' has been destroyed by this point
			// so having to check, which is safe (as it will have removed its own output device list),
			// but not clean..
			if (DeckLinkManager.Instance)
			{
				DeckLinkManager.Instance.RemoveOutputDevice(this);
			}

			refCount--;

			if(refCount == 0)
			{
				QualitySettings.vSyncCount = prevRefCount;
			}

			if (_convertedCompBuffer != null)
			{
				_convertedCompBuffer.Release();
				_convertedCompBuffer = null;
			}

			if (_parameters != null)
			{
				_parameters.Release();
				_parameters = null;
			}

			base.OnDestroy();
		}

#if UNITY_EDITOR
		[ContextMenu("Save Output PNG")]
		public override void SavePNG()
		{
			if (RightEye.inputTexture != null)
			{
				Helper.SavePNG("Image-Output-Left.png", LeftEye.inputTexture);
				Helper.SavePNG("Image-Output-Right.png", RightEye.inputTexture);
				Helper.SavePNG("Image-Output-Converted-Left.png", LeftEye.convertedTexture);
				Helper.SavePNG("Image-Output-Converted-Right.png", RightEye.convertedTexture);
			}
			else
			{
				Helper.SavePNG("Image-Output.png", LeftEye.inputTexture);
				Helper.SavePNG("Image-Output-Converted.png", LeftEye.convertedTexture);
			}
		}

#if UNITY_5_6_OR_NEWER
		[ContextMenu("Save Output EXR")]
		public override void SaveEXR()
		{
			if (RightEye.inputTexture != null)
			{
				Helper.SaveEXR("Image-Output-Left.exr", LeftEye.inputTexture);
				Helper.SaveEXR("Image-Output-Right.exr", RightEye.inputTexture);
				Helper.SaveEXR("Image-Output-Converted-Left.exr", LeftEye.convertedTexture);
				Helper.SaveEXR("Image-Output-Converted-Right.exr", RightEye.convertedTexture);
			}
			else
			{
				Helper.SaveEXR("Image-Output.exr", LeftEye.inputTexture);
				Helper.SaveEXR("Image-Output-Converted.exr", LeftEye.convertedTexture);
			}
		}
#endif
#endif

	}
}
