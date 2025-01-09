#if COM_UNITY_MODULES_PHYSICS2D || COM_UNITY_MODULES_PHYSICS
using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{
#if COM_UNITY_MODULES_PHYSICS2D
    [TestFixture(RigidBodyTypes.Body2D, NetworkTransformRigidBodyTestComponent.AuthorityModes.Server)]
    [TestFixture(RigidBodyTypes.Body2D, NetworkTransformRigidBodyTestComponent.AuthorityModes.Owner)]
#endif
#if COM_UNITY_MODULES_PHYSICS
    [TestFixture(RigidBodyTypes.Body3D, NetworkTransformRigidBodyTestComponent.AuthorityModes.Server)]
    [TestFixture(RigidBodyTypes.Body3D, NetworkTransformRigidBodyTestComponent.AuthorityModes.Owner)]
#endif
    public class NetworkRigidbodyTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;

        private NetworkTransformRigidBodyTestComponent.AuthorityModes m_AuthorityMode;
        private RigidBodyTypes m_RigidBodyType;

        public enum RigidBodyTypes
        {
#if COM_UNITY_MODULES_PHYSICS2D
            Body2D,
#endif
#if COM_UNITY_MODULES_PHYSICS
            Body3D
#endif
        }

        public NetworkRigidbodyTests(RigidBodyTypes rigidBodyType, NetworkTransformRigidBodyTestComponent.AuthorityModes authorityMode)
        {
            m_RigidBodyType = rigidBodyType;
            m_AuthorityMode = authorityMode;
        }

        protected override void OnCreatePlayerPrefab()
        {
            var networkTransform = m_PlayerPrefab.AddComponent<NetworkTransformRigidBodyTestComponent>();
            networkTransform.AuthorityMode = m_AuthorityMode;
#if COM_UNITY_MODULES_PHYSICS2D
            if (m_RigidBodyType == RigidBodyTypes.Body2D)
            {
                m_PlayerPrefab.AddComponent<Rigidbody2D>();
                m_PlayerPrefab.AddComponent<NetworkRigidbody2DTestComponent>();
            }
            else
#endif
#if COM_UNITY_MODULES_PHYSICS
            {
                m_PlayerPrefab.AddComponent<Rigidbody>();
                m_PlayerPrefab.AddComponent<NetworkRigidbodyTestComponent>();
            }
#endif
            base.OnCreatePlayerPrefab();
        }

        /// <summary>
        /// Validates that both the 3D and 2D rigid body components are always kinematic prior to spawn
        /// and that their kinematic setting is correct after spawn based on the NetworkTransform's
        /// authority mode.
        /// </summary>
        [UnityTest]
        public IEnumerator TestKinematicSettings()
        {
            // Validate the Rigidbody's kinematic state after spawned
#if COM_UNITY_MODULES_PHYSICS2D
            if (m_RigidBodyType == RigidBodyTypes.Body2D)
            {
                TestRigidBody2D();
            }
            else
#endif
#if COM_UNITY_MODULES_PHYSICS
            {
                TestRigidBody();
            }
#endif

            yield return null;
        }

#if COM_UNITY_MODULES_PHYSICS2D
        private void TestRigidBody2D()
        {
            // Validate everything was kinematic before spawn
            var serverLocalPlayerNetworkRigidbody2d = m_ServerNetworkManager.LocalClient.PlayerObject.GetComponent<NetworkRigidbody2DTestComponent>();
            var clientLocalPlayerNetworkRigidbody2d = m_ClientNetworkManagers[0].LocalClient.PlayerObject.GetComponent<NetworkRigidbody2DTestComponent>();
            var serverClientPlayerNetworkRigidbody2d = m_PlayerNetworkObjects[m_ServerNetworkManager.LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<NetworkRigidbody2DTestComponent>();
            var clientServerPlayerNetworkRigidbody2d = m_PlayerNetworkObjects[m_ClientNetworkManagers[0].LocalClientId][m_ServerNetworkManager.LocalClientId].GetComponent<NetworkRigidbody2DTestComponent>();
            Assert.True(serverLocalPlayerNetworkRigidbody2d.WasKinematicBeforeSpawn);
            Assert.True(clientLocalPlayerNetworkRigidbody2d.WasKinematicBeforeSpawn);
            Assert.True(serverClientPlayerNetworkRigidbody2d.WasKinematicBeforeSpawn);
            Assert.True(clientServerPlayerNetworkRigidbody2d.WasKinematicBeforeSpawn);

            var isOwnerAuthority = m_AuthorityMode == NetworkTransformRigidBodyTestComponent.AuthorityModes.Owner;
            if (isOwnerAuthority)
            {
                // can commit player has authority and should have a kinematic mode of false (or true in case body was already kinematic).
                Assert.True(!serverLocalPlayerNetworkRigidbody2d.IsKinematic());
                Assert.True(!clientLocalPlayerNetworkRigidbody2d.IsKinematic());
                Assert.True(serverClientPlayerNetworkRigidbody2d.IsKinematic());
                Assert.True(clientServerPlayerNetworkRigidbody2d.IsKinematic());
            }
            else
            {
                Assert.True(!serverLocalPlayerNetworkRigidbody2d.IsKinematic());
                Assert.True(clientLocalPlayerNetworkRigidbody2d.IsKinematic());
                Assert.True(!serverClientPlayerNetworkRigidbody2d.IsKinematic());
                Assert.True(clientServerPlayerNetworkRigidbody2d.IsKinematic());
            }
        }
#endif

#if COM_UNITY_MODULES_PHYSICS
        private void TestRigidBody()
        {
            // Validate everything was kinematic before spawn
            var serverLocalPlayerNetworkRigidbody = m_ServerNetworkManager.LocalClient.PlayerObject.GetComponent<NetworkRigidbodyTestComponent>();
            var clientLocalPlayerNetworkRigidbody = m_ClientNetworkManagers[0].LocalClient.PlayerObject.GetComponent<NetworkRigidbodyTestComponent>();
            var serverClientPlayerNetworkRigidbody = m_PlayerNetworkObjects[m_ServerNetworkManager.LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<NetworkRigidbodyTestComponent>();
            var clientServerPlayerNetworkRigidbody = m_PlayerNetworkObjects[m_ClientNetworkManagers[0].LocalClientId][m_ServerNetworkManager.LocalClientId].GetComponent<NetworkRigidbodyTestComponent>();
            Assert.True(serverLocalPlayerNetworkRigidbody.WasKinematicBeforeSpawn);
            Assert.True(clientLocalPlayerNetworkRigidbody.WasKinematicBeforeSpawn);
            Assert.True(serverClientPlayerNetworkRigidbody.WasKinematicBeforeSpawn);
            Assert.True(clientServerPlayerNetworkRigidbody.WasKinematicBeforeSpawn);

            // Validate kinematic settings after spawn
            var serverLocalPlayerRigidbody = m_ServerNetworkManager.LocalClient.PlayerObject.GetComponent<Rigidbody>();
            var clientLocalPlayerRigidbody = m_ClientNetworkManagers[0].LocalClient.PlayerObject.GetComponent<Rigidbody>();
            var serverClientPlayerRigidbody = m_PlayerNetworkObjects[m_ServerNetworkManager.LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<Rigidbody>();
            var clientServerPlayerRigidbody = m_PlayerNetworkObjects[m_ClientNetworkManagers[0].LocalClientId][m_ServerNetworkManager.LocalClientId].GetComponent<Rigidbody>();

            var isOwnerAuthority = m_AuthorityMode == NetworkTransformRigidBodyTestComponent.AuthorityModes.Owner;
            if (isOwnerAuthority)
            {
                // can commit player has authority and should have a kinematic mode of false (or true in case body was already kinematic).
                Assert.True(!serverLocalPlayerRigidbody.isKinematic);
                Assert.True(!clientLocalPlayerRigidbody.isKinematic);
                Assert.True(serverClientPlayerRigidbody.isKinematic);
                Assert.True(clientServerPlayerRigidbody.isKinematic);
            }
            else
            {
                Assert.True(!serverLocalPlayerRigidbody.isKinematic);
                Assert.True(clientLocalPlayerRigidbody.isKinematic);
                Assert.True(!serverClientPlayerRigidbody.isKinematic);
                Assert.True(clientServerPlayerRigidbody.isKinematic);
            }
        }
#endif
    }
}
#endif
