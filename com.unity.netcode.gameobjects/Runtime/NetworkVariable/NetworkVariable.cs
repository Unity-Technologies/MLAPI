using System.Collections.Generic;
using UnityEngine;
using System.IO;
using System;

namespace Unity.Netcode
{
    /// <summary>
    /// A variable that can be synchronized over the network.
    /// </summary>
    [Serializable]
    public class NetworkVariable<T> : NetworkVariableBase where T : unmanaged
    {
        /// <summary>
        /// Delegate type for value changed event
        /// </summary>
        /// <param name="previousValue">The value before the change</param>
        /// <param name="newValue">The new value</param>
        public delegate void OnValueChangedDelegate(T previousValue, T newValue);
        /// <summary>
        /// The callback to be invoked when the value gets changed
        /// </summary>
        public OnValueChangedDelegate OnValueChanged;

        /// <summary>
        /// Creates a NetworkVariable with the default value and settings
        /// </summary>
        public NetworkVariable() { }

        /// <summary>
        /// Creates a NetworkVariable with the default value and custom settings
        /// </summary>
        /// <param name="settings">The settings to use for the NetworkVariable</param>
        public NetworkVariable(NetworkVariableSettings settings) : base(settings) { }

        /// <summary>
        /// Creates a NetworkVariable with a custom value and custom settings
        /// </summary>
        /// <param name="settings">The settings to use for the NetworkVariable</param>
        /// <param name="value">The initial value to use for the NetworkVariable</param>
        public NetworkVariable(NetworkVariableSettings settings, T value) : base(settings)
        {
            m_InternalValue = value;
        }

        /// <summary>
        /// Creates a NetworkVariable with a custom value and the default settings
        /// </summary>
        /// <param name="value">The initial value to use for the NetworkVariable</param>
        public NetworkVariable(T value)
        {
            m_InternalValue = value;
        }

        [SerializeField]
        private protected T m_InternalValue;

        /// <summary>
        /// The temporary accessor to enable struct element access until [MTT-1020] complete
        /// </summary>
        public ref T ValueRef
        {
            get => ref m_InternalValue;
        }

        /// <summary>
        /// The value of the NetworkVariable container
        /// </summary>
        public virtual T Value
        {
            get => m_InternalValue;
            set
            {
                // this could be improved. The Networking Manager is not always initialized here
                //  Good place to decouple network manager from the network variable

                // Also, note this is not really very water-tight, if you are running as a host
                //  we cannot tell if a NetworkVariable write is happening inside client-ish code
                if (NetworkBehaviour && (NetworkBehaviour.NetworkManager.IsClient && !NetworkBehaviour.NetworkManager.IsHost))
                {
                    throw new InvalidOperationException("Client can't write to NetworkVariables");
                }
                Set(value);
            }
        }

        private protected void Set(T value)
        {
            if (EqualityComparer<T>.Default.Equals(m_InternalValue, value))
            {
                return;
            }

            m_IsDirty = true;
            T previousValue = m_InternalValue;
            m_InternalValue = value;
            OnValueChanged?.Invoke(previousValue, m_InternalValue);
        }

        /// <summary>
        /// Writes the variable to the writer
        /// </summary>
        /// <param name="stream">The stream to write the value to</param>
        public override void WriteDelta(Stream stream)
        {
            WriteField(stream);
        }

        /// <summary>
        /// Reads value from the reader and applies it
        /// </summary>
        /// <param name="stream">The stream to read the value from</param>
        /// <param name="keepDirtyDelta">Whether or not the container should keep the dirty delta, or mark the delta as consumed</param>
        public override void ReadDelta(Stream stream, bool keepDirtyDelta)
        {
            using (var reader = PooledNetworkReader.Get(stream))
            {
                T previousValue = m_InternalValue;
                m_InternalValue = (T)reader.ReadObjectPacked(typeof(T));

                if (keepDirtyDelta)
                {
                    m_IsDirty = true;
                }

                OnValueChanged?.Invoke(previousValue, m_InternalValue);
            }
        }

        /// <inheritdoc />
        public override void ReadField(Stream stream)
        {
            ReadDelta(stream, false);
        }

        /// <inheritdoc />
        public override void WriteField(Stream stream)
        {
            using (var writer = PooledNetworkWriter.Get(stream))
            {
                writer.WriteObjectPacked(m_InternalValue); //BOX
            }
        }
    }

