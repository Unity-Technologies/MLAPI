#if !MULTIPLAYER_TOOLS && !NGO_MINIMALPROJECT
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;
using Random = System.Random;

// NOTE:
// Unity's test runner cannot handle a single test fixture with thousands of tests in it.
// Since this file contains thousands of tests (once all parameters have been taken into account),
// I had to split up the tests into separate fixtures for each test case.
// That was the only way to get Unity to actually be able to handle this number of tests.
// I put them in their own namespace so they would be easier to navigate in the test list.
namespace Unity.Netcode.RuntimeTests.UniversalRpcTests
{
    internal class UniversalRpcNetworkBehaviour : NetworkBehaviour
    {
        public bool Stop = false;
        public string Received = string.Empty;
        public Tuple<int, bool, float, string> ReceivedParams = null;
        public ulong ReceivedFrom = ulong.MaxValue;
        public int ReceivedCount;

        public void OnRpcReceived()
        {
            var st = new StackTrace();
            var sf = st.GetFrame(1);

            var currentMethod = sf.GetMethod();
            Received = currentMethod.Name;
            ReceivedCount++;
        }
        public void OnRpcReceivedWithParams(int a, bool b, float f, string s)
        {
            var st = new StackTrace();
            var sf = st.GetFrame(1);

            var currentMethod = sf.GetMethod();
            Received = currentMethod.Name;
            ReceivedCount++;
            ReceivedParams = new Tuple<int, bool, float, string>(a, b, f, s);
        }

        // Basic RPCs

