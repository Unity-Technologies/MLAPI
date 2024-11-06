#if COM_UNITY_MODULES_PHYSICS2D
using System.Collections;
using NUnit.Framework;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.TestTools;
using Unity.Netcode.TestHelpers.Runtime;

namespace Unity.Netcode.RuntimeTests
{
    internal class NetworkRigidbody2DDynamicTest : NetworkRigidbody2DTestBase
    {
        public override bool Kinematic => false;
    }

    internal class NetworkRigidbody2DKinematicTest : NetworkRigidbody2DTestBase
    {
        public override bool Kinematic => true;
    }

    public abstract class NetworkRigidbody2DTestBase : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;

        public abstract bool Kinematic { get; }

        protected override void OnCreatePlayerPrefab()
        {
            m_PlayerPrefab.AddComponent<NetworkTransform>();
            m_PlayerPrefab.AddComponent<Rigidbody2D>();
            m_PlayerPrefab.AddComponent<NetworkRigidbody2D>();
            m_PlayerPrefab.GetComponent<Rigidbody2D>().interpolation = RigidbodyInterpolation2D.Interpolate;
            m_PlayerPrefab.GetComponent<Rigidbody2D>().isKinematic = Kinematic;
        }

        /// <summary>
        /// Tests that a server can destroy a NetworkObject and that it gets despawned correctly.
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator TestRigidbodyKinematicEnableDisable()
        {
            // This is the *SERVER VERSION* of the *CLIENT PLAYER*
            var serverClientPlayerResult = new NetcodeIntegrationTestHelpers.ResultWrapper<NetworkObject>();
            yield return NetcodeIntegrationTestHelpers.GetNetworkObjectByRepresentation((x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId), m_ServerNetworkManager, serverClientPlayerResult);
            var serverPlayer = serverClientPlayerResult.Result.gameObject;

            // This is the *CLIENT VERSION* of the *CLIENT PLAYER*
            var clientClientPlayerResult = new NetcodeIntegrationTestHelpers.ResultWrapper<NetworkObject>();
            yield return NetcodeIntegrationTestHelpers.GetNetworkObjectByRepresentation((x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId), m_ClientNetworkManagers[0], clientClientPlayerResult);
            var clientPlayer = clientClientPlayerResult.Result.gameObject;

            Assert.IsNotNull(serverPlayer);
            Assert.IsNotNull(clientPlayer);

            yield return WaitForTicks(m_ServerNetworkManager, 5);

            // server rigidbody has authority and should have a kinematic mode of false
            Assert.True(serverPlayer.GetComponent<Rigidbody2D>().isKinematic == Kinematic);
            Assert.AreEqual(RigidbodyInterpolation2D.Interpolate, serverPlayer.GetComponent<Rigidbody2D>().interpolation);

            // client rigidbody has no authority and should have a kinematic mode of true
            Assert.True(clientPlayer.GetComponent<Rigidbody2D>().isKinematic);
            Assert.AreEqual(RigidbodyInterpolation2D.None, clientPlayer.GetComponent<Rigidbody2D>().interpolation);

            // despawn the server player, (but keep it around on the server)
            serverPlayer.GetComponent<NetworkObject>().Despawn(false);

            yield return WaitForTicks(m_ServerNetworkManager, 5);

            // This should equal Kinematic
            Assert.IsTrue(serverPlayer.GetComponent<Rigidbody2D>().isKinematic == Kinematic);

            yield return WaitForTicks(m_ServerNetworkManager, 5);

            Assert.IsTrue(clientPlayer == null); // safety check that object is actually despawned.
        }
    }
}
#endif // COM_UNITY_MODULES_PHYSICS2D
