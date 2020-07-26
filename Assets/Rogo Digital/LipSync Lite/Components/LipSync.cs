using UnityEngine;
using UnityEngine.Events;
using System.Collections;
using System.Collections.Generic;
using System.Xml;

namespace RogoDigital.Lipsync
{

	[AddComponentMenu("Rogo Digital/LipSync Lite")]
	[DisallowMultipleComponent]
	[HelpURL("https://lipsync.rogodigital.com/documentation/lipsync.php")]
	public class LipSync : BlendSystemUser
	{
#pragma warning disable 618

		// Public Variables

		/// <summary>
		/// AudioSource used for playing dialogue
		/// </summary>
		public AudioSource audioSource;

		/// <summary>
		/// Allow bones to be used in phoneme shapes.
		/// </summary>
		public bool useBones = false;

		/// <summary>
		/// Used for deciding if/when to repose boneshapes in LateUpdate.
		/// </summary>
		public bool boneUpdateAnimation = false;

		/// <summary>
		/// All PhonemeShapes on this LipSync instance.
		/// PhonemeShapes are a list of blendables and
		/// weights associated with a particular phoneme.
		/// </summary>
		[SerializeField]
		public List<PhonemeShape> phonemes = new List<PhonemeShape>();

		/// <summary>
		/// If checked, the component will play defaultClip on awake.
		/// </summary>
		public bool playOnAwake = false;

		/// <summary>
		/// If checked, the clip will play again when it finishes.
		/// </summary>
		public bool loop = false;

		/// <summary>
		/// The clip to be played when playOnAwake is checked.
		/// </summary>
		public LipSyncData defaultClip = null;

		/// <summary>
		/// The delay between calling Play() and the clip playing.
		/// </summary>
		public float defaultDelay = 0f;

		/// <summary>
		/// If true, audio playback speed will match the timescale setting (allows slow or fast motion speech)
		/// </summary>
		public bool scaleAudioSpeed = true;

		[SerializeField]
		private AnimationTimingMode m_animationTimingMode = AnimationTimingMode.AudioPlayback;
		/// <summary>
		/// How animation playback is timed. AudioPlayback is linked to the audio position. FixedFrameRate assumes a constant speed (useful for offline rendering).
		/// </summary>
		public AnimationTimingMode animationTimingMode
		{
			get
			{
				return m_animationTimingMode;
			}
			set
			{
#if UNITY_WEBGL
				if(value == AnimationTimingMode.AudioPlayback) {
					Debug.LogError("AnimationTimingMode.AudioPlayback is not supported on WebGL. Falling back to AnimationTimingMode.CustomTimer");
					m_animationTimingMode = AnimationTimingMode.CustomTimer;
				} else {
					m_animationTimingMode = value;
				}
#endif
				m_animationTimingMode = value;
			}
		}

		/// <summary>
		/// The framerate used for fixed framerate rendering.
		/// </summary>
		public int frameRate = 30;

		/// <summary>
		/// If there are no phonemes within this many seconds
		/// of the previous one, a rest will be inserted.
		/// </summary>
		public float restTime = 0.2f;

		/// <summary>
		/// The time, in seconds, that a shape will be held for
		/// before blending to neutral when a rest is inserted.
		/// </summary>
		public float restHoldTime = 0.4f;

		/// <summary>
		/// The method used for generating curve tangents. Tight will ensure poses
		/// are matched exactly, but can make movement robotic, Loose will look
		/// more natural but can can cause poses to be over-emphasized.
		/// </summary>
		public CurveGenerationMode phonemeCurveGenerationMode = CurveGenerationMode.Loose;

		/// <summary>
		/// Whether or not there is currently a LipSync animation playing.
		/// </summary>
		public bool IsPlaying
		{
			get;
			private set;
		}

		/// <summary>
		/// Whether the currently playing animation is paused.
		/// </summary>
		public bool IsPaused
		{
			get;
			private set;
		}

		/// <summary>
		/// Whether the currently playing animation is transitioning back to neutral.
		/// </summary>
		public bool IsStopping
		{
			get;
			private set;
		}

		/// <summary>
		/// Gets the current playback time, in seconds.
		/// </summary>
		public float CurrentTime
		{
			get
			{
				if (!IsPlaying)
					return 0;

				switch (animationTimingMode)
				{
					case AnimationTimingMode.AudioPlayback:
						return audioSource.time;
					default:
						return customTimer;
				}
			}
		}

		/// <summary>
		/// Called when a clip finished playing.
		/// </summary>
		public UnityEvent onFinishedPlaying;

#if UNITY_EDITOR
		/// <summary>
		/// Used for updating Clip Editor previews. Only available in the editor.
		/// </summary>
		public BlendSystem.BlendSystemGenericDelegate onSettingsChanged;
#endif

		// Private Variables

		private AudioClip audioClip;
		private bool ready = false;
		private int currentFileID = 0;
		private LipSyncData lastClip;

		private float customTimer = 0;
		private bool isDelaying = false;

		// Marker Data
		private List<PhonemeMarker> phonemeMarkers;

		private float fileLength;

