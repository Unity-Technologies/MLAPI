using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// Tests properties of NetworkObject for proper functionality.
    /// </summary>
    internal class NetworkObjectPropertyTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;

        private NetworkObject m_TestPrefabNetworkObject;

        protected override void OnServerAndClientsCreated()
        {
            // create prefab and get the NetworkObject component attached to it
            m_TestPrefabNetworkObject = CreateNetworkObjectPrefab("TestObject").GetComponent<NetworkObject>();
        }

        /// <summary>
        /// Tests PrefabHashId returns correctly when the NetworkObject is not a prefab.
        /// </summary>
        [Test]
        public void TestPrefabHashIdPropertyNotAPrefab()
        {
            const uint kInvalidPrefabHashId = 0;

            var gameObject = new GameObject("TestObject");
            var networkObject = gameObject.AddComponent<NetworkObject>();
            Assert.AreEqual(kInvalidPrefabHashId, networkObject.PrefabIdHash);
        }

        /// <summary>
        /// Tests PrefabHashId returns correctly when the NetworkObject is a prefab.
        /// </summary>
        /// <returns></returns>
        [Test]
        public void TestPrefabHashIdPropertyIsAPrefab()
        {
            Assert.AreEqual(m_TestPrefabNetworkObject.GlobalObjectIdHash, m_TestPrefabNetworkObject.PrefabIdHash);
        }
    }
}
