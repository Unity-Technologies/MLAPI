using System;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;

namespace Unity.Netcode.RuntimeTests
{
    internal class EmbeddedManagedNetworkSerializableType : INetworkSerializable
    {
        public int Int;
        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Int);
        }
    }
    internal class ManagedNetworkSerializableType : INetworkSerializable, IEquatable<ManagedNetworkSerializableType>
    {
        public string Str = "";
        public int[] Ints = Array.Empty<int>();
        public EmbeddedManagedNetworkSerializableType Embedded = new EmbeddedManagedNetworkSerializableType();
        public int InMemoryValue;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Str, true);
            var length = Ints.Length;
            serializer.SerializeValue(ref length);
            if (serializer.IsReader)
            {
                Ints = new int[length];
            }

            for (var i = 0; i < length; ++i)
            {
                var val = Ints[i];
                serializer.SerializeValue(ref val);
                Ints[i] = val;
            }

            serializer.SerializeValue(ref Embedded);
        }

        public bool Equals(ManagedNetworkSerializableType other)
        {
            if (ReferenceEquals(null, other))
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (Str != other.Str)
            {
                return false;
            }

            if (Ints.Length != other.Ints.Length)
            {
                return false;
            }

            for (var i = 0; i < Ints.Length; ++i)
            {
                if (Ints[i] != other.Ints[i])
                {
                    return false;
                }
            }

            if (Embedded.Int != other.Embedded.Int)
            {
                return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }

            if (ReferenceEquals(this, obj))
            {
                return true;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((ManagedNetworkSerializableType)obj);
        }

        public override int GetHashCode()
        {
            return 0;
        }
    }
    internal struct UnmanagedNetworkSerializableType : INetworkSerializable, IEquatable<UnmanagedNetworkSerializableType>
    {
        public FixedString32Bytes Str;
        public int Int;
        public int InMemoryValue;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            serializer.SerializeValue(ref Str);
            serializer.SerializeValue(ref Int);
        }

        public bool Equals(UnmanagedNetworkSerializableType other)
        {
            return Str.Equals(other.Str) && Int == other.Int;
        }

        public override bool Equals(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            if (obj.GetType() != GetType())
            {
                return false;
            }

            return Equals((ManagedNetworkSerializableType)obj);
        }

        public override int GetHashCode()
        {
            return Str.GetHashCode() ^ Int.GetHashCode() ^ InMemoryValue.GetHashCode();
        }
    }


    internal struct UnmanagedTemplateNetworkSerializableType<T> : INetworkSerializable where T : unmanaged, INetworkSerializable
    {
        public T Value;

        public void NetworkSerialize<TReaderWriterType>(BufferSerializer<TReaderWriterType> serializer) where TReaderWriterType : IReaderWriter
        {
            serializer.SerializeValue(ref Value);
        }
    }

    internal struct ManagedTemplateNetworkSerializableType<T> : INetworkSerializable where T : class, INetworkSerializable, new()
    {
        public T Value;

        public void NetworkSerialize<TReaderWriterType>(BufferSerializer<TReaderWriterType> serializer) where TReaderWriterType : IReaderWriter
        {
            bool isNull = Value == null;
            serializer.SerializeValue(ref isNull);
            if (!isNull)
            {
                if (Value == null)
                {
                    Value = new T();
                }
                serializer.SerializeValue(ref Value);
            }
        }
    }

    /// <summary>
    /// This provides coverage for all of the predefined NetworkVariable types
    /// The initial goal is for generalized full coverage of NetworkVariables:
    /// Covers all of the various constructor calls (i.e. various parameters or no parameters)
    /// Covers the local NetworkVariable's OnValueChanged functionality (i.e. when a specific type changes do we get a notification?)
    /// This was built as a NetworkBehaviour for further client-server unit testing patterns when this capability is available.
    /// </summary>
    internal class NetworkVariableTestComponent : NetworkBehaviour
    {
        private NetworkVariable<bool> m_NetworkVariableBool = new NetworkVariable<bool>();
        private NetworkVariable<byte> m_NetworkVariableByte = new NetworkVariable<byte>();
        private NetworkVariable<Color> m_NetworkVariableColor = new NetworkVariable<Color>();
        private NetworkVariable<Color32> m_NetworkVariableColor32 = new NetworkVariable<Color32>();
        private NetworkVariable<double> m_NetworkVariableDouble = new NetworkVariable<double>();
        private NetworkVariable<float> m_NetworkVariableFloat = new NetworkVariable<float>();
        private NetworkVariable<int> m_NetworkVariableInt = new NetworkVariable<int>();
        private NetworkVariable<long> m_NetworkVariableLong = new NetworkVariable<long>();
        private NetworkVariable<sbyte> m_NetworkVariableSByte = new NetworkVariable<sbyte>();
        private NetworkVariable<Quaternion> m_NetworkVariableQuaternion = new NetworkVariable<Quaternion>();
        private NetworkVariable<short> m_NetworkVariableShort = new NetworkVariable<short>();
        private NetworkVariable<Vector4> m_NetworkVariableVector4 = new NetworkVariable<Vector4>();
        private NetworkVariable<Vector3> m_NetworkVariableVector3 = new NetworkVariable<Vector3>();
        private NetworkVariable<Vector2> m_NetworkVariableVector2 = new NetworkVariable<Vector2>();
        private NetworkVariable<Ray> m_NetworkVariableRay = new NetworkVariable<Ray>();
        private NetworkVariable<ulong> m_NetworkVariableULong = new NetworkVariable<ulong>();
        private NetworkVariable<uint> m_NetworkVariableUInt = new NetworkVariable<uint>();
        private NetworkVariable<ushort> m_NetworkVariableUShort = new NetworkVariable<ushort>();
        private NetworkVariable<FixedString32Bytes> m_NetworkVariableFixedString32 = new NetworkVariable<FixedString32Bytes>();
        private NetworkVariable<FixedString64Bytes> m_NetworkVariableFixedString64 = new NetworkVariable<FixedString64Bytes>();
        private NetworkVariable<FixedString128Bytes> m_NetworkVariableFixedString128 = new NetworkVariable<FixedString128Bytes>();
        private NetworkVariable<FixedString512Bytes> m_NetworkVariableFixedString512 = new NetworkVariable<FixedString512Bytes>();
        private NetworkVariable<FixedString4096Bytes> m_NetworkVariableFixedString4096 = new NetworkVariable<FixedString4096Bytes>();
        private NetworkVariable<ManagedNetworkSerializableType> m_NetworkVariableManaged = new NetworkVariable<ManagedNetworkSerializableType>();


        public NetworkVariableHelper<bool> Bool_Var;
        public NetworkVariableHelper<byte> Byte_Var;
        public NetworkVariableHelper<Color> Color_Var;
        public NetworkVariableHelper<Color32> Color32_Var;
        public NetworkVariableHelper<double> Double_Var;
        public NetworkVariableHelper<float> Float_Var;
        public NetworkVariableHelper<int> Int_Var;
        public NetworkVariableHelper<long> Long_Var;
        public NetworkVariableHelper<sbyte> Sbyte_Var;
        public NetworkVariableHelper<Quaternion> Quaternion_Var;
        public NetworkVariableHelper<short> Short_Var;
        public NetworkVariableHelper<Vector4> Vector4_Var;
        public NetworkVariableHelper<Vector3> Vector3_Var;
        public NetworkVariableHelper<Vector2> Vector2_Var;
        public NetworkVariableHelper<Ray> Ray_Var;
        public NetworkVariableHelper<ulong> Ulong_Var;
        public NetworkVariableHelper<uint> Uint_Var;
        public NetworkVariableHelper<ushort> Ushort_Var;
        public NetworkVariableHelper<FixedString32Bytes> FixedString32_Var;
        public NetworkVariableHelper<FixedString64Bytes> FixedString64_Var;
        public NetworkVariableHelper<FixedString128Bytes> FixedString128_Var;
        public NetworkVariableHelper<FixedString512Bytes> FixedString512_Var;
        public NetworkVariableHelper<FixedString4096Bytes> FixedString4096_Var;
        public NetworkVariableHelper<ManagedNetworkSerializableType> Managed_Var;


        public bool EnableTesting;
        private bool m_FinishedTests;
        private bool m_ChangesAppliedToNetworkVariables;

        private float m_WaitForChangesTimeout;

        // Start is called before the first frame update
        private void InitializeTest()
        {
            // Generic Constructor Test Coverage
            m_NetworkVariableBool = new NetworkVariable<bool>();
            m_NetworkVariableByte = new NetworkVariable<byte>();
            m_NetworkVariableColor = new NetworkVariable<Color>();
            m_NetworkVariableColor32 = new NetworkVariable<Color32>();
            m_NetworkVariableDouble = new NetworkVariable<double>();
            m_NetworkVariableFloat = new NetworkVariable<float>();
            m_NetworkVariableInt = new NetworkVariable<int>();
            m_NetworkVariableLong = new NetworkVariable<long>();
            m_NetworkVariableSByte = new NetworkVariable<sbyte>();
            m_NetworkVariableQuaternion = new NetworkVariable<Quaternion>();
            m_NetworkVariableShort = new NetworkVariable<short>();
            m_NetworkVariableVector4 = new NetworkVariable<Vector4>();
            m_NetworkVariableVector3 = new NetworkVariable<Vector3>();
            m_NetworkVariableVector2 = new NetworkVariable<Vector2>();
            m_NetworkVariableRay = new NetworkVariable<Ray>();
            m_NetworkVariableULong = new NetworkVariable<ulong>();
            m_NetworkVariableUInt = new NetworkVariable<uint>();
            m_NetworkVariableUShort = new NetworkVariable<ushort>();
            m_NetworkVariableFixedString32 = new NetworkVariable<FixedString32Bytes>();
            m_NetworkVariableFixedString64 = new NetworkVariable<FixedString64Bytes>();
            m_NetworkVariableFixedString128 = new NetworkVariable<FixedString128Bytes>();
            m_NetworkVariableFixedString512 = new NetworkVariable<FixedString512Bytes>();
            m_NetworkVariableFixedString4096 = new NetworkVariable<FixedString4096Bytes>();
            m_NetworkVariableManaged = new NetworkVariable<ManagedNetworkSerializableType>();


            // NetworkVariable Value Type Constructor Test Coverage
            m_NetworkVariableBool = new NetworkVariable<bool>(true);
            m_NetworkVariableByte = new NetworkVariable<byte>(0);
            m_NetworkVariableColor = new NetworkVariable<Color>(new Color(1, 1, 1, 1));
            m_NetworkVariableColor32 = new NetworkVariable<Color32>(new Color32(1, 1, 1, 1));
            m_NetworkVariableDouble = new NetworkVariable<double>(1.0);
            m_NetworkVariableFloat = new NetworkVariable<float>(1.0f);
            m_NetworkVariableInt = new NetworkVariable<int>(1);
            m_NetworkVariableLong = new NetworkVariable<long>(1);
            m_NetworkVariableSByte = new NetworkVariable<sbyte>(0);
            m_NetworkVariableQuaternion = new NetworkVariable<Quaternion>(Quaternion.identity);
            m_NetworkVariableShort = new NetworkVariable<short>(256);
            m_NetworkVariableVector4 = new NetworkVariable<Vector4>(new Vector4(1, 1, 1, 1));
            m_NetworkVariableVector3 = new NetworkVariable<Vector3>(new Vector3(1, 1, 1));
            m_NetworkVariableVector2 = new NetworkVariable<Vector2>(new Vector2(1, 1));
            m_NetworkVariableRay = new NetworkVariable<Ray>(new Ray());
            m_NetworkVariableULong = new NetworkVariable<ulong>(1);
            m_NetworkVariableUInt = new NetworkVariable<uint>(1);
            m_NetworkVariableUShort = new NetworkVariable<ushort>(1);
            m_NetworkVariableFixedString32 = new NetworkVariable<FixedString32Bytes>("1234567890");
            m_NetworkVariableFixedString64 = new NetworkVariable<FixedString64Bytes>("1234567890");
            m_NetworkVariableFixedString128 = new NetworkVariable<FixedString128Bytes>("1234567890");
            m_NetworkVariableFixedString512 = new NetworkVariable<FixedString512Bytes>("1234567890");
            m_NetworkVariableFixedString4096 = new NetworkVariable<FixedString4096Bytes>("1234567890");
            m_NetworkVariableManaged = new NetworkVariable<ManagedNetworkSerializableType>(new ManagedNetworkSerializableType
            {
                Str = "1234567890",
                Ints = new[] { 1, 2, 3, 4, 5 },
                Embedded = new EmbeddedManagedNetworkSerializableType { Int = 6 }
            });

            // Use this nifty class: NetworkVariableHelper
            // Tracks if NetworkVariable changed invokes the OnValueChanged callback for the given instance type
            Bool_Var = new NetworkVariableHelper<bool>(m_NetworkVariableBool);
            Byte_Var = new NetworkVariableHelper<byte>(m_NetworkVariableByte);
            Color_Var = new NetworkVariableHelper<Color>(m_NetworkVariableColor);
            Color32_Var = new NetworkVariableHelper<Color32>(m_NetworkVariableColor32);
            Double_Var = new NetworkVariableHelper<double>(m_NetworkVariableDouble);
            Float_Var = new NetworkVariableHelper<float>(m_NetworkVariableFloat);
            Int_Var = new NetworkVariableHelper<int>(m_NetworkVariableInt);
            Long_Var = new NetworkVariableHelper<long>(m_NetworkVariableLong);
            Sbyte_Var = new NetworkVariableHelper<sbyte>(m_NetworkVariableSByte);
            Quaternion_Var = new NetworkVariableHelper<Quaternion>(m_NetworkVariableQuaternion);
            Short_Var = new NetworkVariableHelper<short>(m_NetworkVariableShort);
            Vector4_Var = new NetworkVariableHelper<Vector4>(m_NetworkVariableVector4);
            Vector3_Var = new NetworkVariableHelper<Vector3>(m_NetworkVariableVector3);
            Vector2_Var = new NetworkVariableHelper<Vector2>(m_NetworkVariableVector2);
            Ray_Var = new NetworkVariableHelper<Ray>(m_NetworkVariableRay);
            Ulong_Var = new NetworkVariableHelper<ulong>(m_NetworkVariableULong);
            Uint_Var = new NetworkVariableHelper<uint>(m_NetworkVariableUInt);
            Ushort_Var = new NetworkVariableHelper<ushort>(m_NetworkVariableUShort);
            FixedString32_Var = new NetworkVariableHelper<FixedString32Bytes>(m_NetworkVariableFixedString32);
            FixedString64_Var = new NetworkVariableHelper<FixedString64Bytes>(m_NetworkVariableFixedString64);
            FixedString128_Var = new NetworkVariableHelper<FixedString128Bytes>(m_NetworkVariableFixedString128);
            FixedString512_Var = new NetworkVariableHelper<FixedString512Bytes>(m_NetworkVariableFixedString512);
            FixedString4096_Var = new NetworkVariableHelper<FixedString4096Bytes>(m_NetworkVariableFixedString4096);
            Managed_Var = new NetworkVariableHelper<ManagedNetworkSerializableType>(m_NetworkVariableManaged);
        }

        /// <summary>
        /// Test result for all values changed the expected number of times (once per unique NetworkVariable type)
        /// </summary>
        public bool DidAllValuesChange()
        {
            if (NetworkVariableBaseHelper.VarChangedCount == NetworkVariableBaseHelper.InstanceCount)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Returns back whether the test has completed the total number of iterations
        /// </summary>
        public bool IsTestComplete()
        {
            return m_FinishedTests;
        }

        public void Awake()
        {
            InitializeTest();
        }

        public void AssertAllValuesAreCorrect()
        {
            Assert.AreEqual(false, m_NetworkVariableBool.Value);
            Assert.AreEqual(255, m_NetworkVariableByte.Value);
            Assert.AreEqual(100, m_NetworkVariableColor.Value.r);
            Assert.AreEqual(100, m_NetworkVariableColor.Value.g);
            Assert.AreEqual(100, m_NetworkVariableColor.Value.b);
            Assert.AreEqual(100, m_NetworkVariableColor32.Value.r);
            Assert.AreEqual(100, m_NetworkVariableColor32.Value.g);
            Assert.AreEqual(100, m_NetworkVariableColor32.Value.b);
            Assert.AreEqual(100, m_NetworkVariableColor32.Value.a);
            Assert.AreEqual(1000, m_NetworkVariableDouble.Value);
            Assert.AreEqual(1000.0f, m_NetworkVariableFloat.Value);
            Assert.AreEqual(1000, m_NetworkVariableInt.Value);
            Assert.AreEqual(100000, m_NetworkVariableLong.Value);
            Assert.AreEqual(-127, m_NetworkVariableSByte.Value);
            Assert.AreEqual(100, m_NetworkVariableQuaternion.Value.w);
            Assert.AreEqual(100, m_NetworkVariableQuaternion.Value.x);
            Assert.AreEqual(100, m_NetworkVariableQuaternion.Value.y);
            Assert.AreEqual(100, m_NetworkVariableQuaternion.Value.z);
            Assert.AreEqual(short.MaxValue, m_NetworkVariableShort.Value);
            Assert.AreEqual(1000, m_NetworkVariableVector4.Value.w);
            Assert.AreEqual(1000, m_NetworkVariableVector4.Value.x);
            Assert.AreEqual(1000, m_NetworkVariableVector4.Value.y);
            Assert.AreEqual(1000, m_NetworkVariableVector4.Value.z);
            Assert.AreEqual(1000, m_NetworkVariableVector3.Value.x);
            Assert.AreEqual(1000, m_NetworkVariableVector3.Value.y);
            Assert.AreEqual(1000, m_NetworkVariableVector3.Value.z);
            Assert.AreEqual(1000, m_NetworkVariableVector2.Value.x);
            Assert.AreEqual(1000, m_NetworkVariableVector2.Value.y);
            Assert.AreEqual(Vector3.one.x, m_NetworkVariableRay.Value.origin.x);
            Assert.AreEqual(Vector3.one.y, m_NetworkVariableRay.Value.origin.y);
            Assert.AreEqual(Vector3.one.z, m_NetworkVariableRay.Value.origin.z);
            Assert.AreEqual(Vector3.right.x, m_NetworkVariableRay.Value.direction.x);
            Assert.AreEqual(Vector3.right.y, m_NetworkVariableRay.Value.direction.y);
            Assert.AreEqual(Vector3.right.z, m_NetworkVariableRay.Value.direction.z);
            Assert.AreEqual(ulong.MaxValue, m_NetworkVariableULong.Value);
            Assert.AreEqual(uint.MaxValue, m_NetworkVariableUInt.Value);
            Assert.AreEqual(ushort.MaxValue, m_NetworkVariableUShort.Value);
            Assert.IsTrue(m_NetworkVariableFixedString32.Value.Equals("FixedString32Bytes"));
            Assert.IsTrue(m_NetworkVariableFixedString64.Value.Equals("FixedString64Bytes"));
            Assert.IsTrue(m_NetworkVariableFixedString128.Value.Equals("FixedString128Bytes"));
            Assert.IsTrue(m_NetworkVariableFixedString512.Value.Equals("FixedString512Bytes"));
            Assert.IsTrue(m_NetworkVariableFixedString4096.Value.Equals("FixedString4096Bytes"));
            Assert.IsTrue(m_NetworkVariableManaged.Value.Equals(new ManagedNetworkSerializableType
            {
                Str = "ManagedNetworkSerializableType",
                Ints = new[] { 1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000, 10000 },
                Embedded = new EmbeddedManagedNetworkSerializableType { Int = 20000 }
            }));
        }

        // Update is called once per frame
        private void Update()
        {
            if (EnableTesting)
            {
                //Added timeout functionality for near future changes to NetworkVariables
                if (!m_FinishedTests && m_ChangesAppliedToNetworkVariables)
                {
                    //We finish testing if all NetworkVariables changed their value or we timed out waiting for
                    //all NetworkVariables to change their value
                    m_FinishedTests = DidAllValuesChange() || (m_WaitForChangesTimeout < Time.realtimeSinceStartup);
                }
                else
                {
                    if (NetworkManager != null && NetworkManager.IsListening)
                    {
                        //Now change all of the values to make sure we are at least testing the local callback
                        m_NetworkVariableBool.Value = false;
                        m_NetworkVariableByte.Value = 255;
                        m_NetworkVariableColor.Value = new Color(100, 100, 100);
                        m_NetworkVariableColor32.Value = new Color32(100, 100, 100, 100);
                        m_NetworkVariableDouble.Value = 1000;
                        m_NetworkVariableFloat.Value = 1000.0f;
                        m_NetworkVariableInt.Value = 1000;
                        m_NetworkVariableLong.Value = 100000;
                        m_NetworkVariableSByte.Value = -127;
                        m_NetworkVariableQuaternion.Value = new Quaternion(100, 100, 100, 100);
                        m_NetworkVariableShort.Value = short.MaxValue;
                        m_NetworkVariableVector4.Value = new Vector4(1000, 1000, 1000, 1000);
                        m_NetworkVariableVector3.Value = new Vector3(1000, 1000, 1000);
                        m_NetworkVariableVector2.Value = new Vector2(1000, 1000);
                        m_NetworkVariableRay.Value = new Ray(Vector3.one, Vector3.right);
                        m_NetworkVariableULong.Value = ulong.MaxValue;
                        m_NetworkVariableUInt.Value = uint.MaxValue;
                        m_NetworkVariableUShort.Value = ushort.MaxValue;
                        m_NetworkVariableFixedString32.Value = new FixedString32Bytes("FixedString32Bytes");
                        m_NetworkVariableFixedString64.Value = new FixedString64Bytes("FixedString64Bytes");
                        m_NetworkVariableFixedString128.Value = new FixedString128Bytes("FixedString128Bytes");
                        m_NetworkVariableFixedString512.Value = new FixedString512Bytes("FixedString512Bytes");
                        m_NetworkVariableFixedString4096.Value = new FixedString4096Bytes("FixedString4096Bytes");
                        m_NetworkVariableManaged.Value = new ManagedNetworkSerializableType
                        {
                            Str = "ManagedNetworkSerializableType",
                            Ints = new[] { 1000, 2000, 3000, 4000, 5000, 6000, 7000, 8000, 9000, 10000 },
                            Embedded = new EmbeddedManagedNetworkSerializableType { Int = 20000 }
                        };

                        //Set the timeout (i.e. how long we will wait for all NetworkVariables to have registered their changes)
                        m_WaitForChangesTimeout = Time.realtimeSinceStartup + 0.50f;
                        m_ChangesAppliedToNetworkVariables = true;
                    }
                }
            }
        }
    }
}
