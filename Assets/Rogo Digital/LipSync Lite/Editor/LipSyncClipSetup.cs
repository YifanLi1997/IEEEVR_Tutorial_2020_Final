using RogoDigital;
using RogoDigital.Lipsync;

using System;
using System.Collections.Generic;
using System.IO;
using System.Xml;
using UnityEditor;
using UnityEditor.AnimatedValues;
using UnityEngine;

public class LipSyncClipSetup : ModalParent
{
	#region Delegates
	public static LipSyncClipEditorMenuDelegate onDrawTopMenuBar;
	public static LipSyncClipEditorMenuItemDelegate onDrawFileMenu;
	public static LipSyncClipEditorMenuItemDelegate onDrawEditMenu;
	public static LipSyncClipEditorMenuItemDelegate onDrawAutoSyncMenu;
	public static LipSyncClipEditorMenuItemDelegate onDrawHelpMenu;
	public static List<RDEditorShortcut.Action> shortcutActions;
	#endregion

	private const float version = 1.501f;
	private const string versionNumber = "Lite 1.501";

	public TemporaryLipSyncData data;

	public float FileLength
	{
		get
		{
			return data.length;
		}
		set
		{
			data.length = value;
			float thumbSize = viewportEnd - viewportStart;
			viewportEnd = Mathf.Clamp(viewportEnd, 0, value);
			viewportStart = Mathf.Clamp(viewportEnd - thumbSize, 0, viewportEnd - 0.5f);
		}
	}

	public List<PhonemeMarker> PhonemeData
	{
		get { return data.phonemeData; }
		set { data.phonemeData = value; }
	}

	public AudioClip Clip
	{
		get { return data.clip; }
		set
		{
			if (data.clip != value)
			{
				data.clip = value;
				if (loadTranscriptFromTxt)
				{
					string path = AssetDatabase.GetAssetPath(data.clip);
					TextAsset text = AssetDatabase.LoadAssetAtPath<TextAsset>(Path.ChangeExtension(path, "txt"));
					if (text)
					{
						data.transcript = text.text;
					}
				}
			}
		}
	}

	public string Transcript
	{
		get { return data.transcript; }
		set { data.transcript = value; }
	}

	private float seekPosition;
	private bool isPlaying = false;
	private bool isPaused = false;
	private bool previewing = false;
	private bool looping = false;

	private Rect oldPos;
	private int waveformHeight;
	private AudioClip oldClip;

	private TimeSpan timeSpan;
	private float oldSeekPosition;
	private float stopTimer = 0;
	private float prevTime = 0;
	private float resetTime = 0;
	private float viewportStart = 0;
	private float viewportEnd = 10;

	private int draggingScrollbar;
	private float scrollbarStartOffset;

	private List<int> selection;
	private int firstSelection;
	private float[] selectionOffsets;
	private float[] sequentialStartOffsets;
	private float[] sequentialEndOffsets;
	private Rect selectionRect = new Rect(0, 0, 0, 0);

	private int copyBufferType;
	private List<object> copyBuffer = new List<object>();

	private int currentMarker = -1;
	private int highlightedMarker = -1;
	private int markerTab = 0;

	private bool dragging = false;

	private int filterMask = -1;
	private string[] phonemeSetNames;

	private float startOffset;
	private float endOffset;
	private float lowSnapPoint;
	private float highSnapPoint;

	private Color nextColor;
	private Color lastColor;

	private string[] markerTypes = new string[] { "Phonemes", "Emotions", "Gestures" };

	private string lastLoad = "";
	public string fileName = "Untitled";
	public bool changed = false;

	#region GUI Images
	private Texture2D playhead_top;
	private Texture2D playhead_line;
	private Texture2D playhead_bottom;
	private Texture2D track_top;
	private Texture2D playIcon;
	private Texture2D stopIcon;
	private Texture2D pauseIcon;
	private Texture2D loopIcon;
	private Texture2D settingsIcon;
	private Texture2D previewIcon;
	private Texture2D windowIcon;

	private Texture2D marker_normal;
	private Texture2D marker_hover;
	private Texture2D marker_selected;
	private Texture2D marker_sustained;
	private Texture2D marker_line;

	private Texture2D preview_bar;
	private Texture2D preview_icon;
	#endregion

	private Rect previewRect;

	#region Editor Settings Variables
	public LipSyncProject settings;
	private bool settingsOpen = false;
	private Vector2 settingsScroll;
	private int settingsTab = 0;

	private bool visualPreview = false;
	private LipSync previewTarget = null;
	public bool previewOutOfDate = true;

	public float defaultPhonemeIntensity;
	public bool defaultUseRandomness;
	public float defaultIntensityRandomness;
	public float defaultBlendableRandomness;
	public float defaultBonePositionRandomness;
	public float defaultBoneRotationRandomness;

	private bool continuousUpdate;
	private bool setViewportOnLoad;
	private bool showExtensionsOnLoad;
	private bool showTimeline;
	private float scrubLength;
	private float volume;

	private bool loadTranscriptFromTxt;

	private RDEditorShortcut[] keyboardShortcuts;
	private bool shortcutsChanged;
	#endregion

	#region EditorWindow Life Cycle Methods
	void OnEnable ()
	{
		//Load Resources;
		playhead_top = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Playhead_top.png");
		playhead_line = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Playhead_middle.png");
		playhead_bottom = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Playhead_bottom.png");

		marker_normal = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/marker.png");
		marker_hover = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/marker-selected.png");
		marker_selected = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/marker-highlight.png");
		marker_sustained = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/marker-sustain.png");
		marker_line = (Texture2D)EditorGUIUtility.Load("Rogo Digital/Shared/white.png");

		preview_bar = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/preview-bar.png");
		preview_icon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/preview-icon.png");

		if (!EditorGUIUtility.isProSkin)
		{
			track_top = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Light/track.png");
			playIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Light/play.png");
			stopIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Light/stop.png");
			pauseIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Light/pause.png");
			loopIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Light/loop.png");
			settingsIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Light/settings.png");
			previewIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Light/eye.png");
		}
		else
		{
			track_top = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Dark/track.png");
			playIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Dark/play.png");
			stopIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Dark/stop.png");
			pauseIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Dark/pause.png");
			loopIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Dark/loop.png");
			settingsIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Dark/settings.png");
			previewIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Dark/eye.png");
		}

		if (data == null)
		{
			data = CreateInstance<TemporaryLipSyncData>();
		}

		settings = LipSyncEditorExtensions.GetProjectFile();

		phonemeSetNames = new string[settings.phonemeSet.phonemes.Length];
		for (int i = 0; i < settings.phonemeSet.phonemes.Length; i++)
		{
			phonemeSetNames[i] = settings.phonemeSet.phonemes[i].name;
		}

		//Get Editor Settings
		continuousUpdate = EditorPrefs.GetBool("LipSync_ContinuousUpdate", true);

		setViewportOnLoad = EditorPrefs.GetBool("LipSync_SetViewportOnLoad", true);
		showTimeline = EditorPrefs.GetBool("LipSync_ShowTimeline", true);
		showExtensionsOnLoad = EditorPrefs.GetBool("LipSync_ShowExtensionsOnLoad", true);
		scrubLength = EditorPrefs.GetFloat("LipSync_ScrubLength", 0.075f);
		volume = EditorPrefs.GetFloat("LipSync_Volume", 1f);

		defaultPhonemeIntensity = EditorPrefs.GetFloat("LipSync_DefaultPhonemeIntensity", 1f);
		defaultUseRandomness = EditorPrefs.GetBool("LipSync_DefaultUseRandomness", false);
		defaultIntensityRandomness = EditorPrefs.GetFloat("LipSync_DefaultIntensityRandomness", 0.1f);
		defaultBlendableRandomness = EditorPrefs.GetFloat("LipSync_DefaultBlendableRandomness", 0.3f);
		defaultBonePositionRandomness = EditorPrefs.GetFloat("LipSync_DefaultBonePositionRandomness", 0.3f);
		defaultBoneRotationRandomness = EditorPrefs.GetFloat("LipSync_DefaultBoneRotationRandomness", 0.3f);

		loadTranscriptFromTxt = EditorPrefs.GetBool("LipSync_LoadTranscriptFromTxt", true);

		// Define Rebindable Actions
		// NOTE: DO NOT ADD ADDITIONAL ACTIONS AT THIS POINT
		// Do this after the "Add Phonemes" section below, otherwise default and/or user-defined shortcuts will have the wrong actions.
		shortcutActions = new List<RDEditorShortcut.Action> {
			new RDEditorShortcut.Action("File/New File", OnNewClick),
			new RDEditorShortcut.Action("File/Open File", OnLoadClick),
			new RDEditorShortcut.Action("File/Import XML", OnXMLImport),
			new RDEditorShortcut.Action("File/Save", OnSaveClick),
			new RDEditorShortcut.Action("File/Save As", OnSaveAsClick),
			new RDEditorShortcut.Action("File/Export (Unitypackage)", OnUnityExport),
			new RDEditorShortcut.Action("File/Export (XML)", OnXMLExport),
			new RDEditorShortcut.Action("Other/Show Project Settings", ShowProjectSettings),
			new RDEditorShortcut.Action("Other/Close Window", Close),
			new RDEditorShortcut.Action("Edit/Select All", SelectAll),
			new RDEditorShortcut.Action("Edit/Select None", SelectNone),
			new RDEditorShortcut.Action("Edit/Invert Selection", InvertSelection),
			new RDEditorShortcut.Action("Edit/Show Clip Settings", ClipSettings),
			new RDEditorShortcut.Action("Obsolete/AutoSync (Default Settings)", () => { Debug.LogWarning("AutoSync (Default Settings) is obsolete."); }),
			new RDEditorShortcut.Action("Obsolete/AutoSync (High Quality Settings)", () => { Debug.LogWarning("AutoSync (High Quality Settings) is obsolete."); }),
			new RDEditorShortcut.Action("AutoSync/Open AutoSync Window", ()=> {}),
			new RDEditorShortcut.Action("AutoSync/AutoSync Batch Process", ()=> {}),
			new RDEditorShortcut.Action("Other/Open Extensions Window", RDExtensionWindow.ShowWindow),
			new RDEditorShortcut.Action("Playback/Seek Backwards 1%", ()=> { seekPosition -= 0.01f; }),
			new RDEditorShortcut.Action("Playback/Seek Forwards 1%", ()=> { seekPosition += 0.01f; }),
			new RDEditorShortcut.Action("Other/Go To Phonemes Tab", ()=> { markerTab = 0; }),
			new RDEditorShortcut.Action("Other/Go To Emotions Tab", ()=> { markerTab = 1; }),
			new RDEditorShortcut.Action("Other/Go To Gestures Tab", ()=> { markerTab = 2; }),
			new RDEditorShortcut.Action("Other/Toggle Settings Page", ()=> { settingsOpen = !settingsOpen; }),
			new RDEditorShortcut.Action("Playback/Play\\Pause", PlayPause),
			new RDEditorShortcut.Action("Playback/Stop", Stop),
			new RDEditorShortcut.Action("Playback/Loop", ()=> { looping = !looping; })
		};

		// Add Phonemes
		for (int p = 0; p < settings.phonemeSet.phonemes.Length; p++)
		{
			int temp = p;
			shortcutActions.Add(new RDEditorShortcut.Action("Insert/Add " + settings.phonemeSet.phonemes[p].name + " Phoneme", () => { PhonemePicked(new object[] { temp, seekPosition }); }));
		}

		// Later-added actions are defined here, to avoid shuffling existing shortcuts around.
		shortcutActions.Add(new RDEditorShortcut.Action("Edit/Copy", CopyMarkers));
		shortcutActions.Add(new RDEditorShortcut.Action("Edit/Paste", () => { PasteMarkers(seekPosition); }));
		shortcutActions.Add(new RDEditorShortcut.Action("AutoSync/Run Default Preset", () => {}));

		// Set Shortcuts
		keyboardShortcuts = RDEditorShortcut.Deserialize("LipSyncLite", shortcutActions, keyboardShortcuts);
		if (keyboardShortcuts == null)
			SetDefaultShortcuts();

		SceneView.onSceneGUIDelegate += OnSceneGUI;

		selection = new List<int>();
		selectionOffsets = new float[0];
		oldPos = this.position;
		oldClip = Clip;
	}