		// Curves
		private List<int> indexBlendables;
		public List<AnimationCurve> animCurves;

		private List<Transform> bones;
		private List<TransformAnimationCurve> boneCurves;

		private List<Vector3> boneNeutralPositions;
		private List<Vector3> boneNeutralScales;
		private List<Quaternion> boneNeutralRotations;

		// Used by the editor
		public ResetDelegate reset;
		public float lastUsedVersion = 0;

		public delegate void ResetDelegate ();

		void Reset ()
		{
			CleanUpBlendSystems();
			if (reset != null)
				reset.Invoke();
		}

		void Awake ()
		{

			// Get reference to attached AudioSource
			if (audioSource == null)
				audioSource = GetComponent<AudioSource>();

			// Ensure BlendSystem is set to allow animation
			if (audioSource == null)
			{
				Debug.LogError("[LipSync - " + gameObject.name + "] No AudioSource specified or found.");
				return;
			}
			else if (blendSystem == null)
			{
				Debug.LogError("[LipSync - " + gameObject.name + "] No BlendSystem set.");
				return;
			}
			else if (blendSystem.isReady == false)
			{
				Debug.LogError("[LipSync - " + gameObject.name + "] BlendSystem is not set up.");
				return;
			}
			else
			{
				ready = true;
			}

			// Check for old-style settings
			if (restTime < 0.1f)
			{
				Debug.LogWarning("[LipSync - " + gameObject.name + "] Rest Time and/or Hold Time are lower than recommended and may cause animation errors. From LipSync 0.6, Rest Time is recommended to be 0.2 and Hold Time is recommended to be 0.1");
			}

			// Start Playing if playOnAwake is set
			if (playOnAwake && defaultClip != null)
				Play(defaultClip, defaultDelay);
		}


		void LateUpdate ()
		{
			if ((IsPlaying && !IsPaused) || IsStopping)
			{
				// Scale audio speed if set
				if (scaleAudioSpeed)
					audioSource.pitch = Time.timeScale;

				if (isDelaying)
				{
					customTimer -= Time.deltaTime;
					if (customTimer <= 0)
						isDelaying = false;
					return;
				}

				float normalisedTimer = 0;

				if (IsPlaying || IsStopping)
				{
					// Update timer based on animationTimingMode
					if (animationTimingMode == AnimationTimingMode.AudioPlayback && audioClip != null && IsPlaying)
					{
						// Use AudioSource playback only if an audioClip is present.
						normalisedTimer = audioSource.time / audioClip.length;
					}
					else if (animationTimingMode == AnimationTimingMode.CustomTimer || (animationTimingMode == AnimationTimingMode.AudioPlayback && audioClip == null) || IsStopping)
					{
						// Play at same rate, but don't tie to audioclip. Fallback for AnimationTimingMode.AudioPlayback when no clip is present.
						customTimer += Time.deltaTime;
						normalisedTimer = customTimer / (IsStopping ? restHoldTime : fileLength);
					}
					else if (animationTimingMode == AnimationTimingMode.FixedFrameRate)
					{
						// Play animation at a fixed framerate for offline rendering.
						customTimer += 1f / frameRate;
						normalisedTimer = customTimer / fileLength;
					}
				}

				// Go through each animCurve and update blendables
				for (int curve = 0; curve < animCurves.Count; curve++)
				{
					blendSystem.SetBlendableValue(indexBlendables[curve], animCurves[curve].Evaluate(normalisedTimer));
				}

				// Do the same for bones
				if (useBones && boneCurves != null)
				{
					for (int curve = 0; curve < boneCurves.Count; curve++)
					{
						if (boneUpdateAnimation == false)
						{
							bones[curve].localPosition = boneCurves[curve].EvaluatePosition(normalisedTimer);
							bones[curve].localRotation = boneCurves[curve].EvaluateRotation(normalisedTimer);
							bones[curve].localScale = boneCurves[curve].EvaluateScale(normalisedTimer);
						}
						else
						{
							// Get transform relative to current animation frame
							Vector3 newPos = boneCurves[curve].EvaluatePosition(normalisedTimer) - boneNeutralPositions[curve];
							Vector3 newRot = boneCurves[curve].EvaluateRotation(normalisedTimer).eulerAngles - boneNeutralRotations[curve].eulerAngles;
							Vector3 newScale = boneCurves[curve].EvaluateScale(normalisedTimer) - boneNeutralScales[curve];

							bones[curve].localPosition += newPos;
							bones[curve].localEulerAngles += newRot;
							bones[curve].localScale += newScale;
						}
					}
				}

				if ((normalisedTimer >= 0.98f))
				{
					if (IsStopping)
					{
						IsStopping = false;
					}
					else
					{
						if (loop)
						{
							Stop(false);
							Play(lastClip);
						}
						else
						{
							Stop(false);
						}
					}
				}
			}
		}

		// Public Functions

