using Unity.Netcode;
using UnityEngine;

namespace TestProject.ManualTests
{
    public class SessionSynchronizedTest : NetworkBehaviour
    {
        public static SessionSynchronizedTest FirstObject;
        public static SessionSynchronizedTest SecondObject;
        public SessionSynchronizedTest ObjectForServerToReference;
        public NetworkVariable<NetworkBehaviourReference> OtherObject = new NetworkVariable<NetworkBehaviourReference>();
        public bool IsFirstObject;
        public bool OnInSceneObjectsSpawnedInvoked;

        [Range(1, 1000)]
        public int ValueToCheck;

        public int OtherValueObtained;

        public SessionSynchronizedTest ClientSideReferencedBehaviour;

        protected override void OnNetworkPreSpawn(ref NetworkManager networkManager)
        {
            if (!networkManager.IsServer)
            {
                if (IsFirstObject)
                {
                    FirstObject = this;
                }
                else
                {
                    SecondObject = this;
                }
            }
            base.OnNetworkPreSpawn(ref networkManager);
        }

        protected override void OnNetworkSessionSynchronized()
        {
            if (!HasAuthority)
            {
                OtherObject.Value.TryGet(out ClientSideReferencedBehaviour, NetworkManager);
                OtherValueObtained = ClientSideReferencedBehaviour.ValueToCheck;
            }
            base.OnNetworkSessionSynchronized();
        }

        /// <summary>
        /// Tests the in-scene objects spawned method gets invoked after a scene has been loaded and the associated in-scene placed NetworkObjects
        /// have been spawned.
        /// </summary>
        protected override void OnInSceneObjectsSpawned()
        {
            if (HasAuthority)
            {
                OtherObject.Value = new NetworkBehaviourReference(ObjectForServerToReference);
            }
            OnInSceneObjectsSpawnedInvoked = true;
            base.OnInSceneObjectsSpawned();
        }
    }
}
