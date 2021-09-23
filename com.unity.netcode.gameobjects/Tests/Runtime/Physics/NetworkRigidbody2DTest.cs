using System.Collections;
using NUnit.Framework;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests.Physics
{
    public class NetworkRigidbody2DDynamicTest : NetworkRigidbodyTestBase
    {
        public override bool Kinematic => false;
    }

    public class NetworkRigidbody2DKinematicTest : NetworkRigidbodyTestBase
    {
        public override bool Kinematic => true;
    }

    public abstract class NetworkRigidbody2DTestBase : BaseMultiInstanceTest
    {
        protected override int NbClients => 1;

        public abstract bool Kinematic { get; }

        [UnitySetUp]
        public override IEnumerator Setup()
        {
            yield return StartSomeClientsAndServerWithPlayers(true, NbClients, playerPrefab =>
            {
                playerPrefab.AddComponent<NetworkTransform>();
                playerPrefab.AddComponent<Rigidbody2D>();
                playerPrefab.AddComponent<NetworkRigidbody>();
                playerPrefab.GetComponent<Rigidbody2D>().isKinematic = Kinematic;
            });
        }

        /// <summary>
        /// Tests that a server can destroy a NetworkObject and that it gets despawned correctly.
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator TestRigidbodyKinematicEnableDisable()
        {
            // This is the *SERVER VERSION* of the *CLIENT PLAYER*
            var serverClientPlayerResult = new MultiInstanceHelpers.CoroutineResultWrapper<NetworkObject>();
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation((x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId), m_ServerNetworkManager, serverClientPlayerResult));
            var serverPlayer = serverClientPlayerResult.Result.gameObject;

            // This is the *CLIENT VERSION* of the *CLIENT PLAYER*
            var clientClientPlayerResult = new MultiInstanceHelpers.CoroutineResultWrapper<NetworkObject>();
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation((x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId), m_ClientNetworkManagers[0], clientClientPlayerResult));
            var clientPlayer = clientClientPlayerResult.Result.gameObject;

            Assert.IsNotNull(serverPlayer);
            Assert.IsNotNull(clientPlayer);

            int waitFor = Time.frameCount + 2;
            yield return new WaitUntil(() => Time.frameCount >= waitFor);

            // server rigidbody has authority and should have a kinematic mode of false
            Assert.True(serverPlayer.GetComponent<Rigidbody2D>().isKinematic == Kinematic);

            // client rigidbody has no authority and should have a kinematic mode of true
            Assert.True(clientPlayer.GetComponent<Rigidbody2D>().isKinematic);

            // despawn the server player, (but keep it around on the server)
            serverPlayer.GetComponent<NetworkObject>().Despawn(false);

            yield return null;

            Assert.IsTrue(serverPlayer.GetComponent<Rigidbody2D>().isKinematic == Kinematic);

            yield return null;
            Assert.IsTrue(clientPlayer == null); // safety check that object is actually despawned.
        }
    }
}
