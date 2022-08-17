#if UNITY_4_1 || UNITY_4_2 || UNITY_4_3 || UNITY_4_4 || UNITY_4_5 || UNITY_4_6 || UNITY_4_7 || UNITY_5 || UNITY_5_4_OR_NEWER
	#define AVPRODECKLINK_UNITYFEATURE_NONPOW2TEXTURES
#endif

using UnityEngine;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

//-----------------------------------------------------------------------------
// Copyright 2014-2019 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProDeckLink
{
	public class FrameTimeCode
	{
		public byte hours;
		public byte minutes;
		public byte seconds;
		public byte frames;
	}

	public class AudioSettings
	{
		public AudioSettings()
		{
			this.channelCount = AudioChannels.None;
			this.bitDepth = AudioBitDepth.Sixteen;
		}

		public AudioSettings(AudioChannels channelCount, AudioBitDepth bitDepth)
		{
			this.channelCount = channelCount;
			this.bitDepth = bitDepth;
		}

		public AudioChannels channelCount = AudioChannels.None;
		public AudioBitDepth bitDepth = AudioBitDepth.Sixteen;
	}

	[System.Serializable]
	public class Device : System.IDisposable
	{
		private const int MaxWidth = 8192;
		private const int MaxHeight = 4320;
		private const int MaxAncDataBytesPerFrame = 8192;
		private const int MaxAncPacketsPerFrame = (MaxAncDataBytesPerFrame / 256);

		private AncPacket[] _ancPackets = new AncPacket[MaxAncPacketsPerFrame];
		private int _ancPacketCount;
		private GCHandle _ancPacketsBufferHandle;
		private System.IntPtr _ancPacketsBufferPtr;

		private byte[] _ancData = new byte[MaxAncDataBytesPerFrame];
		private int _ancDataByteCount;
		private GCHandle _ancDataBufferHandle;
		private System.IntPtr _ancDataBufferPtr;
		private long _ancFrameTimeStamp = -1;

		private FrameTimeCode _timeCode = new FrameTimeCode();
		private long _timeCodeTimeStamp = -1;
		private float _outputTimestampSec = 0.0f;

		private int _deviceIndex;
		private string _name;
		private string _modelName;
		private DeviceProfile[] _deviceProfiles;
		private List<DeviceMode> _inputModes;
		private List<DeviceMode> _outputModes;
		private bool _supportsInputModeAutoDetection;
		private bool _supportsInternalKeying;
		private bool _supportsExternalKeying;
		private bool _supportsConfigurableDuplex;
#if FEATURE_SYNCGROUPS_WIP
		private bool _supportsInputSyncGroups;
		private bool _supportsOutputSyncGroups;
#endif
		private AudioChannels _maxSupportedAudioChannels = AudioChannels.Count16;
		private FormatConverter _formatConverter;
		private DeviceMode _currentMode;
		private DeviceMode _currentOutputMode = null;

		// FPS counters
		private int _inputFrameCount;
		private float _inputStartFrameTime;
		private int _outputFrameCount;
		private float _outputStartFrameTime;

		private bool _isActive = false;
		private DeckLinkOutput.KeyerMode _keyingMode;
		private bool _autoDeinterlace = false;
		private bool _receivedSignal = false;
		private bool _supportsFullFrameGenlockOffset;
		private int _genlockOffset;
		private AudioSettings _inputAudioSettings = new AudioSettings();
		private AudioSettings _outputAudioSettings = new AudioSettings();

		private bool _isStreamingOutput = false;
		private bool _isStreamingInput = false;

		private bool _fullDuplexSupported = false;
		private bool _lowLatencyMode = false;

#if FEATURE_SYNCGROUPS_WIP
		private bool _useInputSyncGroup = false;
		public bool UseInputSyncGroup
		{
			get { return _useInputSyncGroup; }
			set { _useInputSyncGroup = value; }
		}

		private int _inputSyncGroup = 0;
		public int InputSyncGroup
		{
			get { return _inputSyncGroup; }
			set { _inputSyncGroup = value; }
		}

		private bool _useOutputSyncGroup = false;
		public bool UseOutputSyncGroup
		{
			get { return _useOutputSyncGroup; }
			set { _useOutputSyncGroup = value; }
		}

		private int _outputSyncGroup = 0;
		public int OutputSyncGroup
		{
			get { return _outputSyncGroup; }
			set { _outputSyncGroup = value; }
		}
#endif

		public DeviceProfile[] DeviceProfiles
		{
			get { return _deviceProfiles; }
		}

		public List<DeviceMode> InputModes
		{
			get { return _inputModes; }
		}

		public AudioChannels InputAudioChannels
		{
			get { return _inputAudioSettings.channelCount; }
		}

		public AudioChannels OutputAudioChannels
		{
			get { return _outputAudioSettings.channelCount; }
		}

		public AudioBitDepth InputAudioBitDepth
		{
			get { return _inputAudioSettings.bitDepth; }
		}

		public AudioBitDepth OutputAudioBitDepth
		{
			get { return _outputAudioSettings.bitDepth; }
		}		

		public bool Enable3DInput
		{
			get { return _formatConverter == null ? false : _formatConverter.Enable3DInput; }
			set
			{
				if(_formatConverter != null)
				{
					_formatConverter.Enable3DInput = value;
				}
			}
		}

		public bool EnableAncillaryDataInput { get; set; }

		public bool EnableTimeCodeInput { get; set; }

		public bool IgnoreAlphaChannel
		{
			get { return _formatConverter == null ? false : _formatConverter.IgnoreAlphaChannel; }
			set
			{
				if(_formatConverter != null)
				{
					_formatConverter.IgnoreAlphaChannel = value;
				}
			}
		}

		public bool BypassGammaCorrection
		{
			get { return _formatConverter == null ? false : _formatConverter.BypassGammaCorrection; }
			set
			{
				if(_formatConverter != null)
				{
					_formatConverter.BypassGammaCorrection = value;
				}
			}
		}

		public DeckLink.ColorspaceMode InputColorspaceMode
		{
			get { return _formatConverter == null ? DeckLink.ColorspaceMode.Rec709 : _formatConverter.ColorspaceMode; }
			set
			{
				if (_formatConverter != null)
				{
					_formatConverter.ColorspaceMode = value;
				}
			}
		}

		private DeckLink.ColorspaceMode _outputColorspaceMode = (DeckLink.ColorspaceMode)(-1);
		public DeckLink.ColorspaceMode OutputColorspaceMode
		{
			get { return _outputColorspaceMode; }
			set
			{
				// RJT TODO: Translating to native enum as is, would be good to homogenise!
/*				enum ColorSpace
				{
					Unknown,
					Rec601,
					Rec709,
					Rec2020,
				};*/
/*				enum class TransferFunction
				{
					Unknown,
					SDR,
					HDR,
					PQ,
					HLG
				};*/

				_outputColorspaceMode = value;
				switch (_outputColorspaceMode)
				{
/*					case DeckLink.ColorspaceMode.Rec601:
						DeckLinkPlugin.SetOutputColourSpace(_deviceIndex, 1);
						DeckLinkPlugin.SetOutputTransferFunction(_deviceIndex, 1);
						break;*/
//					case DeckLink.ColorspaceMode.Rec709:
					default:								// SDR
						DeckLinkPlugin.SetOutputColourSpace(_deviceIndex, 2);
						DeckLinkPlugin.SetOutputTransferFunction(_deviceIndex, 1);
						break;
					case DeckLink.ColorspaceMode.Rec2020:	// HDR
						DeckLinkPlugin.SetOutputColourSpace(_deviceIndex, 3);
						DeckLinkPlugin.SetOutputTransferFunction(_deviceIndex, 2);
						break;
					case DeckLink.ColorspaceMode.Rec2100:	// HLG
						DeckLinkPlugin.SetOutputColourSpace(_deviceIndex, 3);
						DeckLinkPlugin.SetOutputTransferFunction(_deviceIndex, 4);
						break;
				}
			}
		}

		public List<DeviceMode> OutputModes
		{
			get { return _outputModes; }
		}

		public bool IsActive
		{
			get
			{
				return _isActive;
			}
			set
			{
				_isActive = value;
			}
		}

		public int DeviceIndex
		{
			get { return _deviceIndex; }
		}

		public string Name
		{
			get { return _name; }
		}

		public string ModelName
		{
			get { return _modelName; }
		}

		public DeviceProfile CurrentDeviceProfile
		{
			get { return DeckLinkPlugin.GetCurrentDeviceProfile(_deviceIndex); }
			set 
			{
				if (value != DeviceProfile.Unknown)
				{
					DeviceProfile currentProfile = CurrentDeviceProfile;
					if (currentProfile != value)
					{
						if (!IsStreamingInput && !IsStreamingOutput)
						{
							if (DeckLinkPlugin.SetCurrentDeviceProfile(_deviceIndex, value))
							{
								if (DeckLink.GetSubDeviceCount(currentProfile) != DeckLink.GetSubDeviceCount(value))
								{
									// The number of devices has changed, so we need to fully reenumerate
									DeckLinkManager.Instance.QueueReenumerate();
								}
								else if (DeckLink.IsFullDuplex(currentProfile) != DeckLink.IsFullDuplex(value))
								{
									// The profile has changed, so we need to reenumerate device features for the new profile
									EnumDeviceProfileFeatures();
								}
								else
								{
									Debug.LogError("[AVProDeckLink] Should never reach this condition changing from profile " + currentProfile + " to " + value);
								}
							}
							else
							{
								Debug.LogWarning("[AVProDeckLink] Failed to change to device profile " + value);
							}
						}
						else
						{
							Debug.LogError("[AVProDeckLink] Changing profile while device is running is not yet supported.  Stop streaming before changing profile.");
						}
					}
				}
			}
		}

		public bool SupportsInput
		{
			// This is dynamic as it can change based on the device profile
			get { return DeckLinkPlugin.SupportsInput(_deviceIndex); }
		}

		public bool SupportsOutput
		{
			// This is dynamic as it can change based on the device profile
			get { return DeckLinkPlugin.SupportsOutput(_deviceIndex); }
		}

		public bool IsInputBusy
		{
			// This is dynamic as it can change
			get { return DeckLinkPlugin.IsInputBusy(_deviceIndex); }
		}

		public bool IsOutputBusy
		{
			// This is dynamic as it can change
			get { return DeckLinkPlugin.IsOutputBusy(_deviceIndex); }
		}

		public int NumInputModes
		{
			get { return _inputModes.Count; }
		}

		public int NumOutputModes
		{
			get { return _outputModes.Count; }
		}

		public bool SupportsInputModeAutoDetection
		{
			get { return _supportsInputModeAutoDetection; }
		}

		public bool SupportsInternalKeying
		{
			get { return _supportsInternalKeying; }
		}

		public bool SupportsExternalKeying
		{
			get { return _supportsExternalKeying; }
		}

		public AudioChannels MaxAudioChannels
		{
			get { return _maxSupportedAudioChannels; }
		}

		public bool FlipInputX
		{
			get { return _formatConverter == null ? false : _formatConverter.FlipX; }
			set {
				if (_formatConverter != null)
				{
					_formatConverter.FlipX = value;
				}
			}
		}

		public bool FlipInputY
		{
			get { return _formatConverter == null ? false : _formatConverter.FlipY; }
			set
			{
				if (_formatConverter != null)
				{
					_formatConverter.FlipY = value;
				}
			}
		}

		public bool ForceNoInputFrameUpdate { get; set; }

		public FrameTimeCode GetFrameTimeCode()
		{
			return _timeCode;
		}

		public Texture OutputTexture
		{
			get { if (_formatConverter != null && _formatConverter.ValidPicture) return _formatConverter.OutputTexture; return null; }
		}

		public Texture RightOutputTexture
		{
			get
			{
				if(_formatConverter != null && _formatConverter.Enable3DInput && _formatConverter.ValidPicture)
				{
					return _formatConverter.RightEyeOutputTexture;
				}

				return null;
			}
		}

		public ulong OutputFrameNumber
		{
			get { if (_formatConverter != null && _formatConverter.ValidPicture) return (ulong)_formatConverter.OutputFrameNumber; return 0; }
		}

		public DeviceMode CurrentMode
		{
			get { return _currentMode; }
		}

		public DeviceMode CurrentOutputMode
		{
			get { return _currentOutputMode; }
		}

		public bool IsStreaming
		{
			get;
			private set;
		}

		public bool IsStreamingOutput{
			get
			{
				return _isStreamingOutput;
			}
		}

		public bool IsStreamingInput
		{
			get
			{
				return _isStreamingInput;
			}
		}

		public bool FullDuplexSupported
		{
			get
			{
				return _fullDuplexSupported;
			}
		}

		public int GenlockOffset
		{
			get { return _genlockOffset; }
			set { _genlockOffset = value; }
		}

		public bool IsStreamingAudio
		{
			get;
			private set;
		}

		public DeckLinkOutput.KeyerMode CurrentKeyingMode
		{
			get { return _keyingMode; }
			set { SetKeying(value); }
		}

		public bool IsPaused
		{
			get;
			private set;
		}

		public bool IsInputUseHdr
		{
			get;
			private set;
		}

		public bool IsConfigurableDuplex
		{
			get { return _supportsConfigurableDuplex; }
		}

		public bool IsPicture
		{
			get;
			private set;
		}

		public float InputFPS
		{
			get;
			private set;
		}

		public float OutputFPS
		{
			get;
			private set;
		}

		public int InputFramesTotal
		{
			get;
			private set;
		}

		public int OutputFramesTotal
		{
			get;
			private set;
		}

		public bool AutoDeinterlace
		{
			get { return _autoDeinterlace; }
			set { _autoDeinterlace = value; if (_formatConverter != null) _formatConverter.AutoDeinterlace = _autoDeinterlace; }
		}

		public bool ReceivedSignal
		{
			get { return _receivedSignal; }
		}

		public bool IsGenLocked
		{
			get { return DeckLinkPlugin.IsGenLocked(_deviceIndex); }
		}

		public bool SupportsFullFrameGenlockOffset
		{
			get { return _supportsFullFrameGenlockOffset; }
		}

		public SmpteLevel OutputSmpteLevel
		{
			get { return (SmpteLevel)DeckLinkPlugin.GetOutputSmpteLevel(_deviceIndex); }
		}

		public bool LowLatencyMode
		{
			get { return _lowLatencyMode; }
			set
			{
				if(_lowLatencyMode != value)
				{
					_lowLatencyMode = value;
					DeckLinkPlugin.SetLowLatencyMode(_deviceIndex, _lowLatencyMode);
				}
			}
		}

		public void SetInputBufferSizes(int bufferCount, int readBufferCount)
		{
			DeckLinkPlugin.ConfigureInputBuffer(_deviceIndex, bufferCount, readBufferCount);
		}

		public bool GetInputBufferStats(out int totalBufferCount, out int readBufferCount, out int usedFrameCount, out int pendingFrameCount)
		{
			return DeckLinkPlugin.GetInputBufferStats(_deviceIndex, out totalBufferCount, out readBufferCount, out usedFrameCount, out pendingFrameCount);
		}

		public uint DroppedInputSignalCount
		{
			get; private set;
		}

		public float HealthStatus
		{
			get; private set;
		}

		public Device(string modelName, string name, int index)
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			IsStreamingAudio = false;
			IsStreaming = false;
			IsPaused = true;
			IsPicture = false;
			_name = name;
			_modelName = modelName;
			_deviceIndex = index;
			_genlockOffset = 0;
			_formatConverter = new FormatConverter();
			_deviceProfiles = new DeviceProfile[DeckLinkPlugin.GetDeviceProfileCount(_deviceIndex)];
			for (int i = 0; i < _deviceProfiles.Length ; i++)
			{
				_deviceProfiles[i] = DeckLinkPlugin.GetDeviceProfile(_deviceIndex, i);
			}

			_ancPacketsBufferHandle = GCHandle.Alloc(_ancPackets, GCHandleType.Pinned);
			_ancPacketsBufferPtr = _ancPacketsBufferHandle.AddrOfPinnedObject();
			_ancDataBufferHandle = GCHandle.Alloc(_ancData, GCHandleType.Pinned);
			_ancDataBufferPtr = _ancDataBufferHandle.AddrOfPinnedObject();

#if AVPRODECKLINK_UNITYFEATURE_NONPOW2TEXTURES
#if !UNITY_2019_1_OR_NEWER
			DeckLinkPlugin.SetPotTextures(_deviceIndex, SystemInfo.npotSupport == NPOTSupport.None);
#else
			DeckLinkPlugin.SetPotTextures(_deviceIndex, false);
#endif
			DeckLinkPlugin.SetGammaSpace(_deviceIndex, QualitySettings.activeColorSpace == ColorSpace.Gamma);
#endif
#endif

			EnumDeviceProfileFeatures();
		}

		internal void EnumDeviceProfileFeatures()
		{
			_supportsInputModeAutoDetection = DeckLinkPlugin.SupportsInputModeAutoDetection(_deviceIndex);
			_supportsInternalKeying = DeckLinkPlugin.SupportsInternalKeying(_deviceIndex);
			_supportsExternalKeying = DeckLinkPlugin.SupportsExternalKeying(_deviceIndex);
			_maxSupportedAudioChannels = DeckLinkPlugin.GetMaxSupportedAudioChannels(_deviceIndex);
			_supportsFullFrameGenlockOffset = DeckLinkPlugin.SupportsFullFrameGenlockOffset(_deviceIndex);
			_supportsConfigurableDuplex = DeckLinkPlugin.ConfigurableDuplexMode(_deviceIndex);
			_fullDuplexSupported = DeckLinkPlugin.FullDuplexSupported(_deviceIndex);
#if FEATURE_SYNCGROUPS_WIP
			_supportsInputSyncGroups = DeckLinkPlugin.SupportsInputSyncGroups(_deviceIndex);
			_supportsOutputSyncGroups = DeckLinkPlugin.SupportsOutputSyncGroups(_deviceIndex);
#endif

			_inputModes = new List<DeviceMode>(1024);
			_outputModes = new List<DeviceMode>(1024);
			EnumModes();
		}

		public void Dispose()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

			// Free the pinned buffer
			if (_ancPacketsBufferPtr != System.IntPtr.Zero)
			{
				_ancPacketsBufferHandle.Free();
				_ancPacketsBufferPtr = System.IntPtr.Zero;
			}
			// Free the pinned buffer
			if (_ancDataBufferPtr != System.IntPtr.Zero)
			{
				_ancDataBufferHandle.Free();
				_ancDataBufferPtr = System.IntPtr.Zero;
			}

			if (_formatConverter != null)
			{
				_formatConverter.Dispose();
				_formatConverter = null;
			}
#endif
		}

		public bool StartInput(DeviceMode mode, AudioSettings audioSettings, bool delayResourceCreationUntilFramesStart = false)
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			if (mode != null)
			{
				return StartInput(mode.Index, audioSettings, delayResourceCreationUntilFramesStart);
			}
