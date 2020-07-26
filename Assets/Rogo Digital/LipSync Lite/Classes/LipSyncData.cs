using UnityEngine;

namespace RogoDigital.Lipsync
{
	[System.Serializable]
	public class LipSyncData : ScriptableObject
	{
		public AudioClip clip;
		public PhonemeMarker[] phonemeData;

		public float version;
		public float length;
		public string transcript;

		public AnimationCurve[] phonemePoseCurves = new AnimationCurve[0];

		public void GenerateCurves (int phonemeCount, int emotionCount)
		{
			phonemePoseCurves = new AnimationCurve[phonemeCount];

			// Create Phoneme Pose Curves
			for (int i = 0; i < phonemePoseCurves.Length; i++)
			{
				phonemePoseCurves[i] = new AnimationCurve(new Keyframe[] { new Keyframe(0, 0), new Keyframe(1, 0) });
			}

			// Generate Phoneme Pose Keyframes
			for (int i = 0; i < phonemeData.Length; i++)
			{
				for (int p = 0; p < phonemePoseCurves.Length; p++)
				{
					if (p == phonemeData[i].phonemeNumber)
						continue;

					phonemePoseCurves[p].AddKey(phonemeData[i].time, 0);
				}

				phonemePoseCurves[phonemeData[i].phonemeNumber].AddKey(phonemeData[i].time, phonemeData[i].intensity);
			}
		}

		public static explicit operator LipSyncData (TemporaryLipSyncData data)
		{
			var output = CreateInstance<LipSyncData>();
			output.phonemeData = new PhonemeMarker[data.phonemeData.Count];

			for (int i = 0; i < data.phonemeData.Count; i++)
			{
				output.phonemeData[i] = data.phonemeData[i].CreateCopy();
			}

			output.clip = data.clip;
			output.version = data.version;
			output.length = data.length;
			output.transcript = data.transcript;

			return output;
		}
	}
}