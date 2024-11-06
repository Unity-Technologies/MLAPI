#if COM_UNITY_MODULES_PHYSICS
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Netcode.Components;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
namespace Unity.Netcode.RuntimeTests
{
    [TestFixture(HostOrServer.DAHost, MotionModels.UseTransform)]
    [TestFixture(HostOrServer.DAHost, MotionModels.UseRigidbody)]
    [TestFixture(HostOrServer.Host, MotionModels.UseTransform)]
    internal class NetworkTransformOwnershipTests : IntegrationTestWithApproximation
    {
        public enum MotionModels
        {
            UseRigidbody,
            UseTransform
        }
        protected override int NumberOfClients => 1;

        private GameObject m_ClientNetworkTransformPrefab;
        private GameObject m_NetworkTransformPrefab;

        private MotionModels m_MotionModel;

        public NetworkTransformOwnershipTests(HostOrServer hostOrServer, MotionModels motionModel) : base(hostOrServer)
        {
            m_MotionModel = motionModel;
        }

        protected override void OnServerAndClientsCreated()
        {
            VerifyObjectIsSpawnedOnClient.ResetObjectTable();
            m_ClientNetworkTransformPrefab = CreateNetworkObjectPrefab("OwnerAuthorityTest");
            var clientNetworkTransform = m_ClientNetworkTransformPrefab.AddComponent<TestClientNetworkTransform>();
            clientNetworkTransform.Interpolate = false;
            clientNetworkTransform.UseHalfFloatPrecision = false;
            var rigidBody = m_ClientNetworkTransformPrefab.AddComponent<Rigidbody>();
            rigidBody.useGravity = false;
            rigidBody.interpolation = RigidbodyInterpolation.None;
            rigidBody.maxLinearVelocity = 0;
            // NOTE: We don't use a sphere collider for this integration test because by the time we can
            // assure they don't collide and skew the results the NetworkObjects are already synchronized
            // with skewed results
            var networkRigidbody = m_ClientNetworkTransformPrefab.AddComponent<NetworkRigidbody>();
            networkRigidbody.UseRigidBodyForMotion = m_MotionModel == MotionModels.UseRigidbody;
            m_ClientNetworkTransformPrefab.AddComponent<VerifyObjectIsSpawnedOnClient>();

            m_NetworkTransformPrefab = CreateNetworkObjectPrefab("ServerAuthorityTest");
            var networkTransform = m_NetworkTransformPrefab.AddComponent<NetworkTransform>();
            rigidBody = m_NetworkTransformPrefab.AddComponent<Rigidbody>();
            rigidBody.useGravity = false;
            rigidBody.interpolation = RigidbodyInterpolation.None;
            rigidBody.maxLinearVelocity = 0;
            // NOTE: We don't use a sphere collider for this integration test because by the time we can
            // assure they don't collide and skew the results the NetworkObjects are already synchronized
            // with skewed results
            networkRigidbody = m_NetworkTransformPrefab.AddComponent<NetworkRigidbody>();
            networkRigidbody.UseRigidBodyForMotion = m_MotionModel == MotionModels.UseRigidbody;
            m_NetworkTransformPrefab.AddComponent<VerifyObjectIsSpawnedOnClient>();
            networkTransform.Interpolate = false;
            networkTransform.UseHalfFloatPrecision = false;

            base.OnServerAndClientsCreated();
        }

        /// <summary>
        /// Clients created during a test need to have their prefabs list updated to
        /// match the server's prefab list.
        /// </summary>
        protected override void OnNewClientCreated(NetworkManager networkManager)
        {
            foreach (var networkPrefab in m_ServerNetworkManager.NetworkConfig.Prefabs.Prefabs)
            {
                networkManager.NetworkConfig.Prefabs.Add(networkPrefab);
            }

            base.OnNewClientCreated(networkManager);
        }

