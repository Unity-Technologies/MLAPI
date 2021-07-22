using System;
using MLAPI.NetworkVariable;
using MLAPI.NetworkVariable.Collections;
using NUnit.Framework;
using UnityEngine;

namespace MLAPI.RuntimeTests.Profiling
{
    public sealed class NetworkVariableNameTests
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
            // Properties have the following name format: "<PropertyName>k__BackingField"
            StringAssert.Contains(nameof(NetworkVariableNameComponent.NetworkVarString), m_NetworkVariableNameComponent.NetworkVarString.Name);
            StringAssert.Contains(nameof(NetworkVariableNameComponent.NetworkVarSet), m_NetworkVariableNameComponent.NetworkVarSet.Name);

            // Fields have regular naming
            Assert.AreEqual(nameof(NetworkVariableNameComponent.NetworkVarList), m_NetworkVariableNameComponent.NetworkVarList.Name);
            Assert.AreEqual(nameof(NetworkVariableNameComponent.NetworkVarDictionary), m_NetworkVariableNameComponent.NetworkVarDictionary.Name);
        }

        private class NetworkVariableNameComponent : NetworkBehaviour
        {
            public NetworkVariableString NetworkVarString { get; } = new NetworkVariableString();

            public NetworkSet<ulong> NetworkVarSet { get; } = new NetworkSet<ulong>();

            public NetworkList<ulong> NetworkVarList = new NetworkList<ulong>();

            public NetworkDictionary<ulong, ulong> NetworkVarDictionary = new NetworkDictionary<ulong, ulong>();
        }
    }
}