        [Rpc(SendTo.Everyone)]
        public void DefaultToEveryoneRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Me)]
        public void DefaultToMeRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Owner)]
        public void DefaultToOwnerRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.NotOwner)]
        public void DefaultToNotOwnerRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Server)]
        public void DefaultToServerRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.NotMe)]
        public void DefaultToNotMeRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.NotServer)]
        public void DefaultToNotServerRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.ClientsAndHost)]
        public void DefaultToClientsAndHostRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Authority)]
        public void DefaultToAuthorityRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.NotAuthority)]
        public void DefaultToNotAuthorityRpc()
        {
            OnRpcReceived();
        }

        // RPCs with parameters

        [Rpc(SendTo.Everyone)]
        public void DefaultToEveryoneWithParamsRpc(int i, bool b, float f, string s)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.Me)]
        public void DefaultToMeWithParamsRpc(int i, bool b, float f, string s)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.Owner)]
        public void DefaultToOwnerWithParamsRpc(int i, bool b, float f, string s)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.NotOwner)]
        public void DefaultToNotOwnerWithParamsRpc(int i, bool b, float f, string s)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.Server)]
        public void DefaultToServerWithParamsRpc(int i, bool b, float f, string s)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.NotMe)]
        public void DefaultToNotMeWithParamsRpc(int i, bool b, float f, string s)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.NotServer)]
        public void DefaultToNotServerWithParamsRpc(int i, bool b, float f, string s)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.ClientsAndHost)]
        public void DefaultToClientsAndHostWithParamsRpc(int i, bool b, float f, string s)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.Authority)]
        public void DefaultToAuthorityWithParamsRpc(int i, bool b, float f, string s)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.NotAuthority)]
        public void DefaultToNotAuthorityWithParamsRpc(int i, bool b, float f, string s)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        // RPCs with RPC parameters

        [Rpc(SendTo.Everyone)]
        public void DefaultToEveryoneWithRpcParamsRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
            ReceivedFrom = rpcParams.Receive.SenderClientId;
        }

        [Rpc(SendTo.Me)]
        public void DefaultToMeWithRpcParamsRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
            ReceivedFrom = rpcParams.Receive.SenderClientId;
        }

        [Rpc(SendTo.Owner)]
        public void DefaultToOwnerWithRpcParamsRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
            ReceivedFrom = rpcParams.Receive.SenderClientId;
        }

        [Rpc(SendTo.NotOwner)]
        public void DefaultToNotOwnerWithRpcParamsRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
            ReceivedFrom = rpcParams.Receive.SenderClientId;
        }

        [Rpc(SendTo.Server)]
        public void DefaultToServerWithRpcParamsRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
            ReceivedFrom = rpcParams.Receive.SenderClientId;
        }

        [Rpc(SendTo.NotMe)]
        public void DefaultToNotMeWithRpcParamsRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
            ReceivedFrom = rpcParams.Receive.SenderClientId;
        }

        [Rpc(SendTo.NotServer)]
        public void DefaultToNotServerWithRpcParamsRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
            ReceivedFrom = rpcParams.Receive.SenderClientId;
        }

        [Rpc(SendTo.ClientsAndHost)]
        public void DefaultToClientsAndHostWithRpcParamsRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
            ReceivedFrom = rpcParams.Receive.SenderClientId;
        }

        [Rpc(SendTo.Authority)]
        public void DefaultToAuthorityWithRpcParamsRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
            ReceivedFrom = rpcParams.Receive.SenderClientId;
        }

        [Rpc(SendTo.NotAuthority)]
        public void DefaultToNotAuthorityWithRpcParamsRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
            ReceivedFrom = rpcParams.Receive.SenderClientId;
        }

        // RPCs with parameters and RPC parameters

        [Rpc(SendTo.Everyone)]
        public void DefaultToEveryoneWithParamsAndRpcParamsRpc(int i, bool b, float f, string s, RpcParams rpcParams)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.Me)]
        public void DefaultToMeWithParamsAndRpcParamsRpc(int i, bool b, float f, string s, RpcParams rpcParams)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.Owner)]
        public void DefaultToOwnerWithParamsAndRpcParamsRpc(int i, bool b, float f, string s, RpcParams rpcParams)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.NotOwner)]
        public void DefaultToNotOwnerWithParamsAndRpcParamsRpc(int i, bool b, float f, string s, RpcParams rpcParams)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.Server)]
        public void DefaultToServerWithParamsAndRpcParamsRpc(int i, bool b, float f, string s, RpcParams rpcParams)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.NotMe)]
        public void DefaultToNotMeWithParamsAndRpcParamsRpc(int i, bool b, float f, string s, RpcParams rpcParams)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.NotServer)]
        public void DefaultToNotServerWithParamsAndRpcParamsRpc(int i, bool b, float f, string s, RpcParams rpcParams)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.ClientsAndHost)]
        public void DefaultToClientsAndHostWithParamsAndRpcParamsRpc(int i, bool b, float f, string s, RpcParams rpcParams)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.Authority)]
        public void DefaultToAuthorityWithParamsAndRpcParamsRpc(int i, bool b, float f, string s, RpcParams rpcParams)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        [Rpc(SendTo.NotAuthority)]
        public void DefaultToNotAuthorityWithParamsAndRpcParamsRpc(int i, bool b, float f, string s, RpcParams rpcParams)
        {
            OnRpcReceivedWithParams(i, b, f, s);
        }

        // RPCs with AllowTargetOverride = true

        // AllowTargetOverried is implied with SpecifiedInParams and does not need to be stated
        // Including it will cause a compiler warning
        [Rpc(SendTo.SpecifiedInParams)]
        public void DefaultToSpecifiedInParamsAllowOverrideRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Everyone, AllowTargetOverride = true)]
        public void DefaultToEveryoneAllowOverrideRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Me, AllowTargetOverride = true)]
        public void DefaultToMeAllowOverrideRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Owner, AllowTargetOverride = true)]
        public void DefaultToOwnerAllowOverrideRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.NotOwner, AllowTargetOverride = true)]
        public void DefaultToNotOwnerAllowOverrideRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Server, AllowTargetOverride = true)]
        public void DefaultToServerAllowOverrideRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.NotMe, AllowTargetOverride = true)]
        public void DefaultToNotMeAllowOverrideRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.NotServer, AllowTargetOverride = true)]
        public void DefaultToNotServerAllowOverrideRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.ClientsAndHost, AllowTargetOverride = true)]
        public void DefaultToClientsAndHostAllowOverrideRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Authority, AllowTargetOverride = true)]
        public void DefaultToAuthorityAllowOverrideRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.NotAuthority, AllowTargetOverride = true)]
        public void DefaultToNotAuthorityAllowOverrideRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        // RPCs with DeferLocal = true

        [Rpc(SendTo.Everyone, DeferLocal = true)]
        public void DefaultToEveryoneDeferLocalRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Me, DeferLocal = true)]
        public void DefaultToMeDeferLocalRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Owner, DeferLocal = true)]
        public void DefaultToOwnerDeferLocalRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.NotOwner, DeferLocal = true)]
        public void DefaultToNotOwnerDeferLocalRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Server, DeferLocal = true)]
        public void DefaultToServerDeferLocalRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.NotServer, DeferLocal = true)]
        public void DefaultToNotServerDeferLocalRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.ClientsAndHost, DeferLocal = true)]
        public void DefaultToClientsAndHostDeferLocalRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Authority, DeferLocal = true)]
        public void DefaultToAuthorityDeferLocalRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.NotAuthority, DeferLocal = true)]
        public void DefaultToNotAuthorityDeferLocalRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        // RPCs with RequireOwnership = true

        [Rpc(SendTo.Everyone, RequireOwnership = true)]
        public void DefaultToEveryoneRequireOwnershipRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Me, RequireOwnership = true)]
        public void DefaultToMeRequireOwnershipRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Owner, RequireOwnership = true)]
        public void DefaultToOwnerRequireOwnershipRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.NotOwner, RequireOwnership = true)]
        public void DefaultToNotOwnerRequireOwnershipRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Server, RequireOwnership = true)]
        public void DefaultToServerRequireOwnershipRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.NotMe, RequireOwnership = true)]
        public void DefaultToNotMeRequireOwnershipRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.NotServer, RequireOwnership = true)]
        public void DefaultToNotServerRequireOwnershipRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.ClientsAndHost, RequireOwnership = true)]
        public void DefaultToClientsAndHostRequireOwnershipRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.SpecifiedInParams, RequireOwnership = true)]
        public void SpecifiedInParamsRequireOwnershipRpc(RpcParams rpcParams)
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.Authority, RequireOwnership = true)]
        public void DefaultToAuthorityRequireOwnershipRpc()
        {
            OnRpcReceived();
        }

        [Rpc(SendTo.NotAuthority, RequireOwnership = true)]
        public void DefaultToNotAuthorityRequireOwnershipRpc()
        {
            OnRpcReceived();
        }

        // Mutual RPC Recursion

        [Rpc(SendTo.Server, DeferLocal = true)]
        public void MutualRecursionServerRpc()
        {
            if (Stop)
            {
                Stop = false;
                return;
            }
            OnRpcReceived();
            MutualRecursionClientRpc();
        }

        [Rpc(SendTo.NotServer, DeferLocal = true)]
        public void MutualRecursionClientRpc()
        {
            OnRpcReceived();
            MutualRecursionServerRpc();
        }

        // Self recursion
        [Rpc(SendTo.Server, DeferLocal = true)]
        public void SelfRecursiveRpc()
        {
            if (Stop)
            {
                Stop = false;
                return;
            }
            OnRpcReceived();
            SelfRecursiveRpc();
        }
    }

    internal class UniversalRpcTestsBase : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        public UniversalRpcTestsBase(HostOrServer hostOrServer) : base(hostOrServer)
        {
        }


        protected override NetworkManagerInstatiationMode OnSetIntegrationTestMode()
        {
            return NetworkManagerInstatiationMode.AllTests;
        }

        protected override bool m_EnableTimeTravel => true;

        protected override bool m_SetupIsACoroutine => false;
        protected override bool m_TearDownIsACoroutine => false;

        protected GameObject m_ServerObject;

        protected override void OnCreatePlayerPrefab()
        {
            m_PlayerPrefab.AddComponent<UniversalRpcNetworkBehaviour>();
        }

        protected override void OnServerAndClientsCreated()
        {
            m_ServerObject = new GameObject { name = "Server Object" };
            var networkObject = m_ServerObject.AddComponent<NetworkObject>();
            m_ServerObject.AddComponent<UniversalRpcNetworkBehaviour>();
            networkObject.NetworkManagerOwner = m_ServerNetworkManager;
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObject);
            m_ServerNetworkManager.AddNetworkPrefab(m_ServerObject);
            foreach (var client in m_ClientNetworkManagers)
            {
                client.AddNetworkPrefab(m_ServerObject);
            }
        }

        protected override void OnInlineTearDown()
        {
            Clear();
        }

        protected void Clear()
        {
            foreach (var obj in Object.FindObjectsByType<UniversalRpcNetworkBehaviour>(FindObjectsSortMode.None))
            {
                obj.Received = string.Empty;
                obj.ReceivedCount = 0;
                obj.ReceivedParams = null;
                obj.ReceivedFrom = ulong.MaxValue;
            }
        }

        protected override void OnOneTimeTearDown()
        {
            Object.DestroyImmediate(m_ServerObject);
        }

        protected override void OnTimeTravelServerAndClientsConnected()
        {
            m_ServerObject.GetComponent<NetworkObject>().Spawn();
            WaitForMessageReceivedWithTimeTravel<CreateObjectMessage>(m_ClientNetworkManagers.ToList());
        }

        protected UniversalRpcNetworkBehaviour GetPlayerObject(ulong ownerClientId, ulong onClient)
        {
            if (ownerClientId == NetworkManager.ServerClientId && !m_ServerNetworkManager.IsHost)
            {
                foreach (var obj in Object.FindObjectsByType<UniversalRpcNetworkBehaviour>(FindObjectsSortMode.None))
                {
                    if (obj.name.StartsWith("Server Object") && obj.OwnerClientId == ownerClientId && obj.NetworkManager.LocalClientId == onClient)
                    {
                        return obj;
                    }
                }
            }

            return m_PlayerNetworkObjects[onClient][ownerClientId].GetComponent<UniversalRpcNetworkBehaviour>();
        }

        protected void VerifyLocalReceived(ulong objectOwner, ulong sender, string name, bool verifyReceivedFrom, int expectedReceived = 1)
        {
            var obj = GetPlayerObject(objectOwner, sender);
            Assert.AreEqual(name, obj.Received);
            Assert.That(obj.ReceivedCount, Is.EqualTo(expectedReceived));
            Assert.IsNull(obj.ReceivedParams);
            if (verifyReceivedFrom)
            {
                Assert.AreEqual(sender, obj.ReceivedFrom);
            }
        }

        protected void VerifyLocalReceivedWithParams(ulong objectOwner, ulong sender, string name, int i, bool b, float f, string s)
        {
            var obj = GetPlayerObject(objectOwner, sender);
            Assert.AreEqual(name, obj.Received);
            Assert.That(obj.ReceivedCount, Is.EqualTo(1));
            Assert.IsNotNull(obj.ReceivedParams);
            Assert.AreEqual(i, obj.ReceivedParams.Item1);
            Assert.AreEqual(b, obj.ReceivedParams.Item2);
            Assert.AreEqual(f, obj.ReceivedParams.Item3);
            Assert.AreEqual(s, obj.ReceivedParams.Item4);
        }

        protected void VerifyNotReceived(ulong objectOwner, ulong[] receivedBy)
        {
            foreach (var client in receivedBy)
            {
                UniversalRpcNetworkBehaviour playerObject = GetPlayerObject(objectOwner, client);
                Assert.AreEqual(string.Empty, playerObject.Received);
                Assert.That(playerObject.ReceivedCount, Is.EqualTo(0));
                Assert.IsNull(playerObject.ReceivedParams);
            }
        }

        protected void VerifyRemoteReceived(ulong objectOwner, ulong sender, string message, ulong[] receivedBy, bool verifyReceivedFrom, bool waitForMessages = true, int expectedReceived = 1)
        {
            foreach (var client in receivedBy)
            {
                if (client == sender)
                {
                    VerifyLocalReceived(objectOwner, sender, message, verifyReceivedFrom, expectedReceived);

                    break;
                }
            }

            if (waitForMessages)
            {
                var needsProxyMessage = false;
                var needsServerRpcMessage = false;
                if (sender != 0)
                {
                    foreach (var client in receivedBy)
                    {
                        if (client == sender)
                        {
                            continue;
                        }

                        if (client != 0)
                        {
                            needsProxyMessage = true;
                        }
                        else
                        {
                            needsServerRpcMessage = true;
                        }
                    }
                }

                if (needsProxyMessage)
                {
                    var messages = new List<Type> { typeof(ProxyMessage) };
                    if (needsServerRpcMessage)
                    {
                        messages.Add(typeof(RpcMessage));
                    }

                    WaitForMessagesReceivedWithTimeTravel(messages, new[] { m_ServerNetworkManager }.ToList());
                }

                var managersThatNeedToWaitForRpc = new List<NetworkManager>();
                if (needsServerRpcMessage && !needsProxyMessage)
                {
                    managersThatNeedToWaitForRpc.Add(m_ServerNetworkManager);
                }

                foreach (var client in receivedBy)
                {
                    if (client != sender && client != 0)
                    {
                        managersThatNeedToWaitForRpc.Add(m_ClientNetworkManagers[client - 1]);
                    }
                }

                WaitForMessageReceivedWithTimeTravel<RpcMessage>(managersThatNeedToWaitForRpc);
            }

            foreach (var client in receivedBy)
            {
                UniversalRpcNetworkBehaviour playerObject = GetPlayerObject(objectOwner, client);
                Assert.AreEqual(message, playerObject.Received);
                Assert.That(playerObject.ReceivedCount, Is.EqualTo(expectedReceived));
                Assert.IsNull(playerObject.ReceivedParams);
                if (verifyReceivedFrom)
                {
                    Assert.AreEqual(sender, playerObject.ReceivedFrom);
                }
            }
        }

        protected void VerifyRemoteReceivedWithParams(ulong objectOwner, ulong sender, string message, ulong[] receivedBy, int i, bool b, float f, string s)
        {
            foreach (var client in receivedBy)
            {
                if (client == sender)
                {
                    VerifyLocalReceivedWithParams(objectOwner, sender, message, i, b, f, s);

                    break;
                }
            }

            var needsProxyMessage = false;
            var needsServerRpcMessage = false;
            if (sender != 0)
            {
                foreach (var client in receivedBy)
                {
                    if (client == sender)
                    {
                        continue;
                    }

                    if (client != 0)
                    {
                        needsProxyMessage = true;
                    }
                    else
                    {
                        needsServerRpcMessage = true;
                    }
                }
            }

            if (needsProxyMessage)
            {
                var messages = new List<Type> { typeof(ProxyMessage) };
                if (needsServerRpcMessage)
                {
                    messages.Add(typeof(RpcMessage));
                }

                WaitForMessagesReceivedWithTimeTravel(messages, new[] { m_ServerNetworkManager }.ToList());
            }

            var managersThatNeedToWaitForRpc = new List<NetworkManager>();
            if (needsServerRpcMessage && !needsProxyMessage)
            {
                managersThatNeedToWaitForRpc.Add(m_ServerNetworkManager);
            }

            foreach (var client in receivedBy)
            {
                if (client != sender && client != 0)
                {
                    managersThatNeedToWaitForRpc.Add(m_ClientNetworkManagers[client - 1]);
                }
            }

            WaitForMessageReceivedWithTimeTravel<RpcMessage>(managersThatNeedToWaitForRpc);

            foreach (var client in receivedBy)
            {
                UniversalRpcNetworkBehaviour playerObject = GetPlayerObject(objectOwner, client);
                Assert.AreEqual(message, playerObject.Received);
                Assert.That(playerObject.ReceivedCount, Is.EqualTo(1));

                Assert.IsNotNull(playerObject.ReceivedParams);
                Assert.AreEqual(i, playerObject.ReceivedParams.Item1);
                Assert.AreEqual(b, playerObject.ReceivedParams.Item2);
                Assert.AreEqual(f, playerObject.ReceivedParams.Item3);
                Assert.AreEqual(s, playerObject.ReceivedParams.Item4);
            }
        }

        protected static ulong[] s_ClientIds = new[] { 0ul, 1ul, 2ul };

        public void VerifySentToEveryone(ulong objectOwner, ulong sender, string methodName)
        {
            VerifyRemoteReceived(objectOwner, sender, methodName, s_ClientIds, false);
        }

        public void VerifySentToEveryoneWithReceivedFrom(ulong objectOwner, ulong sender, string methodName)
        {
            VerifyRemoteReceived(objectOwner, sender, methodName, s_ClientIds, true);
        }

        public void VerifySentToEveryoneWithParams(ulong objectOwner, ulong sender, string methodName, int i, bool b, float f, string s)
        {
            VerifyRemoteReceivedWithParams(objectOwner, sender, methodName, s_ClientIds, i, b, f, s);
        }

        public void VerifySentToId(ulong objectOwner, ulong sender, ulong receiver, string methodName, bool verifyReceivedFrom)
        {
            VerifyRemoteReceived(objectOwner, sender, methodName, new[] { receiver }, verifyReceivedFrom);
            VerifyNotReceived(objectOwner, s_ClientIds.Where(c => c != receiver).ToArray());

            // Pass some time to make sure that no other client ever receives this
            TimeTravel(1f, 30);
            VerifyNotReceived(objectOwner, s_ClientIds.Where(c => c != receiver).ToArray());
        }

        public void VerifySentToNotId(ulong objectOwner, ulong sender, ulong notReceiver, string methodName, bool verifyReceivedFrom)
        {
            VerifyNotReceived(objectOwner, new[] { notReceiver });
            VerifyRemoteReceived(objectOwner, sender, methodName, s_ClientIds.Where(c => c != notReceiver).ToArray(), verifyReceivedFrom);
            // Verify again after all the waiting is finished
            VerifyNotReceived(objectOwner, new[] { notReceiver });
        }

        public void VerifySentToIdWithParams(ulong objectOwner, ulong sender, ulong receiver, string methodName, int i, bool b, float f, string s)
        {
            VerifyRemoteReceivedWithParams(objectOwner, sender, methodName, new[] { receiver }, i, b, f, s);
            VerifyNotReceived(objectOwner, s_ClientIds.Where(c => c != receiver).ToArray());

            // Pass some time to make sure that no other client ever receives this
            TimeTravel(1f, 30);
            VerifyNotReceived(objectOwner, s_ClientIds.Where(c => c != receiver).ToArray());
        }

        public void VerifySentToNotIdWithParams(ulong objectOwner, ulong sender, ulong notReceiver, string methodName, int i, bool b, float f, string s)
        {
            VerifyNotReceived(objectOwner, new[] { notReceiver });
            VerifyRemoteReceivedWithParams(objectOwner, sender, methodName, s_ClientIds.Where(c => c != notReceiver).ToArray(), i, b, f, s);
            // Verify again after all the waiting is finished
            VerifyNotReceived(objectOwner, new[] { notReceiver });
        }

        public void VerifySentToOwner(ulong objectOwner, ulong sender, string methodName)
        {
            VerifySentToId(objectOwner, sender, objectOwner, methodName, false);
        }

        public void VerifySentToNotOwner(ulong objectOwner, ulong sender, string methodName)
        {
            VerifySentToNotId(objectOwner, sender, objectOwner, methodName, false);
        }

        public void VerifySentToServer(ulong objectOwner, ulong sender, string methodName)
        {
            VerifySentToId(objectOwner, sender, NetworkManager.ServerClientId, methodName, false);
        }

        public void VerifySentToNotServer(ulong objectOwner, ulong sender, string methodName)
        {
            VerifySentToNotId(objectOwner, sender, NetworkManager.ServerClientId, methodName, false);
        }

        public void VerifySentToClientsAndHost(ulong objectOwner, ulong sender, string methodName)
        {
            if (m_ServerNetworkManager.IsHost)
            {
                VerifySentToEveryone(objectOwner, sender, methodName);
            }
            else
            {
                VerifySentToNotServer(objectOwner, sender, methodName);
            }
        }

        public void VerifySentToMe(ulong objectOwner, ulong sender, string methodName)
        {
            VerifySentToId(objectOwner, sender, sender, methodName, false);
        }

        public void VerifySentToNotMe(ulong objectOwner, ulong sender, string methodName)
        {
            VerifySentToNotId(objectOwner, sender, sender, methodName, false);
        }

        public void VerifySentToAuthority(ulong objectOwner, ulong sender, string methodName)
        {
            var receiver = objectOwner;
            if (!m_DistributedAuthority)
            {
                receiver = NetworkManager.ServerClientId;
            }
            VerifySentToId(objectOwner, sender, receiver, methodName, false);
        }

        public void VerifySentToNotAuthority(ulong objectOwner, ulong sender, string methodName)
        {
            var receiver = objectOwner;
            if (!m_DistributedAuthority)
            {
                receiver = NetworkManager.ServerClientId;
            }
            VerifySentToNotId(objectOwner, sender, receiver, methodName, false);
        }

        public void VerifySentToOwnerWithReceivedFrom(ulong objectOwner, ulong sender, string methodName)
        {
            VerifySentToId(objectOwner, sender, objectOwner, methodName, true);
        }

        public void VerifySentToNotOwnerWithReceivedFrom(ulong objectOwner, ulong sender, string methodName)
        {
            VerifySentToNotId(objectOwner, sender, objectOwner, methodName, true);
        }

        public void VerifySentToServerWithReceivedFrom(ulong objectOwner, ulong sender, string methodName)
        {
            VerifySentToId(objectOwner, sender, NetworkManager.ServerClientId, methodName, true);
        }

        public void VerifySentToNotServerWithReceivedFrom(ulong objectOwner, ulong sender, string methodName)
        {
            VerifySentToNotId(objectOwner, sender, NetworkManager.ServerClientId, methodName, true);
        }

        public void VerifySentToClientsAndHostWithReceivedFrom(ulong objectOwner, ulong sender, string methodName)
        {
            if (m_ServerNetworkManager.IsHost)
            {
                VerifySentToEveryoneWithReceivedFrom(objectOwner, sender, methodName);
            }
            else
            {
                VerifySentToNotServerWithReceivedFrom(objectOwner, sender, methodName);
            }
        }

        public void VerifySentToMeWithReceivedFrom(ulong objectOwner, ulong sender, string methodName)
        {
            VerifySentToId(objectOwner, sender, sender, methodName, true);
        }

        public void VerifySentToNotMeWithReceivedFrom(ulong objectOwner, ulong sender, string methodName)
        {
            VerifySentToNotId(objectOwner, sender, sender, methodName, true);
        }

        public void VerifySentToAuthorityWithReceivedFrom(ulong objectOwner, ulong sender, string methodName)
        {
            var receiver = objectOwner;
            if (!m_DistributedAuthority)
            {
                receiver = NetworkManager.ServerClientId;
            }
            VerifySentToId(objectOwner, sender, receiver, methodName, true);
        }

        public void VerifySentToNotAuthorityWithReceivedFrom(ulong objectOwner, ulong sender, string methodName)
        {
            var receiver = objectOwner;
            if (!m_DistributedAuthority)
            {
                receiver = NetworkManager.ServerClientId;
            }
            VerifySentToNotId(objectOwner, sender, receiver, methodName, true);
        }

        public void VerifySentToOwnerWithParams(ulong objectOwner, ulong sender, string methodName, int i, bool b, float f, string s)
        {
            VerifySentToIdWithParams(objectOwner, sender, objectOwner, methodName, i, b, f, s);
        }

        public void VerifySentToNotOwnerWithParams(ulong objectOwner, ulong sender, string methodName, int i, bool b, float f, string s)
        {
            VerifySentToNotIdWithParams(objectOwner, sender, objectOwner, methodName, i, b, f, s);
        }

        public void VerifySentToServerWithParams(ulong objectOwner, ulong sender, string methodName, int i, bool b, float f, string s)
        {
            VerifySentToIdWithParams(objectOwner, sender, NetworkManager.ServerClientId, methodName, i, b, f, s);
        }

        public void VerifySentToNotServerWithParams(ulong objectOwner, ulong sender, string methodName, int i, bool b, float f, string s)
        {
            VerifySentToNotIdWithParams(objectOwner, sender, NetworkManager.ServerClientId, methodName, i, b, f, s);
        }

        public void VerifySentToClientsAndHostWithParams(ulong objectOwner, ulong sender, string methodName, int i, bool b, float f, string s)
        {
            if (m_ServerNetworkManager.IsHost)
            {
                VerifySentToEveryoneWithParams(objectOwner, sender, methodName, i, b, f, s);
            }
            else
            {
                VerifySentToNotServerWithParams(objectOwner, sender, methodName, i, b, f, s);
            }
        }

        public void VerifySentToMeWithParams(ulong objectOwner, ulong sender, string methodName, int i, bool b, float f, string s)
        {
            VerifySentToIdWithParams(objectOwner, sender, sender, methodName, i, b, f, s);
        }

        public void VerifySentToNotMeWithParams(ulong objectOwner, ulong sender, string methodName, int i, bool b, float f, string s)
        {
            VerifySentToNotIdWithParams(objectOwner, sender, sender, methodName, i, b, f, s);
        }

        public void VerifySentToAuthorityWithParams(ulong objectOwner, ulong sender, string methodName, int i, bool b, float f, string s)
        {
            var receiver = objectOwner;
            if (!m_DistributedAuthority)
            {
                receiver = NetworkManager.ServerClientId;
            }
            VerifySentToIdWithParams(objectOwner, sender, receiver, methodName, i, b, f, s);
        }

        public void VerifySentToNotAuthorityWithParams(ulong objectOwner, ulong sender, string methodName, int i, bool b, float f, string s)
        {
            var receiver = objectOwner;
            if (!m_DistributedAuthority)
            {
                receiver = NetworkManager.ServerClientId;
            }
            VerifySentToNotIdWithParams(objectOwner, sender, receiver, methodName, i, b, f, s);
        }

        public void RethrowTargetInvocationException(Action action)
        {
            try
            {
                action.Invoke();
            }
            catch (TargetInvocationException e)
            {
                throw e.InnerException;
            }
        }
    }

    [TestFixture(HostOrServer.DAHost)]
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class UniversalRpcTestSendingNoOverride : UniversalRpcTestsBase
    {
        public UniversalRpcTestSendingNoOverride(HostOrServer hostOrServer) : base(hostOrServer)
        {

        }

        [Test]
        public void TestSendingNoOverride(
            // Excludes SendTo.SpecifiedInParams
            [Values(SendTo.Everyone, SendTo.Me, SendTo.Owner, SendTo.Server, SendTo.NotMe, SendTo.NotOwner, SendTo.NotServer,
            SendTo.ClientsAndHost, SendTo.Authority, SendTo.NotAuthority)] SendTo sendTo,
            [Values(0u, 1u, 2u)] ulong objectOwner,
            [Values(0u, 1u, 2u)] ulong sender
        )
        {
            var sendMethodName = $"DefaultTo{sendTo}Rpc";
            var verifyMethodName = $"VerifySentTo{sendTo}";

            var senderObject = GetPlayerObject(objectOwner, sender);
            var sendMethod = senderObject.GetType().GetMethod(sendMethodName);
            sendMethod.Invoke(senderObject, new object[] { });

            var verifyMethod = GetType().GetMethod(verifyMethodName);
            verifyMethod.Invoke(this, new object[] { objectOwner, sender, sendMethodName });
        }

    }

    [TestFixture(HostOrServer.DAHost)]
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class UniversalRpcTestSenderClientId : UniversalRpcTestsBase
    {
        public UniversalRpcTestSenderClientId(HostOrServer hostOrServer) : base(hostOrServer)
        {

        }

        [Test]
        public void TestSenderClientId(
            // Excludes SendTo.SpecifiedInParams
            [Values(SendTo.Everyone, SendTo.Me, SendTo.Owner, SendTo.Server, SendTo.NotMe, SendTo.NotOwner, SendTo.NotServer, SendTo.ClientsAndHost, SendTo.Authority, SendTo.NotAuthority)] SendTo sendTo,
            [Values(0u, 1u, 2u)] ulong objectOwner,
            [Values(0u, 1u, 2u)] ulong sender
        )
        {
            var sendMethodName = $"DefaultTo{sendTo}WithRpcParamsRpc";
            var verifyMethodName = $"VerifySentTo{sendTo}WithReceivedFrom";

            var senderObject = GetPlayerObject(objectOwner, sender);
            var sendMethod = senderObject.GetType().GetMethod(sendMethodName);
            sendMethod.Invoke(senderObject, new object[] { new RpcParams() });

            var verifyMethod = GetType().GetMethod(verifyMethodName);
            verifyMethod.Invoke(this, new object[] { objectOwner, sender, sendMethodName });
        }

    }

    [TestFixture(HostOrServer.DAHost)]
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class UniversalRpcTestSendingNoOverrideWithParams : UniversalRpcTestsBase
    {
        public UniversalRpcTestSendingNoOverrideWithParams(HostOrServer hostOrServer) : base(hostOrServer)
        {

        }

        [Test]
        public void TestSendingNoOverrideWithParams(
            // Excludes SendTo.SpecifiedInParams
            [Values(SendTo.Everyone, SendTo.Me, SendTo.Owner, SendTo.Server, SendTo.NotMe, SendTo.NotOwner, SendTo.NotServer, SendTo.ClientsAndHost, SendTo.Authority, SendTo.NotAuthority)] SendTo sendTo,
            [Values(0u, 1u, 2u)] ulong objectOwner,
            [Values(0u, 1u, 2u)] ulong sender
        )
        {
            var rand = new Random();
            var i = rand.Next();
            var f = (float)rand.NextDouble();
            var b = rand.Next() % 2 == 1;
            var s = "";
            var numChars = rand.Next() % 5 + 5;
            const string chars = "abcdefghijklmnopqrstuvwxycABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-=_+[]{}\\|;':\",./<>?";
            for (var j = 0; j < numChars; ++j)
            {
                s += chars[rand.Next(chars.Length)];
            }

            var sendMethodName = $"DefaultTo{sendTo}WithParamsRpc";
            var verifyMethodName = $"VerifySentTo{sendTo}WithParams";

            var senderObject = GetPlayerObject(objectOwner, sender);
            var sendMethod = senderObject.GetType().GetMethod(sendMethodName);
            sendMethod.Invoke(senderObject, new object[] { i, b, f, s });

            var verifyMethod = GetType().GetMethod(verifyMethodName);
            verifyMethod.Invoke(this, new object[] { objectOwner, sender, sendMethodName, i, b, f, s });
        }

    }

    [TestFixture(HostOrServer.DAHost)]
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class UniversalRpcTestSendingNoOverrideWithParamsAndRpcParams : UniversalRpcTestsBase
    {
        public UniversalRpcTestSendingNoOverrideWithParamsAndRpcParams(HostOrServer hostOrServer) : base(hostOrServer)
        {

        }

        [Test]
        public void TestSendingNoOverrideWithParamsAndRpcParams(
            // Excludes SendTo.SpecifiedInParams
            [Values(SendTo.Everyone, SendTo.Me, SendTo.Owner, SendTo.Server, SendTo.NotMe, SendTo.NotOwner, SendTo.NotServer, SendTo.ClientsAndHost, SendTo.Authority, SendTo.NotAuthority)] SendTo sendTo,
            [Values(0u, 1u, 2u)] ulong objectOwner,
            [Values(0u, 1u, 2u)] ulong sender
        )
        {
            var rand = new Random();
            var i = rand.Next();
            var f = (float)rand.NextDouble();
            var b = rand.Next() % 2 == 1;
            var s = "";
            var numChars = rand.Next() % 5 + 5;
            const string chars = "abcdefghijklmnopqrstuvwxycABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*()-=_+[]{}\\|;':\",./<>?";
            for (var j = 0; j < numChars; ++j)
            {
                s += chars[rand.Next(chars.Length)];
            }

            var sendMethodName = $"DefaultTo{sendTo}WithParamsAndRpcParamsRpc";
            var verifyMethodName = $"VerifySentTo{sendTo}WithParams";

            var senderObject = GetPlayerObject(objectOwner, sender);
            var sendMethod = senderObject.GetType().GetMethod(sendMethodName);
            sendMethod.Invoke(senderObject, new object[] { i, b, f, s, new RpcParams() });

            var verifyMethod = GetType().GetMethod(verifyMethodName);
            verifyMethod.Invoke(this, new object[] { objectOwner, sender, sendMethodName, i, b, f, s });
        }

    }


    [TestFixture(HostOrServer.DAHost)]
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class UniversalRpcTestRequireOwnership : UniversalRpcTestsBase
    {
        public UniversalRpcTestRequireOwnership(HostOrServer hostOrServer) : base(hostOrServer)
        {

        }

        [Test]
        public void TestRequireOwnership(
            // Excludes SendTo.SpecifiedInParams
            [Values(SendTo.Everyone, SendTo.Me, SendTo.Owner, SendTo.Server, SendTo.NotMe, SendTo.NotOwner, SendTo.NotServer, SendTo.ClientsAndHost, SendTo.Authority, SendTo.NotAuthority)] SendTo sendTo,
            [Values(0u, 1u, 2u)] ulong objectOwner,
            [Values(0u, 1u, 2u)] ulong sender
        )
        {
            var sendMethodName = $"DefaultTo{sendTo}RequireOwnershipRpc";

            var senderObject = GetPlayerObject(objectOwner, sender);
            var sendMethod = senderObject.GetType().GetMethod(sendMethodName);
            if (sender != objectOwner)
            {
                Assert.Throws<RpcException>(() => RethrowTargetInvocationException(() => sendMethod.Invoke(senderObject, new object[] { })));
            }
            else
            {
                var verifyMethodName = $"VerifySentTo{sendTo}";
                sendMethod.Invoke(senderObject, new object[] { });

                var verifyMethod = GetType().GetMethod(verifyMethodName);
                verifyMethod.Invoke(this, new object[] { objectOwner, sender, sendMethodName });
            }
        }
    }

    [TestFixture(HostOrServer.DAHost)]
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class UniversalRpcTestDisallowedOverride : UniversalRpcTestsBase
    {
        public UniversalRpcTestDisallowedOverride(HostOrServer hostOrServer) : base(hostOrServer)
        {

        }

        // Add both authority and nonauthority
        [Test]
        public void TestDisallowedOverride(
            // Excludes SendTo.SpecifiedInParams
            [Values(SendTo.Everyone, SendTo.Me, SendTo.Owner, SendTo.Server, SendTo.NotMe, SendTo.NotOwner, SendTo.NotServer, SendTo.ClientsAndHost, SendTo.Authority, SendTo.NotAuthority)] SendTo sendTo,
            [Values(0u, 1u, 2u)] ulong objectOwner,
            [Values(0u, 1u, 2u)] ulong sender)
        {
            var senderObject = GetPlayerObject(objectOwner, sender);
            var methodName = $"DefaultTo{sendTo}WithRpcParamsRpc";
            var method = senderObject.GetType().GetMethod(methodName);
            Assert.Throws<RpcException>(() => RethrowTargetInvocationException(() => method.Invoke(senderObject, new object[] { (RpcParams)senderObject.RpcTarget.Everyone })));
            Assert.Throws<RpcException>(() => RethrowTargetInvocationException(() => method.Invoke(senderObject, new object[] { (RpcParams)senderObject.RpcTarget.Owner })));
            Assert.Throws<RpcException>(() => RethrowTargetInvocationException(() => method.Invoke(senderObject, new object[] { (RpcParams)senderObject.RpcTarget.NotOwner })));
            Assert.Throws<RpcException>(() => RethrowTargetInvocationException(() => method.Invoke(senderObject, new object[] { (RpcParams)senderObject.RpcTarget.Server })));
            Assert.Throws<RpcException>(() => RethrowTargetInvocationException(() => method.Invoke(senderObject, new object[] { (RpcParams)senderObject.RpcTarget.NotServer })));
            Assert.Throws<RpcException>(() => RethrowTargetInvocationException(() => method.Invoke(senderObject, new object[] { (RpcParams)senderObject.RpcTarget.ClientsAndHost })));
            Assert.Throws<RpcException>(() => RethrowTargetInvocationException(() => method.Invoke(senderObject, new object[] { (RpcParams)senderObject.RpcTarget.Me })));
            Assert.Throws<RpcException>(() => RethrowTargetInvocationException(() => method.Invoke(senderObject, new object[] { (RpcParams)senderObject.RpcTarget.NotMe })));
            Assert.Throws<RpcException>(() => RethrowTargetInvocationException(() => method.Invoke(senderObject, new object[] { (RpcParams)senderObject.RpcTarget.Single(0, RpcTargetUse.Temp) })));
            Assert.Throws<RpcException>(() => RethrowTargetInvocationException(() => method.Invoke(senderObject, new object[] { (RpcParams)senderObject.RpcTarget.Not(0, RpcTargetUse.Temp) })));
            Assert.Throws<RpcException>(() => RethrowTargetInvocationException(() => method.Invoke(senderObject, new object[] { (RpcParams)senderObject.RpcTarget.Group(new[] { 0ul, 1ul, 2ul }, RpcTargetUse.Temp) })));
            Assert.Throws<RpcException>(() => RethrowTargetInvocationException(() => method.Invoke(senderObject, new object[] { (RpcParams)senderObject.RpcTarget.Not(new[] { 0ul, 1ul, 2ul }, RpcTargetUse.Temp) })));
        }

    }

    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class UniversalRpcTestSendingWithTargetOverride : UniversalRpcTestsBase
    {
        public UniversalRpcTestSendingWithTargetOverride(HostOrServer hostOrServer) : base(hostOrServer)
        {

        }

        // Look at the implementations and add both
        [Test]
        public void TestSendingWithTargetOverride(
            [Values] SendTo defaultSendTo,
            [Values(SendTo.Everyone, SendTo.Me, SendTo.Owner, SendTo.Server, SendTo.NotMe, SendTo.NotOwner, SendTo.NotServer, SendTo.ClientsAndHost)] SendTo overrideSendTo,
            [Values(0u, 1u, 2u)] ulong objectOwner,
            [Values(0u, 1u, 2u)] ulong sender
        )
        {
            var sendMethodName = $"DefaultTo{defaultSendTo}AllowOverrideRpc";
            var targetField = typeof(RpcTarget).GetField(overrideSendTo.ToString());
            var verifyMethodName = $"VerifySentTo{overrideSendTo}";

            var senderObject = GetPlayerObject(objectOwner, sender);
            var target = (BaseRpcTarget)targetField.GetValue(senderObject.RpcTarget);
            var sendMethod = senderObject.GetType().GetMethod(sendMethodName);
            sendMethod.Invoke(senderObject, new object[] { (RpcParams)target });

            var verifyMethod = GetType().GetMethod(verifyMethodName);
            verifyMethod.Invoke(this, new object[] { objectOwner, sender, sendMethodName });
        }


    }

    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class UniversalRpcTestSendingWithSingleOverride : UniversalRpcTestsBase
    {
        public UniversalRpcTestSendingWithSingleOverride(HostOrServer hostOrServer) : base(hostOrServer)
        {

        }

        [Test]
        public void TestSendingWithSingleOverride(
            [Values] SendTo defaultSendTo,
            [Values(0u, 1u, 2u)] ulong recipient,
            [Values(0u, 1u, 2u)] ulong objectOwner,
            [Values(0u, 1u, 2u)] ulong sender
        )
        {
            var sendMethodName = $"DefaultTo{defaultSendTo}AllowOverrideRpc";

            var senderObject = GetPlayerObject(objectOwner, sender);
            var target = senderObject.RpcTarget.Single(recipient, RpcTargetUse.Temp);
            var sendMethod = senderObject.GetType().GetMethod(sendMethodName);
            sendMethod.Invoke(senderObject, new object[] { (RpcParams)target });

            VerifyRemoteReceived(objectOwner, sender, sendMethodName, new[] { recipient }, false);
            VerifyNotReceived(objectOwner, s_ClientIds.Where(c => recipient != c).ToArray());

            // Pass some time to make sure that no other client ever receives this
            TimeTravel(1f, 30);
            VerifyNotReceived(objectOwner, s_ClientIds.Where(c => recipient != c).ToArray());
        }

    }

    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class UniversalRpcTestSendingWithSingleNotOverride : UniversalRpcTestsBase
    {
        public UniversalRpcTestSendingWithSingleNotOverride(HostOrServer hostOrServer) : base(hostOrServer)
        {

        }

        [Test]
        public void TestSendingWithSingleNotOverride(
            [Values] SendTo defaultSendTo,
            [Values(0u, 1u, 2u)] ulong recipient,
            [Values(0u, 1u, 2u)] ulong objectOwner,
            [Values(0u, 1u, 2u)] ulong sender
        )
        {
            var sendMethodName = $"DefaultTo{defaultSendTo}AllowOverrideRpc";

            var senderObject = GetPlayerObject(objectOwner, sender);
            var target = senderObject.RpcTarget.Not(recipient, RpcTargetUse.Temp);
            var sendMethod = senderObject.GetType().GetMethod(sendMethodName);
            sendMethod.Invoke(senderObject, new object[] { (RpcParams)target });

            VerifyRemoteReceived(objectOwner, sender, sendMethodName, s_ClientIds.Where(c => recipient != c).ToArray(), false);
            VerifyNotReceived(objectOwner, new[] { recipient });

            // Pass some time to make sure that no other client ever receives this
            TimeTravel(1f, 30);
            VerifyNotReceived(objectOwner, new[] { recipient });
        }

    }

    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class UniversalRpcTestSendingWithGroupOverride : UniversalRpcTestsBase
    {
        public UniversalRpcTestSendingWithGroupOverride(HostOrServer hostOrServer) : base(hostOrServer)
        {

        }

        public static ulong[][] RecipientGroups = new[]
        {
            new[] { 0ul },
            new[] { 1ul },
            new[] { 0ul, 1ul },
            new[] { 0ul, 1ul, 2ul }
        };

        public enum AllocationType
        {
            Array,
            NativeArray,
            NativeList,
            List
        }

        [Test]
        public void TestSendingWithGroupOverride(
            [Values] SendTo defaultSendTo,
            [ValueSource(nameof(RecipientGroups))] ulong[] recipient,
            [Values(0u, 1u, 2u)] ulong objectOwner,
            [Values(0u, 1u, 2u)] ulong sender,
            [Values] AllocationType allocationType
        )
        {
            var sendMethodName = $"DefaultTo{defaultSendTo}AllowOverrideRpc";

            var senderObject = GetPlayerObject(objectOwner, sender);
            BaseRpcTarget target = null;
            switch (allocationType)
            {
                case AllocationType.Array:
                    target = senderObject.RpcTarget.Group(recipient, RpcTargetUse.Temp);
                    break;
                case AllocationType.List:
                    target = senderObject.RpcTarget.Group(recipient.ToList(), RpcTargetUse.Temp);
                    break;
                case AllocationType.NativeArray:
                    var arr = new NativeArray<ulong>(recipient, Allocator.Temp);
                    target = senderObject.RpcTarget.Group(arr, RpcTargetUse.Temp);
                    arr.Dispose();
                    break;
                case AllocationType.NativeList:
                    // For some reason on 2020.3, calling list.AsArray() and passing that to the next function
                    // causes Allocator.Temp allocations to become invalid somehow. This is not an issue on later
                    // versions of Unity.
                    var list = new NativeList<ulong>(recipient.Length, Allocator.TempJob);
                    foreach (var id in recipient)
                    {
                        list.Add(id);
                    }
                    target = senderObject.RpcTarget.Group(list, RpcTargetUse.Temp);
                    list.Dispose();
                    break;
            }
            var sendMethod = senderObject.GetType().GetMethod(sendMethodName);
            sendMethod.Invoke(senderObject, new object[] { (RpcParams)target });

            VerifyRemoteReceived(objectOwner, sender, sendMethodName, s_ClientIds.Where(c => recipient.Contains(c)).ToArray(), false);
            VerifyNotReceived(objectOwner, s_ClientIds.Where(c => !recipient.Contains(c)).ToArray());

            // Pass some time to make sure that no other client ever receives this
            TimeTravel(1f, 30);
            VerifyNotReceived(objectOwner, s_ClientIds.Where(c => !recipient.Contains(c)).ToArray());
        }

    }

    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class UniversalRpcTestSendingWithGroupNotOverride : UniversalRpcTestsBase
    {
        public UniversalRpcTestSendingWithGroupNotOverride(HostOrServer hostOrServer) : base(hostOrServer)
        {

        }

        public static ulong[][] RecipientGroups = new[]
        {
            new ulong[] {},
            new[] { 0ul },
            new[] { 1ul },
            new[] { 0ul, 1ul },
        };

        public enum AllocationType
        {
            Array,
            NativeArray,
            NativeList,
            List
        }

        [Test]
        public void TestSendingWithGroupNotOverride(
            [Values] SendTo defaultSendTo,
            [ValueSource(nameof(RecipientGroups))] ulong[] recipient,
            [Values(0u, 1u, 2u)] ulong objectOwner,
            [Values(0u, 1u, 2u)] ulong sender,
            [Values] AllocationType allocationType
        )
        {
            var sendMethodName = $"DefaultTo{defaultSendTo}AllowOverrideRpc";

            var senderObject = GetPlayerObject(objectOwner, sender);
            BaseRpcTarget target = null;
            switch (allocationType)
            {
                case AllocationType.Array:
                    target = senderObject.RpcTarget.Not(recipient, RpcTargetUse.Temp);
                    break;
                case AllocationType.List:
                    target = senderObject.RpcTarget.Not(recipient.ToList(), RpcTargetUse.Temp);
                    break;
                case AllocationType.NativeArray:
                    var arr = new NativeArray<ulong>(recipient, Allocator.Temp);
                    target = senderObject.RpcTarget.Not(arr, RpcTargetUse.Temp);
                    arr.Dispose();
                    break;
                case AllocationType.NativeList:
                    // For some reason on 2020.3, calling list.AsArray() and passing that to the next function
                    // causes Allocator.Temp allocations to become invalid somehow. This is not an issue on later
                    // versions of Unity.
                    var list = new NativeList<ulong>(recipient.Length, Allocator.TempJob);
                    foreach (var id in recipient)
                    {
                        list.Add(id);
                    }
                    target = senderObject.RpcTarget.Not(list, RpcTargetUse.Temp);
                    list.Dispose();
                    break;
            }
            var sendMethod = senderObject.GetType().GetMethod(sendMethodName);
            sendMethod.Invoke(senderObject, new object[] { (RpcParams)target });

            VerifyRemoteReceived(objectOwner, sender, sendMethodName, s_ClientIds.Where(c => !recipient.Contains(c)).ToArray(), false);
            VerifyNotReceived(objectOwner, s_ClientIds.Where(c => recipient.Contains(c)).ToArray());

            // Pass some time to make sure that no other client ever receives this
            TimeTravel(1f, 30);
            VerifyNotReceived(objectOwner, s_ClientIds.Where(c => recipient.Contains(c)).ToArray());
        }

    }

    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class UniversalRpcTestDefaultSendToSpecifiedInParamsSendingToServerAndOwner : UniversalRpcTestsBase
    {
        public UniversalRpcTestDefaultSendToSpecifiedInParamsSendingToServerAndOwner(HostOrServer hostOrServer) : base(hostOrServer)
        {

        }
    }

    [TestFixture(HostOrServer.DAHost)]
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class UniversalRpcTestDeferLocal : UniversalRpcTestsBase
    {
        public UniversalRpcTestDeferLocal(HostOrServer hostOrServer) : base(hostOrServer)
        {

        }

        [Test]
        // All the test cases that involve sends that will be delivered locally
        [TestCase(SendTo.Everyone, 0u, 0u)]
        [TestCase(SendTo.Everyone, 0u, 1u)]
        [TestCase(SendTo.Everyone, 0u, 2u)]
        [TestCase(SendTo.Everyone, 1u, 0u)]
        [TestCase(SendTo.Everyone, 1u, 1u)]
        [TestCase(SendTo.Everyone, 1u, 2u)]
        [TestCase(SendTo.Everyone, 2u, 0u)]
        [TestCase(SendTo.Everyone, 2u, 1u)]
        [TestCase(SendTo.Everyone, 2u, 2u)]
        [TestCase(SendTo.Me, 0u, 0u)]
        [TestCase(SendTo.Me, 0u, 1u)]
        [TestCase(SendTo.Me, 0u, 2u)]
        [TestCase(SendTo.Me, 1u, 0u)]
        [TestCase(SendTo.Me, 1u, 1u)]
        [TestCase(SendTo.Me, 1u, 2u)]
        [TestCase(SendTo.Me, 2u, 0u)]
        [TestCase(SendTo.Me, 2u, 1u)]
        [TestCase(SendTo.Me, 2u, 2u)]
        [TestCase(SendTo.Owner, 0u, 0u)]
        [TestCase(SendTo.Owner, 1u, 1u)]
        [TestCase(SendTo.Owner, 2u, 2u)]
        [TestCase(SendTo.Server, 0u, 0u)]
        [TestCase(SendTo.Server, 1u, 0u)]
        [TestCase(SendTo.Server, 2u, 0u)]
        [TestCase(SendTo.NotOwner, 0u, 1u)]
        [TestCase(SendTo.NotOwner, 0u, 2u)]
        [TestCase(SendTo.NotOwner, 1u, 0u)]
        [TestCase(SendTo.NotOwner, 1u, 2u)]
        [TestCase(SendTo.NotOwner, 2u, 0u)]
        [TestCase(SendTo.NotOwner, 2u, 1u)]
        [TestCase(SendTo.NotServer, 0u, 1u)]
        [TestCase(SendTo.NotServer, 0u, 2u)]
        [TestCase(SendTo.NotServer, 1u, 1u)]
        [TestCase(SendTo.NotServer, 1u, 2u)]
        [TestCase(SendTo.NotServer, 2u, 1u)]
        [TestCase(SendTo.NotServer, 2u, 2u)]
        [TestCase(SendTo.ClientsAndHost, 0u, 0u)]
        [TestCase(SendTo.ClientsAndHost, 0u, 1u)]
        [TestCase(SendTo.ClientsAndHost, 0u, 2u)]
        [TestCase(SendTo.ClientsAndHost, 1u, 0u)]
        [TestCase(SendTo.ClientsAndHost, 1u, 1u)]
        [TestCase(SendTo.ClientsAndHost, 1u, 2u)]
        [TestCase(SendTo.ClientsAndHost, 2u, 0u)]
        [TestCase(SendTo.ClientsAndHost, 2u, 1u)]
        [TestCase(SendTo.ClientsAndHost, 2u, 2u)]
        [TestCase(SendTo.Authority, 0u, 0u)]
        [TestCase(SendTo.Authority, 1u, 1u)]
        [TestCase(SendTo.Authority, 2u, 2u)]
        [TestCase(SendTo.NotAuthority, 0u, 1u)]
        [TestCase(SendTo.NotAuthority, 0u, 2u)]
        [TestCase(SendTo.NotAuthority, 1u, 0u)]
        [TestCase(SendTo.NotAuthority, 1u, 2u)]
        [TestCase(SendTo.NotAuthority, 2u, 0u)]
        [TestCase(SendTo.NotAuthority, 2u, 1u)]
        public void TestDeferLocal(
            SendTo defaultSendTo,
            ulong objectOwner,
            ulong sender
        )
        {
            if (defaultSendTo == SendTo.ClientsAndHost && sender == 0u && !m_ServerNetworkManager.IsHost)
            {
                // Not calling Assert.Ignore() because Unity will mark the whole block of tests as ignored
                // Just consider this case a success...
                return;
            }

            // Similar to above, since Server and NotServer are already tested we can consider this a success
            if (!m_DistributedAuthority && defaultSendTo == SendTo.Authority || defaultSendTo == SendTo.NotAuthority)
            {
                return;
            }

            var sendMethodName = $"DefaultTo{defaultSendTo}DeferLocalRpc";
            var verifyMethodName = $"VerifySentTo{defaultSendTo}";
            var senderObject = GetPlayerObject(objectOwner, sender);
            var sendMethod = senderObject.GetType().GetMethod(sendMethodName);
            sendMethod.Invoke(senderObject, new object[] { new RpcParams() });

            VerifyNotReceived(objectOwner, new[] { sender });
            // Should be received on the next frame
            SimulateOneFrame();
            VerifyLocalReceived(objectOwner, sender, sendMethodName, false);

            var verifyMethod = GetType().GetMethod(verifyMethodName);
            verifyMethod.Invoke(this, new object[] { objectOwner, sender, sendMethodName });
        }

        [Test]
        // All the test cases that involve sends that will be delivered locally
        [TestCase(SendTo.Everyone, 0u, 0u)]
        [TestCase(SendTo.Everyone, 0u, 1u)]
        [TestCase(SendTo.Everyone, 0u, 2u)]
        [TestCase(SendTo.Everyone, 1u, 0u)]
        [TestCase(SendTo.Everyone, 1u, 1u)]
        [TestCase(SendTo.Everyone, 1u, 2u)]
        [TestCase(SendTo.Everyone, 2u, 0u)]
        [TestCase(SendTo.Everyone, 2u, 1u)]
        [TestCase(SendTo.Everyone, 2u, 2u)]
        [TestCase(SendTo.Me, 0u, 0u)]
        [TestCase(SendTo.Me, 0u, 1u)]
        [TestCase(SendTo.Me, 0u, 2u)]
        [TestCase(SendTo.Me, 1u, 0u)]
        [TestCase(SendTo.Me, 1u, 1u)]
        [TestCase(SendTo.Me, 1u, 2u)]
        [TestCase(SendTo.Me, 2u, 0u)]
        [TestCase(SendTo.Me, 2u, 1u)]
        [TestCase(SendTo.Me, 2u, 2u)]
        [TestCase(SendTo.Owner, 0u, 0u)]
        [TestCase(SendTo.Owner, 1u, 1u)]
        [TestCase(SendTo.Owner, 2u, 2u)]
        [TestCase(SendTo.Server, 0u, 0u)]
        [TestCase(SendTo.Server, 1u, 0u)]
        [TestCase(SendTo.Server, 2u, 0u)]
        [TestCase(SendTo.NotOwner, 0u, 1u)]
        [TestCase(SendTo.NotOwner, 0u, 2u)]
        [TestCase(SendTo.NotOwner, 1u, 0u)]
        [TestCase(SendTo.NotOwner, 1u, 2u)]
        [TestCase(SendTo.NotOwner, 2u, 0u)]
        [TestCase(SendTo.NotOwner, 2u, 1u)]
        [TestCase(SendTo.NotServer, 0u, 1u)]
        [TestCase(SendTo.NotServer, 0u, 2u)]
        [TestCase(SendTo.NotServer, 1u, 1u)]
        [TestCase(SendTo.NotServer, 1u, 2u)]
        [TestCase(SendTo.NotServer, 2u, 1u)]
        [TestCase(SendTo.NotServer, 2u, 2u)]
        [TestCase(SendTo.ClientsAndHost, 0u, 0u)]
        [TestCase(SendTo.ClientsAndHost, 0u, 1u)]
        [TestCase(SendTo.ClientsAndHost, 0u, 2u)]
        [TestCase(SendTo.ClientsAndHost, 1u, 0u)]
        [TestCase(SendTo.ClientsAndHost, 1u, 1u)]
        [TestCase(SendTo.ClientsAndHost, 1u, 2u)]
        [TestCase(SendTo.ClientsAndHost, 2u, 0u)]
        [TestCase(SendTo.ClientsAndHost, 2u, 1u)]
        [TestCase(SendTo.ClientsAndHost, 2u, 2u)]
        [TestCase(SendTo.Authority, 0u, 0u)]
        [TestCase(SendTo.Authority, 1u, 1u)]
        [TestCase(SendTo.Authority, 2u, 2u)]
        [TestCase(SendTo.NotAuthority, 0u, 1u)]
        [TestCase(SendTo.NotAuthority, 0u, 2u)]
        [TestCase(SendTo.NotAuthority, 1u, 0u)]
        [TestCase(SendTo.NotAuthority, 1u, 2u)]
        [TestCase(SendTo.NotAuthority, 2u, 0u)]
        [TestCase(SendTo.NotAuthority, 2u, 1u)]
        public void TestDeferLocalOverrideToTrue(
            SendTo defaultSendTo,
            ulong objectOwner,
            ulong sender
        )
        {
            if (defaultSendTo == SendTo.ClientsAndHost && sender == 0u && !m_ServerNetworkManager.IsHost)
            {
                // Not calling Assert.Ignore() because Unity will mark the whole block of tests as ignored
                // Just consider this case a success...
                return;
            }
            // Similar to above, since Server and NotServer are already tested we can consider this a success
            if (!m_DistributedAuthority && defaultSendTo == SendTo.Authority || defaultSendTo == SendTo.NotAuthority)
            {
                return;
            }

            var sendMethodName = $"DefaultTo{defaultSendTo}WithRpcParamsRpc";
            var verifyMethodName = $"VerifySentTo{defaultSendTo}";
            var senderObject = GetPlayerObject(objectOwner, sender);
            var sendMethod = senderObject.GetType().GetMethod(sendMethodName);
            sendMethod.Invoke(senderObject, new object[] { (RpcParams)LocalDeferMode.Defer });

            VerifyNotReceived(objectOwner, new[] { sender });
            // Should be received on the next frame
            SimulateOneFrame();
            VerifyLocalReceived(objectOwner, sender, sendMethodName, false);

            var verifyMethod = GetType().GetMethod(verifyMethodName);
            verifyMethod.Invoke(this, new object[] { objectOwner, sender, sendMethodName });
        }

        [Test]
        // All the test cases that involve sends that will be delivered locally
        [TestCase(SendTo.Everyone, 0u, 0u)]
        [TestCase(SendTo.Everyone, 0u, 1u)]
        [TestCase(SendTo.Everyone, 0u, 2u)]
        [TestCase(SendTo.Everyone, 1u, 0u)]
        [TestCase(SendTo.Everyone, 1u, 1u)]
        [TestCase(SendTo.Everyone, 1u, 2u)]
        [TestCase(SendTo.Everyone, 2u, 0u)]
        [TestCase(SendTo.Everyone, 2u, 1u)]
        [TestCase(SendTo.Everyone, 2u, 2u)]
        [TestCase(SendTo.Me, 0u, 0u)]
        [TestCase(SendTo.Me, 0u, 1u)]
        [TestCase(SendTo.Me, 0u, 2u)]
        [TestCase(SendTo.Me, 1u, 0u)]
        [TestCase(SendTo.Me, 1u, 1u)]
        [TestCase(SendTo.Me, 1u, 2u)]
        [TestCase(SendTo.Me, 2u, 0u)]
        [TestCase(SendTo.Me, 2u, 1u)]
        [TestCase(SendTo.Me, 2u, 2u)]
        [TestCase(SendTo.Owner, 0u, 0u)]
        [TestCase(SendTo.Owner, 1u, 1u)]
        [TestCase(SendTo.Owner, 2u, 2u)]
        [TestCase(SendTo.Server, 0u, 0u)]
        [TestCase(SendTo.Server, 1u, 0u)]
        [TestCase(SendTo.Server, 2u, 0u)]
        [TestCase(SendTo.NotOwner, 0u, 1u)]
        [TestCase(SendTo.NotOwner, 0u, 2u)]
        [TestCase(SendTo.NotOwner, 1u, 0u)]
        [TestCase(SendTo.NotOwner, 1u, 2u)]
        [TestCase(SendTo.NotOwner, 2u, 0u)]
        [TestCase(SendTo.NotOwner, 2u, 1u)]
        [TestCase(SendTo.NotServer, 0u, 1u)]
        [TestCase(SendTo.NotServer, 0u, 2u)]
        [TestCase(SendTo.NotServer, 1u, 1u)]
        [TestCase(SendTo.NotServer, 1u, 2u)]
        [TestCase(SendTo.NotServer, 2u, 1u)]
        [TestCase(SendTo.NotServer, 2u, 2u)]
        [TestCase(SendTo.ClientsAndHost, 0u, 0u)]
        [TestCase(SendTo.ClientsAndHost, 0u, 1u)]
        [TestCase(SendTo.ClientsAndHost, 0u, 2u)]
        [TestCase(SendTo.ClientsAndHost, 1u, 0u)]
        [TestCase(SendTo.ClientsAndHost, 1u, 1u)]
        [TestCase(SendTo.ClientsAndHost, 1u, 2u)]
        [TestCase(SendTo.ClientsAndHost, 2u, 0u)]
        [TestCase(SendTo.ClientsAndHost, 2u, 1u)]
        [TestCase(SendTo.ClientsAndHost, 2u, 2u)]
        [TestCase(SendTo.Authority, 0u, 0u)]
        [TestCase(SendTo.Authority, 1u, 1u)]
        [TestCase(SendTo.Authority, 2u, 2u)]
        [TestCase(SendTo.NotAuthority, 0u, 1u)]
        [TestCase(SendTo.NotAuthority, 0u, 2u)]
        [TestCase(SendTo.NotAuthority, 1u, 0u)]
        [TestCase(SendTo.NotAuthority, 1u, 2u)]
        [TestCase(SendTo.NotAuthority, 2u, 0u)]
        [TestCase(SendTo.NotAuthority, 2u, 1u)]
        public void TestDeferLocalOverrideToFalse(
            SendTo defaultSendTo,
            ulong objectOwner,
            ulong sender
        )
        {
            if (defaultSendTo == SendTo.ClientsAndHost && sender == 0u && !m_ServerNetworkManager.IsHost)
            {
                // Not calling Assert.Ignore() because Unity will mark the whole block of tests as ignored
                // Just consider this case a success...
                return;
            }
            // Similar to above, since Server and NotServer are already tested we can consider this a success
            if (!m_DistributedAuthority && defaultSendTo == SendTo.Authority || defaultSendTo == SendTo.NotAuthority)
            {
                return;
            }

            var sendMethodName = $"DefaultTo{defaultSendTo}DeferLocalRpc";
            var verifyMethodName = $"VerifySentTo{defaultSendTo}";
            var senderObject = GetPlayerObject(objectOwner, sender);
            var sendMethod = senderObject.GetType().GetMethod(sendMethodName);
            sendMethod.Invoke(senderObject, new object[] { (RpcParams)LocalDeferMode.SendImmediate });

            VerifyLocalReceived(objectOwner, sender, sendMethodName, false);

            var verifyMethod = GetType().GetMethod(verifyMethodName);
            verifyMethod.Invoke(this, new object[] { objectOwner, sender, sendMethodName });
        }

    }

    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class UniversalRpcTestMutualRecursion : UniversalRpcTestsBase
    {
        public UniversalRpcTestMutualRecursion(HostOrServer hostOrServer) : base(hostOrServer)
        {

        }

        [Test]
        public void TestMutualRecursion()
        {
            var serverObj = GetPlayerObject(NetworkManager.ServerClientId, NetworkManager.ServerClientId);

            serverObj.MutualRecursionClientRpc();

            var serverIdArray = new[] { NetworkManager.ServerClientId };
            var clientIdArray = s_ClientIds.Where(c => c != NetworkManager.ServerClientId).ToArray();

            var clientList = m_ClientNetworkManagers.ToList();
            var serverList = new List<NetworkManager> { m_ServerNetworkManager };

            VerifyNotReceived(NetworkManager.ServerClientId, s_ClientIds);

            var clientListExpected = 1;
            var serverListExpected = 2;
            for (var i = 1; i <= 10; ++i)
            {
                WaitForMessageReceivedWithTimeTravel<RpcMessage>(clientList);
                VerifyRemoteReceived(NetworkManager.ServerClientId, NetworkManager.ServerClientId, nameof(UniversalRpcNetworkBehaviour.MutualRecursionClientRpc), clientIdArray, false, false, clientListExpected);
                VerifyNotReceived(NetworkManager.ServerClientId, serverIdArray);
                clientListExpected *= 2;

                Clear();
                WaitForMessageReceivedWithTimeTravel<RpcMessage>(serverList);
                VerifyRemoteReceived(NetworkManager.ServerClientId, NetworkManager.ServerClientId, nameof(UniversalRpcNetworkBehaviour.MutualRecursionServerRpc), serverIdArray, false, false, serverListExpected);
                VerifyNotReceived(NetworkManager.ServerClientId, clientIdArray);
                serverListExpected *= 2;

                Clear();
            }
            serverObj.Stop = true;
            WaitForMessageReceivedWithTimeTravel<RpcMessage>(serverList);
            Assert.IsFalse(serverObj.Stop);
        }


    }

    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    internal class UniversalRpcTestSelfRecursion : UniversalRpcTestsBase
    {
        public UniversalRpcTestSelfRecursion(HostOrServer hostOrServer) : base(hostOrServer)
        {

        }

        [Test]
        public void TestSelfRecursion()
        {
            var serverObj = GetPlayerObject(NetworkManager.ServerClientId, NetworkManager.ServerClientId);

            serverObj.SelfRecursiveRpc();

            var serverIdArray = new[] { NetworkManager.ServerClientId };
            var clientIdArray = s_ClientIds.Where(c => c != NetworkManager.ServerClientId).ToArray();

            var clientList = m_ClientNetworkManagers.ToList();
            var serverList = new List<NetworkManager> { m_ServerNetworkManager };

            for (var i = 0; i < 10; ++i)
            {
                VerifyNotReceived(NetworkManager.ServerClientId, s_ClientIds);
                SimulateOneFrame();
                VerifyLocalReceived(NetworkManager.ServerClientId, NetworkManager.ServerClientId, nameof(UniversalRpcNetworkBehaviour.SelfRecursiveRpc), false);

                Clear();
            }

            serverObj.Stop = true;
            SimulateOneFrame();
            Assert.IsFalse(serverObj.Stop);
            VerifyNotReceived(NetworkManager.ServerClientId, s_ClientIds);
        }

    }

    [TestFixture(ObjType.Server)]
    [TestFixture(ObjType.Client)]
    internal class UniversalRpcTestRpcTargetUse : UniversalRpcTestsBase
    {
        private ObjType m_ObjType;

        public UniversalRpcTestRpcTargetUse(ObjType objType) : base(HostOrServer.Server)
        {
            m_ObjType = objType;
        }

        public enum AllocationType
        {
            Array,
            NativeArray,
            NativeList,
            List
        }
        private BaseRpcTarget GetGroup(NetworkBehaviour senderObject, ulong[] recipient, AllocationType allocationType, RpcTargetUse use)
        {
            switch (allocationType)
            {
                case AllocationType.Array:
                    return senderObject.RpcTarget.Group(recipient, use);
                case AllocationType.List:
                    return senderObject.RpcTarget.Group(recipient.ToList(), use);
                case AllocationType.NativeArray:
                    var arr = new NativeArray<ulong>(recipient, Allocator.Temp);
                    var naTarget = senderObject.RpcTarget.Group(arr, use);
                    arr.Dispose();
                    return naTarget;
                case AllocationType.NativeList:
                    // For some reason on 2020.3, calling list.AsArray() and passing that to the next function
                    // causes Allocator.Temp allocations to become invalid somehow. This is not an issue on later
                    // versions of Unity.
                    var list = new NativeList<ulong>(recipient.Length, Allocator.TempJob);
                    foreach (var id in recipient)
                    {
                        list.Add(id);
                    }
                    var nlTarget = senderObject.RpcTarget.Group(list, use);
                    list.Dispose();
                    return nlTarget;
            }

            return null;
        }
        private BaseRpcTarget GetNot(NetworkBehaviour senderObject, ulong[] recipient, AllocationType allocationType, RpcTargetUse use)
        {
            switch (allocationType)
            {
                case AllocationType.Array:
                    return senderObject.RpcTarget.Not(recipient, use);
                case AllocationType.List:
                    return senderObject.RpcTarget.Not(recipient.ToList(), use);
                case AllocationType.NativeArray:
                    var arr = new NativeArray<ulong>(recipient, Allocator.Temp);
                    var naTarget = senderObject.RpcTarget.Not(arr, use);
                    arr.Dispose();
                    return naTarget;
                case AllocationType.NativeList:
                    // For some reason on 2020.3, calling list.AsArray() and passing that to the next function
                    // causes Allocator.Temp allocations to become invalid somehow. This is not an issue on later
                    // versions of Unity.
                    var list = new NativeList<ulong>(recipient.Length, Allocator.TempJob);
                    foreach (var id in recipient)
                    {
                        list.Add(id);
                    }
                    var nlTarget = senderObject.RpcTarget.Not(list, use);
                    list.Dispose();
                    return nlTarget;
            }

            return null;
        }

        public enum ObjType
        {
            Client,
            Server
        }

        private NetworkBehaviour m_Obj;

        protected override void OnTimeTravelServerAndClientsConnected()
        {
            base.OnTimeTravelServerAndClientsConnected();

            if (m_ObjType == ObjType.Server)
            {
                m_Obj = GetPlayerObject(NetworkManager.ServerClientId, NetworkManager.ServerClientId);
            }
            else
            {
                m_Obj = GetPlayerObject(1, 1);
            }
        }

        [Test]
        public void TestRpcTargetUseGroup([Values] AllocationType allocationType)
        {
            var group1 = GetGroup(m_Obj, new[] { 1ul, 2ul }, allocationType, RpcTargetUse.Temp);
            var group2 = GetGroup(m_Obj, new[] { 2ul, 3ul }, allocationType, RpcTargetUse.Temp);
            var group3 = GetGroup(m_Obj, new[] { 1ul, 2ul }, allocationType, RpcTargetUse.Persistent);
            var group4 = GetGroup(m_Obj, new[] { 2ul, 3ul }, allocationType, RpcTargetUse.Persistent);

            Assert.AreSame(group1, group2);
            Assert.AreNotSame(group1, group3);
            Assert.AreNotSame(group1, group4);
            Assert.AreNotSame(group2, group3);
            Assert.AreNotSame(group2, group4);
            Assert.AreNotSame(group3, group4);

            Assert.Throws<Exception>(() =>
            {
                group1.Dispose();
            });

            Assert.Throws<Exception>(() =>
            {
                group2.Dispose();
            });

            group3.Dispose();
            group4.Dispose();
        }

        [Test]
        public void TestRpcTargetUseNotGroup([Values] AllocationType allocationType)
        {
            var not1 = GetNot(m_Obj, new[] { 1ul, 2ul }, allocationType, RpcTargetUse.Temp);
            var not2 = GetNot(m_Obj, new[] { 2ul, 3ul }, allocationType, RpcTargetUse.Temp);
            var not3 = GetNot(m_Obj, new[] { 1ul, 2ul }, allocationType, RpcTargetUse.Persistent);
            var not4 = GetNot(m_Obj, new[] { 2ul, 3ul }, allocationType, RpcTargetUse.Persistent);

            Assert.AreSame(not1, not2);
            Assert.AreNotSame(not1, not3);
            Assert.AreNotSame(not1, not4);
            Assert.AreNotSame(not2, not3);
            Assert.AreNotSame(not2, not4);
            Assert.AreNotSame(not3, not4);

            Assert.Throws<Exception>(() =>
            {
                not1.Dispose();
            });

            Assert.Throws<Exception>(() =>
            {
                not2.Dispose();
            });

            not3.Dispose();
            not4.Dispose();
        }

        [Test]
        public void TestRpcTargetUseSingle()
        {
            // Not using 1 here because 1 is a special case that returns a LocalSendTarget for the client
            // because the client versin of this test uses m_Obj from client ID 1 (ergo 1 is localhost in this test).
            // So 1 will always be different from 2 and we want to verify the first two are the same.
            var single1 = m_Obj.RpcTarget.Single(2ul, RpcTargetUse.Temp);
            var single2 = m_Obj.RpcTarget.Single(3ul, RpcTargetUse.Temp);
            var single3 = m_Obj.RpcTarget.Single(2ul, RpcTargetUse.Persistent);
            var single4 = m_Obj.RpcTarget.Single(3ul, RpcTargetUse.Persistent);
            Assert.AreSame(single1, single2);
            Assert.AreNotSame(single1, single3);
            Assert.AreNotSame(single1, single4);
            Assert.AreNotSame(single2, single3);
            Assert.AreNotSame(single2, single4);
            Assert.AreNotSame(single3, single4);

            Assert.Throws<Exception>(() =>
            {
                single1.Dispose();
            });

            Assert.Throws<Exception>(() =>
            {
                single2.Dispose();
            });

            single3.Dispose();
            single4.Dispose();
        }

        [Test]
        public void TestRpcTargetUseNotSingle()
        {
            var singleNot1 = m_Obj.RpcTarget.Not(1ul, RpcTargetUse.Temp);
            var singleNot2 = m_Obj.RpcTarget.Not(2ul, RpcTargetUse.Temp);
            var singleNot3 = m_Obj.RpcTarget.Not(1ul, RpcTargetUse.Persistent);
            var singleNot4 = m_Obj.RpcTarget.Not(2ul, RpcTargetUse.Persistent);
            Assert.AreSame(singleNot1, singleNot2);
            Assert.AreNotSame(singleNot1, singleNot3);
            Assert.AreNotSame(singleNot1, singleNot4);
            Assert.AreNotSame(singleNot2, singleNot3);
            Assert.AreNotSame(singleNot2, singleNot4);
            Assert.AreNotSame(singleNot3, singleNot4);

            Assert.Throws<Exception>(() =>
            {
                singleNot1.Dispose();
            });

            Assert.Throws<Exception>(() =>
            {
                singleNot2.Dispose();
            });

            singleNot3.Dispose();
            singleNot4.Dispose();
        }

    }
}
#endif