#endif
			return false;
		}

		public bool StartOutput(DeviceMode mode, AudioSettings audioSettings)
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			if (!SupportsOutput)
			{
				Debug.LogWarning("[AVProDeckLink] Warning: Unable to start output for device " + _name + " as it doesn't supports outputing");
				return false;
			}
			if (IsOutputBusy || IsInputBusy)
			{
				Debug.LogWarning("[AVProDeckLink] Warning: Unable to start output for device " + _name + " as it is currently busy");
				return false;
			}
			
			if (mode != null)
				return StartOutput(mode.Index, audioSettings);
#endif
			return false;
		}

		public bool StartInput(int modeIndex, AudioSettings audioSettings, bool delayResourceCreationUntilFramesStart = false, bool useHdr = false)
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			bool result = false;

			if (modeIndex == -1)
			{
				return false;
			}

			if (!SupportsInput)
			{
				Debug.LogWarning("[AVProDeckLink] Warning: Unable to start input for device " + _name + " as it doesn't support inputing");
				return false;
			}

			if (IsInputBusy || IsOutputBusy)
			{
				Debug.LogWarning("[AVProDeckLink] Warning: Unable to start input for device " + _name + " as it is currently busy");
				return false;
			}

			if (_formatConverter.Enable3DInput && !this.InputModes[modeIndex].SupportStereo3D)
			{
				_formatConverter.Enable3DInput = false;
				Debug.LogWarning("[AVProDeckLink] Input mode does not support stereo mode " + modeIndex + ", disabling");
			}

			DeckLinkPlugin.Set3DCaptureEnabled(_deviceIndex, _formatConverter.Enable3DInput);
			DeckLinkPlugin.SetAncillaryDataCaptureEnabled(_deviceIndex, EnableAncillaryDataInput);
			DeckLinkPlugin.SetTimeCodeCaptureEnabled(_deviceIndex, EnableTimeCodeInput);

