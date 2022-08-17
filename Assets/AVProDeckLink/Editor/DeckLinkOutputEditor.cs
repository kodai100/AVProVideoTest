using UnityEngine;
using UnityEditor;
using System.Collections;
using System.Collections.Generic;

//-----------------------------------------------------------------------------
// Copyright 2014-2020 RenderHeads Ltd.  All rights reserved.
//-----------------------------------------------------------------------------

namespace RenderHeads.Media.AVProDeckLink.Editor
{
	[CanEditMultipleObjects]
	[CustomEditor(typeof(DeckLinkOutput))]
	public class DeckLinkOutputEditor : DeckLinkEditor
	{
		private DeckLinkOutput _camera;
		private SerializedProperty _propSyncedtoInput;
		private SerializedProperty _propAntiAliasingLevel;
		private SerializedProperty _propBufferBalance;
		private SerializedProperty _propNoCameraMode;
		private SerializedProperty _propDefaultTexture;
		private SerializedProperty _propDefaultColour;
		private SerializedProperty _propCamera;
		private SerializedProperty _propCameraRightEye;
		private SerializedProperty _propLowLatencyMode;
		private SerializedProperty _propOutputAudioSource;
		private SerializedProperty _propMuteOutputAudio;
		private SerializedProperty _propBypassGamma;
		private SerializedProperty _propGenlockPixelOffset;
		private SerializedProperty _propSmpteLevel;
		private SerializedProperty _propKeyerMode;
		private bool validate = true;
		private bool _expandModeChange = false;
		private Vector2 _modeScroll = Vector2.zero;

		protected static bool _expandGeneral = false;
		protected static bool _expandVideo = false;
		protected static bool _expandAudio = false;
		protected static bool _expandMisc = false;		

		void OnEnable()
		{
			Init();
			LoadSettings();
		}

		protected override void Init()
		{
			_camera = (this.target) as DeckLinkOutput;
			_isInput = false;
			_displayModes = true;
			_propSyncedtoInput = serializedObject.FindProperty("_syncedToInput");
			_propAntiAliasingLevel = serializedObject.FindProperty("_antiAliasingLevel");
			_propBufferBalance = serializedObject.FindProperty("_bufferBalance");
			_propNoCameraMode = serializedObject.FindProperty("_noCameraMode");
			_propDefaultTexture = serializedObject.FindProperty("_defaultTexture");
			_propDefaultColour = serializedObject.FindProperty("_defaultColour");
			_propCamera = serializedObject.FindProperty("_camera");
			_propCameraRightEye = serializedObject.FindProperty("_rightEyeCamera");
			_propLowLatencyMode = serializedObject.FindProperty("_lowLatencyMode");
			_propOutputAudioSource = serializedObject.FindProperty("_outputAudioSource");
			_propMuteOutputAudio = serializedObject.FindProperty("_muteOutputAudio");
			_propBypassGamma = serializedObject.FindProperty("_bypassGamma");
			_propGenlockPixelOffset = serializedObject.FindProperty("_genlockPixelOffset");
			_propSmpteLevel = serializedObject.FindProperty("_smpteLevel");
			_propKeyerMode = serializedObject.FindProperty("_keyerMode");
			base.Init();
		}

		protected override void LoadSettings()
		{
			_expandGeneral = EditorPrefs.GetBool(SettingsPrefix + "ExpandGeneral", false);
			_expandVideo = EditorPrefs.GetBool(SettingsPrefix + "ExpandVideo", false);
			_expandAudio= EditorPrefs.GetBool(SettingsPrefix + "ExpandAudio", false);
			_expandMisc = EditorPrefs.GetBool(SettingsPrefix + "ExpandMisc", false);
			base.LoadSettings();
		}

		protected override void SaveSettings()
		{
			EditorPrefs.SetBool(SettingsPrefix + "ExpandGeneral", _expandGeneral);
			EditorPrefs.SetBool(SettingsPrefix + "ExpandVideo", _expandVideo);
			EditorPrefs.SetBool(SettingsPrefix + "ExpandAudio", _expandAudio);
			EditorPrefs.SetBool(SettingsPrefix + "ExpandMisc", _expandMisc);
			base.SaveSettings();
		}

		private void DrawKeyerModes()
		{
			int newKeyMode = EditorGUILayout.Popup("Keying Mode", _propKeyerMode.intValue, _propKeyerMode.enumDisplayNames);

			if (_propKeyerMode.intValue != newKeyMode)
			{
				validate = true;
			}

			_propKeyerMode.intValue = newKeyMode;
		}

		private void ValidateKeyerMode()
		{
			bool internal_supported = DeckLinkPlugin.SupportsInternalKeying(_selectedDevice.intValue);
			bool external_supported = DeckLinkPlugin.SupportsExternalKeying(_selectedDevice.intValue);

			if ((DeckLinkOutput.KeyerMode)_propKeyerMode.intValue == DeckLinkOutput.KeyerMode.External)
			{
				if (!external_supported && validate)
				{
					validate = false;
					Debug.LogWarning("External keying mode for DeckLinkOutput component is not supported by the selected DeckLink device");
				}
			}
			else if ((DeckLinkOutput.KeyerMode)_propKeyerMode.intValue == DeckLinkOutput.KeyerMode.Internal)
			{
				if (!internal_supported && validate)
				{
					validate = false;
					Debug.LogWarning("Internal keying mode for DeckLinkOutput component is not supported by the selected DeckLink device");
				}
			}
		}

		protected override bool ModeValid(DeckLinkPlugin.PixelFormat format)
		{
			return DeckLinkOutput.OutputFormatSupported(format);
		}