		/// <summary>
		/// Loads a LipSyncData file if necessary and
		/// then plays it on the current LipSync component.
		/// </summary>
		public void Play (LipSyncData dataFile, float delay)
		{
			if (ready && enabled)
			{
				// Load File if not already loaded
				bool loadSuccessful = true;
				if (dataFile.GetInstanceID() != currentFileID)
				{
					loadSuccessful = LoadData(dataFile);
				}
				if (!loadSuccessful)
					return;

				ProcessData();

				// Set variables
				IsPlaying = true;
				IsPaused = false;
				IsStopping = false;

				if (audioClip && delay > 0)
				{
					isDelaying = true;
					customTimer = delay;
				}
				else
				{
					isDelaying = false;
					customTimer = 0;
				}

				// Play audio
				if (audioClip && audioSource)
					audioSource.PlayDelayed(delay);
			}
		}

		/// <summary>
		/// Overload of Play with no delay specified. For compatibility with pre 0.4 scripts.
		/// </summary>
		public void Play (LipSyncData dataFile)
		{
			Play(dataFile, 0);
		}

		/// <summary>
		/// Loads an XML file and parses LipSync data from it,
		/// then plays it on the current LipSync component.
		/// </summary>
		public void Play (TextAsset xmlFile, AudioClip clip, float delay)
		{
			if (ready && enabled)
			{
				// Load File
				LoadXML(xmlFile, clip);

				// Set variables
				IsPlaying = true;
				IsPaused = false;
				IsStopping = false;

				ProcessData();

				if (audioClip && delay > 0)
				{
					isDelaying = true;
					customTimer = delay;
				}
				else
				{
					isDelaying = false;
					customTimer = 0;
				}

				// Play audio
				audioSource.PlayDelayed(delay);
			}
		}

		/// <summary>
		/// Overload of Play with no delay specified. For compatibility with pre 0.4 scripts.
		/// </summary>
		public void Play (TextAsset xmlFile, AudioClip clip)
		{
			Play(xmlFile, clip, 0);
		}

		/// <summary>
		/// Loads a LipSyncData file if necessary and
		/// then plays it on the current LipSync component
		/// from a certain point in seconds.
		/// </summary>
		public void PlayFromTime (LipSyncData dataFile, float delay, float time)
		{
			if (ready && enabled)
			{
				// Load File if not already loaded
				bool loadSuccessful = true;
				if (dataFile.GetInstanceID() != currentFileID)
				{
					loadSuccessful = LoadData(dataFile);
				}
				if (!loadSuccessful)
					return;

				// Check that time is within range
				if (time >= fileLength)
				{
					Debug.LogError("[LipSync - " + gameObject.name + "] Couldn't play animation. Time parameter is greater than clip length.");
					return;
				}

				ProcessData();

				// Set variables
				IsPlaying = true;
				IsPaused = false;
				isDelaying = false;
				customTimer = 0;
				IsStopping = false;

				// Play audio
				audioSource.Play();
				audioSource.time = time + delay;
			}
		}

		/// <summary>
		/// Overload of PlayFromTime with no delay specified.
		/// </summary>
		public void PlayFromTime (LipSyncData dataFile, float time)
		{
			PlayFromTime(dataFile, 0, time);
		}

		/// <summary>
		/// Loads an XML file and parses LipSync data from it,
		/// then plays it on the current LipSync component
		/// from a certain point in seconds.
		/// </summary>
		public void PlayFromTime (TextAsset xmlFile, AudioClip clip, float delay, float time)
		{
			if (ready && enabled)
			{
				// Load File
				LoadXML(xmlFile, clip);

				// Check that time is within range
				if (time >= fileLength)
				{
					Debug.LogError("[LipSync - " + gameObject.name + "] Couldn't play animation. Time parameter is greater than clip length.");
					return;
				}

				// Set variables
				IsPlaying = true;
				IsPaused = false;
				isDelaying = false;
				customTimer = 0;

				IsStopping = false;

				ProcessData();

				// Play audio
				audioSource.Play();
				audioSource.time = time + delay;
			}
		}

		/// <summary>
		/// Overload of PlayFromTime with no delay specified.
		/// </summary>
		public void PlayFromTime (TextAsset xmlFile, AudioClip clip, float time)
		{
			PlayFromTime(xmlFile, clip, 0, time);
		}

		/// <summary>
		/// Pauses the currently playing animation.
		/// </summary>
		public void Pause ()
		{
			if (IsPlaying && !IsPaused && enabled)
			{
				IsPaused = true;
				audioSource.Pause();
			}
		}

		/// <summary>
		/// Resumes the current animation after pausing.
		/// </summary>
		public void Resume ()
		{
			if (IsPlaying && IsPaused && enabled)
			{
				IsPaused = false;
				audioSource.UnPause();
			}
		}