#if FEATURE_SYNCGROUPS_WIP
			int syncGroupIndex = -1;
			if (_useInputSyncGroup)
			{
				if (_supportsInputSyncGroups)
				{
					syncGroupIndex = _inputSyncGroup;
				}
				else
				{
					Debug.LogWarning("[AVProDeckLink] Input sync group is not supported on this device.");
				}
			}
#endif

			if (DeckLinkPlugin.StartInputStream(_deviceIndex, modeIndex, audioSettings.channelCount, audioSettings.bitDepth
#if FEATURE_SYNCGROUPS_WIP
			, syncGroupIndex
#endif
			))
			{
				_inputAudioSettings = audioSettings;
				_currentMode = _inputModes[modeIndex];
				result = true;
				IsActive = true;
				IsStreaming = true;
				IsPicture = false;
				IsPaused = false;
				IsInputUseHdr = useHdr;
				DroppedInputSignalCount = 0;
				result = true;
				_isStreamingInput = true;
				ResetInputFPS();

				if (_currentMode.Width > 0 && _currentMode.Width <= MaxWidth && _currentMode.Height > 0 && _currentMode.Height <= MaxHeight)
				{
					_formatConverter.AutoDeinterlace = _autoDeinterlace;
					_formatConverter.Build(_deviceIndex, _currentMode, delayResourceCreationUntilFramesStart, useHdr);
				}
				else
				{
					Debug.LogWarning("[AVProDeckLink] Invalid width or height");
				}
			}
			else
			{
				Debug.LogWarning("[AVProDeckLink] Unable to start input stream on device " + _name);
			}

			if (!result)
			{
				StopInput();
			}

			return result;