	private void SetDefaultShortcuts ()
	{
		keyboardShortcuts = new RDEditorShortcut[] {
			new RDEditorShortcut(9, KeyCode.A, EventModifiers.Control),
			new RDEditorShortcut(3, KeyCode.S, EventModifiers.Control),
			new RDEditorShortcut(18, KeyCode.Comma, EventModifiers.Shift),
			new RDEditorShortcut(19, KeyCode.Period, EventModifiers.Shift),
			new RDEditorShortcut(27, KeyCode.A, (EventModifiers.Control | EventModifiers.Shift)),
			new RDEditorShortcut(28, KeyCode.E, (EventModifiers.Control | EventModifiers.Shift)),
			new RDEditorShortcut(29, KeyCode.U, (EventModifiers.Control | EventModifiers.Shift)),
			new RDEditorShortcut(30, KeyCode.O, (EventModifiers.Control | EventModifiers.Shift)),
			new RDEditorShortcut(31, KeyCode.C, (EventModifiers.Control | EventModifiers.Shift)),
			new RDEditorShortcut(32, KeyCode.F, (EventModifiers.Control | EventModifiers.Shift)),
			new RDEditorShortcut(33, KeyCode.L, (EventModifiers.Control | EventModifiers.Shift)),
			new RDEditorShortcut(34, KeyCode.M, (EventModifiers.Control | EventModifiers.Shift)),
			new RDEditorShortcut(35, KeyCode.W, (EventModifiers.Control | EventModifiers.Shift)),
			new RDEditorShortcut(36, KeyCode.C, EventModifiers.Control),
			new RDEditorShortcut(37, KeyCode.V, EventModifiers.Control),
		};
	}

	void Update ()
	{
		float deltaTime = Time.realtimeSinceStartup - prevTime;
		prevTime = Time.realtimeSinceStartup;

		if (Clip != null)
		{
			isPlaying = AudioUtility.IsClipPlaying(Clip);
		}

		if (isPlaying && !isPaused)
		{
			if ((seekPosition * (FileLength * (position.width / (viewportEnd - viewportStart)))) > position.width + (viewportStart * (position.width / (viewportEnd - viewportStart))))
			{
				float viewportSeconds = viewportEnd - viewportStart;
				viewportStart = seekPosition * FileLength;
				viewportEnd = viewportStart + viewportSeconds;
			}
			else if ((seekPosition * (FileLength * (position.width / (viewportEnd - viewportStart)))) < viewportStart * (position.width / (viewportEnd - viewportStart)))
			{
				float viewportSeconds = viewportEnd - viewportStart;
				viewportStart = seekPosition * FileLength;
				viewportEnd = viewportStart + viewportSeconds;
			}
		}

		//Check for clip change
		if (oldClip != Clip)
		{
			Undo.RecordObject(this, "Change AudioClip");
			oldClip = Clip;
			if (setViewportOnLoad)
			{
				if (Clip)
					FileLength = Clip.length;
				viewportEnd = FileLength;
				viewportStart = 0;
			}
		}

		//Check for resize;
		if (oldPos.width != this.position.width || oldPos.height != this.position.height)
		{
			oldPos = this.position;
		}

		//Check for Seek Position change
		if (oldSeekPosition != seekPosition)
		{
			oldSeekPosition = seekPosition;

			if (!isPlaying || isPaused)
			{
				if (!previewing && Clip != null)
				{
					AudioUtility.PlayClip(Clip);
				}
				previewing = true;
				stopTimer = scrubLength;
				prevTime = Time.realtimeSinceStartup;
				resetTime = seekPosition;
			}

			if (Clip)
				AudioUtility.SetClipSamplePosition(Clip, (int)(seekPosition * AudioUtility.GetSampleCount(Clip)));
		}

		if (isPlaying && !isPaused && Clip != null && focusedWindow == this || continuousUpdate && focusedWindow == this)
		{
			this.Repaint();
		}

		if (Clip != null)
		{
			seekPosition = AudioUtility.GetClipPosition(Clip) / FileLength;

			if (seekPosition >= 0.999f && looping)
			{
				AudioUtility.PlayClip(Clip);
				AudioUtility.SetClipSamplePosition(Clip, 0);
			}
		}
		else if (isPlaying && !isPaused)
		{
			seekPosition += deltaTime / FileLength;

			if (seekPosition >= 1)
			{
				if (looping)
				{
					seekPosition = 0;
					oldSeekPosition = 0;
				}
				else
				{
					isPlaying = false;
					seekPosition = 0;
				}
			}
		}

		oldSeekPosition = seekPosition;

		if (previewing)
		{

			stopTimer -= deltaTime;

			if (stopTimer <= 0)
			{
				previewing = false;
				isPaused = true;
				seekPosition = resetTime;
				oldSeekPosition = seekPosition;
				if (Clip != null)
				{
					AudioUtility.PauseClip(Clip);
					AudioUtility.SetClipSamplePosition(Clip, (int)(seekPosition * AudioUtility.GetSampleCount(Clip)));
				}
			}
		}

		if (isPlaying && !isPaused && visualPreview || previewing && visualPreview || previewOutOfDate && visualPreview)
		{
			UpdatePreview(seekPosition);
		}
	}

	void OnDisable ()
	{
		if (previewTarget != null)
		{
			UpdatePreview(0);
			previewTarget = null;
		}
	}

	void OnDestroy ()
	{
		// Save file and offer to save changes.
		AudioUtility.StopAllClips();
		SceneView.onSceneGUIDelegate -= OnSceneGUI;

		if (changed)
		{
			string oldName = fileName;
			string oldLastLoad = lastLoad;
			float localOldSeekPosition = seekPosition;
			;
			SaveFile(settings, "Assets/LIPSYNC_AUTOSAVE.asset", false, Transcript, FileLength, PhonemeData.ToArray(), Clip);
			int choice = EditorUtility.DisplayDialogComplex("Save Changes", "You have made changes to the current file, do you want to save them before closing?", "Yes", "No", "Cancel");

			if (choice == 0)
			{
				OnSaveClick();
				AssetDatabase.DeleteAsset("Assets/LIPSYNC_AUTOSAVE.asset");
			}
			else if (choice == 1)
			{
				AssetDatabase.DeleteAsset("Assets/LIPSYNC_AUTOSAVE.asset");
			}
			else
			{
				ShowWindow("Assets/LIPSYNC_AUTOSAVE.asset", true, oldName, oldLastLoad, markerTab, localOldSeekPosition);
			}
		}
	}
	#endregion

	#region GUI Rendering
	void OnSceneGUI (SceneView sceneView)
	{
		// Draw overlay when realtime preview is enabled
		if (visualPreview)
		{
			Camera cam = sceneView.camera;
			Handles.BeginGUI();

			Rect bottom = new Rect(0, cam.pixelHeight - 3, cam.pixelWidth, 3);
			GUI.DrawTexture(bottom, preview_bar);

			GUI.DrawTexture(new Rect(cam.pixelWidth - 256, cam.pixelHeight - 64, 256, 64), preview_icon);
			Handles.EndGUI();
		}
	}

