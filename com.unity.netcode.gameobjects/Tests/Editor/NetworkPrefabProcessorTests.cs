using NUnit.Framework;
using Unity.Netcode.Editor.Configuration;
using UnityEditor;
using UnityEngine;

namespace Unity.Netcode.EditorTests
{
    internal class NetworkPrefabProcessorTests
    {
        private NetcodeForGameObjectsProjectSettings m_Settings;
        private bool m_EditorDefaultPrefabSetting;
        private string m_EditorDefaultPrefabLocation;

        private GameObject m_Prefab;

        private const string k_PrefabName = "Assets/TestPrefab.prefab";
        private const string k_DefaultAssetString = "Assets/TestPrefabList.asset";

        [SetUp]
        public void SetUp()
        {
            m_Settings = NetcodeForGameObjectsProjectSettings.instance;
            m_EditorDefaultPrefabSetting = m_Settings.GenerateDefaultNetworkPrefabs;
            m_EditorDefaultPrefabLocation = NetworkPrefabProcessor.DefaultNetworkPrefabsPath;
            NetworkPrefabProcessor.DefaultNetworkPrefabsPath = k_DefaultAssetString;
        }

        [TearDown]
        public void TearDown()
        {
            m_Settings.GenerateDefaultNetworkPrefabs = m_EditorDefaultPrefabSetting;
            NetworkPrefabProcessor.DefaultNetworkPrefabsPath = m_EditorDefaultPrefabLocation;
            AssetDatabase.DeleteAsset(k_PrefabName);
            AssetDatabase.DeleteAsset(k_DefaultAssetString);
        }

        [Test]
        public void WhenGenerateDefaultNetworkPrefabsIsEnabled_AddingAPrefabUpdatesDefaultPrefabList()
        {
            var obj = new GameObject("Object");
            obj.AddComponent<NetworkObject>();
            m_Settings.GenerateDefaultNetworkPrefabs = true;
            m_Prefab = PrefabUtility.SaveAsPrefabAsset(obj, k_PrefabName);
            Object.DestroyImmediate(obj);

            var prefabList = NetworkPrefabProcessor.GetOrCreateNetworkPrefabs(NetworkPrefabProcessor.DefaultNetworkPrefabsPath, out var isNew, false);
            Assert.IsFalse(isNew);
            Assert.IsTrue(prefabList.Contains(m_Prefab));
        }

        [Test]
        public void WhenGenerateDefaultNetworkPrefabsIsEnabled_RemovingAPrefabUpdatesDefaultPrefabList()
        {
            WhenGenerateDefaultNetworkPrefabsIsEnabled_AddingAPrefabUpdatesDefaultPrefabList();

            AssetDatabase.DeleteAsset(k_PrefabName);
            var prefabList = NetworkPrefabProcessor.GetOrCreateNetworkPrefabs(NetworkPrefabProcessor.DefaultNetworkPrefabsPath, out var isNew, false);
            Assert.IsFalse(isNew);
            Assert.IsFalse(prefabList.Contains(m_Prefab));
        }

        [Test]
        public void WhenGenerateDefaultNetworkPrefabsIsNotEnabled_AddingAPrefabDoesNotUpdateDefaultPrefabList()
        {
            var obj = new GameObject("Object");
            obj.AddComponent<NetworkObject>();
            m_Settings.GenerateDefaultNetworkPrefabs = false;
            m_Prefab = PrefabUtility.SaveAsPrefabAsset(obj, k_PrefabName);
            Object.DestroyImmediate(obj);

            var prefabList = NetworkPrefabProcessor.GetOrCreateNetworkPrefabs(NetworkPrefabProcessor.DefaultNetworkPrefabsPath, out var isNew, false);
            Assert.IsTrue(isNew);
            Assert.IsFalse(prefabList.Contains(m_Prefab));
        }

        [Test]
        public void WhenGenerateDefaultNetworkPrefabsIsNotEnabled_RemovingAPrefabDoesNotUpdateDefaultPrefabList()
        {
            // Add it with the list enabled, then disable the list. Removing it
            // should then be nop.
            WhenGenerateDefaultNetworkPrefabsIsEnabled_AddingAPrefabUpdatesDefaultPrefabList();

            m_Settings.GenerateDefaultNetworkPrefabs = false;
            AssetDatabase.DeleteAsset(k_PrefabName);
            var prefabList = NetworkPrefabProcessor.GetOrCreateNetworkPrefabs(NetworkPrefabProcessor.DefaultNetworkPrefabsPath, out var isNew, false);
            Assert.IsFalse(isNew);
            Assert.IsTrue(prefabList.Contains(m_Prefab));
        }
    }
}