#else
			return false;
#endif
		}

		/// <summary>
		/// This is used by the dynamic automatic input detection
		/// </summary>
		private bool ChangeInput(int modeIndex, bool allowRetry)
		{
			bool result = false;
			if (_isStreamingInput)
			{
				DeviceMode newMode = _inputModes[modeIndex];
				Debug.Log("[AVProDeckLink] Changing device '" + this._name + "' input " + newMode.Width + "x" + newMode.Height + " (" + newMode.ModeDescription + " " + newMode.PixelFormatDescription + ")");

				if (_formatConverter.Build(_deviceIndex, newMode, false, IsInputUseHdr))
				{
					_currentMode = newMode;
					result = true;
				}
				else
				{
					Debug.LogWarning("[AVProDeckLink] Failed to build FormatConverter for new mode");
				}
			}

			if (!result && !allowRetry)
			{
				Debug.LogWarning("[AVProDeckLink] unable to change input device mode");
				StopInput();
			}

			return result;
		}

		/*public bool StartAudioOutput(AudioChannels numChannels)
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			if (IsStreamingAudio)
			{
				StopAudio();
			}

			if (numChannels > 0 && numChannels <= MaxAudioChannels)
			{
				IsStreamingAudio = DeckLinkPlugin.StartAudioOutput(_deviceIndex, numChannels);
			}
			else
			{
				Debug.LogError("Unsupported number of audio channels " + numChannels + " vs " + MaxAudioChannels);
			}

			return IsStreamingAudio;
#else
			return false;
#endif
		}*/

		public bool StartOutput(int modeIndex, AudioSettings audioSettings)
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			bool result = false;
			
			if (_isStreamingOutput)
			{
				Debug.Log("Warning: Please stop device before starting new stream");
				return false;
			}

			if (_formatConverter.Enable3DInput && !this.InputModes[modeIndex].SupportStereo3D)
			{
				_formatConverter.Enable3DInput = false;
				Debug.LogWarning("[AVProDeckLink] Input mode does not support stereo mode " + modeIndex + ", disabling");
			}

			DeckLinkPlugin.SetGenlockOffset(_deviceIndex, _genlockOffset);

