using RogoDigital;
using RogoDigital.Lipsync;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEditor.Animations;
using UnityEngine;

[CustomEditor(typeof(LipSync))]
public class LipSyncEditor : Editor
{
#pragma warning disable 618

	private LipSync lsTarget;

	private string[] blendables;
	private int markerTab = 0;
	private bool saving = false;
	private string savingName = "";
	private bool savingRelative = false;

	private AnimBool showBoneOptions;
	private AnimBool showPlayOnAwake;
	private AnimBool showFixedFrameRate;

	private LipSyncProject settings;
	private int blendSystemNumber = 0;

	private Texture2D logo;
	private Texture2D presetsIcon;
	private GUIStyle miniLabelDark;

	private SerializedProperty audioSource;
	private SerializedProperty restTime;
	private SerializedProperty restHoldTime;
	private SerializedProperty phonemeCurveGenerationMode;
	private SerializedProperty playOnAwake;
	private SerializedProperty loop;
	private SerializedProperty defaultClip;
	private SerializedProperty defaultDelay;
	private SerializedProperty scaleAudioSpeed;
	private SerializedProperty animationTimingMode;
	private SerializedProperty frameRate;
	private SerializedProperty useBones;
	private SerializedProperty boneUpdateAnimation;
	private SerializedProperty onFinishedPlaying;

	private float versionNumber = 1.501f;

	void OnEnable ()
	{
		logo = (Texture2D)EditorGUIUtility.Load("Rogo Digital/Lipsync/Dark/logo_component.png");

		presetsIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Dark/presets.png");

		if (!EditorGUIUtility.isProSkin)
		{
			logo = (Texture2D)EditorGUIUtility.Load("Rogo Digital/Lipsync/Light/logo_component.png");
			presetsIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Light/presets.png");
		}

		Undo.undoRedoPerformed += OnUndoRedoPerformed;

		lsTarget = (LipSync)target;
		lsTarget.reset += OnEnable;

		if (lsTarget.lastUsedVersion < versionNumber)
		{
			AutoUpdate(lsTarget.lastUsedVersion);
			lsTarget.lastUsedVersion = versionNumber;
		}

		blendSystemNumber = BlendSystemEditor.FindBlendSystems(lsTarget);

		audioSource = serializedObject.FindProperty("audioSource");
		restTime = serializedObject.FindProperty("restTime");
		restHoldTime = serializedObject.FindProperty("restHoldTime");
		phonemeCurveGenerationMode = serializedObject.FindProperty("phonemeCurveGenerationMode");
		playOnAwake = serializedObject.FindProperty("playOnAwake");
		loop = serializedObject.FindProperty("loop");
		defaultClip = serializedObject.FindProperty("defaultClip");
		defaultDelay = serializedObject.FindProperty("defaultDelay");
		scaleAudioSpeed = serializedObject.FindProperty("scaleAudioSpeed");
		animationTimingMode = serializedObject.FindProperty("m_animationTimingMode");
		frameRate = serializedObject.FindProperty("frameRate");
		useBones = serializedObject.FindProperty("useBones");
		boneUpdateAnimation = serializedObject.FindProperty("boneUpdateAnimation");
		onFinishedPlaying = serializedObject.FindProperty("onFinishedPlaying");

		showBoneOptions = new AnimBool(lsTarget.useBones, Repaint);
		showPlayOnAwake = new AnimBool(lsTarget.playOnAwake, Repaint);
		showFixedFrameRate = new AnimBool(lsTarget.animationTimingMode == LipSync.AnimationTimingMode.FixedFrameRate, Repaint);

		if (lsTarget.blendSystem != null)
		{
			if (lsTarget.blendSystem.isReady)
			{
				GetBlendShapes();
				lsTarget.blendSystem.onBlendablesChanged += GetBlendShapes;
				BlendSystemEditor.GetBlendSystemButtons(lsTarget.blendSystem);
			}
		}

		settings = LipSyncEditorExtensions.GetProjectFile();

		// Mark phonemes as invalid
		for (int a = 0; a < lsTarget.phonemes.Count; a++)
		{
			lsTarget.phonemes[a].verified = false;
		}

		// Get phonemes from PhonemeSet
		if (settings.phonemeSet != null)
		{
			for (int a = 0; a < settings.phonemeSet.phonemes.Length; a++)
			{
				bool wasFound = false;
				for (int b = 0; b < lsTarget.phonemes.Count; b++)
				{
					// Verify existing phoneme
					if (lsTarget.phonemes[b].phonemeName == settings.phonemeSet.phonemes[a].name)
					{
						lsTarget.phonemes[b].verified = wasFound = true;
						break;
					}
				}

				if (!wasFound)
				{
					Undo.RecordObject(target, "Add Phoneme");
					// Add new ones
					lsTarget.phonemes.Add(new PhonemeShape(settings.phonemeSet.phonemes[a].name));
				}
			}
		}
	}