		/// <summary>
		/// Completely stops the current animation to be
		/// started again from the begining.
		/// </summary>
		public void Stop (bool stopAudio)
		{
			if (IsPlaying && enabled)
			{
				IsPlaying = false;
				IsPaused = false;
				isDelaying = false;
				IsStopping = true;
				customTimer = 0;

				// Blend out
				for (int c = 0; c < animCurves.Count; c++)
				{
					float finalValue = animCurves[c].Evaluate(1);
					float startingValue = blendSystem.GetBlendableValue(indexBlendables[c]);

					animCurves[c] = new AnimationCurve(new Keyframe[] { new Keyframe(0, startingValue), new Keyframe(1, finalValue) });
				}

				if (useBones)
				{
					for (int b = 0; b < boneCurves.Count; b++)
					{
						Vector3 finalPosition = boneCurves[b].EvaluatePosition(1);
						Vector3 finalScale = boneCurves[b].EvaluateScale(1);
						Quaternion finalRotation = boneCurves[b].EvaluateRotation(1);
						Vector3 startingPosition = bones[b].localPosition;
						Vector3 startingScale = bones[b].localScale;
						Quaternion startingRotation = bones[b].localRotation;

						boneCurves[b] = new TransformAnimationCurve();
						boneCurves[b].AddKey(0, startingPosition, startingRotation, startingScale, 0, 0);
						boneCurves[b].AddKey(1, finalPosition, finalRotation, finalScale, 0, 0);
					}
				}

				// Stop Audio
				if (stopAudio)
					audioSource.Stop();

				//Invoke Callback
				onFinishedPlaying.Invoke();
			}
		}

		/// <summary>
		/// Sets blendables to their state at a certain time in the animation.
		/// ProcessData must have already been called.
		/// </summary>
		/// <param name="time">Time.</param>
		public void PreviewAtTime (float time)
		{
			if (!IsPlaying && enabled && animCurves != null)
			{
				// Sanity check
				if (indexBlendables == null || animCurves == null)
				{
					// Data hasn't been loaded
					if (phonemeMarkers == null)
						return;

					// Otherwise, recreate animation data
					ProcessData();
				}
				else if (indexBlendables.Count != animCurves.Count)
				{
					// Data hasn't been loaded
					if (phonemeMarkers == null)
						return;

					// Otherwise, recreate animation data
					ProcessData();
				}

				// Go through each animCurve and update blendables
				for (int curve = 0; curve < animCurves.Count; curve++)
				{
					blendSystem.SetBlendableValue(indexBlendables[curve], animCurves[curve].Evaluate(time));
				}

				if (useBones && boneCurves != null)
				{
					for (int curve = 0; curve < boneCurves.Count; curve++)
					{
						if (bones[curve] != null)
							bones[curve].localPosition = boneCurves[curve].EvaluatePosition(time);
						if (bones[curve] != null)
							bones[curve].localRotation = boneCurves[curve].EvaluateRotation(time);
					}
				}
			}
		}

		public void PreviewAudioAtTime (float time, float length)
		{
			if (IsPlaying || !audioSource)
				return;

			if (!audioSource.isPlaying)
			{
				audioSource.PlayOneShot(audioClip);
				if (time <= 1)
					audioSource.time = time * audioClip.length;
				StartCoroutine(StopAudioSource(length));
			}
		}

		/// <summary>
		/// Loads raw data instead of using a serialised asset.
		/// Used for previewing animations in the editor.
		/// </summary>
		/// <param name="pData">Phoneme data.</param>
		/// <param name="eData">Emotion data.</param>
		/// <param name="clip">Audio Clip.</param>
		/// <param name="duration">File Duration.</param>
		public void TempLoad (List<PhonemeMarker> pData, AudioClip clip, float duration)
		{
			TempLoad(pData.ToArray(), clip, duration);
		}

		/// <summary>
		/// Loads raw data instead of using a serialised asset.
		/// Used for previewing animations in the editor.
		/// </summary>
		/// <param name="pData">Phoneme data.</param>
		/// <param name="eData">Emotion data.</param>
		/// <param name="clip">Audio Clip.</param>
		/// <param name="duration">File Duration.</param>
		public void TempLoad (PhonemeMarker[] pData, AudioClip clip, float duration)
		{
			if (enabled)
			{
				// Clear/define marker lists, to overwrite any previous file
				phonemeMarkers = new List<PhonemeMarker>();

				// Copy data from file into new lists
				foreach (PhonemeMarker marker in pData)
				{
					phonemeMarkers.Add(marker);
				}

				// Phonemes are stored out of sequence in the file, for depth sorting in the editor
				// Sort them by timestamp to make finding the current one faster
				phonemeMarkers.Sort(SortTime);

				audioClip = clip;
				fileLength = duration;
			}
		}

