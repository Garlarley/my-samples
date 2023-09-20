namespace MySamples.Audio
{
    public class MusicManager : Singleton<MusicManager>
    {
        [System.Serializable]
        public class QueuedSheet
        {
            public AudioSource source;
            public MusicSheet sheet;
            public float startTimer;
            public float stopTimer;
            public bool markedForDequeue;
        }
        public struct ControlledVolume
        {
            public float volume;
            public GameObject source;
        }

        private static List<AudioSource> sources;
        protected List<ControlledVolume> controlledVolumes = new List<ControlledVolume>();
        public static float controlledVolume
        {
            get
            {
                if (instance != null && instance.controlledVolumes.Count > 0) return instance.controlledVolumes[instance.controlledVolumes.Count - 1].volume;
                return 1;
            }
        }
        public static void SetControlledVolume(GameObject goSource, float vol)
        {
            if (instance == null) return;
            instance.controlledVolumes.Add(new ControlledVolume()
            { source = goSource, volume = vol });
        }
        public static void RemoveControlledVolume(GameObject goSource)
        {
            if (instance == null) return;
            for (int i = instance.controlledVolumes.Count - 1; i >= 0; i--)
            {
                if (instance.controlledVolumes[i].source == goSource)
                {
                    instance.controlledVolumes.RemoveAt(i);
                }
            }
        }
        //private static List<QueuedSheet> fullQueue = new List<QueuedSheet>();
        [Sirenix.OdinInspector.ReadOnly]
        public List<QueuedSheet> queue = new List<QueuedSheet>();
        public static void QueueMusicSheet(MusicSheet sheet)
        {
            if (Instance) Instance.InternalQueueMusicSheet(sheet);
        }
        public static void DequeueMusicSheet(MusicSheet sheet)
        {
            if (Instance) Instance.InternalDequeueMusicSheet(sheet);
        }

        protected void InternalQueueMusicSheet(MusicSheet _sheet)
        {
            if (queue == null) queue = new List<QueuedSheet>();
            if (_sheet == null) return;

            if (_sheet.clips.Length == 0) return;

            bool hasSheet = false;
            _sheet.Initialize();
            for (int i = 0; i < queue.Count; i++)
            {
                queue[i].stopTimer = 0;
                //we already have this sheet
                if (queue[i].sheet == _sheet)
                {
                    hasSheet = true;
                    queue[i].markedForDequeue = false;
                    queue[i].startTimer = 0;
                    // put it on top
                    if (queue.Count > 1 && i != queue.Count - 1)
                    {
                        var tmp = queue[i];
                        queue[i] = queue[queue.Count - 1];
                        queue[queue.Count - 1] = tmp;
                    }
                }
            }

            if (hasSheet == false)
            {
                var qs = new QueuedSheet
                {
                    source = GetAvailableSource(),
                    sheet = _sheet
                };
                queue.Add(qs);
            }
        }
        protected void InternalDequeueMusicSheet(MusicSheet _sheet)
        {
            if (queue == null) queue = new List<QueuedSheet>();
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

        private void Update()
        {
            if (queue == null) queue = new List<QueuedSheet>();
            if (queue.Count == 0) return;
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
            GameObject newGO = new GameObject("Audio Source [" + sources.Count + "]");
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