using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text;
using Unity.Mathematics;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

namespace Unity.Netcode.Components
{
    /// <summary>
    /// A component for syncing transforms.
    /// NetworkTransform will read the underlying transform and replicate it to clients.
    /// The replicated value will be automatically be interpolated (if active) and applied to the underlying GameObject's transform.
    /// </summary>
    [DisallowMultipleComponent]
    [AddComponentMenu("Netcode/Network Transform")]
    public class NetworkTransform : NetworkBehaviour
    {

#if UNITY_EDITOR
        internal virtual bool HideInterpolateValue => false;

        [HideInInspector]
        [SerializeField]
        internal bool NetworkTransformExpanded;
#endif

        #region NETWORK TRANSFORM STATE
        /// <summary>
        /// Data structure used to synchronize the <see cref="NetworkTransform"/>
        /// </summary>
        public struct NetworkTransformState : INetworkSerializable
        {
            private const int k_InLocalSpaceBit = 0x00000001; // Persists between state updates (authority dictates if this is set)
            private const int k_PositionXBit = 0x00000002;
            private const int k_PositionYBit = 0x00000004;
            private const int k_PositionZBit = 0x00000008;
            private const int k_RotAngleXBit = 0x00000010;
            private const int k_RotAngleYBit = 0x00000020;
            private const int k_RotAngleZBit = 0x00000040;
            private const int k_ScaleXBit = 0x00000080;
            private const int k_ScaleYBit = 0x00000100;
            private const int k_ScaleZBit = 0x00000200;
            private const int k_TeleportingBit = 0x00000400;
            private const int k_Interpolate = 0x00000800; // Persists between state updates (authority dictates if this is set)
            private const int k_QuaternionSync = 0x00001000; // Persists between state updates (authority dictates if this is set)
            private const int k_QuaternionCompress = 0x00002000; // Persists between state updates (authority dictates if this is set)
            private const int k_UseHalfFloats = 0x00004000; // Persists between state updates (authority dictates if this is set)
            private const int k_Synchronization = 0x00008000;
            private const int k_PositionSlerp = 0x00010000; // Persists between state updates (authority dictates if this is set)
            private const int k_IsParented = 0x00020000; // When parented and synchronizing, we need to have both lossy and local scale due to varying spawn order
            private const int k_SynchBaseHalfFloat = 0x00040000;
            private const int k_ReliableSequenced = 0x00080000;
            private const int k_UseUnreliableDeltas = 0x00100000;
            private const int k_UnreliableFrameSync = 0x00200000;
            private const int k_TrackStateId = 0x10000000; // (Internal Debugging) When set each state update will contain a state identifier

            // Stores persistent and state relative flags
            private uint m_Bitset;
            internal uint BitSet
            {
                get { return m_Bitset; }
                set { m_Bitset = value; }
            }

            // Used to store the tick calculated sent time
            internal double SentTime;

            // Used for full precision position updates
            internal float PositionX, PositionY, PositionZ;

            // Used for full precision Euler updates
            internal float RotAngleX, RotAngleY, RotAngleZ;

            // Used for full precision quaternion updates
            internal Quaternion Rotation;

            // Used for full precision scale updates
            internal float ScaleX, ScaleY, ScaleZ;

            // Used for half precision delta position updates
            internal Vector3 CurrentPosition;
            internal Vector3 DeltaPosition;
            internal NetworkDeltaPosition NetworkDeltaPosition;

            // Used for half precision scale
            internal HalfVector3 HalfVectorScale;
            internal Vector3 Scale;
            internal Vector3 LossyScale;

            // Used for half precision quaternion
            internal HalfVector4 HalfVectorRotation;

            // Used to store a compressed quaternion
            internal uint QuaternionCompressed;

            // Authoritative and non-authoritative sides use this to determine if a NetworkTransformState is
            // dirty or not.
            internal bool IsDirty { get; set; }

            /// <summary>
            /// The last byte size of the <see cref="NetworkTransformState"/> updated.
            /// </summary>
            public int LastSerializedSize { get; internal set; }

            // Used for NetworkDeltaPosition delta position synchronization
            internal int NetworkTick;

            // Used when tracking by state ID is enabled
            internal int StateId;

            // Set when a state has been explicitly set (i.e. SetState)
            internal bool ExplicitSet;

            // Used during serialization
            private FastBufferReader m_Reader;
            private FastBufferWriter m_Writer;

            /// <summary>
            /// When set, the <see cref="NetworkTransform"/> is operates in local space
            /// </summary>
            public bool InLocalSpace
            {
                get => GetFlag(k_InLocalSpaceBit);
                internal set
                {
                    SetFlag(value, k_InLocalSpaceBit);
                }
            }

            // Position
            /// <summary>
            /// When set, the X-Axis position value has changed
            /// </summary>
            public bool HasPositionX
            {
                get => GetFlag(k_PositionXBit);
                internal set
                {
                    SetFlag(value, k_PositionXBit);
                }
            }

            /// <summary>
            /// When set, the Y-Axis position value has changed
            /// </summary>
            public bool HasPositionY
            {
                get => GetFlag(k_PositionYBit);
                internal set
                {
                    SetFlag(value, k_PositionYBit);
                }
            }

            /// <summary>
            /// When set, the Z-Axis position value has changed
            /// </summary>
            public bool HasPositionZ
            {
                get => GetFlag(k_PositionZBit);
                internal set
                {
                    SetFlag(value, k_PositionZBit);
                }
            }

            /// <summary>
            /// When set, at least one of the position axis values has changed.
            /// </summary>
            public bool HasPositionChange
            {
                get
                {
                    return HasPositionX | HasPositionY | HasPositionZ;
                }
            }

            // RotAngles
            /// <summary>
            /// When set, the Euler rotation X-Axis value has changed.
            /// </summary>
            /// <remarks>
            /// When quaternion synchronization is enabled all axis are always updated.
            /// </remarks>
            public bool HasRotAngleX
            {
                get => GetFlag(k_RotAngleXBit);
                internal set
                {
                    SetFlag(value, k_RotAngleXBit);
                }
            }

            /// <summary>
            /// When set, the Euler rotation Y-Axis value has changed.
            /// </summary>
            /// <remarks>
            /// When quaternion synchronization is enabled all axis are always updated.
            /// </remarks>
            public bool HasRotAngleY
            {
                get => GetFlag(k_RotAngleYBit);
                internal set
                {
                    SetFlag(value, k_RotAngleYBit);
                }
            }

            /// <summary>
            /// When set, the Euler rotation Z-Axis value has changed.
            /// </summary>
            /// <remarks>
            /// When quaternion synchronization is enabled all axis are always updated.
            /// </remarks>
            public bool HasRotAngleZ
            {
                get => GetFlag(k_RotAngleZBit);
                internal set
                {
                    SetFlag(value, k_RotAngleZBit);
                }
            }

