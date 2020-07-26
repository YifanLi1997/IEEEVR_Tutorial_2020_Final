using UnityEngine;
using UnityEditor;
using UnityEditor.AnimatedValues;
using System;
using System.IO;

namespace RogoDigital.Lipsync
{
	public class ClipSettingsWindow : ModalWindow
	{
		private LipSyncClipSetup setup;

		private float length;
		private string transcript;
		private Vector2 scroll;

		private bool willTrim, adjustMarkers = true;
		private AnimBool adjustMarkersAnimBool;

		private void OnGUI ()
		{
			GUILayout.Space(20);
			scroll = GUILayout.BeginScrollView(scroll);

			EditorGUI.BeginDisabledGroup(setup.Clip);
			GUILayout.Space(10);

			willTrim = length != setup.FileLength;
			TimeSpan time = TimeSpan.FromSeconds(length);

			int minutes = time.Minutes;
			int seconds = time.Seconds;
			int milliseconds = time.Milliseconds;

			GUILayout.BeginHorizontal(GUILayout.MaxWidth(280));
			EditorGUI.BeginChangeCheck();
			GUILayout.Label("Duration");
			minutes = EditorGUILayout.IntField(minutes);
			GUILayout.Label("m", EditorStyles.miniLabel);
			seconds = EditorGUILayout.IntField(seconds);
			GUILayout.Label("s", EditorStyles.miniLabel);
			milliseconds = EditorGUILayout.IntField(milliseconds);
			GUILayout.Label("ms", EditorStyles.miniLabel);
			if (EditorGUI.EndChangeCheck())
			{
				float nl = (minutes * 60) + seconds + (milliseconds / 1000f);
				if (setup.Clip)
					nl = Mathf.Clamp(nl, 0, setup.Clip.length);
				length = nl;
			}
			GUILayout.EndHorizontal();

			EditorGUI.EndDisabledGroup();
			adjustMarkersAnimBool.target = willTrim;
			if (EditorGUILayout.BeginFadeGroup(adjustMarkersAnimBool.faded))
			{
				adjustMarkers = EditorGUILayout.Toggle("Keep Marker Times", adjustMarkers);
			}
			EditorGUILayout.EndFadeGroup();

			if (setup.Clip)
				EditorGUILayout.HelpBox("Cannot Change duration as an AudioClip has been added. You will need to trim the clip manually or upgrade to LipSync Pro for built-in trimming.", MessageType.Warning);

			GUILayout.Space(10);
			GUILayout.Label("Transcript");
			transcript = GUILayout.TextArea(transcript, GUILayout.MinHeight(90));

			GUILayout.Space(20);
			GUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			if (GUILayout.Button("Save", GUILayout.MinWidth(100), GUILayout.Height(20)))
			{
				setup.Transcript = transcript;
				if (willTrim)
				{

					if (adjustMarkers)
						AdjustMarkers(0, length);

					setup.FileLength = length;
				}

				setup.changed = true;
				setup.previewOutOfDate = true;
				Close();
			}
			GUILayout.Space(10);
			if (GUILayout.Button("Cancel", GUILayout.MinWidth(100), GUILayout.Height(20)))
			{
				Close();
			}
			GUILayout.FlexibleSpace();
			GUILayout.EndHorizontal();
			GUILayout.EndScrollView();
		}

		void AdjustMarkers (double newStartTime, double newLength)
		{
			// Times
			float newStartNormalised = 1 - ((setup.FileLength - (float)newStartTime) / setup.FileLength);
			float newEndNormalised = ((float)newStartTime + (float)newLength) / setup.FileLength;

			// Adjust Marker timings (go backwards so indices don't change)
			float multiplier = 1 / (newEndNormalised - newStartNormalised);
			for (int p = setup.PhonemeData.Count - 1; p >= 0; p--)
			{
				if (setup.PhonemeData[p].time < newStartNormalised || setup.PhonemeData[p].time > newEndNormalised)
				{
					setup.PhonemeData.RemoveAt(p);
				}
				else
				{
					setup.PhonemeData[p].time -= newStartNormalised;
					setup.PhonemeData[p].time *= multiplier;
				}
			}
		}

		public static ClipSettingsWindow CreateWindow (ModalParent parent, LipSyncClipSetup setup)
		{
			ClipSettingsWindow window = CreateInstance<ClipSettingsWindow>();

			window.length = setup.FileLength;
			window.transcript = setup.Transcript;

			window.position = new Rect(parent.center.x - 250, parent.center.y - 100, 500, 200);
			window.minSize = new Vector2(500, 200);
			window.titleContent = new GUIContent("Clip Settings");

			window.adjustMarkersAnimBool = new AnimBool(window.willTrim, window.Repaint);

			window.setup = setup;
			window.Show(parent);
			return window;
		}
	}
}