#if FEATURE_SYNCGROUPS_WIP
			int syncGroupIndex = -1;
			if (_useOutputSyncGroup)
			{
				if (_supportsOutputSyncGroups)
				{
					syncGroupIndex = _outputSyncGroup;
				}
				else
				{
					Debug.LogWarning("[AVProDeckLink] Output sync group is not supported on this device.");
				}
			}
#endif

			if (DeckLinkPlugin.StartOutputStream(_deviceIndex, modeIndex, audioSettings.channelCount, audioSettings.bitDepth
#if FEATURE_SYNCGROUPS_WIP
			, syncGroupIndex
#endif
			))
			{
				//_currentOutputMode = _outputModes[modeIndex];
				_currentOutputMode = _outputModes[modeIndex];

				if (_currentOutputMode.Width > 0 && _currentOutputMode.Width <= MaxWidth && _currentOutputMode.Height > 0 && _currentOutputMode.Height <= MaxHeight)
				{
					ResetOutputFPS();
					_outputAudioSettings = audioSettings;
					IsActive = true;
					IsStreaming = true;
					_isStreamingOutput = true;
					IsPicture = false;
					IsPaused = false;
					result = true;
				}
				else
				{
					Debug.LogWarning("[AVProDeckLink] Invalid width or height");
				}
			}

			/*if(!DeckLinkPlugin.StartAudioOutput(_deviceIndex, 2))
			{
				Debug.LogWarning("[AVProDeckLink] Unable to start audio output stream");
			}*/

			if (!result)
			{
				Debug.LogWarning("[AVProDeckLink] Unable to start output device");
				if (modeIndex < 0)
				{
					Debug.LogWarning("[AVProDeckLink] Possible that output resolution or pixel format isn't available");
				}
				StopOutput();
			}

			return result;