		/// <summary>
		/// Processes the data into readable animation curves.
		/// Do not call before loading data.
		/// </summary>
		public void ProcessData ()
		{
			if (enabled)
			{

				#region Setup/Definition

				boneNeutralPositions = null;
				boneNeutralRotations = null;
				boneNeutralScales = null;

				List<Transform> tempBones = null;
				List<TransformAnimationCurve> tempBoneCurves = null;

				List<int> tempIndexBlendables = new List<int>();
				List<AnimationCurve> tempCurves = new List<AnimationCurve>();

				Dictionary<int, float> blendableNeutralValues = new Dictionary<int, float>();
				PhonemeShape restPhoneme = null;
				for (int i = 0; i < phonemes.Count; i++)
				{
					if (phonemes[i].phonemeName.ToLowerInvariant() == "rest")
						restPhoneme = phonemes[i];
				}

				indexBlendables = new List<int>();
				animCurves = new List<AnimationCurve>();

				phonemeMarkers.Sort(SortTime);

				if (useBones)
				{
					boneNeutralPositions = new List<Vector3>();
					boneNeutralRotations = new List<Quaternion>();
					boneNeutralScales = new List<Vector3>();

					bones = new List<Transform>();
					boneCurves = new List<TransformAnimationCurve>();

					tempBones = new List<Transform>();
					tempBoneCurves = new List<TransformAnimationCurve>();
				}

				List<Shape> shapes = new List<Shape>();
				#endregion

				#region Get Phoneme Info
				// Add phonemes used
				foreach (PhonemeMarker marker in phonemeMarkers)
				{
					if (shapes.Count == phonemes.Count)
					{
						break;
					}

					if (!shapes.Contains(phonemes[marker.phonemeNumber]))
					{
						shapes.Add(phonemes[marker.phonemeNumber]);

						foreach (int blendable in phonemes[marker.phonemeNumber].blendShapes)
						{
							if (!tempIndexBlendables.Contains(blendable))
							{
								AnimationCurve curve = new AnimationCurve();
								curve.postWrapMode = WrapMode.Once;
								tempCurves.Add(curve);
								tempIndexBlendables.Add(blendable);
							}

							if (!indexBlendables.Contains(blendable))
							{
								AnimationCurve curve = new AnimationCurve();
								curve.postWrapMode = WrapMode.Once;
								animCurves.Add(curve);
								indexBlendables.Add(blendable);
							}

							if (!blendableNeutralValues.ContainsKey(blendable))
							{
								blendableNeutralValues.Add(blendable, 0);
							}
						}

						if (useBones && boneCurves != null)
						{
							foreach (BoneShape boneShape in phonemes[marker.phonemeNumber].bones)
							{
								if (!tempBones.Contains(boneShape.bone))
								{
									TransformAnimationCurve curve = new TransformAnimationCurve();
									curve.postWrapMode = WrapMode.Once;
									tempBoneCurves.Add(curve);
									tempBones.Add(boneShape.bone);
								}

								if (!bones.Contains(boneShape.bone))
								{
									TransformAnimationCurve curve = new TransformAnimationCurve();
									curve.postWrapMode = WrapMode.Once;
									boneCurves.Add(curve);
									bones.Add(boneShape.bone);

									boneNeutralPositions.Add(boneShape.neutralPosition);
									boneNeutralRotations.Add(Quaternion.Euler(boneShape.neutralRotation.ToNegativeEuler()));
									boneNeutralScales.Add(boneShape.neutralScale);
								}
							}
						}
					}
				}
				#endregion

				#region Extras (SetEmotion, Rest pose)

				// Add any blendable not otherwise used, that appear in the rest phoneme
				if (restPhoneme != null)
				{
					foreach (int blendable in restPhoneme.blendShapes)
					{
						if (!tempIndexBlendables.Contains(blendable))
						{
							AnimationCurve curve = new AnimationCurve();
							curve.postWrapMode = WrapMode.Once;
							tempCurves.Add(curve);
							tempIndexBlendables.Add(blendable);
						}

						if (!indexBlendables.Contains(blendable))
						{
							AnimationCurve curve = new AnimationCurve();
							curve.postWrapMode = WrapMode.Once;
							animCurves.Add(curve);
							indexBlendables.Add(blendable);
						}

						if (!blendableNeutralValues.ContainsKey(blendable))
						{
							blendableNeutralValues.Add(blendable, 0);
						}
					}

					if (useBones && boneCurves != null)
					{
						foreach (BoneShape boneShape in restPhoneme.bones)
						{
							if (!tempBones.Contains(boneShape.bone))
							{
								TransformAnimationCurve curve = new TransformAnimationCurve();
								curve.postWrapMode = WrapMode.Once;
								tempBoneCurves.Add(curve);
								tempBones.Add(boneShape.bone);
							}

							if (!bones.Contains(boneShape.bone))
							{
								TransformAnimationCurve curve = new TransformAnimationCurve();
								curve.postWrapMode = WrapMode.Once;
								boneCurves.Add(curve);
								bones.Add(boneShape.bone);

								boneNeutralPositions.Add(boneShape.neutralPosition);
								boneNeutralRotations.Add(Quaternion.Euler(boneShape.neutralRotation.ToNegativeEuler()));
								boneNeutralScales.Add(boneShape.neutralScale);
							}
						}
					}
				}

				// Get neutral values
				for (int i = 0; i < indexBlendables.Count; i++)
				{
					if (restPhoneme != null)
					{
						if (restPhoneme.blendShapes.Contains(indexBlendables[i]))
						{
							blendableNeutralValues[indexBlendables[i]] = restPhoneme.weights[restPhoneme.blendShapes.IndexOf(indexBlendables[i])];
						}
						else
						{
							blendableNeutralValues[indexBlendables[i]] = 0;
						}
					}
					else
					{
						blendableNeutralValues[indexBlendables[i]] = 0;
					}
				}

				if (useBones && boneCurves != null)
				{
					for (int i = 0; i < bones.Count; i++)
					{
						if (restPhoneme != null)
						{
							if (restPhoneme.HasBone(bones[i]))
							{
								boneNeutralPositions[i] = restPhoneme.bones[restPhoneme.IndexOfBone(bones[i])].endPosition;
								boneNeutralRotations[i] = Quaternion.Euler(restPhoneme.bones[restPhoneme.IndexOfBone(bones[i])].endRotation);
								boneNeutralScales[i] = restPhoneme.bones[restPhoneme.IndexOfBone(bones[i])].endScale;
							}
						}
					}
				}
				#endregion

				#region Add Start & End Keys
				// Add neutral start and end keys, or get keys from current custom emotion
				for (int index = 0; index < tempCurves.Count; index++)
				{
						tempCurves[index].AddKey(0, blendableNeutralValues[tempIndexBlendables[index]]);
						tempCurves[index].AddKey(1, blendableNeutralValues[tempIndexBlendables[index]]);
				}

				if (useBones && boneCurves != null)
				{
					for (int index = 0; index < tempBoneCurves.Count; index++)
					{
							tempBoneCurves[index].AddKey(0, boneNeutralPositions[bones.IndexOf(tempBones[index])], boneNeutralRotations[bones.IndexOf(tempBones[index])], boneNeutralScales[bones.IndexOf(tempBones[index])], 0, 0);
							tempBoneCurves[index].AddKey(1, boneNeutralPositions[bones.IndexOf(tempBones[index])], boneNeutralRotations[bones.IndexOf(tempBones[index])], boneNeutralScales[bones.IndexOf(tempBones[index])], 0, 0);
					}
				}
				#endregion

				#region Add Phoneme Marker Keys
				// Get keys from phoneme track
				for (int m = 0; m < phonemeMarkers.Count; m++)
				{
					PhonemeMarker marker = phonemeMarkers[m];
					PhonemeShape shape = phonemes[marker.phonemeNumber];

					float intensityMod = 1;
					float blendableMod = 1;
					float bonePosMod = 1;
					float boneRotMod = 1;

					// Get Random Modifier
					if (marker.useRandomness)
					{
						intensityMod = Random.Range(1 - (marker.intensityRandomness / 2), 1 + (marker.intensityRandomness / 2));
					}

					bool addRest = false;

					// Check for rests
					if (!marker.sustain)
					{
						if (m + 1 < phonemeMarkers.Count)
						{
							if (phonemeMarkers[m + 1].time > marker.time + (restTime / fileLength) + (restHoldTime / fileLength))
							{
								addRest = true;
							}
						}
						else
						{
							// Last marker, add rest after hold time
							addRest = true;
						}
					}

					for (int index = 0; index < tempCurves.Count; index++)
					{
						if (shape.blendShapes.Contains(tempIndexBlendables[index]))
						{
							int b = shape.blendShapes.IndexOf(tempIndexBlendables[index]);

							// Get Random Other Modifiers
							if (marker.useRandomness)
							{
								blendableMod = Random.Range(1 - (marker.blendableRandomness / 2), 1 + (marker.blendableRandomness / 2));
							}

							if (phonemeCurveGenerationMode == CurveGenerationMode.Tight)
							{
								tempCurves[index].AddKey(new Keyframe(marker.time, shape.weights[b] * marker.intensity * intensityMod * blendableMod, 0, 0));

								//Check for pre-rest
								if (m == 0)
								{
									tempCurves[index].AddKey(new Keyframe(phonemeMarkers[m].time - (restHoldTime / fileLength), blendableNeutralValues[tempIndexBlendables[index]], 0, 0));
								}

								if (addRest)
								{
									// Add rest
									tempCurves[index].AddKey(new Keyframe(marker.time + (restHoldTime / fileLength), shape.weights[b] * marker.intensity * intensityMod * blendableMod, 0, 0));
									tempCurves[index].AddKey(new Keyframe(marker.time + ((restHoldTime / fileLength) * 2), blendableNeutralValues[tempIndexBlendables[index]], 0, 0));
									if (m + 1 < phonemeMarkers.Count)
									{
										tempCurves[index].AddKey(new Keyframe(phonemeMarkers[m + 1].time - (restHoldTime / fileLength), blendableNeutralValues[tempIndexBlendables[index]], 0, 0));
									}
								}
							}
							else if (phonemeCurveGenerationMode == CurveGenerationMode.Loose)
							{
								tempCurves[index].AddKey(marker.time, shape.weights[b] * marker.intensity);

								//Check for pre-rest
								if (m == 0)
								{
									tempCurves[index].AddKey(phonemeMarkers[m].time - (restHoldTime / fileLength), blendableNeutralValues[tempIndexBlendables[index]]);
								}

								if (addRest)
								{
									// Add rest
									tempCurves[index].AddKey(marker.time + (restHoldTime / fileLength), shape.weights[b] * marker.intensity * intensityMod * blendableMod);
									tempCurves[index].AddKey(marker.time + ((restHoldTime / fileLength) * 2), blendableNeutralValues[tempIndexBlendables[index]]);
									if (m + 1 < phonemeMarkers.Count)
									{
										tempCurves[index].AddKey(phonemeMarkers[m + 1].time - (restHoldTime / fileLength), blendableNeutralValues[tempIndexBlendables[index]]);
									}
								}
							}

						}
						else
						{
							// Blendable isn't in this marker
							if (phonemeCurveGenerationMode == CurveGenerationMode.Tight)
							{
								tempCurves[index].AddKey(new Keyframe(marker.time, blendableNeutralValues[tempIndexBlendables[index]], 0, 0));
							}
							else if (phonemeCurveGenerationMode == CurveGenerationMode.Loose)
							{
								tempCurves[index].AddKey(marker.time, blendableNeutralValues[tempIndexBlendables[index]]);
							}
							if (addRest)
							{
								if (m + 1 < phonemeMarkers.Count)
								{
									if (phonemeCurveGenerationMode == CurveGenerationMode.Tight)
									{
										tempCurves[index].AddKey(new Keyframe(phonemeMarkers[m + 1].time - (restHoldTime / fileLength), blendableNeutralValues[tempIndexBlendables[index]], 0, 0));
									}
									else if (phonemeCurveGenerationMode == CurveGenerationMode.Loose)
									{
										tempCurves[index].AddKey(phonemeMarkers[m + 1].time - (restHoldTime / fileLength), blendableNeutralValues[tempIndexBlendables[index]]);
									}
								}
							}
						}
					}

					if (useBones && boneCurves != null)
					{
						for (int index = 0; index < tempBoneCurves.Count; index++)
						{
							if (shape.HasBone(bones[index]))
							{
								int b = shape.IndexOfBone(bones[index]);

								// Get Random Other Modifiers
								if (marker.useRandomness)
								{
									bonePosMod = Random.Range(1 - (marker.bonePositionRandomness / 2), 1 + (marker.bonePositionRandomness / 2));
									boneRotMod = Random.Range(1 - (marker.boneRotationRandomness / 2), 1 + (marker.boneRotationRandomness / 2));
								}

								tempBoneCurves[index].AddKey(marker.time, Vector3.Lerp(shape.bones[b].neutralPosition, shape.bones[b].endPosition * bonePosMod, marker.intensity * intensityMod), Quaternion.Slerp(Quaternion.Euler(shape.bones[b].neutralRotation), Quaternion.Euler(shape.bones[b].endRotation * boneRotMod), marker.intensity), Vector3.Lerp(shape.bones[b].neutralScale, shape.bones[b].endScale, marker.intensity * intensityMod), 0, 0);

								//Check for pre-rest
								if (m == 0)
								{
									tempBoneCurves[index].AddKey(phonemeMarkers[m].time - (restHoldTime / fileLength), boneNeutralPositions[index], boneNeutralRotations[index], boneNeutralScales[index], 0, 0);
								}

								if (addRest)
								{
									// Add rest
									tempBoneCurves[index].AddKey(marker.time + (restHoldTime / fileLength), boneNeutralPositions[index], boneNeutralRotations[index], boneNeutralScales[index], 0, 0);
									if (m + 1 < phonemeMarkers.Count)
									{
										tempBoneCurves[index].AddKey(phonemeMarkers[m + 1].time - (restHoldTime / fileLength), boneNeutralPositions[index], boneNeutralRotations[index], boneNeutralScales[index], 0, 0);
									}
								}
							}
							else
							{
								// Blendable isn't in this marker, get value from matching emotion curve if available

								tempBoneCurves[index].AddKey(marker.time, boneNeutralPositions[index], boneNeutralRotations[index], boneNeutralScales[index], 0, 0);

								if (addRest)
								{
									if (m + 1 < phonemeMarkers.Count)
									{
										tempBoneCurves[index].AddKey(phonemeMarkers[m + 1].time - (restHoldTime / fileLength), boneNeutralPositions[index], boneNeutralRotations[index], boneNeutralScales[index], 0, 0);
									}
								}
							}
						}
					}
				}
				#endregion

				#region Composite Animation
				// Merge curve sets
				for (int c = 0; c < animCurves.Count; c++)
				{
					if (tempIndexBlendables.Contains(indexBlendables[c]))
					{
						int pIndex = tempIndexBlendables.IndexOf(indexBlendables[c]);

						for (int k = 0; k < tempCurves[pIndex].keys.Length; k++)
						{
							Keyframe key = tempCurves[pIndex].keys[k];
							animCurves[c].AddKey(key);
						}

					}
				}

				if (useBones && boneCurves != null)
				{
					for (int c = 0; c < boneCurves.Count; c++)
					{
						if (tempBones.Contains(bones[c]))
						{
							int pIndex = tempBones.IndexOf(bones[c]);

							foreach (TransformAnimationCurve.TransformKeyframe key in tempBoneCurves[pIndex].keys)
							{
								boneCurves[c].AddKey(key.time, key.position, key.rotation, key.scale, 0, 0);
							}
						}
					}

					// Fix Quaternion rotations (Credit: Chris Lewis)
					foreach (TransformAnimationCurve curve in boneCurves)
					{
						curve.FixQuaternionContinuity();
					}
				}
				#endregion
			}
		}

