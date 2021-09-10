using System;
using UnityEngine;

namespace Unity.Netcode.Components
{
    /// <summary>
    /// A prototype component for syncing transforms
    /// </summary>
    [AddComponentMenu("Netcode/" + nameof(NetworkTransform))]
    public class NetworkTransform : NetworkBehaviour
    {
        internal struct NetworkState : INetworkSerializable
        {
            internal const int InLocalSpaceBit = 0;
            internal const int PositionXBit = 1;
            internal const int PositionYBit = 2;
            internal const int PositionZBit = 3;
            internal const int RotAngleXBit = 4;
            internal const int RotAngleYBit = 5;
            internal const int RotAngleZBit = 6;
            internal const int ScaleXBit = 7;
            internal const int ScaleYBit = 8;
            internal const int ScaleZBit = 9;
            // 10-15: <unused>
            public ushort Bitset;

            public bool InLocalSpace
            {
                get => (Bitset & (1 << InLocalSpaceBit)) != 0;
                set => Bitset |= (ushort)((value ? 1 : 0) << InLocalSpaceBit);
            }
            // Position
            public bool HasPositionX
            {
                get => (Bitset & (1 << PositionXBit)) != 0;
                set => Bitset |= (ushort)((value ? 1 : 0) << PositionXBit);
            }
            public bool HasPositionY
            {
                get => (Bitset & (1 << PositionYBit)) != 0;
                set => Bitset |= (ushort)((value ? 1 : 0) << PositionYBit);
            }
            public bool HasPositionZ
            {
                get => (Bitset & (1 << PositionZBit)) != 0;
                set => Bitset |= (ushort)((value ? 1 : 0) << PositionZBit);
            }
            // RotAngles
            public bool HasRotAngleX
            {
                get => (Bitset & (1 << RotAngleXBit)) != 0;
                set => Bitset |= (ushort)((value ? 1 : 0) << RotAngleXBit);
            }
            public bool HasRotAngleY
            {
                get => (Bitset & (1 << RotAngleYBit)) != 0;
                set => Bitset |= (ushort)((value ? 1 : 0) << RotAngleYBit);
            }
            public bool HasRotAngleZ
            {
                get => (Bitset & (1 << RotAngleZBit)) != 0;
                set => Bitset |= (ushort)((value ? 1 : 0) << RotAngleZBit);
            }
            // Scale
            public bool HasScaleX
            {
                get => (Bitset & (1 << ScaleXBit)) != 0;
                set => Bitset |= (ushort)((value ? 1 : 0) << ScaleXBit);
            }
            public bool HasScaleY
            {
                get => (Bitset & (1 << ScaleYBit)) != 0;
                set => Bitset |= (ushort)((value ? 1 : 0) << ScaleYBit);
            }
            public bool HasScaleZ
            {
                get => (Bitset & (1 << ScaleZBit)) != 0;
                set => Bitset |= (ushort)((value ? 1 : 0) << ScaleZBit);
            }

            public float PositionX, PositionY, PositionZ;
            public float RotAngleX, RotAngleY, RotAngleZ;
            public float ScaleX, ScaleY, ScaleZ;

            public void NetworkSerialize(NetworkSerializer serializer)
            {
                // InLocalSpace + HasXXX Bits
                serializer.Serialize(ref Bitset);
                // Position Values
                if (HasPositionX)
                {
                    serializer.Serialize(ref PositionX);
                }
                if (HasPositionY)
                {
                    serializer.Serialize(ref PositionY);
                }
                if (HasPositionZ)
                {
                    serializer.Serialize(ref PositionZ);
                }
                // RotAngle Values
                if (HasRotAngleX)
                {
                    serializer.Serialize(ref RotAngleX);
                }
                if (HasRotAngleY)
                {
                    serializer.Serialize(ref RotAngleY);
                }
                if (HasRotAngleZ)
                {
                    serializer.Serialize(ref RotAngleZ);
                }
                // Scale Values
                if (HasScaleX)
                {
                    serializer.Serialize(ref ScaleX);
                }
                if (HasScaleY)
                {
                    serializer.Serialize(ref ScaleY);
                }
                if (HasScaleZ)
                {
                    serializer.Serialize(ref ScaleZ);
                }
            }
        }

        public bool SyncPositionX = true, SyncPositionY = true, SyncPositionZ = true;
        public bool SyncRotAngleX = true, SyncRotAngleY = true, SyncRotAngleZ = true;
        public bool SyncScaleX = true, SyncScaleY = true, SyncScaleZ = true;

        public float PositionThreshold, RotAngleThreshold, ScaleThreshold;

