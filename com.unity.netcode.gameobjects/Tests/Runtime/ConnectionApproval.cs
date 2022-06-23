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
            NetworkManagerHelper.NetworkManagerObject.ConnectionApprovalCallback = NetworkManagerObject_ConnectionApprovalCallback;
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

        private NetworkManager.ConnectionApprovalResponse NetworkManagerObject_ConnectionApprovalCallback(NetworkManager.ConnectionApprovalRequest request)
        {

            //NetworkManager.ConnectionApprovalResponse response = new  NetworkManager.ConnectionApprovalResponse();
            var stringGuid = Encoding.UTF8.GetString(request.Payload);
            if (m_ValidationToken.ToString() == stringGuid)
            {
                m_IsValidated = true;
            }


            return new NetworkManager.ConnectionApprovalResponse{
                Approved = m_IsValidated,
                CreatePlayerObject = false,
                Position = null,
                Rotation = null,
                PlayerPrefabHash = null
            };

            //response.Approved = m_IsValidated;
            //response.CreatePlayerObject = false;
            //response.Position = null;
            //response.Rotation = null;
            //response.PlayerPrefabHash = null;

            //return response;
        }

        [TearDown]
        public void TearDown()
        {
            // Stop, shutdown, and destroy
            NetworkManagerHelper.ShutdownNetworkManager();
        }
    }
}
