namespace MySamples.Audio
{
    public class MusicManager : Singleton<MusicManager>
    {
        protected class QueuedSheet
        {
            public AudioSource source;
            public MusicSheet sheet;
            public float startTimer;
            public float stopTimer;
            public bool markedForDequeue;
        }
        protected struct ControlledVolume
        {
            public float volume;
            public GameObject source;
        }
        /// <summary>
        /// Global audio sources that have been registered in the system
        /// </summary>
        private static List<AudioSource> sources;
        /// <summary>
        /// Current volume manual overrides. If empty, default volume is used
        /// </summary>
        protected List<ControlledVolume> controlledVolumes = new List<ControlledVolume>();
        /// <summary>
        /// Sheets played
        /// </summary>
        protected Sta<QueuedSheet> queue = new List<QueuedSheet>();
        /// <summary>
        /// Current controlled volume that accounts for overrides
        /// </summary>
        public static float ControlledVolume
        {
            get
            {
                if (Instance != null && Instance.controlledVolumes.Count > 0)
                {
                    return Instance.controlledVolumes[Instance.controlledVolumes.Count - 1].volume;
                }
                return 1;
            }
        }
        /// <summary>
        /// Add a volume override
        /// </summary>
        /// <param name="goSource">If null, it is a global override and will be auto-removed when global overrides are</param>
        /// <param name="vol">The target volume level</param>
        public static void SetControlledVolume(GameObject goSource, float vol)
        {
            if (instance == null) return;
            instance.controlledVolumes.Add(new ControlledVolume()
            { source = goSource, volume = vol });
        }
        /// <summary>
        /// Remove override that was triggered by a source. Can be null to remove global
        /// </summary>
        /// <param name="goSource"></param>
        public static void RemoveControlledVolume(GameObject goSource)
        {
            if (Instance == null) return;
            Instance.controlledVolumes.RemoveAll(item => item.source == goSource);
        }
        /// <summary>
        /// Requests to start playing a music sheet
        /// </summary>
        /// <param name="sheet"></param>
        public static void QueueMusicSheet(MusicSheet sheet)
        {
            if (Instance) Instance.InternalQueueMusicSheet(sheet);
        }
        /// <summary>
        /// begins the process of stopping the music sheet from playing based on the sheet's settings
        /// </summary>
        /// <param name="sheet"></param>
        public static void DequeueMusicSheet(MusicSheet sheet)
        {
            if (Instance) Instance.InternalDequeueMusicSheet(sheet);
        }
        /// <summary>
        /// Actual queue implemntation. It checks if we already have a similar sheet, if so re-set and use it instead
        /// </summary>
        /// <param name="_sheet"></param>
        protected void InternalQueueMusicSheet(MusicSheet _sheet)
        {
            if (queue == null) queue = new List<QueuedSheet>();
            if (_sheet == null) return;

            if (_sheet.clips.Length == 0) return;

            _sheet.Initialize();
            for (int i = 0; i < queue.Count; i++)
            {
                queue[i].stopTimer = 0;
                //we already have this sheet
                if (queue[i].sheet == _sheet)
                {
                    queue[i].markedForDequeue = false;
                    queue[i].startTimer = 0;
                    // put it on top
                    if (queue.Count > 1 && i != queue.Count - 1)
                    {
                        var tmp = queue[i];
                        queue[i] = queue[queue.Count - 1];
                        queue[queue.Count - 1] = tmp;
                    }

                    return;
                }
            }

            queue.Add(new QueuedSheet
            {
                source = GetAvailableSource(),
                sheet = _sheet
            });
        }
        /// <summary>
        /// Mark a sheet for removal
        /// </summary>
        /// <param name="_sheet"></param>
        protected void InternalDequeueMusicSheet(MusicSheet _sheet)
        {
            if (queue == null) return;
            for (int i = 0; i < queue.Count; i++)
            {
                queue[i].startTimer = 0;
                if (queue[i].sheet == _sheet)
                {
                    queue[i].markedForDequeue = true;
                    queue[i].stopTimer = 0;
                }
            }
        }
        /// <summary>
        /// Handle playing the music
        /// </summary>
        private void Update()
        {
            if (queue == null || queue.Count == 0) return;
            bool topSheetPlayed = false;
            for (int i = queue.Count - 1; i >= 0; i--)
            {
                if (queue[i] == null || queue[i].sheet == null)
                {
                    queue.RemoveAt(i);
                    continue;
                }

                if (topSheetPlayed || queue[i].markedForDequeue)
                {
                    // remove from queue
                    if (queue[i].sheet.StopSheet(queue[i].stopTimer, queue[i].source) && queue[i].markedForDequeue)
                    {
                        queue[i].source.clip = null;
                        queue.RemoveAt(i);
                        continue;
                    }
                    queue[i].stopTimer += Time.deltaTime;
                }
                else
                {
                    queue[i].sheet.PlaySheet(queue[i].startTimer, queue[i].source);
                    queue[i].startTimer += Time.deltaTime;
                    topSheetPlayed = true;
                }
            }
        }
        /// <summary>
        /// returns the first available audio source
        /// </summary>
        /// <returns></returns>
        private AudioSource GetAvailableSource()
        {
            if (queue == null) queue = new List<QueuedSheet>();

            if (sources == null) sources = new List<AudioSource>();
            for (int i = sources.Count - 1; i >= 0; i--)
            {
                if (sources[i] == null) sources.RemoveAt(i);
            }
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i].clip == null)
                {
                    bool used = false;
                    for (int j = 0; j < queue.Count; j++)
                    {
                        if (queue[j] == null) continue;
                        if (queue[j].source == sources[i])
                        {
                            used = true;
                        }
                    }
                    if (used == false) return sources[i];
                }
            }
            // no available source, create one
            GameObject newGO = new GameObject($"Audio Source [{sources.Count}]");
            newGO.transform.SetParent(transform);
            newGO.transform.localPosition = Vector3.zero;
            AudioSource newSource = newGO.AddComponent<AudioSource>();
            newSource.spatialBlend = 0;
            newSource.loop = false;
            newSource.playOnAwake = false;
            newSource.volume = 0;
            sources.Add(newSource);
            return newSource;
        }
    }
}