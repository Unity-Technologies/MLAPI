using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Netcode.Components
{
    /// <summary>
    /// Half float precision <see cref="Vector3"/>.
    /// </summary>
    /// <remarks>
    /// The Vector3T&lt;ushort&gt; values are half float values returned by <see cref="Mathf.FloatToHalf(float)"/> for each
    /// individual axis and the 16 bits of the half float are stored as <see cref="ushort"/> values since C# does not have
    /// a half float type.
    /// </remarks>
    public struct HalfVector3 : INetworkSerializable
    {
        internal const int Length = 3;

        /// <summary>
        /// The half float precision value of the x-axis as a <see cref="half"/>.
        /// </summary>
        public half X => Axis.x;
        /// <summary>
        /// The half float precision value of the y-axis as a <see cref="half"/>.
        /// </summary>
        public half Y => Axis.y;
        /// <summary>
        /// The half float precision value of the z-axis as a <see cref="half"/>.
        /// </summary>
        public half Z => Axis.z;

        /// <summary>
        /// Used to store the half float precision values as a <see cref="half3"/>
        /// </summary>
        public half3 Axis;

        /// <summary>
        /// Determine which axis will be synchronized during serialization
        /// </summary>
        public bool3 AxisToSynchronize;

        /// <summary>
        /// Directly sets each axial value to the passed in full precision values
        /// that are converted to half precision
        /// </summary>
        internal void Set(float x, float y, float z)
        {
            Axis.x = math.half(x);
            Axis.y = math.half(y);
            Axis.z = math.half(z);
        }

        private void SerializeWrite(FastBufferWriter writer)
        {
            for (int i = 0; i < Length; i++)
            {
                if (AxisToSynchronize[i])
                {
                    writer.WriteUnmanagedSafe(Axis[i]);
                }
            }
        }

        private void SerializeRead(FastBufferReader reader)
        {
            for (int i = 0; i < Length; i++)
            {
                if (AxisToSynchronize[i])
                {
                    var axisValue = Axis[i];
                    reader.ReadUnmanagedSafe(out axisValue);
                    Axis[i] = axisValue;
                }
            }
        }

        /// <summary>
        /// The serialization implementation of <see cref="INetworkSerializable"/>.
        /// </summary>
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            if (serializer.IsReader)
            {
                SerializeRead(serializer.GetFastBufferReader());
            }
            else
            {
                SerializeWrite(serializer.GetFastBufferWriter());
            }
        }

        /// <summary>
        /// Gets the full precision value as a <see cref="Vector3"/>.
        /// </summary>
        /// <returns>a <see cref="Vector3"/> as the full precision value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector3 ToVector3()
        {
            Vector3 fullPrecision = Vector3.zero;
            Vector3 fullConversion = math.float3(Axis);
            for (int i = 0; i < Length; i++)
            {
                if (AxisToSynchronize[i])
                {
                    fullPrecision[i] = fullConversion[i];
                }
            }
            return fullPrecision;
        }

        /// <summary>
        /// Converts a full precision <see cref="Vector3"/> to half precision and updates the current instance.
        /// </summary>
        /// <param name="vector3">The <see cref="Vector3"/> to convert.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateFrom(ref Vector3 vector3)
        {
            var half3Full = math.half3(vector3);
            for (int i = 0; i < Length; i++)
            {
                if (AxisToSynchronize[i])
                {
                    Axis[i] = half3Full[i];
                }
            }
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="vector3">The initial axial values (converted to half floats) when instantiated.</param>
        /// <param name="vector3AxisToSynchronize">The axis to synchronize.</param>
        public HalfVector3(Vector3 vector3, bool3 axisToSynchronize)
        {
            Axis = half3.zero;
            AxisToSynchronize = axisToSynchronize;
            UpdateFrom(ref vector3);
        }

        /// <summary>
        /// Constructor that defaults to all axis being synchronized.
        /// </summary>
        /// <param name="vector3">The initial axial values (converted to half floats) when instantiated.</param>
        public HalfVector3(Vector3 vector3) : this(vector3, math.bool3(true))
        {

        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="x">The initial x axis (converted to half float) value when instantiated.</param>
        /// <param name="y">The initial y axis (converted to half float) value when instantiated.</param>
        /// <param name="z">The initial z axis (converted to half float) value when instantiated.</param>
        /// <param name="axisToSynchronize">The axis to synchronize.</param>
        public HalfVector3(float x, float y, float z, bool3 axisToSynchronize) : this(new Vector3(x, y, z), axisToSynchronize)
        {
        }

        /// <summary>
        /// Constructor that defaults to all axis being synchronized.
        /// </summary>
        /// <param name="x">The initial x axis (converted to half float) value when instantiated.</param>
        /// <param name="y">The initial y axis (converted to half float) value when instantiated.</param>
        /// <param name="z">The initial z axis (converted to half float) value when instantiated.</param>
        public HalfVector3(float x, float y, float z) : this(new Vector3(x, y, z), math.bool3(true))
        {
        }
    }
}