#else
			return false;
#endif
		}

		public void Pause()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			if (_isStreamingInput)
			{
				DeckLinkPlugin.Pause(_deviceIndex);
				IsPaused = true;
			}
#endif
		}

		public void Unpause()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			if (_isStreamingInput)
			{
				DeckLinkPlugin.Unpause(_deviceIndex);
				IsPaused = false;
			}
#endif
		}

		public bool CanInput()
		{
//			bool result = SupportsInput && !IsOutputBusy && !IsInputBusy;
			bool result = SupportsInput && !IsInputBusy && (_fullDuplexSupported || !IsOutputBusy);

			/*if (result)
			{
				if (_fullDuplexSupported)
				{
					result = !IsStreamingInput;
				}
				else
				{
					result = !IsStreaming;
				}
			}*/
			return result;
		}

		public bool CanOutput()
		{
//			bool result = SupportsOutput && !IsOutputBusy && !IsInputBusy;
			bool result = SupportsOutput && !IsOutputBusy && (_fullDuplexSupported || !IsInputBusy);

			/*if (result)
			{
				if (_fullDuplexSupported)
				{
					result = !IsStreamingOutput;
				}
				else
				{
					result = !IsStreaming;
				}
			}*/
			return result;
		}

		public void Update()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			if (_isStreamingInput)
			{
				// Check if the input mode has changed
				if (DeckLinkPlugin.IsNoInputSignal(_deviceIndex))
				{
					_receivedSignal = false;
					DroppedInputSignalCount++;
				return;
				}
				else
				{
					_receivedSignal = true;
				}

				int modeIndex = DeckLinkPlugin.GetVideoInputModeIndex(_deviceIndex);
				if (modeIndex != _currentMode.Index)
				{
					var newMode = _inputModes[modeIndex];

					if (!FormatConverter.InputFormatSupported(newMode.PixelFormat))
					{
						Debug.LogWarning(string.Format("Auto detected format {0} for input device not currently supported", newMode.PixelFormat));
					}

					if (modeIndex >= 0 && modeIndex < _inputModes.Count)
					{
						// If the device has changed mode we may need to rebuild buffers
						ChangeInput(modeIndex, true);
						return;
					}
				}

				// ForceNoInputFrame can be set during an interlaced 2-pass, where we don't want the
				// input texture to update during the second pass.
				if (!ForceNoInputFrameUpdate)
				{
					if (EnableAncillaryDataInput)
					{
						UpdateAncillaryData();
					}
					if (EnableTimeCodeInput)
					{
						UpdateTimeCode();
					}

					// Update textures
					if (_formatConverter != null)
					{
						UpdateInputFPS(_formatConverter.Update());
					}
				}
			}
