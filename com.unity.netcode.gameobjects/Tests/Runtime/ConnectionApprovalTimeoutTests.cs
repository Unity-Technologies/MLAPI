using System.Collections;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    [TestFixture(ApprovalFailureTypes.ServerDoesNotRespond)]
    [TestFixture(ApprovalFailureTypes.ClientDoesNotRequest)]
    public class ConnectionApprovalTimeoutTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;

        public enum ApprovalFailureTypes
        {
            ClientDoesNotRequest,
            ServerDoesNotRespond
        }

        private ApprovalFailureTypes m_ApprovalFailureType;

        public ConnectionApprovalTimeoutTests(ApprovalFailureTypes approvalFailureType)
        {
            m_ApprovalFailureType = approvalFailureType;
        }

        // Must be >= 2 since this is an int value and the test waits for timeout - 1 to try to verify it doesn't
        // time out early
        private const int k_TestTimeoutPeriod = 1;

        private Regex m_ExpectedLogMessage;
        private LogType m_LogType;


        protected override IEnumerator OnSetup()
        {
            m_BypassConnectionTimeout = true;
            return base.OnSetup();
        }

        protected override IEnumerator OnTearDown()
        {
            m_BypassConnectionTimeout = true;
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
            if (m_ApprovalFailureType == ApprovalFailureTypes.ServerDoesNotRespond)
            {
                // We catch (don't process) the incoming approval message to simulate the server not sending the approved message in time
                m_ClientNetworkManagers[0].MessagingSystem.Hook(new MessageCatcher<ConnectionApprovedMessage>(m_ClientNetworkManagers[0]));
                m_ExpectedLogMessage = new Regex("Timed out waiting for the server to approve the connection request.");
                m_LogType = LogType.Log;
            }
            else
            {
                // We catch (don't process) the incoming connection request message to simulate a transport connection but the client never
                // sends (or takes too long to send) the connection request.
                m_ServerNetworkManager.MessagingSystem.Hook(new MessageCatcher<ConnectionRequestMessage>(m_ServerNetworkManager));

                // For this test, we know the timed out client will be Client-1
                m_ExpectedLogMessage = new Regex("Server detected a transport connection from Client-1, but timed out waiting for the connection request message.");
                m_LogType = LogType.Warning;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator ValidateApprovalTimeout()
        {
            // Delay for half of the wait period
            yield return new WaitForSeconds(k_TestTimeoutPeriod * 0.5f);

            // Verify we haven't received the time out message yet (we already waiting for half of the time out period in OnStartedServerAndClients)
            NetcodeLogAssert.LogWasNotReceived(LogType.Log, m_ExpectedLogMessage);

            // Wait for 3/4s of the time out period to pass (totally 1.25 times the wait period)
            yield return new WaitForSeconds(k_TestTimeoutPeriod * 0.75f);

            // We should have a test relative logged message by this time.
            NetcodeLogAssert.LogWasReceived(m_LogType, m_ExpectedLogMessage);

            // It should only have the host client connected
            Assert.AreEqual(1, m_ServerNetworkManager.ConnectedClients.Count, $"Expected only one client when there were {m_ServerNetworkManager.ConnectedClients.Count} clients connected!");
            Assert.AreEqual(0, m_ServerNetworkManager.PendingClients.Count, $"Expected no pending clients when there were {m_ServerNetworkManager.PendingClients.Count} pending clients!");
            Assert.True(!m_ClientNetworkManagers[0].IsApproved, $"Expected the client to not have been approved, but it was!");
        }
    }
}