	void OnDisable ()
	{
		Undo.undoRedoPerformed -= OnUndoRedoPerformed;
		lsTarget.reset -= OnEnable;

		if (lsTarget.blendSystem != null)
		{
			if (lsTarget.blendSystem.isReady)
			{
				lsTarget.blendSystem.onBlendablesChanged -= GetBlendShapes;
				foreach (Shape shape in lsTarget.phonemes)
				{
					for (int blendable = 0; blendable < shape.weights.Count; blendable++)
					{
						lsTarget.blendSystem.SetBlendableValue(shape.blendShapes[blendable], 0);
					}
				}
			}
		}

		if (LipSyncEditorExtensions.currentToggle > -1 && lsTarget.useBones)
		{
			foreach (Shape shape in lsTarget.phonemes)
			{
				foreach (BoneShape bone in shape.bones)
				{
					if (bone.bone != null)
					{
						bone.bone.localPosition = bone.neutralPosition;
						bone.bone.localEulerAngles = bone.neutralRotation;
						bone.bone.localScale = bone.neutralScale;
					}
				}
			}
		}

		LipSyncEditorExtensions.currentToggle = -1;
	}

	void OnUndoRedoPerformed ()
	{
		if (LipSyncEditorExtensions.oldToggle > -1 && lsTarget.useBones)
		{
			if (markerTab == 0)
			{
				foreach (BoneShape boneshape in lsTarget.phonemes[LipSyncEditorExtensions.oldToggle].bones)
				{
					if (boneshape.bone != null)
					{
						boneshape.bone.localPosition = boneshape.neutralPosition;
						boneshape.bone.localEulerAngles = boneshape.neutralRotation;
						boneshape.bone.localScale = boneshape.neutralScale;
					}
				}
			}
		}

		if (markerTab == 0)
		{
			foreach (PhonemeShape shape in lsTarget.phonemes)
			{
				foreach (int blendable in shape.blendShapes)
				{
					lsTarget.blendSystem.SetBlendableValue(blendable, 0);
				}
			}
		}

		if (LipSyncEditorExtensions.currentToggle > -1)
		{
			if (markerTab == 0)
			{
				for (int b = 0; b < lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].blendShapes.Count; b++)
				{
					lsTarget.blendSystem.SetBlendableValue(lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].blendShapes[b], lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].weights[b]);
				}
			}
		}

		blendSystemNumber = BlendSystemEditor.FindBlendSystems(lsTarget);
	}

	public override void OnInspectorGUI ()
	{
		if (serializedObject == null)
		{
			OnEnable();
		}

		if (miniLabelDark == null)
		{
			miniLabelDark = new GUIStyle(EditorStyles.miniLabel);
			miniLabelDark.normal.textColor = Color.black;
		}

		serializedObject.Update();

		EditorGUI.BeginDisabledGroup(saving);
		Rect fullheight = EditorGUILayout.BeginVertical();

		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		GUILayout.Box(logo, GUIStyle.none);
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();

		EditorGUI.BeginChangeCheck();
		blendSystemNumber = BlendSystemEditor.DrawBlendSystemEditor(lsTarget, blendSystemNumber, "LipSync Lite requires a blend system to function.");

		if (lsTarget.blendSystem != null)
		{
			if (lsTarget.blendSystem.isReady)
			{

				if (blendables == null)
				{
					lsTarget.blendSystem.onBlendablesChanged += GetBlendShapes;
					GetBlendShapes();
				}

				EditorGUILayout.Space();
				EditorGUILayout.PropertyField(audioSource, new GUIContent("Audio Source", "AudioSource to play dialogue from."));

				EditorGUILayout.Space();
				EditorGUILayout.PropertyField(useBones, new GUIContent("Use Bone Transforms", "Allow BoneShapes to be added to phoneme poses. This enables the use of bone based facial animation."));
				showBoneOptions.target = lsTarget.useBones;
				if (LipSyncEditorExtensions.FixedBeginFadeGroup(showBoneOptions.faded))
				{
					EditorGUILayout.PropertyField(boneUpdateAnimation, new GUIContent("Account for Animation", "If true, will calculate relative bone positions/rotations each frame. Improves results when using animation, but will cause errors when not."));
					EditorGUILayout.Space();
				}
				LipSyncEditorExtensions.FixedEndFadeGroup(showBoneOptions.faded);
				EditorGUILayout.Space();
				BlendSystemEditor.DrawBlendSystemButtons(lsTarget.blendSystem);
				int oldTab = markerTab;
				markerTab = 0;

				EditorGUILayout.HelpBox("Upgrade to LipSync Pro to access Emotions & Gestures.", MessageType.Info);
				GUILayout.BeginHorizontal();
				GUILayout.FlexibleSpace();
				EditorGUI.BeginDisabledGroup(true);
				markerTab = GUILayout.Toolbar(markerTab, new GUIContent[] { new GUIContent("Phonemes"), new GUIContent("Emotions"), new GUIContent("Gestures") }, GUILayout.MaxWidth(400), GUILayout.MinHeight(23));
				EditorGUI.EndDisabledGroup();
				Rect presetRect = EditorGUILayout.BeginHorizontal();
				if (GUILayout.Button(new GUIContent(presetsIcon, "Presets"), GUILayout.MaxWidth(32), GUILayout.MinHeight(23)))
				{
					GenericMenu menu = new GenericMenu();

					string[] directories = Directory.GetDirectories(Application.dataPath, "Presets", SearchOption.AllDirectories);

					bool noItems = true;
					foreach (string directory in directories)
					{
						foreach (string file in Directory.GetFiles(directory))
						{
							if (Path.GetExtension(file).ToLower() == ".asset")
							{
								LipSyncPreset preset = AssetDatabase.LoadAssetAtPath<LipSyncPreset>("Assets" + file.Substring((Application.dataPath).Length));
								if (preset != null)
								{
									noItems = false;
									menu.AddItem(new GUIContent(Path.GetFileNameWithoutExtension(file)), false, LoadPreset, file);
								}
							}
						}

						string[] subdirectories = Directory.GetDirectories(directory);
						foreach (string subdirectory in subdirectories)
						{
							foreach (string file in Directory.GetFiles(subdirectory))
							{
								if (Path.GetExtension(file).ToLower() == ".asset")
								{
									LipSyncPreset preset = AssetDatabase.LoadAssetAtPath<LipSyncPreset>("Assets" + file.Substring((Application.dataPath).Length));
									if (preset != null)
									{
										noItems = false;
										menu.AddItem(new GUIContent(Path.GetFileName(subdirectory) + "/" + Path.GetFileNameWithoutExtension(file)), false, LoadPreset, file);
									}
								}
							}
						}
					}

					if (noItems)
						menu.AddDisabledItem(new GUIContent("No Presets Found"));

					menu.AddSeparator("");
					menu.AddItem(new GUIContent("Save New Preset"), false, NewPreset);
					if (AssetDatabase.FindAssets("t:BlendShapePreset").Length > 0)
					{
						menu.AddDisabledItem(new GUIContent("Old-style presets found. Convert them to use."));
					}

					menu.DropDown(presetRect);
				}
				EditorGUILayout.EndHorizontal();

				GUILayout.FlexibleSpace();
				GUILayout.EndHorizontal();

				GUILayout.Space(10);

				if (markerTab != oldTab)
				{
					if (oldTab == 0)
					{
						foreach (PhonemeShape phoneme in lsTarget.phonemes)
						{
							foreach (int shape in phoneme.blendShapes)
							{
								lsTarget.blendSystem.SetBlendableValue(shape, 0);
							}
						}
					}

					if (LipSyncEditorExtensions.currentTarget == lsTarget)
						LipSyncEditorExtensions.currentToggle = -1;
				}

				if (markerTab == 0)
				{
					int a = 0;
					foreach (PhonemeShape phoneme in lsTarget.phonemes)
					{
						if (this.DrawShapeEditor(lsTarget.blendSystem, blendables, lsTarget.useBones, true, phoneme, phoneme.phonemeName + " Phoneme", a, "Phoneme does not exist in the chosen Phoneme Set. You can change Phoneme Sets from the Project Settings."))
						{
							// Delete Phoneme
							Undo.RecordObject(lsTarget, "Delete Phoneme");
							foreach (int blendable in phoneme.blendShapes)
							{
								lsTarget.blendSystem.SetBlendableValue(blendable, 0);
							}

							lsTarget.phonemes.Remove(phoneme);
							LipSyncEditorExtensions.currentToggle = -1;
							LipSyncEditorExtensions.selectedBone = 0;
							EditorUtility.SetDirty(lsTarget.gameObject);
							serializedObject.SetIsDifferentCacheDirty();
							break;
						}
						a++;
					}
				}

				EditorGUILayout.Space();
				GUILayout.Box("General Animation Settings", EditorStyles.boldLabel);
				if (EditorUserBuildSettings.activeBuildTarget == BuildTarget.WebGL)
				{
					if (lsTarget.animationTimingMode == LipSync.AnimationTimingMode.AudioPlayback)
					{
						animationTimingMode.enumValueIndex = 1;
					}
					EditorGUILayout.HelpBox("AudioPlayback timing mode is incompatible with WebGL target.", MessageType.Info);
					animationTimingMode.enumValueIndex = EditorGUILayout.IntPopup("Timing Mode", animationTimingMode.enumValueIndex, new string[] { "CustomTimer", "FixedFrameRate" }, new int[] { 1, 2 });
				}
				else
				{
					EditorGUILayout.PropertyField(animationTimingMode, new GUIContent("Timing Mode", "How animations are sampled: AudioPlayback uses the audioclip, CustomTimer uses a framerate independent timer, FixedFrameRate is framerate dependent."));
				}
				showFixedFrameRate.target = lsTarget.animationTimingMode == LipSync.AnimationTimingMode.FixedFrameRate;
				if (EditorGUILayout.BeginFadeGroup(showFixedFrameRate.faded))
				{
					EditorGUILayout.PropertyField(frameRate, new GUIContent("Frame Rate", "The framerate to play the animation at."));
				}
				EditorGUILayout.EndFadeGroup();
				EditorGUILayout.PropertyField(playOnAwake, new GUIContent("Play On Awake", "If checked, the default clip will play when the script awakes."));
				showPlayOnAwake.target = lsTarget.playOnAwake;
				if (EditorGUILayout.BeginFadeGroup(showPlayOnAwake.faded))
				{
					EditorGUILayout.PropertyField(defaultClip, new GUIContent("Default Clip", "The clip to play on awake."));
					EditorGUILayout.PropertyField(defaultDelay, new GUIContent("Default Delay", "The delay between the scene starting and the clip playing."));
				}
				EditorGUILayout.EndFadeGroup();
				EditorGUILayout.PropertyField(loop, new GUIContent("Loop Clip", "If true, will make any played clip loop when it finishes."));
				EditorGUILayout.PropertyField(scaleAudioSpeed, new GUIContent("Scale Audio Speed", "Whether or not the speed of the audio will be slowed/sped up to match Time.timeScale."));
				EditorGUILayout.Space();
				GUILayout.Box("Phoneme Animation Settings", EditorStyles.boldLabel);
				EditorGUILayout.PropertyField(restTime, new GUIContent("Rest Time", "If there are no phonemes within this many seconds of the previous one, a rest will be inserted."));
				EditorGUILayout.PropertyField(restHoldTime, new GUIContent("Pre-Rest Hold Time", "The time, in seconds, a shape will be held before blending when a rest is inserted."));
				EditorGUILayout.PropertyField(phonemeCurveGenerationMode, new GUIContent("Phoneme Curve Generation Mode", "How tangents are generated for animations. Tight is more accurate, Loose is more natural."));
				EditorGUILayout.Space();

				GUILayout.Space(20);

				EditorGUILayout.PropertyField(onFinishedPlaying);

				if (LipSyncEditorExtensions.oldToggle != LipSyncEditorExtensions.currentToggle && LipSyncEditorExtensions.currentTarget == lsTarget)
				{

					if (LipSyncEditorExtensions.oldToggle > -1)
					{
						if (markerTab == 0)
						{
							if (lsTarget.useBones)
							{
								foreach (BoneShape boneshape in lsTarget.phonemes[LipSyncEditorExtensions.oldToggle].bones)
								{
									if (boneshape.bone != null)
									{
										boneshape.bone.localPosition = boneshape.neutralPosition;
										boneshape.bone.localEulerAngles = boneshape.neutralRotation;
										boneshape.bone.localScale = boneshape.neutralScale;
									}
								}
							}

							foreach (PhonemeShape shape in lsTarget.phonemes)
							{
								foreach (int blendable in shape.blendShapes)
								{
									lsTarget.blendSystem.SetBlendableValue(blendable, 0);
								}
							}
						}
					}

					if (LipSyncEditorExtensions.currentToggle > -1)
					{
						if (markerTab == 0)
						{
							if (lsTarget.useBones)
							{
								foreach (BoneShape boneshape in lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].bones)
								{
									if (boneshape.bone != null)
									{
										boneshape.bone.localPosition = boneshape.endPosition;
										boneshape.bone.localEulerAngles = boneshape.endRotation;
										boneshape.bone.localScale = boneshape.endScale;
									}
								}
							}

							for (int b = 0; b < lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].blendShapes.Count; b++)
							{
								lsTarget.blendSystem.SetBlendableValue(lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].blendShapes[b], lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].weights[b]);
							}
						}
					}

					LipSyncEditorExtensions.oldToggle = LipSyncEditorExtensions.currentToggle;
				}

				if (EditorGUI.EndChangeCheck())
				{
					if (lsTarget.onSettingsChanged != null)
					{
						lsTarget.onSettingsChanged.Invoke();
					}
				}

				if (GUI.changed)
				{
					if (blendables == null)
					{
						GetBlendShapes();
					}

					if (LipSyncEditorExtensions.currentToggle > -1 && LipSyncEditorExtensions.currentTarget == lsTarget)
					{
						if (markerTab == 0)
						{
							for (int b = 0; b < lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].blendShapes.Count; b++)
							{
								lsTarget.blendSystem.SetBlendableValue(lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].blendShapes[b], lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].weights[b]);
							}
						}
					}

					EditorUtility.SetDirty(lsTarget);
					serializedObject.SetIsDifferentCacheDirty();
				}
			}
			else
			{
				EditorGUILayout.HelpBox(lsTarget.blendSystem.notReadyMessage, MessageType.Warning);
			}
		}

		EditorGUILayout.EndVertical();
		EditorGUI.EndDisabledGroup();

		if (saving)
		{
			GUI.Box(new Rect(40, fullheight.y + (fullheight.height / 2) - 60, fullheight.width - 80, 140), "", (GUIStyle)"flow node 0");
			GUI.Box(new Rect(50, fullheight.y + (fullheight.height / 2) - 50, fullheight.width - 100, 20), "Create New Preset", EditorStyles.label);
			GUI.Box(new Rect(50, fullheight.y + (fullheight.height / 2) - 20, 180, 20), new GUIContent("Use Relative Bone Transforms", "If true, this preset will store bones relative to their default transformations. This will let you apply the preset to a rig with different bone positions without breaking the overall look."), EditorStyles.label);
			savingRelative = EditorGUI.Toggle(new Rect(240, fullheight.y + (fullheight.height / 2) - 20, 20, 20), savingRelative);

			GUI.Box(new Rect(50, fullheight.y + (fullheight.height / 2) + 5, 80, 20), "Preset Path", EditorStyles.label);
			savingName = EditorGUI.TextField(new Rect(140, fullheight.y + (fullheight.height / 2) + 5, fullheight.width - 290, 20), "", savingName);

			if (GUI.Button(new Rect(fullheight.width - 140, fullheight.y + (fullheight.height / 2) + 5, 80, 20), "Browse"))
			{
				GUI.FocusControl("");
				string newPath = EditorUtility.SaveFilePanelInProject("Choose Preset Location", "New Preset", "asset", "");

				if (newPath != "")
				{
					savingName = newPath.Substring("Assets/".Length);
				}
			}
			if (GUI.Button(new Rect(100, fullheight.y + (fullheight.height / 2) + 40, (fullheight.width / 2) - 110, 25), "Cancel"))
			{
				GUI.FocusControl("");
				savingName = "";
				savingRelative = false;
				saving = false;
			}
			if (GUI.Button(new Rect((fullheight.width / 2) + 10, fullheight.y + (fullheight.height / 2) + 40, (fullheight.width / 2) - 110, 25), "Save"))
			{
				if (!Path.GetDirectoryName(savingName).Contains("Presets"))
				{
					EditorUtility.DisplayDialog("Invalid Path", "Presets must be saved in a folder called Presets, or a subfolder of one.", "OK");
					return;
				}
				else if (!Directory.Exists(Application.dataPath + "/" + Path.GetDirectoryName(savingName)))
				{
					EditorUtility.DisplayDialog("Directory Does Not Exist", "The directory " + Path.GetDirectoryName(savingName) + " does not exist.", "OK");
					return;
				}
				else if (!Path.HasExtension(savingName) || Path.GetExtension(savingName) != ".asset")
				{
					savingName = Path.GetDirectoryName(savingName) + "/" + Path.GetFileNameWithoutExtension(savingName) + ".asset";
				}

				LipSyncPreset preset = CreateInstance<LipSyncPreset>();
				preset.isRelative = savingRelative;
				preset.phonemeShapes = new LipSyncPreset.PhonemeShapeInfo[lsTarget.phonemes.Count];

				// Add phonemes
				for (int p = 0; p < lsTarget.phonemes.Count; p++)
				{
					LipSyncPreset.PhonemeShapeInfo phonemeInfo = new LipSyncPreset.PhonemeShapeInfo();
					phonemeInfo.phonemeName = lsTarget.phonemes[p].phonemeName;
					phonemeInfo.blendables = new LipSyncPreset.BlendableInfo[lsTarget.phonemes[p].blendShapes.Count];
					phonemeInfo.bones = new LipSyncPreset.BoneInfo[lsTarget.phonemes[p].bones.Count];

					// Add blendables
					for (int b = 0; b < lsTarget.phonemes[p].blendShapes.Count; b++)
					{
						LipSyncPreset.BlendableInfo blendable = new LipSyncPreset.BlendableInfo();
						blendable.blendableNumber = lsTarget.phonemes[p].blendShapes[b];
						blendable.blendableName = blendables[lsTarget.phonemes[p].blendShapes[b]];
						blendable.weight = lsTarget.phonemes[p].weights[b];

						phonemeInfo.blendables[b] = blendable;
					}

					// Add bones
					for (int b = 0; b < lsTarget.phonemes[p].bones.Count; b++)
					{
						LipSyncPreset.BoneInfo bone = new LipSyncPreset.BoneInfo();
						bone.name = lsTarget.phonemes[p].bones[b].bone.name;
						bone.lockPosition = lsTarget.phonemes[p].bones[b].lockPosition;
						bone.lockRotation = lsTarget.phonemes[p].bones[b].lockRotation;

						if (savingRelative)
						{
							bone.localPosition = lsTarget.phonemes[p].bones[b].neutralPosition - lsTarget.phonemes[p].bones[b].endPosition;
							bone.localRotation = lsTarget.phonemes[p].bones[b].neutralRotation - lsTarget.phonemes[p].bones[b].endRotation;
						}
						else
						{
							bone.localPosition = lsTarget.phonemes[p].bones[b].endPosition;
							bone.localRotation = lsTarget.phonemes[p].bones[b].endRotation;
						}

						string path = "";
						Transform level = lsTarget.phonemes[p].bones[b].bone.parent;
						while (level != null)
						{
							path += level.name + "/";
							level = level.parent;
						}
						bone.path = path;

						phonemeInfo.bones[b] = bone;
					}

					preset.phonemeShapes[p] = phonemeInfo;
				}

				AssetDatabase.CreateAsset(preset, "Assets/" + savingName);
				AssetDatabase.Refresh();
				savingName = "";
				saving = false;
			}
		}

		serializedObject.ApplyModifiedProperties();
	}

	void OnSceneGUI ()
	{
		if (markerTab == 0 && LipSyncEditorExtensions.currentToggle >= 0)
		{
			Handles.BeginGUI();
			if (LipSyncEditorExtensions.currentToggle < settings.phonemeSet.guideImages.Length)
				GUI.Box(new Rect(Screen.width - 256, Screen.height - 246, 256, 256), settings.phonemeSet.guideImages[LipSyncEditorExtensions.currentToggle], GUIStyle.none);
			Handles.EndGUI();
		}

		// Bone Handles
		if (lsTarget.useBones && LipSyncEditorExtensions.currentToggle >= 0 && LipSyncEditorExtensions.currentTarget == lsTarget)
		{
			BoneShape bone = null;
			if (markerTab == 0)
			{
				if (LipSyncEditorExtensions.selectedBone < lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].bones.Count && lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].bones.Count > 0)
				{
					bone = lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].bones[LipSyncEditorExtensions.selectedBone];
				}
				else
				{
					return;
				}
			}

			if (bone.bone == null)
				return;

			if (Tools.current == Tool.Move)
			{
				Undo.RecordObject(bone.bone, "Move");

				Vector3 change = Handles.PositionHandle(bone.bone.position, bone.bone.rotation);
				if (change != bone.bone.position)
				{
					bone.bone.position = change;
					bone.endPosition = bone.bone.localPosition;
				}
			}
			else if (Tools.current == Tool.Rotate)
			{
				Undo.RecordObject(bone.bone, "Rotate");
				Quaternion change = Handles.RotationHandle(bone.bone.rotation, bone.bone.position);
				if (change != bone.bone.rotation)
				{
					bone.bone.rotation = change;
					bone.endRotation = bone.bone.localEulerAngles;
				}
			}
			else if (Tools.current == Tool.Scale)
			{
				Undo.RecordObject(bone.bone, "Scale");
				Vector3 change = Handles.ScaleHandle(bone.bone.localScale, bone.bone.position, bone.bone.rotation, HandleUtility.GetHandleSize(bone.bone.position));
				if (change != bone.bone.localScale)
				{
					bone.bone.localScale = change;
					bone.endScale = bone.bone.localScale;
				}
			}

		}
	}

	void LoadPreset (object data)
	{
		string file = (string)data;
		if (file.EndsWith(".asset", true, null))
		{
			LipSyncPreset preset = AssetDatabase.LoadAssetAtPath<LipSyncPreset>("Assets" + file.Substring((Application.dataPath).Length));

			if (preset != null)
			{
				List<PhonemeShape> newPhonemes = new List<PhonemeShape>();

				// Phonemes
				for (int shape = 0; shape < preset.phonemeShapes.Length; shape++)
				{
					string phonemeName = preset.phonemeShapes[shape].phonemeName;

					if (string.IsNullOrEmpty(phonemeName))
						phonemeName = preset.phonemeShapes[shape].phoneme.ToString();

					newPhonemes.Add(new PhonemeShape(phonemeName));

					for (int blendable = 0; blendable < preset.phonemeShapes[shape].blendables.Length; blendable++)
					{
						int finalBlendable = preset.FindBlendable(preset.phonemeShapes[shape].blendables[blendable], lsTarget.blendSystem);
						if (finalBlendable >= 0)
						{
							newPhonemes[shape].blendShapes.Add(finalBlendable);
							newPhonemes[shape].weights.Add(preset.phonemeShapes[shape].blendables[blendable].weight);
							newPhonemes[shape].blendableNames.Add(lsTarget.blendSystem.GetBlendables()[finalBlendable]);
						}
					}

					for (int bone = 0; bone < preset.phonemeShapes[shape].bones.Length; bone++)
					{
						var b = preset.FindBone(preset.phonemeShapes[shape].bones[bone], lsTarget.transform);

						if (b)
						{
							BoneShape newBone = new BoneShape();
							newBone.bone = b;
							newBone.SetNeutral();
							newBone.lockPosition = preset.phonemeShapes[shape].bones[bone].lockPosition;
							newBone.lockRotation = preset.phonemeShapes[shape].bones[bone].lockRotation;

							if (preset.isRelative)
							{
								newBone.endPosition = newBone.neutralPosition - preset.phonemeShapes[shape].bones[bone].localPosition;
								newBone.endRotation = newBone.neutralRotation - preset.phonemeShapes[shape].bones[bone].localRotation;
							}
							else
							{
								newBone.endPosition = preset.phonemeShapes[shape].bones[bone].localPosition;
								newBone.endRotation = preset.phonemeShapes[shape].bones[bone].localRotation;
							}

							newPhonemes[shape].bones.Add(newBone);
						}
					}
				}

				lsTarget.phonemes = newPhonemes;

				for (int bShape = 0; bShape < lsTarget.blendSystem.blendableCount; bShape++)
				{
					lsTarget.blendSystem.SetBlendableValue(bShape, 0);
				}

				if (markerTab == 0)
				{
					if (LipSyncEditorExtensions.currentToggle >= 0)
					{
						int b = 0;
						foreach (int shape in lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].blendShapes)
						{
							lsTarget.blendSystem.SetBlendableValue(shape, lsTarget.phonemes[LipSyncEditorExtensions.currentToggle].weights[b]);
							b++;
						}
					}
				}
			}
		}
	}

	void NewPreset ()
	{
		saving = true;
		savingRelative = false;
		savingName = "Rogo Digital/LipSync Lite/Presets/New Preset.asset";
	}

	void GetBlendShapes ()
	{
		if (lsTarget.blendSystem.isReady)
		{
			blendables = lsTarget.blendSystem.GetBlendables();
		}
	}

	private void AutoUpdate (float oldVersion)
	{
		// Used for additional future-proofing
		if (oldVersion < 0.6f)
		{
			// Update new rest time values
			if (EditorUtility.DisplayDialog("LipSync has been updated.", "This character was last used with an old version of LipSync prior to 0.6. The recommended values for Rest Time and Pre-Rest Hold Time have been changed to 0.2 and 0.4 respectively. Do you want to change these values automatically?", "Yes", "No"))
			{
				lsTarget.restTime = 0.2f;
				lsTarget.restHoldTime = 0.4f;
			}
		}

		if (oldVersion < 1.3f)
		{
			// Switch to new phoneme format
			for (int p = 0; p < lsTarget.phonemes.Count; p++)
			{
				lsTarget.phonemes[p].phonemeName = lsTarget.phonemes[p].phoneme.ToString();
			}
		}
	}
}