using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Unity.Netcode.Editor.Configuration
{
    /// <summary>
    /// Updates the default <see cref="NetworkPrefabsList"/> instance when prefabs are updated (created, moved, deleted) in the project.
    /// </summary>
    public class NetworkPrefabProcessor : AssetPostprocessor
    {
        public static string DefaultNetworkPrefabsPath
        {
            get
            {
                return NetcodeForGameObjectsProjectSettings.instance.NetworkPrefabsPath;
            }
            internal set
            {
                NetcodeForGameObjectsProjectSettings.instance.NetworkPrefabsPath = value;
                // Force a recache of the prefab list
                s_PrefabsList = null;
            }
        }
        private static NetworkPrefabsList s_PrefabsList;
        private static Dictionary<string, NetworkPrefab> s_PrefabsListPath = new Dictionary<string, NetworkPrefab>();

        private static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            var settings = NetcodeForGameObjectsProjectSettings.instance;
            if (!settings.GenerateDefaultNetworkPrefabs)
            {
                return;
            }

            bool ProcessImportedAssets(string[] importedAssets1)
            {
                var dirty = false;
                foreach (var assetPath in importedAssets1)
                {
                    // We only care about GameObjects, skip everything else. Can't use the more targeted
                    // OnPostProcessPrefabs since that's not called for moves or deletes
                    if (AssetDatabase.GetMainAssetTypeAtPath(assetPath) != typeof(GameObject))
                    {
                        continue;
                    }

                    var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
                    if (go.TryGetComponent<NetworkObject>(out _))
                    {
                        // Make sure we are not duplicating an already existing entry
                        if (s_PrefabsListPath.ContainsKey(assetPath))
                        {
                            // Is the imported asset different from the one we already have in the list?
                            if (s_PrefabsListPath[assetPath].Prefab.GetHashCode() != go.GetHashCode())
                            {
                                // If so remove the one in the list and continue on to add the imported one
                                s_PrefabsList.List.Remove(s_PrefabsListPath[assetPath]);
                            }
                            else // If they are identical, then just ignore the import
                            {
                                continue;
                            }
                        }

                        s_PrefabsList.List.Add(new NetworkPrefab { Prefab = go });
                        dirty = true;
                    }
                }

                return dirty;
            }

            bool ProcessDeletedAssets(string[] strings)
            {
                var dirty = false;
                var deleted = new List<string>(strings);
                for (int i = s_PrefabsList.List.Count - 1; i >= 0 && deleted.Count > 0; --i)
                {
                    GameObject prefab;
                    try
                    {
                        prefab = s_PrefabsList.List[i].Prefab;
                    }
                    catch (MissingReferenceException)
                    {
                        s_PrefabsList.List.RemoveAt(i);
                        continue;
                    }
                    if (prefab == null)
                    {
                        s_PrefabsList.List.RemoveAt(i);
                    }
                    else
                    {
                        string noPath = AssetDatabase.GetAssetPath(prefab);
                        for (int j = strings.Length - 1; j >= 0; --j)
                        {
                            if (noPath == strings[j])
                            {
                                s_PrefabsList.List.RemoveAt(i);
                                deleted.RemoveAt(j);
                                dirty = true;
                            }
                        }
                    }
                }

                return dirty;
            }

            if (s_PrefabsList == null)
            {
                s_PrefabsList = GetOrCreateNetworkPrefabs(DefaultNetworkPrefabsPath, out var newList, true);
                // A new list already processed all existing assets, no need to double-process imports & deletes
                if (newList)
                {
                    return;
                }
            }
            // Clear our asset path to prefab table each time
            s_PrefabsListPath.Clear();

            // Create our asst path to prefab table
            foreach (var prefabEntry in s_PrefabsList.List)
            {
                if (!s_PrefabsListPath.ContainsKey(AssetDatabase.GetAssetPath(prefabEntry.Prefab)))
                {
                    s_PrefabsListPath.Add(AssetDatabase.GetAssetPath(prefabEntry.Prefab), prefabEntry);
                }
            }

            // Process the imported and deleted assets
            var markDirty = ProcessImportedAssets(importedAssets);
            markDirty |= ProcessDeletedAssets(deletedAssets);

            if (markDirty)
            {
                EditorUtility.SetDirty(s_PrefabsList);
            }
        }

        internal static NetworkPrefabsList GetOrCreateNetworkPrefabs(string path, out bool isNew, bool addAll)
        {
            var defaultPrefabs = AssetDatabase.LoadAssetAtPath<NetworkPrefabsList>(path);
            if (defaultPrefabs == null)
            {
                isNew = true;
                defaultPrefabs = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                defaultPrefabs.IsDefault = true;
                AssetDatabase.CreateAsset(defaultPrefabs, path);

                if (addAll)
                {
                    // This could be very expensive in large projects... maybe make it manually triggered via a menu?
                    defaultPrefabs.List = FindAll();
                }
                EditorUtility.SetDirty(defaultPrefabs);
                AssetDatabase.SaveAssetIfDirty(defaultPrefabs);
                return defaultPrefabs;
            }

            isNew = false;
            return defaultPrefabs;
        }

        private static List<NetworkPrefab> FindAll()
        {
            var list = new List<NetworkPrefab>();

            string[] guids = AssetDatabase.FindAssets("t:GameObject");
            foreach (var guid in guids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(guid);
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

                if (go.TryGetComponent(out NetworkObject _))
                {
                    list.Add(new NetworkPrefab { Prefab = go });
                }
            }

            return list;
        }
    }
}