		/// <summary>
		/// Clears the data cache, forcing the animation curves to be recalculated.
		/// </summary>
		public void ClearDataCache ()
		{
			currentFileID = 0;
		}

		// -----------------
		// Private Functions
		// -----------------
		
		private void LoadXML (TextAsset xmlFile, AudioClip linkedClip)
		{
#if UNITY_WP_8_1 || UNITY_WSA
			Debug.LogWarning("[LipSync - " + gameObject.name + "] XML loading is not supported on Windows Store platforms.");
#else
			XmlDocument document = new XmlDocument();
			document.LoadXml(xmlFile.text);

			// Clear/define marker lists, to overwrite any previous file
			phonemeMarkers = new List<PhonemeMarker>();

			audioClip = linkedClip;
			audioSource.clip = audioClip;

			string version = ReadXML(document, "LipSyncData", "version");

			if (float.Parse(version) < 1.321f)
			{
				Debug.LogError("Cannot load pre-1.321 XML file. Run the converter from Window/Rogo Digital/LipSync Lite/Update XML files.");
				return;
			}

			try
			{
				fileLength = float.Parse(ReadXML(document, "LipSyncData", "length"));

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
							int phoneme = int.Parse(node.Attributes["phonemeNumber"].Value);
							float time = float.Parse(node.Attributes["time"].Value) / fileLength;
							float intensity = float.Parse(node.Attributes["intensity"].Value);
							bool sustain = bool.Parse(node.Attributes["sustain"].Value);

							phonemeMarkers.Add(new PhonemeMarker(phoneme, time, intensity, sustain));
						}
					}
				}

			}
			catch
			{
				Debug.LogError("[LipSync - " + gameObject.name + "] Malformed XML file. See console for details. \nFor the sake of simplicity, LipSync Lite is unable to handle errors in XML files. The clip editor often can, however. Import this XML file into the clip editor and re-export to fix.");
			}

			phonemeMarkers.Sort(SortTime);
