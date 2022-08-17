#if UNITY_WSA_10 || ENABLE_IL2CPP
	#define AVPRODECKLINK_MARSHAL_RETURN_BOOL
#endif

using UnityEngine;
using System.Text;
using System.Runtime.InteropServices;

//-----------------------------------------------------------------------------
// Copyright 2014-2021 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProDeckLink
{
	public enum AudioChannels : int
	{
		None = 0,
		Stereo = 2,
		Count8 = 8,
		Count16 = 16,
		Count32 = 32,
		Count64 = 64,
	}

	public enum AudioBitDepth : int
	{
		Sixteen = 16,
		ThirtyTwo = 32,
	}
	
	[StructLayout(LayoutKind.Sequential, Pack = 1)]
	public struct AncPacket
	{
		public byte dataId1;
		public byte dataId2;
		public uint frameLine;
		public byte dataStreamIndex;
		public ushort dataByteCount;
		public ushort dataByteOffset;
	};

	public enum SmpteLevel : int
	{
		Unspecified,
		LevelA,				// Full frame
		LevelB,				// Frame is split (Default on DeckLink)
	}

	public enum DeviceProfile : int
	{
		Unknown,
		OneSubDevice_FullDuplex,
		OneSubDevice_HalfDuplex,
		TwoSubDevices_FullDuplex,
		TwoSubDevices_HalfDuplex,
		FourSubDevices_FullDuplex,
	}

	public class DeckLinkPlugin
	{
		// For use by GL.IssuePluginEvent
		public const int PluginID = 0xFA50000;
		public enum PluginEvent
		{
			UpdateAllInputs = 0,
			UpdateAllOutputs = 1,
		}

		public enum PixelFormat
		{
			YCbCr_8bpp_422 = 0,
			YCbCr_10bpp_422,

			ARGB_8bpp_444,
			BGRA_8bpp_444,
			RGB_10bpp_444,
			RGBX_10bpp_444,
			RGBX_10bpp_444_LE,

			RGB_12bpp_444,
			RGB_12bpp_444_LE,

			Unknown,
		}

		public static PixelFormat GetPixelFormat(string name)
		{
			PixelFormat result = PixelFormat.Unknown;
			switch (name)
			{
				case "8-bit 4:2:2 YUV":
					result = PixelFormat.YCbCr_8bpp_422;
					break;
				case "10-bit 4:2:2 YUV":
					result = PixelFormat.YCbCr_10bpp_422;
					break;
				case "8-bit 4:4:4:4 ARGB":
					result = PixelFormat.ARGB_8bpp_444;
					break;
				case "8-bit 4:4:4:4 BGRA":
					result = PixelFormat.BGRA_8bpp_444;
					break;
				case "10-bit 4:4:4 RGB":
					result = PixelFormat.RGB_10bpp_444;
					break;
				case "10-bit 4:4:4 RGBX LE":
					result = PixelFormat.RGBX_10bpp_444;
					break;
				case "10-bit 4:4:4 RGBX":
					result = PixelFormat.RGBX_10bpp_444_LE;
					break;
				case "12-bit 4:4:4 RGB LE":
					result = PixelFormat.RGB_12bpp_444_LE;
					break;
				case "12-bit 4:4:4 RGB":
					result = PixelFormat.RGB_12bpp_444;
					break;
				default:
					break;
			}
			return result;
		}

		//////////////////////////////////////////////////////////////////////////////////////////////
		// Global Init/Deinit
		//////////////////////////////////////////////////////////////////////////////////////////////

		[DllImport("AVProDeckLink")]
		private static extern System.IntPtr GetPluginVersion();

		public static string GetNativePluginVersion()
		{
			return System.Runtime.InteropServices.Marshal.PtrToStringAnsi(GetPluginVersion());
		}

		[DllImport("AVProDeckLink")]
		public static extern uint GetDeckLinkAPIVersion();

		[DllImport("AVProDeckLink")]
		public static extern void SetUnityFeatures(bool supportsExternalTextures);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool Init();

		[DllImport("AVProDeckLink")]
		public static extern void Deinit();

		//////////////////////////////////////////////////////////////////////////////////////////////
		// Devices
		//////////////////////////////////////////////////////////////////////////////////////////////

		[DllImport("AVProDeckLink")]
		public static extern int GetNumDevices();

		public static string GetDeviceName(int deviceIndex)
		{
			string result = "Invalid";
			StringBuilder nameBuffer = new StringBuilder(128);
			if (GetDeviceName(deviceIndex, nameBuffer, nameBuffer.Capacity))
			{
				result = nameBuffer.ToString();
			}
			return result;
		}

		public static string GetDeviceDisplayName(int deviceIndex)
		{
			string result = "Invalid";
			StringBuilder nameBuffer = new StringBuilder(128);
			if (GetDeviceDisplayName(deviceIndex, nameBuffer, nameBuffer.Capacity))
			{
				result = nameBuffer.ToString();
			}
			return result;
		}

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool FullDuplexSupported(int device);
		[DllImport("AVProDeckLink")]
		public static extern void SetDuplexMode(int device, bool isFull);
		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool ConfigurableDuplexMode(int device);

		//////////////////////////////////////////////////////////////////////////////////////////////
		// Device Profile
		//////////////////////////////////////////////////////////////////////////////////////////////

		[DllImport("AVProDeckLink")]
		public static extern DeviceProfile GetCurrentDeviceProfile(int device);
		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool SetCurrentDeviceProfile(int device, DeviceProfile profile);
		[DllImport("AVProDeckLink")]
		public static extern int GetDeviceProfileCount(int device);
		[DllImport("AVProDeckLink")]
		public static extern DeviceProfile GetDeviceProfile(int device, int index);

		//////////////////////////////////////////////////////////////////////////////////////////////
		// Input Output status
		//////////////////////////////////////////////////////////////////////////////////////////////

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool SupportsInput(int deviceIndex);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool SupportsOutput(int deviceIndex);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool IsInputBusy(int deviceIndex);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool IsOutputBusy(int deviceIndex);

		//////////////////////////////////////////////////////////////////////////////////////////////
		// Video Input Modes
		//////////////////////////////////////////////////////////////////////////////////////////////

		[DllImport("AVProDeckLink")]
		public static extern int GetNumVideoInputModes(int deviceIndex);

		public static bool GetVideoInputModeInfo(int deviceIndex, int modeIndex, out int width, out int height, out float frameRate, out long frameDuration, out int fieldMode, out string modeDesc, out string formatDesc, out bool supportsStereo3D)
		{
			bool result = false;
			StringBuilder modeDescStr = new StringBuilder(32);
			StringBuilder formatDescStr = new StringBuilder(32);
			if (GetVideoInputModeInfo(deviceIndex, modeIndex, out width, out height, out frameRate, out frameDuration, out fieldMode, modeDescStr, modeDescStr.Capacity, formatDescStr, formatDescStr.Capacity, out supportsStereo3D))
			{
				modeDesc = modeDescStr.ToString();
				formatDesc = formatDescStr.ToString();
				result = true;
			}
			else
			{
				modeDesc = string.Empty;
				formatDesc = string.Empty;
			}

			return result;
		}

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool SupportsInputModeAutoDetection(int deviceIndex);

		//////////////////////////////////////////////////////////////////////////////////////////////
		// Video Output Modes
		//////////////////////////////////////////////////////////////////////////////////////////////

		[DllImport("AVProDeckLink")]
		public static extern int GetNumVideoOutputModes(int deviceIndex);

		public static bool GetVideoOutputModeInfo(int deviceIndex, int modeIndex, out int width, out int height, out float frameRate, out long frameDuration, out int fieldMode, out string modeDesc, out string formatDesc, out bool supportsStereo3D, out bool supportsKeying)
		{
			bool result = false;
			StringBuilder modeDescStr = new StringBuilder(32);
			StringBuilder formatDescStr = new StringBuilder(32);
			if (GetVideoOutputModeInfo(deviceIndex, modeIndex, out width, out height, out frameRate, out frameDuration, out fieldMode, modeDescStr, modeDescStr.Capacity, formatDescStr, formatDescStr.Capacity, out supportsStereo3D, out supportsKeying))
			{
				modeDesc = modeDescStr.ToString();
				formatDesc = formatDescStr.ToString();
				result = true;
			}
			else
			{
				modeDesc = string.Empty;
				formatDesc = string.Empty;
			}

			return result;
		}

		//////////////////////////////////////////////////////////////////////////////////////////////	
		// Input Buffers
		//////////////////////////////////////////////////////////////////////////////////////////////	

		[DllImport("AVProDeckLink")]
		public static extern void ConfigureInputBuffer(int deviceIndex, int totalBufferCount, int readBufferCount);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool GetInputBufferStats(int deviceIndex, out int totalBufferCount, out int readBufferCount, out int usedFrameCount, out int pendingFrameCount);

		//////////////////////////////////////////////////////////////////////////////////////////////	
		// Keying
		//////////////////////////////////////////////////////////////////////////////////////////////	

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool SupportsInternalKeying(int deviceIndex);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool SupportsExternalKeying(int deviceIndex);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool SwitchKeying(int deviceIndex, bool state, bool isExternal);

		//////////////////////////////////////////////////////////////////////////////////////////////	
		// Start / Stop
		//////////////////////////////////////////////////////////////////////////////////////////////	

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool StartInputStream(int deviceIndex, int modeIndex, AudioChannels audioChannelCount, AudioBitDepth audioBitDepth
#if FEATURE_SYNCGROUPS_WIP
		, int syncGroupIndex
#endif
		);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool StartOutputStream(int deviceIndex, int modeIndex, AudioChannels audioChannelCount, AudioBitDepth audioBitDepth
#if FEATURE_SYNCGROUPS_WIP
		, int syncGroupIndex
#endif
		);

		[DllImport("AVProDeckLink")]
		public static extern int GetVideoInputModeIndex(int deviceIndex);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool StopStream(int deviceIndex);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool Pause(int deviceIndex);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool Unpause(int deviceIndex);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool IsNoInputSignal(int deviceIndex);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool StopOutputStream(int deviceIndex);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool StopInputStream(int deviceIndex);

		[DllImport("AVProDeckLink")]
		public static extern void SetAutoDetectEnabled(int device, bool enabled);

		//////////////////////////////////////////////////////////////////////////////////////////////
		// Rendering
		//////////////////////////////////////////////////////////////////////////////////////////////

		[DllImport("AVProDeckLink")]
		public static extern void SetTexturePointer(int deviceIndex, System.IntPtr texturePointer);
		[DllImport("AVProDeckLink")]
		public static extern void SetRightTexturePointer(int deviceIndex, System.IntPtr texturePointer);

		[DllImport("AVProDeckLink")]
		public static extern void SetOutputTexturePointer(int deviceIndex, System.IntPtr texturePtr);
		[DllImport("AVProDeckLink")]
		public static extern void SetRightOutputTexturePointer(int deviceIndex, System.IntPtr texturePtr);

		[DllImport("AVProDeckLink")]
		public static extern void SetOutputBufferPointer(int deviceIndex, byte[] buffer);
		[DllImport("AVProDeckLink")]
		public static extern void SetRightOutputBufferPointer(int deviceIndex, byte[] buffer);

		[DllImport("AVProDeckLink")]
		public static extern System.IntPtr GetTexturePointer(int deviceIndex);
		[DllImport("AVProDeckLink")]
		public static extern System.IntPtr GetRightTexturePointer(int deviceIndex);

		[DllImport("AVProDeckLink")]
		public static extern void SetLowLatencyMode(int deviceIndex, bool lowLatencyMode);

		[DllImport("AVProDeckLink")]
		public static extern ulong GetLastFrameUploaded(int deviceIndex);

		// Interlaced output frame notification
		[DllImport("AVProDeckLink")]
		public static extern void SetInterlacedOutputFrameReady(int deviceIndex, bool isReady);

		// SYNC

		[DllImport("AVProDeckLink")]
		public static extern void SetPresentFrame(long minTime, long maxTime);

		[DllImport("AVProDeckLink")]
		public static extern long GetLastCapturedFrameTime(int deviceIndex);

		[DllImport("AVProDeckLink")]
		public static extern System.IntPtr GetFramePixels(int deviceIndex, long time);

		//////////////////////////////////////////////////////////////////////////////////////////////
		// DEBUGGING
		//////////////////////////////////////////////////////////////////////////////////////////////

		[DllImport("AVProDeckLink")]
		public static extern int GetReadBufferIndex(int deviceIndex);

		[DllImport("AVProDeckLink")]
		public static extern int GetWriteBufferIndex(int deviceIndex);

		[DllImport("AVProDeckLink")]
		public static extern int GetOutputBufferedFramesCount(int deviceIndex);

		[DllImport("AVProDeckLink")]
		public static extern int GetFreeOutputBufferCount(int deviceIndex);

		[DllImport("AVProDeckLink")]
		public static extern int GetWaitingOutputBufferCount(int deviceIndex);

		[DllImport("AVProDeckLink")]
		public static extern uint GetNumDroppedOutputFrames(int deviceIndex);

		[DllImport("AVProDeckLink")]
		public static extern uint GetNumDroppedInputFrames(int deviceIndex);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool InputFrameReceived(int deviceIndex);

		[DllImport("AVProDeckLink")]
		public static extern void SetInputFrameReceived(int deviceIndex, bool received);

		//////////////////////////////////////////////////////////////////////////////////////////////
		// Private internal functions
		//////////////////////////////////////////////////////////////////////////////////////////////

		[DllImport("AVProDeckLink", CharSet = CharSet.Unicode)]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		private static extern bool GetDeviceName(int deviceIndex, StringBuilder name, int nameBufferLength);

		[DllImport("AVProDeckLink", CharSet = CharSet.Unicode)]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		private static extern bool GetDeviceDisplayName(int deviceIndex, StringBuilder name, int nameBufferLength);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		private static extern bool GetVideoInputModeInfo(int deviceIndex, int modeIndex, out int width, out int height, out float frameRate, out long frameDuration, out int fieldMode, StringBuilder modeDesc, int modeDescLength, StringBuilder formatDesc, int formatDescLength, out bool supportsStereo3D);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		private static extern bool GetVideoOutputModeInfo(int deviceIndex, int modeIndex, out int width, out int height, out float frameRate, out long frameDuration, out int fieldMode, StringBuilder modeDesc, int modeDescLength, StringBuilder formatDesc, int formatDescLength, out bool supportsStereo3D, out bool supportsKeying);

#if UNITY_5 && !UNITY_5_0 && !UNITY_5_1 || UNITY_5_4_OR_NEWER
		[DllImport("AVProDeckLink")]
		public static extern System.IntPtr GetRenderEventFunc();
#endif

		[DllImport("AVProDeckLink")]
		public static extern void SetDeviceOutputReady(int deviceIndex);
		[DllImport("AVProDeckLink")]
		public static extern void SetPotTextures(int deviceIndex, bool potTextures);
		[DllImport("AVProDeckLink")]
		public static extern void SetGammaSpace(int deviceIndex, bool isGamma);
		[DllImport("AVProDeckLink")]
		public static extern void SetOutputColourSpace(int deviceIndex, int colourSpace);
		[DllImport("AVProDeckLink")]
		public static extern void SetOutputTransferFunction(int deviceIndex, int transferFunction);

		//////////////////////////////////////////////////////////////////////////////////////////////
		// Genlock functions
		//////////////////////////////////////////////////////////////////////////////////////////////
		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool IsGenLocked(int device);
		[DllImport("AVProDeckLink")]
		public static extern void SetGenlockOffset(int device, int offset);
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		[DllImport("AVProDeckLink")]
		public static extern bool SupportsFullFrameGenlockOffset(int device);

#if FEATURE_SYNCGROUPS_WIP
		//////////////////////////////////////////////////////////////////////////////////////////////
		// Sync Groups
		//////////////////////////////////////////////////////////////////////////////////////////////
		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool SupportsInputSyncGroups(int device);

		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool SupportsOutputSyncGroups(int device);
#endif
		//////////////////////////////////////////////////////////////////////////////////////////////
		// Output SMPTE Level functions
		//////////////////////////////////////////////////////////////////////////////////////////////
		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif		
		public static extern bool SupportsOutputSmpteLevel(int device, int levelIndex);
		[DllImport("AVProDeckLink")]
		public static extern int GetOutputSmpteLevel(int device);
		[DllImport("AVProDeckLink")]
		public static extern void SetOutputSmpteLevel(int device, int levelIndex);


		//////////////////////////////////////////////////////////////////////////////////////////////
		// Audio functions
		//////////////////////////////////////////////////////////////////////////////////////////////
		//It is important to lock/unlock before/after you call GetAudioBufferSize and GetAudioBuffer
		[DllImport("AVProDeckLink")]
		public static extern void GetAudioBuffer(int device, float[] buffer, int size, int channels, float volume);
		[DllImport("AVProDeckLink")]
		public static extern void FlushAudioBuffer(int device);
		[DllImport("AVProDeckLink")]
		public static extern AudioChannels GetMaxSupportedAudioChannels(int device);
		[DllImport("AVProDeckLink")]
		public static extern void OutputAudio(int deviceIndex, float[] audioData, int audioDataFloatCount, int channelCount);


		//////////////////////////////////////////////////////////////////////////////////////////////
		// 3D functions
		//////////////////////////////////////////////////////////////////////////////////////////////
		[DllImport("AVProDeckLink")]
		public static extern void Set3DCaptureEnabled(int device, bool enabled);
		[DllImport("AVProDeckLink")]
		public static extern void Set3DPlaybackEnabled(int device, bool enabled);

		//////////////////////////////////////////////////////////////////////////////////////////////
		// Ancillary Data
		//////////////////////////////////////////////////////////////////////////////////////////////
		[DllImport("AVProDeckLink")]
		public static extern void SetAncillaryDataCaptureEnabled(int device, bool enabled);
		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool GetLastFrameAncillaryData(int deviceIndex, ref long frameTimeStamp, System.IntPtr ancPackets, ref int ancPacketCount, System.IntPtr dstData, ref int dstDataByteCount);

		//////////////////////////////////////////////////////////////////////////////////////////////
		// Time Code
		//////////////////////////////////////////////////////////////////////////////////////////////
		[DllImport("AVProDeckLink")]
		public static extern void SetTimeCodeCaptureEnabled(int device, bool enabled);
		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif
		public static extern bool GetLastFrameTimeCode(int deviceIndex, ref long frameTimeStamp, ref uint timeCode);
		[DllImport("AVProDeckLink")]
		public static extern void SetOutputTimeCode(int deviceIndex, uint timeCode);

		//////////////////////////////////////////////////////////////////////////////////////////////
		// Other functions
		//////////////////////////////////////////////////////////////////////////////////////////////
		[DllImport("AVProDeckLink")]
#if AVPRODECKLINK_MARSHAL_RETURN_BOOL
		[return: MarshalAs(UnmanagedType.I1)]
#endif		
		private static extern bool ActivateLicense([MarshalAs(UnmanagedType.LPStr)] string productName, [MarshalAs(UnmanagedType.LPStr)] string licenseKey, uint iterationCount, StringBuilder licenseType, StringBuilder userName, StringBuilder userCompany, StringBuilder userEmail, StringBuilder expireMessage);

		public static bool ActivateLicense(string productName, string licenseKey, uint iterationCount, out string licenseType, out string userName, out string userCompany, out string userEmail, out string expireMessage)
		{
			bool result = false;
			StringBuilder licenseTypeStr = new StringBuilder(32);
			StringBuilder userNameStr = new StringBuilder(64);
			StringBuilder userCompanyStr = new StringBuilder(64);
			StringBuilder userEmailStr = new StringBuilder(128);
			StringBuilder expireMessageStr = new StringBuilder(256);
			if (ActivateLicense(productName, licenseKey, iterationCount, licenseTypeStr, userNameStr, userCompanyStr, userEmailStr, expireMessageStr))
			{
				licenseType = licenseTypeStr.ToString();
				userName = userNameStr.ToString();
				userCompany = userCompanyStr.ToString();
				userEmail = userEmailStr.ToString();
				result = true;
			}
			else
			{
				licenseType = "Invalid";
				userName = string.Empty;
				userCompany = string.Empty;
				userEmail = string.Empty;
			}

			expireMessage = expireMessageStr.ToString();

			return result;
		}

		//////////////////////////////////////////////////////////////////////////////////////////////
		// Health
		//////////////////////////////////////////////////////////////////////////////////////////////

		[DllImport("AVProDeckLink")]
		public static extern float GetHealthStatus(int deviceIndex);
	}
}