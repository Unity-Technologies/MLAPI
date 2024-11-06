using System;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;

namespace Unity.Netcode.RuntimeTests
{
    internal sealed class NetworkVariableNameTests
    {
        private NetworkVariableNameComponent m_NetworkVariableNameComponent;

        [SetUp]
        public void SetUp()
        {
            NetworkManagerHelper.StartNetworkManager(out _);

            var gameObjectId = NetworkManagerHelper.AddGameNetworkObject(Guid.NewGuid().ToString());
            m_NetworkVariableNameComponent = NetworkManagerHelper.AddComponentToObject<NetworkVariableNameComponent>(gameObjectId);
            NetworkManagerHelper.SpawnNetworkObject(gameObjectId);
        }

        [TearDown]
        public void TearDown()
        {
            NetworkManagerHelper.ShutdownNetworkManager();
        }

        [Test]
        public void VerifyNetworkVariableNameInitialization()
        {
            // Fields have regular naming
            Assert.AreEqual(nameof(NetworkVariableNameComponent.NetworkVarList), m_NetworkVariableNameComponent.NetworkVarList.Name);
        }

        private class NetworkVariableNameComponent : NetworkBehaviour
        {
            public NetworkList<ulong> NetworkVarList = new NetworkList<ulong>();
        }
    }
}
