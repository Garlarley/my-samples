using UnityEngine;

namespace MySamples.Audio
{
    [System.Serializable]
    public struct MusicClip
    {
        public AudioClip clip;
        [InfoBox("0: no loop, -1: infinite looping, n: n count")]
        public int loopCount;
    }

    [CreateAssetMenu(fileName = "MusicSheet", menuName = "MD/Music/MusicSheet")]
    public class MusicSheet : ScriptableObject
    {
        [Tooltip("A music sheet can sequence between multiple clips")]
        public MusicClip[] clips;
        [Tooltip("How many seconds it takes to fade out this sheet. <= 0 is immediate")]
        public float fadeOutTime = 3;
        [Tooltip("How many seconds it begins playing this sheet. <= 0 is immediate")]
        public float fadeInTime = 2;
        [Tooltip("How long before any behaviors take place once this sheet is queued up")]
        public float startDelay = 1;

        protected float internalVolume;
        protected int loopCount;
        protected int clipIndex;
        protected bool isPlaying;
        protected float lerpPoint;
        protected bool ended;
        protected float volume => internalVolume * LocalSummoner.settings.audio.musicVolume * LocalSummoner.settings.audio.masterVolume * MusicManager.ControlledVolume;
        private MusicClip current => (clips != null && clips.Length >= clipIndex ? clips[clipIndex] : default);

        public void Initialize()
        {
            internalVolume = 0;
            loopCount = 0;
            clipIndex = 0;
            isPlaying = false;
            lerpPoint = 0;
            ended = false;
        }

        protected virtual void OnFirstPlay()
        {
            loopCount = 0;
            lerpPoint = internalVolume;
        }

        protected virtual void OnFirstStop()
        {
            lerpPoint = internalVolume;
        }

        /// <summary>
        /// Handles the process of playing a sheet
        /// </summary>
        /// <returns>true if sheet play process has completed</returns>
        public virtual bool PlaySheet(float timeInSheet, AudioSource source)
        {
            if (ended) return false;

            if (!isPlaying)
            {
                OnFirstPlay();
                isPlaying = true;
            }

            if (source.clip == null) source.clip = current.clip;

            if (internalVolume >= 1)
            {
                source.volume = volume;
                HandleClipChanging(source);
                return true;
            }

            if (timeInSheet < startDelay && internalVolume <= 0)
            {
                OnWaitingForDelay(timeInSheet, source);
                return false;
            }
            else if (fadeInTime <= 0) internalVolume = 1;
            else internalVolume = Mathf.Lerp(lerpPoint, 1, (timeInSheet - startDelay) / fadeInTime);
            source.volume = volume;

            if (!source.isPlaying)
            {
                source.Play();
                OnNewClipStarted();
            }

            return internalVolume >= 1;
        }
        /// <summary>
        /// Handles what happens when we reach the end of a clip
        /// </summary>
        protected virtual void HandleClipChanging(AudioSource source)
        {
            if (!source.isPlaying)
            {
                if (loopCount < current.loopCount || current.loopCount < 0)
                {
                    source.Play();
                    loopCount++;
                }
                else
                {
                    if (clips.Length > 1)
                    {
                        clipIndex++;
                        if (clipIndex >= clips.Length)
                        {
                            source.Stop();
                            clipIndex = 0;
                            ended = true;
                            return;
                        }

                        source.clip = current.clip;
                        source.Play();
                        OnNewClipStarted();
                    }
                }
            }
        }
        /// <summary>
        /// Triggered once when a new clip is played
        /// </summary>
        protected virtual void OnNewClipStarted()
        {
            loopCount = 0;
        }
        /// <summary>
        /// sheet is queued, but not active
        /// </summary>
        /// <param name="timeInSheet"></param>
        /// <param name="source"></param>
        protected virtual void OnWaitingForDelay(float timeInSheet, AudioSource source)
        {
            if (source.isPlaying) source.Stop();
            internalVolume = 0;
        }
        /// <summary>
        /// Is called every frame a sheet is in "STOP" mode
        /// </summary>
        public virtual bool StopSheet(float timeInSheet, AudioSource source)
        {
            if (isPlaying)
            {
                OnFirstStop();
                isPlaying = false;
            }

            if (fadeOutTime <= 0 || internalVolume <= 0)
            {
                if (source.isPlaying) source.Pause();
                return true;
            }

            internalVolume = Mathf.Lerp(lerpPoint, 0, timeInSheet / fadeOutTime);
            source.volume = volume;
            clipIndex = 0;
            return internalVolume <= 0;
        }

    }
}