#endif
			// Read latest health
			HealthStatus = DeckLinkPlugin.GetHealthStatus(_deviceIndex);
		}

		private void UpdateAncillaryData()
		{
			int ancPacketCount = _ancPackets.Length;
			int ancDataByteCount = _ancData.Length;
			if (DeckLinkPlugin.GetLastFrameAncillaryData(_deviceIndex, ref _ancFrameTimeStamp, _ancPacketsBufferPtr, ref ancPacketCount, _ancDataBufferPtr, ref ancDataByteCount))
			{
				_ancPacketCount = ancPacketCount;
				_ancDataByteCount = ancDataByteCount;

				//DebugAncillaryData();
				//ParseAncillaryData();
			}
		}

		private void UpdateTimeCode()
		{
			uint timeCode = 0;
			if (DeckLinkPlugin.GetLastFrameTimeCode(_deviceIndex, ref _timeCodeTimeStamp, ref timeCode))
			{
				_timeCode.hours = (byte)((timeCode >> 24) & 0xff);
				_timeCode.minutes = (byte)((timeCode >> 16) & 0xff);
				_timeCode.seconds = (byte)((timeCode >> 8) & 0xff);
				_timeCode.frames = (byte)(timeCode & 0xff);
			}
		}

		private void DebugAncillaryData()
		{
			Debug.Log("New Ancillary data retrieved at time " + _ancFrameTimeStamp + ": " + _ancPacketCount + " packets with " + _ancDataByteCount + " bytes of data");
			for (int i = 0; i < _ancPacketCount; i++)
			{
				Debug.Log("Packet " + i);
				AncPacket packet = _ancPackets[i];
				Debug.Log("ID1: " + packet.dataId1 + " ID2: " + packet.dataId2 + " StreamIndex: " + packet.dataStreamIndex + " frameLine: " + packet.frameLine + " dataOffset: " + packet.dataByteOffset + " dataBytes: " + packet.dataByteCount);
				for (int j = 0; j < packet.dataByteCount; j++)
				{
					Debug.Log("byte " + j + ": "+ _ancData[packet.dataByteOffset + j] + " 0x" + _ancData[packet.dataByteOffset + j].ToString("X2"));
				}
			}

			System.IO.File.WriteAllBytes("ancdata.bin", _ancData);
		}

		[StructLayout(LayoutKind.Explicit, Pack = 1)]
		public struct ARRI
		{
			[FieldOffset(0)] public int reserved;
			[FieldOffset(4)] public int ltc;
			[FieldOffset(8)] public int userBits;
			[FieldOffset(12)] public int recRunTcStart;
			[FieldOffset(16)] public int recRunTcStartOffset;
			[FieldOffset(20)] public int reserved2;
			[FieldOffset(24)] public int sensorFps;
			[FieldOffset(28)] public int shutterAngle;
			[FieldOffset(32)] public int reserved3;
			[FieldOffset(80)] public int exposureTime;
		}

		private void ParseAncillaryData()
		{
			for (int i = 0; i < _ancPacketCount; i++)
			{
				AncPacket packet = _ancPackets[i];
				if (packet.dataId1 == 0x60 &&
					packet.dataId2 == 0x60)
					{
						Debug.Log(i + " Found SMPTE 12M-2 timecode");
					}
				else if (packet.dataId1 == 0x51 && 
						packet.dataId2 == 0x53)
				{
					Debug.Log(i + " Blackmagic SDI Camera remote control");
				}                    
				else if (packet.dataId1 == 0x44 && 
						packet.dataId2 == 0x04)
				{
					Debug.Log(i + " ARRI packet");
				}
				else
				{
					Debug.Log(i + " Unknown packet " + packet.dataId1 + " " + packet.dataId2 + " " + packet.dataByteCount);
				}
			}
		}

		public void SetDuplexMode(bool isFull)
		{
			DeckLinkPlugin.SetDuplexMode(_deviceIndex, isFull);
		}

		protected void ResetInputFPS()
		{
			_inputFrameCount = 0;
			InputFramesTotal = 0;
			InputFPS = 0.0f;
			_inputStartFrameTime = 0.0f;
		}

		protected void ResetOutputFPS()
		{
			_outputFrameCount = 0;
			OutputFramesTotal = 0;
			OutputFPS = 0.0f;
			_outputStartFrameTime = 0.0f;

			_outputTimestampSec = 0.0f;
		}

		public void UpdateInputFPS(bool newFrame)
		{
			if (newFrame)
			{
				_inputFrameCount++;
				InputFramesTotal++;
			}

			float timeNow = Time.realtimeSinceStartup;
			float timeDelta = timeNow - _inputStartFrameTime;
			if (timeDelta >= 0.5f)
			{
				InputFPS = (float)_inputFrameCount / timeDelta;
				_inputFrameCount = 0;
				_inputStartFrameTime = timeNow;
			}
		}

		public void UpdateOutputFPS(bool newFrame)
		{
			if (newFrame)
			{
				_outputFrameCount++;
				OutputFramesTotal++;
			}

			float timeNow = Time.realtimeSinceStartup;
			float timeDelta = timeNow - _outputStartFrameTime;
			if (timeDelta >= 0.5f)
			{
				OutputFPS = (float)_outputFrameCount / timeDelta;
				_outputFrameCount = 0;
				_outputStartFrameTime = timeNow;
			}

			// Update output timecode
			if (newFrame)
			{
				uint timestampSec	= (uint)_outputTimestampSec;
				_timeCode.hours		= (byte)(timestampSec / (60 * 60));
				_timeCode.minutes	= (byte)((timestampSec % (60 * 60)) / 60);
				_timeCode.seconds	= (byte)(timestampSec % 60);
//				_timeCode.frames	= (byte)(OutputFramesTotal % _currentOutputMode.FrameRate);
				_timeCode.frames	= (byte)((((_outputTimestampSec - timestampSec) * _currentOutputMode.FrameRate) + 0.5f) % _currentOutputMode.FrameRate);

/*				if(_timeCode.frames != (byte)(OutputFramesTotal % _currentOutputMode.FrameRate))
				{
					_timeCode.frames = (byte)((_outputTimestampSec-timestampSec) * _currentOutputMode.FrameRate);
				}*/

//				_outputTimestampSec	= timeNow;									// Wall time from app start
//				_outputTimestampSec	+= Time.deltaTime;							// Wall time from stream start
				_outputTimestampSec	+= (1.0f / _currentOutputMode.FrameRate);	// Frame time from stream start
			}
		}

		public void Stop()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			_currentMode = null;

			_isStreamingOutput = false;
			_isStreamingInput = false;
			IsStreaming = false;
			IsPaused = false;
			ResetInputFPS();
			ResetOutputFPS();
			DeckLinkPlugin.StopStream(_deviceIndex);
			_keyingMode = DeckLinkOutput.KeyerMode.None;