#endif
		}

		private bool LoadData (LipSyncData dataFile)
		{
			// Check that the referenced file contains data
			if (dataFile.phonemeData.Length > 0)
			{
				// Store reference to the associated AudioClip.
				audioClip = dataFile.clip;
				fileLength = dataFile.length;

				// Update file to current format if needed
				bool updated = false;

				if (dataFile.version < 1)
				{
					// Pre 1.0 - update emotion blends to new format.
					updated = true;

					if (dataFile.length == 0)
					{
						fileLength = audioClip.length;
					}
				}

				if (dataFile.version < 1.3f)
				{
					// Pre 1.3 - update enum-based phoneme IDs
					updated = true;
					for (int p = 0; p < dataFile.phonemeData.Length; p++)
					{
						dataFile.phonemeData[p].phonemeNumber = (int)dataFile.phonemeData[p].phoneme;
					}
				}

				if (updated)
					Debug.LogWarning("[LipSync - " + gameObject.name + "] Loading data from an old format LipSyncData file. For better performance, open this clip in the Clip Editor and re-save to update.");

				// Clear/define marker lists, to overwrite any previous file
				phonemeMarkers = new List<PhonemeMarker>();

				// Copy data from file into new lists
				foreach (PhonemeMarker marker in dataFile.phonemeData)
				{
					phonemeMarkers.Add(marker);
				}

				// Phonemes are stored out of sequence in the file, for depth sorting in the editor
				// Sort them by timestamp to make finding the current one faster
				phonemeMarkers.Sort(SortTime);

				// Set current AudioClip in the AudioSource
				audioSource.clip = audioClip;

				// Save file InstanceID for later, to skip loading data that is already loaded
				currentFileID = dataFile.GetInstanceID();
				lastClip = dataFile;

				return true;
			}
			else
			{
				return false;
			}
		}

		private IEnumerator StopAudioSource (float delay)
		{
			yield return new WaitForSeconds(delay);
			audioSource.Stop();
		}

		public LipSync ()
		{
			// Constructor used to set version value on new component
			this.lastUsedVersion = 1.501f;
		}

		// Sort PhonemeMarker by timestamp
		public static int SortTime (PhonemeMarker a, PhonemeMarker b)
		{
			float sa = a.time;
			float sb = b.time;

			return sa.CompareTo(sb);
		}

		public static string ReadXML (XmlDocument xml, string parentElement, string elementName)
		{
#if UNITY_WP_8_1 || UNITY_WSA
			return null;
#else
			XmlNode node = xml.SelectSingleNode("//" + parentElement + "//" + elementName);

			if (node == null)
			{
				return null;
			}

			return node.InnerText;
#endif
		}

		public enum AnimationTimingMode
		{
			AudioPlayback,
			CustomTimer,
			FixedFrameRate,
		}

		public enum CurveGenerationMode
		{
			Tight,
			Loose,
		}
	}

	// Old Phoneme List
	public enum Phoneme
	{
		AI,
		E,
		U,
		O,
		CDGKNRSThYZ,
		FV,
		L,
		MBP,
		WQ,
		Rest
	}
}