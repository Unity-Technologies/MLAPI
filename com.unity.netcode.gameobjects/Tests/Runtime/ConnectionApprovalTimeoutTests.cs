using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    [TestFixture(ApprovalTimedOutTypes.ServerDoesNotRespond)]
    [TestFixture(ApprovalTimedOutTypes.ClientDoesNotRequest)]
    internal class ConnectionApprovalTimeoutTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;

        public enum ApprovalTimedOutTypes
        {
            ClientDoesNotRequest,
            ServerDoesNotRespond
        }

        private ApprovalTimedOutTypes m_ApprovalFailureType;

        public ConnectionApprovalTimeoutTests(ApprovalTimedOutTypes approvalFailureType)
        {
            m_ApprovalFailureType = approvalFailureType;
        }

        // Must be >= 5 since this is an int value and the test waits for timeout - 1 to try to verify it doesn't
        // time out early
        private const int k_TestTimeoutPeriod = 5;

        private Regex m_ExpectedLogMessage;
        private LogType m_LogType;


        protected override IEnumerator OnSetup()
        {
            m_BypassConnectionTimeout = true;
            return base.OnSetup();
        }

        protected override IEnumerator OnTearDown()
        {
            m_BypassConnectionTimeout = false;
            return base.OnTearDown();
        }

        protected override void OnServerAndClientsCreated()
        {
            m_ServerNetworkManager.NetworkConfig.ClientConnectionBufferTimeout = k_TestTimeoutPeriod;
            m_ServerNetworkManager.LogLevel = LogLevel.Developer;
            m_ClientNetworkManagers[0].NetworkConfig.ClientConnectionBufferTimeout = k_TestTimeoutPeriod;
            m_ClientNetworkManagers[0].LogLevel = LogLevel.Developer;
            base.OnServerAndClientsCreated();
        }

        protected override IEnumerator OnStartedServerAndClients()
        {
            if (m_ApprovalFailureType == ApprovalTimedOutTypes.ServerDoesNotRespond)
            {
                m_ServerNetworkManager.ConnectionManager.MockSkippingApproval = true;
                // We catch (don't process) the incoming approval message to simulate the server not sending the approved message in time
                m_ClientNetworkManagers[0].ConnectionManager.MessageManager.Hook(new MessageCatcher<ConnectionApprovedMessage>(m_ClientNetworkManagers[0]));
                m_ExpectedLogMessage = new Regex("Timed out waiting for the server to approve the connection request.");
                m_LogType = LogType.Log;
            }
            else
            {
                // We catch (don't process) the incoming connection request message to simulate a transport connection but the client never
                // sends (or takes too long to send) the connection request.
                m_ServerNetworkManager.ConnectionManager.MessageManager.Hook(new MessageCatcher<ConnectionRequestMessage>(m_ServerNetworkManager));

                // For this test, we know the timed out client will be Client-1
                m_ExpectedLogMessage = new Regex("Server detected a transport connection from Client-1, but timed out waiting for the connection request message.");
                m_LogType = LogType.Warning;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ValidateApprovalTimeout()
        {
            // Just delay for a second
            yield return new WaitForSeconds(k_TestTimeoutPeriod * 0.25f);

            // Verify we haven't received the time out message yet
            NetcodeLogAssert.LogWasNotReceived(LogType.Log, m_ExpectedLogMessage);

            yield return new WaitForSeconds(k_TestTimeoutPeriod * 1.25f);

            // We should have the test relative log message by this time.
            NetcodeLogAssert.LogWasReceived(m_LogType, m_ExpectedLogMessage);

            VerboseDebug("Checking connected client count");
            // It should only have the host client connected
            Assert.AreEqual(1, m_ServerNetworkManager.ConnectedClients.Count, $"Expected only one client when there were {m_ServerNetworkManager.ConnectedClients.Count} clients connected!");


            Assert.AreEqual(0, m_ServerNetworkManager.ConnectionManager.PendingClients.Count, $"Expected no pending clients when there were {m_ServerNetworkManager.ConnectionManager.PendingClients.Count} pending clients!");
            Assert.True(!m_ClientNetworkManagers[0].LocalClient.IsApproved, $"Expected the client to not have been approved, but it was!");
        }
    }
}
