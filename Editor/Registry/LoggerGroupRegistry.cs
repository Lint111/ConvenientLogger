#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;

namespace ConvenientLogger.Editor
{
    /// <summary>
    /// Editor-only registry for discovering and caching LoggerGroupAssets.
    /// </summary>
    public static class LoggerGroupRegistry
    {
        private static readonly Dictionary<string, LoggerGroupAsset> _assetsByGuid = new();
        private static readonly Dictionary<LoggerGroupAsset, string> _guidsByAsset = new();
        
        // Cached list of all groups - invalidated on project change
        private static List<LoggerGroupAsset> _cachedAllGroups;
        private static bool _allGroupsCacheDirty = true;

        /// <summary>
        /// Gets a LoggerGroupAsset by its GUID.
        /// </summary>
        public static LoggerGroupAsset GetByGuid(string guid)
        {
            if (string.IsNullOrEmpty(guid)) return null;
            
            if (_assetsByGuid.TryGetValue(guid, out var cached) && cached != null)
                return cached;

            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;
            
            var asset = AssetDatabase.LoadAssetAtPath<LoggerGroupAsset>(path);
            if (asset != null)
            {
                _assetsByGuid[guid] = asset;
                _guidsByAsset[asset] = guid;
            }
            return asset;
        }

        /// <summary>
        /// Gets the GUID for a LoggerGroupAsset.
        /// </summary>
        public static string GetGuid(LoggerGroupAsset asset)
        {
            if (asset == null) return null;
            
            if (_guidsByAsset.TryGetValue(asset, out var cached))
                return cached;
            
            var path = AssetDatabase.GetAssetPath(asset);
            if (string.IsNullOrEmpty(path)) return null;
            
            var guid = AssetDatabase.AssetPathToGUID(path);
            _guidsByAsset[asset] = guid;
            _assetsByGuid[guid] = asset;
            return guid;
        }

        /// <summary>
        /// Finds all LoggerGroupAssets in the project.
        /// Cached for performance - invalidated on project change.
        /// </summary>
        public static IEnumerable<LoggerGroupAsset> GetAll()
        {
            if (_allGroupsCacheDirty || _cachedAllGroups == null)
            {
                _cachedAllGroups ??= new List<LoggerGroupAsset>();
                _cachedAllGroups.Clear();
                
                var guids = AssetDatabase.FindAssets("t:LoggerGroupAsset");
                foreach (var guid in guids)
                {
                    var asset = GetByGuid(guid);
                    if (asset != null)
                        _cachedAllGroups.Add(asset);
                }
                
                _allGroupsCacheDirty = false;
            }
            
            return _cachedAllGroups;
        }

        /// <summary>
        /// Clears the cache. Call this if assets are renamed or deleted.
        /// </summary>
        public static void ClearCache()
        {
            _assetsByGuid.Clear();
            _guidsByAsset.Clear();
            _allGroupsCacheDirty = true;
        }

        /// <summary>
        /// Forces a refresh of the asset cache.
        /// </summary>
        public static void Refresh()
        {
            ClearCache();
            foreach (var _ in GetAll()) { } // Enumerate to populate cache
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            // Listen for asset changes
            EditorApplication.projectChanged += OnProjectChanged;
        }

        private static void OnProjectChanged()
        {
            // Clear cache when project changes (assets added/removed/renamed)
            ClearCache();
        }
    }
}
#endif
