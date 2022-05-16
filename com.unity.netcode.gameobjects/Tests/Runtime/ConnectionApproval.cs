using System;
using System.Text;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;

namespace Unity.Netcode.RuntimeTests
{
    public class ConnectionApprovalTests
    {
        private Guid m_ValidationToken;
        private bool m_IsValidated;

        [SetUp]
        public void Setup()
        {
            // Create, instantiate, and host
            Assert.IsTrue(NetworkManagerHelper.StartNetworkManager(out _, NetworkManagerHelper.NetworkManagerOperatingMode.None));
            m_ValidationToken = Guid.NewGuid();
        }

        [UnityTest]
        public IEnumerator ConnectionApproval()
        {
            NetworkManagerHelper.NetworkManagerObject.ConnectionApprovalHandler += NetworkManagerObject_ConnectionApprovalCallback;
            NetworkManagerHelper.NetworkManagerObject.NetworkConfig.ConnectionApproval = true;
            NetworkManagerHelper.NetworkManagerObject.NetworkConfig.PlayerPrefab = null;
            NetworkManagerHelper.NetworkManagerObject.NetworkConfig.ConnectionData = Encoding.UTF8.GetBytes(m_ValidationToken.ToString());
            m_IsValidated = false;
            NetworkManagerHelper.NetworkManagerObject.StartHost();

            var timeOut = Time.realtimeSinceStartup + 3.0f;
            var timedOut = false;
            while (!m_IsValidated)
            {
                yield return new WaitForSeconds(0.01f);
                if (timeOut < Time.realtimeSinceStartup)
                {
                    timedOut = true;
                }
            }

            //Make sure we didn't time out
            Assert.False(timedOut);
            Assert.True(m_IsValidated);
        }

        private NetworkManager.ConnectionApprovalResult NetworkManagerObject_ConnectionApprovalCallback(NetworkManager.ConnectionApprovalRequest request)
        {
            var decision = new NetworkManager.ConnectionApprovalResult();
            var stringGuid = Encoding.UTF8.GetString(request.Payload);
            if (m_ValidationToken.ToString() == stringGuid)
            {
                m_IsValidated = true;
            }

            decision.Approved = m_IsValidated;
            decision.CreatePlayerObject = false;
            decision.Position = null;
            decision.Rotation = null;
            decision.PlayerPrefabHash = null;

            return decision;
        }

        [TearDown]
        public void TearDown()
        {
            // Stop, shutdown, and destroy
            NetworkManagerHelper.ShutdownNetworkManager();
        }
    }
}
