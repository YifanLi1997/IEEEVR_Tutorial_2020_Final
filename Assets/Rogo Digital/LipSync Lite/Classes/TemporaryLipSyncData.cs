using System.Collections.Generic;
using UnityEngine;

namespace RogoDigital.Lipsync
{
	public class TemporaryLipSyncData : ScriptableObject
	{
		public AudioClip clip;
		public List<PhonemeMarker> phonemeData;

		public float version;
		public float length = 10;
		public string transcript = "";

		private void OnEnable ()
		{
			hideFlags = HideFlags.DontSaveInBuild | HideFlags.DontSaveInEditor;
		}

		public static explicit operator TemporaryLipSyncData (LipSyncData data)
		{
			var output = CreateInstance<TemporaryLipSyncData>();

			// Data
			output.phonemeData = new List<PhonemeMarker>();

			for (int i = 0; i < data.phonemeData.Length; i++)
			{
				output.phonemeData.Add(data.phonemeData[i].CreateCopy());
			}

			output.clip = data.clip;
			output.version = data.version;
			output.length = data.length;
			output.transcript = data.transcript;

			return output;
		}
	}
}