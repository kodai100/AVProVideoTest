﻿using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

//-----------------------------------------------------------------------------
// Copyright 2014-2020 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProDeckLink.Editor
{
	public abstract class DeckLinkEditor : UnityEditor.Editor
	{
		protected bool _isInput = true;
		private Vector2 _scrollPos = new Vector2(0, 0);
		protected bool _displayModes;
		private bool _expandModes = false;
		private static Texture2D _icon;

		protected const string SettingsPrefix = "DeckLink-DeckLinkEditor-";
		protected static bool _expandPreview = false;
		protected static bool _expandDeviceSelection = false;
		protected static bool _expandModeSelection = false;
		protected static bool _expandAbout = false;

		protected SerializedProperty _selectedDevice;
		protected SerializedProperty _selectedMode;
		protected SerializedProperty _selectedResolution;
		protected SerializedProperty _exactDeviceName;
		protected SerializedProperty _desiredDeviceName;
		protected SerializedProperty _desiredDeviceIndex;
		protected SerializedProperty _exactDeviceIndex;
		protected SerializedProperty _filterDeviceByName;
		protected SerializedProperty _filterDeviceByIndex;
		protected SerializedProperty _filterModeByResolution;
		protected SerializedProperty _filterModeByFormat;
		protected SerializedProperty _filterModeByFPS;
		protected SerializedProperty _filterModeByInterlacing;
		protected SerializedProperty _modeWidth;
		protected SerializedProperty _modeHeight;
		protected SerializedProperty _modeFormat;
		protected SerializedProperty _modeFPS;
		protected SerializedProperty _modeInterlacing;
		protected SerializedProperty _deviceProfile;
		protected SerializedProperty _showExplorer;
		protected SerializedProperty _enable3D;
		protected SerializedProperty _enableAncillaryData;
		protected SerializedProperty _enableTimeCodeCapture;
		protected SerializedProperty _useHdr;
		protected SerializedProperty _colorspaceMode;
		protected SerializedProperty _ignoreAlphaChannel;
		protected SerializedProperty _playOnStart;
		protected SerializedProperty _propAudioChannels;
		protected SerializedProperty _propAudioBitDepth;

#if FEATURE_SYNCGROUPS_WIP
		protected SerializedProperty _propUseSyncGroup;
		protected SerializedProperty _propSyncGroupIndex;
#endif

		private DeckLinkPlugin.PixelFormat[] formats = null;
		private string[] formatNames = null;

		private string[] fpsNames = null;
		private float[] frameRates = null;

		private Resolution[] resolutions = null;
		private string[] resolutionNames = null;

		private const string LinkPluginWebsite = "https://renderheads.com/products/avpro-decklink/";
		private const string LinkForumPage = "http://forum.unity3d.com/threads/released-avpro-decklink-broadcast-video-input-and-output-for-unity.423940/";
		private const string LinkAssetStorePage = "https://assetstore.unity.com/packages/tools/video/avpro-decklink-68784?aid=1101lcNgx";
		private const string LinkEmailSupport = "mailto:unitysupport@renderheads.com";
		private const string LinkUserManual = "http://downloads.renderheads.com/docs/UnityAVProDeckLink.pdf";
		private const string LinkScriptingClassReference = "http://downloads.renderheads.com/docs/AVProDeckLinkClassReference/";
		private const string SupportMessage = "If you are reporting a bug, please include any relevant files and details so that we may remedy the problem as fast as possible.\n\n" +
			"Essential details:\n" +
			"+ Error message\n" +
			"      + The exact error message\n" +
			"      + The console/output log if possible\n" +
			"+ Hardware\n" +
			"      + Phone / tablet / device type and OS version\n" +
			"      + DeckLink device model\n" +
			"      + Input / output device information\n" +
			"+ Development environment\n" +
			"      + Unity version\n" +
			"      + Development OS version\n" +
			"      + AVPro DeckLink plugin version\n" +
			" + Mode details\n" +
			"      + Resolution\n" +
			"      + Format\n" +
			"      + Frame Rate\n" +
			"      + Interlaced / Non-interlaced\n";


		protected void OnDisable()
		{
			SaveSettings();
		}

		public override bool RequiresConstantRepaint()
		{
			return _expandPreview;
		}

		protected virtual void LoadSettings()
		{
			_expandPreview = EditorPrefs.GetBool(SettingsPrefix + "ExpandPreview", false);
			_expandDeviceSelection = EditorPrefs.GetBool(SettingsPrefix + "ExpandDeviceSelection", false);
			_expandModeSelection = EditorPrefs.GetBool(SettingsPrefix + "ExpandModeSelection", false);
			_expandAbout = EditorPrefs.GetBool(SettingsPrefix + "ExpandAbout", false);
		}

		protected virtual void SaveSettings()
		{
			EditorPrefs.SetBool(SettingsPrefix + "ExpandPreview", _expandPreview);
			EditorPrefs.SetBool(SettingsPrefix + "ExpandDeviceSelection", _expandDeviceSelection);
			EditorPrefs.SetBool(SettingsPrefix + "ExpandModeSelection", _expandModeSelection);
			EditorPrefs.SetBool(SettingsPrefix + "ExpandAbout", _expandAbout);
		}

		protected virtual void Init()
		{
			_selectedDevice = serializedObject.FindProperty("_deviceIndex");
			_selectedMode = serializedObject.FindProperty("_modeIndex");
			_selectedResolution = serializedObject.FindProperty("_resolutionIndex");

			_exactDeviceName = serializedObject.FindProperty("_exactDeviceName");
			_desiredDeviceName = serializedObject.FindProperty("_desiredDeviceName");
			_desiredDeviceIndex = serializedObject.FindProperty("_desiredDeviceIndex");
			_exactDeviceIndex = serializedObject.FindProperty("_exactDeviceIndex");

			_filterDeviceByName = serializedObject.FindProperty("_filterDeviceByName");
			_filterDeviceByIndex = serializedObject.FindProperty("_filterDeviceByIndex");
			_filterModeByResolution = serializedObject.FindProperty("_filterModeByResolution");
			_filterModeByFormat = serializedObject.FindProperty("_filterModeByFormat");
			_filterModeByFPS = serializedObject.FindProperty("_filterModeByFPS");
			_filterModeByInterlacing = serializedObject.FindProperty("_filterModeByInterlacing");

			_modeWidth = serializedObject.FindProperty("_modeWidth");
			_modeHeight = serializedObject.FindProperty("_modeHeight");
			_modeFormat = serializedObject.FindProperty("_modeFormat");
			_modeFPS = serializedObject.FindProperty("_modeFPS");
			_modeInterlacing = serializedObject.FindProperty("_modeInterlacing");

			_deviceProfile = serializedObject.FindProperty("_deviceProfile");
			_showExplorer = serializedObject.FindProperty("_showExplorer");

			_enable3D = serializedObject.FindProperty("_enable3D");
			_enableAncillaryData = serializedObject.FindProperty("_enableAncillaryData");
			_enableTimeCodeCapture = serializedObject.FindProperty("_enableTimeCodeCapture");
			_useHdr = serializedObject.FindProperty("_useHdr");
			_colorspaceMode = serializedObject.FindProperty("_colorspaceMode");
			_ignoreAlphaChannel = serializedObject.FindProperty("_ignoreAlphaChannel");
			_playOnStart = serializedObject.FindProperty("_playOnStart");

			_propAudioChannels = serializedObject.FindProperty("_audioChannels");
			_propAudioBitDepth = serializedObject.FindProperty("_audioBitDepth");

#if FEATURE_SYNCGROUPS_WIP
			_propUseSyncGroup = serializedObject.FindProperty("_useSyncGroup");
			_propSyncGroupIndex = serializedObject.FindProperty("_syncGroupIndex");
#endif
		}

		protected void DrawDeviceFilters()
		{
			GUILayout.Space(8f);

			if (GUILayout.Button("Device Selection", EditorStyles.toolbarButton))
			{
				_expandDeviceSelection = !_expandDeviceSelection;
			}

			if (_expandDeviceSelection)
			{
				GUILayout.BeginVertical("box");
				if (_filterDeviceByName.boolValue)
				{
					GUI.color = Color.green;
				}

				GUIStyle buttonStyle = GUI.skin.GetStyle("Button");
				buttonStyle.alignment = TextAnchor.UpperCenter;

				GUILayout.Space(4f);
				if (GUILayout.Button("Select device by name", buttonStyle)){
					_filterDeviceByName.boolValue = !_filterDeviceByName.boolValue;
				}

				GUI.color = Color.white;
				
				if (_filterDeviceByName.boolValue)
				{
					EditorGUILayout.BeginVertical("box");
					_exactDeviceName.boolValue = !EditorGUILayout.Toggle("Approximate search", !_exactDeviceName.boolValue);
					_desiredDeviceName.stringValue = EditorGUILayout.TextField("Device Name", _desiredDeviceName.stringValue).Trim();
					EditorGUILayout.EndVertical();
				}

				if (_filterDeviceByIndex.boolValue)
				{
					GUI.color = Color.green;
				}

				GUILayout.Space(4f);
				if (GUILayout.Button("Select device by index")){
					_filterDeviceByIndex.boolValue = !_filterDeviceByIndex.boolValue;
				}

				GUI.color = Color.white;

				if (_filterDeviceByIndex.boolValue)
				{
					EditorGUILayout.BeginVertical("box");
					_exactDeviceIndex.boolValue = !EditorGUILayout.Toggle("Approximate search", !_exactDeviceIndex.boolValue);
					_desiredDeviceIndex.intValue = EditorGUILayout.IntField("Device Index", _desiredDeviceIndex.intValue);
					EditorGUILayout.EndVertical();
				}

				GUILayout.Space(4f);

				GUILayout.EndVertical();

			}
		}

		private static string[] GetInputFormats(out DeckLinkPlugin.PixelFormat[] formats)
		{
			string[] names = new string[5];
			formats = new DeckLinkPlugin.PixelFormat[5];

			names[0] = "8-bit UYVY 4:2:2";
			names[1] = "10-bit UYVY 4:2:2";
			names[2] = "8-bit ARGB 4:4:4:4";
			names[3] = "8-bit BGRA 4:4:4:4";
			names[4] = "10-bit RGB 4:4:4";

			formats[0] = DeckLinkPlugin.PixelFormat.YCbCr_8bpp_422;
			formats[1] = DeckLinkPlugin.PixelFormat.YCbCr_10bpp_422;
			formats[2] = DeckLinkPlugin.PixelFormat.ARGB_8bpp_444;
			formats[3] = DeckLinkPlugin.PixelFormat.BGRA_8bpp_444;
			formats[4] = DeckLinkPlugin.PixelFormat.RGB_10bpp_444;

			// TODO: add the rest of the formats here?

			return names;
		}

		private static string[] GetOutputFormats(out DeckLinkPlugin.PixelFormat[] formats)
		{
			string[] names = new string[7];
			formats = new DeckLinkPlugin.PixelFormat[7];

			names[0] = "8-bit UYVY 4:2:2";
			names[1] = "10-bit UYVY 4:2:2";
			names[2] = "8-bit ARGB";
			names[3] = "8-bit BGRA";
			names[4] = "10-bit RGB";
			names[5] = "10-bit RGBX";
			names[6] = "10-bit RGBX LE";

			formats[0] = DeckLinkPlugin.PixelFormat.YCbCr_8bpp_422;
			formats[1] = DeckLinkPlugin.PixelFormat.YCbCr_10bpp_422;
			formats[2] = DeckLinkPlugin.PixelFormat.ARGB_8bpp_444;
			formats[3] = DeckLinkPlugin.PixelFormat.BGRA_8bpp_444;
			formats[4] = DeckLinkPlugin.PixelFormat.RGB_10bpp_444;
			formats[5] = DeckLinkPlugin.PixelFormat.RGBX_10bpp_444;
			formats[6] = DeckLinkPlugin.PixelFormat.RGBX_10bpp_444_LE;

			return names;
		}

		private static Texture2D _statusTextureGood = null;
		private static Texture2D _statusTextureBad = null;
		private static Texture2D _statusTextureOkay = null;

		protected enum StatusLevel
		{
			Good,
			Okay,
			Bad,
		}

		protected static void DrawStatusIcon(StatusLevel level)
		{
			// Could create our own graphic, but instead just use some built-in ones
			if (_statusTextureGood == null) _statusTextureGood = EditorGUIUtility.FindTexture("winbtn_mac_max");
			if (_statusTextureBad == null) _statusTextureBad = EditorGUIUtility.FindTexture("winbtn_mac_close");
			if (_statusTextureOkay == null) _statusTextureOkay = EditorGUIUtility.FindTexture("winbtn_mac_min");

			Texture2D statusTexture = _statusTextureGood;
			if (level == StatusLevel.Okay) statusTexture = _statusTextureOkay;
			if (level == StatusLevel.Bad) statusTexture = _statusTextureBad;
			
			if (statusTexture != null)
			{
				GUILayout.Label(new GUIContent("", statusTexture), GUILayout.MaxWidth(16.0f));
			}
		}

		protected void DrawModeFilters(bool isInput)
		{
			GUILayout.Space(8f);

			if (GUILayout.Button("Mode Selection", EditorStyles.toolbarButton))
			{
				_expandModeSelection = !_expandModeSelection;
			}

			if (_expandModeSelection)
			{
				GUILayout.BeginVertical("box");

				if (isInput)
				{
					((DeckLinkInputEditor)this).DrawModeFilters_Input();
				}

				GUILayout.Space(4f);
				if (_filterModeByResolution.boolValue)
				{
					GUI.color = Color.green;
				}

				string resolutionButton = "Force Resolution";
				string framerateButton = "Force Frame Rate";
				string interlaceButton = "Force Interlacing";
				string formatButton = "Force Format";
				if (isInput)
				{
					if (((DeckLinkInputEditor)this).IsAutoDetectMode())
					{
						resolutionButton = "Fallback Resolution";
						framerateButton = "Fallback Frame Rate";
						interlaceButton = "Fallback Interlacing";
						formatButton = "Fallback Format";
					}
				}

				if (GUILayout.Button(resolutionButton))
				{
					_filterModeByResolution.boolValue = !_filterModeByResolution.boolValue;
				}

				GUI.color = Color.white;

				if (_filterModeByResolution.boolValue)
				{
					GUILayout.BeginVertical("box");

					if (resolutions == null || resolutionNames == null || resolutions.Length == 0 || resolutionNames.Length == 0)
					{
						GetResolutions(out resolutions, out resolutionNames);
					}

					int foundPos = resolutionNames.Length - 1;
					for (int i = 0; i < resolutions.Length; ++i)
					{
						
						if (resolutions[i].width == _modeWidth.intValue && resolutions[i].height == _modeHeight.intValue)
						{
							foundPos = i;
							break;
						}
					}
					
					int newPos = EditorGUILayout.Popup("Resolution", foundPos, resolutionNames);
										

					if (newPos < resolutions.Length)
					{
						_modeWidth.intValue = resolutions[newPos].width;
						_modeHeight.intValue = resolutions[newPos].height;
					}
					else
					{
						if(newPos != foundPos)
						{
							_modeWidth.intValue = 0;
							_modeHeight.intValue = 0;
						}

						_modeWidth.intValue = EditorGUILayout.IntField("Width ", _modeWidth.intValue);
						_modeHeight.intValue = EditorGUILayout.IntField("Height ", _modeHeight.intValue);
					}
					
					GUILayout.EndVertical();
				}

				GUILayout.Space(4f);

				if (_filterModeByFPS.boolValue)
				{
					GUI.color = Color.green;
				}

				if (GUILayout.Button(framerateButton))
				{
					_filterModeByFPS.boolValue = !_filterModeByFPS.boolValue;
				}

				GUI.color = Color.white;

				if (_filterModeByFPS.boolValue)
				{
					GUILayout.BeginVertical("box");

					if (frameRates == null || fpsNames == null || frameRates.Length == 0 || fpsNames.Length == 0)
					{
						GetFrameRates(out frameRates, out fpsNames);
					}

					int foundPos = fpsNames.Length - 1;
					for(int i = 0; i < frameRates.Length; ++i)
					{
						if(Mathf.Abs(frameRates[i] - _modeFPS.floatValue) < 0.005f)
						{
							foundPos = i;
							break;
						}
					}

					int newPos = EditorGUILayout.Popup("Frame Rate", foundPos, fpsNames);
					if (newPos < frameRates.Length)
					{
						_modeFPS.floatValue = frameRates[newPos];
					}
					else
					{
						if(newPos != foundPos)
						{
							_modeFPS.floatValue = 0f;
						}

						_modeFPS.floatValue = EditorGUILayout.FloatField("FrameRate", _modeFPS.floatValue);
					}

					GUILayout.EndVertical();
				}

				GUILayout.Space(4f);

				if (_filterModeByInterlacing.boolValue)
				{
					GUI.color = Color.green;
				}
				

				if (GUILayout.Button(interlaceButton))
				{
					_filterModeByInterlacing.boolValue = !_filterModeByInterlacing.boolValue;
				}

				GUI.color = Color.white;

				if (_filterModeByInterlacing.boolValue)
				{
					GUILayout.BeginVertical("box");
					_modeInterlacing.boolValue = EditorGUILayout.Toggle("Interlaced", _modeInterlacing.boolValue);
					GUILayout.EndVertical();
				}

				GUILayout.Space(4f);

				if (_filterModeByFormat.boolValue)
				{
					GUI.color = Color.green;
				}

				if (GUILayout.Button(formatButton))
				{
					_filterModeByFormat.boolValue = !_filterModeByFormat.boolValue;
				}

				GUI.color = Color.white;

				if (_filterModeByFormat.boolValue)
				{
					GUILayout.BeginVertical("box");

					if(formatNames == null || formats == null || formatNames.Length == 0 || formats.Length == 0)
					{
						if (isInput)
						{
							formatNames = GetInputFormats(out formats);
						}
						else
						{
							formatNames = GetOutputFormats(out formats);
						}
					}
					

					DeckLinkPlugin.PixelFormat prevFormat = (DeckLinkPlugin.PixelFormat)_modeFormat.intValue;
					int prevSelected = 0;

					for (int i = 0; i < formats.Length; ++i)
					{
						if (prevFormat == formats[i])
						{
							prevSelected = i;
							break;
						}
					}

					int selected = EditorGUILayout.Popup("Pixel Format", prevSelected, formatNames);
					_modeFormat.intValue = (int)formats[selected];
					GUILayout.EndVertical();
				}

				GUILayout.Space(4f);

				GUILayout.EndVertical();
			}
		}

		private string[] GetDevices()
		{
			int num_devices = DeckLinkPlugin.GetNumDevices();

			string[] devices = new string[num_devices];

			for (int i = 0; i < num_devices; ++i)
			{
				devices[i] = DeckLinkPlugin.GetDeviceDisplayName(i);
			}

			return devices;
		}

		private void GetResolutions(out Resolution[] resolutions, out string[] resolutionNames)
		{
			resolutions = new Resolution[9];
			resolutions[0].width = 720; resolutions[0].height = 486;
			resolutions[1].width = 720; resolutions[1].height = 576;
			resolutions[2].width = 1280; resolutions[2].height = 720;
			resolutions[3].width = 1920; resolutions[3].height = 1080;
			resolutions[4].width = 2048; resolutions[4].height = 1080;
			resolutions[5].width = 3840; resolutions[5].height = 2160;
			resolutions[6].width = 4096; resolutions[6].height = 2160;
			resolutions[7].width = 7680; resolutions[7].height = 4320;
			resolutions[8].width = 8192; resolutions[8].height = 4320;

			resolutionNames = new string[10] {
				"NTSC",
				"PAL",
				"HD720p",
				"HD1080p",
				"2K DCI",
				"4K UHD",
				"4K DCI",
				"8K UHD",
				"8K DCI",
				"Custom"
			};
		}

		private void GetFrameRates(out float[] frameRates, out string[] frameRateNames)
		{
			frameRates = new float[15]
			{
				23.98f,
				24f,
				25f,
				29.97f,
				30f,
				47.95f,
				48f,
				50f,
				59.94f,
				60f,
				95.90f,
				96f,
				100f,
				119.88f,
				120f
			};

			frameRateNames = new string[16]
			{
				"23.98",
				"24",
				"25",
				"29.97",
				"30",
				"47.95",
				"48",
				"50",
				"59.94",
				"60",
				"95.90",
				"96",
				"100",
				"119.88",
				"120",
				"Custom"
			};
		}

		protected abstract bool ModeValid(DeckLinkPlugin.PixelFormat format);

		private string[] GetDeviceModes(int device, out List<Resolution> resolutions, out Resolution[] modeResolutions, out int[] positions)
		{
			List<Resolution> outputRes = new List<Resolution>();
			int num_modes = DeckLinkPlugin.GetNumVideoInputModes(device);

			List<string> modes = new List<string>();
			List<Resolution> mrs = new List<Resolution>();
			List<int> actual_positions = new List<int>();

			for (int i = 0; i < num_modes; ++i)
			{
				int width, height, fieldMode;
				float frameRate;
				long frameDuration;
				string modeDesc, pixelFormatDesc;
				bool supportsStereo3D;
				bool supportsKeying = false;

				if (_isInput)
				{
					DeckLinkPlugin.GetVideoInputModeInfo(device, i, out width, out height, out frameRate, out frameDuration, out fieldMode, out modeDesc, out pixelFormatDesc, out supportsStereo3D);
				}
				else
				{
					DeckLinkPlugin.GetVideoOutputModeInfo(device, i, out width, out height, out frameRate, out frameDuration, out fieldMode, out modeDesc, out pixelFormatDesc, out supportsStereo3D, out supportsKeying);
				}

				DeckLinkPlugin.PixelFormat format = DeckLinkPlugin.GetPixelFormat(pixelFormatDesc);

				if (FormatConverter.InputFormatSupported(format))
				{
					modes.Add(modeDesc + " " + pixelFormatDesc + (supportsStereo3D?"(3D)":""));

					Resolution r = new Resolution();
					r.width = width;
					r.height = height;
					mrs.Add(r);

					actual_positions.Add(i);

					bool resolutionFound = false;
					for (int j = 0; j < outputRes.Count; ++j)
					{
						if (width == outputRes[j].width && height == outputRes[j].height)
						{
							resolutionFound = true;
							break;
						}
					}

					if (!resolutionFound)
					{
						Resolution res = new Resolution();
						res.width = width;
						res.height = height;
						outputRes.Add(res);
					}
				}
			}

			resolutions = outputRes;
			modeResolutions = mrs.ToArray();
			positions = actual_positions.ToArray();

			return modes.ToArray();
		}

		protected void OnInspectorGUI_About()
		{
			GUILayout.Space(8f);

			if (GUILayout.Button("About", EditorStyles.toolbarButton))
			{
				_expandAbout = !_expandAbout;
			}

			if (_expandAbout)
			{
				string version = DeckLinkPlugin.GetNativePluginVersion();

				EditorGUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (_icon == null)
				{
					_icon = Resources.Load<Texture2D>("AVProDeckLinkIcon");
				}
				if (_icon != null)
				{
					GUILayout.Label(new GUIContent(_icon));
				}
				GUILayout.FlexibleSpace();
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUI.color = Color.yellow;
				GUILayout.Label("AVPro DeckLink by RenderHeads Ltd", EditorStyles.boldLabel);
				GUI.color = Color.white;
				GUILayout.FlexibleSpace();
				EditorGUILayout.EndHorizontal();

				EditorGUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUI.color = Color.yellow;
				GUILayout.Label("version " + version + " (scripts v" + Helper.Version + ")");
				GUI.color = Color.white;
				GUILayout.FlexibleSpace();
				EditorGUILayout.EndHorizontal();

				GUILayout.Space(32f);
				GUI.backgroundColor = Color.white;

				EditorGUILayout.LabelField("Links", EditorStyles.boldLabel);

				GUILayout.Space(8f);

				EditorGUILayout.LabelField("Documentation");
				if (GUILayout.Button("User Manual", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(LinkUserManual);
				}
				if (GUILayout.Button("Scripting Class Reference", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(LinkScriptingClassReference);
				}

				GUILayout.Space(16f);

				GUILayout.Label("Rate and Review (★★★★☆)", GUILayout.ExpandWidth(false));
				if (GUILayout.Button("Unity Asset Store Page", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(LinkAssetStorePage);
				}

				GUILayout.Space(16f);

				GUILayout.Label("Community");
				if (GUILayout.Button("Unity Forum Page", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(LinkForumPage);
				}

				GUILayout.Space(16f);

				GUILayout.Label("Homepage", GUILayout.ExpandWidth(false));
				if (GUILayout.Button("AVPro DeckLink Website", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(LinkPluginWebsite);
				}

				GUILayout.Space(16f);

				GUILayout.Label("Bugs and Support");
				EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button("Email unitysupport@renderheads.com", GUILayout.ExpandWidth(false)))
				{
					Application.OpenURL(LinkEmailSupport);
				}
				EditorGUILayout.EndHorizontal();

				GUILayout.Space(32f);

				EditorGUILayout.LabelField("Credits", EditorStyles.boldLabel);
				GUILayout.Space(8f);

				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUILayout.Label("Programming", EditorStyles.boldLabel);
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
				GUILayout.Space(8f);
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUILayout.Label("Andrew Griffiths");
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUILayout.Label("Sunrise Wang");
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				GUILayout.Space(8f);
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUILayout.Label("Graphics", EditorStyles.boldLabel);
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();
				GUILayout.Space(8f);

				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				GUILayout.Label("Jeff Rusch");
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				GUILayout.Space(32f);

				EditorGUILayout.LabelField("Bug Reporting Notes", EditorStyles.boldLabel);

				EditorGUILayout.SelectableLabel(SupportMessage, EditorStyles.wordWrappedLabel, GUILayout.Height(300f));
			}
		}

		protected void DrawDeviceModes()
		{
			if (GUILayout.Button("Refresh devices"))
			{
				DeckLinkPlugin.Deinit();
				DeckLinkPlugin.Init();
			}

			var devices = GetDevices();

			_selectedDevice.intValue = Mathf.Min(_selectedDevice.intValue, devices.Length - 1);
			_selectedDevice.intValue = EditorGUILayout.Popup("Device", _selectedDevice.intValue < 0 ? 0 : _selectedDevice.intValue, devices);

			if (devices.Length == 0)
			{
				_selectedDevice.intValue = -1;
			}

			if (_displayModes)
			{
				GUILayout.Space(8f);

				if (GUILayout.Button("Modes", EditorStyles.toolbarButton))
				{
					_expandModes = !_expandModes;
				}

				if (_expandModes)
				{
					string[] modes;
					List<Resolution> resolutions;
					Resolution[] modeResolutions;
					int[] actual_positions;

					if (_selectedDevice.intValue >= 0)
					{
						modes = GetDeviceModes(_selectedDevice.intValue, out resolutions, out modeResolutions, out actual_positions);
					}
					else
					{
						modes = new string[0];
						resolutions = new List<Resolution>();
						modeResolutions = new Resolution[0];
						actual_positions = new int[0];
					}

					int prev_pos = 0;

					for (int i = 0; i < actual_positions.Length; ++i)
					{
						if (actual_positions[i] == _selectedMode.intValue)
						{
							prev_pos = i;
							break;
						}
					}

					EditorGUILayout.LabelField("Mode");

					int rows = resolutions.Count % 4 == 0 ? resolutions.Count / 4 : resolutions.Count / 4 + 1;

					for (int i = 0; i < rows; ++i)
					{
						EditorGUILayout.BeginHorizontal();
						for (int j = 0; j < 4; ++j)
						{
							int pos = i * 4 + j;
							if (pos >= resolutions.Count)
							{
								break;
							}

							if (_selectedResolution.intValue == pos)
							{
								GUI.color = Color.cyan;
							}

							if (GUILayout.Button(resolutions[pos].width + "x" + resolutions[pos].height))
							{
								_selectedResolution.intValue = pos;
							}

							GUI.color = Color.white;
						}
						EditorGUILayout.EndHorizontal();
					}

					if (_selectedResolution.intValue >= 0 && _selectedResolution.intValue < resolutions.Count)
					{
						_scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, false, false, GUILayout.MaxHeight(200), GUILayout.MinHeight(200));

						for (int i = 0; i < modes.Length; ++i)
						{
							if (modeResolutions[i].width != resolutions[_selectedResolution.intValue].width ||
								modeResolutions[i].height != resolutions[_selectedResolution.intValue].height)
							{
								continue;
							}

							bool selected = false;

							if (prev_pos == i)
							{
								GUI.color = Color.yellow;
							}

							selected = GUILayout.Button(modes[i]);

							if (prev_pos == i)
							{
								GUI.color = Color.white;
							}

							if (selected)
							{
								_selectedMode.intValue = actual_positions[i];
							}
						}

						EditorGUILayout.EndScrollView();
					}

					if (modes.Length == 0)
					{
						_selectedMode.intValue = -1;
					}
				}

			}

			OnInspectorGUI_About();
		}

		protected void DrawPreviewTexture(DeckLink decklink)
		{
			GUILayout.Space(8f);

			GUI.color = Color.cyan;
			if (GUILayout.Button("Preview", EditorStyles.toolbarButton))
			{
				_expandPreview = !_expandPreview;
			}
			GUI.color = Color.white;

			if (_expandPreview)
			{
				bool active = decklink != null && decklink.Device != null;

				GUI.enabled = active;

				Texture previewTex = null;
				Texture rightEyeTex = null;
				if (active)
				{
					previewTex = _isInput ? ((DeckLinkInput)decklink).OutputTexture : ((DeckLinkOutput)decklink).InputTexture;
					if (previewTex == null)
					{
						previewTex = EditorGUIUtility.whiteTexture;
					}

					rightEyeTex = _isInput ? ((DeckLinkInput)decklink).RightOutputTexture : ((DeckLinkOutput)decklink).RightInputTexture;
					if(rightEyeTex == null)
					{
						rightEyeTex = EditorGUIUtility.whiteTexture;
					}
					
				}
				else
				{
					previewTex = EditorGUIUtility.whiteTexture;
				}

				GUILayout.Space(8f);

				if (previewTex != EditorGUIUtility.whiteTexture)
				{
					Rect textureRect = GUILayoutUtility.GetRect(128.0f, 128.0f, GUILayout.MinWidth(128.0f), GUILayout.MinHeight(128.0f));
					GUI.DrawTexture(textureRect, previewTex, ScaleMode.ScaleToFit);
				}
				else
				{
					Rect textureRect = GUILayoutUtility.GetRect(1920f / 40, 1080f / 40);
					GUI.DrawTexture(textureRect, EditorGUIUtility.whiteTexture, ScaleMode.ScaleToFit);
				}

				if(active)
				{
					bool drawRightEye = _isInput ? ((DeckLinkInput)decklink)._enable3D : ((DeckLinkOutput)decklink)._enable3D;
					if (drawRightEye)
					{
						if (previewTex != EditorGUIUtility.whiteTexture)
						{
							Rect textureRect = GUILayoutUtility.GetRect(128.0f, 128.0f, GUILayout.MinWidth(128.0f), GUILayout.MinHeight(128.0f));
							GUI.DrawTexture(textureRect, rightEyeTex, ScaleMode.ScaleToFit);
						}
						else
						{
							Rect textureRect = GUILayoutUtility.GetRect(1920f / 40, 1080f / 40);
							GUI.DrawTexture(textureRect, EditorGUIUtility.whiteTexture, ScaleMode.ScaleToFit);
						}
					}
				}
				
				// Texture tools
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				if (GUILayout.Button("Select Texture", GUILayout.ExpandWidth(false)))
				{
					Selection.activeObject = previewTex;
				}
				if (GUILayout.Button("Save PNG", GUILayout.ExpandWidth(false)))
				{
					decklink.SavePNG();
				}
#if UNITY_5_6_OR_NEWER
				if (GUILayout.Button("Save EXR", GUILayout.ExpandWidth(false)))
				{
					decklink.SaveEXR();
				}
#endif
				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				Device device = active ? decklink.Device : null;
				string deviceName = active ? device.Name : "N/A";
				GUILayout.Label("Device: " + deviceName);

				DeviceMode mode = null;

				if (device != null)
				{
					GUILayout.Label("Profile: " + device.CurrentDeviceProfile.ToString());

					mode = _isInput ? device.CurrentMode : device.CurrentOutputMode;

					if (active && mode != null)
					{
						GUILayout.Label(string.Format("Mode: {0}x{1}/{2}hz {3}", mode.Width, mode.Height, mode.FrameRate.ToString("F2"), mode.PixelFormatDescription));
					}

					if (device.EnableTimeCodeInput)
					{
						FrameTimeCode tc = device.GetFrameTimeCode();
						GUILayout.Label(string.Format("TimeCode: {0}:{1}:{2}:{3}", tc.hours, tc.minutes, tc.seconds, tc.frames));
					}

					if (_isInput)
					{
						GUILayout.BeginHorizontal();
						if (active && device.InputFramesTotal > 30)
						{
							GUILayout.Label("Running at " + device.InputFPS.ToString("F1") + " fps");
							GUILayout.Label("Frame: " + device.InputFramesTotal);
							GUILayout.Label("Signal Drops: " + device.DroppedInputSignalCount);
						}
						else
						{
							GUILayout.Label("Running at ... fps");
						}
						GUILayout.EndHorizontal();

						if (active && device.IsStreaming)
						{
							GUILayout.BeginHorizontal();
							if (device.IsPaused)
							{
								if (GUILayout.Button("Unpause Stream"))
								{
									device.Unpause();
								}
							}
							else
							{
								if (GUILayout.Button("Pause Stream"))
								{
									device.Pause();
								}
							}
							GUILayout.EndHorizontal();
						}
					}
					else
					{
						if (active)
						{
							GUILayout.BeginHorizontal();
							if (active && device.OutputFramesTotal > 30)
							{
								GUILayout.Label("Running at " + device.OutputFPS.ToString("F1") + " fps");
								GUILayout.Label("Frame: " + device.OutputFramesTotal);
								GUILayout.Label("Dropped: " + device.GetDroppedOutputFrames());
							}
							else
							{
								GUILayout.Label("Running at ... fps");
							}
							GUILayout.EndHorizontal();

							GUILayout.Label("Genlock status: " + (device.IsGenLocked ? " Locked" : "Not Locked"));
							if (device.IsGenLocked)
							{
								GUILayout.Label("Pixel offset: " + device.GenlockOffset);
								GUILayout.Label("Full Frame Pixel Offset is " + (device.SupportsFullFrameGenlockOffset ? " supported" : "not supported"));
							}
							{
								switch (device.OutputSmpteLevel)
								{
									case SmpteLevel.LevelA:
										GUILayout.Label("SMPTE Level A");
										break;
									case SmpteLevel.LevelB:
										GUILayout.Label("SMPTE Level B");
										break;
								}
							}
						}
					}

					// Health
					{
						GUILayout.Label("Health status:");

						StatusLevel status = StatusLevel.Good;
						if (device.HealthStatus < 0.66f) status = StatusLevel.Okay;
						if (device.HealthStatus < 0.33f) status = StatusLevel.Bad;

						GUILayout.BeginHorizontal();
						DrawStatusIcon(status);
						GUILayout.HorizontalSlider(device.HealthStatus, 0f, 1f);
						GUILayout.EndHorizontal();
					}
				}

				GUI.enabled = true;
			}
		}
	}
}
