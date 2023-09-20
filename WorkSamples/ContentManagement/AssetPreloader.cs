namespace MySamples.Assets
{
    public class AssetPreloader : MonoBehaviour
    {
        [Tooltip("If true, all pending assets will be rushed (push the preloading process to its limit) to load as quickly as possible when a game simulation that requires the assets have been started")]
        public bool rushPreloaderBeforeSimulation;
        public MDQDatabase[] databases;
        [InfoBox("If > 0, every frame will ask Addressables to preload this amount of assets")]
        public int assetCallPerTick = 0;
        [Tooltip("If the game is currently in a loading screen. It is advised to have this higher than the base assetCallPerTick.")]
        public int assetCallPerTickIfLoadingScreen = 0;
        [InfoBox("Destroy gameObject vs Destroy component only")]
        public bool destroyGameObjectWhenDone = false;

        /// <summary>
        /// Static is important in case we want to run multiple pre-loaders
        /// </summary>
        static int preloadingCalls = 0;
        internal static HashSet<string> loadedAssets = new HashSet<string>();
        public static bool APreloaderIsLoading => preloadingCalls > 0;
        public bool IsLoading { get; private set; }
        private void Start()
        {
            AsyncPreload();
        }
        /// <summary>
        /// How many assets are we trying to load per frame
        /// </summary>
        protected int perTick
        {
            get
            {
                if (assetCallPerTick <= 0) return 0;
                if (rushPreloaderBeforeSimulation) return 50;
                if (assetCallPerTickIfLoadingScreen > 0 && MD.UI.LoadingUI.IsScreenVisible()) return assetCallPerTickIfLoadingScreen;
                return assetCallPerTick;
            }
        }
        async void AsyncPreload()
        {
            preloadingCalls++;
            IsLoading = true;
            List<Task> tasks = new List<Task>();
            if (perTick <= 0)
            {
                for (int d = 0; d < databases.Length; d++)
                {
                    var database = databases[d];
                    if (database != null && database.db != null)
                    {
                        for (int i = 0; i < database.db.Length; i++)
                        {
                            if (string.IsNullOrEmpty(database.db[i])) continue;
                            if (loadedAssets.Contains(database.db[i])) continue;
                            loadedAssets.Add(database.db[i]);
                            tasks.Add(Addressables.LoadAssetAsync<AssetBase>(database.db[i]).Task);
                        }
                        await Task.WhenAll(tasks);
                    }
                }
            }
            else
            {
#if UNITY_EDITOR && DEBUG_PRELOADER
                int totalCount = 0;
#endif
                for (int d = 0; d < databases.Length; d++)
                {
                    var database = databases[d];
                    if (database != null && database.db != null)
                    {
                        int i = 0;
                        int runCount = 0;
                        while (i < database.db.Length)
                        {
                            if (string.IsNullOrEmpty(database.db[i]) == false && loadedAssets.Contains(database.db[i]) == false)
                            {
                                tasks.Add(Addressables.LoadAssetAsync<AssetBase>(database.db[i]).Task);
                            }
                            i++;
                            runCount++;

                            if (runCount >= perTick)
                            {
                                runCount = 0;
                                await Task.WhenAll(tasks);
#if UNITY_EDITOR && DEBUG_PRELOADER
                                totalCount += perTick;
                                Debug.Log($"{totalCount} have been loaded so far");
#endif
                                tasks.Clear();
                            }
                        }
                        // last run
                        if (runCount > 0)
                        {
                            await Task.WhenAll(tasks);
#if UNITY_EDITOR && DEBUG_PRELOADER
                            totalCount += runCount;
                            Debug.Log($"{totalCount} Finished loading.");
#endif
                        }
                    }
                }
            }
            preloadingCalls--;
            IsLoading = false;
            if (destroyGameObjectWhenDone)
            {
                Destroy(gameObject);
            }
            else
            {
                Destroy(this);
            }
        }
    }
}