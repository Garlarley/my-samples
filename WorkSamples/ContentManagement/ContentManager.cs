namespace MySamples.Assets
{
    public static class ExtendedAddressableFunctions
    {
        public static void SafeAddressableLoad<T>(this AssetReference assetRef, System.Action<T> onLoaded) where T : UnityEngine.Object
        {
            if (assetRef.RuntimeKeyIsValid() == false || onLoaded == null) return;

            if (assetRef.IsValid())
            {
                if (assetRef.IsDone)
                {
                    if (assetRef.OperationHandle.Result != null)
                    {
                        if (assetRef.OperationHandle.Result.GetType() == typeof(T))
                        {
                            onLoaded.Invoke((T)assetRef.OperationHandle.Result);
                        }
                    }
                }
                else
                {
                    assetRef.OperationHandle.Completed += handle =>
                    {
                        if (handle.IsValid() && handle.Result != null)
                        {
                            if (handle.Result.GetType() == typeof(T))
                            {
                                onLoaded.Invoke((T)handle.Result);
                            }
                        }
                    };
                }
            }
            else
            {
                assetRef.LoadAssetAsync<T>().Completed += handle =>
                {
                    if (handle.IsValid() && handle.Result != null)
                    {
                        onLoaded.Invoke(handle.Result);
                    }
                };
            }
        }
    }

    namespace MD.ContentManagement
    {
        public static class ContentManager
        {
            public static UnityEngine.Events.UnityAction onNextContentUpdateComplete;
            public static UnityEngine.Events.UnityAction onAnyContentUpdateComplete;

            public static void CheckForContentUpdate()
            {
#if UNITY_EDITOR
            Debug.Log("Content: Checking for content update...");
#endif
                Addressables.CheckForCatalogUpdates().Completed += CheckForCatalogUpdates_Completed;
            }

            /// <summary>
            /// Result of checking if content catalogues need to be updated
            /// </summary>
            /// <param name="obj"></param>
            private static void CheckForCatalogUpdates_Completed(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<List<string>> obj)
            {
#if UNITY_EDITOR
            Debug.Log("Content: Check complete.");
#endif
                // -- need to perform an update
                if (obj.Result != null && obj.Result.Count > 0)
                {
#if UNITY_EDITOR
                Debug.Log("Content: Updating catalog.");
#endif
                    Addressables.UpdateCatalogs(obj.Result).Completed += UpdateCatalogs_Completed;
                }
                else
                {
#if UNITY_EDITOR
                Debug.Log("Content: No need for catalog update.");
#endif
                    if (onNextContentUpdateComplete != null)
                    {
                        onNextContentUpdateComplete.Invoke();
                        onNextContentUpdateComplete = null;
                    }
                    if (onAnyContentUpdateComplete != null) onAnyContentUpdateComplete.Invoke();
                }
            }

            /// <summary>
            /// Result of attempting to update content 
            /// </summary>
            /// <param name="obj"></param>
            private static void UpdateCatalogs_Completed(UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle<List<UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator>> obj)
            {
#if UNITY_EDITOR
            Debug.Log("Content: Catalog update complete.");
#endif
                if (onNextContentUpdateComplete != null)
                {
                    onNextContentUpdateComplete.Invoke();
                    onNextContentUpdateComplete = null;
                }
                if (onAnyContentUpdateComplete != null) onAnyContentUpdateComplete.Invoke();
            }
            // Auto releases all addressable handles, useful when switching between scenes or game sessions
            public static void UnloadAllAddressableHandles(bool unloadMDQMaps = true)
            {
                ReleaseAsyncOperationHandles(GetAllAsyncOperationHandles(unloadMDQMaps));
            }
            public static List<AsyncOperationHandle> GetAllAsyncOperationHandles(bool unloadMDQMaps = true)
            {
                // Workaround for problems:
                // https://forum.unity.com/threads/how-to-unload-everything-currently-loaded-by-addressables.1121998/

                var handles = new List<AsyncOperationHandle>();

                var resourceManagerType = Addressables.ResourceManager.GetType();
                var dictionaryMember = resourceManagerType.GetField("m_AssetOperationCache", BindingFlags.NonPublic | BindingFlags.Instance);
                var dictionary = dictionaryMember.GetValue(Addressables.ResourceManager) as IDictionary;

                foreach (var asyncOperationInterface in dictionary.Values)
                {
                    if (asyncOperationInterface == null)
                        continue;

                    var handle = typeof(AsyncOperationHandle).InvokeMember(nameof(AsyncOperationHandle),
                        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.CreateInstance,
                        null, null, new object[] { asyncOperationInterface });
                    var x = (AsyncOperationHandle)handle;
                    bool add = false;

                    if (x.Result != null)
                    {
                        var t = x.Result.GetType();
                        #region CUSTOM TYPES
                        // ---------- CUSTOM EXCEPTIONS, DO NOT RE-USE
                        if (t == typeof(UnityEngine.GameObject) || t == typeof(MD.Localization.Library.LibraryData) || t == typeof(UnityEngine.Texture2D) || t == typeof(UnityEngine.AudioClip)
                            || t == typeof(MD.Icons.IconLibrary) || (unloadMDQMaps && t == typeof(MD.Content.MDQMap)) || t == typeof(MD.Content.VisualDataContainer))
                        {
                            add = true;
                        }
                        // -------------------------------------------
                        #endregion
                    }
                    if (add)
                    {
                        handles.Add(x);
                    }
                }

                return handles;
            }

            public static void ReleaseAsyncOperationHandles(List<AsyncOperationHandle> handles)
            {
                foreach (var handle in handles)
                {
                    if (!handle.IsDone)
                    {
#if UNITY_EDITOR
                    Debug.LogWarning($"AsyncOperationHandle not completed yet. Releasing anyway!");
#endif
                    }
                    while (handle.IsValid())
                    {
                        Addressables.ResourceManager.Release(handle);
                    }
                }
            }
        }
    }
}