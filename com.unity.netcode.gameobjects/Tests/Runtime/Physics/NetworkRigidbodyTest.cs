#if COM_UNITY_MODULES_PHYSICS
using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Unity.Netcode.Components;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    [TestFixture(RigidbodyInterpolation.Interpolate, true, true)] // This should be allowed under all condistions when using Rigidbody motion
    [TestFixture(RigidbodyInterpolation.Extrapolate, true, true)] // This should not allow extrapolation on non-auth instances when using Rigidbody motion & NT interpolation
    [TestFixture(RigidbodyInterpolation.Extrapolate, false, true)] // This should allow extrapolation on non-auth instances when using Rigidbody & NT has no interpolation
    [TestFixture(RigidbodyInterpolation.Interpolate, true, false)] // This should not allow kinematic instances to have Rigidbody interpolation enabled
    [TestFixture(RigidbodyInterpolation.Interpolate, false, false)] // Testing that rigid body interpolation remains the same if NT interpolate is disabled
    internal class NetworkRigidbodyTest : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;
        private bool m_NetworkTransformInterpolate;
        private bool m_UseRigidBodyForMotion;
        private RigidbodyInterpolation m_RigidbodyInterpolation;

        public NetworkRigidbodyTest(RigidbodyInterpolation rigidbodyInterpolation, bool networkTransformInterpolate, bool useRigidbodyForMotion)
        {
            m_RigidbodyInterpolation = rigidbodyInterpolation;
            m_NetworkTransformInterpolate = networkTransformInterpolate;
            m_UseRigidBodyForMotion = useRigidbodyForMotion;
        }

        protected override void OnCreatePlayerPrefab()
        {
            var networkTransform = m_PlayerPrefab.AddComponent<NetworkTransform>();
            networkTransform.Interpolate = m_NetworkTransformInterpolate;
            var rigidbody = m_PlayerPrefab.AddComponent<Rigidbody>();
            rigidbody.interpolation = m_RigidbodyInterpolation;
            var networkRigidbody = m_PlayerPrefab.AddComponent<NetworkRigidbody>();
            networkRigidbody.UseRigidBodyForMotion = m_UseRigidBodyForMotion;
        }

        /// <summary>
        /// Tests that a server can destroy a NetworkObject and that it gets despawned correctly.
        /// </summary>
        /// <returns></returns>
        [UnityTest]
        public IEnumerator TestRigidbodyKinematicEnableDisable()
        {
            // This is the *SERVER VERSION* of the *CLIENT PLAYER*
            var serverClientPlayerInstance = m_ServerNetworkManager.ConnectedClients[m_ClientNetworkManagers[0].LocalClientId].PlayerObject;

            // This is the *CLIENT VERSION* of the *CLIENT PLAYER*
            var clientPlayerInstance = m_ClientNetworkManagers[0].LocalClient.PlayerObject;

            Assert.IsNotNull(serverClientPlayerInstance, $"{nameof(serverClientPlayerInstance)} is null!");
            Assert.IsNotNull(clientPlayerInstance, $"{nameof(clientPlayerInstance)} is null!");

            var serverClientInstanceRigidBody = serverClientPlayerInstance.GetComponent<Rigidbody>();
            var clientRigidBody = clientPlayerInstance.GetComponent<Rigidbody>();

            if (m_UseRigidBodyForMotion)
            {
                var interpolateCompareNonAuthoritative = m_NetworkTransformInterpolate ? RigidbodyInterpolation.Interpolate : m_RigidbodyInterpolation;

                // Server authoritative NT should yield non-kinematic mode for the server-side player instance
                Assert.False(serverClientInstanceRigidBody.isKinematic, $"[Server-Side] Client-{m_ClientNetworkManagers[0].LocalClientId} player's {nameof(Rigidbody)} is kinematic!");

                // The authoritative instance can be None, Interpolate, or Extrapolate for the Rigidbody interpolation settings.
                Assert.AreEqual(m_RigidbodyInterpolation, serverClientInstanceRigidBody.interpolation, $"[Server-Side] Client-{m_ClientNetworkManagers[0].LocalClientId} " +
                    $"player's {nameof(Rigidbody)}'s interpolation is {serverClientInstanceRigidBody.interpolation} and not {m_RigidbodyInterpolation}!");

                // Server authoritative NT should yield kinematic mode for the client-side player instance
                Assert.True(clientRigidBody.isKinematic, $"[Client-Side] Client-{m_ClientNetworkManagers[0].LocalClientId} player's {nameof(Rigidbody)} is not kinematic!");

                // When using Rigidbody motion, authoritative and non-authoritative Rigidbody interpolation settings should be preserved (except when extrapolation is used
                Assert.AreEqual(interpolateCompareNonAuthoritative, clientRigidBody.interpolation, $"[Client-Side] Client-{m_ClientNetworkManagers[0].LocalClientId} " +
                    $"player's {nameof(Rigidbody)}'s interpolation is {clientRigidBody.interpolation} and not {interpolateCompareNonAuthoritative}!");
            }
            else
            {
                // server rigidbody has authority and should not be kinematic
                Assert.False(serverClientInstanceRigidBody.isKinematic, $"[Server-Side] Client-{m_ClientNetworkManagers[0].LocalClientId} player's {nameof(Rigidbody)} is kinematic!");
                Assert.AreEqual(RigidbodyInterpolation.Interpolate, serverClientInstanceRigidBody.interpolation, $"[Server-Side] Client-{m_ClientNetworkManagers[0].LocalClientId} " +
                    $"player's {nameof(Rigidbody)}'s interpolation is {serverClientInstanceRigidBody.interpolation} and not {nameof(RigidbodyInterpolation.Interpolate)}!");

                // Server authoritative NT should yield kinematic mode for the client-side player instance
                Assert.True(clientRigidBody.isKinematic, $"[Client-Side] Client-{m_ClientNetworkManagers[0].LocalClientId} player's {nameof(Rigidbody)} is not kinematic!");

                // client rigidbody has no authority with NT interpolation disabled should allow Rigidbody interpolation
                if (!m_NetworkTransformInterpolate)
                {
                    Assert.AreEqual(RigidbodyInterpolation.Interpolate, clientRigidBody.interpolation, $"[Client-Side] Client-{m_ClientNetworkManagers[0].LocalClientId} " +
                        $"player's {nameof(Rigidbody)}'s interpolation is {clientRigidBody.interpolation} and not {nameof(RigidbodyInterpolation.Interpolate)}!");
                }
                else
                {
                    Assert.AreEqual(RigidbodyInterpolation.None, clientRigidBody.interpolation, $"[Client-Side] Client-{m_ClientNetworkManagers[0].LocalClientId} " +
                        $"player's {nameof(Rigidbody)}'s interpolation is {clientRigidBody.interpolation} and not {nameof(RigidbodyInterpolation.None)}!");
                }
            }

            // despawn the server player (but keep it around on the server)
            serverClientPlayerInstance.Despawn(false);

            yield return WaitForConditionOrTimeOut(() => !serverClientPlayerInstance.IsSpawned && !clientPlayerInstance.IsSpawned);
            AssertOnTimeout("Timed out waiting for client player to despawn on both server and client!");

            // When despawned, we should always be kinematic (i.e. don't apply physics when despawned)
            Assert.True(serverClientInstanceRigidBody.isKinematic, $"[Server-Side][Despawned] Client-{m_ClientNetworkManagers[0].LocalClientId} player's {nameof(Rigidbody)} is not kinematic when despawned!");
            Assert.IsTrue(clientPlayerInstance == null, $"[Client-Side] Player {nameof(NetworkObject)} is not null!");
        }
    }

    internal class ContactEventTransformHelperWithInfo : ContactEventTransformHelper, IContactEventHandlerWithInfo
    {
        public ContactEventHandlerInfo GetContactEventHandlerInfo()
        {
            var contactEventHandlerInfo = new ContactEventHandlerInfo()
            {
                HasContactEventPriority = IsOwner,
                ProvideNonRigidBodyContactEvents = m_EnableNonRigidbodyContacts.Value,
            };
            return contactEventHandlerInfo;
        }

        protected override void OnRegisterForContactEvents(bool isRegistering)
        {
            RigidbodyContactEventManager.Instance.RegisterHandler(this, isRegistering);
        }
    }


    internal class ContactEventTransformHelper : NetworkTransform, IContactEventHandler
    {
        public static Vector3 SessionOwnerSpawnPoint;
        public static Vector3 ClientSpawnPoint;
        public static bool VerboseDebug;
        public enum HelperStates
        {
            None,
            MoveForward,
        }

        private HelperStates m_HelperState;

        public void SetHelperState(HelperStates state)
        {
            m_HelperState = state;
            if (!m_NetworkRigidbody.IsKinematic())
            {
                m_NetworkRigidbody.Rigidbody.angularVelocity = Vector3.zero;
                m_NetworkRigidbody.Rigidbody.linearVelocity = Vector3.zero;
            }
            m_NetworkRigidbody.Rigidbody.isKinematic = m_HelperState == HelperStates.None;
            if (!m_NetworkRigidbody.IsKinematic())
            {
                m_NetworkRigidbody.Rigidbody.angularVelocity = Vector3.zero;
                m_NetworkRigidbody.Rigidbody.linearVelocity = Vector3.zero;
            }

        }

        protected struct ContactEventInfo
        {
            public ulong EventId;
            public Vector3 AveragedCollisionNormal;
            public Rigidbody CollidingBody;
            public Vector3 ContactPoint;
        }

        protected List<ContactEventInfo> m_ContactEvents = new List<ContactEventInfo>();

        protected NetworkVariable<bool> m_EnableNonRigidbodyContacts = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);

        protected NetworkRigidbody m_NetworkRigidbody;
        public ContactEventTransformHelper Target;

        public bool HasContactEvents()
        {
            return m_ContactEvents.Count > 0;
        }

        public Rigidbody GetRigidbody()
        {
            return m_NetworkRigidbody.Rigidbody;
        }

        public bool HadContactWith(ContactEventTransformHelper otherObject)
        {
            if (otherObject == null)
            {
                return false;
            }
            foreach (var contactEvent in m_ContactEvents)
            {
                if (contactEvent.CollidingBody == otherObject.m_NetworkRigidbody.Rigidbody)
                {
                    return true;
                }
            }
            return false;
        }

        protected virtual void CheckToStopMoving()
        {
            SetHelperState(HadContactWith(Target) ? HelperStates.None : HelperStates.MoveForward);
        }

        public void ContactEvent(ulong eventId, Vector3 averagedCollisionNormal, Rigidbody collidingBody, Vector3 contactPoint, bool hasCollisionStay = false, Vector3 averagedCollisionStayNormal = default)
        {
            if (Target == null)
            {
                return;
            }

            if (collidingBody != null)
            {
                Log($">>>>>>> contact event with {collidingBody.name}!");
            }
            else
            {
                Log($">>>>>>> contact event with non-rigidbody!");
            }

            m_ContactEvents.Add(new ContactEventInfo()
            {
                EventId = eventId,
                AveragedCollisionNormal = averagedCollisionNormal,
                CollidingBody = collidingBody,
                ContactPoint = contactPoint,
            });
            CheckToStopMoving();
        }

        private void SetInitialPositionClientServer()
        {
            if (IsServer)
            {
                if (!NetworkManager.DistributedAuthorityMode && !IsLocalPlayer)
                {
                    transform.position = ClientSpawnPoint;
                    m_NetworkRigidbody.Rigidbody.position = ClientSpawnPoint;
                }
                else
                {
                    transform.position = SessionOwnerSpawnPoint;
                    m_NetworkRigidbody.Rigidbody.position = SessionOwnerSpawnPoint;
                }
            }
            else
            {
                transform.position = ClientSpawnPoint;
                m_NetworkRigidbody.Rigidbody.position = ClientSpawnPoint;
            }
        }

        private void SetInitialPositionDistributedAuthority()
        {
            if (HasAuthority)
            {
                if (IsSessionOwner)
                {
                    transform.position = SessionOwnerSpawnPoint;
                    m_NetworkRigidbody.Rigidbody.position = SessionOwnerSpawnPoint;
                }
                else
                {
                    transform.position = ClientSpawnPoint;
                    m_NetworkRigidbody.Rigidbody.position = ClientSpawnPoint;
                }
            }
        }

        public override void OnNetworkSpawn()
        {
            m_NetworkRigidbody = GetComponent<NetworkRigidbody>();

            m_NetworkRigidbody.Rigidbody.maxLinearVelocity = 15;
            m_NetworkRigidbody.Rigidbody.maxAngularVelocity = 10;

            if (NetworkManager.DistributedAuthorityMode)
            {
                SetInitialPositionDistributedAuthority();
            }
            else
            {
                SetInitialPositionClientServer();
            }
            if (IsLocalPlayer)
            {
                RegisterForContactEvents(true);
            }
            else
            {
                m_NetworkRigidbody.Rigidbody.detectCollisions = false;
            }
            base.OnNetworkSpawn();
        }

        protected virtual void OnRegisterForContactEvents(bool isRegistering)
        {
            RigidbodyContactEventManager.Instance.RegisterHandler(this, isRegistering);
        }

        public void RegisterForContactEvents(bool isRegistering)
        {
            OnRegisterForContactEvents(isRegistering);
        }

        private void FixedUpdate()
        {
            if (!IsSpawned || !IsOwner || m_HelperState != HelperStates.MoveForward)
            {
                return;
            }
            var distance = Vector3.Distance(Target.transform.position, transform.position);
            var moveAmount = Mathf.Max(1.2f, distance);
            // Head towards our target
            var dir = (Target.transform.position - transform.position).normalized;
            var deltaMove = dir * moveAmount * Time.fixedDeltaTime;
            m_NetworkRigidbody.Rigidbody.MovePosition(m_NetworkRigidbody.Rigidbody.position + deltaMove);


            Log($" Loc: {transform.position} | Dest: {Target.transform.position} | Dist: {distance} | MoveDelta: {deltaMove}");
        }

        protected void Log(string msg)
        {
            if (VerboseDebug)
            {
                Debug.Log($"Client-{OwnerClientId} {msg}");
            }
        }
    }

    [TestFixture(HostOrServer.Host, ContactEventTypes.Default)]
    [TestFixture(HostOrServer.DAHost, ContactEventTypes.Default)]
    [TestFixture(HostOrServer.Host, ContactEventTypes.WithInfo)]
    [TestFixture(HostOrServer.DAHost, ContactEventTypes.WithInfo)]
    internal class RigidbodyContactEventManagerTests : IntegrationTestWithApproximation
    {
        protected override int NumberOfClients => 1;


        private GameObject m_RigidbodyContactEventManager;

        public enum ContactEventTypes
        {
            Default,
            WithInfo
        }

        private ContactEventTypes m_ContactEventType;
        private StringBuilder m_ErrorLogger = new StringBuilder();

        public RigidbodyContactEventManagerTests(HostOrServer hostOrServer, ContactEventTypes contactEventType) : base(hostOrServer)
        {
            m_ContactEventType = contactEventType;
        }

        protected override void OnCreatePlayerPrefab()
        {
            ContactEventTransformHelper.SessionOwnerSpawnPoint = GetRandomVector3(-4, -3);
            ContactEventTransformHelper.ClientSpawnPoint = GetRandomVector3(3, 4);
            if (m_ContactEventType == ContactEventTypes.Default)
            {
                var helper = m_PlayerPrefab.AddComponent<ContactEventTransformHelper>();
                helper.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            }
            else
            {
                var helperWithInfo = m_PlayerPrefab.AddComponent<ContactEventTransformHelperWithInfo>();
                helperWithInfo.AuthorityMode = NetworkTransform.AuthorityModes.Owner;
            }

            var rigidbody = m_PlayerPrefab.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
            rigidbody.mass = 5.0f;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
            var sphereCollider = m_PlayerPrefab.AddComponent<SphereCollider>();
            sphereCollider.radius = 0.5f;
            sphereCollider.providesContacts = true;

            var networkRigidbody = m_PlayerPrefab.AddComponent<NetworkRigidbody>();
            networkRigidbody.UseRigidBodyForMotion = true;
            networkRigidbody.AutoUpdateKinematicState = false;

            m_RigidbodyContactEventManager = new GameObject();
            m_RigidbodyContactEventManager.AddComponent<RigidbodyContactEventManager>();
        }



        private bool PlayersSpawnedInRightLocation()
        {
            var position = m_ServerNetworkManager.LocalClient.PlayerObject.transform.position;
            if (!Approximately(ContactEventTransformHelper.SessionOwnerSpawnPoint, position))
            {
                m_ErrorLogger.AppendLine($"Client-{m_ServerNetworkManager.LocalClientId} player position {position} does not match the assigned player position {ContactEventTransformHelper.SessionOwnerSpawnPoint}!");
                return false;
            }

            position = m_ClientNetworkManagers[0].LocalClient.PlayerObject.transform.position;
            if (!Approximately(ContactEventTransformHelper.ClientSpawnPoint, position))
            {
                m_ErrorLogger.AppendLine($"Client-{m_ClientNetworkManagers[0].LocalClientId} player position {position} does not match the assigned player position {ContactEventTransformHelper.ClientSpawnPoint}!");
                return false;
            }
            var playerObject = (NetworkObject)null;
            if (!m_ServerNetworkManager.SpawnManager.SpawnedObjects.ContainsKey(m_ClientNetworkManagers[0].LocalClient.PlayerObject.NetworkObjectId))
            {
                m_ErrorLogger.AppendLine($"Client-{m_ServerNetworkManager.LocalClientId} cannot find a local spawned instance of Client-{m_ClientNetworkManagers[0].LocalClientId}'s player object!");
                return false;
            }
            playerObject = m_ServerNetworkManager.SpawnManager.SpawnedObjects[m_ClientNetworkManagers[0].LocalClient.PlayerObject.NetworkObjectId];
            position = playerObject.transform.position;

            if (!Approximately(ContactEventTransformHelper.ClientSpawnPoint, position))
            {
                m_ErrorLogger.AppendLine($"Client-{m_ServerNetworkManager.LocalClientId} player position {position} for Client-{playerObject.OwnerClientId} does not match the assigned player position {ContactEventTransformHelper.ClientSpawnPoint}!");
                return false;
            }

            if (!m_ClientNetworkManagers[0].SpawnManager.SpawnedObjects.ContainsKey(m_ServerNetworkManager.LocalClient.PlayerObject.NetworkObjectId))
            {
                m_ErrorLogger.AppendLine($"Client-{m_ClientNetworkManagers[0].LocalClientId} cannot find a local spawned instance of Client-{m_ServerNetworkManager.LocalClientId}'s player object!");
                return false;
            }
            playerObject = m_ClientNetworkManagers[0].SpawnManager.SpawnedObjects[m_ServerNetworkManager.LocalClient.PlayerObject.NetworkObjectId];
            position = playerObject.transform.position;
            if (!Approximately(ContactEventTransformHelper.SessionOwnerSpawnPoint, playerObject.transform.position))
            {
                m_ErrorLogger.AppendLine($"Client-{m_ClientNetworkManagers[0].LocalClientId} player position {position} for Client-{playerObject.OwnerClientId} does not match the assigned player position {ContactEventTransformHelper.SessionOwnerSpawnPoint}!");
                return false;
            }
            return true;
        }


        [UnityTest]
        public IEnumerator TestContactEvents()
        {
            ContactEventTransformHelper.VerboseDebug = m_EnableVerboseDebug;

            m_PlayerPrefab.SetActive(false);
            m_ErrorLogger.Clear();
            // Validate all instances are spawned in the right location
            yield return WaitForConditionOrTimeOut(PlayersSpawnedInRightLocation);
            AssertOnTimeout($"Timed out waiting for all player instances to spawn in the corect location:\n {m_ErrorLogger}");
            m_ErrorLogger.Clear();

            var sessionOwnerPlayer = m_ContactEventType == ContactEventTypes.Default ? m_ServerNetworkManager.LocalClient.PlayerObject.GetComponent<ContactEventTransformHelper>() :
                m_ServerNetworkManager.LocalClient.PlayerObject.GetComponent<ContactEventTransformHelperWithInfo>();
            var clientPlayer = m_ContactEventType == ContactEventTypes.Default ? m_ClientNetworkManagers[0].LocalClient.PlayerObject.GetComponent<ContactEventTransformHelper>() :
                m_ClientNetworkManagers[0].LocalClient.PlayerObject.GetComponent<ContactEventTransformHelperWithInfo>();

            // Get both players to point towards each other
            sessionOwnerPlayer.Target = clientPlayer;
            clientPlayer.Target = sessionOwnerPlayer;

            sessionOwnerPlayer.SetHelperState(ContactEventTransformHelper.HelperStates.MoveForward);
            clientPlayer.SetHelperState(ContactEventTransformHelper.HelperStates.MoveForward);


            yield return WaitForConditionOrTimeOut(() => sessionOwnerPlayer.HadContactWith(clientPlayer) || clientPlayer.HadContactWith(sessionOwnerPlayer));
            AssertOnTimeout("Timed out waiting for a player to collide with another player!");

            clientPlayer.RegisterForContactEvents(false);
            sessionOwnerPlayer.RegisterForContactEvents(false);
            var otherPlayer = m_ContactEventType == ContactEventTypes.Default ? m_ServerNetworkManager.SpawnManager.SpawnedObjects[clientPlayer.NetworkObjectId].GetComponent<ContactEventTransformHelper>() :
                m_ServerNetworkManager.SpawnManager.SpawnedObjects[clientPlayer.NetworkObjectId].GetComponent<ContactEventTransformHelperWithInfo>();
            otherPlayer.RegisterForContactEvents(false);
            otherPlayer = m_ContactEventType == ContactEventTypes.Default ? m_ClientNetworkManagers[0].SpawnManager.SpawnedObjects[sessionOwnerPlayer.NetworkObjectId].GetComponent<ContactEventTransformHelper>() :
                m_ClientNetworkManagers[0].SpawnManager.SpawnedObjects[sessionOwnerPlayer.NetworkObjectId].GetComponent<ContactEventTransformHelperWithInfo>();
            otherPlayer.RegisterForContactEvents(false);

            Object.Destroy(m_RigidbodyContactEventManager);
            m_RigidbodyContactEventManager = null;
        }

        protected override IEnumerator OnTearDown()
        {
            // In case of a test failure
            if (m_RigidbodyContactEventManager)
            {
                Object.Destroy(m_RigidbodyContactEventManager);
                m_RigidbodyContactEventManager = null;
            }

            return base.OnTearDown();
        }
    }
}
#endif // COM_UNITY_MODULES_PHYSICS