	public override void OnModalGUI ()
	{
		// Create Styles
		GUIStyle centeredStyle = new GUIStyle(EditorStyles.whiteLabel);
		centeredStyle.alignment = TextAnchor.MiddleCenter;


		#region Top Menu
		//Toolbar
		Rect topToolbarRect = EditorGUILayout.BeginHorizontal();
		GUI.Box(topToolbarRect, "", EditorStyles.toolbar);
		Rect fileRect = EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("File", EditorStyles.toolbarDropDown, GUILayout.Width(60)))
		{
			GenericMenu fileMenu = new GenericMenu();

			fileMenu.AddItem(new GUIContent("New File"), false, OnNewClick);
			fileMenu.AddItem(new GUIContent("Open File"), false, OnLoadClick);
			fileMenu.AddItem(new GUIContent("Import XML"), false, OnXMLImport);
			fileMenu.AddSeparator("");
			if (PhonemeData.Count > 0)
			{
				fileMenu.AddItem(new GUIContent("Save"), false, OnSaveClick);
				fileMenu.AddItem(new GUIContent("Save As"), false, OnSaveAsClick);
				fileMenu.AddItem(new GUIContent("Export"), false, OnUnityExport);
				fileMenu.AddItem(new GUIContent("Export XML"), false, OnXMLExport);
			}
			else
			{
				fileMenu.AddDisabledItem(new GUIContent("Save"));
				fileMenu.AddDisabledItem(new GUIContent("Save As"));
				fileMenu.AddDisabledItem(new GUIContent("Export"));
				fileMenu.AddDisabledItem(new GUIContent("Export XML"));
			}
			fileMenu.AddSeparator("");

			if (onDrawFileMenu != null)
			{
				onDrawFileMenu.Invoke(this, fileMenu);
				fileMenu.AddSeparator("");
			}

			fileMenu.AddItem(new GUIContent("Project Settings"), false, ShowProjectSettings);
			fileMenu.AddSeparator("");
			fileMenu.AddItem(new GUIContent("Exit"), false, Close);
			fileMenu.DropDown(fileRect);
		}
		GUILayout.EndHorizontal();
		Rect editRect = EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("Edit", EditorStyles.toolbarDropDown, GUILayout.Width(60)))
		{
			GenericMenu editMenu = new GenericMenu();
			if (selection.Count > 0)
			{
				editMenu.AddItem(new GUIContent("Copy"), false, CopyMarkers);
			}
			else
			{
				editMenu.AddDisabledItem(new GUIContent("Copy"));
			}
			if (copyBuffer.Count > 0)
			{
				editMenu.AddItem(new GUIContent("Paste"), false, () => { PasteMarkers(seekPosition); });
			}
			else
			{
				editMenu.AddDisabledItem(new GUIContent("Paste"));
			}
			editMenu.AddSeparator("");
			editMenu.AddItem(new GUIContent("Select All"), false, SelectAll);
			editMenu.AddItem(new GUIContent("Select None"), false, SelectNone);
			editMenu.AddItem(new GUIContent("Invert Selection"), false, InvertSelection);
			editMenu.AddSeparator("");
			if (markerTab == 0)
			{
				if (Clip != null)
				{
					editMenu.AddItem(new GUIContent("Set Intensity From Volume"), false, SetIntensitiesVolume);
				}
				else
				{
					editMenu.AddDisabledItem(new GUIContent("Set Intensity From Volume"));
				}

				editMenu.AddItem(new GUIContent("Reset Intensities"), false, ResetIntensities);
			}

			editMenu.AddSeparator("");
			if (markerTab == 0 || markerTab == 1)
				editMenu.AddItem(new GUIContent("Default Marker Settings"), false, DefaultMarkerSettings);
			editMenu.AddItem(new GUIContent("Clip Settings"), false, ClipSettings);
			if (onDrawEditMenu != null)
			{
				editMenu.AddSeparator("");
				onDrawEditMenu.Invoke(this, editMenu);
			}

			editMenu.DropDown(editRect);
		}
		GUILayout.EndHorizontal();
		Rect autoRect = EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("AutoSync", EditorStyles.toolbarDropDown, GUILayout.Width(70)))
		{
			GenericMenu autoMenu = new GenericMenu();

			autoMenu.AddDisabledItem(new GUIContent("Upgrade to LipSync Pro to access AutoSync"));
			autoMenu.AddDisabledItem(new GUIContent("Setup"));
			autoMenu.AddDisabledItem(new GUIContent("View Guide"));
			autoMenu.AddSeparator("");
			autoMenu.AddDisabledItem(new GUIContent("Run Default"));
			autoMenu.AddDisabledItem(new GUIContent("Presets"));
			autoMenu.AddDisabledItem(new GUIContent("Modules"));
			autoMenu.AddSeparator("");
			autoMenu.AddDisabledItem(new GUIContent("Open AutoSync Window"));
			autoMenu.AddDisabledItem(new GUIContent("Batch Process"));

			if (onDrawAutoSyncMenu != null)
			{
				autoMenu.AddSeparator("");
				onDrawAutoSyncMenu.Invoke(this, autoMenu);
			}

			autoMenu.DropDown(autoRect);
		}
		GUILayout.EndHorizontal();
		if (onDrawTopMenuBar != null)
			onDrawTopMenuBar.Invoke(this);
		Rect helpRect = EditorGUILayout.BeginHorizontal();
		if (GUILayout.Button("Help", EditorStyles.toolbarDropDown, GUILayout.Width(60)))
		{
			GenericMenu helpMenu = new GenericMenu();

			helpMenu.AddDisabledItem(new GUIContent("LipSync " + versionNumber));
			helpMenu.AddDisabledItem(new GUIContent("© Rogo Digital " + DateTime.Now.Year.ToString()));
			helpMenu.AddSeparator("");
			helpMenu.AddItem(new GUIContent("Get LipSync Pro"), false, OpenURL, "http://u3d.as/cag");
			helpMenu.AddItem(new GUIContent("Get LipSync Extensions"), false, RDExtensionWindow.ShowWindowGeneric, "LipSync_Lite");
			helpMenu.AddSeparator("");
			helpMenu.AddItem(new GUIContent("Documentation"), false, OpenURL, "https://lipsync.rogodigital.com/documentation");
			helpMenu.AddItem(new GUIContent("Forum Thread"), false, OpenURL, "http://forum.unity3d.com/threads/released-lipsync-and-eye-controller-lipsyncing-and-facial-animation-tools.309324/");
			if (onDrawHelpMenu != null)
			{
				helpMenu.AddSeparator("");
				onDrawHelpMenu.Invoke(this, helpMenu);
				helpMenu.AddSeparator("");
			}
			helpMenu.DropDown(helpRect);
		}
		GUILayout.EndHorizontal();
		GUILayout.FlexibleSpace();
		if (changed == true)
		{
			GUILayout.Box(fileName + "*", EditorStyles.label);
		}
		else
		{
			GUILayout.Box(fileName, EditorStyles.label);
		}
		GUILayout.FlexibleSpace();

		settingsOpen = GUILayout.Toggle(settingsOpen, new GUIContent(settingsIcon, "Settings"), EditorStyles.toolbarButton, GUILayout.MaxWidth(40));

		bool newPreviewState = GUILayout.Toggle(visualPreview, new GUIContent(previewIcon, "Realtime Preview"), EditorStyles.toolbarButton, GUILayout.MaxWidth(40));
		if (visualPreview != newPreviewState)
		{
			GenericMenu previewMenu = new GenericMenu();

			previewMenu.AddDisabledItem(new GUIContent("Choose a target"));

			LipSync[] targets = GameObject.FindObjectsOfType<LipSync>();

			previewMenu.AddItem(new GUIContent("No Preview"), !visualPreview, TargetChosen, null);
			foreach (LipSync t in targets)
			{
				previewMenu.AddItem(new GUIContent(t.name), previewTarget == t ? true : false, TargetChosen, t);
			}

			previewMenu.ShowAsContext();
		}

		GUILayout.Space(20);
		GUILayout.Box(versionNumber, EditorStyles.label);
		GUILayout.Box("", EditorStyles.toolbar);
		EditorGUILayout.EndHorizontal();
		#endregion

		// Keyboard Shortcuts
		if (Event.current.type == EventType.KeyUp)
		{
			Event e = Event.current;

			for (int i = 0; i < keyboardShortcuts.Length; i++)
			{
				if (e.keyCode == keyboardShortcuts[i].key && e.modifiers == keyboardShortcuts[i].modifiers)
				{
					shortcutActions[keyboardShortcuts[i].action].action.Invoke();
					e.Use();
					break;
				}
			}
		}

		#region Settings Screen
		if (settingsOpen)
		{
			//Settings Screen
			GUILayout.Space(10);
			LipSyncEditorExtensions.BeginPaddedHorizontal(20);
			settingsTab = GUILayout.Toolbar(settingsTab, new string[] { "General", "AutoSync", "Keyboard Shortcuts" }, GUILayout.MaxWidth(700));
			LipSyncEditorExtensions.EndPaddedHorizontal(20);
			settingsScroll = GUILayout.BeginScrollView(settingsScroll);
			GUILayout.Space(10);
			switch (settingsTab)
			{
				case 0:
					GUILayout.Box("General Settings", EditorStyles.boldLabel);
					bool oldUpdate = continuousUpdate;
					continuousUpdate = GUILayout.Toggle(continuousUpdate, new GUIContent("Continuous Update", "Whether to update the window every frame. This makes editing more responsive, but may be taxing on low-powered systems."));
					if (oldUpdate != continuousUpdate)
					{
						EditorPrefs.SetBool("LipSync_ContinuousUpdate", continuousUpdate);
					}
					bool oldLoadTranscript = loadTranscriptFromTxt;
					loadTranscriptFromTxt = GUILayout.Toggle(loadTranscriptFromTxt, new GUIContent("Load Transcripts from .Txt", "Should the clip editor look for a transcript in a matching .txt file next to the audio clip when the clip is changed?"));
					if (oldLoadTranscript != loadTranscriptFromTxt)
					{
						EditorPrefs.SetBool("LipSync_LoadTranscriptFromTxt", loadTranscriptFromTxt);
					}

					bool oldSetViewportOnLoad = setViewportOnLoad;
					setViewportOnLoad = GUILayout.Toggle(setViewportOnLoad, new GUIContent("Set Viewport on File Load", "Whether to set the viewport to show the entire clip when a new file is loaded."));
					if (oldSetViewportOnLoad != setViewportOnLoad)
					{
						EditorPrefs.SetBool("LipSync_SetViewportOnLoad", setViewportOnLoad);
					}

					bool oldShowTimeline = showTimeline;
					showTimeline = GUILayout.Toggle(showTimeline, new GUIContent("Show Time Markers", "Whether to show time markers under the timeline."));
					if (oldShowTimeline != showTimeline)
					{
						EditorPrefs.SetBool("LipSync_ShowTimeline", showTimeline);
					}

					float oldScrubLength = scrubLength;
					scrubLength = EditorGUILayout.FloatField(new GUIContent("Scrubbing Preview Length", "The duration, in seconds, the clip will be played for when scrubbing."), scrubLength, GUILayout.MaxWidth(400));
					if (oldScrubLength != scrubLength)
					{
						EditorPrefs.SetFloat("LipSync_ScrubLength", scrubLength);
					}

					float oldVolume = volume;
					volume = EditorGUILayout.Slider(new GUIContent("Preview Volume"), volume, 0, 1, GUILayout.MaxWidth(400));
					if (oldVolume != volume)
					{
						EditorPrefs.SetFloat("LipSync_Volume", volume);
						AudioUtility.SetVolume(volume);
					}
					GUILayout.Space(10);
					bool oldShowExtensionsOnLoad = showExtensionsOnLoad;
					showExtensionsOnLoad = GUILayout.Toggle(showExtensionsOnLoad, new GUIContent("Show Extensions Window", "Whether to automatically dock an extensions window to this one when it is opened."));
					if (oldShowExtensionsOnLoad != showExtensionsOnLoad)
					{
						EditorPrefs.SetBool("LipSync_ShowExtensionsOnLoad", showExtensionsOnLoad);
					}
					break;
				case 1:
					GUILayout.Box("General AutoSync Settings", EditorStyles.boldLabel);
					EditorGUILayout.HelpBox("AutoSync features are only available in LipSync Pro.", MessageType.Info);
					break;
				case 2:
					GUILayout.Box("Keyboard Shortcut Rebinding", EditorStyles.boldLabel);
					GUILayout.Space(7);
					if (shortcutsChanged)
						EditorGUILayout.HelpBox("You have made changes to the keyboard shortcuts. Press Save Shortcuts to avoid losing them.", MessageType.Warning);
					GUILayout.Space(7);
					if (keyboardShortcuts.Length == 0)
						GUILayout.Box("No Keyboard Shortcuts!", EditorStyles.centeredGreyMiniLabel);

					for (int i = 0; i < keyboardShortcuts.Length; i++)
					{
						Rect lineRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(25));
						if (i % 2 == 0)
						{
							GUI.Box(lineRect, "", (GUIStyle)"hostview");
						}
						GUILayout.Space(10);
						GUILayout.Label("Action");
						EditorGUI.BeginChangeCheck();
						keyboardShortcuts[i].action = EditorGUILayout.Popup(keyboardShortcuts[i].action, Array.ConvertAll(shortcutActions.ToArray(), item => (string)item));
						GUILayout.FlexibleSpace();
						GUILayout.Label("Shortcut");
						//todo: Clean this up, it feels hacky.
#if UNITY_2018_3_OR_NEWER
						keyboardShortcuts[i].modifiers = (EventModifiers)((int)(EventModifiers)EditorGUILayout.EnumFlagsField((EventModifiers)((int)keyboardShortcuts[i].modifiers << 1)) >> 1);
#else
						keyboardShortcuts[i].modifiers = (EventModifiers)((int)(EventModifiers)EditorGUILayout.EnumMaskField((EventModifiers)((int)keyboardShortcuts[i].modifiers << 1)) >> 1);
#endif
						GUILayout.Space(5);
						keyboardShortcuts[i].key = (KeyCode)EditorGUILayout.EnumPopup(keyboardShortcuts[i].key);
						if (EditorGUI.EndChangeCheck())
						{
							shortcutsChanged = true;
						}
						GUILayout.Space(5);
						if (GUILayout.Button("X"))
						{
							RDEditorShortcut[] newArray = new RDEditorShortcut[keyboardShortcuts.Length - 1];
							int b = 0;
							for (int a = 0; a < keyboardShortcuts.Length; a++)
							{
								if (a != i)
								{
									newArray[b] = keyboardShortcuts[a];
									b++;
								}
							}
							shortcutsChanged = true;
							keyboardShortcuts = newArray;
							break;
						}
						GUILayout.Space(10);
						EditorGUILayout.EndHorizontal();
					}
					GUILayout.Space(5);
					EditorGUILayout.BeginHorizontal();
					GUILayout.Space(53);
					if (GUILayout.Button("Add Shortcut", GUILayout.MaxWidth(200)))
					{
						GenericMenu menu = new GenericMenu();
						for (int i = 0; i < shortcutActions.Count; i++)
						{
							menu.AddItem(new GUIContent(shortcutActions[i].name), false, (object choice) =>
							{
								RDEditorShortcut[] newArray = new RDEditorShortcut[keyboardShortcuts.Length + 1];
								for (int a = 0; a < keyboardShortcuts.Length; a++)
								{
									newArray[a] = keyboardShortcuts[a];
								}
								newArray[newArray.Length - 1] = new RDEditorShortcut((int)choice, KeyCode.None, EventModifiers.None);
								shortcutsChanged = true;
								keyboardShortcuts = newArray;
							}, i);
						}
						menu.ShowAsContext();
					}
					GUILayout.FlexibleSpace();
					if (GUILayout.Button("Save Shortcuts"))
					{
						shortcutsChanged = false;
						RDEditorShortcut.Serialize("LipSyncLite", keyboardShortcuts);
					}

					if (GUILayout.Button("Revert to Saved"))
					{
						shortcutsChanged = false;
						SetDefaultShortcuts();
						keyboardShortcuts = RDEditorShortcut.Deserialize("LipSyncLite", shortcutActions, keyboardShortcuts);
					}
					GUILayout.Space(10);
					EditorGUILayout.EndHorizontal();
					if (shortcutsChanged)
						EditorGUILayout.HelpBox("You have made changes to the keyboard shortcuts. Press Save Shortcuts to avoid losing them.", MessageType.Warning);
					break;
			}
			GUILayout.Space(20);
			GUILayout.EndScrollView();
			return;
		}
		#endregion

		// Main Body
		GUILayout.Space(15);
		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		GUILayout.Space(20);
		EditorGUI.BeginChangeCheck();
		Clip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", Clip, typeof(AudioClip), false, GUILayout.MaxWidth(800));

		GUILayout.Space(20);
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
		GUILayout.Space(15);

		// Define input variables
		float viewportSeconds = (viewportEnd - viewportStart);
		float pixelsPerSecond = position.width / viewportSeconds;
		float mouseX = Event.current.mousePosition.x;
		float mouseY = Event.current.mousePosition.y;

		// Tab Control
		int oldTab = markerTab;
		EditorGUI.BeginDisabledGroup(true);
		GUILayout.BeginHorizontal();
		GUILayout.FlexibleSpace();
		GUILayout.Space(20);
		markerTab = 0;
		GUILayout.Toolbar(markerTab, new string[] { "Phonemes", "Emotions", "Gestures" }, GUILayout.MaxWidth(800));
		GUILayout.Space(20);
		GUILayout.FlexibleSpace();
		GUILayout.EndHorizontal();
		EditorGUI.EndDisabledGroup();
		if (oldTab != markerTab)
		{
			selection.Clear();
			firstSelection = 0;
		}
		GUILayout.Space(40);

		// Waveform Visulization
		GUILayout.Box("", (GUIStyle)"PreBackground", GUILayout.Width(position.width), GUILayout.Height((position.height - waveformHeight) - 18));
		if (Event.current.type == EventType.Repaint)
		{
			previewRect = GUILayoutUtility.GetLastRect(); // Only set previewRect dimensions on Repaint, GetLastRect is incorrect on Layout.
		}

		// Right Click Menu
		if (Event.current.type == EventType.ContextClick && previewRect.Contains(Event.current.mousePosition))
		{
			GenericMenu previewMenu = new GenericMenu();
			float cursorTime = (viewportStart + (Event.current.mousePosition.x / pixelsPerSecond)) / FileLength;

			if (markerTab == 0)
			{
				// Phonemes
				for (int a = 0; a < settings.phonemeSet.phonemes.Length; a++)
				{
					previewMenu.AddItem(new GUIContent("Add Marker Here/" + settings.phonemeSet.phonemes[a].name), false, PhonemePicked, new object[] { a, cursorTime });
				}
			}

			// Paste
			if (copyBuffer.Count > 0)
			{
				previewMenu.AddItem(new GUIContent("Paste Here"), false, () => { PasteMarkers(cursorTime); });
			}
			else
			{
				previewMenu.AddDisabledItem(new GUIContent("Paste Here"));
			}

			previewMenu.ShowAsContext();
		}

		// Draw Waveform Visulization
		waveformHeight = (int)previewRect.y;

		if(Event.current.type == EventType.Repaint)
		{
			if (Clip != null)
				AudioUtility.DrawWaveForm(Clip, 0, previewRect, viewportStart, viewportSeconds);
		}

		// Draw Playhead
		if (Event.current.button != 1)
		{
			if (Clip != null)
			{
				seekPosition = GUI.HorizontalSlider(new Rect(-(viewportStart * pixelsPerSecond), previewRect.y + 3, Clip.length * pixelsPerSecond, (position.height - waveformHeight) - 33), seekPosition, 0, 1, GUIStyle.none, GUIStyle.none);
			}
			else
			{
				seekPosition = GUI.HorizontalSlider(new Rect(-(viewportStart * pixelsPerSecond), previewRect.y + 3, FileLength * pixelsPerSecond, (position.height - waveformHeight) - 33), seekPosition, 0, 1, GUIStyle.none, GUIStyle.none);
			}
		}

		GUI.DrawTexture(new Rect((-(viewportStart * pixelsPerSecond)) + (seekPosition * (FileLength * pixelsPerSecond)) - 3, previewRect.y, 7, previewRect.height - 20), playhead_line);
		GUI.DrawTexture(new Rect((-(viewportStart * pixelsPerSecond)) + (seekPosition * (FileLength * pixelsPerSecond)) - 7, previewRect.y, 15, 15), playhead_top);
		GUI.DrawTexture(new Rect((-(viewportStart * pixelsPerSecond)) + (seekPosition * (FileLength * pixelsPerSecond)) - 7, position.height - 48, 15, 15), playhead_bottom);

		GUI.DrawTexture(new Rect(0, previewRect.y - 35, position.width, 36), track_top);

		// Draw Time Lines
		if (showTimeline)
		{
			LipSyncEditorExtensions.DrawTimeline(previewRect.y, viewportStart, viewportEnd, position.width);
		}

		// Preview Warning
		if (visualPreview)
		{
			EditorGUI.HelpBox(new Rect(20, previewRect.y + previewRect.height - 45, position.width - 40, 25), "Preview mode active.", MessageType.Info);
		}
		else if (previewTarget != null)
		{
			UpdatePreview(0);
			previewTarget = null;
		}

		// Viewport Scrolling
		MinMaxScrollbar(new Rect(0, (previewRect.y + previewRect.height) - 15, previewRect.width, 15), previewRect, ref viewportStart, ref viewportEnd, 0, FileLength, 0.5f);


		// ********************
		// Render Marker Tracks
		// ********************

		GUIContent tip = null;
		MessageType tipType = MessageType.None;

		if (markerTab == 0)
		{
			// Phonemes
			#region Phoneme Interaction
			if (currentModal == null)
			{
				highlightedMarker = -1;
			}

			if (dragging == false)
			{
				for (int m = 0; m < PhonemeData.Count; m++)
				{
					PhonemeMarker marker = PhonemeData[m];

					if ((filterMask & (int)Mathf.Pow(2, marker.phonemeNumber)) == (int)Mathf.Pow(2, marker.phonemeNumber))
					{
						Rect markerRect = new Rect((-(viewportStart * pixelsPerSecond)) + (marker.time * (FileLength * pixelsPerSecond)) - 12.5f, previewRect.y - 25, 25, 26);

						// Test for marker at mouse position
						if (mouseX > markerRect.x + 5 && mouseX < markerRect.x + markerRect.width - 5 && mouseY > markerRect.y && mouseY < markerRect.y + markerRect.height - 4 && currentMarker == -1 && selectionRect.width < 2 && selectionRect.width > -2)
						{
							highlightedMarker = m;
							break;
						}
					}
				}
			}

			if (dragging == false && highlightedMarker > -1 && focusedWindow == this)
			{
				PhonemeMarker cm = PhonemeData[highlightedMarker];

				// Get Input
				if (Event.current.type == EventType.MouseDrag)
				{
					// Started dragging a marker

					currentMarker = highlightedMarker;
					startOffset = cm.time - ((mouseX + (viewportStart * pixelsPerSecond)) / (FileLength * pixelsPerSecond));

					// Clear selection if this marker isn't in it, otherwise calculate offsets for whole selection
					if (selection.Count > 0)
					{
						if (!selection.Contains(highlightedMarker))
						{
							selection.Clear();
						}
						else
						{
							selectionOffsets = new float[selection.Count];
							for (int marker = 0; marker < selectionOffsets.Length; marker++)
							{
								selectionOffsets[marker] = PhonemeData[currentMarker].time - PhonemeData[selection[marker]].time;
							}
						}
					}

					dragging = true;
				}
				else if (Event.current.clickCount == 2 && Event.current.button == 0 && selection.Count < 2)
				{
					PhonemeMarkerSettings(cm);
				}
				else if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
				{
					// Released left mouse button

					if (Event.current.modifiers == EventModifiers.Shift)
					{
						// Holding shift
						// Clear selection and add all markers between this one and the last selected one.
						// If selection is empty, start afresh.
						if (selection.Count > 0)
						{
							List<PhonemeMarker> tempData = new List<PhonemeMarker>(PhonemeData);
							tempData.Sort(LipSync.SortTime);

							int tempIndex = tempData.IndexOf(cm);
							int tempStart = tempData.IndexOf(PhonemeData[firstSelection]);
							selection.Clear();

							if (tempStart > tempIndex)
							{
								for (int m = tempIndex; m <= tempStart; m++)
								{
									int realIndex = PhonemeData.IndexOf(tempData[m]);

									selection.Add(realIndex);
								}
							}
							else if (tempStart < tempIndex)
							{
								for (int m = tempStart; m <= tempIndex; m++)
								{
									int realIndex = PhonemeData.IndexOf(tempData[m]);

									selection.Add(realIndex);
								}
							}
						}
						else
						{
							firstSelection = highlightedMarker;
							selection.Add(highlightedMarker);
						}
					}
					else if (Event.current.modifiers == EventModifiers.Control)
					{
						// Holding control
						// Simply add this marker to the existing selection.
						// If marker is already in the selection, remove it.
						if (!selection.Contains(highlightedMarker))
						{
							selection.Add(highlightedMarker);
							if (cm.time < PhonemeData[firstSelection].time || selection.Count == 1)
							{
								firstSelection = highlightedMarker;
							}
						}
						else
						{
							selection.Remove(highlightedMarker);
							if (highlightedMarker == firstSelection)
							{
								firstSelection = 0;
							}
						}
						selection.Sort(SortInt);
					}
					else
					{
						selection.Clear();
						selection.Add(highlightedMarker);
						firstSelection = highlightedMarker;
					}
				}
				else if (Event.current.type == EventType.ContextClick)
				{
					// Right clicked (two-finger tap on macOS)
					// Open marker context menu.
					GenericMenu markerMenu = new GenericMenu();
					markerMenu.AddItem(new GUIContent("Copy"), false, CopyMarkers);

					if (selection.Count > 1)
					{
						markerMenu.AddItem(new GUIContent("Delete Selection"), false, DeleteSelectedPhonemes);
						markerMenu.AddSeparator("");
						markerMenu.AddItem(new GUIContent("Selection Settings"), false, PhonemeMarkerSettingsMulti);
					}
					else
					{
						markerMenu.AddItem(new GUIContent("Delete"), false, DeletePhoneme, cm);

						for (int a = 0; a < settings.phonemeSet.phonemes.Length; a++)
						{
							markerMenu.AddItem(new GUIContent("Change/" + settings.phonemeSet.phonemes[a].name), false, ChangePhonemePicked, new List<int> { highlightedMarker, a });
						}

						if (highlightedMarker + 1 < PhonemeData.Count)
						{
							if (PhonemeData[highlightedMarker + 1].phonemeNumber == cm.phonemeNumber)
							{
								markerMenu.AddItem(new GUIContent("Sustain Marker"), cm.sustain, ToggleSustain, cm);
							}
							else
							{
								markerMenu.AddDisabledItem(new GUIContent("Sustain Marker"));
							}
						}
						else
						{
							markerMenu.AddDisabledItem(new GUIContent("Sustain Marker"));
						}

						markerMenu.AddSeparator("");
						markerMenu.AddItem(new GUIContent("Marker Settings"), false, PhonemeMarkerSettings, cm);
					}

					markerMenu.ShowAsContext();
				}
				else if (Event.current.type == EventType.KeyUp && selection.Count == 0)
				{
					// Check keyboard input (individual marker)
					if (Event.current.keyCode == KeyCode.Delete)
					{
						DeletePhoneme(PhonemeData[highlightedMarker]);
					}
				}
			}
			else if (dragging == false && focusedWindow == this)
			{
				if (Event.current.type == EventType.MouseUp && !(Event.current.modifiers == EventModifiers.Control || Event.current.modifiers == EventModifiers.Shift))
				{
					// Release mouse button while not dragging or holding any modifiers - clear selection
					selection.Clear();
				}
				else if (Event.current.type == EventType.KeyUp && selection.Count > 0)
				{
					// Check keyboard input (on selection)
					if (Event.current.keyCode == KeyCode.Delete)
					{
						DeleteSelectedPhonemes();
					}
				}
			}
			#endregion

			#region Phoneme Drawing
			// Draw Markers
			for (int m = 0; m < PhonemeData.Count; m++)
			{
				PhonemeMarker marker = PhonemeData[m];

				if ((filterMask & (int)Mathf.Pow(2, marker.phonemeNumber)) == (int)Mathf.Pow(2, marker.phonemeNumber))
				{
					Rect markerRect = new Rect((-(viewportStart * pixelsPerSecond)) + (marker.time * (FileLength * pixelsPerSecond)) - 12.5f, previewRect.y - 25, 25, 26);

					if (marker.sustain && m + 1 < PhonemeData.Count)
					{
						GUI.DrawTexture(new Rect(markerRect.x + 12.5f, markerRect.y + 7, ((-(viewportStart * pixelsPerSecond)) + (PhonemeData[m + 1].time * (FileLength * pixelsPerSecond))) - (markerRect.x + 12.5f), 1), marker_line);
					}

					GUI.color = Color.Lerp(Color.gray, Color.white, marker.intensity);
					if (currentMarker == m)
					{
						GUI.Box(markerRect, marker_selected, GUIStyle.none);
						GUI.DrawTexture(new Rect(markerRect.x + 12, previewRect.y, 1, previewRect.height - 15), marker_line);
						tip = new GUIContent(settings.phonemeSet.phonemes[marker.phonemeNumber].name + " - " + Mathf.RoundToInt(marker.intensity * 100f).ToString() + "%");
					}
					else if (highlightedMarker == m)
					{
						GUI.Box(markerRect, marker_hover, GUIStyle.none);
						GUI.DrawTexture(new Rect(markerRect.x + 12, previewRect.y, 1, previewRect.height - 15), marker_line);
						tip = new GUIContent(settings.phonemeSet.phonemes[marker.phonemeNumber].name + " - " + Mathf.RoundToInt(marker.intensity * 100f).ToString() + "%");
					}
					else if (selection.Contains(m))
					{
						GUI.Box(markerRect, marker_selected, GUIStyle.none);
						GUI.DrawTexture(new Rect(markerRect.x + 12, previewRect.y, 1, previewRect.height - 15), marker_line);
					}
					else
					{
						GUI.Box(markerRect, marker_normal, GUIStyle.none);
					}

					GUI.color = Color.white;

					if (marker.sustain)
					{
						GUI.Box(markerRect, marker_sustained, GUIStyle.none);
					}
				}
			}
			#endregion

		}

		// Rect Selection
		if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && new Rect(0, previewRect.y - 35, position.width, 36).Contains(Event.current.mousePosition) && highlightedMarker == -1)
		{
			// Left mouse button pressed - begin selection
			selection.Clear();
			selectionRect = new Rect(Event.current.mousePosition, Vector2.zero);
		}
		else if ((Event.current.type == EventType.MouseUp && Event.current.button == 0 || currentMarker > -1) && selection.Count == 0)
		{

			// If selection rect is inverted (width < 0), flip along X
			if (selectionRect.width < 0)
			{
				selectionRect = new Rect(selectionRect.x + selectionRect.width, selectionRect.y, -selectionRect.width, selectionRect.height);
			}

			// If no modifiers, clear selection
			// Shift and Control are treated the same for rect selecting for the sake of simplicity
			if (Event.current.modifiers != EventModifiers.Shift && Event.current.modifiers != EventModifiers.Control)
			{
				selection.Clear();
			}

			if (markerTab == 0)
			{
				// Phonemes
				for (int i = 0; i < PhonemeData.Count; i++)
				{
					float screenPos = (-(viewportStart * pixelsPerSecond)) + (PhonemeData[i].time * (FileLength * pixelsPerSecond));
					if (screenPos > selectionRect.x && screenPos < selectionRect.x + selectionRect.width)
					{
						if (selection.Contains(i))
						{
							selection.Remove(i);
						}
						else
						{
							selection.Add(i);
						}

					}
				}
			}

			// Reset rect
			selectionRect = new Rect(0, 0, 0, 0);
		}

		// Update and Draw Selection Rect
		if (selectionRect != new Rect(0, 0, 0, 0))
		{
			selectionRect = Rect.MinMaxRect(selectionRect.position.x, selectionRect.y, Event.current.mousePosition.x, Mathf.Clamp(Event.current.mousePosition.y, previewRect.y - 35, previewRect.y - 3));
			EditorGUI.DrawRect(selectionRect, new Color(0.2f, 0.4f, 1f, 0.5f));
		}

		// Draw tooltip
		if (tip != null && dragging == false)
		{
			Rect tooltipRect = new Rect();
			float tooltipWidth = Mathf.Clamp(((GUIStyle)"flow node 0").CalcSize(tip).x + 20, 40, 450);
			if (Event.current.mousePosition.x + tooltipWidth + 10 > this.position.width)
			{
				tooltipRect = new Rect(Event.current.mousePosition.x - (tooltipWidth + 10), Event.current.mousePosition.y - 10, tooltipWidth, 30);
			}
			else
			{
				tooltipRect = new Rect(Event.current.mousePosition.x + 10, Event.current.mousePosition.y - 10, tooltipWidth, 30);
			}

			if (tipType == MessageType.None)
			{
				GUI.Box(tooltipRect, tip, (GUIStyle)"flow node 0");
			}
			else if (tipType == MessageType.Info)
			{
				GUI.Box(tooltipRect, tip, (GUIStyle)"flow node 1");
			}
			else if (tipType == MessageType.Warning)
			{
				GUI.Box(tooltipRect, tip, (GUIStyle)"flow node 5");
			}
			else if (tipType == MessageType.Error)
			{
				GUI.Box(tooltipRect, tip, (GUIStyle)"flow node 6");
			}
		}

		// Update marker dragging
		if (markerTab == 0)
		{
			// Phonemes
			if (dragging == true && Event.current.type == EventType.MouseDrag && currentMarker > -1)
			{
				if (selection.Count > 0)
				{
					selection.Sort(SortInt);
					float firstMarkerOffset = PhonemeData[currentMarker].time - PhonemeData[selection[0]].time;
					float lastMarkerOffset = PhonemeData[selection[selection.Count - 1]].time - PhonemeData[currentMarker].time;
					float currentMarkerNewTime = Mathf.Clamp01(((Event.current.mousePosition.x / (FileLength * pixelsPerSecond)) + (viewportStart / FileLength)) + startOffset);

					if (currentMarkerNewTime - firstMarkerOffset >= 0 && currentMarkerNewTime + lastMarkerOffset <= 1)
					{
						for (int marker = 0; marker < selection.Count; marker++)
						{
							PhonemeData[selection[marker]].time = currentMarkerNewTime - selectionOffsets[marker];
						}
					}
				}
				else
				{
					PhonemeData[currentMarker].time = Mathf.Clamp01(((Event.current.mousePosition.x / (FileLength * pixelsPerSecond)) + (viewportStart / FileLength)) + startOffset);
				}

				changed = true;
				previewOutOfDate = true;
			}
		}

		if (Event.current.type == EventType.MouseUp && dragging == true)
		{
			// Finished dragging
			dragging = false;

			// Sort markers now that dragging has finished
			if (markerTab == 0)
			{
				// Phonemes
				PhonemeData.Sort(LipSync.SortTime);

				// Check sustained markers are still valid 
				for (int i = 0; i < PhonemeData.Count; i++)
				{
					PhonemeMarker m = PhonemeData[i];

					if (i + 1 < PhonemeData.Count)
					{
						if (m.sustain && PhonemeData[i + 1].phonemeNumber != m.phonemeNumber)
						{
							m.sustain = false;
							if (i - 1 >= 0)
							{
								if (PhonemeData[i - 1].phonemeNumber == m.phonemeNumber)
								{
									PhonemeData[i - 1].sustain = true;
								}
							}
						}
					}
					else if (i - 1 >= 0)
					{
						if (m.sustain && PhonemeData[i - 1].phonemeNumber == m.phonemeNumber)
						{
							m.sustain = false;
							PhonemeData[i - 1].sustain = true;
						}
						else
						{
							m.sustain = false;
						}
					}
					else
					{
						m.sustain = false;
					}
				}
			}

			currentMarker = -1;
		}

		//Controls
		Rect bottomToolbarRect = EditorGUILayout.BeginHorizontal();
		GUI.Box(bottomToolbarRect, "", EditorStyles.toolbar);
		timeSpan = TimeSpan.FromSeconds(seekPosition * FileLength);
		Char pad = '0';
		string minutes = timeSpan.Minutes.ToString().PadLeft(2, pad);
		string seconds = timeSpan.Seconds.ToString().PadLeft(2, pad);
		string milliseconds = timeSpan.Milliseconds.ToString().PadLeft(3, pad);

		string currentTime = minutes + ":" + seconds + ":" + milliseconds;

		timeSpan = TimeSpan.FromSeconds(FileLength);
		minutes = timeSpan.Minutes.ToString().PadLeft(2, pad);
		seconds = timeSpan.Seconds.ToString().PadLeft(2, pad);
		milliseconds = timeSpan.Milliseconds.ToString().PadLeft(3, pad);

		string totalTime = minutes + ":" + seconds + ":" + milliseconds;
		GUILayout.FlexibleSpace();
		if (GUILayout.Button(new GUIContent(currentTime + " / " + totalTime, "Change Clip Length"), EditorStyles.toolbarTextField))
		{
			ClipSettings();
		}
		GUILayout.FlexibleSpace();
		if (GUILayout.Button(isPlaying && !isPaused ? pauseIcon : playIcon, EditorStyles.toolbarButton, GUILayout.Width(50)))
		{
			PlayPause();
		}
		if (GUILayout.Button(stopIcon, EditorStyles.toolbarButton, GUILayout.Width(50)))
		{
			Stop();
		}
		looping = GUILayout.Toggle(looping, loopIcon, EditorStyles.toolbarButton, GUILayout.Width(50));

		GUILayout.FlexibleSpace();

		switch (markerTab)
		{
			case 0:
				if (GUILayout.Button("Add Phoneme", EditorStyles.toolbarButton, GUILayout.Width(160)))
				{
					GenericMenu phonemeMenu = new GenericMenu();

					for (int a = 0; a < settings.phonemeSet.phonemes.Length; a++)
					{
						phonemeMenu.AddItem(new GUIContent(settings.phonemeSet.phonemes[a].name), false, PhonemePicked, new object[] { a, seekPosition });
					}
					phonemeMenu.ShowAsContext();
				}

				GUILayout.Space(40);
				GUILayout.Box("Filters:", EditorStyles.label);
				filterMask = EditorGUILayout.MaskField(filterMask, phonemeSetNames, EditorStyles.toolbarPopup, GUILayout.MaxWidth(100));
				break;
		}

		GUILayout.FlexibleSpace();
		EditorGUILayout.EndHorizontal();
		GUILayout.Space(1);
	}
	#endregion

	#region Window Management
	[MenuItem("Window/Rogo Digital/LipSync Lite/Open Clip Editor %&a", false, 11)]
	public static LipSyncClipSetup ShowWindow ()
	{
		return ShowWindow("", false, "", "", 0, 0);
	}

	public static LipSyncClipSetup ShowWindow (string loadPath, bool newWindow)
	{
		return ShowWindow(loadPath, newWindow, "", "", 0, 0);
	}

	public static LipSyncClipSetup ShowWindow (string loadPath, bool newWindow, string oldFileName, string oldLastLoad, int oldMarkerTab, float oldSeekPosition)
	{
		LipSyncClipSetup window;

		UnityEngine.Object[] current = Selection.GetFiltered(typeof(AudioClip), SelectionMode.Assets);

		if (newWindow)
		{
			window = ScriptableObject.CreateInstance<LipSyncClipSetup>();
			window.Show();
		}
		else
		{
			window = EditorWindow.GetWindow<LipSyncClipSetup>();
		}

		window.data = CreateInstance<TemporaryLipSyncData>();

		if (current.Length > 0)
		{
			window.Clip = (AudioClip)current[0];
			window.FileLength = window.Clip.length;
		}
		else if (loadPath == "")
		{
			current = Selection.GetFiltered(typeof(LipSyncData), SelectionMode.Assets);
			if (current.Length > 0)
			{
				loadPath = AssetDatabase.GetAssetPath(current[0]);
			}
		}

		if (EditorPrefs.GetBool("LipSync_ShowExtensionsOnLoad", true))
		{
			EditorWindow.GetWindow<RDExtensionWindow>("Extensions", false, typeof(LipSyncClipSetup));
			RDExtensionWindow.ShowWindow("LipSync");
		}

		window.Focus();

		if (window.changed)
		{
			int choice = EditorUtility.DisplayDialogComplex("Save Changes", "You have made changes to the current file, do you want to save them before closing?", "Yes", "No", "Cancel");
			if (choice != 2)
			{
				if (choice == 0)
				{
					window.OnSaveClick();
				}

				window.changed = false;
				window.fileName = "Untitled";
				window.oldClip = window.Clip;
				window.PhonemeData = new List<PhonemeMarker>();

				window.seekPosition = 0;
				AudioUtility.StopAllClips();
				window.currentMarker = -1;

				if (loadPath != "")
				{
					window.LoadFile(loadPath);
					window.previewOutOfDate = true;
				}
			}
			else
			{
				window.Clip = window.oldClip;
			}
		}
		else
		{
			window.oldClip = window.Clip;
			window.fileName = "Untitled";
			window.PhonemeData = new List<PhonemeMarker>();

			window.seekPosition = 0;
			AudioUtility.StopAllClips();
			window.currentMarker = -1;

			if (loadPath != "")
			{
				window.LoadFile(loadPath);
				window.previewOutOfDate = true;
			}
		}


		if (EditorGUIUtility.isProSkin)
		{
			window.windowIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Dark/icon.png");
		}
		else
		{
			window.windowIcon = (Texture2D)EditorGUIUtility.Load("Rogo Digital/LipSync/Light/icon.png");
		}

		window.titleContent = new GUIContent("LipSync", window.windowIcon);
		window.minSize = new Vector2(700, 200);
		if (newWindow)
		{
			window.changed = true;
			window.lastLoad = oldLastLoad;
			window.fileName = oldFileName;
			window.markerTab = oldMarkerTab;
			window.seekPosition = oldSeekPosition;
			window.oldSeekPosition = oldSeekPosition;
			if (window.Clip != null)
				AudioUtility.SetClipSamplePosition(window.Clip, (int)(window.seekPosition * AudioUtility.GetSampleCount(window.Clip)));
			AssetDatabase.DeleteAsset("Assets/LIPSYNC_AUTOSAVE.asset");
		}

		if (EditorPrefs.GetBool("LipSync_SetViewportOnLoad", true))
		{
			if (window.Clip != null)
			{
				window.viewportEnd = window.FileLength;
			}
			window.viewportStart = 0;
		}

		return window;
	}
	#endregion

	#region Input/Output
	void LoadFile (string path)
	{
		if (changed)
		{
			int choice = EditorUtility.DisplayDialogComplex("Save Changes", "You have made changes to the current file, do you want to save them before closing?", "Yes", "No", "Cancel");

			if (choice == 1)
			{
				OnSaveClick();
			}
			else if (choice == 2)
			{
				return;
			}
		}
		LipSyncData file = (LipSyncData)AssetDatabase.LoadAssetAtPath(path, typeof(LipSyncData));

		fileName = file.name + ".Asset";

		data = (TemporaryLipSyncData)file;
		oldClip = Clip;

		if (setViewportOnLoad)
		{
			if (Clip != null)
				FileLength = Clip.length;
			viewportEnd = FileLength;
			viewportStart = 0;
		}

		currentMarker = -1;
		previewOutOfDate = true;
		changed = false;

		if (file.version < version)
		{
			UpdateFile(file.version, false, false);
		}

		lastLoad = path;
	}

	private void LoadXML (TextAsset xmlFile, AudioClip linkedClip, bool suppressInput)
	{
		Clip = linkedClip;
		LoadXML(xmlFile, suppressInput);
	}

	private void LoadXML (TextAsset xmlFile, bool suppressInput)
	{
		XmlDocument document = new XmlDocument();
		document.LoadXml(xmlFile.text);

		// Clear/define marker lists, to overwrite any previous file
		PhonemeData = new List<PhonemeMarker>();

		float fileVersion = 0;

		if (LipSync.ReadXML(document, "LipSyncData", "version") == null)
		{
			// Update data
			FileLength = Clip.length;
		}
		else
		{
			fileVersion = float.Parse(LipSync.ReadXML(document, "LipSyncData", "version"));
			FileLength = float.Parse(LipSync.ReadXML(document, "LipSyncData", "length"));
		}

		if (LipSync.ReadXML(document, "LipSyncData", "transcript") != null)
		{
			Transcript = LipSync.ReadXML(document, "LipSyncData", "transcript");
		}

		//Phonemes
		XmlNode phonemesNode = document.SelectSingleNode("//LipSyncData//phonemes");
		if (phonemesNode != null)
		{
			XmlNodeList phonemeNodes = phonemesNode.ChildNodes;

			for (int p = 0; p < phonemeNodes.Count; p++)
			{
				XmlNode node = phonemeNodes[p];

				if (node.LocalName == "marker")
				{
					string phonemeName = "";
					float time = float.Parse(node.Attributes["time"].Value) / FileLength;
					float intensity = 1;
					bool sustain = false;
					bool useModifier = false;
					float intensityMod = 0.1f;
					float blendableMod = 0.3f;
					float bonePosMod = 0.3f;
					float boneRotMod = 0.3f;

					if (node.Attributes["phonemeName"] != null)
					{
						phonemeName = node.Attributes["phonemeName"].Value;
					}
					else
					{
						phonemeName = node.Attributes["phoneme"].Value;
					}
					if (node.Attributes["intensity"] != null)
						intensity = float.Parse(node.Attributes["intensity"].Value);
					if (node.Attributes["sustain"] != null)
						sustain = bool.Parse(node.Attributes["sustain"].Value);
					if (node.Attributes["useRandomness"] != null)
						useModifier = bool.Parse(node.Attributes["useRandomness"].Value);
					if (node.Attributes["intensityRandomness"] != null)
						intensityMod = float.Parse(node.Attributes["intensityRandomness"].Value);
					if (node.Attributes["blendableRandomness"] != null)
						blendableMod = float.Parse(node.Attributes["blendableRandomness"].Value);
					if (node.Attributes["bonePositionRandomness"] != null)
						bonePosMod = float.Parse(node.Attributes["bonePositionRandomness"].Value);
					if (node.Attributes["boneRotationRandomness"] != null)
						boneRotMod = float.Parse(node.Attributes["boneRotationRandomness"].Value);

					bool found = false;
					int phoneme;
					for (phoneme = 0; phoneme < settings.phonemeSet.phonemes.Length; phoneme++)
					{
						if (settings.phonemeSet.phonemes[phoneme].name == phonemeName)
						{
							found = true;
							break;
						}
					}

					if (found)
					{
						PhonemeMarker phonemeMarker = new PhonemeMarker(phoneme, time, intensity, sustain);
						phonemeMarker.useRandomness = useModifier;
						phonemeMarker.intensityRandomness = intensityMod;
						phonemeMarker.blendableRandomness = blendableMod;
						phonemeMarker.bonePositionRandomness = bonePosMod;
						phonemeMarker.boneRotationRandomness = boneRotMod;
						PhonemeData.Add(phonemeMarker);
					}
					else
					{
						Debug.LogWarning("XML Parser: Phoneme '" + phonemeName + "' does not exist in the current set adn has been skipped.");
					}

				}
			}
		}

		PhonemeData.Sort(LipSync.SortTime);

		if (fileVersion < version)
		{
			UpdateFile(fileVersion, true, suppressInput);
		}

		changed = true;
		previewOutOfDate = true;
	}

