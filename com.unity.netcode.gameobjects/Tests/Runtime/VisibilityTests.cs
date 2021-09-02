using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace Unity.Netcode.RuntimeTests
{
    public class VisibilityComponent : NetworkBehaviour
    {
        public readonly NetworkVariable<int> SomeVar = new NetworkVariable<int>();

        public void Awake()
        {
        }
    }

    public class VisibilityTests : BaseMultiInstanceTest
    {
        protected override int NbClients => 2;

        // Player1 component on the server
        private VisibilityComponent m_Player1OnServer;

        // Player2 component on the server
        private VisibilityComponent m_Player2OnServer;

        // Player1 component on client1
        private VisibilityComponent m_Player1OnClient1;

        // Player2 component on client1
        private VisibilityComponent m_Player2OnClient2;

        // client2's version of client1's player object
        private VisibilityComponent m_Player1OnClient2;

        private bool m_TestWithHost;

        [UnitySetUp]
        public override IEnumerator Setup()
        {
            yield return StartSomeClientsAndServerWithPlayers(useHost: m_TestWithHost, nbClients: NbClients,
                updatePlayerPrefab: playerPrefab =>
                {
                    var networkTransform = playerPrefab.AddComponent<VisibilityComponent>(); //??
                });

            // These are the *SERVER VERSIONS* of the *CLIENT PLAYER 1 & 2*
            var result = new MultiInstanceHelpers.CoroutineResultWrapper<NetworkObject>();

            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation(
                x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId,
                m_ServerNetworkManager, result));
            m_Player1OnServer = result.Result.GetComponent<VisibilityComponent>();

            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation(
                x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[1].LocalClientId,
                m_ServerNetworkManager, result));
            m_Player2OnServer = result.Result.GetComponent<VisibilityComponent>();

            // This is client1's view of itself
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation(
                x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId,
                m_ClientNetworkManagers[0], result));

            m_Player1OnClient1 = result.Result.GetComponent<VisibilityComponent>();

            // This is client2's view of itself
            result = new MultiInstanceHelpers.CoroutineResultWrapper<NetworkObject>();
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation(
                x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[1].LocalClientId,
                m_ClientNetworkManagers[1], result));

            m_Player2OnClient2 = result.Result.GetComponent<VisibilityComponent>();

            // This is client2's view of client 1's object
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation(
                x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId,
                m_ClientNetworkManagers[1], result));

            //            var client2client1 = result.Result;
            m_Player1OnClient2 = result.Result.GetComponent<VisibilityComponent>();
        }


        [UnityTest]
        public IEnumerator SomeTest1([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;
            var client1Id = m_Player1OnServer.NetworkObject.OwnerClientId;
            var client2Id = m_Player2OnServer.NetworkObject.OwnerClientId;

// todo: check rpc
// todo: see if clients can set visibility
            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () =>
                {
//                    m_Player1OnServer.NetworkObject.NetworkShow(client1Id);  // check, should break?
//                    m_Player1OnServer.NetworkObject.NetworkShow(client2Id);  // check, should break
                    m_Player1OnServer.SomeVar.Value = 1;
                },
                () =>
                {
                    return m_Player1OnClient1.SomeVar.Value == 1 &&
                           m_Player1OnClient2.SomeVar.Value == 1;
                }
            );
            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () =>
                {
                    m_Player1OnServer.NetworkObject.NetworkHide(client2Id);
                    m_Player1OnServer.SomeVar.Value = 2;
                },
                () =>
                {
                    return m_Player1OnClient1.SomeVar.Value == 2 &&
                           m_Player1OnClient2.SomeVar.Value == 1;
                }
            );
            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () =>
                {
                    m_Player1OnServer.NetworkObject.NetworkShow(client2Id);
                    m_Player1OnServer.SomeVar.Value = 3;  // SHOULD NOT NEED
                },
                () =>
                {
                    Debug.Log(m_Player1OnClient1.SomeVar.Value + ", " + m_Player1OnClient2.SomeVar.Value);
                    return m_Player1OnClient1.SomeVar.Value == 3 && 
                           m_Player1OnClient2.SomeVar.Value == 3;   // this breaks, client2 does not get the update
                }
            );
        }

        [UnityTearDown]
        public override IEnumerator Teardown()
        {
            yield return base.Teardown();
        }
    }
}
