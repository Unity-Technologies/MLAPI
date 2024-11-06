using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Quaternion = UnityEngine.Quaternion;
using Vector2 = UnityEngine.Vector2;
using Vector3 = UnityEngine.Vector3;
using Vector4 = UnityEngine.Vector4;


namespace Unity.Netcode.RuntimeTests
{
    internal class RpcTypeSerializationTests : NetcodeIntegrationTest
    {
        public RpcTypeSerializationTests()
        {
            m_UseHost = false;
        }

        internal class RpcTestNB : NetworkBehaviour
        {
            public delegate void OnReceivedDelegate(object obj);
            public OnReceivedDelegate OnReceived;

            [ClientRpc]
            public void ByteClientRpc(byte value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void ByteArrayClientRpc(byte[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void ByteNativeArrayClientRpc(NativeArray<byte> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void ByteNativeListClientRpc(NativeList<byte> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void SbyteClientRpc(sbyte value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void SbyteArrayClientRpc(sbyte[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void SbyteNativeArrayClientRpc(NativeArray<sbyte> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void SbyteNativeListClientRpc(NativeList<sbyte> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void ShortClientRpc(short value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void ShortArrayClientRpc(short[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void ShortNativeArrayClientRpc(NativeArray<short> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void ShortNativeListClientRpc(NativeList<short> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void UshortClientRpc(ushort value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void UshortArrayClientRpc(ushort[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void UshortNativeArrayClientRpc(NativeArray<ushort> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void UshortNativeListClientRpc(NativeList<ushort> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void IntClientRpc(int value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void IntArrayClientRpc(int[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void IntNativeArrayClientRpc(NativeArray<int> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void IntNativeListClientRpc(NativeList<int> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void UintClientRpc(uint value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void UintArrayClientRpc(uint[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void UintNativeArrayClientRpc(NativeArray<uint> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void UintNativeListClientRpc(NativeList<uint> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void LongClientRpc(long value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void LongArrayClientRpc(long[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void LongNativeArrayClientRpc(NativeArray<long> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void LongNativeListClientRpc(NativeList<long> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void UlongClientRpc(ulong value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void UlongArrayClientRpc(ulong[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void UlongNativeArrayClientRpc(NativeArray<ulong> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void UlongNativeListClientRpc(NativeList<ulong> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void BoolClientRpc(bool value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void BoolArrayClientRpc(bool[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void BoolNativeArrayClientRpc(NativeArray<bool> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void BoolNativeListClientRpc(NativeList<bool> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void CharClientRpc(char value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void CharArrayClientRpc(char[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void CharNativeArrayClientRpc(NativeArray<char> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void CharNativeListClientRpc(NativeList<char> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void FloatClientRpc(float value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void FloatArrayClientRpc(float[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void FloatNativeArrayClientRpc(NativeArray<float> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void FloatNativeListClientRpc(NativeList<float> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void DoubleClientRpc(double value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void DoubleArrayClientRpc(double[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void DoubleNativeArrayClientRpc(NativeArray<double> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void DoubleNativeListClientRpc(NativeList<double> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void ByteEnumClientRpc(ByteEnum value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void ByteEnumArrayClientRpc(ByteEnum[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void ByteEnumNativeArrayClientRpc(NativeArray<ByteEnum> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void ByteEnumNativeListClientRpc(NativeList<ByteEnum> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void SByteEnumClientRpc(SByteEnum value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void SByteEnumArrayClientRpc(SByteEnum[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void SByteEnumNativeArrayClientRpc(NativeArray<SByteEnum> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void SByteEnumNativeListClientRpc(NativeList<SByteEnum> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void ShortEnumClientRpc(ShortEnum value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void ShortEnumArrayClientRpc(ShortEnum[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void ShortEnumNativeArrayClientRpc(NativeArray<ShortEnum> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void ShortEnumNativeListClientRpc(NativeList<ShortEnum> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void UShortEnumClientRpc(UShortEnum value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void UShortEnumArrayClientRpc(UShortEnum[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void UShortEnumNativeArrayClientRpc(NativeArray<UShortEnum> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void UShortEnumNativeListClientRpc(NativeList<UShortEnum> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void IntEnumClientRpc(IntEnum value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void IntEnumArrayClientRpc(IntEnum[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void IntEnumNativeArrayClientRpc(NativeArray<IntEnum> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void IntEnumNativeListClientRpc(NativeList<IntEnum> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void UIntEnumClientRpc(UIntEnum value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void UIntEnumArrayClientRpc(UIntEnum[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void UIntEnumNativeArrayClientRpc(NativeArray<UIntEnum> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void UIntEnumNativeListClientRpc(NativeList<UIntEnum> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void LongEnumClientRpc(LongEnum value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void LongEnumArrayClientRpc(LongEnum[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void LongEnumNativeArrayClientRpc(NativeArray<LongEnum> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void LongEnumNativeListClientRpc(NativeList<LongEnum> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void ULongEnumClientRpc(ULongEnum value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void ULongEnumArrayClientRpc(ULongEnum[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void ULongEnumNativeArrayClientRpc(NativeArray<ULongEnum> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void ULongEnumNativeListClientRpc(NativeList<ULongEnum> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void Vector2ClientRpc(Vector2 value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void Vector2ArrayClientRpc(Vector2[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void Vector2NativeArrayClientRpc(NativeArray<Vector2> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void Vector2NativeListClientRpc(NativeList<Vector2> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void Vector3ClientRpc(Vector3 value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void Vector3ArrayClientRpc(Vector3[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void Vector3NativeArrayClientRpc(NativeArray<Vector3> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void Vector3NativeListClientRpc(NativeList<Vector3> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void Vector2IntClientRpc(Vector2Int value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void Vector2IntArrayClientRpc(Vector2Int[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void Vector2IntNativeArrayClientRpc(NativeArray<Vector2Int> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void Vector2IntNativeListClientRpc(NativeList<Vector2Int> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void Vector3IntClientRpc(Vector3Int value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void Vector3IntArrayClientRpc(Vector3Int[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void Vector3IntNativeArrayClientRpc(NativeArray<Vector3Int> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void Vector3IntNativeListClientRpc(NativeList<Vector3Int> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void Vector4ClientRpc(Vector4 value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void Vector4ArrayClientRpc(Vector4[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void Vector4NativeArrayClientRpc(NativeArray<Vector4> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void Vector4NativeListClientRpc(NativeList<Vector4> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void QuaternionClientRpc(Quaternion value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void QuaternionArrayClientRpc(Quaternion[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void QuaternionNativeArrayClientRpc(NativeArray<Quaternion> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void QuaternionNativeListClientRpc(NativeList<Quaternion> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void ColorClientRpc(Color value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void ColorArrayClientRpc(Color[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void ColorNativeArrayClientRpc(NativeArray<Color> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void ColorNativeListClientRpc(NativeList<Color> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void Color32ClientRpc(Color32 value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void Color32ArrayClientRpc(Color32[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void Color32NativeArrayClientRpc(NativeArray<Color32> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void Color32NativeListClientRpc(NativeList<Color32> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void RayClientRpc(Ray value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void RayArrayClientRpc(Ray[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void RayNativeArrayClientRpc(NativeArray<Ray> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void RayNativeListClientRpc(NativeList<Ray> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void Ray2DClientRpc(Ray2D value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void Ray2DArrayClientRpc(Ray2D[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void Ray2DNativeArrayClientRpc(NativeArray<Ray2D> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void Ray2DNativeListClientRpc(NativeList<Ray2D> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void NetworkVariableTestStructClientRpc(NetworkVariableTestStruct value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void NetworkVariableTestStructArrayClientRpc(NetworkVariableTestStruct[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void NetworkVariableTestStructNativeArrayClientRpc(NativeArray<NetworkVariableTestStruct> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void NetworkVariableTestStructNativeListClientRpc(NativeList<NetworkVariableTestStruct> value)
            {
                OnReceived(value);
            }
#endif

            [ClientRpc]
            public void FixedString32BytesClientRpc(FixedString32Bytes value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void FixedString32BytesArrayClientRpc(FixedString32Bytes[] value)
            {
                OnReceived(value);
            }

            [ClientRpc]
            public void FixedString32BytesNativeArrayClientRpc(NativeArray<FixedString32Bytes> value)
            {
                OnReceived(value);
            }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
            [ClientRpc]
            public void FixedString32BytesNativeListClientRpc(NativeList<FixedString32Bytes> value)
            {
                OnReceived(value);
            }
#endif
        }

        protected override int NumberOfClients => 1;

        protected override void OnCreatePlayerPrefab()
        {
            m_PlayerPrefab.AddComponent<RpcTestNB>();
        }

        public IEnumerator TestValueType<T>(T firstTest, T secondTest) where T : unmanaged
        {
            var methods = typeof(RpcTestNB).GetMethods();
            foreach (var method in methods)
            {
                var parms = method.GetParameters();
                if (parms.Length != 1)
                {
                    continue;
                }
                if (parms[0].ParameterType == typeof(T) && method.Name.EndsWith("ClientRpc"))
                {
                    object receivedValue = null;

                    // This is the *SERVER VERSION* of the *CLIENT PLAYER* RpcTestNB component
                    var serverObject = m_PlayerNetworkObjects[m_ServerNetworkManager.LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<RpcTestNB>();

                    // This is the *CLIENT VERSION* of the *CLIENT PLAYER* RpcTestNB component
                    var clientObject = m_PlayerNetworkObjects[m_ClientNetworkManagers[0].LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<RpcTestNB>();

                    clientObject.OnReceived = o =>
                    {
                        receivedValue = o;
                        VerboseDebug($"Received value {o}");
                    };
                    VerboseDebug($"Sending first RPC with {firstTest}");
                    method.Invoke(serverObject, new object[] { firstTest });

                    yield return WaitForMessageReceived<ClientRpcMessage>(m_ClientNetworkManagers.ToList());

                    Assert.IsNotNull(receivedValue);

                    Assert.AreEqual(receivedValue.GetType(), typeof(T));
                    var value = (T)receivedValue;
                    Assert.IsTrue(NetworkVariableSerialization<T>.AreEqual(ref value, ref firstTest));

                    receivedValue = null;

                    VerboseDebug($"Sending second RPC with {secondTest}");
                    method.Invoke(serverObject, new object[] { secondTest });

                    yield return WaitForMessageReceived<ClientRpcMessage>(m_ClientNetworkManagers.ToList());

                    Assert.IsNotNull(receivedValue);

                    Assert.AreEqual(receivedValue.GetType(), typeof(T));
                    value = (T)receivedValue;
                    Assert.IsTrue(NetworkVariableSerialization<T>.AreEqual(ref value, ref secondTest));
                    yield break;
                }
            }
            Assert.Fail($"Could not find RPC function for {typeof(T).Name}");
        }

        public IEnumerator TestValueTypeArray<T>(T[] firstTest, T[] secondTest) where T : unmanaged
        {
            var methods = typeof(RpcTestNB).GetMethods();
            foreach (var method in methods)
            {
                var parms = method.GetParameters();
                if (parms.Length != 1)
                {
                    continue;
                }
                if (parms[0].ParameterType == typeof(T[]) && method.Name.EndsWith("ClientRpc"))
                {
                    object receivedValue = null;

                    // This is the *SERVER VERSION* of the *CLIENT PLAYER* RpcTestNB component
                    var serverObject = m_PlayerNetworkObjects[m_ServerNetworkManager.LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<RpcTestNB>();

                    // This is the *CLIENT VERSION* of the *CLIENT PLAYER* RpcTestNB component
                    var clientObject = m_PlayerNetworkObjects[m_ClientNetworkManagers[0].LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<RpcTestNB>();

                    clientObject.OnReceived = o =>
                    {
                        receivedValue = o;
                        VerboseDebug($"Received value {o}");
                    };
                    VerboseDebug($"Sending first RPC with {firstTest}");
                    method.Invoke(serverObject, new object[] { firstTest });

                    yield return WaitForMessageReceived<ClientRpcMessage>(m_ClientNetworkManagers.ToList());

                    Assert.IsNotNull(receivedValue);

                    Assert.AreEqual(receivedValue.GetType(), typeof(T[]));
                    var value = (T[])receivedValue;
                    Assert.AreEqual(value, firstTest);

                    receivedValue = null;

                    VerboseDebug($"Sending second RPC with {secondTest}");
                    method.Invoke(serverObject, new object[] { secondTest });

                    yield return WaitForMessageReceived<ClientRpcMessage>(m_ClientNetworkManagers.ToList());

                    Assert.IsNotNull(receivedValue);

                    Assert.AreEqual(receivedValue.GetType(), typeof(T[]));
                    value = (T[])receivedValue;
                    Assert.AreEqual(value, secondTest);

                    method.Invoke(serverObject, new object[] { null });

                    yield return WaitForMessageReceived<ClientRpcMessage>(m_ClientNetworkManagers.ToList());

                    Assert.IsNull(receivedValue);
                    yield break;
                }
            }
            Assert.Fail($"Could not find RPC function for {typeof(T).Name}");
        }

        public IEnumerator TestValueTypeNativeArray<T>(NativeArray<T> firstTest, NativeArray<T> secondTest) where T : unmanaged
        {
            var methods = typeof(RpcTestNB).GetMethods();
            foreach (var method in methods)
            {
                var parms = method.GetParameters();
                if (parms.Length != 1)
                {
                    continue;
                }
                if (parms[0].ParameterType == typeof(NativeArray<T>) && method.Name.EndsWith("ClientRpc"))
                {
                    var receivedValue = new NativeArray<T>();

                    // This is the *SERVER VERSION* of the *CLIENT PLAYER* RpcTestNB component
                    var serverObject = m_PlayerNetworkObjects[m_ServerNetworkManager.LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<RpcTestNB>();

                    // This is the *CLIENT VERSION* of the *CLIENT PLAYER* RpcTestNB component
                    var clientObject = m_PlayerNetworkObjects[m_ClientNetworkManagers[0].LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<RpcTestNB>();

                    clientObject.OnReceived = o =>
                    {
                        Assert.AreEqual(o.GetType(), typeof(NativeArray<T>));
                        var oAsArray = (NativeArray<T>)o;
                        receivedValue = new NativeArray<T>(oAsArray, Allocator.Persistent);
                        VerboseDebug($"Received value {o}");
                    };
                    VerboseDebug($"Sending first RPC with {firstTest}");
                    method.Invoke(serverObject, new object[] { firstTest });

                    yield return WaitForMessageReceived<ClientRpcMessage>(m_ClientNetworkManagers.ToList());

                    Assert.IsTrue(receivedValue.IsCreated);

                    Assert.IsTrue(NetworkVariableSerialization<NativeArray<T>>.AreEqual(ref receivedValue, ref firstTest));
                    receivedValue.Dispose();
                    firstTest.Dispose();

                    receivedValue = new NativeArray<T>();

                    VerboseDebug($"Sending second RPC with {secondTest}");
                    method.Invoke(serverObject, new object[] { secondTest });

                    yield return WaitForMessageReceived<ClientRpcMessage>(m_ClientNetworkManagers.ToList());

                    Assert.IsTrue(receivedValue.IsCreated);

                    Assert.IsTrue(NetworkVariableSerialization<NativeArray<T>>.AreEqual(ref receivedValue, ref secondTest));
                    receivedValue.Dispose();
                    secondTest.Dispose();
                    yield break;
                }
            }
            Assert.Fail($"Could not find RPC function for {typeof(T).Name}");
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        public IEnumerator TestValueTypeNativeList<T>(NativeList<T> firstTest, NativeList<T> secondTest) where T : unmanaged
        {
            var methods = typeof(RpcTestNB).GetMethods();
            foreach (var method in methods)
            {
                var parms = method.GetParameters();
                if (parms.Length != 1)
                {
                    continue;
                }
                if (parms[0].ParameterType == typeof(NativeList<T>) && method.Name.EndsWith("ClientRpc"))
                {
                    var receivedValue = new NativeList<T>();

                    // This is the *SERVER VERSION* of the *CLIENT PLAYER* RpcTestNB component
                    var serverObject = m_PlayerNetworkObjects[m_ServerNetworkManager.LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<RpcTestNB>();

                    // This is the *CLIENT VERSION* of the *CLIENT PLAYER* RpcTestNB component
                    var clientObject = m_PlayerNetworkObjects[m_ClientNetworkManagers[0].LocalClientId][m_ClientNetworkManagers[0].LocalClientId].GetComponent<RpcTestNB>();

                    clientObject.OnReceived = o =>
                    {
                        Assert.AreEqual(o.GetType(), typeof(NativeList<T>));
                        var oAsList = (NativeList<T>)o;
                        receivedValue = new NativeList<T>(oAsList.Length, Allocator.Persistent);
                        foreach (var item in oAsList)
                        {
                            receivedValue.Add(item);
                        }
                        VerboseDebug($"Received value {o}");
                    };
                    VerboseDebug($"Sending first RPC with {firstTest}");
                    method.Invoke(serverObject, new object[] { firstTest });

                    yield return WaitForMessageReceived<ClientRpcMessage>(m_ClientNetworkManagers.ToList());

                    Assert.IsTrue(receivedValue.IsCreated);

                    Assert.IsTrue(NetworkVariableSerialization<NativeList<T>>.AreEqual(ref receivedValue, ref firstTest));
                    receivedValue.Dispose();
                    firstTest.Dispose();

                    receivedValue = new NativeList<T>();

                    VerboseDebug($"Sending second RPC with {secondTest}");
                    method.Invoke(serverObject, new object[] { secondTest });

                    yield return WaitForMessageReceived<ClientRpcMessage>(m_ClientNetworkManagers.ToList());

                    Assert.IsTrue(receivedValue.IsCreated);

                    Assert.IsTrue(NetworkVariableSerialization<NativeList<T>>.AreEqual(ref receivedValue, ref secondTest));
                    receivedValue.Dispose();
                    secondTest.Dispose();
                    yield break;
                }
            }
            Assert.Fail($"Could not find RPC function for {typeof(T).Name}");
        }
#endif

        [UnityTest]
        public IEnumerator WhenSendingAValueTypeOverAnRpc_ValuesAreSerializedCorrectly(

            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(ByteEnum), typeof(SByteEnum), typeof(ShortEnum), typeof(UShortEnum), typeof(IntEnum),
                typeof(UIntEnum), typeof(LongEnum), typeof(ULongEnum), typeof(Vector2), typeof(Vector3),
                typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4), typeof(Quaternion), typeof(Color),
                typeof(Color32), typeof(Ray), typeof(Ray2D), typeof(NetworkVariableTestStruct), typeof(FixedString32Bytes))]
            Type testType)
        {
            if (testType == typeof(byte))
            {
                yield return TestValueType<byte>(byte.MinValue + 5, byte.MaxValue);
            }
            else if (testType == typeof(sbyte))
            {
                yield return TestValueType<sbyte>(sbyte.MinValue + 5, sbyte.MaxValue);
            }
            else if (testType == typeof(short))
            {
                yield return TestValueType<short>(short.MinValue + 5, short.MaxValue);
            }
            else if (testType == typeof(ushort))
            {
                yield return TestValueType<ushort>(ushort.MinValue + 5, ushort.MaxValue);
            }
            else if (testType == typeof(int))
            {
                yield return TestValueType(int.MinValue + 5, int.MaxValue);
            }
            else if (testType == typeof(uint))
            {
                yield return TestValueType(uint.MinValue + 5, uint.MaxValue);
            }
            else if (testType == typeof(long))
            {
                yield return TestValueType(long.MinValue + 5, long.MaxValue);
            }
            else if (testType == typeof(ulong))
            {
                yield return TestValueType(ulong.MinValue + 5, ulong.MaxValue);
            }
            else if (testType == typeof(bool))
            {
                yield return TestValueType(true, false);
            }
            else if (testType == typeof(char))
            {
                yield return TestValueType('z', ' ');
            }
            else if (testType == typeof(float))
            {
                yield return TestValueType(float.MinValue + 5.12345678f, float.MaxValue);
            }
            else if (testType == typeof(double))
            {
                yield return TestValueType(double.MinValue + 5.12345678, double.MaxValue);
            }
            else if (testType == typeof(ByteEnum))
            {
                yield return TestValueType(ByteEnum.B, ByteEnum.C);
            }
            else if (testType == typeof(SByteEnum))
            {
                yield return TestValueType(SByteEnum.B, SByteEnum.C);
            }
            else if (testType == typeof(ShortEnum))
            {
                yield return TestValueType(ShortEnum.B, ShortEnum.C);
            }
            else if (testType == typeof(UShortEnum))
            {
                yield return TestValueType(UShortEnum.B, UShortEnum.C);
            }
            else if (testType == typeof(IntEnum))
            {
                yield return TestValueType(IntEnum.B, IntEnum.C);
            }
            else if (testType == typeof(UIntEnum))
            {
                yield return TestValueType(UIntEnum.B, UIntEnum.C);
            }
            else if (testType == typeof(LongEnum))
            {
                yield return TestValueType(LongEnum.B, LongEnum.C);
            }
            else if (testType == typeof(ULongEnum))
            {
                yield return TestValueType(ULongEnum.B, ULongEnum.C);
            }
            else if (testType == typeof(Vector2))
            {
                yield return TestValueType(
                    new Vector2(5, 10),
                    new Vector2(15, 20));
            }
            else if (testType == typeof(Vector3))
            {
                yield return TestValueType(
                    new Vector3(5, 10, 15),
                    new Vector3(20, 25, 30));
            }
            else if (testType == typeof(Vector2Int))
            {
                yield return TestValueType(
                    new Vector2Int(5, 10),
                    new Vector2Int(15, 20));
            }
            else if (testType == typeof(Vector3Int))
            {
                yield return TestValueType(
                    new Vector3Int(5, 10, 15),
                    new Vector3Int(20, 25, 30));
            }
            else if (testType == typeof(Vector4))
            {
                yield return TestValueType(
                    new Vector4(5, 10, 15, 20),
                    new Vector4(25, 30, 35, 40));
            }
            else if (testType == typeof(Quaternion))
            {
                yield return TestValueType(
                    new Quaternion(5, 10, 15, 20),
                    new Quaternion(25, 30, 35, 40));
            }
            else if (testType == typeof(Color))
            {
                yield return TestValueType(
                    new Color(1, 0, 0),
                    new Color(0, 1, 1));
            }
            else if (testType == typeof(Color32))
            {
                yield return TestValueType(
                    new Color32(255, 0, 0, 128),
                    new Color32(0, 255, 255, 255));
            }
            else if (testType == typeof(Ray))
            {
                yield return TestValueType(
                    new Ray(new Vector3(0, 1, 2), new Vector3(3, 4, 5)),
                    new Ray(new Vector3(6, 7, 8), new Vector3(9, 10, 11)));
            }
            else if (testType == typeof(Ray2D))
            {
                yield return TestValueType(
                    new Ray2D(new Vector2(0, 1), new Vector2(2, 3)),
                    new Ray2D(new Vector2(4, 5), new Vector2(6, 7)));
            }
            else if (testType == typeof(NetworkVariableTestStruct))
            {
                yield return TestValueType(NetworkVariableTestStruct.GetTestStruct(), NetworkVariableTestStruct.GetTestStruct());
            }
            else if (testType == typeof(FixedString32Bytes))
            {
                yield return TestValueType(new FixedString32Bytes("foobar"), new FixedString32Bytes("12345678901234567890123456789"));
            }
        }

        [UnityTest]
        public IEnumerator WhenSendingAnArrayOfValueTypesOverAnRpc_ValuesAreSerializedCorrectly(

            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(ByteEnum), typeof(SByteEnum), typeof(ShortEnum), typeof(UShortEnum), typeof(IntEnum),
                typeof(UIntEnum), typeof(LongEnum), typeof(ULongEnum), typeof(Vector2), typeof(Vector3),
                typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4), typeof(Quaternion), typeof(Color),
                typeof(Color32), typeof(Ray), typeof(Ray2D), typeof(NetworkVariableTestStruct), typeof(FixedString32Bytes))]
            Type testType)
        {
            if (testType == typeof(byte))
            {
                yield return TestValueTypeArray(
                    new byte[] { byte.MinValue + 5, byte.MaxValue },
                    new byte[] { 0, byte.MinValue + 10, byte.MaxValue - 10 });
            }
            else if (testType == typeof(sbyte))
            {
                yield return TestValueTypeArray(
                    new sbyte[] { sbyte.MinValue + 5, sbyte.MaxValue },
                    new sbyte[] { 0, sbyte.MinValue + 10, sbyte.MaxValue - 10 });
            }
            else if (testType == typeof(short))
            {
                yield return TestValueTypeArray(
                    new short[] { short.MinValue + 5, short.MaxValue },
                    new short[] { 0, short.MinValue + 10, short.MaxValue - 10 });
            }
            else if (testType == typeof(ushort))
            {
                yield return TestValueTypeArray(
                    new ushort[] { ushort.MinValue + 5, ushort.MaxValue },
                    new ushort[] { 0, ushort.MinValue + 10, ushort.MaxValue - 10 });
            }
            else if (testType == typeof(int))
            {
                yield return TestValueTypeArray(
                    new int[] { int.MinValue + 5, int.MaxValue },
                    new int[] { 0, int.MinValue + 10, int.MaxValue - 10 });
            }
            else if (testType == typeof(uint))
            {
                yield return TestValueTypeArray(
                    new uint[] { uint.MinValue + 5, uint.MaxValue },
                    new uint[] { 0, uint.MinValue + 10, uint.MaxValue - 10 });
            }
            else if (testType == typeof(long))
            {
                yield return TestValueTypeArray(
                    new long[] { long.MinValue + 5, long.MaxValue },
                    new long[] { 0, long.MinValue + 10, long.MaxValue - 10 });
            }
            else if (testType == typeof(ulong))
            {
                yield return TestValueTypeArray(
                    new ulong[] { ulong.MinValue + 5, ulong.MaxValue },
                    new ulong[] { 0, ulong.MinValue + 10, ulong.MaxValue - 10 });
            }
            else if (testType == typeof(bool))
            {
                yield return TestValueTypeArray(
                    new bool[] { true, false, true },
                    new bool[] { false, true, false, true, false });
            }
            else if (testType == typeof(char))
            {
                yield return TestValueTypeArray(
                    new char[] { 'z', ' ', '?' },
                    new char[] { 'n', 'e', 'w', ' ', 'v', 'a', 'l', 'u', 'e' });
            }
            else if (testType == typeof(float))
            {
                yield return TestValueTypeArray(
                    new float[] { float.MinValue + 5.12345678f, float.MaxValue },
                    new float[] { 0, float.MinValue + 10.987654321f, float.MaxValue - 10.135792468f });
            }
            else if (testType == typeof(double))
            {
                yield return TestValueTypeArray(
                    new double[] { double.MinValue + 5.12345678, double.MaxValue },
                    new double[] { 0, double.MinValue + 10.987654321, double.MaxValue - 10.135792468 });
            }
            else if (testType == typeof(ByteEnum))
            {
                yield return TestValueTypeArray(
                    new ByteEnum[] { ByteEnum.C, ByteEnum.B, ByteEnum.A },
                    new ByteEnum[] { ByteEnum.B, ByteEnum.C, ByteEnum.B, ByteEnum.A, ByteEnum.C });
            }
            else if (testType == typeof(SByteEnum))
            {
                yield return TestValueTypeArray(
                    new SByteEnum[] { SByteEnum.C, SByteEnum.B, SByteEnum.A },
                    new SByteEnum[] { SByteEnum.B, SByteEnum.C, SByteEnum.B, SByteEnum.A, SByteEnum.C });
            }
            else if (testType == typeof(ShortEnum))
            {
                yield return TestValueTypeArray(
                    new ShortEnum[] { ShortEnum.C, ShortEnum.B, ShortEnum.A },
                    new ShortEnum[] { ShortEnum.B, ShortEnum.C, ShortEnum.B, ShortEnum.A, ShortEnum.C });
            }
            else if (testType == typeof(UShortEnum))
            {
                yield return TestValueTypeArray(
                    new UShortEnum[] { UShortEnum.C, UShortEnum.B, UShortEnum.A },
                    new UShortEnum[] { UShortEnum.B, UShortEnum.C, UShortEnum.B, UShortEnum.A, UShortEnum.C });
            }
            else if (testType == typeof(IntEnum))
            {
                yield return TestValueTypeArray(
                    new IntEnum[] { IntEnum.C, IntEnum.B, IntEnum.A },
                    new IntEnum[] { IntEnum.B, IntEnum.C, IntEnum.B, IntEnum.A, IntEnum.C });
            }
            else if (testType == typeof(UIntEnum))
            {
                yield return TestValueTypeArray(
                    new UIntEnum[] { UIntEnum.C, UIntEnum.B, UIntEnum.A },
                    new UIntEnum[] { UIntEnum.B, UIntEnum.C, UIntEnum.B, UIntEnum.A, UIntEnum.C });
            }
            else if (testType == typeof(LongEnum))
            {
                yield return TestValueTypeArray(
                    new LongEnum[] { LongEnum.C, LongEnum.B, LongEnum.A },
                    new LongEnum[] { LongEnum.B, LongEnum.C, LongEnum.B, LongEnum.A, LongEnum.C });
            }
            else if (testType == typeof(ULongEnum))
            {
                yield return TestValueTypeArray(
                    new ULongEnum[] { ULongEnum.C, ULongEnum.B, ULongEnum.A },
                    new ULongEnum[] { ULongEnum.B, ULongEnum.C, ULongEnum.B, ULongEnum.A, ULongEnum.C });
            }
            else if (testType == typeof(Vector2))
            {
                yield return TestValueTypeArray(
                    new Vector2[] { new Vector2(5, 10), new Vector2(15, 20) },
                    new Vector2[] { new Vector2(25, 30), new Vector2(35, 40), new Vector2(45, 50) });
            }
            else if (testType == typeof(Vector3))
            {
                yield return TestValueTypeArray(
                    new Vector3[] { new Vector3(5, 10, 15), new Vector3(20, 25, 30) },
                    new Vector3[] { new Vector3(35, 40, 45), new Vector3(50, 55, 60), new Vector3(65, 70, 75) });
            }
            else if (testType == typeof(Vector2Int))
            {
                yield return TestValueTypeArray(
                    new Vector2Int[] { new Vector2Int(5, 10), new Vector2Int(15, 20) },
                    new Vector2Int[] { new Vector2Int(25, 30), new Vector2Int(35, 40), new Vector2Int(45, 50) });
            }
            else if (testType == typeof(Vector3Int))
            {
                yield return TestValueTypeArray(
                    new Vector3Int[] { new Vector3Int(5, 10, 15), new Vector3Int(20, 25, 30) },
                    new Vector3Int[] { new Vector3Int(35, 40, 45), new Vector3Int(50, 55, 60), new Vector3Int(65, 70, 75) });
            }
            else if (testType == typeof(Vector4))
            {
                yield return TestValueTypeArray(
                    new Vector4[] { new Vector4(5, 10, 15, 20), new Vector4(25, 30, 35, 40) },
                    new Vector4[] { new Vector4(45, 50, 55, 60), new Vector4(65, 70, 75, 80), new Vector4(85, 90, 95, 100) });
            }
            else if (testType == typeof(Quaternion))
            {
                yield return TestValueTypeArray(
                    new Quaternion[] { new Quaternion(5, 10, 15, 20), new Quaternion(25, 30, 35, 40) },
                    new Quaternion[] { new Quaternion(45, 50, 55, 60), new Quaternion(65, 70, 75, 80), new Quaternion(85, 90, 95, 100) });
            }
            else if (testType == typeof(Color))
            {
                yield return TestValueTypeArray(
                    new Color[] { new Color(.5f, .10f, .15f), new Color(.20f, .25f, .30f) },
                    new Color[] { new Color(.35f, .40f, .45f), new Color(.50f, .55f, .60f), new Color(.65f, .70f, .75f) });
            }
            else if (testType == typeof(Color32))
            {
                yield return TestValueTypeArray(
                    new Color32[] { new Color32(5, 10, 15, 20), new Color32(25, 30, 35, 40) },
                    new Color32[] { new Color32(45, 50, 55, 60), new Color32(65, 70, 75, 80), new Color32(85, 90, 95, 100) });
            }
            else if (testType == typeof(Ray))
            {
                yield return TestValueTypeArray(
                    new Ray[]
                    {
                        new Ray(new Vector3(0, 1, 2), new Vector3(3, 4, 5)),
                        new Ray(new Vector3(6, 7, 8), new Vector3(9, 10, 11)),
                    },
                    new Ray[]
                    {
                        new Ray(new Vector3(12, 13, 14), new Vector3(15, 16, 17)),
                        new Ray(new Vector3(18, 19, 20), new Vector3(21, 22, 23)),
                        new Ray(new Vector3(24, 25, 26), new Vector3(27, 28, 29)),
                    });
            }
            else if (testType == typeof(Ray2D))
            {
                yield return TestValueTypeArray(
                    new Ray2D[]
                    {
                        new Ray2D(new Vector2(0, 1), new Vector2(3, 4)),
                        new Ray2D(new Vector2(6, 7), new Vector2(9, 10)),
                    },
                    new Ray2D[]
                    {
                        new Ray2D(new Vector2(12, 13), new Vector2(15, 16)),
                        new Ray2D(new Vector2(18, 19), new Vector2(21, 22)),
                        new Ray2D(new Vector2(24, 25), new Vector2(27, 28)),
                    });
            }
            else if (testType == typeof(NetworkVariableTestStruct))
            {
                yield return TestValueTypeArray(
                    new NetworkVariableTestStruct[]
                    {
                        NetworkVariableTestStruct.GetTestStruct(),
                        NetworkVariableTestStruct.GetTestStruct()
                    },
                    new NetworkVariableTestStruct[]
                    {
                        NetworkVariableTestStruct.GetTestStruct(),
                        NetworkVariableTestStruct.GetTestStruct(),
                        NetworkVariableTestStruct.GetTestStruct()
                    });
            }
            else if (testType == typeof(FixedString32Bytes))
            {
                yield return TestValueTypeArray(
                    new FixedString32Bytes[]
                    {
                        new FixedString32Bytes("foobar"),
                        new FixedString32Bytes("12345678901234567890123456789")
                    },
                    new FixedString32Bytes[]
                    {
                        new FixedString32Bytes("BazQux"),
                        new FixedString32Bytes("98765432109876543210987654321"),
                        new FixedString32Bytes("FixedString32Bytes")
                    });
            }
        }

        [UnityTest]
        public IEnumerator WhenSendingANativeArrayOfValueTypesOverAnRpc_ValuesAreSerializedCorrectly(

            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(ByteEnum), typeof(SByteEnum), typeof(ShortEnum), typeof(UShortEnum), typeof(IntEnum),
                typeof(UIntEnum), typeof(LongEnum), typeof(ULongEnum), typeof(Vector2), typeof(Vector3),
                typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4), typeof(Quaternion), typeof(Color),
                typeof(Color32), typeof(Ray), typeof(Ray2D), typeof(NetworkVariableTestStruct), typeof(FixedString32Bytes))]
            Type testType)
        {
            if (testType == typeof(byte))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<byte>(new byte[] { byte.MinValue + 5, byte.MaxValue }, Allocator.Persistent),
                    new NativeArray<byte>(new byte[] { 0, byte.MinValue + 10, byte.MaxValue - 10 }, Allocator.Persistent));
            }
            else if (testType == typeof(sbyte))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<sbyte>(new sbyte[] { sbyte.MinValue + 5, sbyte.MaxValue }, Allocator.Persistent),
                    new NativeArray<sbyte>(new sbyte[] { 0, sbyte.MinValue + 10, sbyte.MaxValue - 10 }, Allocator.Persistent));
            }
            else if (testType == typeof(short))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<short>(new short[] { short.MinValue + 5, short.MaxValue }, Allocator.Persistent),
                    new NativeArray<short>(new short[] { 0, short.MinValue + 10, short.MaxValue - 10 }, Allocator.Persistent));
            }
            else if (testType == typeof(ushort))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<ushort>(new ushort[] { ushort.MinValue + 5, ushort.MaxValue }, Allocator.Persistent),
                    new NativeArray<ushort>(new ushort[] { 0, ushort.MinValue + 10, ushort.MaxValue - 10 }, Allocator.Persistent));
            }
            else if (testType == typeof(int))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<int>(new int[] { int.MinValue + 5, int.MaxValue }, Allocator.Persistent),
                    new NativeArray<int>(new int[] { 0, int.MinValue + 10, int.MaxValue - 10 }, Allocator.Persistent));
            }
            else if (testType == typeof(uint))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<uint>(new uint[] { uint.MinValue + 5, uint.MaxValue }, Allocator.Persistent),
                    new NativeArray<uint>(new uint[] { 0, uint.MinValue + 10, uint.MaxValue - 10 }, Allocator.Persistent));
            }
            else if (testType == typeof(long))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<long>(new long[] { long.MinValue + 5, long.MaxValue }, Allocator.Persistent),
                    new NativeArray<long>(new long[] { 0, long.MinValue + 10, long.MaxValue - 10 }, Allocator.Persistent));
            }
            else if (testType == typeof(ulong))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<ulong>(new ulong[] { ulong.MinValue + 5, ulong.MaxValue }, Allocator.Persistent),
                    new NativeArray<ulong>(new ulong[] { 0, ulong.MinValue + 10, ulong.MaxValue - 10 }, Allocator.Persistent));
            }
            else if (testType == typeof(bool))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<bool>(new bool[] { true, false, true }, Allocator.Persistent),
                    new NativeArray<bool>(new bool[] { false, true, false, true, false }, Allocator.Persistent));
            }
            else if (testType == typeof(char))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<char>(new char[] { 'z', ' ', '?' }, Allocator.Persistent),
                    new NativeArray<char>(new char[] { 'n', 'e', 'w', ' ', 'v', 'a', 'l', 'u', 'e' }, Allocator.Persistent));
            }
            else if (testType == typeof(float))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<float>(new float[] { float.MinValue + 5.12345678f, float.MaxValue }, Allocator.Persistent),
                    new NativeArray<float>(new float[] { 0, float.MinValue + 10.987654321f, float.MaxValue - 10.135792468f }, Allocator.Persistent));
            }
            else if (testType == typeof(double))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<double>(new double[] { double.MinValue + 5.12345678, double.MaxValue }, Allocator.Persistent),
                    new NativeArray<double>(new double[] { 0, double.MinValue + 10.987654321, double.MaxValue - 10.135792468 }, Allocator.Persistent));
            }
            else if (testType == typeof(ByteEnum))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<ByteEnum>(new ByteEnum[] { ByteEnum.C, ByteEnum.B, ByteEnum.A }, Allocator.Persistent),
                    new NativeArray<ByteEnum>(new ByteEnum[] { ByteEnum.B, ByteEnum.C, ByteEnum.B, ByteEnum.A, ByteEnum.C }, Allocator.Persistent));
            }
            else if (testType == typeof(SByteEnum))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<SByteEnum>(new SByteEnum[] { SByteEnum.C, SByteEnum.B, SByteEnum.A }, Allocator.Persistent),
                    new NativeArray<SByteEnum>(new SByteEnum[] { SByteEnum.B, SByteEnum.C, SByteEnum.B, SByteEnum.A, SByteEnum.C }, Allocator.Persistent));
            }
            else if (testType == typeof(ShortEnum))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<ShortEnum>(new ShortEnum[] { ShortEnum.C, ShortEnum.B, ShortEnum.A }, Allocator.Persistent),
                    new NativeArray<ShortEnum>(new ShortEnum[] { ShortEnum.B, ShortEnum.C, ShortEnum.B, ShortEnum.A, ShortEnum.C }, Allocator.Persistent));
            }
            else if (testType == typeof(UShortEnum))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<UShortEnum>(new UShortEnum[] { UShortEnum.C, UShortEnum.B, UShortEnum.A }, Allocator.Persistent),
                    new NativeArray<UShortEnum>(new UShortEnum[] { UShortEnum.B, UShortEnum.C, UShortEnum.B, UShortEnum.A, UShortEnum.C }, Allocator.Persistent));
            }
            else if (testType == typeof(IntEnum))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<IntEnum>(new IntEnum[] { IntEnum.C, IntEnum.B, IntEnum.A }, Allocator.Persistent),
                    new NativeArray<IntEnum>(new IntEnum[] { IntEnum.B, IntEnum.C, IntEnum.B, IntEnum.A, IntEnum.C }, Allocator.Persistent));
            }
            else if (testType == typeof(UIntEnum))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<UIntEnum>(new UIntEnum[] { UIntEnum.C, UIntEnum.B, UIntEnum.A }, Allocator.Persistent),
                    new NativeArray<UIntEnum>(new UIntEnum[] { UIntEnum.B, UIntEnum.C, UIntEnum.B, UIntEnum.A, UIntEnum.C }, Allocator.Persistent));
            }
            else if (testType == typeof(LongEnum))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<LongEnum>(new LongEnum[] { LongEnum.C, LongEnum.B, LongEnum.A }, Allocator.Persistent),
                    new NativeArray<LongEnum>(new LongEnum[] { LongEnum.B, LongEnum.C, LongEnum.B, LongEnum.A, LongEnum.C }, Allocator.Persistent));
            }
            else if (testType == typeof(ULongEnum))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<ULongEnum>(new ULongEnum[] { ULongEnum.C, ULongEnum.B, ULongEnum.A }, Allocator.Persistent),
                    new NativeArray<ULongEnum>(new ULongEnum[] { ULongEnum.B, ULongEnum.C, ULongEnum.B, ULongEnum.A, ULongEnum.C }, Allocator.Persistent));
            }
            else if (testType == typeof(Vector2))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<Vector2>(new Vector2[] { new Vector2(5, 10), new Vector2(15, 20) }, Allocator.Persistent),
                    new NativeArray<Vector2>(new Vector2[] { new Vector2(25, 30), new Vector2(35, 40), new Vector2(45, 50) }, Allocator.Persistent));
            }
            else if (testType == typeof(Vector3))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<Vector3>(new Vector3[] { new Vector3(5, 10, 15), new Vector3(20, 25, 30) }, Allocator.Persistent),
                    new NativeArray<Vector3>(new Vector3[] { new Vector3(35, 40, 45), new Vector3(50, 55, 60), new Vector3(65, 70, 75) }, Allocator.Persistent));
            }
            else if (testType == typeof(Vector2Int))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<Vector2Int>(new Vector2Int[] { new Vector2Int(5, 10), new Vector2Int(15, 20) }, Allocator.Persistent),
                    new NativeArray<Vector2Int>(new Vector2Int[] { new Vector2Int(25, 30), new Vector2Int(35, 40), new Vector2Int(45, 50) }, Allocator.Persistent));
            }
            else if (testType == typeof(Vector3Int))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<Vector3Int>(new Vector3Int[] { new Vector3Int(5, 10, 15), new Vector3Int(20, 25, 30) }, Allocator.Persistent),
                    new NativeArray<Vector3Int>(new Vector3Int[] { new Vector3Int(35, 40, 45), new Vector3Int(50, 55, 60), new Vector3Int(65, 70, 75) }, Allocator.Persistent));
            }
            else if (testType == typeof(Vector4))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<Vector4>(new Vector4[] { new Vector4(5, 10, 15, 20), new Vector4(25, 30, 35, 40) }, Allocator.Persistent),
                    new NativeArray<Vector4>(new Vector4[] { new Vector4(45, 50, 55, 60), new Vector4(65, 70, 75, 80), new Vector4(85, 90, 95, 100) }, Allocator.Persistent));
            }
            else if (testType == typeof(Quaternion))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<Quaternion>(new Quaternion[] { new Quaternion(5, 10, 15, 20), new Quaternion(25, 30, 35, 40) }, Allocator.Persistent),
                    new NativeArray<Quaternion>(new Quaternion[] { new Quaternion(45, 50, 55, 60), new Quaternion(65, 70, 75, 80), new Quaternion(85, 90, 95, 100) }, Allocator.Persistent));
            }
            else if (testType == typeof(Color))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<Color>(new Color[] { new Color(.5f, .10f, .15f), new Color(.20f, .25f, .30f) }, Allocator.Persistent),
                    new NativeArray<Color>(new Color[] { new Color(.35f, .40f, .45f), new Color(.50f, .55f, .60f), new Color(.65f, .70f, .75f) }, Allocator.Persistent));
            }
            else if (testType == typeof(Color32))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<Color32>(new Color32[] { new Color32(5, 10, 15, 20), new Color32(25, 30, 35, 40) }, Allocator.Persistent),
                    new NativeArray<Color32>(new Color32[] { new Color32(45, 50, 55, 60), new Color32(65, 70, 75, 80), new Color32(85, 90, 95, 100) }, Allocator.Persistent));
            }
            else if (testType == typeof(Ray))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<Ray>(new Ray[]
                    {
                        new Ray(new Vector3(0, 1, 2), new Vector3(3, 4, 5)),
                        new Ray(new Vector3(6, 7, 8), new Vector3(9, 10, 11)),
                    }, Allocator.Persistent),
                    new NativeArray<Ray>(new Ray[]
                    {
                        new Ray(new Vector3(12, 13, 14), new Vector3(15, 16, 17)),
                        new Ray(new Vector3(18, 19, 20), new Vector3(21, 22, 23)),
                        new Ray(new Vector3(24, 25, 26), new Vector3(27, 28, 29)),
                    }, Allocator.Persistent));
            }
            else if (testType == typeof(Ray2D))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<Ray2D>(new Ray2D[]
                    {
                        new Ray2D(new Vector2(0, 1), new Vector2(3, 4)),
                        new Ray2D(new Vector2(6, 7), new Vector2(9, 10)),
                    }, Allocator.Persistent),
                    new NativeArray<Ray2D>(new Ray2D[]
                    {
                        new Ray2D(new Vector2(12, 13), new Vector2(15, 16)),
                        new Ray2D(new Vector2(18, 19), new Vector2(21, 22)),
                        new Ray2D(new Vector2(24, 25), new Vector2(27, 28)),
                    }, Allocator.Persistent));
            }
            else if (testType == typeof(NetworkVariableTestStruct))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<NetworkVariableTestStruct>(new NetworkVariableTestStruct[]
                    {
                        NetworkVariableTestStruct.GetTestStruct(),
                        NetworkVariableTestStruct.GetTestStruct()
                    }, Allocator.Persistent),
                    new NativeArray<NetworkVariableTestStruct>(new NetworkVariableTestStruct[]
                    {
                        NetworkVariableTestStruct.GetTestStruct(),
                        NetworkVariableTestStruct.GetTestStruct(),
                        NetworkVariableTestStruct.GetTestStruct()
                    }, Allocator.Persistent));
            }
            else if (testType == typeof(FixedString32Bytes))
            {
                yield return TestValueTypeNativeArray(
                    new NativeArray<FixedString32Bytes>(new FixedString32Bytes[]
                    {
                        new FixedString32Bytes("foobar"),
                        new FixedString32Bytes("12345678901234567890123456789")
                    }, Allocator.Persistent),
                    new NativeArray<FixedString32Bytes>(new FixedString32Bytes[]
                    {
                        new FixedString32Bytes("BazQux"),
                        new FixedString32Bytes("98765432109876543210987654321"),
                        new FixedString32Bytes("FixedString32Bytes")
                    }, Allocator.Persistent));
            }
        }

#if UNITY_NETCODE_NATIVE_COLLECTION_SUPPORT
        [UnityTest]
        public IEnumerator WhenSendingANativeListOfValueTypesOverAnRpc_ValuesAreSerializedCorrectly(

            [Values(typeof(byte), typeof(sbyte), typeof(short), typeof(ushort), typeof(int), typeof(uint),
                typeof(long), typeof(ulong), typeof(bool), typeof(char), typeof(float), typeof(double),
                typeof(ByteEnum), typeof(SByteEnum), typeof(ShortEnum), typeof(UShortEnum), typeof(IntEnum),
                typeof(UIntEnum), typeof(LongEnum), typeof(ULongEnum), typeof(Vector2), typeof(Vector3),
                typeof(Vector2Int), typeof(Vector3Int), typeof(Vector4), typeof(Quaternion), typeof(Color),
                typeof(Color32), typeof(Ray), typeof(Ray2D), typeof(NetworkVariableTestStruct), typeof(FixedString32Bytes))]
            Type testType)
        {
            if (testType == typeof(byte))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<byte>(Allocator.Persistent) { byte.MinValue + 5, byte.MaxValue },
                    new NativeList<byte>(Allocator.Persistent) { 0, byte.MinValue + 10, byte.MaxValue - 10 });
            }
            else if (testType == typeof(sbyte))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<sbyte>(Allocator.Persistent) { sbyte.MinValue + 5, sbyte.MaxValue },
                    new NativeList<sbyte>(Allocator.Persistent) { 0, sbyte.MinValue + 10, sbyte.MaxValue - 10 });
            }
            else if (testType == typeof(short))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<short>(Allocator.Persistent) { short.MinValue + 5, short.MaxValue },
                    new NativeList<short>(Allocator.Persistent) { 0, short.MinValue + 10, short.MaxValue - 10 });
            }
            else if (testType == typeof(ushort))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<ushort>(Allocator.Persistent) { ushort.MinValue + 5, ushort.MaxValue },
                    new NativeList<ushort>(Allocator.Persistent) { 0, ushort.MinValue + 10, ushort.MaxValue - 10 });
            }
            else if (testType == typeof(int))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<int>(Allocator.Persistent) { int.MinValue + 5, int.MaxValue },
                    new NativeList<int>(Allocator.Persistent) { 0, int.MinValue + 10, int.MaxValue - 10 });
            }
            else if (testType == typeof(uint))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<uint>(Allocator.Persistent) { uint.MinValue + 5, uint.MaxValue },
                    new NativeList<uint>(Allocator.Persistent) { 0, uint.MinValue + 10, uint.MaxValue - 10 });
            }
            else if (testType == typeof(long))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<long>(Allocator.Persistent) { long.MinValue + 5, long.MaxValue },
                    new NativeList<long>(Allocator.Persistent) { 0, long.MinValue + 10, long.MaxValue - 10 });
            }
            else if (testType == typeof(ulong))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<ulong>(Allocator.Persistent) { ulong.MinValue + 5, ulong.MaxValue },
                    new NativeList<ulong>(Allocator.Persistent) { 0, ulong.MinValue + 10, ulong.MaxValue - 10 });
            }
            else if (testType == typeof(bool))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<bool>(Allocator.Persistent) { true, false, true },
                    new NativeList<bool>(Allocator.Persistent) { false, true, false, true, false });
            }
            else if (testType == typeof(char))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<char>(Allocator.Persistent) { 'z', ' ', '?' },
                    new NativeList<char>(Allocator.Persistent) { 'n', 'e', 'w', ' ', 'v', 'a', 'l', 'u', 'e' });
            }
            else if (testType == typeof(float))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<float>(Allocator.Persistent) { float.MinValue + 5.12345678f, float.MaxValue },
                    new NativeList<float>(Allocator.Persistent) { 0, float.MinValue + 10.987654321f, float.MaxValue - 10.135792468f });
            }
            else if (testType == typeof(double))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<double>(Allocator.Persistent) { double.MinValue + 5.12345678, double.MaxValue },
                    new NativeList<double>(Allocator.Persistent) { 0, double.MinValue + 10.987654321, double.MaxValue - 10.135792468 });
            }
            else if (testType == typeof(ByteEnum))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<ByteEnum>(Allocator.Persistent) { ByteEnum.C, ByteEnum.B, ByteEnum.A },
                    new NativeList<ByteEnum>(Allocator.Persistent) { ByteEnum.B, ByteEnum.C, ByteEnum.B, ByteEnum.A, ByteEnum.C });
            }
            else if (testType == typeof(SByteEnum))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<SByteEnum>(Allocator.Persistent) { SByteEnum.C, SByteEnum.B, SByteEnum.A },
                    new NativeList<SByteEnum>(Allocator.Persistent) { SByteEnum.B, SByteEnum.C, SByteEnum.B, SByteEnum.A, SByteEnum.C });
            }
            else if (testType == typeof(ShortEnum))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<ShortEnum>(Allocator.Persistent) { ShortEnum.C, ShortEnum.B, ShortEnum.A },
                    new NativeList<ShortEnum>(Allocator.Persistent) { ShortEnum.B, ShortEnum.C, ShortEnum.B, ShortEnum.A, ShortEnum.C });
            }
            else if (testType == typeof(UShortEnum))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<UShortEnum>(Allocator.Persistent) { UShortEnum.C, UShortEnum.B, UShortEnum.A },
                    new NativeList<UShortEnum>(Allocator.Persistent) { UShortEnum.B, UShortEnum.C, UShortEnum.B, UShortEnum.A, UShortEnum.C });
            }
            else if (testType == typeof(IntEnum))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<IntEnum>(Allocator.Persistent) { IntEnum.C, IntEnum.B, IntEnum.A },
                    new NativeList<IntEnum>(Allocator.Persistent) { IntEnum.B, IntEnum.C, IntEnum.B, IntEnum.A, IntEnum.C });
            }
            else if (testType == typeof(UIntEnum))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<UIntEnum>(Allocator.Persistent) { UIntEnum.C, UIntEnum.B, UIntEnum.A },
                    new NativeList<UIntEnum>(Allocator.Persistent) { UIntEnum.B, UIntEnum.C, UIntEnum.B, UIntEnum.A, UIntEnum.C });
            }
            else if (testType == typeof(LongEnum))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<LongEnum>(Allocator.Persistent) { LongEnum.C, LongEnum.B, LongEnum.A },
                    new NativeList<LongEnum>(Allocator.Persistent) { LongEnum.B, LongEnum.C, LongEnum.B, LongEnum.A, LongEnum.C });
            }
            else if (testType == typeof(ULongEnum))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<ULongEnum>(Allocator.Persistent) { ULongEnum.C, ULongEnum.B, ULongEnum.A },
                    new NativeList<ULongEnum>(Allocator.Persistent) { ULongEnum.B, ULongEnum.C, ULongEnum.B, ULongEnum.A, ULongEnum.C });
            }
            else if (testType == typeof(Vector2))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<Vector2>(Allocator.Persistent) { new Vector2(5, 10), new Vector2(15, 20) },
                    new NativeList<Vector2>(Allocator.Persistent) { new Vector2(25, 30), new Vector2(35, 40), new Vector2(45, 50) });
            }
            else if (testType == typeof(Vector3))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<Vector3>(Allocator.Persistent) { new Vector3(5, 10, 15), new Vector3(20, 25, 30) },
                    new NativeList<Vector3>(Allocator.Persistent) { new Vector3(35, 40, 45), new Vector3(50, 55, 60), new Vector3(65, 70, 75) });
            }
            else if (testType == typeof(Vector2Int))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<Vector2Int>(Allocator.Persistent) { new Vector2Int(5, 10), new Vector2Int(15, 20) },
                    new NativeList<Vector2Int>(Allocator.Persistent) { new Vector2Int(25, 30), new Vector2Int(35, 40), new Vector2Int(45, 50) });
            }
            else if (testType == typeof(Vector3Int))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<Vector3Int>(Allocator.Persistent) { new Vector3Int(5, 10, 15), new Vector3Int(20, 25, 30) },
                    new NativeList<Vector3Int>(Allocator.Persistent) { new Vector3Int(35, 40, 45), new Vector3Int(50, 55, 60), new Vector3Int(65, 70, 75) });
            }
            else if (testType == typeof(Vector4))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<Vector4>(Allocator.Persistent) { new Vector4(5, 10, 15, 20), new Vector4(25, 30, 35, 40) },
                    new NativeList<Vector4>(Allocator.Persistent) { new Vector4(45, 50, 55, 60), new Vector4(65, 70, 75, 80), new Vector4(85, 90, 95, 100) });
            }
            else if (testType == typeof(Quaternion))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<Quaternion>(Allocator.Persistent) { new Quaternion(5, 10, 15, 20), new Quaternion(25, 30, 35, 40) },
                    new NativeList<Quaternion>(Allocator.Persistent) { new Quaternion(45, 50, 55, 60), new Quaternion(65, 70, 75, 80), new Quaternion(85, 90, 95, 100) });
            }
            else if (testType == typeof(Color))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<Color>(Allocator.Persistent) { new Color(.5f, .10f, .15f), new Color(.20f, .25f, .30f) },
                    new NativeList<Color>(Allocator.Persistent) { new Color(.35f, .40f, .45f), new Color(.50f, .55f, .60f), new Color(.65f, .70f, .75f) });
            }
            else if (testType == typeof(Color32))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<Color32>(Allocator.Persistent) { new Color32(5, 10, 15, 20), new Color32(25, 30, 35, 40) },
                    new NativeList<Color32>(Allocator.Persistent) { new Color32(45, 50, 55, 60), new Color32(65, 70, 75, 80), new Color32(85, 90, 95, 100) });
            }
            else if (testType == typeof(Ray))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<Ray>(Allocator.Persistent)
                    {
                        new Ray(new Vector3(0, 1, 2), new Vector3(3, 4, 5)),
                        new Ray(new Vector3(6, 7, 8), new Vector3(9, 10, 11)),
                    },
                    new NativeList<Ray>(Allocator.Persistent)
                    {
                        new Ray(new Vector3(12, 13, 14), new Vector3(15, 16, 17)),
                        new Ray(new Vector3(18, 19, 20), new Vector3(21, 22, 23)),
                        new Ray(new Vector3(24, 25, 26), new Vector3(27, 28, 29)),
                    });
            }
            else if (testType == typeof(Ray2D))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<Ray2D>(Allocator.Persistent)
                    {
                        new Ray2D(new Vector2(0, 1), new Vector2(3, 4)),
                        new Ray2D(new Vector2(6, 7), new Vector2(9, 10)),
                    },
                    new NativeList<Ray2D>(Allocator.Persistent)
                    {
                        new Ray2D(new Vector2(12, 13), new Vector2(15, 16)),
                        new Ray2D(new Vector2(18, 19), new Vector2(21, 22)),
                        new Ray2D(new Vector2(24, 25), new Vector2(27, 28)),
                    });
            }
            else if (testType == typeof(NetworkVariableTestStruct))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<NetworkVariableTestStruct>(Allocator.Persistent)
                    {
                        NetworkVariableTestStruct.GetTestStruct(),
                        NetworkVariableTestStruct.GetTestStruct()
                    },
                    new NativeList<NetworkVariableTestStruct>(Allocator.Persistent)
                    {
                        NetworkVariableTestStruct.GetTestStruct(),
                        NetworkVariableTestStruct.GetTestStruct(),
                        NetworkVariableTestStruct.GetTestStruct()
                    });
            }
            else if (testType == typeof(FixedString32Bytes))
            {
                yield return TestValueTypeNativeList(
                    new NativeList<FixedString32Bytes>(Allocator.Persistent)
                    {
                        new FixedString32Bytes("foobar"),
                        new FixedString32Bytes("12345678901234567890123456789")
                    },
                    new NativeList<FixedString32Bytes>(Allocator.Persistent)
                    {
                        new FixedString32Bytes("BazQux"),
                        new FixedString32Bytes("98765432109876543210987654321"),
                        new FixedString32Bytes("FixedString32Bytes")
                    });
            }
        }
#endif
    }
}
