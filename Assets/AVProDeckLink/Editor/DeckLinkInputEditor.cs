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
	[CustomEditor(typeof(DeckLinkInput))]
	public class DeckLinkInputEditor : DeckLinkEditor
	{
		private DeckLinkInput _camera;
		protected SerializedProperty _flipx;
		protected SerializedProperty _flipy;
		protected SerializedProperty _propInputBufferCount;
		protected SerializedProperty _propInputReadBufferCount;
		protected SerializedProperty _propAutoDeinterlace;
		protected SerializedProperty _propAutoDetectMode;
		protected SerializedProperty _propAudioVolume;
		protected SerializedProperty _propMuteAudio;
		protected SerializedProperty _propBypassGamma;

		protected static bool _expandGeneral = false;
		protected static bool _expandVideo = false;
		protected static bool _expandAudio = false;
		protected static bool _expandMisc = false;


		void OnEnable()
		{
			LoadSettings();
			Init();
		}

		protected override void Init()
		{
			_camera = (this.target) as DeckLinkInput;
			_isInput = true;
			_propAutoDeinterlace = serializedObject.FindProperty("_autoDeinterlace");
			_propAutoDetectMode = serializedObject.FindProperty("_autoDetectMode");
			_propAudioVolume = serializedObject.FindProperty("_audioVolume");
			_propMuteAudio = serializedObject.FindProperty("_muteAudio");
			_propBypassGamma = serializedObject.FindProperty("_bypassGamma");
			_propInputBufferCount = serializedObject.FindProperty("_inputBufferCount");
			_propInputReadBufferCount = serializedObject.FindProperty("_inputBufferReadCount");
			_flipx = serializedObject.FindProperty("_flipX");
			_flipy = serializedObject.FindProperty("_flipY");
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

		protected override bool ModeValid(DeckLinkPlugin.PixelFormat format)
		{
			return FormatConverter.InputFormatSupported(format);
		}

		private void DrawFlipCheckboxes()
		{
			_flipx.boolValue = EditorGUILayout.Toggle("Flip X", _flipx.boolValue);
			_camera.FlipX = _flipx.boolValue;

			_flipy.boolValue = EditorGUILayout.Toggle("Flip Y", _flipy.boolValue);
			_camera.FlipY = _flipy.boolValue;
		}

		private void DrawBufferStats()
		{
			if (_camera.Device == null || !_camera.Device.IsStreamingInput)
			{
				EditorGUILayout.PropertyField(_propInputBufferCount);
				EditorGUILayout.PropertyField(_propInputReadBufferCount);
				_propInputReadBufferCount.intValue = Mathf.Clamp(_propInputReadBufferCount.intValue, 0, _propInputBufferCount.intValue - 1);
			}
			else if (_camera.Device != null && _camera.Device.IsStreamingInput)
			{
				int totalBufferCount;
				int readBufferCount;
				int usedBufferCount;
				int pendingBufferCount;
				if (_camera.Device.GetInputBufferStats(out totalBufferCount, out readBufferCount, out usedBufferCount, out pendingBufferCount))
				{
					GUILayout.BeginVertical(GUI.skin.box);
					EditorGUILayout.LabelField(_propInputBufferCount.displayName, totalBufferCount.ToString());
					EditorGUILayout.LabelField(_propInputReadBufferCount.displayName, readBufferCount.ToString());
					GUILayout.Label("Used: " + usedBufferCount + " Pending: " + pendingBufferCount.ToString());
					GUILayout.EndVertical();
				}
			}
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

				EditorGUILayout.PropertyField(_enableAncillaryData);
				EditorGUILayout.PropertyField(_enableTimeCodeCapture);

				GUILayout.EndVertical();
			}
		}

		internal bool IsAutoDetectMode()
		{
			return _propAutoDetectMode.boolValue;
		}

		internal void DrawModeFilters_Input()
		{
			EditorGUILayout.PropertyField(_propAutoDetectMode);
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

				EditorGUILayout.PropertyField(_propAudioVolume);
				EditorGUILayout.PropertyField(_propAudioChannels);
				EditorGUILayout.PropertyField(_propAudioBitDepth);
				EditorGUILayout.PropertyField(_propMuteAudio);

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
					EditorGUILayout.PropertyField(_propAutoDeinterlace);
					EditorGUILayout.PropertyField(_ignoreAlphaChannel);
					EditorGUILayout.PropertyField(_propBypassGamma);
				}
				DrawFlipCheckboxes();

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

				DrawBufferStats();
				EditorGUILayout.PropertyField(_showExplorer);

				GUILayout.EndVertical();
			}
		}

		public override void OnInspectorGUI()
		{
			if(serializedObject == null)
			{
				return;
			}

			if (_camera == null)
			{
				Init();
			}

			serializedObject.Update();

			if (!Application.isPlaying || _camera.Device == null)
			{
				EditorGUIUtility.labelWidth = 150;
				DrawDeviceFilters();
				EditorGUIUtility.labelWidth = 175;
				DrawModeFilters(true);
				DrawGeneral();
				DrawVideo();
				DrawAudio();
				DrawMisc();
				DrawPreviewTexture(null);
			}
			else
			{
				DrawVideo();
				DrawMisc();
				DrawPreviewTexture(_camera);
			}
			OnInspectorGUI_About();         
			serializedObject.ApplyModifiedProperties();
		}
	}
}