#endif
		}

		public void StopOutput()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			_currentOutputMode = null;
			_isStreamingOutput = false;
			IsStreaming = _isStreamingOutput || _isStreamingInput;
			ResetOutputFPS();
			DeckLinkPlugin.StopOutputStream(_deviceIndex);
			_keyingMode = DeckLinkOutput.KeyerMode.None;
#endif
		}

		public void StopInput()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			_currentMode = null;
			_isStreamingInput = false;
			IsStreaming = _isStreamingOutput || _isStreamingInput;
			IsPaused = false;
			ResetInputFPS();
			DeckLinkPlugin.StopInputStream(_deviceIndex);
			DeckLinkPlugin.SetTexturePointer(_deviceIndex, System.IntPtr.Zero);
#endif
		}

		public DeviceMode GetInputMode(int index)
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			DeviceMode result = null;

			if (index >= 0 && index < _inputModes.Count)
				result = _inputModes[index];

			return result;
#else
			return null;
#endif
		}

		public DeviceMode GetOutputMode(int index)
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			DeviceMode result = null;

			if (index >= 0 && index < _outputModes.Count)
				result = _outputModes[index];

			return result;
#else
			return null;
#endif
		}

		public uint GetDroppedOutputFrames()
		{
			return DeckLinkPlugin.GetNumDroppedOutputFrames(_deviceIndex);
		}

		public uint GetDroppedInputFrames()
		{
			return DeckLinkPlugin.GetNumDroppedInputFrames(_deviceIndex);
		}

		public bool InputFrameReceived()
		{
			return DeckLinkPlugin.InputFrameReceived(_deviceIndex);
		}

		public void SetInputFrameReceived(bool received)
		{
			DeckLinkPlugin.SetInputFrameReceived(_deviceIndex, received);
		}

		private void SetKeying(DeckLinkOutput.KeyerMode mode)
		{
			if (_supportsInternalKeying || _supportsExternalKeying)
			{
				DeckLinkPlugin.SwitchKeying(_deviceIndex, mode != DeckLinkOutput.KeyerMode.None, mode == DeckLinkOutput.KeyerMode.External);
				_keyingMode = mode;
			}
		}

		private void EnumModes()
		{
			int numModes = DeckLinkPlugin.GetNumVideoInputModes(_deviceIndex);
			for (int modeIndex = 0; modeIndex < numModes; modeIndex++)
			{
				int width, height;
				float frameRate;
				string modeDesc;
				string pixelFormatDesc;
				long frameDuration;
				int fieldMode;
				bool supportsStereo3D;
				if (DeckLinkPlugin.GetVideoInputModeInfo(_deviceIndex, modeIndex, out width, out height, out frameRate, out frameDuration, out fieldMode, out modeDesc, out pixelFormatDesc, out supportsStereo3D))
				{
					DeviceMode mode = new DeviceMode(this, modeIndex, width, height, frameRate, frameDuration, (DeviceMode.FieldMode)fieldMode, modeDesc, pixelFormatDesc, supportsStereo3D, false);
					_inputModes.Add(mode);
				}
			}

			numModes = DeckLinkPlugin.GetNumVideoOutputModes(_deviceIndex);
			for (int modeIndex = 0; modeIndex < numModes; modeIndex++)
			{
				int width, height;
				float frameRate;
				string modeDesc;
				string pixelFormatDesc;
				long frameDuration;
				int fieldMode;
				bool supportsStereo3D;
				bool supportsKeying;
				if (DeckLinkPlugin.GetVideoOutputModeInfo(_deviceIndex, modeIndex, out width, out height, out frameRate, out frameDuration, out fieldMode, out modeDesc, out pixelFormatDesc, out supportsStereo3D, out supportsKeying))
				{
					DeviceMode mode = new DeviceMode(this, modeIndex, width, height, frameRate, frameDuration, (DeviceMode.FieldMode)fieldMode, modeDesc, pixelFormatDesc, supportsStereo3D, supportsKeying);
					_outputModes.Add(mode);
				}
			}
		}
	}
}