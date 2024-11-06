using System.Runtime.CompilerServices;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Netcode.Components
{
    /// <summary>
    /// Half Precision <see cref="Vector4"/> that can also be used to convert a <see cref="Quaternion"/> to half precision.
    /// </summary>
    /// <remarks>
    /// The Vector4T&lt;ushort&gt; values are half float values returned by <see cref="Mathf.FloatToHalf(float)"/> for each
    /// individual axis and the 16 bits of the half float are stored as <see cref="ushort"/> values since C# does not have
    /// a half float type.
    /// </remarks>
    public struct HalfVector4 : INetworkSerializable
    {
        internal const int Length = 4;
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
        /// The half float precision value of the w-axis as a <see cref="half"/>.
        /// </summary>
        public half W => Axis.w;

        /// <summary>
        /// Used to store the half float precision values as a <see cref="half4"/>
        /// </summary>
        public half4 Axis;

        private void SerializeWrite(FastBufferWriter writer)
        {
            for (int i = 0; i < Length; i++)
            {
                writer.WriteUnmanagedSafe(Axis[i]);
            }
        }

        private void SerializeRead(FastBufferReader reader)
        {
            for (int i = 0; i < Length; i++)
            {
                var axisValue = Axis[i];
                reader.ReadUnmanagedSafe(out axisValue);
                Axis[i] = axisValue;
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
        /// Converts this instance to a full precision <see cref="Vector4"/>.
        /// </summary>
        /// <returns>A <see cref="Vector4"/> as the full precision value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Vector4 ToVector4()
        {
            return math.float4(Axis);
        }

        /// <summary>
        /// Converts this instance to a full precision <see cref="Quaternion"/>.
        /// </summary>
        /// <returns>A <see cref="Quaternion"/> as the full precision value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public Quaternion ToQuaternion()
        {
            return math.quaternion(Axis);
        }

        /// <summary>
        /// Converts a full precision <see cref="Vector4"/> to half precision and updates the current instance.
        /// </summary>
        /// <param name="vector4">The <see cref="Vector4"/> to convert and update this instance with.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateFrom(ref Vector4 vector4)
        {
            Axis = math.half4(vector4);
        }

        /// <summary>
        /// Converts a full precision <see cref="Vector4"/> to half precision and updates the current instance.
        /// </summary>
        /// <param name="quaternion">The <see cref="Quaternion"/> to convert and update this instance with.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void UpdateFrom(ref Quaternion quaternion)
        {
            Axis = math.half4(math.half(quaternion.x), math.half(quaternion.y), math.half(quaternion.z), math.half(quaternion.w));
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="vector4">The initial axial values (converted to half floats) when instantiated.</param>
        public HalfVector4(Vector4 vector4)
        {
            Axis = default;
            UpdateFrom(ref vector4);
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="x">The initial x axis (converted to half float) value when instantiated.</param>
        /// <param name="y">The initial y axis (converted to half float) value when instantiated.</param>
        /// <param name="z">The initial z axis (converted to half float) value when instantiated.</param>
        /// <param name="w">The initial w axis (converted to half float) value when instantiated.</param>
        public HalfVector4(float x, float y, float z, float w) : this(new Vector4(x, y, z, w))
        {
        }
    }
}
