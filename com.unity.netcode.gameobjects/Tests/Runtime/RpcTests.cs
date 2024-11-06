using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine.TestTools;
using Vector3 = UnityEngine.Vector3;

namespace Unity.Netcode.RuntimeTests
{
    internal class RpcTests : NetcodeIntegrationTest
    {
        internal class CompileTimeNoRpcsBaseClassTest : NetworkBehaviour
        {

        }

        internal class CompileTimeHasRpcsChildClassDerivedFromNoRpcsBaseClassTest : CompileTimeNoRpcsBaseClassTest
        {
            [ServerRpc]
            public void SomeDummyServerRpc()
            {

            }
        }

        internal class GenericRpcTestNB<T> : NetworkBehaviour where T : unmanaged
        {
            public event Action<T, ServerRpcParams> OnServer_Rpc;

            [ServerRpc]
            public void MyServerRpc(T clientId, ServerRpcParams param = default)
            {
                OnServer_Rpc(clientId, param);
            }
        }

        internal class RpcTestNBFloat : GenericRpcTestNB<float>
        {
        }

        internal class RpcTestNB : GenericRpcTestNB<ulong>
        {
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            public event Action<NativeList<ulong>, ServerRpcParams> OnNativeListServer_Rpc;
#endif
            public event Action<Vector3, Vector3[],
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
                NativeList<Vector3>,
#endif
                FixedString32Bytes> OnTypedServer_Rpc;

            public event Action OnClient_Rpc;

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ServerRpc]
            public void MyNativeListServerRpc(NativeList<ulong> clientId, ServerRpcParams param = default)
            {
                OnNativeListServer_Rpc(clientId, param);
            }
#endif

            [ClientRpc]
            public void MyClientRpc()
            {
                OnClient_Rpc();
            }

            [ServerRpc]
            public void MyTypedServerRpc(Vector3 param1, Vector3[] param2,
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
                NativeList<Vector3> param3,
#endif
                FixedString32Bytes param4)
            {
                OnTypedServer_Rpc(param1, param2,
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
                    param3,
#endif
                    param4);
            }
        }

        protected override int NumberOfClients => 1;

        protected override void OnCreatePlayerPrefab()
        {
            m_PlayerPrefab.AddComponent<RpcTestNB>();
            m_PlayerPrefab.AddComponent<RpcTestNBFloat>();
        }