        private bool ClientIsOwner()
        {
            var clientId = m_ClientNetworkManagers[0].LocalClientId;
            if (!VerifyObjectIsSpawnedOnClient.GetClientsThatSpawnedThisPrefab().Contains(clientId))
            {
                return false;
            }
            if (VerifyObjectIsSpawnedOnClient.GetClientInstance(clientId).OwnerClientId != clientId)
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// This test verifies a late joining client cannot change the transform when:
        /// - A NetworkObject is spawned with a host and one or more connected clients
        /// - The NetworkTransform is owner authoritative and spawned with the host as the owner
        /// - The host does not change the transform values
        /// - One of the already connected clients gains ownership of the spawned NetworkObject
        /// - The new client owner does not change the transform values
        /// - A new late joining client connects and is synchronized
        /// - The newly connected late joining client tries to change the transform of the NetworkObject
        /// it does not own
        /// </summary>
        [UnityTest]
        public IEnumerator LateJoinedNonOwnerClientCannotChangeTransform()
        {
            // Spawn the m_ClientNetworkTransformPrefab with the host starting as the owner
            var hostInstance = SpawnObject(m_ClientNetworkTransformPrefab, m_ServerNetworkManager);

            // Wait for the client to spawn it
            yield return WaitForConditionOrTimeOut(() => VerifyObjectIsSpawnedOnClient.GetClientsThatSpawnedThisPrefab().Contains(m_ClientNetworkManagers[0].LocalClientId));

            // Change the ownership to the connectd client
            hostInstance.GetComponent<NetworkObject>().ChangeOwnership(m_ClientNetworkManagers[0].LocalClientId);

            // Wait until the client gains ownership
            yield return WaitForConditionOrTimeOut(ClientIsOwner);
            AssertOnTimeout($"Timed out waiting for the {nameof(ClientIsOwner)} condition to be met!");

            // Spawn a new client
            yield return CreateAndStartNewClient();

            yield return WaitForConditionOrTimeOut(() => VerifyObjectIsSpawnedOnClient.NetworkManagerRelativeSpawnedObjects.ContainsKey(m_ClientNetworkManagers[1].LocalClientId));
            AssertOnTimeout($"Timed out waiting for late joing client VerifyObjectIsSpawnedOnClient entry to be created!");

            // Get the instance of the object relative to the newly joined client
            var newClientObjectInstance = VerifyObjectIsSpawnedOnClient.GetClientInstance(m_ClientNetworkManagers[1].LocalClientId);

            // Attempt to change the transform values
            var currentPosition = newClientObjectInstance.transform.position;
            newClientObjectInstance.transform.position = GetRandomVector3(0.5f, 10.0f);
            var rotation = newClientObjectInstance.transform.rotation;
            var currentRotation = rotation.eulerAngles;
            rotation.eulerAngles = GetRandomVector3(1.0f, 180.0f);
            var currentScale = newClientObjectInstance.transform.localScale;
            newClientObjectInstance.transform.localScale = GetRandomVector3(0.25f, 4.0f);

            // Wait one frame so the NetworkTransform can apply the owner's last state received on the late joining client side
            // (i.e. prevent the non-owner from changing the transform)
            if (m_MotionModel == MotionModels.UseRigidbody)
            {
                // Allow fixed update to run twice for values to propogate to Unity transform
                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();
            }
            else
            {
                yield return null;
            }

            // Get the owner instance
            var ownerInstance = VerifyObjectIsSpawnedOnClient.GetClientInstance(m_ClientNetworkManagers[0].LocalClientId);

            // Verify that the non-owner instance transform values are the same before they were changed last frame
            Assert.True(Approximately(currentPosition, newClientObjectInstance.transform.position), $"Non-owner instance was able to change the position!");
            Assert.True(Approximately(currentRotation, newClientObjectInstance.transform.rotation.eulerAngles), $"Non-owner instance was able to change the rotation!");
            Assert.True(Approximately(currentScale, newClientObjectInstance.transform.localScale), $"Non-owner instance was able to change the scale!");

            // Verify that the non-owner instance transform is still the same as the owner instance transform
            Assert.True(Approximately(ownerInstance.transform.position, newClientObjectInstance.transform.position), "Non-owner and owner instance position values are not the same!");
            Assert.True(Approximately(ownerInstance.transform.rotation.eulerAngles, newClientObjectInstance.transform.rotation.eulerAngles), "Non-owner and owner instance rotation values are not the same!");
            Assert.True(Approximately(ownerInstance.transform.localScale, newClientObjectInstance.transform.localScale), "Non-owner and owner instance scale values are not the same!");
        }

        public enum StartingOwnership
        {
            HostStartsAsOwner,
            ClientStartsAsOwner,
        }

        private bool ClientAndServerSpawnedInstance()
        {
            return VerifyObjectIsSpawnedOnClient.NetworkManagerRelativeSpawnedObjects.ContainsKey(m_ServerNetworkManager.LocalClientId) && VerifyObjectIsSpawnedOnClient.NetworkManagerRelativeSpawnedObjects.ContainsKey(m_ClientNetworkManagers[0].LocalClientId);
        }

        private bool m_UseAdjustedVariance;
        private const float k_AdjustedVariance = 0.025f;

        protected override float GetDeltaVarianceThreshold()
        {
            if (m_UseAdjustedVariance)
            {
                return k_AdjustedVariance;
            }
            return base.GetDeltaVarianceThreshold();
        }

        /// <summary>
        /// This verifies that when authority is owner authoritative the owner's
        /// Rigidbody is kinematic and the non-owner's is not.
        /// This also verifies that we can switch between owners and that only the
        /// owner can update the transform while non-owners cannot.
        /// </summary>
        /// <param name="spawnWithHostOwnership">determines who starts as the owner (true): host | (false): client</param>
        [UnityTest]
        public IEnumerator OwnerAuthoritativeTest([Values] StartingOwnership startingOwnership)
        {
            // Get the current ownership layout
            var networkManagerOwner = startingOwnership == StartingOwnership.HostStartsAsOwner ? m_ServerNetworkManager : m_ClientNetworkManagers[0];
            var networkManagerNonOwner = startingOwnership == StartingOwnership.HostStartsAsOwner ? m_ClientNetworkManagers[0] : m_ServerNetworkManager;

            // Spawn the m_ClientNetworkTransformPrefab and wait for the client-side to spawn the object
            var serverSideInstance = SpawnObject(m_ClientNetworkTransformPrefab, networkManagerOwner);
            yield return WaitForConditionOrTimeOut(ClientAndServerSpawnedInstance);
            AssertOnTimeout($"Timed out waiting for all object instances to be spawned!");

            // Get owner relative instances
            var ownerInstance = VerifyObjectIsSpawnedOnClient.GetClientInstance(networkManagerOwner.LocalClientId);
            var nonOwnerInstance = VerifyObjectIsSpawnedOnClient.GetClientInstance(networkManagerNonOwner.LocalClientId);
            Assert.NotNull(ownerInstance);
            Assert.NotNull(nonOwnerInstance);
            Assert.True(networkManagerOwner.LocalClientId != networkManagerNonOwner.LocalClientId);
            Assert.True(nonOwnerInstance.OwnerClientId != networkManagerNonOwner.LocalClientId);
            Assert.True(nonOwnerInstance.NetworkManager.LocalClientId == networkManagerNonOwner.LocalClientId);

            Vector3 GetNonOwnerPosition()
            {
                if (m_MotionModel == MotionModels.UseRigidbody)
                {
                    return nonOwnerInstance.GetComponent<Rigidbody>().position;
                }
                else
                {
                    return nonOwnerInstance.transform.position;
                }
            }

            Quaternion GetNonOwnerRotation()
            {
                if (m_MotionModel == MotionModels.UseRigidbody)
                {
                    return nonOwnerInstance.GetComponent<Rigidbody>().rotation;
                }
                else
                {
                    return nonOwnerInstance.transform.rotation;
                }
            }

            void LogNonOwnerRigidBody(int stage)
            {
                if (m_MotionModel == MotionModels.UseRigidbody && m_EnableVerboseDebug)
                {
                    var rigidbody = nonOwnerInstance.GetComponent<Rigidbody>();
                    Debug.Log($"[{stage}][Rigidbody-NonOwner][Owner:{nonOwnerInstance.OwnerClientId} [Client-{nonOwnerInstance.NetworkManager.LocalClientId}][Gravity: {rigidbody.useGravity}][Kinematic: {rigidbody.isKinematic}][RB-Pos: {rigidbody.position}][RB-Rotation: {rigidbody.rotation}]");
                }
            }

            void LogOwnerRigidBody(int stage)
            {
                if (m_MotionModel == MotionModels.UseRigidbody && m_EnableVerboseDebug)
                {
                    var rigidbody = ownerInstance.GetComponent<Rigidbody>();
                    Debug.Log($"[{stage}][Rigidbody-Owner][Owner:{ownerInstance.OwnerClientId} [Client-{ownerInstance.NetworkManager.LocalClientId}][Gravity: {rigidbody.useGravity}][Kinematic: {rigidbody.isKinematic}][RB-Pos: {rigidbody.position}][RB-Rotation: {rigidbody.rotation}]");
                }
            }

            // Make sure the owner is not kinematic and the non-owner(s) are kinematic
            Assert.True(nonOwnerInstance.GetComponent<Rigidbody>().isKinematic, $"{networkManagerNonOwner.name}'s object instance {nonOwnerInstance.name} is not kinematic when it should be!");
            Assert.False(ownerInstance.GetComponent<Rigidbody>().isKinematic, $"{networkManagerOwner.name}'s object instance {ownerInstance.name} is kinematic when it should not be!");
            if (m_MotionModel == MotionModels.UseRigidbody)
            {
                nonOwnerInstance.GetComponent<NetworkTransform>().LogStateUpdate = m_EnableVerboseDebug;
            }
            // Owner changes transform values
            var valueSetByOwner = Vector3.one * 2;
            var rotation = new Quaternion
            {
                eulerAngles = valueSetByOwner
            };
            if (m_MotionModel == MotionModels.UseRigidbody)
            {
                var ownerRigidbody = ownerInstance.GetComponent<Rigidbody>();
                ownerRigidbody.Move(valueSetByOwner, rotation);
                ownerRigidbody.linearVelocity = Vector3.zero;
                yield return s_DefaultWaitForTick;
                ownerInstance.transform.localScale = valueSetByOwner;
            }
            else
            {
                ownerInstance.transform.position = valueSetByOwner;
                ownerInstance.transform.rotation = rotation;
                ownerInstance.transform.localScale = valueSetByOwner;
            }

            var transformToTest = nonOwnerInstance.transform;
            LogNonOwnerRigidBody(1);
            yield return WaitForConditionOrTimeOut(() => Approximately(transformToTest.position, valueSetByOwner) && Approximately(transformToTest.localScale, valueSetByOwner) && Approximately(transformToTest.rotation, rotation));
            Assert.False(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for {networkManagerNonOwner.name}'s object instance {nonOwnerInstance.name} to change its transform!\n" +
                $"Expected Position: {valueSetByOwner} | Current Position: {transformToTest.position}\n" +
                $"Expected Rotation: {valueSetByOwner} | Current Rotation: {transformToTest.rotation.eulerAngles}\n" +
                $"Expected Scale: {valueSetByOwner} | Current Scale: {transformToTest.localScale}");

            // Verify non-owners cannot change transform values
            nonOwnerInstance.transform.position = Vector3.zero;
            yield return s_DefaultWaitForTick;
            if (m_MotionModel == MotionModels.UseRigidbody)
            {
                yield return new WaitForFixedUpdate();
            }

            LogNonOwnerRigidBody(2);
            Assert.True(Approximately(GetNonOwnerPosition(), valueSetByOwner), $"{networkManagerNonOwner.name}'s object instance {nonOwnerInstance.name} was allowed to change its position! Expected: {valueSetByOwner} Is Currently:{GetNonOwnerPosition()}");

            // Change ownership and wait for the non-owner to reflect the change
            VerifyObjectIsSpawnedOnClient.ResetObjectTable();
            if (m_DistributedAuthority)
            {
                ownerInstance.NetworkObject.ChangeOwnership(networkManagerNonOwner.LocalClientId);
            }
            else
            {
                m_ServerNetworkManager.SpawnManager.ChangeOwnership(serverSideInstance.GetComponent<NetworkObject>(), networkManagerNonOwner.LocalClientId, true);
            }
            LogNonOwnerRigidBody(3);
            yield return WaitForConditionOrTimeOut(() => nonOwnerInstance.GetComponent<NetworkObject>().OwnerClientId == networkManagerNonOwner.LocalClientId);
            Assert.False(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for {networkManagerNonOwner.name}'s object instance {nonOwnerInstance.name} to change ownership!");

            LogNonOwnerRigidBody(4);
            // Re-assign the ownership references and wait for the non-owner instance to be notified of ownership change
            networkManagerOwner = startingOwnership == StartingOwnership.HostStartsAsOwner ? m_ClientNetworkManagers[0] : m_ServerNetworkManager;
            networkManagerNonOwner = startingOwnership == StartingOwnership.HostStartsAsOwner ? m_ServerNetworkManager : m_ClientNetworkManagers[0];
            ownerInstance = VerifyObjectIsSpawnedOnClient.GetClientInstance(networkManagerOwner.LocalClientId);
            Assert.NotNull(ownerInstance);
            yield return WaitForConditionOrTimeOut(() => VerifyObjectIsSpawnedOnClient.GetClientInstance(networkManagerNonOwner.LocalClientId) != null);
            nonOwnerInstance = VerifyObjectIsSpawnedOnClient.GetClientInstance(networkManagerNonOwner.LocalClientId);
            Assert.NotNull(nonOwnerInstance);

            // Make sure the owner is not kinematic and the non-owner(s) are kinematic
            Assert.False(ownerInstance.GetComponent<Rigidbody>().isKinematic, $"{networkManagerOwner.name}'s object instance {ownerInstance.name} is kinematic when it should not be!");
            Assert.True(nonOwnerInstance.GetComponent<Rigidbody>().isKinematic, $"{networkManagerNonOwner.name}'s object instance {nonOwnerInstance.name} is not kinematic when it should be!");
            transformToTest = nonOwnerInstance.transform;
            Assert.True(networkManagerOwner.LocalClientId != networkManagerNonOwner.LocalClientId);
            Assert.True(nonOwnerInstance.OwnerClientId != networkManagerNonOwner.LocalClientId);
            Assert.True(nonOwnerInstance.NetworkManager.LocalClientId == networkManagerNonOwner.LocalClientId);

            yield return WaitForConditionOrTimeOut(() => Approximately(transformToTest.position, valueSetByOwner) && Approximately(transformToTest.localScale, valueSetByOwner) && Approximately(transformToTest.rotation, rotation));
            Assert.False(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for {networkManagerNonOwner.name}'s object instance {nonOwnerInstance.name} to change its transform!\n" +
                $"Expected Position: {valueSetByOwner} | Current Position: {transformToTest.position}\n" +
                $"Expected Rotation: {valueSetByOwner} | Current Rotation: {transformToTest.rotation.eulerAngles}\n" +
                $"Expected Scale: {valueSetByOwner} | Current Scale: {transformToTest.localScale}");

            LogNonOwnerRigidBody(5);
            // Have the new owner change transform values and wait for those values to be applied on the non-owner side.
            valueSetByOwner = Vector3.one * 10;
            ownerInstance.transform.localScale = valueSetByOwner;
            rotation.eulerAngles = valueSetByOwner;
            LogOwnerRigidBody(1);
            if (m_MotionModel == MotionModels.UseRigidbody)
            {
                m_UseAdjustedVariance = true;
                var ownerRigidbody = ownerInstance.GetComponent<Rigidbody>();
                ownerRigidbody.Move(valueSetByOwner, rotation);
                LogOwnerRigidBody(2);
                ownerInstance.GetComponent<NetworkTransform>().LogMotion = m_EnableVerboseDebug;
                nonOwnerInstance.GetComponent<NetworkTransform>().LogMotion = m_EnableVerboseDebug;
                ownerRigidbody.linearVelocity = Vector3.zero;
            }
            else
            {
                m_UseAdjustedVariance = false;
                ownerInstance.transform.position = valueSetByOwner;
                ownerInstance.transform.rotation = rotation;
            }

            LogOwnerRigidBody(3);
            LogNonOwnerRigidBody(6);
            yield return WaitForConditionOrTimeOut(() => Approximately(GetNonOwnerPosition(), valueSetByOwner) && Approximately(transformToTest.localScale, valueSetByOwner) && Approximately(GetNonOwnerRotation(), rotation));
            if (s_GlobalTimeoutHelper.TimedOut)
            {
                LogOwnerRigidBody(4);
                LogNonOwnerRigidBody(7);
            }
            Assert.False(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for {networkManagerNonOwner.name}'s object instance {nonOwnerInstance.name} to change its transform!\n" +
                $"Expected Position: {valueSetByOwner} | Current Position: {transformToTest.position}\n" +
                $"Expected Rotation: {valueSetByOwner} | Current Rotation: {transformToTest.rotation.eulerAngles}\n" +
                $"Expected Scale: {valueSetByOwner} | Current Scale: {transformToTest.localScale}");

            // The last check is to verify non-owners cannot change transform values after ownership has changed
            nonOwnerInstance.transform.position = Vector3.zero;
            yield return s_DefaultWaitForTick;
            if (m_MotionModel == MotionModels.UseRigidbody)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.True(Approximately(GetNonOwnerPosition(), valueSetByOwner), $"{networkManagerNonOwner.name}'s object instance {nonOwnerInstance.name} was allowed to change its position! Expected: {valueSetByOwner} Is Currently:{GetNonOwnerPosition()}");
        }

        /// <summary>
        /// This verifies that when authority is server authoritative the
        /// client's Rigidbody is kinematic and the server is not.
        /// This also verifies only the server can apply updates to the
        /// transform while the clients cannot.
        /// </summary>
        [UnityTest]
        public IEnumerator ServerAuthoritativeTest()
        {
            // Spawn the m_NetworkTransformPrefab and wait for the client-side to spawn the object
            var serverSideInstance = SpawnObject(m_NetworkTransformPrefab, m_ServerNetworkManager);
            yield return WaitForConditionOrTimeOut(() => VerifyObjectIsSpawnedOnClient.GetClientsThatSpawnedThisPrefab().Contains(m_ClientNetworkManagers[0].LocalClientId));

            var ownerInstance = VerifyObjectIsSpawnedOnClient.GetClientInstance(m_ServerNetworkManager.LocalClientId);
            var nonOwnerInstance = VerifyObjectIsSpawnedOnClient.GetClientInstance(m_ClientNetworkManagers[0].LocalClientId);

            // Make sure the owner is not kinematic and the non-owner(s) are kinematic
            Assert.False(ownerInstance.GetComponent<Rigidbody>().isKinematic, $"{m_ServerNetworkManager.name}'s object instance {ownerInstance.name} is kinematic when it should not be!");
            Assert.True(nonOwnerInstance.GetComponent<Rigidbody>().isKinematic, $"{m_ClientNetworkManagers[0].name}'s object instance {nonOwnerInstance.name} is not kinematic when it should be!");

            // Server changes transform values
            var valueSetByOwner = Vector3.one * 2;
            ownerInstance.transform.position = valueSetByOwner;
            ownerInstance.transform.localScale = valueSetByOwner;
            var rotation = new Quaternion
            {
                eulerAngles = valueSetByOwner
            };
            ownerInstance.transform.rotation = rotation;

            // Allow scale to update first when using rigid body motion
            if (m_MotionModel == MotionModels.UseRigidbody)
            {
                yield return new WaitForFixedUpdate();
            }
            var transformToTest = nonOwnerInstance.transform;
            yield return WaitForConditionOrTimeOut(() => transformToTest.position == valueSetByOwner && transformToTest.localScale == valueSetByOwner && transformToTest.rotation == rotation);
            Assert.False(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for {m_ClientNetworkManagers[0].name}'s object instance {nonOwnerInstance.name} to change its transform!\n" +
                $"Expected Position: {valueSetByOwner} | Current Position: {transformToTest.position}\n" +
                $"Expected Rotation: {valueSetByOwner} | Current Rotation: {transformToTest.rotation.eulerAngles}\n" +
                $"Expected Scale: {valueSetByOwner} | Current Scale: {transformToTest.localScale}");

            // The last check is to verify clients cannot change transform values
            nonOwnerInstance.transform.position = Vector3.zero;
            yield return s_DefaultWaitForTick;
            // Allow scale to update first when using rigid body motion
            if (m_MotionModel == MotionModels.UseRigidbody)
            {
                yield return new WaitForFixedUpdate();
            }
            Assert.True(nonOwnerInstance.transform.position == valueSetByOwner, $"{m_ClientNetworkManagers[0].name}'s object instance {nonOwnerInstance.name} was allowed to change its position! Expected: {Vector3.one} Is Currently:{nonOwnerInstance.transform.position}");
        }

        /// <summary>
        /// NetworkTransformOwnershipTests helper behaviour
        /// </summary>
        internal class VerifyObjectIsSpawnedOnClient : NetworkBehaviour
        {
            public static Dictionary<ulong, VerifyObjectIsSpawnedOnClient> NetworkManagerRelativeSpawnedObjects = new Dictionary<ulong, VerifyObjectIsSpawnedOnClient>();

            public static void ResetObjectTable()
            {
                NetworkManagerRelativeSpawnedObjects.Clear();
            }

            public override void OnGainedOwnership()
            {
                if (!NetworkManagerRelativeSpawnedObjects.ContainsKey(NetworkManager.LocalClientId))
                {
                    NetworkManagerRelativeSpawnedObjects.Add(NetworkManager.LocalClientId, this);
                }
                base.OnGainedOwnership();
            }

            public override void OnLostOwnership()
            {
                if (!NetworkManagerRelativeSpawnedObjects.ContainsKey(NetworkManager.LocalClientId))
                {
                    NetworkManagerRelativeSpawnedObjects.Add(NetworkManager.LocalClientId, this);
                }
                base.OnLostOwnership();
            }

            public static List<ulong> GetClientsThatSpawnedThisPrefab()
            {
                return NetworkManagerRelativeSpawnedObjects.Keys.ToList();
            }

            public static VerifyObjectIsSpawnedOnClient GetClientInstance(ulong clientId)
            {
                if (NetworkManagerRelativeSpawnedObjects.ContainsKey(clientId))
                {
                    return NetworkManagerRelativeSpawnedObjects[clientId];
                }
                return null;
            }

            public override void OnNetworkSpawn()
            {
                if (!NetworkManagerRelativeSpawnedObjects.ContainsKey(NetworkManager.LocalClientId))
                {
                    NetworkManagerRelativeSpawnedObjects.Add(NetworkManager.LocalClientId, this);
                }
                base.OnNetworkSpawn();
            }

            public override void OnNetworkDespawn()
            {
                if (NetworkManagerRelativeSpawnedObjects.ContainsKey(NetworkManager.LocalClientId))
                {
                    NetworkManagerRelativeSpawnedObjects.Remove(NetworkManager.LocalClientId);
                }
                base.OnNetworkDespawn();
            }
        }

        /// <summary>
        /// Until we can better locate the ClientNetworkTransform
        /// This will have to be used to verify the ownership authority
        /// </summary>
        [DisallowMultipleComponent]
        internal class TestClientNetworkTransform : NetworkTransform
        {
            //public override void OnNetworkSpawn()
            //{
            //    base.OnNetworkSpawn();
            //    CanCommitToTransform = IsOwner;
            //}

            //protected override void Update()
            //{
            //    CanCommitToTransform = IsOwner;
            //    base.Update();
            //    if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsConnectedClient || NetworkManager.Singleton.IsListening))
            //    {
            //        if (CanCommitToTransform)
            //        {
            //            TryCommitTransformToServer(transform, NetworkManager.LocalTime.Time);
            //        }
            //    }
            //}

            protected override bool OnIsServerAuthoritative()
            {
                return false;
            }
        }
    }

    [TestFixture(HostOrServer.DAHost, NetworkTransform.AuthorityModes.Owner)] // Validate the NetworkTransform owner authoritative mode fix using distributed authority
    [TestFixture(HostOrServer.Host, NetworkTransform.AuthorityModes.Server)] // Validate we have not impacted NetworkTransform server authoritative mode
    [TestFixture(HostOrServer.Host, NetworkTransform.AuthorityModes.Owner)] // Validate the NetworkTransform owner authoritative mode fix using client-server
    internal class NestedNetworkTransformTests : IntegrationTestWithApproximation
    {
        private const int k_NestedChildren = 5;
        protected override int NumberOfClients => 2;

        private GameObject m_SpawnObject;

        private NetworkTransform.AuthorityModes m_AuthorityMode;

        private StringBuilder m_ErrorLog = new StringBuilder();

        private List<NetworkManager> m_NetworkManagers = new List<NetworkManager>();
        private List<GameObject> m_SpawnedObjects = new List<GameObject>();

        public NestedNetworkTransformTests(HostOrServer hostOrServer, NetworkTransform.AuthorityModes authorityMode) : base(hostOrServer)
        {
            m_AuthorityMode = authorityMode;
        }

        /// <summary>
        /// Creates a player prefab with several nested NetworkTransforms
        /// </summary>
        protected override void OnCreatePlayerPrefab()
        {
            var networkTransform = m_PlayerPrefab.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = m_AuthorityMode;
            var parent = m_PlayerPrefab;
            // Add several nested NetworkTransforms
            for (int i = 0; i < k_NestedChildren; i++)
            {
                var nestedChild = new GameObject();
                nestedChild.transform.parent = parent.transform;
                var nestedNetworkTransform = nestedChild.AddComponent<NetworkTransform>();
                nestedNetworkTransform.AuthorityMode = m_AuthorityMode;
                nestedNetworkTransform.InLocalSpace = true;
                parent = nestedChild;
            }
            base.OnCreatePlayerPrefab();
        }

        private void RandomizeObjectTransformPositions(GameObject gameObject)
        {
            var networkObject = gameObject.GetComponent<NetworkObject>();
            Assert.True(networkObject.ChildNetworkBehaviours.Count > 0);

            foreach (var networkTransform in networkObject.NetworkTransforms)
            {
                networkTransform.gameObject.transform.position = GetRandomVector3(-15.0f, 15.0f);
            }
        }

        /// <summary>
        /// Randomizes each player's position when validating distributed authority
        /// </summary>
        /// <returns></returns>
        private GameObject FetchLocalPlayerPrefabToSpawn()
        {
            RandomizeObjectTransformPositions(m_PlayerPrefab);
            return m_PlayerPrefab;
        }

        /// <summary>
        /// Randomizes the player position when validating client-server
        /// </summary>
        /// <param name="connectionApprovalRequest"></param>
        /// <param name="connectionApprovalResponse"></param>
        private void ConnectionApprovalHandler(NetworkManager.ConnectionApprovalRequest connectionApprovalRequest, NetworkManager.ConnectionApprovalResponse connectionApprovalResponse)
        {
            connectionApprovalResponse.Approved = true;
            connectionApprovalResponse.CreatePlayerObject = true;
            RandomizeObjectTransformPositions(m_PlayerPrefab);
            connectionApprovalResponse.Position = GetRandomVector3(-15.0f, 15.0f);
        }

        protected override void OnServerAndClientsCreated()
        {
            // Create a prefab to spawn with each NetworkManager as the owner
            m_SpawnObject = CreateNetworkObjectPrefab("SpawnObj");
            var networkTransform = m_SpawnObject.AddComponent<NetworkTransform>();
            networkTransform.AuthorityMode = m_AuthorityMode;
            var parent = m_SpawnObject;
            // Add several nested NetworkTransforms
            for (int i = 0; i < k_NestedChildren; i++)
            {
                var nestedChild = new GameObject();
                nestedChild.transform.parent = parent.transform;
                var nestedNetworkTransform = nestedChild.AddComponent<NetworkTransform>();
                nestedNetworkTransform.AuthorityMode = m_AuthorityMode;
                nestedNetworkTransform.InLocalSpace = true;
                parent = nestedChild;
            }

            if (m_DistributedAuthority)
            {
                if (!UseCMBService())
                {
                    m_ServerNetworkManager.OnFetchLocalPlayerPrefabToSpawn = FetchLocalPlayerPrefabToSpawn;
                }

                foreach (var client in m_ClientNetworkManagers)
                {
                    client.OnFetchLocalPlayerPrefabToSpawn = FetchLocalPlayerPrefabToSpawn;
                }
            }
            else
            {
                m_ServerNetworkManager.NetworkConfig.ConnectionApproval = true;
                m_ServerNetworkManager.ConnectionApprovalCallback += ConnectionApprovalHandler;
                foreach (var client in m_ClientNetworkManagers)
                {
                    client.NetworkConfig.ConnectionApproval = true;
                }
            }

            base.OnServerAndClientsCreated();
        }

        /// <summary>
        /// Validates the transform positions of two NetworkObject instances
        /// </summary>
        /// <param name="current">the local instance (source of truth)</param>
        /// <param name="testing">the remote instance</param>
        /// <returns></returns>
        private bool ValidateTransforms(NetworkObject current, NetworkObject testing)
        {
            if (current.ChildNetworkBehaviours.Count == 0 || testing.ChildNetworkBehaviours.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < current.NetworkTransforms.Count - 1; i++)
            {
                var transformA = current.NetworkTransforms[i].transform;
                var transformB = testing.NetworkTransforms[i].transform;
                if (!Approximately(transformA.position, transformB.position))
                {
                    m_ErrorLog.AppendLine($"TransformA Position {transformA.position} != TransformB Position {transformB.position}");
                    return false;
                }
                if (!Approximately(transformA.localPosition, transformB.localPosition))
                {
                    m_ErrorLog.AppendLine($"TransformA Local Position {transformA.position} != TransformB Local Position {transformB.position}");
                    return false;
                }
                if (transformA.parent != null)
                {
                    if (current.NetworkTransforms[i].InLocalSpace != testing.NetworkTransforms[i].InLocalSpace)
                    {
                        m_ErrorLog.AppendLine($"NetworkTransform-{current.OwnerClientId}-{current.NetworkTransforms[i].NetworkBehaviourId} InLocalSpace ({current.NetworkTransforms[i].InLocalSpace}) is different from the remote instance version on Client-{testing.NetworkManager.LocalClientId}!");
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Validates all player instances spawned with the correct positions including all nested NetworkTransforms
        /// When running in server authority mode we are validating this fix did not impact that.
        /// </summary>
        private bool AllClientInstancesSynchronized()
        {
            m_ErrorLog.Clear();

            foreach (var current in m_NetworkManagers)
            {
                var currentPlayer = current.LocalClient.PlayerObject;
                var currentNetworkObjectId = currentPlayer.NetworkObjectId;
                foreach (var testing in m_NetworkManagers)
                {
                    if (currentPlayer == testing.LocalClient.PlayerObject)
                    {
                        continue;
                    }

                    if (!testing.SpawnManager.SpawnedObjects.ContainsKey(currentNetworkObjectId))
                    {
                        m_ErrorLog.AppendLine($"Failed to find Client-{currentPlayer.OwnerClientId}'s player instance on Client-{testing.LocalClientId}!");
                        return false;
                    }

                    var remoteInstance = testing.SpawnManager.SpawnedObjects[currentNetworkObjectId];
                    if (!ValidateTransforms(currentPlayer, remoteInstance))
                    {
                        m_ErrorLog.AppendLine($"Failed to validate Client-{currentPlayer.OwnerClientId} against its remote instance on Client-{testing.LocalClientId}!");
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Validates that dynamically spawning works the same.
        /// When running in server authority mode we are validating this fix did not impact that.
        /// </summary>
        /// <returns></returns>
        private bool AllSpawnedObjectsSynchronized()
        {
            m_ErrorLog.Clear();

            foreach (var current in m_SpawnedObjects)
            {
                var currentNetworkObject = current.GetComponent<NetworkObject>();
                var currentNetworkObjectId = currentNetworkObject.NetworkObjectId;
                foreach (var testing in m_NetworkManagers)
                {
                    if (currentNetworkObject.OwnerClientId == testing.LocalClientId)
                    {
                        continue;
                    }

                    if (!testing.SpawnManager.SpawnedObjects.ContainsKey(currentNetworkObjectId))
                    {
                        m_ErrorLog.AppendLine($"Failed to find Client-{currentNetworkObject.OwnerClientId}'s player instance on Client-{testing.LocalClientId}!");
                        return false;
                    }

                    var remoteInstance = testing.SpawnManager.SpawnedObjects[currentNetworkObjectId];
                    if (!ValidateTransforms(currentNetworkObject, remoteInstance))
                    {
                        m_ErrorLog.AppendLine($"Failed to validate Client-{currentNetworkObject.OwnerClientId} against its remote instance on Client-{testing.LocalClientId}!");
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Validates that spawning player and dynamically spawned prefab instances with nested NetworkTransforms
        /// synchronizes properly in both client-server and distributed authority when using owner authoritative mode.
        /// </summary>
        [UnityTest]
        public IEnumerator NestedNetworkTransformSpawnPositionTest()
        {
            if (!m_DistributedAuthority || (m_DistributedAuthority && !UseCMBService()))
            {
                m_NetworkManagers.Add(m_ServerNetworkManager);
            }
            m_NetworkManagers.AddRange(m_ClientNetworkManagers);

            yield return WaitForConditionOrTimeOut(AllClientInstancesSynchronized);
            AssertOnTimeout($"Failed to synchronize all client instances!\n{m_ErrorLog}");

            foreach (var networkManager in m_NetworkManagers)
            {
                // Randomize the position 
                RandomizeObjectTransformPositions(m_SpawnObject);

                // Create an instance owned by the specified networkmanager
                m_SpawnedObjects.Add(SpawnObject(m_SpawnObject, networkManager));
            }
            // Randomize the position once more just to assure we are instantiating remote instances
            // with a completely different position
            RandomizeObjectTransformPositions(m_SpawnObject);
            yield return WaitForConditionOrTimeOut(AllSpawnedObjectsSynchronized);
            AssertOnTimeout($"Failed to synchronize all spawned NetworkObject instances!\n{m_ErrorLog}");
            m_SpawnedObjects.Clear();
            m_NetworkManagers.Clear();
        }

        protected override IEnumerator OnTearDown()
        {
            // In case there was a failure, go ahead and clear these lists out for any pending TextFixture passes
            m_SpawnedObjects.Clear();
            m_NetworkManagers.Clear();
            return base.OnTearDown();
        }
    }
}
#endif