        /// <summary>
        /// Sets whether this transform should sync in local space or in world space.
        /// This is important to set since reparenting this transform could have issues,
        /// if using world position (depending on who gets synced first: the parent or the child)
        /// Having a child always at position 0,0,0 for example will have less possibilities of desync than when using world positions
        /// </summary>
        [Tooltip("Sets whether this transform should sync in local space or in world space")]
        public bool InLocalSpace = false;

        // todo: revisit after MTT-876
        public bool Interpolate = true;

        /// <summary>
        /// The base amount of sends per seconds to use when range is disabled
        /// </summary>
        [SerializeField, Range(0, 120), Tooltip("The base amount of sends per seconds to use when range is disabled")]
        public float FixedSendsPerSecond = 30f;

        private Transform m_Transform; // cache the transform component to reduce unnecessary bounce between managed and native
        internal NetworkState LocalNetworkState;
        internal readonly NetworkVariable<NetworkState> ReplNetworkState = new NetworkVariable<NetworkState>(new NetworkState());
        internal NetworkState PrevNetworkState;

        // updates `NetworkState` properties if they need to and returns a `bool` indicating whether or not there was any changes made
        // returned boolean would be useful to change encapsulating `NetworkVariable<NetworkState>`'s dirty state, e.g. ReplNetworkState.SetDirty(isDirty);
        internal bool UpdateNetworkState(ref NetworkState networkState)
        {
            var position = InLocalSpace ? m_Transform.localPosition : m_Transform.position;
            var rotAngles = InLocalSpace ? m_Transform.localEulerAngles : m_Transform.eulerAngles;
            var scale = InLocalSpace ? m_Transform.localScale : m_Transform.lossyScale;

            bool isDirty = false;

            if (InLocalSpace != networkState.InLocalSpace)
            {
                networkState.InLocalSpace = InLocalSpace;
                isDirty |= true;
            }

            if (SyncPositionX &&
                Mathf.Abs(networkState.PositionX - position.x) >= PositionThreshold &&
                !Mathf.Approximately(networkState.PositionX, position.x))
            {
                networkState.PositionX = position.x;
                networkState.HasPositionX = true;
                isDirty |= true;
            }

            if (SyncPositionY &&
                Mathf.Abs(networkState.PositionY - position.y) >= PositionThreshold &&
                !Mathf.Approximately(networkState.PositionY, position.y))
            {
                networkState.PositionY = position.y;
                networkState.HasPositionY = true;
                isDirty |= true;
            }

            if (SyncPositionZ &&
                Mathf.Abs(networkState.PositionZ - position.z) >= PositionThreshold &&
                !Mathf.Approximately(networkState.PositionZ, position.z))
            {
                networkState.PositionZ = position.z;
                networkState.HasPositionZ = true;
                isDirty |= true;
            }

            if (SyncRotAngleX &&
                Mathf.Abs(networkState.RotAngleX - rotAngles.x) >= RotAngleThreshold &&
                !Mathf.Approximately(networkState.RotAngleX, rotAngles.x))
            {
                networkState.RotAngleX = rotAngles.x;
                networkState.HasRotAngleX = true;
                isDirty |= true;
            }

            if (SyncRotAngleY &&
                Mathf.Abs(networkState.RotAngleY - rotAngles.y) >= RotAngleThreshold &&
                !Mathf.Approximately(networkState.RotAngleY, rotAngles.y))
            {
                networkState.RotAngleY = rotAngles.y;
                networkState.HasRotAngleY = true;
                isDirty |= true;
            }

            if (SyncRotAngleZ &&
                Mathf.Abs(networkState.RotAngleZ - rotAngles.z) >= RotAngleThreshold &&
                !Mathf.Approximately(networkState.RotAngleZ, rotAngles.z))
            {
                networkState.RotAngleZ = rotAngles.z;
                networkState.HasRotAngleZ = true;
                isDirty |= true;
            }

            if (SyncScaleX &&
                Mathf.Abs(networkState.ScaleX - scale.x) >= ScaleThreshold &&
                !Mathf.Approximately(networkState.ScaleX, scale.x))
            {
                networkState.ScaleX = scale.x;
                networkState.HasScaleX = true;
                isDirty |= true;
            }

            if (SyncScaleY &&
                Mathf.Abs(networkState.ScaleY - scale.y) >= ScaleThreshold &&
                !Mathf.Approximately(networkState.ScaleY, scale.y))
            {
                networkState.ScaleY = scale.y;
                networkState.HasScaleY = true;
                isDirty |= true;
            }

            if (SyncScaleZ &&
                Mathf.Abs(networkState.ScaleZ - scale.z) >= ScaleThreshold &&
                !Mathf.Approximately(networkState.ScaleZ, scale.z))
            {
                networkState.ScaleZ = scale.z;
                networkState.HasScaleZ = true;
                isDirty |= true;
            }

            return isDirty;
        }

