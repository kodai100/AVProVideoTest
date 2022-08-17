#define AVPRODECKLINK_UNITYFEATURE_EXTERNALTEXTURES

using UnityEngine;
using System.Text;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

//-----------------------------------------------------------------------------
// Copyright 2014-2020 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProDeckLink
{
	public class DeckLinkManager : Singleton<DeckLinkManager>
	{
		protected DeckLinkManager() { }

		public bool _logDeviceEnumeration;
		[SerializeField] Shader _shader_YCbCr_8bpp_422 = null;
		[SerializeField] Shader _shader_YCbCr_10bpp_422 = null;
		[SerializeField] Shader _shader_ARGB_8bpp_444 = null;
		[SerializeField] Shader _shader_BGRA_8bpp_444 = null;
		[SerializeField] Shader _shaderDeinterlace = null;

		private static ChromaLerp _lerpType;
		public DeinterlaceMethod _deinterlaceMethod = DeinterlaceMethod.Blend;

		private bool _queueReenumerate;
		
#if UNITY_5 && !UNITY_5_0 && !UNITY_5_1 || UNITY_5_4_OR_NEWER
		private System.IntPtr _renderEventFunctor;
#endif

		public enum ChromaLerp
		{
			Off,
			Lerp,
			Smart,
		}

		public enum DeinterlaceMethod
		{
			None,
			Discard,
			DiscardSmooth,
			Blend,
		}

		private List<Device> _devices;
		private bool _isInitialised;
		private bool _isOpenGL;
		//private long _frameTime;
		private List<DeckLinkOutput> _outputs;

		//-------------------------------------------------------------------------

		public ChromaLerp LerpType
		{
			get { return _lerpType; }
		}

		public bool IsOpenGL
		{
			get { return _isOpenGL; }
		}

		public int NumDevices
		{
			get { if (_devices != null) return _devices.Count; return 0; }
		}

		new void Awake()
		{
			base.Awake();
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			if (Init())
			{
				Debug.Log("[AVProDeckLink] Initialised (plugin v" + DeckLinkPlugin.GetNativePluginVersion() + " script v" + Helper.Version + ")");

				uint apiVersionCode = DeckLinkPlugin.GetDeckLinkAPIVersion();
				string apiVersionString = "" + ((apiVersionCode >> 24) & 255) + "." + ((apiVersionCode >> 16) & 255) + "." + ((apiVersionCode >> 8) & 255) + "." + ((apiVersionCode >> 0) & 255);
				Debug.Log("[AVProDeckLink] Using DeckLink API version " + apiVersionString);
			}
			else
			{
				Debug.LogError("[AVProDeckLink] failed to initialise.");
				this.enabled = false;
				_isInitialised = false;
			}
#endif
		}

		protected bool Init()
		{
			DeckLinkPlugin.Init();

			if (!_shaderDeinterlace) _shaderDeinterlace = Shader.Find("AVProDeckLink/Deinterlace");
			if (!_shader_ARGB_8bpp_444) _shader_ARGB_8bpp_444 = Shader.Find("AVProDeckLink/CompositeARGB");
			if (!_shader_BGRA_8bpp_444) _shader_BGRA_8bpp_444 = Shader.Find("AVProDeckLink/CompositeBGRA");
			if (!_shader_YCbCr_10bpp_422) _shader_YCbCr_10bpp_422 = Shader.Find("AVProDeckLink/CompositeV210");
			if (!_shader_YCbCr_8bpp_422) _shader_YCbCr_8bpp_422 = Shader.Find("AVProDeckLink/CompositeUYVY");
#if UNITY_5 && !UNITY_5_0 && !UNITY_5_1 || UNITY_5_4_OR_NEWER
			_renderEventFunctor = DeckLinkPlugin.GetRenderEventFunc();
#endif

#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			try
			{

				bool unitySupportsExternalTextures = false;
#if AVPRODECKLINK_UNITYFEATURE_EXTERNALTEXTURES
				unitySupportsExternalTextures = true;
#endif
				DeckLinkPlugin.SetUnityFeatures(unitySupportsExternalTextures);
			}
			catch (System.DllNotFoundException e)
			{
				Debug.LogError("[AVProDeckLink] Unity couldn't find the DLL, did you move the 'Plugins' folder to the root of your project?");
				throw e;
			}

			_isOpenGL = SystemInfo.graphicsDeviceVersion.StartsWith("OpenGL");

			bool swapRedBlue = false;
			if (SystemInfo.graphicsDeviceVersion.StartsWith("Direct3D 11"))
				swapRedBlue = true;

			if (swapRedBlue)
			{
				Shader.DisableKeyword("SWAP_RED_BLUE_OFF");
				Shader.EnableKeyword("SWAP_RED_BLUE_ON");
			}
			else
			{
				Shader.DisableKeyword("SWAP_RED_BLUE_ON");
				Shader.EnableKeyword("SWAP_RED_BLUE_OFF");
			}

			SetChromaInterpolation(ChromaLerp.Lerp);

			EnumDevices();

			//_frameTime = GetFrameInterval(Screen.currentResolution.refreshRate);
			//Debug.Log("[AVProDeckLink] Using frame interval " + _frameTime + " for rate of " + Screen.currentResolution.refreshRate.ToString("F3"));

			_isInitialised = true;
			StartCoroutine("FinalRenderCapture");

			return _isInitialised;
#else
			return false;
#endif
		}

		public static void SetChromaInterpolation(ChromaLerp lerp)
		{
			Shader.DisableKeyword("CHROMA_NOLERP");
			Shader.DisableKeyword("CHROMA_LERP");
			Shader.DisableKeyword("CHROMA_SMARTLERP");
			switch (lerp)
			{
				case ChromaLerp.Off:
					Shader.EnableKeyword("CHROMA_NOLERP");
					break;
				case ChromaLerp.Lerp:
					Shader.EnableKeyword("CHROMA_LERP");
					break;
				case ChromaLerp.Smart:
					Shader.EnableKeyword("CHROMA_SMARTLERP");
					break;
			}

			_lerpType = lerp;
		}


#if UNITY_EDITOR
		[ContextMenu("Set Chroma Lerp: Off")]
		private void SetChromaLerpOff()
		{
			SetChromaInterpolation(ChromaLerp.Off);
		}

		[ContextMenu("Set Chroma Lerp: Lerp")]
		private void SetChromaLerpOn()
		{
			SetChromaInterpolation(ChromaLerp.Lerp);
		}

		[ContextMenu("Set Chroma Lerp: Smart")]
		private void SetChromaLerpSmart()
		{
			SetChromaInterpolation(ChromaLerp.Smart);
		}
#endif


		private static long GetFrameInterval(float fps)
		{
			long frameTime = 0;
			switch (fps.ToString("F3"))
			{
				case "60.000":
					frameTime = 166667;
					break;
				case "59.000":
				case "59.940":
					frameTime = 166833;
					break;
				case "50.000":
					frameTime = 200000;
					break;
				case "30.000":
					frameTime = 333333;
					break;
				case "29.970":
					frameTime = 333667;
					break;
				case "25.000":
					frameTime = 400000;
					break;
				case "24.000":
					frameTime = 416667;
					break;
				case "23.976":
					frameTime = 417188;
					break;
			}
			return frameTime;
		}

		private IEnumerator FinalRenderCapture()
		{
			var wait = new WaitForEndOfFrame();
			while (Application.isPlaying)
			{
				yield return wait;

				RenderOutputs();
			}
		}

		internal void AddOutputDevice(DeckLinkOutput output)
		{
			if (_outputs == null)
			{
				_outputs = new List<DeckLinkOutput>(8);
			}

			_outputs.Add(output);
		}

		internal void RemoveOutputDevice(DeckLinkOutput output)
		{
			_outputs.Remove(output);

			if (_outputs.Count == 0)
			{
				_outputs = null;
			}
		}

		void Update()
		{
			if (_queueReenumerate)
			{
				Reset();
				return;
			}

			RenderInputs();
//			RenderOutputs();
//			System.Threading.Thread.Sleep(2);
#if true
			// Determine number of live outputs
			if (_outputs != null)
			{
				int liveOutputs = 0;
				foreach(DeckLinkOutput output in _outputs)
				{
					if ((output.Device != null) && output.Device.IsStreamingOutput)
					{
						++liveOutputs;
					}
				}

				// Update multi-output flag appropriately
				bool _multiOutputLast = DeckLinkSettings.Instance._multiOutput;
				DeckLinkSettings.Instance._multiOutput = (liveOutputs > 1);
				if (DeckLinkSettings.Instance._multiOutput != _multiOutputLast)
				{
					// RJT NOTE: Default to original 'burst mode' and run Unity unbounded
					Application.targetFrameRate = -1; Time.captureFramerate = -1;
				}
			}
#endif
		}

		// RJT TODO: Better location

		// Greatest Common Denominator of two numbers (GCD)
		private int GCD(int a, int b)
		{
			if (b == 0)
			{  
				return a;
			}

			return GCD(b, (a % b));
		}  		

		// Smallest Common Denominator of two numbers (SCD)
		private int SCD(int a, int b)
		{
			return (a * b / GCD(a, b));
		}

		void LateUpdate()
		{
#if true
			// If multiple outputs are active then attempt to sync Unity to them
			// RJT TODO: Would be far nicer to have a single point of control for Unity update speed so could
			// look at migrating non-multi-output/'DeckLinkOutput::SetUpdateFrameRate()' here too?
			if (DeckLinkSettings.Instance._multiOutput)
			{
	#if true	// Lowest Common Multiple (LCM) of all output rates
				{
					// Determine LCM (if multiple outputs)
					// RJT NOTE: We also add (/subtract) any changes made in 'DeckLinkOutput::AdjustPlaybackFramerate()'
					// to compensate for buffers becoming too empty (/full) but don't factor this into the LCM or
					// target framerate could be all over the place!
					int targetFramerate = -1, compensateFramerate = 0;
					foreach (DeckLinkOutput output in _outputs)
					{
						if ((output.Device != null) && output.Device.IsStreamingOutput)
						{
							targetFramerate = ((targetFramerate < 0) ?
								output./*OutputFramerate*/TargetFramerate :
								SCD(targetFramerate, output./*OutputFramerate*/TargetFramerate)
							);

							compensateFramerate += (output.OutputFramerate - output.TargetFramerate);
						}
					}

					// Lock Unity to this rate
					Application.targetFrameRate = (targetFramerate + compensateFramerate);
				}
	#elif false	// ?
				{

				}
	#endif
			}
#endif
		}

		/*void OnPreCull()
		{
			RenderInputs();
			RenderOutputs();
			System.Threading.Thread.Sleep(2);
		}

		void OnPreRender()
		{
			RenderInputs();
			RenderOutputs();
			System.Threading.Thread.Sleep(2);
		}

		void OnPostRender()
		{
			RenderInputs();
			RenderOutputs();
			System.Threading.Thread.Sleep(2);
		}*/

		public void RenderInputs()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
#if UNITY_5 && !UNITY_5_0 && !UNITY_5_1 || UNITY_5_4_OR_NEWER
			GL.IssuePluginEvent(_renderEventFunctor, DeckLinkPlugin.PluginID | (int)DeckLinkPlugin.PluginEvent.UpdateAllInputs);
#else
			GL.IssuePluginEvent(DeckLinkPlugin.PluginID | (int)DeckLinkPlugin.PluginEvent.UpdateAllInputs);
#endif
#endif
		}

		public void RenderOutputs()
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN

#if UNITY_5 && !UNITY_5_0 && !UNITY_5_1 || UNITY_5_4_OR_NEWER
				GL.IssuePluginEvent(_renderEventFunctor, DeckLinkPlugin.PluginID | (int)DeckLinkPlugin.PluginEvent.UpdateAllOutputs);
#else
				GL.IssuePluginEvent(DeckLinkPlugin.PluginID | (int)DeckLinkPlugin.PluginEvent.UpdateAllOutputs);
#endif

#endif            
		}

		public bool GetPixelConversionShader(DeckLinkPlugin.PixelFormat format, ref Shader shader, ref int pass)
		{
			bool result = true;
			pass = 0;
			switch (format)
			{
				case DeckLinkPlugin.PixelFormat.YCbCr_8bpp_422:
					shader = _shader_YCbCr_8bpp_422;
					break;
				case DeckLinkPlugin.PixelFormat.YCbCr_10bpp_422:
					result = false;		// Handled by compute shader
					break;
				case DeckLinkPlugin.PixelFormat.ARGB_8bpp_444:
					shader = _shader_ARGB_8bpp_444;
					break;
				case DeckLinkPlugin.PixelFormat.BGRA_8bpp_444:
					shader = _shader_BGRA_8bpp_444;
					break;
				case DeckLinkPlugin.PixelFormat.RGB_10bpp_444:
					result = false;        // Handled by compute shader
					break;
				default:
					Debug.LogError("[AVProDeckLink] Unsupported pixel format " + format);
					result = false;
					break;
			}

			return result;
		}

		public Shader GetDeinterlaceShader()
		{
			return _shaderDeinterlace;
		}

		internal void QueueReenumerate()
		{
			// Sometimes when the number of devices changes we need to run enumeration again
			_queueReenumerate = true;
		}

		public void Reset()
		{
			_queueReenumerate = false;
			Deinit();
//			DeckLinkPlugin.Deinit();
//			DeckLinkPlugin.Init();
			Init();
		}

		void OnApplicationQuit()
		{
			Deinit();
		}

		private void Deinit()
		{
			StopCoroutine("FinalRenderCapture");

			if (_devices != null)
			{
				for (int i = 0; i < _devices.Count; i++)
				{
					_devices[i].StopInput();
					_devices[i].StopOutput();
					_devices[i].Dispose();
				}
				_devices.Clear();
				_devices = null;
			}

			_isInitialised = false;

			DeckLinkPlugin.Deinit();
		}

		private void EnumDevices()
		{
			_devices = new List<Device>(8);
			int numDevices = DeckLinkPlugin.GetNumDevices();

			if(numDevices == 0)
			{
				uint apiVersionCode = DeckLinkPlugin.GetDeckLinkAPIVersion();
				string apiVersionString = "" + ((apiVersionCode >> 24) & 255) + "." + ((apiVersionCode >> 16) & 255) + "." + ((apiVersionCode >> 8) & 255) + "." + ((apiVersionCode >> 0) & 255);
				Debug.LogWarning("[AVProDeckLink] Unable to find any DeckLink Devices, It is possible that your Desktop Video is out of date. Please update to version " + apiVersionString);
			}

			for (int deviceIndex = 0; deviceIndex < numDevices; deviceIndex++)
			{
				int numInputModes = DeckLinkPlugin.GetNumVideoInputModes(deviceIndex);
				int numOutputModes = DeckLinkPlugin.GetNumVideoOutputModes(deviceIndex);
				if (numInputModes > 0 || numOutputModes > 0)
				{
					string modelName = DeckLinkPlugin.GetDeviceName(deviceIndex);
					string displayName = DeckLinkPlugin.GetDeviceDisplayName(deviceIndex);
					Device device = new Device(modelName, displayName, deviceIndex);
					_devices.Add(device);

					if (_logDeviceEnumeration)
					{
						Debug.Log("[AVProDeckLink] Device" + deviceIndex + ": " + displayName + "(" + modelName + ") has " + device.NumInputModes + " video input modes, " + device.NumOutputModes + " video output modes");
						Debug.Log("[AVProDeckLink] Has " + device.DeviceProfiles.Length + " profiles, current profile: " + device.CurrentDeviceProfile.ToString());
						for (int profileIndex = 0; profileIndex < device.DeviceProfiles.Length; profileIndex++)
						{
							Debug.Log("[AVProDeckLink]\t\tProfile " + profileIndex + ": " + device.DeviceProfiles[profileIndex].ToString());
						}

						if (device.SupportsInputModeAutoDetection)
							Debug.Log("[AVProDeckLink]\tSupports input video mode auto-detection");
						if (device.SupportsInternalKeying)
							Debug.Log("[AVProDeckLink]\tSupports internal keyer");
						if (device.SupportsExternalKeying)
							Debug.Log("[AVProDeckLink]\tSupports external keyer");

						for (int modeIndex = 0; modeIndex < device.NumInputModes; modeIndex++)
						{
							DeviceMode mode = device.GetInputMode(modeIndex);
							Debug.Log("[AVProDeckLink]\t\tInput Mode" + modeIndex + ":  " + mode.ModeDescription + " " + mode.Width + "x" + mode.Height + " @" + mode.FrameRate.ToString("F2") + " (" + mode.PixelFormatDescription + ") " + (mode.SupportStereo3D ? "Stereo":""));
						}
						for (int modeIndex = 0; modeIndex < device.NumOutputModes; modeIndex++)
						{
							DeviceMode mode = device.GetOutputMode(modeIndex);
							Debug.Log("[AVProDeckLink]\t\tOutput Mode" + modeIndex + ": " + mode.ModeDescription + " " + mode.Width + "x" + mode.Height + " @" + mode.FrameRate.ToString("F2") + " (" + mode.PixelFormatDescription + ") " + (mode.SupportStereo3D ? "Stereo":"") + " "+ (mode.SupportKeying ? "Keying":"" ));
						}
					}
				}
			}
		}

		public Device GetDeviceByIndex(int index)
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			Device result = null;

			if (_devices != null && index >= 0 && index < _devices.Count)
			{
				result = _devices[index];
			}

			return result;
#else
			return null;
#endif
		}

		public Device GetDeviceByDeviceIndex(int index)
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			Device result = null;

			if (_devices != null)
			{
				foreach (Device device in _devices)
				{
					if (device.DeviceIndex == index)
					{
						result = device;
						break;
					}
				}
			}

			return result;
#else
			return null;
#endif
		}		

		public Device GetDeviceByName(string name)
		{
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
			Device result = null;
			int numDevices = NumDevices;
			for (int i = 0; i < numDevices; i++)
			{
				Device device = GetDeviceByIndex(i);
				if (device.Name == name)
				{
					result = device;
					break;
				}
			}
			return result;
#else
			return null;
#endif
		}
	}
}
