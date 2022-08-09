using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Netcode.Components
{
    /// <summary>
    /// A component for syncing transforms.
    /// NetworkTransform will read the underlying transform and replicate it to clients.
    /// The replicated value will be automatically be interpolated (if active) and applied to the underlying GameObject's transform.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Netcode/" + nameof(NetworkTransform))]
    [DefaultExecutionOrder(100000)] // this is needed to catch the update time after the transform was updated by user scripts
    public class NetworkTransform : NetworkBehaviour
    {
        /// <summary>
        /// The default position change threshold value.
        /// Any changes above this threshold will be replicated.
        /// </summary>
        public const float PositionThresholdDefault = 0.001f;

        /// <summary>
        /// The default rotation angle change threshold value.
        /// Any changes above this threshold will be replicated.
        /// </summary>
        public const float RotAngleThresholdDefault = 0.01f;

        /// <summary>
        /// The default scale change threshold value.
        /// Any changes above this threshold will be replicated.
        /// </summary>
        public const float ScaleThresholdDefault = 0.01f;

        /// <summary>
        /// The handler delegate type that takes client requested changes and returns resulting changes handled by the server.
        /// </summary>
        /// <param name="pos">The position requested by the client.</param>
        /// <param name="rot">The rotation requested by the client.</param>
        /// <param name="scale">The scale requested by the client.</param>
        /// <returns>The resulting position, rotation and scale changes after handling.</returns>
        public delegate (Vector3 pos, Quaternion rotOut, Vector3 scale) OnClientRequestChangeDelegate(Vector3 pos, Quaternion rot, Vector3 scale);

        /// <summary>
        /// The handler that gets invoked when server receives a change from a client.
        /// This handler would be useful for server to modify pos/rot/scale before applying client's request.
        /// </summary>
        public OnClientRequestChangeDelegate OnClientRequestChange;

        internal struct NetworkTransformState : INetworkSerializable
        {
            private const int k_InLocalSpaceBit = 0;
            private const int k_PositionXBit = 1;
            private const int k_PositionYBit = 2;
            private const int k_PositionZBit = 3;
            private const int k_RotAngleXBit = 4;
            private const int k_RotAngleYBit = 5;
            private const int k_RotAngleZBit = 6;
            private const int k_ScaleXBit = 7;
            private const int k_ScaleYBit = 8;
            private const int k_ScaleZBit = 9;
            private const int k_TeleportingBit = 10;
            // 11-15: <unused>

            private ushort m_Bitset;

            internal bool InLocalSpace
            {
                get => (m_Bitset & (1 << k_InLocalSpaceBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_InLocalSpaceBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_InLocalSpaceBit)); }
                }
            }

            // Position
            internal bool HasPositionX
            {
                get => (m_Bitset & (1 << k_PositionXBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_PositionXBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_PositionXBit)); }
                }
            }

            internal bool HasPositionY
            {
                get => (m_Bitset & (1 << k_PositionYBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_PositionYBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_PositionYBit)); }
                }
            }

            internal bool HasPositionZ
            {
                get => (m_Bitset & (1 << k_PositionZBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_PositionZBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_PositionZBit)); }
                }
            }

            // RotAngles
            internal bool HasRotAngleX
            {
                get => (m_Bitset & (1 << k_RotAngleXBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_RotAngleXBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_RotAngleXBit)); }
                }
            }

            internal bool HasRotAngleY
            {
                get => (m_Bitset & (1 << k_RotAngleYBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_RotAngleYBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_RotAngleYBit)); }
                }
            }

            internal bool HasRotAngleZ
            {
                get => (m_Bitset & (1 << k_RotAngleZBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_RotAngleZBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_RotAngleZBit)); }
                }
            }

            // Scale
            internal bool HasScaleX
            {
                get => (m_Bitset & (1 << k_ScaleXBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_ScaleXBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_ScaleXBit)); }
                }
            }

            internal bool HasScaleY
            {
                get => (m_Bitset & (1 << k_ScaleYBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_ScaleYBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_ScaleYBit)); }
                }
            }

            internal bool HasScaleZ
            {
                get => (m_Bitset & (1 << k_ScaleZBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_ScaleZBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_ScaleZBit)); }
                }
            }

            internal bool IsTeleportingNextFrame
            {
                get => (m_Bitset & (1 << k_TeleportingBit)) != 0;
                set
                {
                    if (value) { m_Bitset = (ushort)(m_Bitset | (1 << k_TeleportingBit)); }
                    else { m_Bitset = (ushort)(m_Bitset & ~(1 << k_TeleportingBit)); }
                }
            }

            internal float PositionX, PositionY, PositionZ;
            internal float RotAngleX, RotAngleY, RotAngleZ;
            internal float ScaleX, ScaleY, ScaleZ;
            internal double SentTime;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref SentTime);
                // InLocalSpace + HasXXX Bits
                serializer.SerializeValue(ref m_Bitset);
                // Position Values
                if (HasPositionX)
                {
                    serializer.SerializeValue(ref PositionX);
                }

                if (HasPositionY)
                {
                    serializer.SerializeValue(ref PositionY);
                }

                if (HasPositionZ)
                {
                    serializer.SerializeValue(ref PositionZ);
                }

                // RotAngle Values
                if (HasRotAngleX)
                {
                    serializer.SerializeValue(ref RotAngleX);
                }

                if (HasRotAngleY)
                {
                    serializer.SerializeValue(ref RotAngleY);
                }

                if (HasRotAngleZ)
                {
                    serializer.SerializeValue(ref RotAngleZ);
                }

                // Scale Values
                if (HasScaleX)
                {
                    serializer.SerializeValue(ref ScaleX);
                }

                if (HasScaleY)
                {
                    serializer.SerializeValue(ref ScaleY);
                }

                if (HasScaleZ)
                {
                    serializer.SerializeValue(ref ScaleZ);
                }
            }
        }

        /// <summary>
        /// Whether or not x component of position will be replicated
        /// </summary>
        public bool SyncPositionX = true;
        /// <summary>
        /// Whether or not y component of position will be replicated
        /// </summary>
        public bool SyncPositionY = true;
        /// <summary>
        /// Whether or not z component of position will be replicated
        /// </summary>
        public bool SyncPositionZ = true;
        /// <summary>
        /// Whether or not x component of rotation will be replicated
        /// </summary>
        public bool SyncRotAngleX = true;
        /// <summary>
        /// Whether or not y component of rotation will be replicated
        /// </summary>
        public bool SyncRotAngleY = true;
        /// <summary>
        /// Whether or not z component of rotation will be replicated
        /// </summary>
        public bool SyncRotAngleZ = true;
        /// <summary>
        /// Whether or not x component of scale will be replicated
        /// </summary>
        public bool SyncScaleX = true;
        /// <summary>
        /// Whether or not y component of scale will be replicated
        /// </summary>
        public bool SyncScaleY = true;
        /// <summary>
        /// Whether or not z component of scale will be replicated
        /// </summary>
        public bool SyncScaleZ = true;

        /// <summary>
        /// The current position threshold value
        /// Any changes to the position that exceeds the current threshold value will be replicated
        /// </summary>
        public float PositionThreshold = PositionThresholdDefault;

        /// <summary>
        /// The current rotation threshold value
        /// Any changes to the rotation that exceeds the current threshold value will be replicated
        /// Minimum Value: 0.001
        /// Maximum Value: 360.0
        /// </summary>
        [Range(0.001f, 360.0f)]
        public float RotAngleThreshold = RotAngleThresholdDefault;

        /// <summary>
        /// The current scale threshold value
        /// Any changes to the scale that exceeds the current threshold value will be replicated
        /// </summary>
        public float ScaleThreshold = ScaleThresholdDefault;


        /// <summary>
        /// Sets whether this transform should sync in local space or in world space.
        /// This is important to set since reparenting this transform could have issues,
        /// if using world position (depending on who gets synced first: the parent or the child)
        /// Having a child always at position 0,0,0 for example will have less possibilities of desync than when using world positions
        /// </summary>
        [Tooltip("Sets whether this transform should sync in local space or in world space")]
        public bool InLocalSpace = false;

        /// <summary>
        /// When enabled (default) interpolation is applied and when disabled no interpolation is applied
        /// </summary>
        /// <remarks>
        /// This should only be changed by the authoritative side during runtime. Non-authoritative changes
        /// will be overridden upon the next <see cref="NetworkTransform"></see> state update.
        /// </remarks>
        public bool Interpolate = true;

        /// <summary>
        /// Used to determine who can write to this transform. Server only for this transform.
        /// Changing this value alone in a child implementation will not allow you to create a NetworkTransform which can be written to by clients. See the ClientNetworkTransform Sample
        /// in the package samples for how to implement a NetworkTransform with client write support.
        /// If using different values, please use RPCs to write to the server. Netcode doesn't support client side network variable writing
        /// </summary>
        // This is public to make sure that users don't depend on this IsClient && IsOwner check in their code. If this logic changes in the future, we can make it invisible here
        // TODO: With recent updates 08-2022, this should be changed to a private set.
        public bool CanCommitToTransform { get; protected set; }

        /// <summary>
        /// Internally used by <see cref="NetworkTransform"/> to keep track of whether this <see cref="NetworkBehaviour"/> derived class instance
        /// was instantiated on the server side or not.
        /// </summary>
        protected bool m_CachedIsServer;

        /// <summary>
        /// Internally used by <see cref="NetworkTransform"/> to keep track of the <see cref="NetworkManager"/> instance assigned to this
        /// this <see cref="NetworkBehaviour"/> derived class instance.
        /// </summary>
        protected NetworkManager m_CachedNetworkManager;


        /// <summary>
        /// We have two internal NetworkVariables.
        /// One for server authoritative and one for "client/owner" authoritative.
        /// </summary>
        private readonly NetworkVariable<NetworkTransformState> m_ReplicatedNetworkStateServer = new NetworkVariable<NetworkTransformState>(new NetworkTransformState(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        private readonly NetworkVariable<NetworkTransformState> m_ReplicatedNetworkStateOwner = new NetworkVariable<NetworkTransformState>(new NetworkTransformState(), NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Owner);


        internal NetworkVariable<NetworkTransformState> GetReplicatedNetworkState()
        {
            if (!IsServerAuthoritative())
            {
                return m_ReplicatedNetworkStateOwner;
            }

            return m_ReplicatedNetworkStateServer;
        }

        private NetworkTransformState m_LocalAuthoritativeNetworkState;

        private bool m_HasSentLastValue = false; // used to send one last value, so clients can make the difference between lost replication data (clients extrapolate) and no more data to send.


        private BufferedLinearInterpolator<float> m_PositionXInterpolator; // = new BufferedLinearInterpolatorFloat();
        private BufferedLinearInterpolator<float> m_PositionYInterpolator; // = new BufferedLinearInterpolatorFloat();
        private BufferedLinearInterpolator<float> m_PositionZInterpolator; // = new BufferedLinearInterpolatorFloat();
        private BufferedLinearInterpolator<Quaternion> m_RotationInterpolator; // = new BufferedLinearInterpolatorQuaternion(); // rotation is a single Quaternion since each euler axis will affect the quaternion's final value
        private BufferedLinearInterpolator<float> m_ScaleXInterpolator; // = new BufferedLinearInterpolatorFloat();
        private BufferedLinearInterpolator<float> m_ScaleYInterpolator; // = new BufferedLinearInterpolatorFloat();
        private BufferedLinearInterpolator<float> m_ScaleZInterpolator; // = new BufferedLinearInterpolatorFloat();
        private readonly List<BufferedLinearInterpolator<float>> m_AllFloatInterpolators = new List<BufferedLinearInterpolator<float>>(6);

        private Transform m_Transform; // cache the transform component to reduce unnecessary bounce between managed and native
        private int m_LastSentTick;
        private NetworkTransformState m_LastSentState;

        internal NetworkTransformState GetLastSentState()
        {
            return m_LastSentState;
        }

        /// <summary>
        /// Tries updating the server authoritative transform, only if allowed.
        /// If this called server side, this will commit directly.
        /// If no update is needed, nothing will be sent. This method should still be called every update, it'll self manage when it should and shouldn't send
        /// </summary>
        /// <param name="transformToCommit"></param>
        /// <param name="dirtyTime"></param>
        protected void TryCommitTransformToServer(Transform transformToCommit, double dirtyTime)
        {
            TryCommitTransform(transformToCommit, dirtyTime);
        }

        /// <summary>
        /// Authoritative side only
        /// This will try to send/commit the current transform delta states (if any)
        /// </summary>
        /// <remarks>
        /// It is not recommended to use this method in a derived class
        /// </remarks>
        protected void TryCommitTransform(Transform transformToCommit, double dirtyTime)
        {
            if (!CanCommitToTransform)
            {
                Debug.LogError($"[{name}] is trying to commit the transform without authority!");
                return;
            }
            var isDirty = ApplyTransformToNetworkState(ref m_LocalAuthoritativeNetworkState, dirtyTime, transformToCommit);
            TryCommit(isDirty);
            // Authority will reset this the same frame.
            // (non-authority doesn't need to reset this value as it is updated by authority)
            m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame = false;
        }

        private void TryCommitValues(Vector3 position, Vector3 rotation, Vector3 scale, double dirtyTime)
        {
            var isDirty = ApplyTransformToNetworkStateWithInfo(ref m_LocalAuthoritativeNetworkState, dirtyTime, position, rotation, scale);
            TryCommit(isDirty.isDirty);
        }

        private void TryCommit(bool isDirty)
        {
            void Send(NetworkTransformState stateToSend)
            {
                if (CanCommitToTransform)
                {
                    // server RPC takes a few frames to execute server side, we want this to execute immediately
                    CommitLocallyAndReplicate(stateToSend);
                }
                else
                {
                    CommitTransformServerRpc(stateToSend);
                }
            }

            // if dirty, send
            // if not dirty anymore, but hasn't sent last value for limiting extrapolation, still set isDirty
            // if not dirty and has already sent last value, don't do anything
            // extrapolation works by using last two values. if it doesn't receive anything anymore, it'll continue to extrapolate.
            // This is great in case there's message loss, not so great if we just don't have new values to send.
            // the following will send one last "copied" value so unclamped interpolation tries to extrapolate between two identical values, effectively
            // making it immobile.
            if (isDirty)
            {
                Send(m_LocalAuthoritativeNetworkState);
                m_HasSentLastValue = false;
                m_LastSentTick = m_CachedNetworkManager.LocalTime.Tick;
                m_LastSentState = m_LocalAuthoritativeNetworkState;
            }
            else if (!m_HasSentLastValue && m_CachedNetworkManager.LocalTime.Tick >= m_LastSentTick + 1) // check for state.IsDirty since update can happen more than once per tick. No need for client, RPCs will just queue up
            {
                m_LastSentState.IsTeleportingNextFrame = false; // This is required here
                m_LastSentState.SentTime = m_CachedNetworkManager.LocalTime.Time; // time 1+ tick later
                Send(m_LastSentState);
                m_HasSentLastValue = true;
            }
        }

        [ServerRpc(RequireOwnership = false)]
        private void CommitTransformServerRpc(NetworkTransformState networkState, ServerRpcParams serverParams = default)
        {
            if (serverParams.Receive.SenderClientId == OwnerClientId) // RPC call when not authorized to write could happen during the RTT interval during which a server's ownership change hasn't reached the client yet
            {
                CommitLocallyAndReplicate(networkState);
            }
        }

        private void CommitLocallyAndReplicate(NetworkTransformState networkState)
        {
            GetReplicatedNetworkState().Value = networkState;
        }

        private void ResetInterpolatedStateToCurrentAuthoritativeState()
        {
            var serverTime = NetworkManager.ServerTime.Time;
            m_PositionXInterpolator.ResetTo(m_LocalAuthoritativeNetworkState.PositionX, serverTime);
            m_PositionYInterpolator.ResetTo(m_LocalAuthoritativeNetworkState.PositionY, serverTime);
            m_PositionZInterpolator.ResetTo(m_LocalAuthoritativeNetworkState.PositionZ, serverTime);

            m_RotationInterpolator.ResetTo(Quaternion.Euler(m_LocalAuthoritativeNetworkState.RotAngleX, m_LocalAuthoritativeNetworkState.RotAngleY, m_LocalAuthoritativeNetworkState.RotAngleZ), serverTime);

            m_ScaleXInterpolator.ResetTo(m_LocalAuthoritativeNetworkState.ScaleX, serverTime);
            m_ScaleYInterpolator.ResetTo(m_LocalAuthoritativeNetworkState.ScaleY, serverTime);
            m_ScaleZInterpolator.ResetTo(m_LocalAuthoritativeNetworkState.ScaleZ, serverTime);
        }

        /// <summary>
        /// Will apply the transform to the LocalAuthoritativeNetworkState and get detailed isDirty information returned.
        /// </summary>
        /// <param name="transform">transform to apply</param>
        /// <returns>bool isDirty, bool isPositionDirty, bool isRotationDirty, bool isScaleDirty</returns>
        internal (bool isDirty, bool isPositionDirty, bool isRotationDirty, bool isScaleDirty) ApplyLocalNetworkState(Transform transform)
        {
            return ApplyTransformToNetworkStateWithInfo(ref m_LocalAuthoritativeNetworkState, m_CachedNetworkManager.LocalTime.Time, transform);
        }

        // updates `NetworkState` properties if they need to and returns a `bool` indicating whether or not there was any changes made
        // returned boolean would be useful to change encapsulating `NetworkVariable<NetworkState>`'s dirty state, e.g. ReplNetworkState.SetDirty(isDirty);
        internal bool ApplyTransformToNetworkState(ref NetworkTransformState networkState, double dirtyTime, Transform transformToUse)
        {
            return ApplyTransformToNetworkStateWithInfo(ref networkState, dirtyTime, transformToUse).isDirty;
        }

        private (bool isDirty, bool isPositionDirty, bool isRotationDirty, bool isScaleDirty) ApplyTransformToNetworkStateWithInfo(ref NetworkTransformState networkState, double dirtyTime, Transform transformToUse)
        {
            var position = InLocalSpace ? transformToUse.localPosition : transformToUse.position;
            var rotAngles = InLocalSpace ? transformToUse.localEulerAngles : transformToUse.eulerAngles;
            var scale = transformToUse.localScale;
            return ApplyTransformToNetworkStateWithInfo(ref networkState, dirtyTime, position, rotAngles, scale);
        }

        private (bool isDirty, bool isPositionDirty, bool isRotationDirty, bool isScaleDirty) ApplyTransformToNetworkStateWithInfo(ref NetworkTransformState networkState, double dirtyTime, Vector3 position, Vector3 rotAngles, Vector3 scale)
        {
            var isDirty = false;
            var isPositionDirty = false;
            var isRotationDirty = false;
            var isScaleDirty = false;

            // hasPositionZ set to false when it should be true?

            if (InLocalSpace != networkState.InLocalSpace)
            {
                networkState.InLocalSpace = InLocalSpace;
                isDirty = true;
            }

            // we assume that if x, y or z are dirty then we'll have to send all 3 anyway, so for efficiency
            //  we skip doing the (quite expensive) Math.Approximately() and check against PositionThreshold
            //  this still is overly costly and could use more improvements.
            //
            // (ditto for scale components)
            if (SyncPositionX)
            {
                if (Mathf.Abs(networkState.PositionX - position.x) > PositionThreshold || networkState.IsTeleportingNextFrame)
                {
                    networkState.PositionX = position.x;
                    networkState.HasPositionX = true;
                    isPositionDirty = true;
                }
                else
                {
                    networkState.HasPositionX = false;
                }
            }

            if (SyncPositionY)
            {
                if (Mathf.Abs(networkState.PositionY - position.y) > PositionThreshold || networkState.IsTeleportingNextFrame)
                {
                    networkState.PositionY = position.y;
                    networkState.HasPositionY = true;
                    isPositionDirty = true;
                }
                else
                {
                    networkState.HasPositionY = false;
                }
            }

            if (SyncPositionZ)
            {
                if (Mathf.Abs(networkState.PositionZ - position.z) > PositionThreshold || networkState.IsTeleportingNextFrame)
                {
                    networkState.PositionZ = position.z;
                    networkState.HasPositionZ = true;
                    isPositionDirty = true;
                }
                else
                {
                    networkState.HasPositionZ = false;
                }
            }

            if (SyncRotAngleX)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(networkState.RotAngleX, rotAngles.x)) > RotAngleThreshold || networkState.IsTeleportingNextFrame)
                {
                    networkState.RotAngleX = rotAngles.x;
                    networkState.HasRotAngleX = true;
                    isRotationDirty = true;
                }
                else
                {
                    networkState.HasRotAngleX = false;
                }
            }

            if (SyncRotAngleY)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(networkState.RotAngleY, rotAngles.y)) > RotAngleThreshold || networkState.IsTeleportingNextFrame)
                {
                    networkState.RotAngleY = rotAngles.y;
                    networkState.HasRotAngleY = true;
                    isRotationDirty = true;
                }
                else
                {
                    networkState.HasRotAngleY = false;
                }
            }

            if (SyncRotAngleZ)
            {
                if (Mathf.Abs(Mathf.DeltaAngle(networkState.RotAngleZ, rotAngles.z)) > RotAngleThreshold || networkState.IsTeleportingNextFrame)
                {
                    networkState.RotAngleZ = rotAngles.z;
                    networkState.HasRotAngleZ = true;
                    isRotationDirty = true;
                }
                else
                {
                    networkState.HasRotAngleZ = false;
                }
            }

            if (SyncScaleX)
            {
                if (Mathf.Abs(networkState.ScaleX - scale.x) > ScaleThreshold || networkState.IsTeleportingNextFrame)
                {
                    networkState.ScaleX = scale.x;
                    networkState.HasScaleX = true;
                    isScaleDirty = true;
                }
                else
                {
                    networkState.HasScaleX = false;
                }
            }

            if (SyncScaleY)
            {
                if (Mathf.Abs(networkState.ScaleY - scale.y) > ScaleThreshold || networkState.IsTeleportingNextFrame)
                {
                    networkState.ScaleY = scale.y;
                    networkState.HasScaleY = true;
                    isScaleDirty = true;
                }
                else
                {
                    networkState.HasScaleY = false;
                }
            }

            if (SyncScaleZ)
            {
                if (Mathf.Abs(networkState.ScaleZ - scale.z) > ScaleThreshold || networkState.IsTeleportingNextFrame)
                {
                    networkState.ScaleZ = scale.z;
                    networkState.HasScaleZ = true;
                    isScaleDirty = true;
                }
                else
                {
                    networkState.HasScaleZ = false;
                }
            }

            isDirty |= isPositionDirty || isRotationDirty || isScaleDirty;

            if (isDirty)
            {
                networkState.SentTime = dirtyTime;
            }

            return (isDirty, isPositionDirty, isRotationDirty, isScaleDirty);
        }

        /// <summary>
        /// Applies the authoritative state to the local transform
        /// </summary>
        /// <remarks>
        /// The serverTime is required for position and scale in order to prevent a single element of each 3 elements
        /// from having too large of a delta time between the time it was last updated and the time any new update is
        /// sent.
        /// </remarks>
        /// <param name="networkState">new state to apply</param>
        /// <param name="transformToUpdate">transform to update</param>
        /// <param name="serverTime">required to interpolate when axis is not updated in the state to apply</param>
        private void ApplyInterpolatedNetworkStateToTransform(NetworkTransformState networkState, Transform transformToUpdate, double serverTime)
        {
            var interpolatedPosition = networkState.InLocalSpace ? transformToUpdate.localPosition : transformToUpdate.position;

            // todo: we should store network state w/ quats vs. euler angles
            var interpolatedRotAngles = networkState.InLocalSpace ? transformToUpdate.localEulerAngles : transformToUpdate.eulerAngles;
            var interpolatedScale = transformToUpdate.localScale;

            // InLocalSpace Read:
            InLocalSpace = networkState.InLocalSpace;

            // Update the position values that were changed in this state update
            if (networkState.HasPositionX)
            {
                interpolatedPosition.x = networkState.IsTeleportingNextFrame || !Interpolate ? networkState.PositionX : m_PositionXInterpolator.GetInterpolatedValue();
            }

            if (networkState.HasPositionY)
            {
                interpolatedPosition.y = networkState.IsTeleportingNextFrame || !Interpolate ? networkState.PositionY : m_PositionYInterpolator.GetInterpolatedValue();
            }

            if (networkState.HasPositionZ)
            {
                interpolatedPosition.z = networkState.IsTeleportingNextFrame || !Interpolate ? networkState.PositionZ : m_PositionZInterpolator.GetInterpolatedValue();
            }

            // Update the rotation values that were changed in this state update
            if (networkState.HasRotAngleX || networkState.HasRotAngleY || networkState.HasRotAngleZ)
            {
                var eulerAngles = new Vector3();
                if (Interpolate)
                {
                    eulerAngles = m_RotationInterpolator.GetInterpolatedValue().eulerAngles;
                }

                if (networkState.HasRotAngleX)
                {
                    interpolatedRotAngles.x = networkState.IsTeleportingNextFrame || !Interpolate ? networkState.RotAngleX : eulerAngles.x;
                }

                if (networkState.HasRotAngleY)
                {
                    interpolatedRotAngles.y = networkState.IsTeleportingNextFrame || !Interpolate ? networkState.RotAngleY : eulerAngles.y;
                }

                if (networkState.HasRotAngleZ)
                {
                    interpolatedRotAngles.z = networkState.IsTeleportingNextFrame || !Interpolate ? networkState.RotAngleZ : eulerAngles.z;
                }
            }

            // Update all scale axis that were changed in this state update
            if (networkState.HasScaleX)
            {
                interpolatedScale.x = networkState.IsTeleportingNextFrame || !Interpolate ? networkState.ScaleX : m_ScaleXInterpolator.GetInterpolatedValue();
            }

            if (networkState.HasScaleY)
            {
                interpolatedScale.y = networkState.IsTeleportingNextFrame || !Interpolate ? networkState.ScaleY : m_ScaleYInterpolator.GetInterpolatedValue();
            }

            if (networkState.HasScaleZ)
            {
                interpolatedScale.z = networkState.IsTeleportingNextFrame || !Interpolate ? networkState.ScaleZ : m_ScaleZInterpolator.GetInterpolatedValue();
            }

            // Apply the new position
            if (networkState.HasPositionX || networkState.HasPositionY || networkState.HasPositionZ)
            {
                if (InLocalSpace)
                {

                    transformToUpdate.localPosition = interpolatedPosition;
                }
                else
                {
                    transformToUpdate.position = interpolatedPosition;
                }
            }

            // Apply the new rotation
            if (networkState.HasRotAngleX || networkState.HasRotAngleY || networkState.HasRotAngleZ)
            {
                if (InLocalSpace)
                {
                    transformToUpdate.localRotation = Quaternion.Euler(interpolatedRotAngles);
                }
                else
                {
                    transformToUpdate.rotation = Quaternion.Euler(interpolatedRotAngles);
                }
            }

            // Apply the new scale
            if (networkState.HasScaleX || networkState.HasScaleY || networkState.HasScaleZ)
            {
                transformToUpdate.localScale = interpolatedScale;
            }
        }

        /// <summary>
        /// Only non-authoritative instances should invoke this
        /// </summary>
        private void AddInterpolatedState(NetworkTransformState newState)
        {
            var sentTime = newState.SentTime;

            // When there is a change in interpolation or if teleporting, we reset
            if ((newState.InLocalSpace != InLocalSpace) || newState.IsTeleportingNextFrame)
            {
                InLocalSpace = newState.InLocalSpace;

                // we should clear our float interpolators
                foreach (var interpolator in m_AllFloatInterpolators)
                {
                    interpolator.Clear();
                }

                // we should clear our quaternion interpolator
                m_RotationInterpolator.Clear();

                // Make sure no unauthorized changes occurred
                ApplyTransformValues();

                // Then we should apply the state passed to us
                if (newState.HasPositionX)
                {
                    m_PositionXInterpolator.ResetTo(newState.PositionX, sentTime);
                    m_LastStatePosition.x = newState.PositionX;
                }

                if (newState.HasPositionY)
                {
                    m_PositionYInterpolator.ResetTo(newState.PositionY, sentTime);
                    m_LastStatePosition.y = newState.PositionY;
                }

                if (newState.HasPositionZ)
                {
                    m_PositionZInterpolator.ResetTo(newState.PositionZ, sentTime);
                    m_LastStatePosition.z = newState.PositionZ;
                }

                // Get our current rotation
                var rotation = transform.rotation.eulerAngles;

                // Adjust it based on which axis changed
                if (newState.HasRotAngleX)
                {
                    rotation.x = newState.RotAngleX;
                }

                if (newState.HasRotAngleY)
                {
                    rotation.y = newState.RotAngleY;
                }

                if (newState.HasRotAngleZ)
                {
                    rotation.z = newState.RotAngleZ;
                }

                // Apply the rotation
                m_LastStateRotation = Quaternion.Euler(rotation);
                m_RotationInterpolator.ResetTo(m_LastStateRotation, sentTime);


                if (newState.HasScaleX)
                {
                    m_ScaleXInterpolator.ResetTo(newState.ScaleX, sentTime);
                    m_LocalScale.x = newState.ScaleX;
                }

                if (newState.HasScaleY)
                {
                    m_ScaleYInterpolator.ResetTo(newState.ScaleY, sentTime);
                    m_LocalScale.y = newState.ScaleY;
                }

                if (newState.HasScaleZ)
                {
                    m_ScaleZInterpolator.ResetTo(newState.ScaleZ, sentTime);
                    m_LocalScale.z = newState.ScaleZ;
                }

                // Finally, we should apply the updated values
                ApplyTransformValues();
                return;
            }

            var currentPosition = InLocalSpace ? m_Transform.localPosition : m_Transform.position;

            // Note: Any values we don't have an update for need their interpolator deltas reset.
            // Bandwidth optimizations mean that an update without the value set indicates "this hasn't changed",
            // and so we need to tell the interpolators that it's the last currently known axis value(s) at this
            // time delta.

            if (newState.HasPositionX)
            {
                m_PositionXInterpolator.AddMeasurement(newState.PositionX, sentTime);
            }
            else
            {
                m_PositionXInterpolator.AddMeasurement(m_LastStatePosition.x, sentTime);
            }

            if (newState.HasPositionY)
            {
                m_PositionYInterpolator.AddMeasurement(newState.PositionY, sentTime);
            }
            else
            {
                m_PositionYInterpolator.AddMeasurement(m_LastStatePosition.y, sentTime);
            }

            if (newState.HasPositionZ)
            {
                m_PositionZInterpolator.AddMeasurement(newState.PositionZ, sentTime);
            }
            else
            {
                m_PositionZInterpolator.AddMeasurement(m_LastStatePosition.z, sentTime);
            }

            // Here we take the new state Euler angles and apply any local
            // Euler angles to the axis that did not change prior to adding
            // the rotation measurement.
            var stateEuler = Quaternion.Euler(newState.RotAngleX, newState.RotAngleY, newState.RotAngleZ).eulerAngles;
            var currentEuler = m_LastStateRotation.eulerAngles;
            if (!newState.HasRotAngleX)
            {
                stateEuler.x = currentEuler.x;
            }
            if (!newState.HasRotAngleY)
            {
                stateEuler.y = currentEuler.y;
            }
            if (!newState.HasRotAngleZ)
            {
                stateEuler.z = currentEuler.z;
            }

            m_RotationInterpolator.AddMeasurement(Quaternion.Euler(stateEuler), sentTime);

            if (newState.HasScaleX)
            {
                m_ScaleXInterpolator.AddMeasurement(newState.ScaleX, sentTime);
            }
            else
            {
                m_ScaleXInterpolator.AddMeasurement(m_LocalScale.x, sentTime);
            }

            if (newState.HasScaleY)
            {
                m_ScaleYInterpolator.AddMeasurement(newState.ScaleY, sentTime);
            }
            else
            {
                m_ScaleYInterpolator.AddMeasurement(m_LocalScale.y, sentTime);
            }

            if (newState.HasScaleZ)
            {
                m_ScaleZInterpolator.AddMeasurement(newState.ScaleZ, sentTime);
            }
            else
            {
                m_ScaleZInterpolator.AddMeasurement(m_LocalScale.z, sentTime);
            }
        }

        /// <summary>
        /// Only non-authoritative instances should invoke this method
        /// </summary>
        private void OnNetworkStateChanged(NetworkTransformState oldState, NetworkTransformState newState)
        {
            if (!NetworkObject.IsSpawned)
            {
                return;
            }

            if (CanCommitToTransform)
            {
                // we're the authority, we ignore incoming changes
                return;
            }

            if (Interpolate)
            {
                AddInterpolatedState(newState);
            }
        }

        /// <summary>
        /// Will set the maximum interpolation boundary for the interpolators of this <see cref="NetworkTransform"/> instance.
        /// This value roughly translates to the maximum value of 't' in <see cref="Mathf.Lerp(float, float, float)"/> and
        /// <see cref="Mathf.LerpUnclamped(float, float, float)"/> for all transform elements being monitored by
        /// <see cref="NetworkTransform"/> (i.e. Position, Rotation, and Scale)
        /// </summary>
        /// <param name="maxInterpolationBound">Maximum time boundary that can be used in a frame when interpolating between two values</param>
        public void SetMaxInterpolationBound(float maxInterpolationBound)
        {
            m_PositionXInterpolator.MaxInterpolationBound = maxInterpolationBound;
            m_PositionYInterpolator.MaxInterpolationBound = maxInterpolationBound;
            m_PositionZInterpolator.MaxInterpolationBound = maxInterpolationBound;
            m_RotationInterpolator.MaxInterpolationBound = maxInterpolationBound;
            m_ScaleXInterpolator.MaxInterpolationBound = maxInterpolationBound;
            m_ScaleYInterpolator.MaxInterpolationBound = maxInterpolationBound;
            m_ScaleZInterpolator.MaxInterpolationBound = maxInterpolationBound;
        }

        private void Awake()
        {
            // we only want to create our interpolators during Awake so that, when pooled, we do not create tons
            //  of gc thrash each time objects wink out and are re-used
            m_PositionXInterpolator = new BufferedLinearInterpolatorFloat();
            m_PositionYInterpolator = new BufferedLinearInterpolatorFloat();
            m_PositionZInterpolator = new BufferedLinearInterpolatorFloat();
            m_RotationInterpolator = new BufferedLinearInterpolatorQuaternion(); // rotation is a single Quaternion since each euler axis will affect the quaternion's final value
            m_ScaleXInterpolator = new BufferedLinearInterpolatorFloat();
            m_ScaleYInterpolator = new BufferedLinearInterpolatorFloat();
            m_ScaleZInterpolator = new BufferedLinearInterpolatorFloat();

            if (m_AllFloatInterpolators.Count == 0)
            {
                m_AllFloatInterpolators.Add(m_PositionXInterpolator);
                m_AllFloatInterpolators.Add(m_PositionYInterpolator);
                m_AllFloatInterpolators.Add(m_PositionZInterpolator);
                m_AllFloatInterpolators.Add(m_ScaleXInterpolator);
                m_AllFloatInterpolators.Add(m_ScaleYInterpolator);
                m_AllFloatInterpolators.Add(m_ScaleZInterpolator);
            }
        }

        /// <summary>
        /// These local values are for the non-authoritative side
        /// This prevents non-authoritative instances from changing
        /// the transform.
        /// </summary>
        private Vector3 m_LastStatePosition;
        private Vector3 m_LocalScale;
        private Quaternion m_LastStateRotation;

        /// <summary>
        /// Applies the last authorized position, scale, and rotation
        /// </summary>
        private void ApplyTransformValues()
        {
            if (InLocalSpace)
            {
                m_Transform.localPosition = m_LastStatePosition;
            }
            else
            {
                m_Transform.position = m_LastStatePosition;
            }

            if (InLocalSpace)
            {
                m_Transform.localRotation = m_LastStateRotation;
            }
            else
            {
                m_Transform.rotation = m_LastStateRotation;
            }

            m_Transform.localScale = m_LocalScale;
        }

        /// <summary>
        /// Sets the currently authorized position, scale, and rotation
        /// </summary>
        private void SetTransformValues()
        {
            m_LastStatePosition = InLocalSpace ? m_Transform.localPosition : m_Transform.position;
            m_LastStateRotation = InLocalSpace ? m_Transform.localRotation : m_Transform.rotation;
            m_LocalScale = m_Transform.localScale;
        }

        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            m_CachedIsServer = IsServer;
            m_CachedNetworkManager = NetworkManager;

            // crucial we do this to reset the interpolators so that recycled objects when using a pool will
            // not have leftover interpolator state from the previous object
            Initialize();

            // This assures the initial spawning of the object synchronizes all connected clients
            // with the current transform values. This should not be placed within Initialize since
            // that can be invoked when ownership changes.
            if (CanCommitToTransform)
            {
                Teleport(m_Transform.position, m_Transform.rotation, m_Transform.localScale);
                TryCommitTransform(transform, m_CachedNetworkManager.LocalTime.Time);
            }
        }

        /// <inheritdoc/>
        public override void OnNetworkDespawn()
        {
            GetReplicatedNetworkState().OnValueChanged -= OnNetworkStateChanged;
        }

        /// <inheritdoc/>
        public override void OnGainedOwnership()
        {
            Initialize();
        }

        /// <inheritdoc/>
        public override void OnLostOwnership()
        {
            Initialize();
        }

        private void Initialize()
        {
            if (!IsSpawned)
            {
                return;
            }

            // must set up m_Transform in OnNetworkSpawn because it's possible an object spawns but is disabled
            //  and thus awake won't be called.
            // TODO: investigate further on not sending data for something that is not enabled
            m_Transform = transform;

            CanCommitToTransform = IsServerAuthoritative() ? IsServer : IsOwner;
            var replicatedState = GetReplicatedNetworkState();
            m_LocalAuthoritativeNetworkState = replicatedState.Value;

            if (CanCommitToTransform)
            {
                replicatedState.OnValueChanged -= OnNetworkStateChanged;
            }
            else
            {
                replicatedState.OnValueChanged += OnNetworkStateChanged;

                // In case we are late joining
                ResetInterpolatedStateToCurrentAuthoritativeState();

                // For the non-authoritative side, we need to capture the initial values of the transform
                SetTransformValues();
            }
        }

        /// <summary>
        /// Directly sets a state on the authoritative transform.
        /// This will override any changes made previously to the transform
        /// This isn't resistant to network jitter. Server side changes due to this method won't be interpolated.
        /// The parameters are broken up into pos / rot / scale on purpose so that the caller can perturb
        ///  just the desired one(s)
        /// </summary>
        /// <param name="posIn"></param> new position to move to.  Can be null
        /// <param name="rotIn"></param> new rotation to rotate to.  Can be null
        /// <param name="scaleIn">new scale to scale to. Can be null</param>
        /// <param name="shouldGhostsInterpolate">Should other clients interpolate this change or not. True by default</param>
        /// new scale to scale to.  Can be null
        /// <exception cref="Exception"></exception>
        public void SetState(Vector3? posIn = null, Quaternion? rotIn = null, Vector3? scaleIn = null, bool shouldGhostsInterpolate = true)
        {
            if (!IsSpawned)
            {
                return;
            }

            if (!CanCommitToTransform)
            {
                throw new Exception("Non-owner non-authority instance cannot set the state of the NetworkTransform!");
            }

            Vector3 pos = posIn == null ? InLocalSpace ? m_Transform.localPosition : m_Transform.position : (Vector3)posIn;
            Quaternion rot = rotIn == null ? InLocalSpace ? m_Transform.localRotation : m_Transform.rotation : (Quaternion)rotIn;
            Vector3 scale = scaleIn == null ? m_Transform.localScale : (Vector3)scaleIn;

            SetStateInternal(pos, rot, scale, shouldGhostsInterpolate);
        }

        /// <summary>
        /// Authoritative only method
        /// Sets the internal state (teleporting or just set state)
        /// </summary>
        private void SetStateInternal(Vector3 pos, Quaternion rot, Vector3 scale, bool shouldTeleport)
        {
            if (InLocalSpace)
            {
                m_Transform.localPosition = pos;
                m_Transform.localRotation = rot;
            }
            else
            {
                m_Transform.position = pos;
                m_Transform.rotation = rot;
            }
            m_Transform.localScale = scale;
            m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame = shouldTeleport;
        }

        // todo: this is currently in update, to be able to catch any transform changes. A FixedUpdate mode could be added to be less intense, but it'd be
        // conditional to users only making transform update changes in FixedUpdate.
        /// <inheritdoc/>
        protected virtual void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (CanCommitToTransform)
            {
                TryCommitTransform(transform, m_CachedNetworkManager.LocalTime.Time);
            }
            else
            {
                // eventually, we could hoist this calculation so that it happens once for all objects, not once per object
                var serverTime = NetworkManager.ServerTime;
                if (Interpolate)
                {
                    var cachedDeltaTime = Time.deltaTime;
                    var cachedServerTime = serverTime.Time;
                    var cachedRenderTime = serverTime.TimeTicksAgo(1).Time;
                    foreach (var interpolator in m_AllFloatInterpolators)
                    {
                        interpolator.Update(cachedDeltaTime, cachedRenderTime, cachedServerTime);
                    }

                    m_RotationInterpolator.Update(cachedDeltaTime, cachedRenderTime, cachedServerTime);
                }

                // Always update from the last transform values before updating from the transform state to assure
                // no non-authoritative changes are allowed
                ApplyTransformValues();

                // Apply updated interpolated value
                ApplyInterpolatedNetworkStateToTransform(GetReplicatedNetworkState().Value, m_Transform, serverTime.Time);

                // Always set the any new transform values to assure only the authoritative side is updating the transform
                SetTransformValues();
            }
        }

        /// <summary>
        /// Teleport the transform to the given values without interpolating
        /// </summary>
        /// <param name="newPosition"></param> new position to move to.
        /// <param name="newRotation"></param> new rotation to rotate to.
        /// <param name="newScale">new scale to scale to.</param>
        /// <exception cref="Exception"></exception>
        public void Teleport(Vector3 newPosition, Quaternion newRotation, Vector3 newScale)
        {
            if (!CanCommitToTransform)
            {
                throw new Exception("Teleporting on non-authoritative side is not allowed!");
            }

            // Do not allow this instance to teleport while teleporting
            if (m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame)
            {
                return;
            }

            // Teleporting now is as simple as:
            // - Setting teleport flag
            // - Applying the new position, rotation, and scale
            SetStateInternal(newPosition, newRotation, newScale, true);
        }

        /// <summary>
        /// Override this method and return false to switch to owner authoritative mode
        /// </summary>
        /// <returns>(<see cref="true"/> or <see cref="false"/>) where when false it runs as owner-client authoritative</returns>
        protected virtual bool OnIsServerAuthoritative()
        {
            return true;
        }

        /// <summary>
        /// Used by <see cref="NetworkRigidbody"/> to determines if this is server or owner authoritative.
        /// </summary>
        internal bool IsServerAuthoritative()
        {
            return OnIsServerAuthoritative();
        }
    }
}
