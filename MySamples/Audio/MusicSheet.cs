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
        public MusicClip[] clips;
        public float fadeOutTime = 3;
        public float fadeInTime = 2;
        public float startDelay = 1;
        public bool debug;

        protected float internalVolume;
        protected int loopCount;
        protected int clipIndex;
        protected bool isPlaying;
        protected float lerpPoint;
        protected bool ended;
        protected MusicClip current
        {
            get
            {
                return clips[clipIndex];
            }
        }
        protected float volume
        {
            get
            {
                return internalVolume * LocalSummoner.settings.audio.musicVolume * LocalSummoner.settings.audio.masterVolume * MusicManager.controlledVolume;
            }
        }
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
        /// <param name="timeInSheet"></param>
        /// <param name="source"></param>
        /// <returns>true if sheet play process has completed</returns>
        public virtual bool PlaySheet(float timeInSheet, AudioSource source)
        {
            if (ended) return false;

            if (isPlaying == false)
            {
                OnFirstPlay();
                isPlaying = true;
            }
            if (source.clip == null) source.clip = current.clip;
            if (internalVolume >= 1)
            {
                // keep updating in-case settings changed
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
            if (source.isPlaying == false)
            {
                source.Play();
                OnNewClipStarted();
            }

            return internalVolume >= 1;
        }
        /// <summary>
        /// Handles what happens when we reach the end of a clip
        /// </summary>
        /// <param name="source"></param>
        protected virtual void HandleClipChanging(AudioSource source)
        {
            if (source.isPlaying == false)
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
        protected virtual void OnNewClipStarted()
        {
            loopCount = 0;
        }
        protected virtual void OnWaitingForDelay(float timeInSheet, AudioSource source)
        {
            if (source.isPlaying) source.Stop();
            internalVolume = 0;
        }
        /// <summary>
        /// Begin the stop play process
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