		protected void DrawGeneral()
		{
			GUILayout.Space(8f);

			if (GUILayout.Button("General", EditorStyles.toolbarButton))
			{
				_expandGeneral = !_expandGeneral;
			}

			if (_expandGeneral)
			{
				GUILayout.BeginVertical("box");

				EditorGUILayout.PropertyField(_deviceProfile);
				EditorGUILayout.PropertyField(_playOnStart);


#if FEATURE_SYNCGROUPS_WIP
				EditorGUILayout.PropertyField(_propUseSyncGroup);
				EditorGUI.BeginDisabledGroup(!_propUseSyncGroup.boolValue);
				{
					EditorGUI.indentLevel++;
					EditorGUILayout.PropertyField(_propSyncGroupIndex);
					EditorGUI.indentLevel--;
				}
				EditorGUI.EndDisabledGroup();
#endif

				EditorGUILayout.Space();
				
				EditorGUILayout.PropertyField(_propSyncedtoInput);
				EditorGUILayout.PropertyField(_propSmpteLevel);
				EditorGUILayout.PropertyField(_propGenlockPixelOffset);

				EditorGUILayout.Space();

				EditorGUILayout.PropertyField(_enableAncillaryData);
				EditorGUILayout.PropertyField(_enableTimeCodeCapture);
				
				GUILayout.EndVertical();
			}
		}

		protected void DrawAudio()
		{
			GUILayout.Space(8f);

			if (GUILayout.Button("Audio", EditorStyles.toolbarButton))
			{
				_expandAudio = !_expandAudio;
			}

			if (_expandAudio)
			{
				GUILayout.BeginVertical("box");

				EditorGUILayout.PropertyField(_propOutputAudioSource);
				EditorGUILayout.PropertyField(_propAudioChannels);
				EditorGUILayout.PropertyField(_propAudioBitDepth);
				EditorGUILayout.PropertyField(_propMuteOutputAudio);

				GUILayout.EndVertical();
			}
		}

		protected void DrawVideo()
		{
			GUILayout.Space(8f);

			if (GUILayout.Button("Video", EditorStyles.toolbarButton))
			{
				_expandVideo = !_expandVideo;
			}

			if (_expandVideo)
			{
				GUILayout.BeginVertical("box");

				if (!Application.isPlaying)
				{
					EditorGUILayout.PropertyField(_enable3D);
					EditorGUILayout.PropertyField(_useHdr);
					EditorGUILayout.PropertyField(_colorspaceMode);
					EditorGUILayout.PropertyField(_ignoreAlphaChannel);
					EditorGUILayout.PropertyField(_propBypassGamma);
					DrawKeyerModes();
					GUILayout.Space(8f);
					EditorGUILayout.PropertyField(_propAntiAliasingLevel);
					EditorGUILayout.PropertyField(_propNoCameraMode);
					EditorGUILayout.PropertyField(_propDefaultTexture);
					EditorGUILayout.PropertyField(_propDefaultColour);
					EditorGUILayout.PropertyField(_propCamera);
					EditorGUILayout.PropertyField(_propCameraRightEye);
				}

				GUILayout.EndVertical();
			}
		}

		protected void DrawMisc()
		{
			GUILayout.Space(8f);

			if (GUILayout.Button("Misc", EditorStyles.toolbarButton))
			{
				_expandMisc = !_expandMisc;
			}

			if (_expandMisc)
			{
				GUILayout.BeginVertical("box");

				if (!Application.isPlaying || _camera.Device == null)
				{
					EditorGUILayout.PropertyField(_propBufferBalance);
					EditorGUILayout.PropertyField(_propLowLatencyMode);
				}

				EditorGUILayout.PropertyField(_showExplorer);

				GUILayout.EndVertical();
			}
		}

		public override void OnInspectorGUI()
		{
			if (serializedObject == null)
			{
				Init();
			}

			serializedObject.Update();

			if (!Application.isPlaying)
			{
				EditorGUIUtility.labelWidth = 150;
				DrawDeviceFilters();
				EditorGUIUtility.labelWidth = 175;
				DrawModeFilters(false);
				DrawGeneral();
				DrawVideo();
				DrawAudio();
				DrawMisc();
				DrawPreviewTexture(null);
			}
			else
			{
				DrawMisc();
				DrawPreviewTexture(_camera);
				DrawModeSelection();
			}
			OnInspectorGUI_About();
			serializedObject.ApplyModifiedProperties();
		}

		private void DrawModeSelection()
		{
			GUI.color = Color.white;
			GUILayout.Space(8f);

			if (GUILayout.Button("Change Mode", EditorStyles.toolbarButton))
			{
				_expandModeChange = !_expandModeChange;
			}

			if (_expandModeChange)
			{
				if (_camera != null && _camera.Device != null)
				{
					_modeScroll = GUILayout.BeginScrollView(_modeScroll, GUILayout.Height(384f));
					int selectedModeIndex = -1;
					for (int i = 0; i < _camera.Device.NumOutputModes; i++)
					{
						DeviceMode mode = _camera.Device.OutputModes[i];

						if (mode.Index == _camera.Device.CurrentOutputMode.Index)
						{
							GUI.color = Color.green;
						}

						if (GUILayout.Button("" + i.ToString("D2") + ") " + mode.ModeDescription + " - " + mode.PixelFormatDescription + " - " + mode.Width + "x" + mode.Height))
						{
							selectedModeIndex = i;
						}

						if (mode.Index == _camera.Device.CurrentOutputMode.Index)
						{
							GUI.color = Color.white;
						}
					}

					if (selectedModeIndex >= 0)
					{
						_camera.StopOutput();
						_camera.ModeIndex = selectedModeIndex;
						_camera.Begin();
					}
					GUILayout.EndScrollView();
				}
			}
		}
	}
}