        // TODO: temporary! the function body below probably needs to be rewritten later with interpolation in mind
        internal void ApplyNetworkState(NetworkState networkState)
        {
            PrevNetworkState = networkState;

            var position = InLocalSpace ? m_Transform.localPosition : m_Transform.position;
            var rotAngles = InLocalSpace ? m_Transform.localEulerAngles : m_Transform.eulerAngles;
            var scale = InLocalSpace ? m_Transform.localScale : m_Transform.lossyScale;

            // InLocalSpace Read
            InLocalSpace = networkState.InLocalSpace;
            // Position Read
            if (networkState.HasPositionX)
            {
                position.x = networkState.PositionX;
            }
            if (networkState.HasPositionY)
            {
                position.y = networkState.PositionY;
            }
            if (networkState.HasPositionZ)
            {
                position.z = networkState.PositionZ;
            }
            // RotAngles Read
            if (networkState.HasRotAngleX)
            {
                rotAngles.x = networkState.RotAngleX;
            }
            if (networkState.HasRotAngleY)
            {
                rotAngles.y = networkState.RotAngleY;
            }
            if (networkState.HasRotAngleZ)
            {
                rotAngles.z = networkState.RotAngleZ;
            }
            // Scale Read
            if (networkState.HasScaleX)
            {
                scale.x = networkState.ScaleX;
            }
            if (networkState.HasScaleY)
            {
                scale.y = networkState.ScaleY;
            }
            if (networkState.HasScaleZ)
            {
                scale.z = networkState.ScaleZ;
            }

            // Position Apply
            if (networkState.HasPositionX || networkState.HasPositionY || networkState.HasPositionZ)
            {
                if (InLocalSpace)
                {
                    m_Transform.localPosition = position;
                }
                else
                {
                    m_Transform.position = position;
                }
            }
            // RotAngles Apply
            if (networkState.HasRotAngleX || networkState.HasRotAngleY || networkState.HasRotAngleZ)
            {
                if (InLocalSpace)
                {
                    m_Transform.localEulerAngles = rotAngles;
                }
                else
                {
                    m_Transform.eulerAngles = rotAngles;
                }
            }
            // Scale Apply
            if (networkState.HasScaleX || networkState.HasScaleY || networkState.HasScaleZ)
            {
                if (InLocalSpace)
                {
                    m_Transform.localScale = scale;
                }
                else
                {
                    m_Transform.localScale = Vector3.one;
                    var lossyScale = m_Transform.lossyScale;
                    m_Transform.localScale = new Vector3(networkState.ScaleX / lossyScale.x, networkState.ScaleY / lossyScale.y, networkState.ScaleZ / lossyScale.z);
                }
            }
        }

        private void OnNetworkStateChanged(NetworkState oldState, NetworkState newState)
        {
            // skip if we're not spawned, or the server, or the owner of this object making the change
            if (!NetworkObject.IsSpawned || IsServer || IsOwner)
            {
                return;
            }

            // as a non-server, non-owner client, apply networkstate locally to make transform move
            ApplyNetworkState(newState);
        }

        private void Awake()
        {
            m_Transform = transform;
            ReplNetworkState.OnValueChanged += OnNetworkStateChanged;
        }

        private void OnDestroy()
        {
            ReplNetworkState.OnValueChanged -= OnNetworkStateChanged;
        }

        [ServerRpc(Delivery = RpcDelivery.Reliable)]
        private void SubmitNetworkStateServerRpc(NetworkState networkState)
        {
            // as a server, apply whatever networkstate owner client sent to us, to make NetworkTransform move locally on the server
            ApplyNetworkState(networkState);

            // as a server, update netvar<networkstate> to cause it to be replicated down to clients
            UpdateNetworkVariable(ref networkState);
        }

        private void FixedUpdate()
        {
            if (!NetworkObject.IsSpawned)
            {
                return;
            }

            // if I'm the owner client
            if (NetworkObject.IsOwner)
            {
                if (UpdateNetworkState(ref LocalNetworkState))
                {
                    if (IsServer)
                    {
                        UpdateNetworkVariable(ref LocalNetworkState);
                    }
                    else
                    {
                        SubmitNetworkStateServerRpc(LocalNetworkState);
                    }
                }
            }
        }

        private void UpdateNetworkVariable(ref NetworkState networkState)
        {
            ReplNetworkState.Value = networkState;
            ReplNetworkState.SetDirty(true);
        }

        /// <summary>
        /// Teleports the transform to the given values without interpolating
        /// </summary>
        public void Teleport(Vector3 newPosition, Quaternion newRotation, Vector3 newScale)
        {
            throw new NotImplementedException(); // TODO MTT-769
        }
    }
}