        [UnityTest]
        public IEnumerator TestRpcs()
        {
            // This is the *SERVER VERSION* of the *CLIENT PLAYER* RpcTestNB component
            var serverClientRpcTestNB = m_PlayerNetworkObjects[m_ServerNetworkManager.LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<RpcTestNB>();
            var serverClientRpcTestNBFloat = m_PlayerNetworkObjects[m_ServerNetworkManager.LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<RpcTestNBFloat>();

            // This is the *CLIENT VERSION* of the *CLIENT PLAYER* RpcTestNB component
            var localClienRpcTestNB = m_PlayerNetworkObjects[m_ClientNetworkManagers[0].LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<RpcTestNB>();
            var localClienRpcTestNBFloat = m_PlayerNetworkObjects[m_ClientNetworkManagers[0].LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<RpcTestNBFloat>();

            // Setup state
            bool hasReceivedServerRpc = false;
            bool hasReceivedFloatServerRpc = false;
            bool hasReceivedTypedServerRpc = false;
            bool hasReceivedClientRpcRemotely = false;
            bool hasReceivedClientRpcLocally = false;

            var vector3 = new Vector3(1, 2, 3);
            Vector3[] vector3s = new[] { new Vector3(4, 5, 6), new Vector3(7, 8, 9) };
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            using var vector3sNativeList = new NativeList<Vector3>(Allocator.Persistent)
            {
                new Vector3(10, 11, 12),
                new Vector3(13, 14, 15)
            };
#endif

            localClienRpcTestNB.OnClient_Rpc += () =>
            {
                VerboseDebug("ClientRpc received on client object");
                hasReceivedClientRpcRemotely = true;
            };

            localClienRpcTestNB.OnServer_Rpc += (clientId, param) =>
            {
                // The RPC invoked locally. (Weaver failure?)
                Assert.Fail("ServerRpc invoked locally. Weaver failure?");
            };

            localClienRpcTestNBFloat.OnServer_Rpc += (clientId, param) =>
            {
                // The RPC invoked locally. (Weaver failure?)
                Assert.Fail("ServerRpc (float) invoked locally. Weaver failure?");
            };

            serverClientRpcTestNB.OnServer_Rpc += (clientId, param) =>
            {
                VerboseDebug("ServerRpc received on server object");
                Assert.True(param.Receive.SenderClientId == clientId);
                hasReceivedServerRpc = true;
            };

            serverClientRpcTestNBFloat.OnServer_Rpc += (clientId, param) =>
            {
                VerboseDebug("ServerRpc (float) received on server object");
                Assert.True(param.Receive.SenderClientId == clientId);
                hasReceivedFloatServerRpc = true;
            };

            serverClientRpcTestNB.OnClient_Rpc += () =>
            {
                // The RPC invoked locally. (Weaver failure?)
                VerboseDebug("ClientRpc received on server object");
                hasReceivedClientRpcLocally = true;
            };

            var str = new FixedString32Bytes("abcdefg");

            serverClientRpcTestNB.OnTypedServer_Rpc += (param1, param2,

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
                param3,
#endif
                param4) =>
            {
                VerboseDebug("TypedServerRpc received on server object");
                Assert.AreEqual(param1, vector3);
                Assert.AreEqual(param2.Length, vector3s.Length);
                Assert.AreEqual(param2[0], vector3s[0]);
                Assert.AreEqual(param2[1], vector3s[1]);
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
                Assert.AreEqual(param3.Length, vector3s.Length);
                Assert.AreEqual(param3[0], vector3sNativeList[0]);
                Assert.AreEqual(param3[1], vector3sNativeList[1]);
#endif
                Assert.AreEqual(param4, str);
                hasReceivedTypedServerRpc = true;
            };

            // Send ServerRpc
            localClienRpcTestNB.MyServerRpc(m_ClientNetworkManagers[0].LocalClientId);
            localClienRpcTestNBFloat.MyServerRpc(m_ClientNetworkManagers[0].LocalClientId);

            // Send TypedServerRpc
            localClienRpcTestNB.MyTypedServerRpc(vector3, vector3s,
#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
                vector3sNativeList,
#endif
                str);

            // Send ClientRpc
            serverClientRpcTestNB.MyClientRpc();

            // Validate each NetworkManager relative NetworkMessageManager received each respective RPC
            var messageHookList = new List<MessageHookEntry>();
            var serverMessageHookEntry = new MessageHookEntry(m_ServerNetworkManager);
            serverMessageHookEntry.AssignMessageType<ServerRpcMessage>();
            messageHookList.Add(serverMessageHookEntry);

            var typedServerMessageHookEntry = new MessageHookEntry(m_ServerNetworkManager);
            typedServerMessageHookEntry.AssignMessageType<ServerRpcMessage>();
            messageHookList.Add(typedServerMessageHookEntry);

            foreach (var client in m_ClientNetworkManagers)
            {
                var clientMessageHookEntry = new MessageHookEntry(client);
                clientMessageHookEntry.AssignMessageType<ClientRpcMessage>();
                messageHookList.Add(clientMessageHookEntry);
            }

            var rpcMessageHooks = new MessageHooksConditional(messageHookList);
            yield return WaitForConditionOrTimeOut(rpcMessageHooks);
            Assert.False(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for messages: {rpcMessageHooks.GetHooksStillWaiting()}");

            // Make sure RPCs propagated all the way up and were called on the relative destination class instance
            yield return WaitForConditionOrTimeOut(() => hasReceivedServerRpc && hasReceivedClientRpcLocally && hasReceivedClientRpcRemotely && hasReceivedTypedServerRpc);

            Assert.True(hasReceivedServerRpc, "ServerRpc was not received");
            Assert.True(hasReceivedFloatServerRpc, "ServerRpc was not received");
            Assert.True(hasReceivedTypedServerRpc, "TypedServerRpc was not received");
            Assert.True(hasReceivedClientRpcLocally, "ClientRpc was not locally received on the server");
            Assert.True(hasReceivedClientRpcRemotely, "ClientRpc was not remotely received on the client");
        }
    }
}