#pragma warning disable 618
	void UpdateFile (float version, bool isXML, bool suppressInput)
	{
		bool wasChanged = false;

		if (version < 1.3f && !isXML)
		{
			// 1.0 to 1.201 - Convert enum Phonemes
			wasChanged = true;

			for (int p = 0; p < PhonemeData.Count; p++)
			{
				PhonemeData[p].phonemeNumber = (int)PhonemeData[p].phoneme;
			}

			previewOutOfDate = true;
			changed = true;
		}

		Repaint();
		if (wasChanged && !suppressInput)
			EditorUtility.DisplayDialog("Loading Old File", "This file was created in an old version of LipSync. It has been automatically updated to work with the current version, but the original file has not been overwritten.", "Ok");
	}
#pragma warning restore 618

	public static LipSyncData SaveFile (LipSyncProject settings, string path, bool isXML, string transcript, float fileLength, PhonemeMarker[] phonemeData, AudioClip clip = null)
	{
		if (isXML)
		{
			XmlWriterSettings xmlWriterSettings = new XmlWriterSettings { Indent = true, IndentChars = "\t" };
			XmlWriter writer = XmlWriter.Create(path, xmlWriterSettings);

			writer.WriteStartDocument();

			//Header
			writer.WriteComment("Exported from Rogo Digital LipSync " + versionNumber + ". Exported at " + DateTime.Now.ToString());
			writer.WriteComment("Note: This format cannot directly reference the linked AudioClip like a LipSyncData asset can. It is advised that you use that format instead unless you need to process the data further outside of Unity.");

			writer.WriteStartElement("LipSyncData");
			writer.WriteElementString("version", version.ToString());
			writer.WriteElementString("transcript", transcript);
			writer.WriteElementString("length", fileLength.ToString());
			//Data
			writer.WriteStartElement("phonemes");
			foreach (PhonemeMarker marker in phonemeData)
			{
				writer.WriteStartElement("marker");

				writer.WriteAttributeString("time", (marker.time * fileLength).ToString());
				writer.WriteAttributeString("intensity", marker.intensity.ToString());
				writer.WriteAttributeString("sustain", marker.sustain.ToString());
				writer.WriteAttributeString("useRandomness", marker.useRandomness.ToString());
				writer.WriteAttributeString("intensityRandomness", marker.intensityRandomness.ToString());
				writer.WriteAttributeString("blendableRandomness", marker.blendableRandomness.ToString());
				writer.WriteAttributeString("bonePositionRandomness", marker.bonePositionRandomness.ToString());
				writer.WriteAttributeString("boneRotationRandomness", marker.boneRotationRandomness.ToString());
				writer.WriteAttributeString("phonemeName", settings.phonemeSet.phonemes[marker.phonemeNumber].name);
				writer.WriteAttributeString("phonemeNumber", marker.phonemeNumber.ToString());
				writer.WriteEndElement();
			}
			writer.WriteEndElement();

			writer.WriteEndDocument();
			writer.Close();
			AssetDatabase.Refresh();
			return null;
		}
		else
		{
			LipSyncData file = ScriptableObject.CreateInstance<LipSyncData>();
			file.phonemeData = phonemeData;
			file.version = version;
			file.clip = clip;
			file.length = fileLength;
			file.transcript = transcript;

			LipSyncData outputFile = (LipSyncData)AssetDatabase.LoadAssetAtPath(path, typeof(LipSyncData));

			if (outputFile != null)
			{
				EditorUtility.CopySerialized(file, outputFile);
				AssetDatabase.SaveAssets();
			}
			else
			{
				outputFile = ScriptableObject.CreateInstance<LipSyncData>();
				EditorUtility.CopySerialized(file, outputFile);
				AssetDatabase.CreateAsset(outputFile, path);
			}

			DestroyImmediate(file);
			AssetDatabase.Refresh();
			return outputFile;
		}
	}

	// Quick Update XML files
	[MenuItem("Window/Rogo Digital/LipSync Lite/Update All XML Files")]
	public static void UpdateXMLFiles ()
	{
		string[] guids = AssetDatabase.FindAssets("t:TextAsset");
		string filesUpdated = "";
		int filesUpdatedCount = 0;

		for (int i = 0; i < guids.Length; i++)
		{
			string path = AssetDatabase.GUIDToAssetPath(guids[i]);
			if (Path.GetExtension(path).ToLowerInvariant() == ".xml")
			{
				TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
				XmlDocument document = new XmlDocument();
				document.LoadXml(textAsset.text);

				XmlNode node = document.SelectSingleNode("//LipSyncData");
				if (node != null)
				{
					string version = LipSync.ReadXML(document, "LipSyncData", "version");
					bool upToDate = false;
					if (!string.IsNullOrEmpty(version))
					{
						if (float.Parse(version) >= 1.321f)
							upToDate = true;
					}

					if (!upToDate)
					{
						string[] audioGuids = AssetDatabase.FindAssets(Path.GetFileNameWithoutExtension(path) + " t:AudioClip", new string[] { Path.GetDirectoryName(path) });
						AudioClip clip = null;

						if (audioGuids.Length == 1)
						{
							clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(audioGuids[0]));
						}
						else
						{
							EditorUtility.DisplayDialog("Could Not Locate AudioClip", "No matching audio file was found for the file " + Path.GetFileName(path) + ". Please press Continue and locate the audio file.", "Continue");
							string audioPath = EditorUtility.OpenFilePanel("Locate AudioClip", Path.GetDirectoryName(path), "*");
							clip = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets" + audioPath.Substring((Application.dataPath).Length));
						}

						LipSyncClipSetup window = ShowWindow();
						window.LoadXML(textAsset, clip, true);
						SaveFile(window.settings, path, true, window.Transcript, window.FileLength, window.PhonemeData.ToArray(), clip);
						window.changed = false;
						window.Close();

						filesUpdated += path + "\n";
						filesUpdatedCount++;
					}
				}
			}
		}

		if (filesUpdatedCount > 0)
		{
			EditorUtility.DisplayDialog("Update Complete", "Updated " + filesUpdatedCount + " XML file(s):\n" + filesUpdated, "Ok");
		}
		else
		{
			EditorUtility.DisplayDialog("Update Complete", "No changes have been made. All files are up-to-date", "Ok");
		}
	}
	#endregion

	#region Utility
	static int SortInt (int a, int b)
	{
		return a.CompareTo(b);
	}

	static Color HexToColor (int color)
	{
		string hex = color.ToString("X").PadLeft(6, (char)'0');

		int R = Convert.ToInt32(hex.Substring(0, 2), 16);
		int G = Convert.ToInt32(hex.Substring(2, 2), 16);
		int B = Convert.ToInt32(hex.Substring(4, 2), 16);
		return new Color(R / 255f, G / 255f, B / 255f);
	}

	static int ColorToHex (Color color)
	{
		string R = ((int)(color.r * 255)).ToString("X").PadLeft(2, (char)'0');
		string G = ((int)(color.g * 255)).ToString("X").PadLeft(2, (char)'0');
		string B = ((int)(color.b * 255)).ToString("X").PadLeft(2, (char)'0');

		string hex = R + G + B;
		return Convert.ToInt32(hex, 16);
	}

	static Color Darken (Color color, float amount)
	{
		return new Color(color.r * amount, color.g * amount, color.b * amount);
	}

	#endregion

	// *********************
	// Menu Callback Methods
	// *********************

	#region Marker Addition

	void PhonemePicked (object raw)
	{
		object[] data = (object[])raw;
		int picked = (int)data[0];
		float time = (float)data[1];
		Undo.RecordObject(this, "Add Phoneme Marker");
		PhonemeMarker newMarker = new PhonemeMarker(picked, time, defaultPhonemeIntensity, false);
		newMarker.useRandomness = defaultUseRandomness;
		newMarker.intensityRandomness = defaultIntensityRandomness;
		newMarker.blendableRandomness = defaultBlendableRandomness;
		newMarker.bonePositionRandomness = defaultBonePositionRandomness;
		newMarker.boneRotationRandomness = defaultBoneRotationRandomness;

		PhonemeData.Add(newMarker);
		PhonemeData.Sort(LipSync.SortTime);

		for (int i = 0; i < PhonemeData.Count; i++)
		{
			PhonemeMarker m = PhonemeData[i];

			if (i + 1 < PhonemeData.Count)
			{
				if (m.sustain && PhonemeData[i + 1].phonemeNumber != m.phonemeNumber)
				{
					m.sustain = false;
					if (i - 1 >= 0)
					{
						if (PhonemeData[i - 1].phonemeNumber == m.phonemeNumber)
						{
							PhonemeData[i - 1].sustain = true;
						}
					}
				}
			}
			else if (i - 1 >= 0)
			{
				if (m.sustain && PhonemeData[i - 1].phonemeNumber == m.phonemeNumber)
				{
					m.sustain = false;
					PhonemeData[i - 1].sustain = true;
				}
				else
				{
					m.sustain = false;
				}
			}
			else
			{
				m.sustain = false;
			}
		}

		changed = true;
		previewOutOfDate = true;
	}
	#endregion

	#region Marker Editing
	void ChangePhonemePicked (object info)
	{
		Undo.RecordObject(this, "Change Phoneme Marker");
		List<int> finalInfo = (List<int>)info;

		PhonemeMarker marker = PhonemeData[finalInfo[0]];
		marker.phonemeNumber = finalInfo[1];

		for (int i = 0; i < PhonemeData.Count; i++)
		{
			PhonemeMarker m = PhonemeData[i];

			if (i + 1 < PhonemeData.Count)
			{
				if (m.sustain && PhonemeData[i + 1].phonemeNumber != m.phonemeNumber)
				{
					m.sustain = false;
					if (i - 1 >= 0)
					{
						if (PhonemeData[i - 1].phonemeNumber == m.phonemeNumber)
						{
							PhonemeData[i - 1].sustain = true;
						}
					}
				}
			}
			else if (i - 1 >= 0)
			{
				if (m.sustain && PhonemeData[i - 1].phonemeNumber == m.phonemeNumber)
				{
					m.sustain = false;
					PhonemeData[i - 1].sustain = true;
				}
				else
				{
					m.sustain = false;
				}
			}
			else
			{
				m.sustain = false;
			}
		}

		changed = true;
		previewOutOfDate = true;
	}

	void PhonemeMarkerSettings (object info)
	{
		PhonemeMarker marker = (PhonemeMarker)info;
		highlightedMarker = PhonemeData.IndexOf(marker);
		MarkerSettingsWindow.CreateWindow(this, this, marker);
	}

	void PhonemeMarkerSettingsMulti ()
	{
		MarkerSettingsWindow.CreateWindow(this, this, PhonemeData, selection);
	}

	void ToggleSustain (object marker)
	{
		PhonemeMarker m = (PhonemeMarker)marker;
		m.sustain = !m.sustain;

		changed = true;
		previewOutOfDate = true;
	}

	void DefaultMarkerSettings ()
	{
		DefaultMarkerSettingsWindow.CreateWindow(this, this, markerTab);
	}

	void CopyMarkers ()
	{
		copyBufferType = markerTab;
		copyBuffer.Clear();

		if (selection.Count == 0)
		{
			if (highlightedMarker == -1)
				return;

			switch (markerTab)
			{
				case 0:
					copyBuffer.Add(PhonemeData[highlightedMarker].CreateCopy());
					break;
			}
		}
		else
		{
			for (int s = 0; s < selection.Count; s++)
			{
				switch (markerTab)
				{
					case 0:
						copyBuffer.Add(PhonemeData[selection[s]].CreateCopy());
						break;
				}
			}
		}

		ShowNotification(new GUIContent(string.Format("Copied {0} Marker{1}", copyBuffer.Count, copyBuffer.Count == 1 ? "" : "s")));
	}

	void PasteMarkers (float startTime)
	{
		if (copyBuffer.Count < 1)
			return;
		if (copyBufferType != markerTab)
		{
			ShowNotification(new GUIContent(string.Format("Can't paste {0} into the {1} track!", markerTypes[copyBufferType], markerTypes[markerTab])));
			return;
		}

		selection.Clear();

		float selectionStartTime = 0;

		switch (copyBufferType)
		{
			case 0:
				selectionStartTime = ((PhonemeMarker)copyBuffer[0]).time;

				for (int i = 0; i < copyBuffer.Count; i++)
				{
					PhonemeMarker marker = (PhonemeMarker)copyBuffer[i];
					PhonemeMarker newMarker = marker.CreateCopy();

					newMarker.time = (marker.time - selectionStartTime) + startTime;

					PhonemeData.Add(newMarker);
					PhonemeData.Sort(LipSync.SortTime);
				}
				break;
		}

		changed = true;
		previewOutOfDate = true;
	}

	#endregion

	#region Marker Removal
	void DeletePhoneme (object marker)
	{
		Undo.RecordObject(this, "Delete Phoneme Marker");
		int i = PhonemeData.IndexOf((PhonemeMarker)marker);

		if (i - 1 >= 0)
		{
			PhonemeData[i - 1].sustain = false;
		}

		PhonemeData.Remove((PhonemeMarker)marker);
		selection.Clear();
		firstSelection = 0;
		changed = true;
		previewOutOfDate = true;
	}

	void DeleteSelectedPhonemes ()
	{
		Undo.RecordObject(this, "Delete Phoneme Markers");
		selection.Sort(SortInt);

		int i = PhonemeData.IndexOf(PhonemeData[selection[0]]);

		if (i - 1 >= 0)
		{
			PhonemeData[i - 1].sustain = false;
		}

		for (int marker = selection.Count - 1; marker >= 0; marker--)
		{
			PhonemeData.Remove((PhonemeData[selection[marker]]));
		}
		selection.Clear();
		firstSelection = 0;
		changed = true;
		previewOutOfDate = true;
	}
	#endregion

	#region File Methods
	void OnNewClick ()
	{
		if (changed)
		{
			int choice = EditorUtility.DisplayDialogComplex("Save Changes", "You have made changes to the current file, do you want to save them before closing?", "Yes", "No", "Cancel");

			if (choice == 0)
			{
				OnSaveClick();
			}
			else if (choice == 2)
			{
				return;
			}
		}

		lastLoad = "";
		fileName = "Untitled";
		AudioUtility.StopAllClips();
		seekPosition = 0;
		oldSeekPosition = 0;
		Clip = null;
		PhonemeData = new List<PhonemeMarker>();
		oldClip = null;
		changed = false;
		FileLength = 10;
		Transcript = "";
		previewOutOfDate = true;

		FileLength = 10;

	}

	void OnLoadClick ()
	{
		string loadPath = EditorUtility.OpenFilePanel("Load LipSync Data File", "Assets", "asset");

		if (loadPath != "")
		{
			loadPath = "Assets" + loadPath.Substring(Application.dataPath.Length);
			LoadFile(loadPath);
		}
	}

	void OnSaveClick ()
	{
		if (lastLoad != "")
		{
			string savePath = lastLoad;
			LipSyncData outputFile = SaveFile(settings, savePath, false, Transcript, FileLength, PhonemeData.ToArray(), Clip);
			fileName = outputFile.name + ".Asset";
			changed = false;
		}
		else
		{
			OnSaveAsClick();
		}
	}

	void OnSaveAsClick ()
	{
		string defaultName = "New Asset";

		if (Clip != null)
		{
			defaultName = Clip.name;
		}

		string savePath = EditorUtility.SaveFilePanel("Save LipSync Data File", "Assets" + lastLoad, defaultName + ".asset", "asset");
		if (savePath != "")
		{
			savePath = "Assets" + savePath.Substring(Application.dataPath.Length);
			LipSyncData outputFile = SaveFile(settings, savePath, false, Transcript, FileLength, PhonemeData.ToArray(), Clip);
			fileName = outputFile.name + ".Asset";
			changed = false;
			lastLoad = savePath;
		}
	}

	void OnUnityExport ()
	{
		string savePath = EditorUtility.SaveFilePanel("Export LipSync Data and audio", "Assets" + lastLoad, Clip.name + ".unitypackage", "unitypackage");
		if (savePath != "")
		{
			if (!savePath.Contains("Assets/"))
			{
				if (EditorUtility.DisplayDialog("Invalid Save Path", "Cannot export outside the assets folder. Do you want to pick a different folder?", "yes", "no"))
				{
					OnUnityExport();
				}
				return;
			}

			savePath = "Assets" + savePath.Substring(Application.dataPath.Length);
			string folderPath = savePath.Remove(savePath.Length - Path.GetFileName(savePath).Length) + Path.GetFileNameWithoutExtension(savePath);
			AssetDatabase.CreateFolder(savePath.Remove(savePath.Length - (Path.GetFileName(savePath).Length + 1)), Path.GetFileNameWithoutExtension(savePath));

			string originalName = fileName;

			if (Clip != null)
			{
				AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(Clip), folderPath + "/" + Path.GetFileName(AssetDatabase.GetAssetPath(Clip)));
				AssetDatabase.ImportAsset(folderPath + "/" + Path.GetFileName(AssetDatabase.GetAssetPath(Clip)));
			}

			AudioClip newClip = AssetDatabase.LoadAssetAtPath<AudioClip>(folderPath + "/" + Path.GetFileName(AssetDatabase.GetAssetPath(Clip)));
			AudioClip originalClip = Clip;
			if (newClip != null)
			{
				Clip = newClip;
			}
			else
			{
				Debug.Log("LipSync: AudioClip copy at " + folderPath + "/" + Path.GetFileName(AssetDatabase.GetAssetPath(Clip)) + " could not be reloaded for compression. Proceding without AudioClip.");
			}

			SaveFile(settings, folderPath + "/" + Path.ChangeExtension(Path.GetFileName(savePath), ".asset"), false, Transcript, FileLength, PhonemeData.ToArray(), Clip);

			LipSyncData file = AssetDatabase.LoadAssetAtPath<LipSyncData>(folderPath + "/" + Path.ChangeExtension(Path.GetFileName(savePath), ".asset"));
			if (file != null)
			{
				AssetDatabase.ExportPackage(folderPath, savePath, ExportPackageOptions.Recurse);
				AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
				AssetDatabase.DeleteAsset(folderPath);

				fileName = originalName;
				lastLoad = "";
			}
			else
			{
				Debug.LogError("LipSync: File could not be reloaded for compression. Aborting Export.");
			}

			Clip = originalClip;
		}
	}

	void OnXMLExport ()
	{
		string savePath = EditorUtility.SaveFilePanel("Export LipSync Data to XML", "Assets" + lastLoad, Path.GetFileNameWithoutExtension(fileName) + ".xml", "xml");
		if (savePath != "")
		{
			savePath = "Assets" + savePath.Substring(Application.dataPath.Length);
			SaveFile(settings, savePath, true, Transcript, FileLength, PhonemeData.ToArray(), Clip);
		}
	}

	void OnXMLImport ()
	{
		string xmlPath = EditorUtility.OpenFilePanel("Import LipSync Data from XML", "Assets" + lastLoad, "xml");
		if (string.IsNullOrEmpty(xmlPath))
			return;

		bool loadAudio = EditorUtility.DisplayDialog("Load Audio?", "Do you want to load an audioclip for this animation?", "Yes", "No");
		string audioPath = "";
		if (loadAudio)
		{
			audioPath = EditorUtility.OpenFilePanel("Load AudioClip", "Assets" + lastLoad, "wav;*.mp3;*.ogg");
		}

		xmlPath = "Assets" + xmlPath.Substring(Application.dataPath.Length);
		TextAsset xmlFile = AssetDatabase.LoadAssetAtPath<TextAsset>(xmlPath);

		if (loadAudio)
		{
			audioPath = "Assets" + audioPath.Substring(Application.dataPath.Length);
			AudioClip linkedClip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);

			LoadXML(xmlFile, linkedClip, false);
		}
		else
		{
			LoadXML(xmlFile, false);
		}
	}

	void ShowProjectSettings ()
	{
		LipSyncProjectSettings.ShowWindow();
	}
	#endregion

	#region Edit Methods
	void SelectAll ()
	{
		if (markerTab == 0)
		{
			if (PhonemeData.Count > 0)
			{
				selection.Clear();
				firstSelection = 0;
				for (int marker = 0; marker < PhonemeData.Count; marker++)
				{
					selection.Add(marker);
				}
			}
		}
	}

	void SelectNone ()
	{
		selection.Clear();
		firstSelection = 0;
	}

	void InvertSelection ()
	{
		if (markerTab == 0)
		{
			if (PhonemeData.Count > 0)
			{
				List<int> tempSelection = new List<int>();
				for (int marker = 0; marker < PhonemeData.Count; marker++)
				{
					if (!selection.Contains(marker))
					{
						if (tempSelection.Count == 0)
						{
							firstSelection = marker;
						}
						tempSelection.Add(marker);
					}
				}
				selection = tempSelection;
			}
		}
	}

	void SetIntensitiesVolume ()
	{
		SetIntensityWindow.CreateWindow(this, this);
	}

	void ResetIntensities ()
	{
		if (markerTab == 0)
		{
			for (int m = 0; m < PhonemeData.Count; m++)
			{
				PhonemeData[m].intensity = defaultPhonemeIntensity;
			}
		}

		changed = true;
		previewOutOfDate = true;
	}

	void ClipSettings ()
	{
		ClipSettingsWindow.CreateWindow(this, this);
	}
	#endregion

	#region Realtime Preview
	void TargetChosen (object data)
	{
		if (data == null)
		{
			visualPreview = false;
		}
		else
		{
			LipSync target = (LipSync)data;
			visualPreview = true;
			previewTarget = target;
			target.onSettingsChanged += () =>
			{
				previewOutOfDate = true;
			};
			previewOutOfDate = true;
			SceneView.RepaintAll();
		}
	}

	void UpdatePreview (float time)
	{
		if (previewTarget != null)
		{
			if (previewTarget.blendSystem != null)
			{
				if (previewTarget.blendSystem.isReady)
				{
					if (previewOutOfDate)
					{
						previewTarget.PreviewAtTime(0);
						previewTarget.TempLoad(PhonemeData, Clip, FileLength);
						previewTarget.ProcessData();
						previewOutOfDate = false;
					}

					previewTarget.PreviewAtTime(time);
					EditorUtility.SetDirty(previewTarget.blendSystem);
				}
			}
		}
	}
	#endregion

	#region Playback Methods
	void Stop ()
	{
		isPaused = true;
		isPlaying = false;
		if (Clip)
			AudioUtility.StopClip(Clip);
		seekPosition = 0;
		oldSeekPosition = seekPosition;
		float vpDiff = viewportEnd - viewportStart;
		viewportStart = 0;
		viewportEnd = vpDiff;
	}

	void PlayPause ()
	{
		if (isPlaying && !isPaused)
		{
			AudioUtility.PauseClip(Clip);
			isPaused = true;
		}
		else
		{
			AudioUtility.PlayClip(Clip);

			isPaused = false;
			isPlaying = true;
		}
	}
	#endregion

	#region Generic Methods
	void OpenURL (object url)
	{
		Application.OpenURL((string)url);
	}
	#endregion

	// ********************

	public bool MinMaxScrollbar (Rect position, Rect viewportRect, ref float minValue, ref float maxValue, float minLimit, float maxLimit, float minThumbSize)
	{
		float thumbWidth = (maxValue - minValue) / (maxLimit - minLimit);
		Rect thumbRect = new Rect((position.x + 32) + ((position.width - 64) * (minValue / maxLimit)), position.y, (position.width - 64) * thumbWidth, position.height);
		Rect thumbLeftRect = new Rect(thumbRect.x - 15, thumbRect.y, 15, thumbRect.height);
		Rect thumbRightRect = new Rect(thumbRect.x + thumbRect.width, thumbRect.y, 15, thumbRect.height);

		// Draw Dummy Scrollbar
		GUI.Box(new Rect(position.x + 17, position.y, position.width - 34, position.height), "", (GUIStyle)"horizontalScrollbar");

		if (GUI.Button(new Rect(position.x, position.y, 17, position.height), "", (GUIStyle)"horizontalScrollbarLeftButton"))
		{
			float size = maxValue - minValue;
			minValue -= 0.2f;
			maxValue = minValue + size;

			if (minValue < minLimit)
			{
				minValue = minLimit;
				maxValue = minValue + size;
			}
		}

		if (GUI.Button(new Rect((position.x + position.width) - 17, position.y, 17, position.height), "", (GUIStyle)"horizontalScrollbarRightButton"))
		{
			float size = maxValue - minValue;
			minValue += 0.2f;
			maxValue = minValue + size;

			if (maxValue > maxLimit)
			{
				maxValue = maxLimit;
				minValue = maxValue - size;
			}
		}

		GUI.Box(new Rect(thumbRect.x - 15, thumbRect.y, thumbRect.width + 30, thumbRect.height), "", (GUIStyle)"HorizontalMinMaxScrollbarThumb");

		// Logic
		if (Event.current.type == EventType.MouseDown && draggingScrollbar == 0)
		{
			if (thumbRect.Contains(Event.current.mousePosition))
			{
				draggingScrollbar = 1;
				scrollbarStartOffset = Event.current.mousePosition.x - thumbRect.x + 32;
				Event.current.Use();
			}
			else if (thumbLeftRect.Contains(Event.current.mousePosition))
			{
				draggingScrollbar = 2;
				scrollbarStartOffset = Event.current.mousePosition.x - thumbLeftRect.x + 17;
				Event.current.Use();
			}
			else if (thumbRightRect.Contains(Event.current.mousePosition))
			{
				draggingScrollbar = 3;
				scrollbarStartOffset = Event.current.mousePosition.x - thumbRightRect.x + 32;
				Event.current.Use();
			}
		}

		if (Event.current.type == EventType.MouseDrag && draggingScrollbar == 1)
		{
			float size = maxValue - minValue;
			minValue = ((Event.current.mousePosition.x - scrollbarStartOffset) / (position.width - 64)) * maxLimit;
			maxValue = minValue + size;

			if (minValue < minLimit)
			{
				minValue = minLimit;
				maxValue = minValue + size;
			}
			else if (maxValue > maxLimit)
			{
				maxValue = maxLimit;
				minValue = maxValue - size;
			}

			Event.current.Use();
		}
		else if (Event.current.type == EventType.MouseDrag && draggingScrollbar == 2)
		{
			minValue = ((Event.current.mousePosition.x - scrollbarStartOffset) / (position.width - 64)) * maxLimit;

			if (minValue < minLimit)
			{
				minValue = minLimit;
			}
			minValue = Mathf.Clamp(minValue, 0, maxValue - minThumbSize);

			Event.current.Use();
		}
		else if (Event.current.type == EventType.MouseDrag && draggingScrollbar == 3)
		{
			maxValue = ((Event.current.mousePosition.x - scrollbarStartOffset) / (position.width - 64)) * maxLimit;

			if (maxValue > maxLimit)
			{
				maxValue = maxLimit;
			}
			maxValue = Mathf.Clamp(maxValue, minValue + minThumbSize, FileLength);

			Event.current.Use();
		}

		if (Event.current.type == EventType.ScrollWheel && viewportRect.Contains(Event.current.mousePosition))
		{
			//float viewportSeconds = (maxValue - minValue);
			//float pixelsPerSecond = viewportRect.width / viewportSeconds;

			//float pointerTime = minValue + (Event.current.mousePosition.x / pixelsPerSecond);
			//float viewCentre = minValue + ((maxValue - minValue) / 2);
			//float diff = pointerTime - viewCentre;

			minValue -= Event.current.delta.y / 8f;
			maxValue += Event.current.delta.y / 8f;

			minValue = Mathf.Clamp(minValue, 0, maxValue - minThumbSize);
			maxValue = Mathf.Clamp(maxValue, minValue + minThumbSize, FileLength);

			Event.current.Use();
		}

		if (Event.current.type == EventType.MouseUp && draggingScrollbar > 0)
		{
			draggingScrollbar = 0;
			Event.current.Use();
			return true;
		}

		return false;
	}

	// Customisation Delegates
	public delegate void LipSyncClipEditorMenuDelegate (LipSyncClipSetup instance);
	public delegate void LipSyncClipEditorMenuItemDelegate (LipSyncClipSetup instance, GenericMenu menu);
}