            /// <summary>
            /// When set, at least one of the rotation axis values has changed.
            /// </summary>
            /// <remarks>
            /// When quaternion synchronization is enabled all axis are always updated.
            /// </remarks>
            public bool HasRotAngleChange
            {
                get
                {
                    return HasRotAngleX | HasRotAngleY | HasRotAngleZ;
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal bool HasScale(int axisIndex)
            {
                return GetFlag(k_ScaleXBit << axisIndex);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            internal void SetHasScale(int axisIndex, bool isSet)
            {
                SetFlag(isSet, k_ScaleXBit << axisIndex);
            }

            // Scale
            /// <summary>
            /// When set, the X-Axis scale value has changed.
            /// </summary>
            public bool HasScaleX
            {
                get => GetFlag(k_ScaleXBit);
                internal set
                {
                    SetFlag(value, k_ScaleXBit);
                }
            }

            /// <summary>
            /// When set, the Y-Axis scale value has changed.
            /// </summary>
            public bool HasScaleY
            {
                get => GetFlag(k_ScaleYBit);
                internal set
                {
                    SetFlag(value, k_ScaleYBit);
                }
            }

            /// <summary>
            /// When set, the Z-Axis scale value has changed.
            /// </summary>
            public bool HasScaleZ
            {
                get => GetFlag(k_ScaleZBit);
                internal set
                {
                    SetFlag(value, k_ScaleZBit);
                }
            }

            /// <summary>
            /// When set, at least one of the scale axis values has changed.
            /// </summary>
            public bool HasScaleChange
            {
                get
                {
                    return HasScaleX | HasScaleY | HasScaleZ;
                }
            }

            /// <summary>
            /// When set, the current state will be treated as a teleport.
            /// </summary>
            /// <remarks>
            /// When teleporting:
            /// - Interpolation is reset.
            /// - If using half precision, full precision values are used.
            /// - All axis marked to be synchronized will be updated.
            /// </remarks>
            public bool IsTeleportingNextFrame
            {
                get => GetFlag(k_TeleportingBit);
                internal set
                {
                    SetFlag(value, k_TeleportingBit);
                }
            }

            /// <summary>
            /// When set the <see cref="NetworkTransform"/> is uses interpolation.
            /// </summary>
            /// <remarks>
            /// Authority does not apply interpolation via <see cref="NetworkTransform"/>.
            /// Authority should handle its own motion/rotation/scale smoothing locally.
            /// </remarks>
            public bool UseInterpolation
            {
                get => GetFlag(k_Interpolate);
                internal set
                {
                    SetFlag(value, k_Interpolate);
                }
            }

            /// <summary>
            /// When enabled, this <see cref="NetworkTransform"/> instance uses <see cref="Quaternion"/> synchronization.
            /// </summary>
            /// <remarks>
            /// Use quaternion synchronization if you are nesting <see cref="NetworkTransform"/>s and rotation can occur on both the parent and child.
            /// When quaternion synchronization is enabled, the entire quaternion is updated when there are any changes to any axial values.
            /// You can use half float precision or quaternion compression to reduce the bandwidth cost.
            /// </remarks>
            public bool QuaternionSync
            {
                get => GetFlag(k_QuaternionSync);
                internal set
                {
                    SetFlag(value, k_QuaternionSync);
                }
            }

            /// <summary>
            /// When set <see cref="Quaternion"/>s will be compressed down to 4 bytes using a smallest three implementation.
            /// </summary>
            /// <remarks>
            /// This only will be applied when <see cref="QuaternionSync"/> is enabled.
            /// Half float precision provides a higher precision than quaternion compression but at the cost of 4 additional bytes per update.
            /// - Quaternion Compression: 4 bytes per delta update
            /// - Half float precision: 8 bytes per delta update
            /// </remarks>
            public bool QuaternionCompression
            {
                get => GetFlag(k_QuaternionCompress);
                internal set
                {
                    SetFlag(value, k_QuaternionCompress);
                }
            }

            /// <summary>
            /// When set, the <see cref="NetworkTransform"/> will use half float precision for position, rotation, and scale.
            /// </summary>
            /// <remarks>
            /// Postion is synchronized through delta position updates in order to reduce precision loss/drift and to extend to positions beyond the limitation of half float maximum values.
            /// Rotation and scale both use half float precision (<see cref="HalfVector4"/> and <see cref="HalfVector3"/>)
            /// </remarks>
            public bool UseHalfFloatPrecision
            {
                get => GetFlag(k_UseHalfFloats);
                internal set
                {
                    SetFlag(value, k_UseHalfFloats);
                }
            }

            /// <summary>
            /// When set, this indicates it is the first state being synchronized.
            /// Typically when the associate <see cref="NetworkObject"/> is spawned or a client is being synchronized after connecting to a network session in progress.
            /// </summary>
            public bool IsSynchronizing
            {
                get => GetFlag(k_Synchronization);
                internal set
                {
                    SetFlag(value, k_Synchronization);
                }
            }

            /// <summary>
            /// Determines if position interpolation will Slerp towards its target position.
            /// This is only really useful if you are moving around a point in a circular pattern.
            /// </summary>
            public bool UsePositionSlerp
            {
                get => GetFlag(k_PositionSlerp);
                internal set
                {
                    SetFlag(value, k_PositionSlerp);
                }
            }

            /// <summary>
            /// Returns whether this state update was a frame synchronization when 
            /// UseUnreliableDeltas is enabled. When set, the entire transform will 
            /// be or has been synchronized.
            /// </summary>
            public bool IsUnreliableFrameSync()
            {
                return UnreliableFrameSync;
            }

            /// <summary>
            /// Returns true if this state was sent with reliable delivery.
            /// If false, then it was sent with unreliable delivery.
            /// </summary>
            /// <remarks>
            /// Unreliable delivery will only be used if <see cref="UseUnreliableDeltas"/> is set.
            /// </remarks>
            public bool IsReliableStateUpdate()
            {
                return ReliableSequenced;
            }

            internal bool IsParented
            {
                get => GetFlag(k_IsParented);
                set
                {
                    SetFlag(value, k_IsParented);
                }
            }

            internal bool SynchronizeBaseHalfFloat
            {
                get => GetFlag(k_SynchBaseHalfFloat);
                set
                {
                    SetFlag(value, k_SynchBaseHalfFloat);
                }
            }

            internal bool ReliableSequenced
            {
                get => GetFlag(k_ReliableSequenced);
                set
                {
                    SetFlag(value, k_ReliableSequenced);
                }
            }

            internal bool UseUnreliableDeltas
            {
                get => GetFlag(k_UseUnreliableDeltas);
                set
                {
                    SetFlag(value, k_UseUnreliableDeltas);
                }
            }

            internal bool UnreliableFrameSync
            {
                get => GetFlag(k_UnreliableFrameSync);
                set
                {
                    SetFlag(value, k_UnreliableFrameSync);
                }
            }

            internal bool TrackByStateId
            {
                get => GetFlag(k_TrackStateId);
                set
                {
                    SetFlag(value, k_TrackStateId);
                }
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool GetFlag(int flag)
            {
                return (m_Bitset & flag) != 0;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private void SetFlag(bool set, int flag)
            {
                if (set) { m_Bitset = m_Bitset | (uint)flag; }
                else { m_Bitset = m_Bitset & (uint)~flag; }
            }

            internal void ClearBitSetForNextTick()
            {
                // Clear everything but flags that should persist between state updates until changed by authority
                m_Bitset &= k_InLocalSpaceBit | k_Interpolate | k_UseHalfFloats | k_QuaternionSync | k_QuaternionCompress | k_PositionSlerp | k_UseUnreliableDeltas;
                IsDirty = false;
            }

            /// <summary>
            /// Returns the current state's rotation. If there is no change in the rotation,
            /// then it will return <see cref="Quaternion.identity"/>.
            /// </summary>
            /// <remarks>
            /// When there is no change in an updated state's rotation then there are no values to return.
            /// Checking for <see cref="HasRotAngleChange"/> is one way to detect this.
            /// </remarks>
            /// <returns><see cref="Quaternion"/></returns>
            public Quaternion GetRotation()
            {
                if (HasRotAngleChange)
                {
                    if (QuaternionSync)
                    {
                        return Rotation;
                    }
                    else
                    {
                        return Quaternion.Euler(RotAngleX, RotAngleY, RotAngleZ);
                    }
                }
                return Quaternion.identity;
            }

            /// <summary>
            /// Returns the current state's position. If there is no change in position,
            /// then it returns <see cref="Vector3.zero"/>.
            /// </summary>
            /// <remarks>
            /// When there is no change in an updated state's position then there are no values to return.
            /// Checking for <see cref="HasPositionChange"/> is one way to detect this.
            /// When used with half precision it returns the half precision delta position state update
            /// which will not be the full position.
            /// To get a NettworkTransform's full position, use <see cref="GetSpaceRelativePosition(bool)"/> and
            /// pass true as the parameter.
            /// </remarks>
            /// <returns><see cref="Vector3"/></returns>
            public Vector3 GetPosition()
            {
                if (HasPositionChange)
                {
                    if (UseHalfFloatPrecision)
                    {
                        if (IsTeleportingNextFrame)
                        {
                            return CurrentPosition;
                        }
                        else
                        {
                            return NetworkDeltaPosition.GetFullPosition();
                        }
                    }
                    else
                    {
                        return new Vector3(PositionX, PositionY, PositionZ);
                    }
                }
                return Vector3.zero;
            }

            /// <summary>
            /// Returns the current state's scale. If there is no change in scale,
            /// then it returns <see cref="Vector3.zero"/>.
            /// </summary>
            /// <remarks>
            /// When there is no change in an updated state's scale then there are no values to return.
            /// Checking for <see cref="HasScaleChange"/> is one way to detect this.
            /// </remarks>
            /// <returns><see cref="Vector3"/></returns>
            public Vector3 GetScale()
            {
                if (HasScaleChange)
                {
                    if (UseHalfFloatPrecision)
                    {
                        if (IsTeleportingNextFrame)
                        {
                            return Scale;
                        }
                        else
                        {
                            return HalfVectorScale.ToVector3();
                        }
                    }
                    else
                    {
                        return new Vector3(ScaleX, ScaleY, ScaleZ);
                    }
                }
                return Vector3.zero;
            }

            /// <summary>
            /// The network tick that this state was sent by the authoritative instance.
            /// </summary>
            /// <returns><see cref="int"/></returns>
            public int GetNetworkTick()
            {
                return NetworkTick;
            }

            internal HalfVector3 HalfEulerRotation;

            /// <summary>
            /// Serializes this <see cref="NetworkTransformState"/>
            /// </summary>
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                // Used to calculate the LastSerializedSize value
                var positionStart = 0;
                var isWriting = serializer.IsWriter;
                if (isWriting)
                {
                    m_Writer = serializer.GetFastBufferWriter();
                    positionStart = m_Writer.Position;
                }
                else
                {
                    m_Reader = serializer.GetFastBufferReader();
                    positionStart = m_Reader.Position;
                }

                // Synchronize State Flags and Network Tick
                {
                    if (isWriting)
                    {
                        if (UseUnreliableDeltas)
                        {
                            // If teleporting, synchronizing, doing an axial frame sync, or using half float precision and we collapsed a delta into the base position
                            if (IsTeleportingNextFrame || IsSynchronizing || UnreliableFrameSync || (UseHalfFloatPrecision && NetworkDeltaPosition.CollapsedDeltaIntoBase))
                            {
                                // Send the message reliably
                                ReliableSequenced = true;
                            }
                            else
                            {
                                ReliableSequenced = false;
                            }
                        }
                        else // If not using UseUnreliableDeltas, then always use reliable fragmented sequenced
                        {
                            ReliableSequenced = true;
                        }

                        BytePacker.WriteValueBitPacked(m_Writer, m_Bitset);
                        // We use network ticks as opposed to absolute time as the authoritative
                        // side updates on every new tick.
                        BytePacker.WriteValueBitPacked(m_Writer, NetworkTick);

                    }
                    else
                    {
                        ByteUnpacker.ReadValueBitPacked(m_Reader, out m_Bitset);
                        // We use network ticks as opposed to absolute time as the authoritative
                        // side updates on every new tick.
                        ByteUnpacker.ReadValueBitPacked(m_Reader, out NetworkTick);
                    }
                }

                // If debugging states and track by state identifier is enabled, serialize the current state identifier
                if (TrackByStateId)
                {
                    serializer.SerializeValue(ref StateId);
                }

                // Synchronize Position
                if (HasPositionChange)
                {
                    if (UseHalfFloatPrecision)
                    {
                        NetworkDeltaPosition.SynchronizeBase = SynchronizeBaseHalfFloat;

                        // Apply which axis should be updated for both write/read (teleporting, synchronizing, or just updating)
                        NetworkDeltaPosition.HalfVector3.AxisToSynchronize[0] = HasPositionX;
                        NetworkDeltaPosition.HalfVector3.AxisToSynchronize[1] = HasPositionY;
                        NetworkDeltaPosition.HalfVector3.AxisToSynchronize[2] = HasPositionZ;

                        if (IsTeleportingNextFrame)
                        {
                            // **Always use full precision when teleporting and UseHalfFloatPrecision is enabled**
                            serializer.SerializeValue(ref CurrentPosition);
                            // If we are synchronizing, then include the half vector position's delta offset
                            if (IsSynchronizing)
                            {
                                serializer.SerializeValue(ref DeltaPosition);
                                if (!isWriting)
                                {
                                    NetworkDeltaPosition.NetworkTick = NetworkTick;
                                    NetworkDeltaPosition.NetworkSerialize(serializer);
                                }
                                else
                                {
                                    serializer.SerializeNetworkSerializable(ref NetworkDeltaPosition);
                                }
                            }
                        }
                        else
                        {
                            if (!isWriting)
                            {
                                NetworkDeltaPosition.NetworkTick = NetworkTick;
                                NetworkDeltaPosition.NetworkSerialize(serializer);
                            }
                            else
                            {
                                serializer.SerializeNetworkSerializable(ref NetworkDeltaPosition);
                            }
                        }
                    }
                    else // Full precision axis specific position synchronization
                    {
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
                    }
                }

                // Synchronize Rotation
                if (HasRotAngleChange)
                {
                    if (QuaternionSync)
                    {
                        // Always use the full quaternion if teleporting
                        if (IsTeleportingNextFrame)
                        {
                            serializer.SerializeValue(ref Rotation);
                        }
                        else
                        {
                            // Use the quaternion compressor if enabled
                            if (QuaternionCompression)
                            {
                                if (isWriting)
                                {
                                    QuaternionCompressed = QuaternionCompressor.CompressQuaternion(ref Rotation);
                                }

                                serializer.SerializeValue(ref QuaternionCompressed);

                                if (!isWriting)
                                {
                                    QuaternionCompressor.DecompressQuaternion(ref Rotation, QuaternionCompressed);
                                }
                            }
                            else
                            {
                                if (UseHalfFloatPrecision)
                                {
                                    if (isWriting)
                                    {
                                        HalfVectorRotation.UpdateFrom(ref Rotation);
                                    }

                                    serializer.SerializeNetworkSerializable(ref HalfVectorRotation);

                                    if (!isWriting)
                                    {
                                        Rotation = HalfVectorRotation.ToQuaternion();
                                    }
                                }
                                else
                                {
                                    serializer.SerializeValue(ref Rotation);
                                }
                            }
                        }
                    }
                    else // Euler Rotation Synchronization
                    {
                        // Half float precision (full precision when teleporting)
                        if (UseHalfFloatPrecision && !IsTeleportingNextFrame)
                        {
                            if (HasRotAngleChange)
                            {
                                // Apply which axis should be updated for both write/read
                                HalfEulerRotation.AxisToSynchronize[0] = HasRotAngleX;
                                HalfEulerRotation.AxisToSynchronize[1] = HasRotAngleY;
                                HalfEulerRotation.AxisToSynchronize[2] = HasRotAngleZ;

                                if (isWriting)
                                {
                                    HalfEulerRotation.Set(RotAngleX, RotAngleY, RotAngleZ);
                                }

                                serializer.SerializeValue(ref HalfEulerRotation);

                                if (!isWriting)
                                {
                                    var eulerRotation = HalfEulerRotation.ToVector3();
                                    if (HasRotAngleX)
                                    {
                                        RotAngleX = eulerRotation.x;
                                    }

                                    if (HasRotAngleY)
                                    {
                                        RotAngleY = eulerRotation.y;
                                    }

                                    if (HasRotAngleZ)
                                    {
                                        RotAngleZ = eulerRotation.z;
                                    }
                                }
                            }
                        }
                        else // Full precision Euler
                        {
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
                        }
                    }
                }

                // Synchronize Scale
                if (HasScaleChange)
                {
                    // If we are teleporting (which includes synchronizing) and the associated NetworkObject has a parent
                    // then we want to serialize the LossyScale since NetworkObject spawn order is not  guaranteed
                    if (IsTeleportingNextFrame && IsParented)
                    {
                        serializer.SerializeValue(ref LossyScale);
                    }
                    // Half precision scale synchronization
                    if (UseHalfFloatPrecision)
                    {
                        if (IsTeleportingNextFrame)
                        {
                            serializer.SerializeValue(ref Scale);
                        }
                        else
                        {
                            // Apply which axis should be updated for both write/read
                            HalfVectorScale.AxisToSynchronize[0] = HasScaleX;
                            HalfVectorScale.AxisToSynchronize[1] = HasScaleY;
                            HalfVectorScale.AxisToSynchronize[2] = HasScaleZ;

                            // For scale, when half precision is enabled we can still only send the axis with deltas
                            if (isWriting)
                            {
                                HalfVectorScale.Set(Scale[0], Scale[1], Scale[2]);
                            }

                            serializer.SerializeValue(ref HalfVectorScale);

                            if (!isWriting)
                            {
                                Scale = HalfVectorScale.ToVector3();
                                if (HasScaleX)
                                {
                                    ScaleX = Scale.x;
                                }

                                if (HasScaleY)
                                {
                                    ScaleY = Scale.y;
                                }

                                if (HasScaleZ)
                                {
                                    ScaleZ = Scale.x;
                                }
                            }
                        }
                    }
                    else // Full precision scale synchronization
                    {
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

                // Only if we are receiving state
                if (!isWriting)
                {
                    // Go ahead and mark the local state dirty
                    IsDirty = HasPositionChange || HasRotAngleChange || HasScaleChange;
                    LastSerializedSize = m_Reader.Position - positionStart;
                }
                else
                {
                    LastSerializedSize = m_Writer.Position - positionStart;
                }
            }
        }
        #endregion

        #region PROPERTIES AND GENERAL METHODS


        public enum AuthorityModes
        {
            Server,
            Owner,
        }
#if MULTIPLAYER_SERVICES_SDK_INSTALLED
        [Tooltip("Selects who has authority (sends state updates) over the transform. When the network topology is set to distributed authority, this always defaults to owner authority. If server (the default), then only server-side adjustments to the " +
            "transform will be synchronized with clients. If owner (or client), then only the owner-side adjustments to the transform will be synchronized with both the server and other clients.")]
#else
        [Tooltip("Selects who has authority (sends state updates) over the transform. If server (the default), then only server-side adjustments to the transform will be synchronized with clients. If owner (or client), " +
            "then only the owner-side adjustments to the transform will be synchronized with both the server and other clients.")]
#endif
        public AuthorityModes AuthorityMode;


        /// <summary>
        /// When enabled, any parented <see cref="NetworkObject"/>s (children) of this <see cref="NetworkObject"/> will be forced to synchronize their transform when this <see cref="NetworkObject"/> instance sends a state update.<br />
        /// This can help to reduce out of sync updates that can lead to slight jitter between a parent and its child/children.
        /// </summary>
        /// <remarks>
        /// - If this is set on a child and the parent does not have this set then the child will not be tick synchronized with its parent. <br />
        /// - If the parent instance does not send any state updates, the children will still send state updates when exceeding axis delta threshold. <br />
        /// - This does not need to be set on children to be applied.
        /// </remarks>
        [Tooltip("When enabled, any parented children of this instance will send a state update when this instance sends a state update. If this instance doesn't send a state update, the children will still send state updates when reaching their axis specified threshold delta. Children do not have to have this setting enabled.")]
        public bool TickSyncChildren = false;

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

        /// <summary>
        /// When set each state update will contain a state identifier
        /// </summary>
        internal static bool TrackByStateId = false;

        /// <summary>
        /// Enabled by default.
        /// When set (enabled by default), NetworkTransform will send common state updates using unreliable network delivery
        /// to provide a higher tolerance to poor network conditions (especially packet loss). When disabled, all state updates
        /// are sent using a reliable fragmented sequenced network delivery.
        /// </summary>
        /// <remarks>
        /// The following more critical state updates are still sent as reliable fragmented sequenced:
        /// - The initial synchronization state update
        /// - The teleporting state update.
        /// - When using half float precision and the `NetworkDeltaPosition` delta exceeds the maximum delta forcing the axis in
        /// question to be collapsed into the core base position, this state update will be sent as reliable fragmented sequenced.
        ///
        /// In order to preserve a continual consistency of axial values when unreliable delta messaging is enabled (due to the
        /// possibility of dropping packets), NetworkTransform instances will send 1 axial frame synchronization update per
        /// second (only for the axis marked to synchronize are sent as reliable fragmented sequenced) as long as a delta state
        /// update had been previously sent. When a NetworkObject is at rest, axial frame synchronization updates are not sent.
        /// </remarks>
        [Tooltip("When set, NetworkTransform will send common state updates using unreliable network delivery " +
            "to provide a higher tolerance to poor network conditions (especially packet loss). When disabled, all state updates are " +
            "sent using reliable fragmented sequenced network delivery.")]
        public bool UseUnreliableDeltas = false;

        /// <summary>
        /// When enabled (default), the x component of position will be synchronized by authority.
        /// </summary>
        /// <remarks>
        /// Changes to this on non-authoritative instances has no effect.
        /// </remarks>
        public bool SyncPositionX = true;

        /// <summary>
        /// When enabled (default), the y component of position will be synchronized by authority.
        /// </summary>
        /// <remarks>
        /// Changes to this on non-authoritative instances has no effect.
        /// </remarks>
        public bool SyncPositionY = true;

        /// <summary>
        /// When enabled (default), the z component of position will be synchronized by authority.
        /// </summary>
        /// <remarks>
        /// Changes to this on non-authoritative instances has no effect.
        /// </remarks>
        public bool SyncPositionZ = true;

        private bool SynchronizePosition
        {
            get
            {
                return SyncPositionX || SyncPositionY || SyncPositionZ;
            }
        }

        /// <summary>
        /// When enabled (default), the x component of rotation will be synchronized by authority.
        /// </summary>
        /// <remarks>
        /// When <see cref="UseQuaternionSynchronization"/> is enabled this does not apply.
        /// Changes to this on non-authoritative instances has no effect.
        /// </remarks>
        public bool SyncRotAngleX = true;

        /// <summary>
        /// When enabled (default), the y component of rotation will be synchronized by authority.
        /// </summary>
        /// <remarks>
        /// When <see cref="UseQuaternionSynchronization"/> is enabled this does not apply.
        /// Changes to this on non-authoritative instances has no effect.
        /// </remarks>
        public bool SyncRotAngleY = true;

        /// <summary>
        /// When enabled (default), the z component of rotation will be synchronized by authority.
        /// </summary>
        /// <remarks>
        /// When <see cref="UseQuaternionSynchronization"/> is enabled this does not apply.
        /// Changes to this on non-authoritative instances has no effect.
        /// </remarks>
        public bool SyncRotAngleZ = true;

        private bool SynchronizeRotation
        {
            get
            {
                return SyncRotAngleX || SyncRotAngleY || SyncRotAngleZ;
            }
        }

        /// <summary>
        /// When enabled (default), the x component of scale will be synchronized by authority.
        /// </summary>
        /// <remarks>
        /// Changes to this on non-authoritative instances has no effect.
        /// </remarks>
        public bool SyncScaleX = true;

        /// <summary>
        /// When enabled (default), the y component of scale will be synchronized by authority.
        /// </summary>
        /// <remarks>
        /// Changes to this on non-authoritative instances has no effect.
        /// </remarks>
        public bool SyncScaleY = true;

        /// <summary>
        /// When enabled (default), the z component of scale will be synchronized by authority.
        /// </summary>
        /// <remarks>
        /// Changes to this on non-authoritative instances has no effect.
        /// </remarks>
        public bool SyncScaleZ = true;

        private bool SynchronizeScale
        {
            get
            {
                return SyncScaleX || SyncScaleY || SyncScaleZ;
            }
        }

        /// <summary>
        /// The position threshold value that triggers a delta state update by the authoritative instance.
        /// </summary>
        /// <remarks>
        /// Note: setting this to zero will update position every network tick whether it changed or not.
        /// </remarks>
        public float PositionThreshold = PositionThresholdDefault;

        /// <summary>
        /// The rotation threshold value that triggers a delta state update by the authoritative instance.
        /// </summary>
        /// <remarks>
        /// Minimum Value: 0.00001
        /// Maximum Value: 360.0
        /// </remarks>
        [Range(0.00001f, 360.0f)]
        public float RotAngleThreshold = RotAngleThresholdDefault;

        /// <summary>
        /// The scale threshold value that triggers a delta state update by the authoritative instance.
        /// </summary>
        /// <remarks>
        /// Note: setting this to zero will update position every network tick whether it changed or not.
        /// </remarks>
        public float ScaleThreshold = ScaleThresholdDefault;

        /// <summary>
        /// Enable this on the authority side for quaternion synchronization
        /// </summary>
        /// <remarks>
        /// This is synchronized by authority. During runtime, this should only be changed by the
        /// authoritative side. Non-authoritative instances will be overridden by the next
        /// authoritative state update.
        /// </remarks>
        [Tooltip("When enabled, this will synchronize the full Quaternion (i.e. all Euler rotation axis are updated if one axis has a delta)")]
        public bool UseQuaternionSynchronization = false;

        /// <summary>
        /// Enabled this on the authority side for quaternion compression
        /// </summary>
        /// <remarks>
        /// This has a lower precision than half float precision. Recommended only for low precision
        /// scenarios. <see cref="UseHalfFloatPrecision"/> provides better precision at roughly half
        /// the cost of a full quaternion update.
        /// This is synchronized by authority. During runtime, this should only be changed by the
        /// authoritative side. Non-authoritative instances will be overridden by the next
        /// authoritative state update.
        /// </remarks>
        [Tooltip("When enabled, this uses a smallest three implementation that reduces full Quaternion updates down to the size of an unsigned integer (ignores half float precision settings).")]
        public bool UseQuaternionCompression = false;

        /// <summary>
        /// Enable this to use half float precision for position, rotation, and scale.
        /// When enabled, delta position synchronization is used.
        /// </summary>
        /// <remarks>
        /// This is synchronized by authority. During runtime, this should only be changed by the
        /// authoritative side. Non-authoritative instances will be overridden by the next
        /// authoritative state update.
        /// </remarks>
        [Tooltip("When enabled, this will use half float precision values for position (uses delta position updating), rotation (except when Quaternion compression is enabled), and scale.")]
        public bool UseHalfFloatPrecision = false;

        /// <summary>
        /// Sets whether the transform should be treated as local (true) or world (false) space.
        /// </summary>
        /// <remarks>
        /// This is synchronized by authority. During runtime, this should only be changed by the
        /// authoritative side. Non-authoritative instances will be overridden by the next
        /// authoritative state update.
        /// </remarks>
        [Tooltip("Sets whether this transform should sync in local space or in world space")]
        public bool InLocalSpace = false;

        /// <summary>
        /// When enabled, the NetworkTransform will automatically handle transitioning into the respective transform space when its <see cref="NetworkObject"/> parent changes.<br />
        /// When parented: Automatically transitions into local space and coverts any existing pending interpolated states to local space on non-authority instances.<br />
        /// When deparented: Automatically transitions into world space and converts any existing pending interpolated states to world space on non-authority instances.<br />
        /// Set on the root <see cref="NetworkTransform"/> instance (nested <see cref="NetworkTransform"/> components should be pre-set in-editor to local space. <br />
        /// </summary>
        /// <remarks>
        /// Only works with <see cref="NetworkTransform"/> components that are not paired with a <see cref="NetworkRigidbody"/> or <see cref="NetworkRigidbody2D"/> component that is configured to use the rigid body for motion.<br />
        /// <see cref="TickSyncChildren"/> will automatically be set when this is enabled.
        /// Does not auto-synchronize clients if changed on the authority instance during runtime (i.e. apply this setting in-editor).
        /// </remarks>
        public bool SwitchTransformSpaceWhenParented = false;

        protected bool PositionInLocalSpace => (!SwitchTransformSpaceWhenParented && InLocalSpace) || (m_PositionInterpolator != null && m_PositionInterpolator.InLocalSpace && SwitchTransformSpaceWhenParented);
        protected bool RotationInLocalSpace => (!SwitchTransformSpaceWhenParented && InLocalSpace) || (m_RotationInterpolator != null && m_RotationInterpolator.InLocalSpace && SwitchTransformSpaceWhenParented);

        /// <summary>
        /// When enabled (default) interpolation is applied.
        /// When disabled interpolation is disabled.
        /// </summary>
        /// <remarks>
        /// This is synchronized by authority and changes to interpolation during runtime forces a
        /// teleport/full update. During runtime, this should only be changed by the authoritative
        /// side. Non-authoritative instances will be overridden by the next authoritative state update.
        /// </remarks>
        public bool Interpolate = true;

        /// <summary>
        /// When true and interpolation is enabled, this will Slerp to the target position.
        /// </summary>
        /// <remarks>
        /// This is synchronized by authority and only applies to position interpolation.
        /// During runtime, this should only be changed by the authoritative side. Non-authoritative
        /// instances will be overridden by the next authoritative state update.
        /// </remarks>
        [Tooltip("When enabled the position interpolator will Slerp towards its current target position.")]
        public bool SlerpPosition = false;

        /// <summary>
        /// Used to determine who can write to this transform. Server only for this transform.
        /// Changing this value alone in a child implementation will not allow you to create a NetworkTransform which can be written to by clients. See the ClientNetworkTransform Sample
        /// in the package samples for how to implement a NetworkTransform with client write support.
        /// If using different values, please use RPCs to write to the server. Netcode doesn't support client side network variable writing
        /// </summary>
        public bool CanCommitToTransform { get; protected set; }

        /// <summary>
        /// Internally used by <see cref="NetworkTransform"/> to keep track of the <see cref="NetworkManager"/> instance assigned to this
        /// this <see cref="NetworkBehaviour"/> derived class instance.
        /// </summary>
        protected NetworkManager m_CachedNetworkManager;

        /// <summary>
        /// Helper method that returns the space relative position of the transform.
        /// </summary>
        /// <remarks>
        /// If InLocalSpace is <see cref="true"/> then it returns the transform.localPosition
        /// If InLocalSpace is <see cref="false"/> then it returns the transform.position
        /// When invoked on the non-authority side:
        /// If <see cref="getCurrentState"/> is true then it will return the most
        /// current authority position from the most recent state update. This can be useful
        /// if interpolation is enabled and you need to determine the final target position.
        /// When invoked on the authority side:
        /// It will always return the space relative position.
        /// </remarks>
        /// <param name="getCurrentState">
        /// Authority always returns the space relative transform position (whether true or false).
        /// Non-authority:
        /// When false (default): returns the space relative transform position
        /// When true: returns the authority position from the most recent state update.
        /// </param>
        /// <returns><see cref="Vector3"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetSpaceRelativePosition(bool getCurrentState = false)
        {
            if (!getCurrentState || CanCommitToTransform)
            {
                return InLocalSpace ? transform.localPosition : transform.position;
            }
            else
            {
                // When half float precision is enabled, get the NetworkDeltaPosition's full position
                if (UseHalfFloatPrecision)
                {
                    return m_HalfPositionState.GetFullPosition();
                }
                else
                {
                    // Otherwise, just get the current position
                    return m_InternalCurrentPosition;
                }
            }
        }

        /// <summary>
        /// Helper method that returns the space relative rotation of the transform.
        /// </summary>
        /// <remarks>
        /// If InLocalSpace is <see cref="true"/> then it returns the transform.localRotation
        /// If InLocalSpace is <see cref="false"/> then it returns the transform.rotation
        /// When invoked on the non-authority side:
        /// If <see cref="getCurrentState"/> is true then it will return the most
        /// current authority rotation from the most recent state update. This can be useful
        /// if interpolation is enabled and you need to determine the final target rotation.
        /// When invoked on the authority side:
        /// It will always return the space relative rotation.
        /// </remarks>
        /// <param name="getCurrentState">
        /// Authority always returns the space relative transform rotation (whether true or false).
        /// Non-authority:
        /// When false (default): returns the space relative transform rotation
        /// When true: returns the authority rotation from the most recent state update.
        /// </param>
        /// <returns><see cref="Quaternion"/></returns>
        public Quaternion GetSpaceRelativeRotation(bool getCurrentState = false)
        {
            if (!getCurrentState || CanCommitToTransform)
            {
                return InLocalSpace ? transform.localRotation : transform.rotation;
            }
            else
            {
                return m_InternalCurrentRotation;
            }
        }

        /// <summary>
        /// Helper method that returns the scale of the transform.
        /// </summary>
        /// <remarks>
        /// When invoked on the non-authority side:
        /// If <see cref="getCurrentState"/> is true then it will return the most
        /// current authority scale from the most recent state update. This can be useful
        /// if interpolation is enabled and you need to determine the final target scale.
        /// When invoked on the authority side:
        /// It will always return the space relative scale.
        /// </remarks>
        /// <param name="getCurrentState">
        /// Authority always returns the space relative transform scale (whether true or false).
        /// Non-authority:
        /// When false (default): returns the space relative transform scale
        /// When true: returns the authority scale from the most recent state update.
        /// </param>
        /// <returns><see cref="Vector3"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetScale(bool getCurrentState = false)
        {
            if (!getCurrentState || CanCommitToTransform)
            {
                return transform.localScale;
            }
            else
            {
                return m_InternalCurrentScale;
            }
        }

        // Used by both authoritative and non-authoritative instances.
        // This represents the most recent local authoritative state.
        private NetworkTransformState m_LocalAuthoritativeNetworkState;

        internal NetworkTransformState LocalAuthoritativeNetworkState
        {
            get
            {
                return m_LocalAuthoritativeNetworkState;
            }
            set
            {
                m_LocalAuthoritativeNetworkState = value;
            }
        }

        private ClientRpcParams m_ClientRpcParams = new ClientRpcParams() { Send = new ClientRpcSendParams() };
        private List<ulong> m_ClientIds = new List<ulong>() { 0 };

        private BufferedLinearInterpolatorVector3 m_PositionInterpolator;
        private BufferedLinearInterpolatorVector3 m_ScaleInterpolator;
        private BufferedLinearInterpolatorQuaternion m_RotationInterpolator; // rotation is a single Quaternion since each Euler axis will affect the quaternion's final value

        // The previous network state
        private NetworkTransformState m_OldState = new NetworkTransformState();

        // Non-Authoritative's current position, scale, and rotation that is used to assure the non-authoritative side cannot make adjustments to
        // the portions of the transform being synchronized.
        private Vector3 m_InternalCurrentPosition;
        private Vector3 m_TargetPosition;
        private Vector3 m_InternalCurrentScale;
        private Vector3 m_TargetScale;
        private Quaternion m_InternalCurrentRotation;
        private Vector3 m_TargetRotation;

#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
        private bool m_UseRigidbodyForMotion;
        private NetworkRigidbodyBase m_NetworkRigidbodyInternal;

        internal void RegisterRigidbody(NetworkRigidbodyBase networkRigidbody)
        {
            if (networkRigidbody != null)
            {
                m_NetworkRigidbodyInternal = networkRigidbody;
                m_UseRigidbodyForMotion = m_NetworkRigidbodyInternal.UseRigidBodyForMotion;
            }
        }
#endif

#if DEBUG_NETWORKTRANSFORM || UNITY_INCLUDE_TESTS
        /// <summary>
        /// For debugging delta position and half vector3
        /// </summary>
        protected delegate void AddLogEntryHandler(ref NetworkTransformState networkTransformState, ulong targetClient, bool preUpdate = false);
        protected AddLogEntryHandler m_AddLogEntry;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddLogEntry(ref NetworkTransformState networkTransformState, ulong targetClient, bool preUpdate = false)
        {
            m_AddLogEntry?.Invoke(ref networkTransformState, targetClient, preUpdate);
        }


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected int GetStateId(ref NetworkTransformState state)
        {
            return state.StateId;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected NetworkDeltaPosition GetHalfPositionState()
        {
            return m_HalfPositionState;
        }

#else
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void AddLogEntry(ref NetworkTransformState networkTransformState, ulong targetClient, bool preUpdate = false)
        {
        }
#endif

        /// <summary>
        /// Only used when UseHalfFloatPrecision is enabled
        /// </summary>
        private NetworkDeltaPosition m_HalfPositionState = new NetworkDeltaPosition(Vector3.zero, 0);

        internal void UpdatePositionSlerp()
        {
            if (m_PositionInterpolator != null)
            {
                m_PositionInterpolator.IsSlerp = SlerpPosition;
            }
        }

        /// <summary>
        /// Determines if synchronization is needed.
        /// Basically only if we are running in owner authoritative mode and it
        /// is the owner being synchronized we don't want to synchronize with
        /// the exception of the NetworkObject being owned by the server.
        /// </summary>
        private bool ShouldSynchronizeHalfFloat(ulong targetClientId)
        {
            if (!IsServerAuthoritative() && NetworkObject.OwnerClientId == targetClientId)
            {
                // In distributed authority mode we want to synchronize the half float if we are the owner.
                return (!NetworkManager.DistributedAuthorityMode && NetworkObject.IsOwnedByServer) || (NetworkManager.DistributedAuthorityMode);
            }
            return true;
        }

        // For test logging purposes
        internal NetworkTransformState SynchronizeState;

        #endregion

        #region ONSYNCHRONIZE

        /// <summary>
        /// This is invoked when a new client joins (server and client sides)
        /// Server Side: Serializes as if we were teleporting (everything is sent via NetworkTransformState)
        /// Client Side: Adds the interpolated state which applies the NetworkTransformState as well
        /// </summary>
        /// <remarks>
        /// If a derived class overrides this, then make sure to invoke this base method!
        /// </remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="serializer"></param>
        /// <param name="targetClientId">the clientId being synchronized (both reading and writing)</param>
        protected override void OnSynchronize<T>(ref BufferSerializer<T> serializer)
        {
            var targetClientId = m_TargetIdBeingSynchronized;
            SynchronizeState = new NetworkTransformState()
            {
                HalfEulerRotation = new HalfVector3(),
                HalfVectorRotation = new HalfVector4(),
                HalfVectorScale = new HalfVector3(),
                NetworkDeltaPosition = new NetworkDeltaPosition(),
            };

            if (serializer.IsWriter)
            {
                SynchronizeState.IsTeleportingNextFrame = true;
                var transformToCommit = transform;
                // If we are using Half Float Precision, then we want to only synchronize the authority's m_HalfPositionState.FullPosition in order for
                // for the non-authority side to be able to properly synchronize delta position updates.
                CheckForStateChange(ref SynchronizeState, ref transformToCommit, true, targetClientId);
                SynchronizeState.NetworkSerialize(serializer);
            }
            else
            {
                SynchronizeState.NetworkSerialize(serializer);
            }
        }

        /// <summary>
        /// We now apply synchronization after everything has spawned
        /// </summary>
        private void ApplySynchronization()
        {
            // Set the transform's synchronization modes
            InLocalSpace = SynchronizeState.InLocalSpace;
            Interpolate = SynchronizeState.UseInterpolation;
            UseQuaternionSynchronization = SynchronizeState.QuaternionSync;
            UseHalfFloatPrecision = SynchronizeState.UseHalfFloatPrecision;
            UseQuaternionCompression = SynchronizeState.QuaternionCompression;
            SlerpPosition = SynchronizeState.UsePositionSlerp;
            UpdatePositionSlerp();
            // Teleport/Fully Initialize based on the state
            ApplyTeleportingState(SynchronizeState);
            m_LocalAuthoritativeNetworkState = SynchronizeState;
            m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame = false;
            m_LocalAuthoritativeNetworkState.IsSynchronizing = false;
            SynchronizeState.IsSynchronizing = false;
        }
        #endregion

        #region AUTHORITY STATE UPDATE
        /// <summary>
        /// This will try to send/commit the current transform delta states (if any)
        /// </summary>
        /// <remarks>
        /// Only client owners or the server should invoke this method
        /// </remarks>
        /// <param name="transformToCommit">the transform to be committed</param>
        /// <param name="dirtyTime">time it was marked dirty</param>
        internal void TryCommitTransformToServer(Transform transformToCommit, double dirtyTime)
        {
            if (!IsSpawned)
            {
                NetworkLog.LogError($"Cannot commit transform when not spawned!");
                return;
            }

            // Only the server or the owner is allowed to commit a transform
            if (!IsServer && !IsOwner)
            {
                var errorMessage = gameObject != NetworkObject.gameObject ?
                    $"Non-authority instance of {NetworkObject.gameObject.name} is trying to commit a transform on {gameObject.name}!" :
                    $"Non-authority instance of {NetworkObject.gameObject.name} is trying to commit a transform!";
                NetworkLog.LogError(errorMessage);
                return;
            }

            // If we are authority, update the authoritative state
            if (CanCommitToTransform)
            {
                OnUpdateAuthoritativeState(ref transformToCommit);
            }
            else // Non-Authority
            {
                var position = InLocalSpace ? transformToCommit.localPosition : transformToCommit.position;
                var rotation = InLocalSpace ? transformToCommit.localRotation : transformToCommit.rotation;
                // We are an owner requesting to update our state
                if (!IsServer)
                {
                    SetStateServerRpc(position, rotation, transformToCommit.localScale, false);
                }
                else // Server is always authoritative (including owner authoritative)
                {
                    SetStateClientRpc(position, rotation, transformToCommit.localScale, false);
                }
            }
        }

        /// <summary>
        /// Invoked just prior to being pushed to non-authority instances.
        /// </summary>
        /// <remarks>
        /// This is useful to know the exact position, rotation, or scale values sent
        /// to non-authoritative instances. This is only invoked on the authoritative
        /// instance.
        /// </remarks>
        /// <param name="networkTransformState">the state being pushed</param>
        protected virtual void OnAuthorityPushTransformState(ref NetworkTransformState networkTransformState)
        {
        }

        // Only set if a delta has been sent, this is reset after an axial synch has been sent
        // to assure the instance doesn't continue to send axial synchs when an object is at rest.
        private bool m_DeltaSynch;

        /// <summary>
        /// Authoritative side only
        /// If there are any transform delta states, this method will synchronize the
        /// state with all non-authority instances.
        /// </summary>
        private void TryCommitTransform(ref Transform transformToCommit, bool synchronize = false, bool settingState = false)
        {
            // Only the server or the owner is allowed to commit a transform
            if (!IsServer && !IsOwner)
            {
                NetworkLog.LogError($"[{name}] is trying to commit the transform without authority!");
                return;
            }
#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
            // TODO: Make this an authority flag
            // For now, just synchronize with the NetworkRigidbodyBase UseRigidBodyForMotion
            if (m_NetworkRigidbodyInternal != null)
            {
                m_UseRigidbodyForMotion = m_NetworkRigidbodyInternal.UseRigidBodyForMotion;
            }
#endif

            // If the transform has deltas (returns dirty) or if an explicitly set state is pending
            if (m_LocalAuthoritativeNetworkState.ExplicitSet || CheckForStateChange(ref m_LocalAuthoritativeNetworkState, ref transformToCommit, synchronize, forceState: settingState))
            {
                // If the state was explicitly set, then update the network tick to match the locally calculate tick
                if (m_LocalAuthoritativeNetworkState.ExplicitSet)
                {
                    m_LocalAuthoritativeNetworkState.NetworkTick = m_CachedNetworkManager.NetworkTickSystem.ServerTime.Tick;
                }

                // Send the state update
                UpdateTransformState();

                // Mark the last tick and the old state (for next ticks)
                m_OldState = m_LocalAuthoritativeNetworkState;

                // Reset the teleport and explicit state flags after we have sent the state update.
                // These could be set again in the below OnAuthorityPushTransformState virtual method
                m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame = false;
                m_LocalAuthoritativeNetworkState.ExplicitSet = false;

                try
                {
                    // Notify of the pushed state update
                    OnAuthorityPushTransformState(ref m_LocalAuthoritativeNetworkState);
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }

                // The below is part of assuring we only send a frame synch, when sending unreliable deltas, if 
                // we have already sent at least one unreliable delta state update. At this point in the callstack,
                // a delta state update has just been sent in the above UpdateTransformState() call and as long as
                // we didn't send a frame synch and we are not synchronizing then we know at least one unreliable
                // delta has been sent. Under this scenario, we should start checking for this instance's alloted 
                // frame synch "tick slot". Once we send a frame synch, if no other deltas occur after that
                // (i.e. the object is at rest) then we will stop sending frame synch's until the object begins
                // moving, rotating, or scaling again.
                if (UseUnreliableDeltas && !m_LocalAuthoritativeNetworkState.UnreliableFrameSync && !synchronize)
                {
                    m_DeltaSynch = true;
                }

#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
                // We handle updating attached bodies when the "parent" body has a state update in order to keep their delta state updates tick synchronized.
                if (m_UseRigidbodyForMotion && m_NetworkRigidbodyInternal.NetworkRigidbodyConnections.Count > 0)
                {
                    foreach (var childRigidbody in m_NetworkRigidbodyInternal.NetworkRigidbodyConnections)
                    {
                        childRigidbody.NetworkTransform.OnNetworkTick(true);
                    }
                }
#endif
                // When enabled, any children will get tick synchronized with state updates
                if (TickSyncChildren)
                {
                    // Synchronize any nested NetworkTransforms with the parent's
                    foreach (var childNetworkTransform in NetworkObject.NetworkTransforms)
                    {
                        // Don't update the same instance
                        if (childNetworkTransform == this)
                        {
                            continue;
                        }
                        if (childNetworkTransform.CanCommitToTransform)
                        {
                            childNetworkTransform.OnNetworkTick(true);
                        }
                    }

                    // Synchronize any parented children with the parent's motion
                    foreach (var child in m_ParentedChildren)
                    {
                        // Synchronize any nested NetworkTransforms of the child with the parent's
                        foreach (var childNetworkTransform in child.NetworkTransforms)
                        {
                            if (childNetworkTransform.CanCommitToTransform)
                            {
                                childNetworkTransform.OnNetworkTick(true);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Used for integration testing:
        /// Will apply the transform to the LocalAuthoritativeNetworkState and get detailed dirty information returned
        /// in the <see cref="NetworkTransformState"/> returned.
        /// </summary>
        /// <param name="transform">transform to apply</param>
        /// <returns>NetworkTransformState</returns>
        internal NetworkTransformState ApplyLocalNetworkState(Transform transform)
        {
            // Since we never commit these changes, we need to simulate that any changes were committed previously and the bitset
            // value would already be reset prior to having the state applied
            m_LocalAuthoritativeNetworkState.ClearBitSetForNextTick();

            // Now check the transform for any threshold value changes
            CheckForStateChange(ref m_LocalAuthoritativeNetworkState, ref transform);

            // Return the entire state to be used by the integration test
            return m_LocalAuthoritativeNetworkState;
        }

        /// <summary>
        /// Used for integration testing
        /// </summary>
        internal bool ApplyTransformToNetworkState(ref NetworkTransformState networkState, double dirtyTime, Transform transformToUse)
        {
            m_CachedNetworkManager = NetworkManager;
            // Apply the interpolate and PostionDeltaCompression flags, otherwise we get false positives whether something changed or not.
            networkState.UseInterpolation = Interpolate;
            networkState.QuaternionSync = UseQuaternionSynchronization;
            networkState.UseHalfFloatPrecision = UseHalfFloatPrecision;
            networkState.QuaternionCompression = UseQuaternionCompression;
            networkState.UseUnreliableDeltas = UseUnreliableDeltas;
            m_HalfPositionState = new NetworkDeltaPosition(Vector3.zero, 0, math.bool3(SyncPositionX, SyncPositionY, SyncPositionZ));

            return CheckForStateChange(ref networkState, ref transformToUse);
        }

        /// <summary>
        /// Applies the transform to the <see cref="NetworkTransformState"/> specified.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool CheckForStateChange(ref NetworkTransformState networkState, ref Transform transformToUse, bool isSynchronization = false, ulong targetClientId = 0, bool forceState = false)
        {
            // As long as we are not doing our first synchronization and we are sending unreliable deltas, each
            // NetworkTransform will stagger its full transfom synchronization over a 1 second period based on the
            // assigned tick slot (m_TickSync).
            // More about m_DeltaSynch:
            // If we have not sent any deltas since our last frame synch, then this will prevent us from sending
            // frame synch's when the object is at rest. If this is false and a state update is detected and sent,
            // then it will be set to true and each subsequent tick will do this check to determine if it should
            // send a full frame synch.
            var isAxisSync = false;
            // We compare against the NetworkTickSystem version since ServerTime is set when updating ticks
            if (UseUnreliableDeltas && !isSynchronization && m_DeltaSynch && m_NextTickSync <= m_CachedNetworkManager.NetworkTickSystem.ServerTime.Tick)
            {
                // Increment to the next frame synch tick position for this instance
                m_NextTickSync += (int)m_CachedNetworkManager.NetworkConfig.TickRate;
                // If we are teleporting, we do not need to send a frame synch for this tick slot
                // as a "frame synch" really is effectively just a teleport.
                isAxisSync = !networkState.IsTeleportingNextFrame;
                // Reset our delta synch trigger so we don't send another frame synch until we
                // send at least 1 unreliable state update after this fame synch or teleport
                m_DeltaSynch = false;
            }
            // This is used to determine if we need to send the state update reliably (if we are doing an axial sync)
            networkState.UnreliableFrameSync = isAxisSync;

            var isTeleportingAndNotSynchronizing = networkState.IsTeleportingNextFrame && !isSynchronization;
            var isDirty = false;
            var isPositionDirty = isTeleportingAndNotSynchronizing ? networkState.HasPositionChange : false;
            var isRotationDirty = isTeleportingAndNotSynchronizing ? networkState.HasRotAngleChange : false;
            var isScaleDirty = isTeleportingAndNotSynchronizing ? networkState.HasScaleChange : false;

#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
            var position = m_UseRigidbodyForMotion ? m_NetworkRigidbodyInternal.GetPosition() : InLocalSpace ? transformToUse.localPosition : transformToUse.position;
            var rotation = m_UseRigidbodyForMotion ? m_NetworkRigidbodyInternal.GetRotation() : InLocalSpace ? transformToUse.localRotation : transformToUse.rotation;

            var positionThreshold = Vector3.one * PositionThreshold;
            var rotationThreshold = Vector3.one * RotAngleThreshold;

            if (m_UseRigidbodyForMotion)
            {
                positionThreshold = m_NetworkRigidbodyInternal.GetAdjustedPositionThreshold();
                rotationThreshold = m_NetworkRigidbodyInternal.GetAdjustedRotationThreshold();
            }
#else
            var position = InLocalSpace ? transformToUse.localPosition : transformToUse.position;
            var rotation = InLocalSpace ? transformToUse.localRotation : transformToUse.rotation;
            var positionThreshold =  Vector3.one * PositionThreshold;
            var rotationThreshold = Vector3.one * RotAngleThreshold;
#endif
            var rotAngles = rotation.eulerAngles;
            var scale = transformToUse.localScale;
            networkState.IsSynchronizing = isSynchronization;

            // All of the checks below, up to the delta position checking portion, are to determine if the
            // authority changed a property during runtime that requires a full synchronizing.
#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
            if ((InLocalSpace != networkState.InLocalSpace || isSynchronization) && !m_UseRigidbodyForMotion)
#else
            if (InLocalSpace != networkState.InLocalSpace)
#endif
            {
                networkState.InLocalSpace = SwitchTransformSpaceWhenParented ? transform.parent != null : InLocalSpace;
                isDirty = true;
                networkState.IsTeleportingNextFrame = !SwitchTransformSpaceWhenParented;
                forceState = SwitchTransformSpaceWhenParented;
            }
#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
            else if (InLocalSpace && m_UseRigidbodyForMotion)
            {
                // TODO: Provide more options than just FixedJoint
                Debug.LogError($"[Rigidbody] WHen using a Rigidbody for motion, you cannot use {nameof(InLocalSpace)}! If parenting, use the integrated FixedJoint or use a Joint on Authority side.");
            }
#endif

            // Check for parenting when synchronizing and/or teleporting
            if (isSynchronization || networkState.IsTeleportingNextFrame)
            {
                // This all has to do with complex nested hierarchies and how it impacts scale
                // when set for the first time or teleporting and depends upon whether the
                // NetworkObject is parented (or "de-parented") at the same time any scale
                // values are applied.
                var hasParentNetworkObject = false;

                var parentNetworkObject = (NetworkObject)null;

                // If the NetworkObject belonging to this NetworkTransform instance has a parent
                // (i.e. this handles nested NetworkTransforms under a parent at some layer above)
                if (NetworkObject.transform.parent != null)
                {
                    parentNetworkObject = NetworkObject.transform.parent.GetComponent<NetworkObject>();

                    // In-scene placed NetworkObjects parented under a GameObject with no
                    // NetworkObject preserve their lossyScale when synchronizing.
                    if (parentNetworkObject == null && NetworkObject.IsSceneObject != false)
                    {
                        hasParentNetworkObject = true;
                    }
                    else
                    {
                        // Or if the relative NetworkObject has a parent NetworkObject
                        hasParentNetworkObject = parentNetworkObject != null;
                    }
                }

                networkState.IsParented = hasParentNetworkObject;
            }

            if (Interpolate != networkState.UseInterpolation)
            {
                networkState.UseInterpolation = Interpolate;
                isDirty = true;
                // When we change from interpolating to not interpolating (or vice versa) we need to synchronize/reset everything
                networkState.IsTeleportingNextFrame = true;
            }

            if (UseQuaternionSynchronization != networkState.QuaternionSync)
            {
                networkState.QuaternionSync = UseQuaternionSynchronization;
                isDirty = true;
                networkState.IsTeleportingNextFrame = true;
            }

            if (UseQuaternionCompression != networkState.QuaternionCompression)
            {
                networkState.QuaternionCompression = UseQuaternionCompression;
                isDirty = true;
                networkState.IsTeleportingNextFrame = true;
            }

            if (UseHalfFloatPrecision != networkState.UseHalfFloatPrecision)
            {
                networkState.UseHalfFloatPrecision = UseHalfFloatPrecision;
                isDirty = true;
                networkState.IsTeleportingNextFrame = true;
            }

            if (SlerpPosition != networkState.UsePositionSlerp)
            {
                networkState.UsePositionSlerp = SlerpPosition;
                isDirty = true;
                networkState.IsTeleportingNextFrame = true;
            }

            if (UseUnreliableDeltas != networkState.UseUnreliableDeltas)
            {
                networkState.UseUnreliableDeltas = UseUnreliableDeltas;
                isDirty = true;
                networkState.IsTeleportingNextFrame = true;
            }

            // Begin delta checks against last sent state update
            if (!UseHalfFloatPrecision)
            {
                if (SyncPositionX && (Mathf.Abs(networkState.PositionX - position.x) >= positionThreshold.x || networkState.IsTeleportingNextFrame || isAxisSync || forceState))
                {
                    networkState.PositionX = position.x;
                    networkState.HasPositionX = true;
                    isPositionDirty = true;
                }

                if (SyncPositionY && (Mathf.Abs(networkState.PositionY - position.y) >= positionThreshold.y || networkState.IsTeleportingNextFrame || isAxisSync || forceState))
                {
                    networkState.PositionY = position.y;
                    networkState.HasPositionY = true;
                    isPositionDirty = true;
                }

                if (SyncPositionZ && (Mathf.Abs(networkState.PositionZ - position.z) >= positionThreshold.z || networkState.IsTeleportingNextFrame || isAxisSync || forceState))
                {
                    networkState.PositionZ = position.z;
                    networkState.HasPositionZ = true;
                    isPositionDirty = true;
                }
            }
            else if (SynchronizePosition)
            {
                // If we are teleporting then we can skip the delta threshold check
                isPositionDirty = networkState.IsTeleportingNextFrame || isAxisSync || forceState;
                if (m_HalfFloatTargetTickOwnership > m_CachedNetworkManager.ServerTime.Tick)
                {
                    isPositionDirty = true;
                }

                // For NetworkDeltaPosition, if any axial value is dirty then we always send a full update
                if (!isPositionDirty)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (Math.Abs(position[i] - m_HalfPositionState.PreviousPosition[i]) >= positionThreshold[i])
                        {
                            isPositionDirty = i == 0 ? SyncPositionX : i == 1 ? SyncPositionY : SyncPositionZ;
                            if (!isPositionDirty)
                            {
                                continue;
                            }
                            break;
                        }
                    }
                }

                // If the position is dirty or we are teleporting (which includes synchronization)
                // then determine what parts of the NetworkDeltaPosition should be updated
                if (isPositionDirty)
                {
                    // If we are not synchronizing the transform state for the first time
                    if (!isSynchronization)
                    {
                        // With global teleporting (broadcast to all non-authority instances)
                        // we re-initialize authority's NetworkDeltaPosition and synchronize all
                        // non-authority instances with the new full precision position
                        if (networkState.IsTeleportingNextFrame)
                        {
                            m_HalfPositionState = new NetworkDeltaPosition(position, networkState.NetworkTick, math.bool3(SyncPositionX, SyncPositionY, SyncPositionZ));
                            networkState.CurrentPosition = position;
                        }
                        else // Otherwise, just synchronize the delta position value
                        {
                            m_HalfPositionState.HalfVector3.AxisToSynchronize = math.bool3(SyncPositionX, SyncPositionY, SyncPositionZ);
                            m_HalfPositionState.UpdateFrom(ref position, networkState.NetworkTick);
                        }

                        networkState.NetworkDeltaPosition = m_HalfPositionState;

                        // If ownership offset is greater or we are doing an axial synchronization then synchronize the base position 
                        if ((m_HalfFloatTargetTickOwnership > m_CachedNetworkManager.ServerTime.Tick || isAxisSync) && !networkState.IsTeleportingNextFrame)
                        {
                            networkState.SynchronizeBaseHalfFloat = true;
                        }
                        else
                        {
                            networkState.SynchronizeBaseHalfFloat = UseUnreliableDeltas ? m_HalfPositionState.CollapsedDeltaIntoBase : false;
                        }
                    }
                    else // If synchronizing is set, then use the current full position value on the server side
                    {
                        if (ShouldSynchronizeHalfFloat(targetClientId))
                        {
                            // If we have a NetworkDeltaPosition that has a state applied, then we want to determine
                            // what needs to be synchronized. For owner authoritative mode, the server side
                            // will have no valid state yet.
                            if (m_HalfPositionState.NetworkTick > 0)
                            {
                                // Always synchronize the base position and the ushort values of the
                                // current m_HalfPositionState
                                networkState.CurrentPosition = m_HalfPositionState.CurrentBasePosition;
                                networkState.NetworkDeltaPosition = m_HalfPositionState;
                                // If the server is the owner, in both server and owner authoritative modes,
                                // or we are running in server authoritative mode, then we use the
                                // HalfDeltaConvertedBack value as the delta position
                                if (NetworkObject.IsOwnedByServer || IsServerAuthoritative())
                                {
                                    networkState.DeltaPosition = m_HalfPositionState.HalfDeltaConvertedBack;
                                }
                                else
                                {
                                    // Otherwise, we are in owner authoritative mode and the server's NetworkDeltaPosition
                                    // state is "non-authoritative" relative so we use the DeltaPosition.
                                    networkState.DeltaPosition = m_HalfPositionState.DeltaPosition;
                                }
                            }
                            else // Reset everything and just send the current position
                            {
                                networkState.NetworkDeltaPosition = new NetworkDeltaPosition(Vector3.zero, 0, math.bool3(SyncPositionX, SyncPositionY, SyncPositionZ));
                                networkState.DeltaPosition = Vector3.zero;
                                networkState.CurrentPosition = position;
                            }
                        }
                        else
                        {
                            networkState.NetworkDeltaPosition = new NetworkDeltaPosition(Vector3.zero, 0, math.bool3(SyncPositionX, SyncPositionY, SyncPositionZ));
                            networkState.CurrentPosition = position;
                        }
                        // Add log entry for this update relative to the client being synchronized
                        AddLogEntry(ref networkState, targetClientId, true);
                    }
                    networkState.HasPositionX = SyncPositionX;
                    networkState.HasPositionY = SyncPositionY;
                    networkState.HasPositionZ = SyncPositionZ;
                }
            }

            if (!UseQuaternionSynchronization)
            {
                if (SyncRotAngleX && (Mathf.Abs(Mathf.DeltaAngle(networkState.RotAngleX, rotAngles.x)) >= rotationThreshold.x || networkState.IsTeleportingNextFrame || isAxisSync || forceState))
                {
                    networkState.RotAngleX = rotAngles.x;
                    networkState.HasRotAngleX = true;
                    isRotationDirty = true;
                }

                if (SyncRotAngleY && (Mathf.Abs(Mathf.DeltaAngle(networkState.RotAngleY, rotAngles.y)) >= rotationThreshold.y || networkState.IsTeleportingNextFrame || isAxisSync || forceState))
                {
                    networkState.RotAngleY = rotAngles.y;
                    networkState.HasRotAngleY = true;
                    isRotationDirty = true;
                }

                if (SyncRotAngleZ && (Mathf.Abs(Mathf.DeltaAngle(networkState.RotAngleZ, rotAngles.z)) >= rotationThreshold.z || networkState.IsTeleportingNextFrame || isAxisSync || forceState))
                {
                    networkState.RotAngleZ = rotAngles.z;
                    networkState.HasRotAngleZ = true;
                    isRotationDirty = true;
                }
            }
            else if (SynchronizeRotation)
            {
                // If we are teleporting then we can skip the delta threshold check
                isRotationDirty = networkState.IsTeleportingNextFrame || isAxisSync || forceState;
                // For quaternion synchronization, if one angle is dirty we send a full update
                if (!isRotationDirty)
                {
                    var previousRotation = networkState.Rotation.eulerAngles;
                    for (int i = 0; i < 3; i++)
                    {
                        if (Mathf.Abs(Mathf.DeltaAngle(previousRotation[i], rotAngles[i])) >= rotationThreshold[i])
                        {
                            isRotationDirty = true;
                            break;
                        }
                    }
                }
                if (isRotationDirty)
                {
                    networkState.Rotation = rotation;
                    networkState.HasRotAngleX = true;
                    networkState.HasRotAngleY = true;
                    networkState.HasRotAngleZ = true;
                }
            }

            // For scale, we need to check for parenting when synchronizing and/or teleporting (synchronization is always teleporting)
            if (networkState.IsTeleportingNextFrame)
            {
                // If we are synchronizing and the associated NetworkObject has a parent then we want to send the
                // LossyScale if the NetworkObject has a parent since NetworkObject spawn order is not guaranteed
                if (networkState.IsParented)
                {
                    networkState.LossyScale = transform.lossyScale;
                }
            }

            // Checking scale deltas when not synchronizing
            if (!isSynchronization)
            {
                if (!UseHalfFloatPrecision)
                {
                    if (SyncScaleX && (Mathf.Abs(networkState.ScaleX - scale.x) >= ScaleThreshold || networkState.IsTeleportingNextFrame || isAxisSync || forceState))
                    {
                        networkState.ScaleX = scale.x;
                        networkState.HasScaleX = true;
                        isScaleDirty = true;
                    }

                    if (SyncScaleY && (Mathf.Abs(networkState.ScaleY - scale.y) >= ScaleThreshold || networkState.IsTeleportingNextFrame || isAxisSync || forceState))
                    {
                        networkState.ScaleY = scale.y;
                        networkState.HasScaleY = true;
                        isScaleDirty = true;
                    }

                    if (SyncScaleZ && (Mathf.Abs(networkState.ScaleZ - scale.z) >= ScaleThreshold || networkState.IsTeleportingNextFrame || isAxisSync || forceState))
                    {
                        networkState.ScaleZ = scale.z;
                        networkState.HasScaleZ = true;
                        isScaleDirty = true;
                    }
                }
                else if (SynchronizeScale)
                {
                    var previousScale = networkState.Scale;
                    for (int i = 0; i < 3; i++)
                    {
                        if (Mathf.Abs(scale[i] - previousScale[i]) >= ScaleThreshold || networkState.IsTeleportingNextFrame || isAxisSync || forceState)
                        {
                            isScaleDirty = true;
                            networkState.Scale[i] = scale[i];
                            networkState.SetHasScale(i, i == 0 ? SyncScaleX : i == 1 ? SyncScaleY : SyncScaleZ);
                        }
                    }
                }
            }
            else // Just apply the full local scale when synchronizing
            if (SynchronizeScale)
            {
                if (!UseHalfFloatPrecision)
                {
                    networkState.ScaleX = transform.localScale.x;
                    networkState.ScaleY = transform.localScale.y;
                    networkState.ScaleZ = transform.localScale.z;
                }
                else
                {
                    networkState.Scale = transform.localScale;
                }
                networkState.HasScaleX = true;
                networkState.HasScaleY = true;
                networkState.HasScaleZ = true;
                isScaleDirty = true;
            }
            isDirty |= isPositionDirty || isRotationDirty || isScaleDirty;

            if (isDirty)
            {
                // Some integration/unit tests disable the NetworkTransform and there is no
                // NetworkManager
                if (enabled)
                {
                    // We use the NetworkTickSystem version since ServerTime is set when updating ticks
                    networkState.NetworkTick = m_CachedNetworkManager.NetworkTickSystem.ServerTime.Tick;
                }
            }

            // Mark the state dirty for the next network tick update to clear out the bitset values
            networkState.IsDirty |= isDirty;
            return isDirty;
        }

        /// <summary>
        /// Authority subscribes to network tick events and will invoke
        /// <see cref="OnUpdateAuthoritativeState(ref Transform)"/> each network tick.
        /// </summary>
        private void OnNetworkTick(bool isCalledFromParent = false)
        {
            // If not active, then ignore the update
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            // As long as we are still authority
            if (CanCommitToTransform)
            {
                if (m_CachedNetworkManager.DistributedAuthorityMode && !IsOwner)
                {
                    Debug.LogError($"Non-owner Client-{m_CachedNetworkManager.LocalClientId} is being updated by network tick still!!!!");
                    return;
                }

                // If we are nested and have already sent a state update this tick, then exit early (otherwise check for any changes in state)
                if (IsNested && m_LocalAuthoritativeNetworkState.NetworkTick == m_CachedNetworkManager.ServerTime.Tick)
                {
                    return;
                }

#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
                // Let the parent handle the updating of this to keep the two synchronized
                if (!isCalledFromParent && m_UseRigidbodyForMotion && m_NetworkRigidbodyInternal.ParentBody != null && !m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame)
                {
                    return;
                }
#endif

                // Update any changes to the transform
                var transformSource = transform;
                OnUpdateAuthoritativeState(ref transformSource, isCalledFromParent);
#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
                m_InternalCurrentPosition = m_TargetPosition = m_UseRigidbodyForMotion ? m_NetworkRigidbodyInternal.GetPosition() : GetSpaceRelativePosition();
#else
                m_InternalCurrentPosition = GetSpaceRelativePosition();
                m_TargetPosition = GetSpaceRelativePosition();
#endif
            }
            else // If we are no longer authority, unsubscribe to the tick event
            {
                DeregisterForTickUpdate(this);
            }
        }
        #endregion

        #region NON-AUTHORITY STATE UPDATE

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void UpdatePositionInterpolator(Vector3 position, double time, bool resetInterpolator = false)
        {
            if (!CanCommitToTransform)
            {
                if (resetInterpolator)
                {
                    m_PositionInterpolator.ResetTo(position, time);
                }
                else
                {
                    m_PositionInterpolator.AddMeasurement(position, time);
                }
            }
        }

        internal bool LogMotion;

        protected virtual void OnTransformUpdated()
        {

        }

        /// <summary>
        /// Applies the authoritative state to the transform
        /// </summary>
        protected internal void ApplyAuthoritativeState()
        {
#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
            // TODO: Make this an authority flag
            // For now, just synchronize with the NetworkRigidbodyBase UseRigidBodyForMotion
            if (m_NetworkRigidbodyInternal != null)
            {
                m_UseRigidbodyForMotion = m_NetworkRigidbodyInternal.UseRigidBodyForMotion;
            }
#endif
            var networkState = m_LocalAuthoritativeNetworkState;
            // The m_InternalCurrentPosition, m_InternalCurrentRotation, and m_InternalCurrentScale values are continually updated
            // at the end of this method and assure that when not interpolating the non-authoritative side
            // cannot make adjustments to any portions the transform not being synchronized.
            var adjustedPosition = m_InternalCurrentPosition;
            var adjustedRotation = m_InternalCurrentRotation;

            var adjustedRotAngles = adjustedRotation.eulerAngles;
            var adjustedScale = m_InternalCurrentScale;

            // Non-Authority Preservers the authority's transform state update modes
            InLocalSpace = networkState.InLocalSpace;
            Interpolate = networkState.UseInterpolation;
            UseHalfFloatPrecision = networkState.UseHalfFloatPrecision;
            UseQuaternionSynchronization = networkState.QuaternionSync;
            UseQuaternionCompression = networkState.QuaternionCompression;
            UseUnreliableDeltas = networkState.UseUnreliableDeltas;

            if (SlerpPosition != networkState.UsePositionSlerp)
            {
                SlerpPosition = networkState.UsePositionSlerp;
                UpdatePositionSlerp();
            }

            // NOTE ABOUT INTERPOLATING AND THE CODE BELOW:
            // We always apply the interpolated state for any axis we are synchronizing even when the state has no deltas
            // to assure we fully interpolate to our target even after we stop extrapolating 1 tick later.

            if (Interpolate)
            {
                if (SynchronizePosition)
                {
                    var interpolatedPosition = m_PositionInterpolator.GetInterpolatedValue();
                    if (UseHalfFloatPrecision)
                    {
                        adjustedPosition = interpolatedPosition;
                    }
                    else
                    {
                        if (SyncPositionX) { adjustedPosition.x = interpolatedPosition.x; }
                        if (SyncPositionY) { adjustedPosition.y = interpolatedPosition.y; }
                        if (SyncPositionZ) { adjustedPosition.z = interpolatedPosition.z; }
                    }
                }

                if (SynchronizeScale)
                {
                    if (UseHalfFloatPrecision)
                    {
                        adjustedScale = m_ScaleInterpolator.GetInterpolatedValue();
                    }
                    else
                    {
                        var interpolatedScale = m_ScaleInterpolator.GetInterpolatedValue();
                        if (SyncScaleX) { adjustedScale.x = interpolatedScale.x; }
                        if (SyncScaleY) { adjustedScale.y = interpolatedScale.y; }
                        if (SyncScaleZ) { adjustedScale.z = interpolatedScale.z; }
                    }
                }

                if (SynchronizeRotation)
                {
                    var interpolatedRotation = m_RotationInterpolator.GetInterpolatedValue();
                    if (UseQuaternionSynchronization)
                    {
                        adjustedRotation = interpolatedRotation;
                    }
                    else
                    {
                        var interpolatedEulerAngles = interpolatedRotation.eulerAngles;
                        if (SyncRotAngleX) { adjustedRotAngles.x = interpolatedEulerAngles.x; }
                        if (SyncRotAngleY) { adjustedRotAngles.y = interpolatedEulerAngles.y; }
                        if (SyncRotAngleZ) { adjustedRotAngles.z = interpolatedEulerAngles.z; }
                        adjustedRotation.eulerAngles = adjustedRotAngles;
                    }
                }
            }
            else
            {
                // Non-Interpolated Position and Scale
                if (UseHalfFloatPrecision)
                {
                    if (networkState.HasPositionChange && SynchronizePosition)
                    {
                        adjustedPosition = m_TargetPosition;
                    }

                    if (networkState.HasScaleChange && SynchronizeScale)
                    {
                        for (int i = 0; i < 3; i++)
                        {
                            if (m_LocalAuthoritativeNetworkState.HasScale(i))
                            {
                                adjustedScale[i] = m_LocalAuthoritativeNetworkState.Scale[i];
                            }
                        }
                    }
                }
                else
                {
                    if (networkState.HasPositionX) { adjustedPosition.x = networkState.PositionX; }
                    if (networkState.HasPositionY) { adjustedPosition.y = networkState.PositionY; }
                    if (networkState.HasPositionZ) { adjustedPosition.z = networkState.PositionZ; }
                    if (networkState.HasScaleX) { adjustedScale.x = networkState.ScaleX; }
                    if (networkState.HasScaleY) { adjustedScale.y = networkState.ScaleY; }
                    if (networkState.HasScaleZ) { adjustedScale.z = networkState.ScaleZ; }
                }

                // Non-interpolated rotation
                if (SynchronizeRotation)
                {
                    if (networkState.QuaternionSync && networkState.HasRotAngleChange)
                    {
                        adjustedRotation = networkState.Rotation;
                    }
                    else
                    {
                        if (networkState.HasRotAngleX) { adjustedRotAngles.x = networkState.RotAngleX; }
                        if (networkState.HasRotAngleY) { adjustedRotAngles.y = networkState.RotAngleY; }
                        if (networkState.HasRotAngleZ) { adjustedRotAngles.z = networkState.RotAngleZ; }
                        adjustedRotation.eulerAngles = adjustedRotAngles;
                    }
                }
            }

            // Apply the position if we are synchronizing position
            if (SynchronizePosition)
            {
                // Update our current position if it changed or we are interpolating
                if (networkState.HasPositionChange || Interpolate)
                {
                    m_InternalCurrentPosition = adjustedPosition;
                }
#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
                if (m_UseRigidbodyForMotion)
                {
                    m_NetworkRigidbodyInternal.MovePosition(m_InternalCurrentPosition);
                    if (LogMotion)
                    {
                        Debug.Log($"[Client-{m_CachedNetworkManager.LocalClientId}][Interpolate: {networkState.UseInterpolation}][TransPos: {transform.position}][RBPos: {m_NetworkRigidbodyInternal.GetPosition()}][CurrentPos: {m_InternalCurrentPosition}");
                    }

                }
                else
#endif
                {
                    if (PositionInLocalSpace)
                    {
                        // This handles the edge case of transitioning from local to world space where applying a local
                        // space value to a non-parented transform will be applied in world space. Since parenting is not
                        // tick synchronized, there can be one or two ticks between a state update with the InLocalSpace
                        // state update which can cause the body to seemingly "teleport" when it is just applying a local
                        // space value relative to world space 0,0,0.
                        if (SwitchTransformSpaceWhenParented && m_IsFirstNetworkTransform && Interpolate && m_PreviousNetworkObjectParent != null
                            && transform.parent == null)
                        {
                            m_InternalCurrentPosition = m_PreviousNetworkObjectParent.transform.TransformPoint(m_InternalCurrentPosition);
                            transform.position = m_InternalCurrentPosition;
                        }
                        else
                        {
                            transform.localPosition = m_InternalCurrentPosition;
                        }
                    }
                    else
                    {
                        transform.position = m_InternalCurrentPosition;
                    }
                }
            }

            // Apply the rotation if we are synchronizing rotation
            if (SynchronizeRotation)
            {
                // Update our current rotation if it changed or we are interpolating
                if (networkState.HasRotAngleChange || Interpolate)
                {
                    m_InternalCurrentRotation = adjustedRotation;
                }

#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
                if (m_UseRigidbodyForMotion)
                {
                    m_NetworkRigidbodyInternal.MoveRotation(m_InternalCurrentRotation);
                }
                else
#endif
                {
                    if (RotationInLocalSpace)
                    {
                        // This handles the edge case of transitioning from local to world space where applying a local
                        // space value to a non-parented transform will be applied in world space. Since parenting is not
                        // tick synchronized, there can be one or two ticks between a state update with the InLocalSpace
                        // state update which can cause the body to rotate world space relative and cause a slight rotation
                        // of the body in-between this transition period.
                        if (SwitchTransformSpaceWhenParented && m_IsFirstNetworkTransform && Interpolate && m_PreviousNetworkObjectParent != null && transform.parent == null)
                        {
                            m_InternalCurrentRotation = m_PreviousNetworkObjectParent.transform.rotation * m_InternalCurrentRotation;
                            transform.rotation = m_InternalCurrentRotation;
                        }
                        else
                        {
                            transform.localRotation = m_InternalCurrentRotation;
                        }
                    }
                    else
                    {
                        transform.rotation = m_InternalCurrentRotation;
                    }
                }
            }

            // Apply the scale if we are synchronizing scale
            if (SynchronizeScale)
            {
                // Update our current scale if it changed or we are interpolating
                if (networkState.HasScaleChange || Interpolate)
                {
                    m_InternalCurrentScale = adjustedScale;
                }
                transform.localScale = m_InternalCurrentScale;
            }
            OnTransformUpdated();
        }

        /// <summary>
        /// Handles applying the full authoritative state (i.e. teleporting)
        /// </summary>
        /// <remarks>
        /// Only non-authoritative instances should invoke this
        /// </remarks>
        private void ApplyTeleportingState(NetworkTransformState newState)
        {
            if (!newState.IsTeleportingNextFrame)
            {
                return;
            }

            var sentTime = newState.SentTime;
            var currentPosition = GetSpaceRelativePosition();
            var currentRotation = GetSpaceRelativeRotation();
            var currentEulerAngles = currentRotation.eulerAngles;
            var currentScale = transform.localScale;

            var isSynchronization = newState.IsSynchronizing;

            // Clear all interpolators
            m_ScaleInterpolator.Clear();
            m_PositionInterpolator.Clear();
            m_RotationInterpolator.Clear();

            if (newState.HasPositionChange)
            {
                if (!UseHalfFloatPrecision)
                {
                    // Adjust based on which axis changed
                    if (newState.HasPositionX)
                    {
                        currentPosition.x = newState.PositionX;
                    }

                    if (newState.HasPositionY)
                    {
                        currentPosition.y = newState.PositionY;
                    }

                    if (newState.HasPositionZ)
                    {
                        currentPosition.z = newState.PositionZ;
                    }
                }
                else
                {
                    // With delta position teleport updates or synchronization, we create a new instance and provide the current network tick.
                    m_HalfPositionState = new NetworkDeltaPosition(newState.CurrentPosition, newState.NetworkTick, math.bool3(SyncPositionX, SyncPositionY, SyncPositionZ));

                    // When first synchronizing we determine if we need to apply the current delta position
                    // offset or not. This is specific to owner authoritative mode on the owner side only
                    if (isSynchronization)
                    {
                        // Need to use NetworkManager vs m_CachedNetworkManager here since we are yet to be spawned
                        if (ShouldSynchronizeHalfFloat(NetworkManager.LocalClientId))
                        {
                            m_HalfPositionState.HalfVector3.Axis = newState.NetworkDeltaPosition.HalfVector3.Axis;
                            m_HalfPositionState.DeltaPosition = newState.DeltaPosition;
                            currentPosition = m_HalfPositionState.ToVector3(newState.NetworkTick);
                        }
                        else
                        {
                            currentPosition = newState.CurrentPosition;
                        }
                        // Before the state is applied add a log entry if AddLogEntry is assigned
                        AddLogEntry(ref newState, NetworkObject.OwnerClientId, true);
                    }
                    else
                    {
                        // If we are just teleporting, then we already created a new NetworkDeltaPosition value.
                        // set the current position to the state's current position
                        currentPosition = newState.CurrentPosition;
                    }
                }

                m_InternalCurrentPosition = currentPosition;
                m_TargetPosition = currentPosition;

                // Apply the position
                if (newState.InLocalSpace)
                {
                    transform.localPosition = currentPosition;
                }
                else
                {
                    transform.position = currentPosition;
                }

#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
                if (m_UseRigidbodyForMotion)
                {
                    m_NetworkRigidbodyInternal.SetPosition(transform.position);
                }
#endif

                if (Interpolate)
                {
                    UpdatePositionInterpolator(currentPosition, sentTime, true);
                }
            }

            if (newState.HasScaleChange)
            {
                bool shouldUseLossy = false;

                if (UseHalfFloatPrecision)
                {
                    currentScale = shouldUseLossy ? newState.LossyScale : newState.Scale;
                }
                else
                {
                    // Adjust based on which axis changed
                    if (newState.HasScaleX)
                    {
                        currentScale.x = shouldUseLossy ? newState.LossyScale.x : newState.ScaleX;
                    }

                    if (newState.HasScaleY)
                    {
                        currentScale.y = shouldUseLossy ? newState.LossyScale.y : newState.ScaleY;
                    }

                    if (newState.HasScaleZ)
                    {
                        currentScale.z = shouldUseLossy ? newState.LossyScale.z : newState.ScaleZ;
                    }
                }

                m_InternalCurrentScale = currentScale;
                m_TargetScale = currentScale;

                // Apply the adjusted scale
                transform.localScale = currentScale;

                if (Interpolate)
                {
                    m_ScaleInterpolator.ResetTo(currentScale, sentTime);
                }
            }

            if (newState.HasRotAngleChange)
            {
                if (newState.QuaternionSync)
                {
                    currentRotation = newState.Rotation;
                }
                else
                {
                    // Adjust based on which axis changed
                    if (newState.HasRotAngleX)
                    {
                        currentEulerAngles.x = newState.RotAngleX;
                    }

                    if (newState.HasRotAngleY)
                    {
                        currentEulerAngles.y = newState.RotAngleY;
                    }

                    if (newState.HasRotAngleZ)
                    {
                        currentEulerAngles.z = newState.RotAngleZ;
                    }
                    currentRotation.eulerAngles = currentEulerAngles;
                }

                m_InternalCurrentRotation = currentRotation;
                m_TargetRotation = currentRotation.eulerAngles;

                if (InLocalSpace)
                {
                    transform.localRotation = currentRotation;
                }
                else
                {
                    transform.rotation = currentRotation;
                }

#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
                if (m_UseRigidbodyForMotion)
                {
                    m_NetworkRigidbodyInternal.SetRotation(transform.rotation);
                }
#endif

                if (Interpolate)
                {
                    m_RotationInterpolator.ResetTo(currentRotation, sentTime);
                }
            }

            // Add log after to applying the update if AddLogEntry is defined
            if (isSynchronization)
            {
                AddLogEntry(ref newState, NetworkObject.OwnerClientId);
            }

            OnTransformUpdated();
        }

        /// <summary>
        /// Adds the new state's values to their respective interpolator
        /// </summary>
        /// <remarks>
        /// Only non-authoritative instances should invoke this
        /// </remarks>
        private void ApplyUpdatedState(NetworkTransformState newState)
        {
            // Set the transforms's synchronization modes
            InLocalSpace = newState.InLocalSpace;
            Interpolate = newState.UseInterpolation;
            UseQuaternionSynchronization = newState.QuaternionSync;
            UseQuaternionCompression = newState.QuaternionCompression;
            UseHalfFloatPrecision = newState.UseHalfFloatPrecision;
            UseUnreliableDeltas = newState.UseUnreliableDeltas;

            if (SlerpPosition != newState.UsePositionSlerp)
            {
                SlerpPosition = newState.UsePositionSlerp;
                UpdatePositionSlerp();
            }

            m_LocalAuthoritativeNetworkState = newState;
            if (m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame)
            {
                ApplyTeleportingState(m_LocalAuthoritativeNetworkState);
                return;
            }

            var sentTime = newState.SentTime;
            var currentRotation = GetSpaceRelativeRotation();

            // Only if using half float precision and our position had changed last update then
            if (UseHalfFloatPrecision && m_LocalAuthoritativeNetworkState.HasPositionChange)
            {
                if (m_LocalAuthoritativeNetworkState.SynchronizeBaseHalfFloat)
                {
                    m_HalfPositionState = m_LocalAuthoritativeNetworkState.NetworkDeltaPosition;
                }
                else
                {
                    // assure our local NetworkDeltaPosition state is updated
                    m_HalfPositionState.HalfVector3.Axis = m_LocalAuthoritativeNetworkState.NetworkDeltaPosition.HalfVector3.Axis;
                    m_LocalAuthoritativeNetworkState.NetworkDeltaPosition.CurrentBasePosition = m_HalfPositionState.CurrentBasePosition;

                    // This is to assure when you get the position of the state it is the correct position
                    m_LocalAuthoritativeNetworkState.NetworkDeltaPosition.ToVector3(0);
                }
                // Update our target position
                m_TargetPosition = m_HalfPositionState.ToVector3(newState.NetworkTick);
                m_LocalAuthoritativeNetworkState.CurrentPosition = m_TargetPosition;
            }

            if (!Interpolate)
            {
                return;
            }

            AdjustForChangeInTransformSpace();

            // Apply axial changes from the new state
            // Either apply the delta position target position or the current state's delta position
            // depending upon whether UsePositionDeltaCompression is enabled
            if (m_LocalAuthoritativeNetworkState.HasPositionChange)
            {
                if (!m_LocalAuthoritativeNetworkState.UseHalfFloatPrecision)
                {
                    var position = m_LocalAuthoritativeNetworkState.GetPosition();
                    var newTargetPosition = m_TargetPosition;
                    if (m_LocalAuthoritativeNetworkState.HasPositionX)
                    {
                        newTargetPosition.x = position.x;
                    }

                    if (m_LocalAuthoritativeNetworkState.HasPositionY)
                    {
                        newTargetPosition.y = position.y;
                    }

                    if (m_LocalAuthoritativeNetworkState.HasPositionZ)
                    {
                        newTargetPosition.z = position.z;
                    }
                    m_TargetPosition = newTargetPosition;
                }
                UpdatePositionInterpolator(m_TargetPosition, sentTime);
            }

            if (m_LocalAuthoritativeNetworkState.HasScaleChange)
            {
                var currentScale = m_TargetScale;
                if (UseHalfFloatPrecision)
                {
                    for (int i = 0; i < 3; i++)
                    {
                        if (m_LocalAuthoritativeNetworkState.HasScale(i))
                        {
                            currentScale[i] = m_LocalAuthoritativeNetworkState.Scale[i];
                        }
                    }
                }
                else
                {
                    if (m_LocalAuthoritativeNetworkState.HasScaleX)
                    {
                        currentScale.x = m_LocalAuthoritativeNetworkState.ScaleX;
                    }

                    if (m_LocalAuthoritativeNetworkState.HasScaleY)
                    {
                        currentScale.y = m_LocalAuthoritativeNetworkState.ScaleY;
                    }

                    if (m_LocalAuthoritativeNetworkState.HasScaleZ)
                    {
                        currentScale.z = m_LocalAuthoritativeNetworkState.ScaleZ;
                    }
                }
                m_TargetScale = currentScale;
                m_ScaleInterpolator.AddMeasurement(currentScale, sentTime);
            }

            // With rotation, we check if there are any changes first and
            // if so then apply the changes to the current Euler rotation
            // values.
            if (m_LocalAuthoritativeNetworkState.HasRotAngleChange)
            {
                if (m_LocalAuthoritativeNetworkState.QuaternionSync)
                {
                    currentRotation = m_LocalAuthoritativeNetworkState.Rotation;
                }
                else
                {
                    var currentEulerAngles = m_TargetRotation;
                    // Adjust based on which axis changed
                    // (both half precision and full precision apply Eulers to the RotAngle properties when reading the update)
                    if (m_LocalAuthoritativeNetworkState.HasRotAngleX)
                    {
                        currentEulerAngles.x = m_LocalAuthoritativeNetworkState.RotAngleX;
                    }

                    if (m_LocalAuthoritativeNetworkState.HasRotAngleY)
                    {
                        currentEulerAngles.y = m_LocalAuthoritativeNetworkState.RotAngleY;
                    }

                    if (m_LocalAuthoritativeNetworkState.HasRotAngleZ)
                    {
                        currentEulerAngles.z = m_LocalAuthoritativeNetworkState.RotAngleZ;
                    }
                    m_TargetRotation = currentEulerAngles;
                    currentRotation.eulerAngles = currentEulerAngles;
                }

                m_RotationInterpolator.AddMeasurement(currentRotation, sentTime);
            }
        }

        /// <summary>
        /// Invoked on the non-authoritative side when the NetworkTransformState has been updated
        /// </summary>
        /// <param name="oldState">the previous <see cref="NetworkTransformState"/></param>
        /// <param name="newState">the new <see cref="NetworkTransformState"/></param>
        protected virtual void OnNetworkTransformStateUpdated(ref NetworkTransformState oldState, ref NetworkTransformState newState)
        {

        }

        protected virtual void OnBeforeUpdateTransformState()
        {

        }

        internal bool LogStateUpdate;
        /// <summary>
        /// Only non-authoritative instances should invoke this method
        /// </summary>
        private void OnNetworkStateChanged(NetworkTransformState oldState, NetworkTransformState newState)
        {
            if (!NetworkObject.IsSpawned || CanCommitToTransform)
            {
                return;
            }

            // If we are using UseUnreliableDeltas and our old state tick is greater than the new state tick,
            // then just ignore the newstate. This avoids any scenario where the new state is out of order
            // from the old state (with unreliable traffic and/or mixed unreliable and reliable)
            if (UseUnreliableDeltas && oldState.NetworkTick > newState.NetworkTick && !newState.IsTeleportingNextFrame && !newState.UnreliableFrameSync)
            {
                return;
            }

            // Get the time when this new state was sent
            newState.SentTime = new NetworkTime(m_CachedNetworkManager.NetworkConfig.TickRate, newState.NetworkTick).Time;

            if (LogStateUpdate)
            {
                var builder = new StringBuilder();
                builder.AppendLine($"[Client-{m_CachedNetworkManager.LocalClientId}][State Update: {newState.GetNetworkTick()}][HasPos: {newState.HasPositionChange}][Has Rot: {newState.HasRotAngleChange}][Has Scale: {newState.HasScaleChange}]");
                if (newState.HasPositionChange)
                {
                    builder.AppendLine($"Position = {newState.GetPosition()}");
                }
                if (newState.HasRotAngleChange)
                {
                    builder.AppendLine($"Rotation = {newState.GetRotation()}");
                }
                if (newState.HasScaleChange)
                {
                    builder.AppendLine($"Scale = {newState.GetScale()}");
                }
                Debug.Log(builder);
            }

            // Notification prior to applying a state update
            OnBeforeUpdateTransformState();

            // Apply the new state
            ApplyUpdatedState(newState);

            // Tick synchronize any parented child NetworkObject(s) NetworkTransform(s)
            if (TickSyncChildren && m_IsFirstNetworkTransform)
            {
                // Synchronize any nested NetworkTransforms with the parent's
                foreach (var childNetworkTransform in NetworkObject.NetworkTransforms)
                {
                    // Don't update the same instance
                    if (childNetworkTransform == this)
                    {
                        continue;
                    }
                    if (childNetworkTransform.CanCommitToTransform)
                    {
                        childNetworkTransform.OnNetworkTick(true);
                    }
                }

                // Synchronize any parented children with the parent's motion
                foreach (var child in m_ParentedChildren)
                {
                    // Synchronize any nested NetworkTransforms of the child with the parent's
                    foreach (var childNetworkTransform in child.NetworkTransforms)
                    {
                        if (childNetworkTransform.CanCommitToTransform)
                        {
                            childNetworkTransform.OnNetworkTick(true);
                        }
                    }
                }
            }

            // Provide notifications when the state has been updated
            // We use the m_LocalAuthoritativeNetworkState because newState has been applied and adjustments could have
            // been made (i.e. half float precision position values will have been updated)
            OnNetworkTransformStateUpdated(ref oldState, ref m_LocalAuthoritativeNetworkState);
        }

        /// <summary>
        /// Will set the maximum interpolation boundary for the interpolators of this <see cref="NetworkTransform"/> instance.
        /// This value roughly translates to the maximum value of 't' in <see cref="Vector3.Lerp(Vector3, Vector3, float)"/> and
        /// <see cref="Quaternion.Lerp(Quaternion, Quaternion, float)"/> for all transform elements being monitored by
        /// <see cref="NetworkTransform"/> (i.e. Position, Scale, and Rotation)
        /// </summary>
        /// <param name="maxInterpolationBound">Maximum time boundary that can be used in a frame when interpolating between two values</param>
        public void SetMaxInterpolationBound(float maxInterpolationBound)
        {
            m_RotationInterpolator.MaxInterpolationBound = maxInterpolationBound;
            m_PositionInterpolator.MaxInterpolationBound = maxInterpolationBound;
            m_ScaleInterpolator.MaxInterpolationBound = maxInterpolationBound;
        }

        /// <summary>
        /// Checks for changes in the axis to synchronize. If one or more did change it
        /// then determines if the axis were enabled and if the delta between the last known
        /// delta position and the current position for the axis exceeds the adjustment range
        /// before it is collapsed into the base position.
        /// If it does exceed the adjustment range, then we have to teleport the object so
        /// a full position synchronization takes place and the NetworkDeltaPosition is
        /// reset with the updated base position that it then will generating a new delta position from.
        /// </summary>
        /// <remarks>
        /// This only happens if a user disables an axis, continues to update the disabled axis,
        /// and then later enables the axis. (which will not be a recommended best practice)
        /// </remarks>
        private void AxisChangedDeltaPositionCheck()
        {
            if (UseHalfFloatPrecision && SynchronizePosition)
            {
                var synAxis = m_HalfPositionState.HalfVector3.AxisToSynchronize;
                if (SyncPositionX != synAxis.x || SyncPositionY != synAxis.y || SyncPositionZ != synAxis.z)
                {
                    var positionState = m_HalfPositionState.GetFullPosition();
                    var relativePosition = GetSpaceRelativePosition();
                    bool needsToTeleport = false;
                    // Only if the synchronization of an axis is turned on do we need to
                    // check if a teleport is required due to the delta from the last known
                    // to the currently known axis value exceeds MaxDeltaBeforeAdjustment.
                    if (SyncPositionX && SyncPositionX != synAxis.x)
                    {
                        needsToTeleport = Mathf.Abs(relativePosition.x - positionState.x) >= NetworkDeltaPosition.MaxDeltaBeforeAdjustment;
                    }
                    if (SyncPositionY && SyncPositionY != synAxis.y)
                    {
                        needsToTeleport = Mathf.Abs(relativePosition.y - positionState.y) >= NetworkDeltaPosition.MaxDeltaBeforeAdjustment;
                    }
                    if (SyncPositionZ && SyncPositionZ != synAxis.z)
                    {
                        needsToTeleport = Mathf.Abs(relativePosition.z - positionState.z) >= NetworkDeltaPosition.MaxDeltaBeforeAdjustment;
                    }
                    // If needed, force a teleport as the delta is outside of the valid delta boundary
                    m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame = needsToTeleport;
                }
            }
        }

        /// <summary>
        /// Called by authority to check for deltas and update non-authoritative instances
        /// if any are found.
        /// </summary>
        internal void OnUpdateAuthoritativeState(ref Transform transformSource, bool settingState = false)
        {
            // If our replicated state is not dirty and our local authority state is dirty, clear it.
            if (!m_LocalAuthoritativeNetworkState.ExplicitSet && m_LocalAuthoritativeNetworkState.IsDirty && !m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame)
            {
                // Now clear our bitset and prepare for next network tick state update
                m_LocalAuthoritativeNetworkState.ClearBitSetForNextTick();
                if (TrackByStateId)
                {
                    m_LocalAuthoritativeNetworkState.TrackByStateId = true;
                    m_LocalAuthoritativeNetworkState.StateId++;
                }
                else
                {
                    m_LocalAuthoritativeNetworkState.TrackByStateId = false;
                }
            }

            AxisChangedDeltaPositionCheck();

            TryCommitTransform(ref transformSource, settingState: settingState);
        }
        #endregion

        #region SPAWN, DESPAWN, AND INITIALIZATION

        private void NonAuthorityFinalizeSynchronization()
        {
            // For all child NetworkTransforms nested under the same NetworkObject,
            // we apply the initial synchronization based on their parented/ordered
            // heirarchy.
            if (SynchronizeState.IsSynchronizing && m_IsFirstNetworkTransform)
            {
                foreach (var child in NetworkObject.NetworkTransforms)
                {
                    child.ApplySynchronization();

                    // For all nested (under the root/same NetworkObject) child NetworkTransforms, we need to run through
                    // initialization once more to assure any values applied or stored are relative to the Root's transform.
                    child.InternalInitialization();
                }
            }
        }

        /// <summary>
        /// Handle applying the synchronization state once everything has spawned.
        /// The first NetowrkTransform handles invoking this on any other nested NetworkTransform.
        /// </summary>
        protected internal override void InternalOnNetworkSessionSynchronized()
        {
            NonAuthorityFinalizeSynchronization();

            base.InternalOnNetworkSessionSynchronized();
        }

        private void ApplyPlayerTransformState()
        {
            SynchronizeState.InLocalSpace = InLocalSpace;
            SynchronizeState.UseInterpolation = Interpolate;
            SynchronizeState.QuaternionSync = UseQuaternionSynchronization;
            SynchronizeState.UseHalfFloatPrecision = UseHalfFloatPrecision;
            SynchronizeState.QuaternionCompression = UseQuaternionCompression;
            SynchronizeState.UsePositionSlerp = SlerpPosition;
        }

        /// <summary>
        /// For dynamically spawned NetworkObjects, when the non-authority instance's client is already connected and
        /// the SynchronizeState is still pending synchronization then we want to finalize the synchornization at this time.
        /// </summary>
        protected internal override void InternalOnNetworkPostSpawn()
        {
            // This is a special case for client-server where a server is spawning an owner authoritative NetworkObject but has yet to serialize anything.
            // When the server detects that:
            // - We are not in a distributed authority session (DAHost check).
            // - This is the first/root NetworkTransform.
            // - We are in owner authoritative mode.
            // - The NetworkObject is not owned by the server.
            // - The SynchronizeState.IsSynchronizing is set to false.
            // Then we want to:
            // - Force the "IsSynchronizing" flag so the NetworkTransform has its state updated properly and runs through the initialization again.
            // - Make sure the SynchronizingState is updated to the instantiated prefab's default flags/settings.
            if (NetworkManager.IsServer && !NetworkManager.DistributedAuthorityMode && m_IsFirstNetworkTransform && !OnIsServerAuthoritative() && !IsOwner && !SynchronizeState.IsSynchronizing)
            {
                // Assure the first/root NetworkTransform has the synchronizing flag set so the server runs through the final post initialization steps
                SynchronizeState.IsSynchronizing = true;
                // Assure the SynchronizeState matches the initial prefab's values for each associated NetworkTransfrom (this includes root + all children)
                foreach (var child in NetworkObject.NetworkTransforms)
                {
                    child.ApplyPlayerTransformState();
                }
                // Now fall through to the final synchronization portion of the spawning for NetworkTransform
            }

            if (!CanCommitToTransform && NetworkManager.IsConnectedClient && SynchronizeState.IsSynchronizing)
            {
                NonAuthorityFinalizeSynchronization();
            }

            base.InternalOnNetworkPostSpawn();
        }

        /// <summary>
        /// Create interpolators when first instantiated to avoid memory allocations if the
        /// associated NetworkObject persists (i.e. despawned but not destroyed or pools)
        /// </summary>
        protected virtual void Awake()
        {
            // Rotation is a single Quaternion since each Euler axis will affect the quaternion's final value
            m_RotationInterpolator = new BufferedLinearInterpolatorQuaternion();
            m_PositionInterpolator = new BufferedLinearInterpolatorVector3();
            m_ScaleInterpolator = new BufferedLinearInterpolatorVector3();
        }

        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            m_ParentedChildren.Clear();
            m_CachedNetworkManager = NetworkManager;

            Initialize();

            if (CanCommitToTransform)
            {
                SetState(GetSpaceRelativePosition(), GetSpaceRelativeRotation(), GetScale(), false);
            }
        }

        private void CleanUpOnDestroyOrDespawn()
        {
            m_ParentedChildren.Clear();
#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
            var forUpdate = !m_UseRigidbodyForMotion;
#else
            var forUpdate = true;
#endif
            if (m_CachedNetworkObject != null)
            {
                NetworkManager?.NetworkTransformRegistration(m_CachedNetworkObject, forUpdate, false);
            }

            DeregisterForTickUpdate(this);
            CanCommitToTransform = false;
        }

        /// <inheritdoc/>
        public override void OnNetworkDespawn()
        {
            CleanUpOnDestroyOrDespawn();
            base.OnNetworkDespawn();
        }

        /// <inheritdoc/>
        public override void OnDestroy()
        {
            CleanUpOnDestroyOrDespawn();
            base.OnDestroy();
        }

        /// <summary>
        /// Invoked when first spawned and when ownership changes.
        /// </summary>
        /// <param name="replicatedState">the current <see cref="NetworkTransformState"/> after initializing</param>
        protected virtual void OnInitialize(ref NetworkTransformState replicatedState)
        {
        }

        /// <summary>
        /// This method is only invoked by the owner
        /// Use: OnInitialize(ref NetworkTransformState replicatedState) to be notified on all instances
        /// </summary>
        /// <param name="replicatedState"></param>
        protected virtual void OnInitialize(ref NetworkVariable<NetworkTransformState> replicatedState)
        {

        }

        private int m_HalfFloatTargetTickOwnership;

        /// <summary>
        /// Initializes the interpolators with the current transform values
        /// </summary>
        private void ResetInterpolatedStateToCurrentAuthoritativeState()
        {
            var serverTime = NetworkManager.ServerTime.Time;
#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
            var position = m_UseRigidbodyForMotion ? m_NetworkRigidbodyInternal.GetPosition() : GetSpaceRelativePosition();
            var rotation = m_UseRigidbodyForMotion ? m_NetworkRigidbodyInternal.GetRotation() : GetSpaceRelativeRotation();
#else
            var position = GetSpaceRelativePosition();
            var rotation = GetSpaceRelativeRotation();
#endif
            m_PositionInterpolator.InLocalSpace = InLocalSpace;
            m_RotationInterpolator.InLocalSpace = InLocalSpace;

            UpdatePositionInterpolator(position, serverTime, true);
            UpdatePositionSlerp();

            m_ScaleInterpolator.ResetTo(transform.localScale, serverTime);
            m_RotationInterpolator.ResetTo(rotation, serverTime);
        }
        private NetworkObject m_CachedNetworkObject;
        /// <summary>
        /// The internal initialzation method to allow for internal API adjustments
        /// </summary>
        /// <param name="isOwnershipChange"></param>
        private void InternalInitialization(bool isOwnershipChange = false)
        {
            if (!IsSpawned)
            {
                return;
            }
            m_CachedNetworkObject = NetworkObject;

            // Determine if this is the first NetworkTransform in the associated NetworkObject's list
            m_IsFirstNetworkTransform = NetworkObject.NetworkTransforms[0] == this;


            if (m_CachedNetworkManager && m_CachedNetworkManager.DistributedAuthorityMode)
            {
                AuthorityMode = AuthorityModes.Owner;
            }
            CanCommitToTransform = IsServerAuthoritative() ? IsServer : IsOwner;

            if (SwitchTransformSpaceWhenParented)
            {
                if (CanCommitToTransform)
                {
                    InLocalSpace = transform.parent != null;
                }
                // Always apply this if SwitchTransformSpaceWhenParented is set.
                TickSyncChildren = true;
            }

            var currentPosition = GetSpaceRelativePosition();
            var currentRotation = GetSpaceRelativeRotation();

            if (NetworkManager.DistributedAuthorityMode)
            {
                RegisterNetworkManagerForTickUpdate(NetworkManager);
                m_NetworkTransformTickRegistration = s_NetworkTickRegistration[m_CachedNetworkManager];
            }

#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
            // Depending upon order of operations, we invoke this in order to assure that proper settings are applied.
            if (m_NetworkRigidbodyInternal)
            {
                m_NetworkRigidbodyInternal.UpdateOwnershipAuthority();
            }

            if (m_UseRigidbodyForMotion)
            {
                m_NetworkRigidbodyInternal.SetPosition(currentPosition);
                m_NetworkRigidbodyInternal.SetRotation(currentRotation);
            }

            var forUpdate = !m_UseRigidbodyForMotion;
#else
            var forUpdate = true;
#endif
            m_LocalAuthoritativeNetworkState.SynchronizeBaseHalfFloat = false;

            if (CanCommitToTransform)
            {
                // Make sure authority doesn't get added to updates (no need to do this on the authority side)
                m_CachedNetworkManager.NetworkTransformRegistration(NetworkObject, forUpdate, false);
                if (UseHalfFloatPrecision)
                {
                    m_HalfPositionState = new NetworkDeltaPosition(currentPosition, m_CachedNetworkManager.ServerTime.Tick, math.bool3(SyncPositionX, SyncPositionY, SyncPositionZ));
                    m_LocalAuthoritativeNetworkState.SynchronizeBaseHalfFloat = isOwnershipChange;
                    SetState(teleportDisabled: false);
                }

                m_InternalCurrentPosition = currentPosition;
                m_TargetPosition = currentPosition;

                RegisterForTickUpdate(this);

                if (UseHalfFloatPrecision && isOwnershipChange && !IsServerAuthoritative() && Interpolate)
                {
                    m_HalfFloatTargetTickOwnership = m_CachedNetworkManager.ServerTime.Tick;
                }
            }
            else
            {
                // Non-authority needs to be added to updates for interpolation and applying state purposes
                m_CachedNetworkManager.NetworkTransformRegistration(NetworkObject, forUpdate, true);
                // Remove this instance from the tick update
                DeregisterForTickUpdate(this);
                ResetInterpolatedStateToCurrentAuthoritativeState();
                m_InternalCurrentPosition = currentPosition;
                m_TargetPosition = currentPosition;
                m_InternalCurrentScale = transform.localScale;
                m_TargetScale = transform.localScale;
                m_InternalCurrentRotation = currentRotation;
                m_TargetRotation = currentRotation.eulerAngles;
            }
            OnInitialize(ref m_LocalAuthoritativeNetworkState);
        }

        /// <summary>
        /// Initializes NetworkTransform when spawned and ownership changes.
        /// </summary>
        protected void Initialize()
        {
            InternalInitialization();
        }
        #endregion

        #region PARENTING AND OWNERSHIP
        // This might seem aweful, but when transitioning between two parents in local space we need to
        // catch the moment the transition happens and only apply the special case parenting from one parent
        // to another parent once. Keeping track of the "previous previous" allows us to detect the
        // back and fourth scenario:
        // - No parent (world space)
        // - Parent under NetworkObjectA (world to local)
        // - Parent under NetworkObjectB (local to local) (catch with "previous previous")
        // - Parent under NetworkObjectA (local to local) (catch with "previous previous")
        // - Parent under NetworkObjectB (local to local) (catch with "previous previous")
        private NetworkObject m_PreviousCurrentParent;
        private NetworkObject m_PreviousPreviousParent;
        private void AdjustForChangeInTransformSpace()
        {
            if (SwitchTransformSpaceWhenParented && m_IsFirstNetworkTransform && (m_PositionInterpolator.InLocalSpace != InLocalSpace ||
                m_RotationInterpolator.InLocalSpace != InLocalSpace ||
                (InLocalSpace && m_CurrentNetworkObjectParent && m_PreviousNetworkObjectParent && m_PreviousCurrentParent != m_CurrentNetworkObjectParent && m_PreviousPreviousParent != m_PreviousNetworkObjectParent)))
            {
                var parent = m_CurrentNetworkObjectParent ? m_CurrentNetworkObjectParent : m_PreviousNetworkObjectParent;
                if (parent)
                {
                    // In the event it is a NetworkObject to NetworkObject parenting transfer, we will need to migrate our interpolators
                    // and our current position and rotation to world space relative to the previous parent before converting them to local
                    // space relative to the new parent
                    if (InLocalSpace && m_CurrentNetworkObjectParent && m_PreviousNetworkObjectParent)
                    {
                        m_PreviousCurrentParent = m_CurrentNetworkObjectParent;
                        m_PreviousPreviousParent = m_PreviousNetworkObjectParent;
                        // Convert our current postion and rotation to world space based on the previous parent's transform
                        m_InternalCurrentPosition = m_PreviousNetworkObjectParent.transform.TransformPoint(m_InternalCurrentPosition);
                        m_InternalCurrentRotation = m_PreviousNetworkObjectParent.transform.rotation * m_InternalCurrentRotation;
                        // Convert our current postion and rotation to local space based on the current parent's transform
                        m_InternalCurrentPosition = m_CurrentNetworkObjectParent.transform.InverseTransformPoint(m_InternalCurrentPosition);
                        m_InternalCurrentRotation = Quaternion.Inverse(m_CurrentNetworkObjectParent.transform.rotation) * m_InternalCurrentRotation;
                        // Convert both interpolators to world space based on the previous parent's transform
                        m_PositionInterpolator.ConvertTransformSpace(m_PreviousNetworkObjectParent.transform, false);
                        m_RotationInterpolator.ConvertTransformSpace(m_PreviousNetworkObjectParent.transform, false);
                        // Next, fall into normal transform space conversion of both interpolators to local space based on the current parent's transform
                    }

                    m_PositionInterpolator.ConvertTransformSpace(parent.transform, InLocalSpace);
                    m_RotationInterpolator.ConvertTransformSpace(parent.transform, InLocalSpace);
                }
            }
        }

        /// <inheritdoc/>
        public override void OnLostOwnership()
        {
            base.OnLostOwnership();
        }

        /// <inheritdoc/>
        public override void OnGainedOwnership()
        {
            base.OnGainedOwnership();
        }

        protected override void OnOwnershipChanged(ulong previous, ulong current)
        {
            // If we were the previous owner or the newly assigned owner then reinitialize
            if (current == m_CachedNetworkManager.LocalClientId || previous == m_CachedNetworkManager.LocalClientId)
            {
                InternalInitialization(true);
            }
            base.OnOwnershipChanged(previous, current);
        }

        internal bool IsNested;
        private List<NetworkObject> m_ParentedChildren = new List<NetworkObject>();

        private bool m_IsFirstNetworkTransform;
        private NetworkObject m_CurrentNetworkObjectParent = null;
        private NetworkObject m_PreviousNetworkObjectParent = null;

        internal void ChildRegistration(NetworkObject child, bool isAdding)
        {
            if (isAdding)
            {
                m_ParentedChildren.Add(child);
            }
            else
            {
                m_ParentedChildren.Remove(child);
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// When not using a NetworkRigidbody and using an owner authoritative motion model, you can <br />
        /// improve parenting transitions into and out of world and local space by:<br />
        /// - Disabling <see cref="NetworkObject.SyncOwnerTransformWhenParented"/><br />
        /// - Enabling <see cref="NetworkObject.AllowOwnerToParent"/><br />
        /// - Enabling <see cref="SwitchTransformSpaceWhenParented"/><br />
        /// -- Note: This handles changing from world space to local space for you.<br />
        /// When these settings are applied, transitioning from: <br />
        /// - World space to local space (root-null parent/null to <see cref="NetworkObject"/> parent)
        /// - Local space back to world space (<see cref="NetworkObject"/> parent to root-null parent)
        /// - Local space to local space (<see cref="NetworkObject"/> parent to <see cref="NetworkObject"/> parent)
        /// Will all smoothly transition while interpolation is enabled.
        /// (Does not work if using a <see cref="Rigidbody"/> or <see cref="Rigidbody2D"/> for motion)
        /// 
        /// When a parent changes, non-authoritative instances should:<br />
        /// - Apply the resultant position, rotation, and scale from the parenting action.<br />
        /// - Clear interpolators (even if not enabled on this frame)<br />
        /// - Reset the interpolators to the position, rotation, and scale resultant values.<br />
        /// This prevents interpolation visual anomalies and issues during initial synchronization<br />
        /// </remarks>
        public override void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject)
        {
            base.OnNetworkObjectParentChanged(parentNetworkObject);
        }


        internal override void InternalOnNetworkObjectParentChanged(NetworkObject parentNetworkObject)
        {
            // The root NetworkTransform handles tracking any NetworkObject parenting since nested NetworkTransforms (of the same NetworkObject)
            // will never (or rather should never) change their world space once spawned.
#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
            // Handling automatic transform space switching can only be applied to NetworkTransforms that don't use the Rigidbody for motion
            if (!m_UseRigidbodyForMotion && SwitchTransformSpaceWhenParented)
#else
            if (SwitchTransformSpaceWhenParented)
#endif
            {
                m_PreviousNetworkObjectParent = m_CurrentNetworkObjectParent;
                m_CurrentNetworkObjectParent = parentNetworkObject;
                if (m_IsFirstNetworkTransform)
                {
                    if (CanCommitToTransform)
                    {
                        InLocalSpace = m_CurrentNetworkObjectParent != null;
                    }
                    if (m_PreviousNetworkObjectParent && m_PreviousNetworkObjectParent.NetworkTransforms != null && m_PreviousNetworkObjectParent.NetworkTransforms.Count > 0)
                    {
                        // Always deregister with the first NetworkTransform in the list
                        m_PreviousNetworkObjectParent.NetworkTransforms[0].ChildRegistration(NetworkObject, false);
                    }
                    if (m_CurrentNetworkObjectParent && m_CurrentNetworkObjectParent.NetworkTransforms != null && m_CurrentNetworkObjectParent.NetworkTransforms.Count > 0)
                    {
                        // Always register with the first NetworkTransform in the list
                        m_CurrentNetworkObjectParent.NetworkTransforms[0].ChildRegistration(NetworkObject, true);
                    }
                }
            }
            else
            {
                // Keep the same legacy behaviour for compatibility purposes
                if (!CanCommitToTransform)
                {
#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
                    var position = m_UseRigidbodyForMotion ? m_NetworkRigidbodyInternal.GetPosition() : GetSpaceRelativePosition();
                    var rotation = m_UseRigidbodyForMotion ? m_NetworkRigidbodyInternal.GetRotation() : GetSpaceRelativeRotation();
#else
                    var position = GetSpaceRelativePosition();
                    var rotation = GetSpaceRelativeRotation();
#endif
                    m_TargetPosition = m_InternalCurrentPosition = position;
                    m_InternalCurrentRotation = rotation;
                    m_TargetRotation = m_InternalCurrentRotation.eulerAngles;
                    m_TargetScale = m_InternalCurrentScale = GetScale();

                    if (Interpolate)
                    {
                        m_ScaleInterpolator.Clear();
                        m_PositionInterpolator.Clear();
                        m_RotationInterpolator.Clear();

                        // Always use NetworkManager here as this can be invoked prior to spawning
                        var tempTime = new NetworkTime(NetworkManager.NetworkConfig.TickRate, NetworkManager.ServerTime.Tick).Time;
                        UpdatePositionInterpolator(m_InternalCurrentPosition, tempTime, true);
                        m_ScaleInterpolator.ResetTo(m_InternalCurrentScale, tempTime);
                        m_RotationInterpolator.ResetTo(m_InternalCurrentRotation, tempTime);
                    }
                }
            }
            base.InternalOnNetworkObjectParentChanged(parentNetworkObject);
        }
        #endregion

        #region API STATE UPDATE METHODS
        /// <summary>
        /// Directly sets a state on the authoritative transform.
        /// Owner clients can directly set the state on a server authoritative transform
        /// This will override any changes made previously to the transform
        /// This isn't resistant to network jitter. Server side changes due to this method won't be interpolated.
        /// The parameters are broken up into pos / rot / scale on purpose so that the caller can perturb
        ///  just the desired one(s)
        /// </summary>
        /// <param name="posIn"></param> new position to move to.  Can be null
        /// <param name="rotIn"></param> new rotation to rotate to.  Can be null
        /// <param name="scaleIn">new scale to scale to. Can be null</param>
        /// <param name="teleportDisabled">When true (the default) the <see cref="NetworkObject"/> will not be teleported and, if enabled, will interpolate. When false the <see cref="NetworkObject"/> will teleport/apply the parameters provided immediately.</param>
        /// <exception cref="Exception"></exception>
        public void SetState(Vector3? posIn = null, Quaternion? rotIn = null, Vector3? scaleIn = null, bool teleportDisabled = true)
        {
            if (!IsSpawned)
            {
                NetworkLog.LogError($"Cannot commit transform when not spawned!");
                return;
            }

            // Only the server or the owner is allowed to commit a transform
            if (!IsServer && !IsOwner)
            {
                var errorMessage = gameObject != NetworkObject.gameObject ?
                    $"Non-authority instance of {NetworkObject.gameObject.name} is trying to commit a transform on {gameObject.name}!" :
                    $"Non-authority instance of {NetworkObject.gameObject.name} is trying to commit a transform!";
                NetworkLog.LogError(errorMessage);
                return;
            }
#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
            var position = m_UseRigidbodyForMotion ? m_NetworkRigidbodyInternal.GetPosition() : GetSpaceRelativePosition();
            var rotation = m_UseRigidbodyForMotion ? m_NetworkRigidbodyInternal.GetRotation() : GetSpaceRelativeRotation();
#else
            var position = GetSpaceRelativePosition();
            var rotation = GetSpaceRelativeRotation();
#endif
            Vector3 pos = posIn == null ? position : posIn.Value;
            Quaternion rot = rotIn == null ? rotation : rotIn.Value;
            Vector3 scale = scaleIn == null ? transform.localScale : scaleIn.Value;

            if (!CanCommitToTransform)
            {
                // Preserving the ability for owner authoritative mode to accept state changes from server
                if (IsServer)
                {
                    m_ClientIds[0] = OwnerClientId;
                    m_ClientRpcParams.Send.TargetClientIds = m_ClientIds;
                    SetStateClientRpc(pos, rot, scale, !teleportDisabled, m_ClientRpcParams);
                }
                else // Preserving the ability for server authoritative mode to accept state changes from owner
                {
                    SetStateServerRpc(pos, rot, scale, !teleportDisabled);
                }
                return;
            }

            SetStateInternal(pos, rot, scale, !teleportDisabled);
        }

        /// <summary>
        /// Authoritative only method
        /// Sets the internal state (teleporting or just set state) of the authoritative
        /// transform directly.
        /// </summary>
        private void SetStateInternal(Vector3 pos, Quaternion rot, Vector3 scale, bool shouldTeleport)
        {
#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
            if (m_UseRigidbodyForMotion)
            {
                m_NetworkRigidbodyInternal.SetPosition(pos);
                m_NetworkRigidbodyInternal.SetRotation(rot);
            }
            else
#endif
            {
                if (InLocalSpace)
                {
                    transform.localPosition = pos;
                    transform.localRotation = rot;
                }
                else
                {
                    transform.SetPositionAndRotation(pos, rot);
                }
            }

            transform.localScale = scale;
            m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame = shouldTeleport;

            var transformToCommit = transform;

            // Explicit set states are cumulative during a fractional tick period of time (i.e. each SetState invocation will 
            // update the axial deltas to whatever changes are applied). As such, we need to preserve the dirty and explicit
            // state flags.
            var stateWasDirty = m_LocalAuthoritativeNetworkState.IsDirty;
            var explicitState = m_LocalAuthoritativeNetworkState.ExplicitSet;

            // Apply any delta states to the m_LocalAuthoritativeNetworkState
            var isDirty = CheckForStateChange(ref m_LocalAuthoritativeNetworkState, ref transformToCommit);

            // If we were dirty and the explicit state was set (prior to checking for deltas) or the current explicit state is dirty,
            // then we set the explicit state flag.
            m_LocalAuthoritativeNetworkState.ExplicitSet = (stateWasDirty && explicitState) || isDirty;

            // If the current explicit set flag is set, then we are dirty. This assures if more than one explicit set state is invoked
            // in between a fractional tick period and the current explicit set state did not find any deltas that we preserve any
            // previous dirty state.
            m_LocalAuthoritativeNetworkState.IsDirty = m_LocalAuthoritativeNetworkState.ExplicitSet;
        }

        /// <summary>
        /// Invoked by <see cref="SetState"/>, allows a non-owner server to update the transform state
        /// </summary>
        /// <remarks>
        /// Continued support for client-driven server authority model
        /// </remarks>
        [ClientRpc]
        private void SetStateClientRpc(Vector3 pos, Quaternion rot, Vector3 scale, bool shouldTeleport, ClientRpcParams clientRpcParams = default)
        {
            // Server dictated state is always applied
            SetStateInternal(pos, rot, scale, shouldTeleport);
        }

        /// <summary>
        /// Invoked by <see cref="SetState"/>, allows an owner-client update the transform state
        /// </summary>
        /// <remarks>
        /// Continued support for client-driven server authority model
        /// </remarks>
        [ServerRpc]
        private void SetStateServerRpc(Vector3 pos, Quaternion rot, Vector3 scale, bool shouldTeleport)
        {
            // server has received this RPC request to move change transform. give the server a chance to modify or even reject the move
            if (OnClientRequestChange != null)
            {
                (pos, rot, scale) = OnClientRequestChange(pos, rot, scale);
            }
            SetStateInternal(pos, rot, scale, shouldTeleport);
        }

        /// <summary>
        /// Teleport an already spawned object to the given values without interpolating.
        /// </summary>
        /// <remarks>
        /// This is intended to be used on already spawned objects, for setting the position of a dynamically spawned object just apply the transform values prior to spawning. <br />
        /// With player objects, override the <see cref="OnNetworkSpawn"/> method and have the authority make adjustments to the transform prior to invoking base.OnNetworkSpawn.
        /// </remarks>
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

            // Teleporting now is as simple as setting the internal state and passing the teleport flag
            SetStateInternal(newPosition, newRotation, newScale, true);
        }
        #endregion

        #region UPDATES AND AUTHORITY CHECKS
        private NetworkTransformTickRegistration m_NetworkTransformTickRegistration;
        private void UpdateInterpolation()
        {
            // Non-Authority
            if (Interpolate)
            {
                AdjustForChangeInTransformSpace();

                var serverTime = m_CachedNetworkManager.ServerTime;
                var cachedServerTime = serverTime.Time;
                //var offset = (float)serverTime.TickOffset;
#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
                var cachedDeltaTime = m_UseRigidbodyForMotion ? m_CachedNetworkManager.RealTimeProvider.FixedDeltaTime : m_CachedNetworkManager.RealTimeProvider.DeltaTime;
#else
                var cachedDeltaTime = m_CachedNetworkManager.RealTimeProvider.DeltaTime;
#endif
                // With owner authoritative mode, non-authority clients can lag behind
                // by more than 1 tick period of time. The current "solution" for now
                // is to make their cachedRenderTime run 2 ticks behind.

                // TODO: This could most likely just always be 2
                //var ticksAgo = ((!IsServerAuthoritative() && !IsServer) || m_CachedNetworkManager.DistributedAuthorityMode) && !m_CachedNetworkManager.DAHost ? 2 : 1;
                var ticksAgo = 2;

                var cachedRenderTime = serverTime.TimeTicksAgo(ticksAgo).Time;

                // Now only update the interpolators for the portions of the transform being synchronized
                if (SynchronizePosition)
                {
                    m_PositionInterpolator.Update(cachedDeltaTime, cachedRenderTime, cachedServerTime);
                }

                if (SynchronizeRotation)
                {
                    // When using half precision Lerp towards the target rotation.
                    // When using full precision Slerp towards the target rotation.
                    /// <see cref="BufferedLinearInterpolatorQuaternion.IsSlerp"/>
                    m_RotationInterpolator.IsSlerp = !UseHalfFloatPrecision;
                    m_RotationInterpolator.Update(cachedDeltaTime, cachedRenderTime, cachedServerTime);
                }

                if (SynchronizeScale)
                {
                    m_ScaleInterpolator.Update(cachedDeltaTime, cachedRenderTime, cachedServerTime);
                }
            }
        }

        /// <inheritdoc/>
        /// <remarks>
        /// If you override this method, be sure that:
        /// - Non-authority always invokes this base class method.
        /// </remarks>
        public virtual void OnUpdate()
        {
            // If not spawned or this instance has authority, exit early
#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D
            if (!IsSpawned || CanCommitToTransform || m_UseRigidbodyForMotion)
#else
            if (!IsSpawned || CanCommitToTransform)
#endif
            {
                return;
            }

            // Update interpolation
            UpdateInterpolation();

            // Apply the current authoritative state
            ApplyAuthoritativeState();
        }

#if COM_UNITY_MODULES_PHYSICS || COM_UNITY_MODULES_PHYSICS2D

        /// <summary>
        /// When paired with a NetworkRigidbody and NetworkRigidbody.UseRigidBodyForMotion is enabled,
        /// this will be invoked during <see cref="NetworkRigidbody.FixedUpdate"/>.
        /// </summary>
        public virtual void OnFixedUpdate()
        {
            // If not spawned or this instance has authority, exit early
            if (!m_UseRigidbodyForMotion || !IsSpawned || CanCommitToTransform)
            {
                return;
            }

            m_NetworkRigidbodyInternal.WakeIfSleeping();

            // Update interpolation
            UpdateInterpolation();

            // Apply the current authoritative state
            ApplyAuthoritativeState();
        }
#endif

        /// <summary>
        /// Determines whether the <see cref="NetworkTransform"/> is <see cref="AuthorityModes.Server"/> or <see cref="AuthorityModes.Owner"/> based on the <see cref="AuthorityMode"/> property.
        /// You can override this method to control this logic. 
        /// </summary>
        /// <returns><see cref="true"/> or <see cref="false"/></returns>
        protected virtual bool OnIsServerAuthoritative()
        {
            return AuthorityMode == AuthorityModes.Server;
        }

        /// <summary>
        /// Method to determine if this <see cref="NetworkTransform"/> instance is owner or server authoritative.
        /// </summary>
        /// <remarks>
        /// When using a <see cref="NetworkTopologyTypes.DistributedAuthority"/> <see cref="NetworkConfig.NetworkTopology"/>, this will always be viewed as a <see cref="AuthorityModes.Owner"/> authoritative motion model.
        /// </remarks>
        /// <returns><see cref="true"/> or <see cref="false"/></returns>
        public bool IsServerAuthoritative()
        {
            if (m_CachedNetworkManager && m_CachedNetworkManager.DistributedAuthorityMode)
            {
                return false;
            }
            else
            {
                return OnIsServerAuthoritative();
            }
        }

        #endregion

        #region MESSAGE HANDLING

        internal NetworkTransformState InboundState = new NetworkTransformState();
        internal NetworkTransformState OutboundState
        {
            get
            {
                return m_LocalAuthoritativeNetworkState;
            }
        }

        /// <summary>
        /// Invoked by <see cref="NetworkTransformMessage"/> to update the transform state
        /// </summary>
        /// <param name="networkTransformState"></param>
        internal void TransformStateUpdate(ulong senderId)
        {
            if (CanCommitToTransform)
            {
                // TODO: Investigate where this state should be applied or just discarded.
                // For now, discard the state if we assumed ownership.
                return;
            }
            // Store the previous/old state
            m_OldState = m_LocalAuthoritativeNetworkState;

            // Assign the new incoming state
            m_LocalAuthoritativeNetworkState = InboundState;

            // Apply the state update
            OnNetworkStateChanged(m_OldState, m_LocalAuthoritativeNetworkState);
        }

        // Used to send outbound messages
        private NetworkTransformMessage m_OutboundMessage = new NetworkTransformMessage();


        internal int SerializeMessage(FastBufferWriter writer, int targetVersion)
        {
            var networkObject = NetworkObject;
            var position = writer.Position;
            BytePacker.WriteValueBitPacked(writer, NetworkObjectId);
            BytePacker.WriteValueBitPacked(writer, (int)NetworkBehaviourId);
            writer.WriteNetworkSerializable(m_LocalAuthoritativeNetworkState);
            if (m_CachedNetworkManager.DistributedAuthorityMode)
            {
                BytePacker.WriteValuePacked(writer, networkObject.Observers.Count - 1);

                foreach (var targetId in networkObject.Observers)
                {
                    if (OwnerClientId == targetId)
                    {
                        continue;
                    }
                    BytePacker.WriteValuePacked(writer, targetId);
                }
            }
            return writer.Position - position;
        }

        /// <summary>
        /// Invoked by the authoritative instance to sends a <see cref="NetworkTransformMessage"/> containing the <see cref="NetworkTransformState"/>
        /// </summary>
        private void UpdateTransformState()
        {
            if (m_CachedNetworkManager.ShutdownInProgress || (m_CachedNetworkManager.DistributedAuthorityMode && !m_CachedNetworkManager.CMBServiceConnection && m_CachedNetworkObject.Observers.Count - 1 == 0))
            {
                return;
            }

            bool isServerAuthoritative = IsServerAuthoritative();
            if (isServerAuthoritative && !IsServer)
            {
                Debug.LogError($"Server authoritative {nameof(NetworkTransform)} can only be updated by the server!");
            }
            else if (!isServerAuthoritative && !IsServer && !IsOwner)
            {
                Debug.LogError($"Owner authoritative {nameof(NetworkTransform)} can only be updated by the owner!");
            }
            var customMessageManager = m_CachedNetworkManager.CustomMessagingManager;
            m_OutboundMessage.NetworkTransform = this;

            // Determine what network delivery method to use:
            // When to send reliable packets:
            // - If UsUnrealiable is not enabled
            // - If teleporting or synchronizing
            // - If sending an UnrealiableFrameSync or synchronizing the base position of the NetworkDeltaPosition
            var networkDelivery = !UseUnreliableDeltas | m_LocalAuthoritativeNetworkState.IsTeleportingNextFrame | m_LocalAuthoritativeNetworkState.IsSynchronizing
                | m_LocalAuthoritativeNetworkState.UnreliableFrameSync | m_LocalAuthoritativeNetworkState.SynchronizeBaseHalfFloat
                ? NetworkDelivery.ReliableSequenced : NetworkDelivery.UnreliableSequenced;

            // Server-host-dahost always sends updates to all clients (but itself)
            if (IsServer)
            {
                var clientCount = m_CachedNetworkManager.ConnectionManager.ConnectedClientsList.Count;
                for (int i = 0; i < clientCount; i++)
                {
                    var clientId = m_CachedNetworkManager.ConnectionManager.ConnectedClientsList[i].ClientId;
                    if (NetworkManager.ServerClientId == clientId)
                    {
                        continue;
                    }
                    if (!NetworkObject.Observers.Contains(clientId))
                    {
                        continue;
                    }
                    NetworkManager.MessageManager.SendMessage(ref m_OutboundMessage, networkDelivery, clientId);
                }
            }
            else
            {
                // Clients (owner authoritative) send messages to the server-host
                NetworkManager.MessageManager.SendMessage(ref m_OutboundMessage, networkDelivery, NetworkManager.ServerClientId);
            }
            m_LocalAuthoritativeNetworkState.LastSerializedSize = m_OutboundMessage.BytesWritten;
        }
        #endregion

        #region NETWORK TICK REGISTRATOIN AND HANDLING
        private static Dictionary<NetworkManager, NetworkTransformTickRegistration> s_NetworkTickRegistration = new Dictionary<NetworkManager, NetworkTransformTickRegistration>();

        internal static float GetTickLatency(NetworkManager networkManager)
        {
            if (s_NetworkTickRegistration.ContainsKey(networkManager))
            {
                return s_NetworkTickRegistration[networkManager].TicksAgo;
            }
            return 0f;
        }

        /// <summary>
        /// Returns the number of ticks (fractional) a client is latent relative
        /// to its current RTT.
        /// </summary>
        public static float GetTickLatency()
        {
            return GetTickLatency(NetworkManager.Singleton);
        }

        internal static float GetTickLatencyInSeconds(NetworkManager networkManager)
        {
            if (s_NetworkTickRegistration.ContainsKey(networkManager))
            {
                return s_NetworkTickRegistration[networkManager].TicksAgoInSeconds();
            }
            return 0f;
        }

        /// <summary>
        /// Returns the tick latency in seconds (typically fractional)
        /// </summary>
        public static float GetTickLatencyInSeconds()
        {
            return GetTickLatencyInSeconds(NetworkManager.Singleton);
        }

        private static void RemoveTickUpdate(NetworkManager networkManager)
        {
            s_NetworkTickRegistration.Remove(networkManager);
        }

        /// <summary>
        /// Having the tick update once and cycling through registered instances to update is evidently less processor
        /// intensive than having each instance subscribe and update individually.
        /// </summary>
        private class NetworkTransformTickRegistration
        {
            private Action m_NetworkTickUpdate;
            private NetworkManager m_NetworkManager;
            public HashSet<NetworkTransform> NetworkTransforms = new HashSet<NetworkTransform>();

            private int m_LastTick;
            private void OnNetworkManagerStopped(bool value)
            {
                Remove();
            }

            public void Remove()
            {
                m_NetworkManager.NetworkTickSystem.Tick -= m_NetworkTickUpdate;
                m_NetworkTickUpdate = null;
                NetworkTransforms.Clear();
                RemoveTickUpdate(m_NetworkManager);
            }

            internal float TicksAgoInSeconds()
            {
                return 2 * m_TickFrequency;
                // TODO: We need an RTT that updates regularly and not just when the client sends packets
                //return Mathf.Max(1.0f, TicksAgo) * m_TickFrequency;
            }

            /// <summary>
            /// Invoked once per network tick, this will update any registered
            /// authority instances.
            /// </summary>
            private void TickUpdate()
            {
                // TODO: We need an RTT that updates regularly and not just when the client sends packets
                //if (m_UnityTransport != null)
                //{
                //    // Determine the desired ticks ago by the RTT (this really should be the combination of the
                //    // authority and non-authority 1/2 RTT but in the end anything beyond 300ms is considered very poor
                //    // network quality so latent interpolation is going to be expected).
                //    var rtt = Mathf.Max(m_TickInMS, m_UnityTransport.GetCurrentRtt(NetworkManager.ServerClientId));
                //    m_TicksAgoSamples[m_TickSampleIndex] = Mathf.Max(1, (int)(rtt * m_TickFrequency));
                //    var tickAgoSum = 0.0f;
                //    foreach (var tickAgo in m_TicksAgoSamples)
                //    {
                //        tickAgoSum += tickAgo;
                //    }
                //    m_PreviousTicksAgo = TicksAgo;
                //    TicksAgo = Mathf.Lerp(m_PreviousTicksAgo, tickAgoSum / m_TickRate, m_TickFrequency);
                //    m_TickSampleIndex = (m_TickSampleIndex + 1) % m_TickRate;
                //    // Get the partial tick value for when this is all calculated to provide an offset for determining
                //    // the relative starting interpolation point for the next update
                //    Offset = m_OffsetTickFrequency * (Mathf.Max(2, TicksAgo) - (int)TicksAgo);
                //}

                // TODO FIX: The local NetworkTickSystem can invoke with the same network tick as before
                if (m_NetworkManager.ServerTime.Tick <= m_LastTick)
                {
                    return;
                }
                foreach (var networkTransform in NetworkTransforms)
                {
                    if (networkTransform.IsSpawned)
                    {
                        networkTransform.OnNetworkTick();
                    }
                }
                m_LastTick = m_NetworkManager.ServerTime.Tick;
            }


            private UnityTransport m_UnityTransport;
            private float m_TickFrequency;
            //private float m_OffsetTickFrequency;
            //private ulong m_TickInMS;
            //private int m_TickSampleIndex;
            private int m_TickRate;
            public float TicksAgo { get; private set; }
            //public float Offset { get; private set; }
            //private float m_PreviousTicksAgo;

            private List<float> m_TicksAgoSamples = new List<float>();

            public NetworkTransformTickRegistration(NetworkManager networkManager)
            {
                m_NetworkManager = networkManager;
                m_NetworkTickUpdate = new Action(TickUpdate);
                networkManager.NetworkTickSystem.Tick += m_NetworkTickUpdate;
                m_TickRate = (int)m_NetworkManager.NetworkConfig.TickRate;
                m_TickFrequency = 1.0f / m_TickRate;
                //// For the offset, it uses the fractional remainder of the tick to determine the offset.
                //// In order to keep within tick boundaries, we increment the tick rate by 1 to assure it
                //// will always be < the tick frequency.
                //m_OffsetTickFrequency = 1.0f / (m_TickRate + 1);
                //m_TickInMS = (ulong)(1000 * m_TickFrequency);
                //m_UnityTransport = m_NetworkManager.NetworkConfig.NetworkTransport as UnityTransport;
                //// Fill the sample with a starting value of 1
                //for (int i = 0; i < m_TickRate; i++)
                //{
                //    m_TicksAgoSamples.Add(1f);
                //}
                TicksAgo = 2f;
                //m_PreviousTicksAgo = 1f;
                if (networkManager.IsServer)
                {
                    networkManager.OnServerStopped += OnNetworkManagerStopped;
                }
                else
                {
                    networkManager.OnClientStopped += OnNetworkManagerStopped;
                }
            }
        }
        private static int s_TickSynchPosition;
        private int m_NextTickSync;

        internal void RegisterForTickSynchronization()
        {
            s_TickSynchPosition++;
            m_NextTickSync = NetworkManager.ServerTime.Tick + (s_TickSynchPosition % (int)NetworkManager.NetworkConfig.TickRate);
        }

        private static void RegisterNetworkManagerForTickUpdate(NetworkManager networkManager)
        {
            if (!s_NetworkTickRegistration.ContainsKey(networkManager))
            {
                s_NetworkTickRegistration.Add(networkManager, new NetworkTransformTickRegistration(networkManager));
            }
        }

        /// <summary>
        /// Will register the NetworkTransform instance for the single tick update entry point.
        /// If a NetworkTransformTickRegistration has not yet been registered for the NetworkManager
        /// instance, then create an entry.
        /// </summary>
        /// <param name="networkTransform"></param>
        private static void RegisterForTickUpdate(NetworkTransform networkTransform)
        {

            if (!networkTransform.NetworkManager.DistributedAuthorityMode && !s_NetworkTickRegistration.ContainsKey(networkTransform.NetworkManager))
            {
                s_NetworkTickRegistration.Add(networkTransform.NetworkManager, new NetworkTransformTickRegistration(networkTransform.NetworkManager));
            }

            networkTransform.RegisterForTickSynchronization();
            s_NetworkTickRegistration[networkTransform.NetworkManager].NetworkTransforms.Add(networkTransform);
        }

        /// <summary>
        /// If a NetworkTransformTickRegistration exists for the NetworkManager instance, then this will
        /// remove the NetworkTransform instance from the single tick update entry point.
        /// </summary>
        /// <param name="networkTransform"></param>
        private static void DeregisterForTickUpdate(NetworkTransform networkTransform)
        {
            if (networkTransform.NetworkManager == null)
            {
                return;
            }
            if (s_NetworkTickRegistration.ContainsKey(networkTransform.NetworkManager))
            {
                s_NetworkTickRegistration[networkTransform.NetworkManager].NetworkTransforms.Remove(networkTransform);
                if (!networkTransform.NetworkManager.DistributedAuthorityMode && s_NetworkTickRegistration[networkTransform.NetworkManager].NetworkTransforms.Count == 0)
                {
                    var registrationEntry = s_NetworkTickRegistration[networkTransform.NetworkManager];
                    registrationEntry.Remove();
                }
            }
        }
        #endregion
    }

    internal interface INetworkTransformLogStateEntry
    {
        void AddLogEntry(NetworkTransform.NetworkTransformState networkTransformState, ulong targetClient, bool preUpdate = false);
    }
}
