using System;
using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    internal class DisconnectReasonObject : NetworkBehaviour
    {

    }

    internal class DisconnectReasonTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        private GameObject m_PrefabToSpawn;

        protected override void OnServerAndClientsCreated()
        {
            m_PrefabToSpawn = CreateNetworkObjectPrefab("DisconnectReasonObject");
            m_PrefabToSpawn.AddComponent<DisconnectReasonObject>();
        }

        private int m_DisconnectCount;
        private bool m_ThrowOnDisconnect = false;

        public void OnClientDisconnectCallback(ulong clientId)
        {
            m_DisconnectCount++;
            if (m_ThrowOnDisconnect)
            {
                throw new SystemException("whatever");
            }
        }

        [UnityTest]
        public IEnumerator DisconnectReasonTest()
        {
            float startTime = Time.realtimeSinceStartup;
            m_ThrowOnDisconnect = false;
            m_DisconnectCount = 0;

            // Add a callback for both clients, when they get disconnected
            m_ClientNetworkManagers[0].OnClientDisconnectCallback += OnClientDisconnectCallback;
            m_ClientNetworkManagers[1].OnClientDisconnectCallback += OnClientDisconnectCallback;

            // Disconnect both clients, from the server
            m_ServerNetworkManager.DisconnectClient(m_ClientNetworkManagers[0].LocalClientId, "Bogus reason 1");
            m_ServerNetworkManager.DisconnectClient(m_ClientNetworkManagers[1].LocalClientId, "Bogus reason 2");

            while (m_DisconnectCount < 2 && Time.realtimeSinceStartup < startTime + 10.0f)
            {
                yield return null;
            }

            Assert.AreEqual(m_ClientNetworkManagers[0].DisconnectReason, "Bogus reason 1");
            Assert.AreEqual(m_ClientNetworkManagers[1].DisconnectReason, "Bogus reason 2");

            Debug.Assert(m_DisconnectCount == 2);
        }

        [UnityTest]
        public IEnumerator DisconnectExceptionTest()
        {
            m_ThrowOnDisconnect = true;
            m_DisconnectCount = 0;
            float startTime = Time.realtimeSinceStartup;

            // Add a callback for first client, when they get disconnected
            m_ClientNetworkManagers[0].OnClientDisconnectCallback += OnClientDisconnectCallback;
            m_ClientNetworkManagers[1].OnClientDisconnectCallback += OnClientDisconnectCallback;

            // Disconnect first client, from the server
            LogAssert.Expect(LogType.Exception, new Regex(".*whatever.*"));
            m_ServerNetworkManager.DisconnectClient(m_ClientNetworkManagers[0].LocalClientId);

            // Disconnect second client, from the server
            LogAssert.Expect(LogType.Exception, new Regex(".*whatever.*"));
            m_ServerNetworkManager.DisconnectClient(m_ClientNetworkManagers[1].LocalClientId);

            while (m_DisconnectCount < 2 && Time.realtimeSinceStartup < startTime + 10.0f)
            {
                yield return null;
            }

            Debug.Assert(m_DisconnectCount == 2);
        }
    }
}
