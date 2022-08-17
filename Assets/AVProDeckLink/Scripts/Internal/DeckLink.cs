using UnityEngine;
using System;
using System.Text;
using System.Collections.Generic;

//-----------------------------------------------------------------------------
// Copyright 2014-2021 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProDeckLink
{	
	public abstract class DeckLink : MonoBehaviour
	{
		public enum ColorspaceMode
		{
			Rec709, Rec2020, Rec2100
		}

		[HideInInspector]
		public bool _showExplorer = false;

		[SerializeField]
		private DeviceProfile _deviceProfile = DeviceProfile.Unknown;

#if FEATURE_SYNCGROUPS_WIP
		[SerializeField]
		protected bool _useSyncGroup = false;

		[SerializeField]
		protected int _syncGroupIndex = 0;

		public bool UseSyncGroup
		{
			get { return _useSyncGroup; }
			set { _useSyncGroup = value; }
		}
		public int SyncGroupIndex
		{
			get { return _syncGroupIndex; }
			set { _syncGroupIndex = value; }
		}
#endif

		private GUIStyle _modeListStyle;
		private Vector2 _deviceScrollPos = Vector2.zero;
		private Vector2 _modeScrollPos = Vector2.zero;

		private const float EPSILON = 0.005f;
		protected Device _device = null;
		protected DeckLinkManager _manager = null;

		[HideInInspector]
		public int _deviceIndex = -1;

		[HideInInspector]
		public int _modeIndex = -1;

		[HideInInspector]
		public int _resolutionIndex = -1;

		public bool _enable3D = false;
		public bool _enableAncillaryData = false;
		public bool _enableTimeCodeCapture = false;
		public bool _useHdr = false;
		public ColorspaceMode _colorspaceMode = ColorspaceMode.Rec709;

		public AudioChannels _audioChannels = AudioChannels.Stereo;
		public AudioBitDepth _audioBitDepth = AudioBitDepth.Sixteen;

		[SerializeField]
		protected bool _ignoreAlphaChannel = true;

		public bool IgnoreAlphaChannel { get { return _ignoreAlphaChannel; } set { _ignoreAlphaChannel = value; } }

		public bool _playOnStart = true;

		//---------for approximate matching device-----------
		[HideInInspector]
		public bool _exactDeviceName = false;

		[HideInInspector]
		public string _desiredDeviceName = "DeckLink";

		[HideInInspector]
		public int _desiredDeviceIndex = -1;

		[HideInInspector]
		public bool _exactDeviceIndex = false;

		[HideInInspector]
		public bool _filterDeviceByName = false;

		[HideInInspector]
		public bool _filterDeviceByIndex = false;
		//---------------------------------------------------


		//--------------for approximate matching mode------
		[HideInInspector]
		public bool _filterModeByResolution = false;

		[HideInInspector]
		public bool _filterModeByFormat = false;

		[HideInInspector]
		public bool _filterModeByFPS = false;

		[HideInInspector]
		public bool _filterModeByInterlacing = false;

		[HideInInspector]
		public int _modeWidth = 1920;

		[HideInInspector]
		public int _modeHeight = 1080;

		[HideInInspector]
		public DeckLinkPlugin.PixelFormat _modeFormat = DeckLinkPlugin.PixelFormat.Unknown;

		[HideInInspector]
		public float _modeFPS = 29.97f;

		[HideInInspector]
		public bool _modeInterlacing = false;
		//-------------------------------------------------

		//protected Texture2D _defaultTexture = null;

		public Device Device
		{
			get { return _device; }
		}

		public int DeviceIndex
		{
			get { return _deviceIndex; }
			set { _deviceIndex = value; }
		}

		public int ModeIndex
		{
			get { return _modeIndex; }
			set { _modeIndex = value; }
		}

		protected bool _initialized = false;

		public bool Initialized
		{
			get { return _initialized; }
		}

		public void RenderExplorer()
		{
			if (!_showExplorer)
			{
				return;
			}

			bool restartDevice = false;

			if (_modeListStyle == null)
			{
				_modeListStyle = GUI.skin.GetStyle("ModeList");
			}

			// List the devices
			GUILayout.BeginVertical("box", GUILayout.MinWidth(200), GUILayout.MaxHeight(200));
				
			if (GUILayout.Button("Select Device"))
			{
				_deviceIndex = -1;
				_modeIndex = -1;
				restartDevice = true;
			}

			_deviceScrollPos = GUILayout.BeginScrollView(_deviceScrollPos, false, false);
			for (int i = 0; i < DeckLink.GetNumDevices(); i++)
			{
				Device device = DeckLink.GetDeviceByIndex(i);
				
				if (device.DeviceIndex == _deviceIndex)
				{
					DeviceMode mode = IsInput() ? device.CurrentMode : device.CurrentOutputMode;
					if (mode != null && _modeIndex == mode.Index)
					{
						GUI.color = Color.green;
					}
					else
					{
						GUI.color = Color.blue;
					}
				}

				bool deviceValid = IsInput() ? device.SupportsInput : device.SupportsOutput;

				if (device.DeviceIndex == _deviceIndex || deviceValid)
				{
					string busyStatus = "";
					if (device.IsInputBusy || device.IsOutputBusy) busyStatus = " BUSY";
					string deviceButton = string.Format("{0}{1} ({2})", device.Name, busyStatus, device.ModelName);
					if (GUILayout.Button(deviceButton, _modeListStyle))
					{
						if (_deviceIndex != device.DeviceIndex)
						{
							_deviceIndex = device.DeviceIndex;
							_modeIndex = -1;
							if (string.IsNullOrEmpty(busyStatus))
							{ 
								restartDevice = true;
							}
						}
					}
				}

				GUI.color = Color.white;
			}
			GUILayout.EndScrollView();
			GUILayout.EndVertical();

			Device currDevice = DeckLink.GetDeviceByDeviceIndex(_deviceIndex);
			if (currDevice != null)
			{
				GUILayout.BeginVertical("box", GUILayout.MinWidth(500), GUILayout.MaxHeight(200));

				if (GUILayout.Button("Select Mode:"))
				{
					_modeIndex = -1;
					if (!currDevice.IsInputBusy && !currDevice.IsOutputBusy)
					{ 
						restartDevice = true;
					}
				}
				_modeScrollPos = GUILayout.BeginScrollView(_modeScrollPos, false, false);
				int numModes = IsInput() ? currDevice.NumInputModes : currDevice.NumOutputModes;
				for (int j = 0; j < numModes; j++)
				{
					DeviceMode mode = IsInput() ? currDevice.GetInputMode(j) : currDevice.GetOutputMode(j);

					if (mode.Index == _modeIndex)
					{
						bool streamRunning = IsInput() ? currDevice.IsStreamingInput : currDevice.IsStreamingOutput;
						if (streamRunning)
						{
							GUI.color = Color.green;
						}
						else
						{
							GUI.color = Color.blue;
						}
					}

					if (GUILayout.Button("" + j.ToString("D2") + ") " + mode.ModeDescription + " - " + mode.PixelFormatDescription + " - " + mode.Width + "x" + mode.Height, _modeListStyle))
					{
						if (mode.Index != _modeIndex)
						{
							_modeIndex = mode.Index;
							if (!currDevice.IsInputBusy && !currDevice.IsOutputBusy)
							{
								restartDevice = true;
							}
						}
					}

					GUI.color = Color.white;
				}
				GUILayout.EndScrollView();
				GUILayout.EndVertical();
			}

			if (restartDevice)
			{
				Begin();
			}
		}

		protected int FindClosestMatchingMode(int deviceIndex, bool isInput)
		{
			if (deviceIndex < 0)
			{
				return -1;
			}

			Device device = DeckLink.GetDeviceByDeviceIndex(deviceIndex);

			List<DeviceMode> modes = isInput ? device.InputModes : device.OutputModes;

			for (int i = 0; i < modes.Count; ++i)
			{
				if (_filterModeByResolution)
				{
					if (modes[i].Width != _modeWidth || modes[i].Height != _modeHeight)
					{
						continue;
					}
				}

				if (_filterModeByFormat)
				{
					if (modes[i].PixelFormat != _modeFormat)
					{
						continue;
					}
				}

				if (_filterModeByFPS)
				{
					float modeFps = modes[i].InterlacedFieldMode ? modes[i].FrameRate * 2 : modes[i].FrameRate;
					if (Mathf.Abs(modeFps - _modeFPS) > EPSILON)
					{
						continue;
					}
				}

				if (_filterModeByInterlacing)
				{
					if (_modeInterlacing != modes[i].InterlacedFieldMode)
					{
						continue;
					}
				}

				if (_enable3D && !modes[i].SupportStereo3D)
				{
					continue;
				}

				return modes[i].Index;
			}

			return -1;
		}

		protected int FindClosestMatchingDevice()
		{
			int numDevices = GetNumDevices();

			if (numDevices < 1)
			{
				return -1;
			}

			Device[] devices = new Device[numDevices];

			for (int i = 0; i < numDevices; ++i)
			{
				devices[i] = GetDeviceByIndex(i);
			}

			int index = -1;

			for (int i = 0; i < numDevices; ++i)
			{
				bool deviceValid = (!devices[i].IsInputBusy && !devices[i].IsOutputBusy);
				if (deviceValid)
				{
					index = devices[i].DeviceIndex;
					break;
				}
			}

			if (index < 0)
			{
				return -1;
			}

			List<Device> filteredDevicesLst;
			Device[] filteredDevices;

			if (_filterDeviceByName)
			{
				filteredDevicesLst = new List<Device>();
				for (int i = 0; i < devices.Length; ++i)
				{
					if (_exactDeviceName)
					{
						if (devices[i].Name == _desiredDeviceName)
						{
							filteredDevicesLst.Add(devices[i]);
						}
					}
					else
					{
						if (devices[i].Name.Contains(_desiredDeviceName))
						{
							filteredDevicesLst.Add(devices[i]);
						}
					}
				}

				filteredDevices = filteredDevicesLst.ToArray();
			}
			else
			{
				filteredDevices = devices;
			}

			if (filteredDevices.Length < 1)
			{
				return -1;
			}

			if (_filterDeviceByIndex)
			{
				if (!_exactDeviceIndex)
				{
					_desiredDeviceIndex = Mathf.Clamp(_desiredDeviceIndex, 0, filteredDevices.Length - 1);

					Device selected = filteredDevices[_desiredDeviceIndex];

					bool doneBot = false;
					bool doneTop = false;

					int deviation = 1;

					bool selectedValid = IsInput() ? selected.CanInput() : selected.CanOutput();

					while (!selectedValid || (doneBot & doneTop))
					{
						int newIdx = _desiredDeviceIndex + deviation;

						newIdx = Mathf.Clamp(newIdx, 0, filteredDevices.Length - 1);

						selected = filteredDevices[newIdx];

						selectedValid = IsInput() ? selected.CanInput() : selected.CanOutput();

						if (newIdx == 0)
						{
							doneBot = true;
						}
						else if (newIdx == filteredDevices.Length)
						{
							doneTop = true;
						}

						deviation = deviation < 0 ? -deviation + 1 : -deviation;
					}

					if (!selectedValid)
					{
						index = -1;
					}
					else
					{
						index = selected.DeviceIndex;
					}
				}
				else
				{
					if (_desiredDeviceIndex < 0 || _desiredDeviceIndex >= filteredDevices.Length)
					{
						index = -1;
					}
					else return filteredDevices[_desiredDeviceIndex].DeviceIndex;
				}
			}
			else
			{
				index = -1;
				for (int i = 0; i < filteredDevices.Length; ++i)
				{
					bool valid = IsInput() ? filteredDevices[i].CanInput() : filteredDevices[i].CanOutput();

					if (valid)
					{
						index = filteredDevices[i].DeviceIndex;
						break;
					}
				}
			}

			return index;
		}

		protected abstract bool IsInput();

		protected virtual void Init()
		{

		}

		public virtual void Awake()
		{
			_manager = DeckLinkManager.Instance;
			Init();
		}

		public void Start()
		{
			if (_initialized)
			{
				return;
			}

			if (_playOnStart)
			{
				Begin(true);
			}

			_initialized = true;
		}

		protected abstract void BeginDevice();

		public void Begin(bool search = false)
		{
			if (_device != null)
			{
				if (IsInput() && _device.IsStreamingInput)
				{
					_device.StopInput();
				}
				else if (!IsInput() && _device.IsStreamingOutput)
				{
					_device.StopOutput();
				}

				_device = null;
			}

			if (search || _deviceIndex < 0)
			{
				_deviceIndex = FindClosestMatchingDevice();
				if (_deviceIndex < 0)
				{
					Debug.LogWarning("[AVProDeckLink] Desired device not found");
				}
			}

			if (_deviceIndex < 0)
			{
				return;
			}

			_initialized = false;

			if (search || _modeIndex < 0)
			{
				_modeIndex = FindClosestMatchingMode(_deviceIndex, IsInput());
				if (_modeIndex < 0 && !IsInput())
				{
					Debug.LogWarning("[AVProDeckLink] Desired format not found - the resolution or pixel format may not be supported");
				}
			}

			_device = DeckLinkManager.Instance.GetDeviceByDeviceIndex(_deviceIndex);

			if (_deviceProfile != DeviceProfile.Unknown && _deviceProfile != _device.CurrentDeviceProfile)
			{
				_device.CurrentDeviceProfile = _deviceProfile;
			}

			if (_device != null)
			{
				bool valid = IsInput() ? _device.CanInput() : _device.CanOutput();

				if (valid)
				{
					BeginDevice();
					_initialized = true;
				}
				else
				{
					Debug.LogWarning("[AVProDeckLink] Unable to start device " + _device.Name + ", it might be currently busy");
					_device = null;
				}
			}
			else
			{
				Debug.LogWarning("[AVProDeckLink] No device found for device id " + _deviceIndex);
			}
		}

		protected abstract void Process();

		public void Update()
		{
			if (!_showExplorer)
			{
				DeviceExplorerManager.Instance.UnregisterExplorer(this);
			}
			else
			{
				DeviceExplorerManager.Instance.RegisterExplorer(this);
			}

			Process();
			if (_device != null)
			{
				// RJT NOTE: Changed to allow any device to update so we can read latest health
				// - Input-specific code checks for input streaming anyhow
//				if (this.GetType() == typeof(DeckLinkInput))
				{
					_device.Update();
				}

				_modeIndex = -1;
				if (IsInput())
				{
					if (_device.CurrentMode != null)
					{
						_modeIndex = _device.CurrentMode.Index;
					}
				}
				else
				{
					if (_device.CurrentOutputMode != null)
					{
						_modeIndex = _device.CurrentOutputMode.Index;
					}
				}
				//_modeIndex =  &&  ? _device.CurrentMode.Index : _device.CurrentOutputMode.Index;
			}
		}

		protected abstract void Cleanup();

		public virtual void OnDestroy()
		{
			Cleanup();

			if (_device != null)
			{
				if (IsInput())
				{
					_device.StopInput();
				}
				else
				{
					_device.StopOutput();
				}
			}
			_device = null;

			if (_showExplorer && DeviceExplorerManager.Instance != null)
			{
				DeviceExplorerManager.Instance.UnregisterExplorer(this);
			}
		}

		void OnEnable()
		{
			if (_device != null)
			{
				_device.IsActive = true;
			}
		}

		void OnDisable()
		{
			if (_device != null)
			{
				_device.IsActive = false;
			}
		}

#if UNITY_EDITOR
		public abstract void SavePNG();
#if UNITY_5_6_OR_NEWER
		public abstract void SaveEXR();
#endif
#endif

		public static int GetNumDevices()
		{
			return DeckLinkManager.Instance.NumDevices;
		}

		public static Device GetDeviceByIndex(int localIndex)
		{
			return DeckLinkManager.Instance.GetDeviceByIndex(localIndex);
		}

		public static Device GetDeviceByDeviceIndex(int deviceIndex)
		{
			return DeckLinkManager.Instance.GetDeviceByDeviceIndex(deviceIndex);
		}

		public static int GetSubDeviceCount(DeviceProfile profile)
		{
			int result = 0;
			switch (profile)
			{
				case DeviceProfile.OneSubDevice_HalfDuplex:
				case DeviceProfile.OneSubDevice_FullDuplex:
					result = 1;
					break;
				case DeviceProfile.TwoSubDevices_FullDuplex:
				case DeviceProfile.TwoSubDevices_HalfDuplex:
					result = 2;
					break;
				case DeviceProfile.FourSubDevices_FullDuplex:
					result = 4;
					break;
			}
			return result;
		}

		public static bool IsFullDuplex(DeviceProfile profile)
		{
			bool result = false;
			switch (profile)
			{
				case DeviceProfile.OneSubDevice_FullDuplex:
				case DeviceProfile.TwoSubDevices_FullDuplex:
				case DeviceProfile.FourSubDevices_FullDuplex:
					result = true;
					break;
				case DeviceProfile.OneSubDevice_HalfDuplex:
				case DeviceProfile.TwoSubDevices_HalfDuplex:
					result = false;
					break;
			}
			return result;
		}
	}
}