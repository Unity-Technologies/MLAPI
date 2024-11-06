using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Netcode.Components
{
    /// <summary>
    /// Used to synchromnize delta position when half float precision is enabled
    /// </summary>
    public struct NetworkDeltaPosition : INetworkSerializable
    {
        internal const float MaxDeltaBeforeAdjustment = 64f;

        /// <summary>
        /// The HalfVector3 used to synchronize the delta in position
        /// </summary>
        public HalfVector3 HalfVector3;

        internal Vector3 CurrentBasePosition;
        internal Vector3 PrecisionLossDelta;
        internal Vector3 HalfDeltaConvertedBack;
        internal Vector3 PreviousPosition;
        internal Vector3 DeltaPosition;
        internal int NetworkTick;

        internal bool SynchronizeBase;

        internal bool CollapsedDeltaIntoBase;

        /// <summary>
        /// The serialization implementation of <see cref="INetworkSerializable"/>
        /// </summary>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (!SynchronizeBase)
            {
                HalfVector3.NetworkSerialize(serializer);
            }
            else
            {
                serializer.SerializeValue(ref DeltaPosition);
                serializer.SerializeValue(ref CurrentBasePosition);
            }
        }

        /// <summary>
        /// Gets the full precision value of Vector3 position while also potentially updating the current base position.
        /// </summary>
        /// <param name="networkTick">Use the current network tick value.</param>
        /// <returns>The full position as a <see cref="Vector3"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ToVector3(int networkTick)
        {
            // When synchronizing, it is possible to have a state update arrive
            // for the same synchronization network tick.  Under this scenario,
            // we only want to return the existing CurrentBasePosition + DeltaPosition
            // values and not process the X, Y, or Z values.
            // (See the constructors below)
            if (networkTick == NetworkTick)
            {
                return CurrentBasePosition + DeltaPosition;
            }
            for (int i = 0; i < HalfVector3.Length; i++)
            {
                if (HalfVector3.AxisToSynchronize[i])
                {
                    DeltaPosition[i] = Mathf.HalfToFloat(HalfVector3.Axis[i].value);
                    // If we exceed or are equal to the maximum delta value then we need to
                    // apply the delta to the CurrentBasePosition value and reset the delta
                    // position for the axis.
                    if (Mathf.Abs(DeltaPosition[i]) >= MaxDeltaBeforeAdjustment)
                    {
                        CurrentBasePosition[i] += DeltaPosition[i];
                        DeltaPosition[i] = 0.0f;
                        HalfVector3.Axis[i] = half.zero;
                    }
                }
            }
            return CurrentBasePosition + DeltaPosition;
        }

        /// <summary>
        /// Returns the current base position (excluding the delta position offset).
        /// </summary>
        /// <returns>The current base position as a <see cref="Vector3"/></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetCurrentBasePosition()
        {
            return CurrentBasePosition;
        }

        /// <summary>
        /// Returns the full position which includes the delta offset position.
        /// </summary>
        /// <returns>The full position as a <see cref="Vector3"/>.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetFullPosition()
        {
            return CurrentBasePosition + DeltaPosition;
        }

        /// <summary>
        /// The half float vector3 version of the current delta position.
        /// </summary>
        /// <remarks>
        /// Only applies to the authoritative side for <see cref="NetworkTransform"/> instances.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetConvertedDelta()
        {
            return HalfDeltaConvertedBack;
        }

        /// <summary>
        /// The full precision current delta position.
        /// </summary>
        /// <remarks>
        /// Authoritative: Will have no precision loss
        /// Non-Authoritative: Has the current network tick's loss of precision.
        /// Precision loss adjustments are one network tick behind on the
        /// non-authoritative side.
        /// </remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 GetDeltaPosition()
        {
            return DeltaPosition;
        }

        /// <summary>
        /// Updates the position delta based off of the current base position.
        /// </summary>
        /// <param name="vector3">The full precision <see cref="Vector3"/> value to (converted to half floats) used to determine the delta offset positon.</param>
        /// <param name="networkTick">Set the current network tick value when updating.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateFrom(ref Vector3 vector3, int networkTick)
        {
            CollapsedDeltaIntoBase = false;
            NetworkTick = networkTick;
            DeltaPosition = (vector3 + PrecisionLossDelta) - CurrentBasePosition;
            for (int i = 0; i < HalfVector3.Length; i++)
            {
                if (HalfVector3.AxisToSynchronize[i])
                {
                    HalfVector3.Axis[i] = math.half(DeltaPosition[i]);
                    HalfDeltaConvertedBack[i] = Mathf.HalfToFloat(HalfVector3.Axis[i].value);
                    PrecisionLossDelta[i] = DeltaPosition[i] - HalfDeltaConvertedBack[i];
                    if (Mathf.Abs(HalfDeltaConvertedBack[i]) >= MaxDeltaBeforeAdjustment)
                    {
                        CurrentBasePosition[i] += HalfDeltaConvertedBack[i];
                        HalfDeltaConvertedBack[i] = 0.0f;
                        DeltaPosition[i] = 0.0f;
                        CollapsedDeltaIntoBase = true;
                    }
                }
            }

            for (int i = 0; i < HalfVector3.Length; i++)
            {
                if (HalfVector3.AxisToSynchronize[i])
                {
                    PreviousPosition[i] = vector3[i];
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="vector3">The initial axial values (converted to half floats) when instantiated.</param>
        /// <param name="networkTick">Set the network tick value to the current network tick when instantiating.</param>
        /// <param name="axisToSynchronize">The axis to be synchronized.</param>
        public NetworkDeltaPosition(Vector3 vector3, int networkTick, bool3 axisToSynchronize)
        {
            NetworkTick = networkTick;
            CurrentBasePosition = vector3;
            PreviousPosition = vector3;
            PrecisionLossDelta = Vector3.zero;
            DeltaPosition = Vector3.zero;
            HalfDeltaConvertedBack = Vector3.zero;
            HalfVector3 = new HalfVector3(vector3, axisToSynchronize);
            SynchronizeBase = false;
            CollapsedDeltaIntoBase = false;
            UpdateFrom(ref vector3, networkTick);
        }

        /// <summary>
        /// Constructor that defaults to all axis being synchronized.
        /// </summary>
        /// <param name="vector3">The initial axial values (converted to half floats) when instantiated.</param>
        /// <param name="networkTick">Set the network tick value to the current network tick when instantiating.</param>
        public NetworkDeltaPosition(Vector3 vector3, int networkTick) : this(vector3, networkTick, math.bool3(true))
        {

        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="x">The initial x axis (converted to half float) value when instantiated.</param>
        /// <param name="y">The initial y axis (converted to half float) value when instantiated.</param>
        /// <param name="z">The initial z axis (converted to half float) value when instantiated.</param>
        /// <param name="networkTick">Set the network tick value to the current network tick when instantiating.</param>
        /// <param name="axisToSynchronize">The axis to be synchronized.</param>
        public NetworkDeltaPosition(float x, float y, float z, int networkTick, bool3 axisToSynchronize) :
            this(new Vector3(x, y, z), networkTick, axisToSynchronize)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="x">The initial x axis (converted to half float) value when instantiated.</param>
        /// <param name="y">The initial y axis (converted to half float) value when instantiated.</param>
        /// <param name="z">The initial z axis (converted to half float) value when instantiated.</param>
        /// <param name="networkTick">Set the network tick value to the current network tick when instantiating.</param>
        public NetworkDeltaPosition(float x, float y, float z, int networkTick) :
            this(new Vector3(x, y, z), networkTick, math.bool3(true))
        {
        }
    }
}
