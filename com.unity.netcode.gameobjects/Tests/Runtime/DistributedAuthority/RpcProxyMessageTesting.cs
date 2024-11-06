using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// This test validates PR-3000 where it would invoke
    /// TODO:
    /// We really need to get the service running during tests
    /// so we can validate these issues. While this test does
    /// partially validate it we still need to manually validate
    /// with a service connection.
    /// </summary>
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.DAHost)]
    public class RpcProxyMessageTesting : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        private List<RpcProxyText> m_ProxyTestInstances = new List<RpcProxyText>();

        private StringBuilder m_ValidationLogger = new StringBuilder();

        public RpcProxyMessageTesting(HostOrServer hostOrServer) : base(hostOrServer) { }

        protected override IEnumerator OnSetup()
        {
            m_ProxyTestInstances.Clear();
            return base.OnSetup();
        }

        protected override void OnCreatePlayerPrefab()
        {
            m_PlayerPrefab.AddComponent<RpcProxyText>();
            base.OnCreatePlayerPrefab();
        }


        private bool ValidateRpcProxyRpcs()
        {
            m_ValidationLogger.Clear();
            foreach (var proxy in m_ProxyTestInstances)
            {
                if (proxy.ReceivedRpc.Count < NumberOfClients)
                {
                    m_ValidationLogger.AppendLine($"Not all clients received RPC from Client-{proxy.OwnerClientId}!");
                }
                foreach (var clientId in proxy.ReceivedRpc)
                {
                    if (clientId == proxy.OwnerClientId)
                    {
                        m_ValidationLogger.AppendLine($"Client-{proxy.OwnerClientId} sent itself an Rpc!");
                    }
                }
            }
            return m_ValidationLogger.Length == 0;
        }


        public IEnumerator ProxyDoesNotInvokeOnSender()
        {
            m_ProxyTestInstances.Add(m_ServerNetworkManager.LocalClient.PlayerObject.GetComponent<RpcProxyText>());
            foreach (var client in m_ClientNetworkManagers)
            {
                m_ProxyTestInstances.Add(client.LocalClient.PlayerObject.GetComponent<RpcProxyText>());
            }

            foreach (var clientProxyTest in m_ProxyTestInstances)
            {
                clientProxyTest.SendToEveryOneButMe();
            }

            yield return WaitForConditionOrTimeOut(ValidateRpcProxyRpcs);
            AssertOnTimeout(m_ValidationLogger.ToString());
        }

        public class RpcProxyText : NetworkBehaviour
        {
            public List<ulong> ReceivedRpc = new List<ulong>();

            public void SendToEveryOneButMe()
            {
                var baseTarget = NetworkManager.DistributedAuthorityMode ? RpcTarget.NotAuthority : RpcTarget.NotMe;
                TestRpc(baseTarget);
            }

            [Rpc(SendTo.SpecifiedInParams)]
            private void TestRpc(RpcParams rpcParams = default)
            {
                ReceivedRpc.Add(rpcParams.Receive.SenderClientId);
            }
        }
    }
}