    /// <summary>
    /// A NetworkVariable that holds bools and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableBool : NetworkVariable<bool>
    {
        /// <inheritdoc />
        public NetworkVariableBool() { }

        /// <inheritdoc />
        public NetworkVariableBool(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableBool(bool value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableBool(NetworkVariableSettings settings, bool value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds bytes and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableByte : NetworkVariable<byte>
    {
        /// <inheritdoc />
        public NetworkVariableByte() { }

        /// <inheritdoc />
        public NetworkVariableByte(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableByte(byte value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableByte(NetworkVariableSettings settings, byte value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds sbytes and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableSByte : NetworkVariable<sbyte>
    {
        /// <inheritdoc />
        public NetworkVariableSByte() { }

        /// <inheritdoc />
        public NetworkVariableSByte(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableSByte(sbyte value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableSByte(NetworkVariableSettings settings, sbyte value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds ushorts and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableUShort : NetworkVariable<ushort>
    {
        /// <inheritdoc />
        public NetworkVariableUShort() { }

        /// <inheritdoc />
        public NetworkVariableUShort(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableUShort(ushort value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableUShort(NetworkVariableSettings settings, ushort value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds shorts and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableShort : NetworkVariable<short>
    {
        /// <inheritdoc />
        public NetworkVariableShort() { }

        /// <inheritdoc />
        public NetworkVariableShort(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableShort(short value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableShort(NetworkVariableSettings settings, short value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds uints and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableUInt : NetworkVariable<uint>
    {
        /// <inheritdoc />
        public NetworkVariableUInt() { }

        /// <inheritdoc />
        public NetworkVariableUInt(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableUInt(uint value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableUInt(NetworkVariableSettings settings, uint value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds ints and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableInt : NetworkVariable<int>
    {
        /// <inheritdoc />
        public NetworkVariableInt() { }

        /// <inheritdoc />
        public NetworkVariableInt(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableInt(int value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableInt(NetworkVariableSettings settings, int value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds ulongs and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableULong : NetworkVariable<ulong>
    {
        /// <inheritdoc />
        public NetworkVariableULong() { }

        /// <inheritdoc />
        public NetworkVariableULong(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableULong(ulong value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableULong(NetworkVariableSettings settings, ulong value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds longs and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableLong : NetworkVariable<long>
    {
        /// <inheritdoc />
        public NetworkVariableLong() { }

        /// <inheritdoc />
        public NetworkVariableLong(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableLong(long value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableLong(NetworkVariableSettings settings, long value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds floats and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableFloat : NetworkVariable<float>
    {
        /// <inheritdoc />
        public NetworkVariableFloat() { }

        /// <inheritdoc />
        public NetworkVariableFloat(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableFloat(float value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableFloat(NetworkVariableSettings settings, float value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds doubles and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableDouble : NetworkVariable<double>
    {
        /// <inheritdoc />
        public NetworkVariableDouble() { }

        /// <inheritdoc />
        public NetworkVariableDouble(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableDouble(double value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableDouble(NetworkVariableSettings settings, double value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds vector2s and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableVector2 : NetworkVariable<Vector2>
    {
        /// <inheritdoc />
        public NetworkVariableVector2() { }

        /// <inheritdoc />
        public NetworkVariableVector2(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableVector2(Vector2 value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableVector2(NetworkVariableSettings settings, Vector2 value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds vector3s and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableVector3 : NetworkVariable<Vector3>
    {
        /// <inheritdoc />
        public NetworkVariableVector3() { }

        /// <inheritdoc />
        public NetworkVariableVector3(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableVector3(Vector3 value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableVector3(NetworkVariableSettings settings, Vector3 value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds vector4s and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableVector4 : NetworkVariable<Vector4>
    {
        /// <inheritdoc />
        public NetworkVariableVector4() { }

        /// <inheritdoc />
        public NetworkVariableVector4(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableVector4(Vector4 value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableVector4(NetworkVariableSettings settings, Vector4 value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds colors and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableColor : NetworkVariable<Color>
    {
        /// <inheritdoc />
        public NetworkVariableColor() { }

        /// <inheritdoc />
        public NetworkVariableColor(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableColor(Color value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableColor(NetworkVariableSettings settings, Color value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds color32s and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableColor32 : NetworkVariable<Color32>
    {
        /// <inheritdoc />
        public NetworkVariableColor32() { }

        /// <inheritdoc />
        public NetworkVariableColor32(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableColor32(Color32 value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableColor32(NetworkVariableSettings settings, Color32 value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds rays and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableRay : NetworkVariable<Ray>
    {
        /// <inheritdoc />
        public NetworkVariableRay() { }

        /// <inheritdoc />
        public NetworkVariableRay(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableRay(Ray value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableRay(NetworkVariableSettings settings, Ray value) : base(settings, value) { }
    }

    /// <summary>
    /// A NetworkVariable that holds quaternions and support serialization
    /// </summary>
    [Serializable]
    public class NetworkVariableQuaternion : NetworkVariable<Quaternion>
    {
        /// <inheritdoc />
        public NetworkVariableQuaternion() { }

        /// <inheritdoc />
        public NetworkVariableQuaternion(NetworkVariableSettings settings) : base(settings) { }

        /// <inheritdoc />
        public NetworkVariableQuaternion(Quaternion value) : base(value) { }

        /// <inheritdoc />
        public NetworkVariableQuaternion(NetworkVariableSettings settings, Quaternion value) : base(settings, value) { }
    }
}
