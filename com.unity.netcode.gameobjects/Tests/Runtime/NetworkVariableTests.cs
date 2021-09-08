using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;
using Unity.Collections;


namespace Unity.Netcode.RuntimeTests
{
    public struct FixedString32Struct : INetworkSerializable
    {
        public FixedString32 FixedString;
        public void NetworkSerialize(NetworkSerializer serializer)
        {
            if (serializer.IsReading)
            {
                var stringArraySize = 0;
                serializer.Serialize(ref stringArraySize);
                var stringArray = new char[stringArraySize];
                serializer.Serialize(ref stringArray);
                var asString = new string(stringArray);
                FixedString.CopyFrom(asString);
            }
            else
            {
                var stringArray = FixedString.Value.ToCharArray();
                var stringArraySize = stringArray.Length;
                serializer.Serialize(ref stringArraySize);
                serializer.Serialize(ref stringArray);
            }
        }
    }

    public struct TestStruct : INetworkSerializable
    {
        public uint SomeInt;
        public bool SomeBool;

        public void NetworkSerialize(NetworkSerializer serializer)
        {
            serializer.Serialize(ref SomeInt);
            serializer.Serialize(ref SomeBool);
        }
    }
    public class NetworkVariableTest : NetworkBehaviour
    {
        public readonly ClientNetworkVariable<int> ClientVar = new ClientNetworkVariable<int>();

        public readonly ClientNetworkVariable<int> ClientVarPrivate =
            new ClientNetworkVariable<int>(NetworkVariableReadPermission.OwnerOnly);

        public readonly NetworkVariable<int> TheScalar = new NetworkVariable<int>();
        public readonly NetworkList<int> TheList = new NetworkList<int>();
        public readonly NetworkSet<int> TheSet = new NetworkSet<int>();
        public readonly NetworkDictionary<int, int> TheDictionary = new NetworkDictionary<int, int>();

        public readonly NetworkVariable<FixedString32Struct> FixedStringStruct = new NetworkVariable<FixedString32Struct>();

        private void ListChanged(NetworkListEvent<int> e)
        {
            ListDelegateTriggered = true;
        }

        private void SetChanged(NetworkSetEvent<int> e)
        {
            SetDelegateTriggered = true;
        }

        private void DictionaryChanged(NetworkDictionaryEvent<int, int> e)
        {
            DictionaryDelegateTriggered = true;
        }

        public void Awake()
        {
            TheList.OnListChanged += ListChanged;
            TheSet.OnSetChanged += SetChanged;
            TheDictionary.OnDictionaryChanged += DictionaryChanged;
        }

        public readonly NetworkVariable<TestStruct> TheStruct = new NetworkVariable<TestStruct>();

        public bool ListDelegateTriggered;
        public bool SetDelegateTriggered;
        public bool DictionaryDelegateTriggered;
    }

    public class NetworkVariableTests : BaseMultiInstanceTest
    {
        private const string k_FixedStringTestValue = "abcdefghijklmnopqrstuvwxyz";
        protected override int NbClients => 2;

        private const uint k_TestUInt = 0x12345678;

        private const int k_TestVal1 = 111;
        private const int k_TestVal2 = 222;
        private const int k_TestVal3 = 333;

        private const int k_TestKey1 = 0x0f0f;
        private const int k_TestKey2 = 0xf0f0;

        // Player1 component on the server
        private NetworkVariableTest m_Player1OnServer;

        // Player2 component on the server
        private NetworkVariableTest m_Player2OnServer;

        // Player1 component on client1
        private NetworkVariableTest m_Player1OnClient1;

        // Player2 component on client1
        private NetworkVariableTest m_Player2OnClient2;

        // client2's version of client1's player object
        private NetworkVariableTest m_Player1OnClient2;

        private bool m_TestWithHost;

        [UnitySetUp]
        public override IEnumerator Setup()
        {
            yield return StartSomeClientsAndServerWithPlayers(useHost: m_TestWithHost, nbClients: NbClients,
                updatePlayerPrefab: playerPrefab =>
                {
                    playerPrefab.AddComponent<NetworkVariableTest>();
                });

            // These are the *SERVER VERSIONS* of the *CLIENT PLAYER 1 & 2*
            var result = new MultiInstanceHelpers.CoroutineResultWrapper<NetworkObject>();

            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation(
                x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId,
                m_ServerNetworkManager, result));
            m_Player1OnServer = result.Result.GetComponent<NetworkVariableTest>();

            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation(
                x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[1].LocalClientId,
                m_ServerNetworkManager, result));
            m_Player2OnServer = result.Result.GetComponent<NetworkVariableTest>();

            // This is client1's view of itself
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation(
                x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId,
                m_ClientNetworkManagers[0], result));

            m_Player1OnClient1 = result.Result.GetComponent<NetworkVariableTest>();

            // This is client2's view of itself
            result = new MultiInstanceHelpers.CoroutineResultWrapper<NetworkObject>();
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation(
                x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[1].LocalClientId,
                m_ClientNetworkManagers[1], result));

            m_Player2OnClient2 = result.Result.GetComponent<NetworkVariableTest>();

            // This is client2's view of client 1's object
            yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation(
                x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId,
                m_ClientNetworkManagers[1], result));

            m_Player1OnClient2 = result.Result.GetComponent<NetworkVariableTest>();

            m_Player1OnServer.TheList.Clear();
            m_Player1OnServer.TheSet.Clear();
            m_Player1OnServer.TheDictionary.Clear();

            if (m_Player1OnServer.TheList.Count > 0 || m_Player1OnServer.TheSet.Count > 0 || m_Player1OnServer.TheDictionary.Count > 0)
            {
                throw new Exception("at least one server network container not empty at start");
            }
            if (m_Player1OnClient1.TheList.Count > 0 || m_Player1OnClient1.TheSet.Count > 0 || m_Player1OnClient1.TheDictionary.Count > 0)
            {
                throw new Exception("at least one client network container not empty at start");
            }
        }

        /// <summary>
        /// Runs generalized tests on all predefined NetworkVariable types
        /// </summary>
        [UnityTest]
        public IEnumerator AllNetworkVariableTypes([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;

            // Create, instantiate, and host
            // This would normally go in Setup, but since every other test but this one
            //  uses MultiInstanceHelper, and it does its own NetworkManager setup / teardown,
            //  for now we put this within this one test until we migrate it to MIH
            Assert.IsTrue(NetworkManagerHelper.StartNetworkManager(out _));

            Guid gameObjectId = NetworkManagerHelper.AddGameNetworkObject("NetworkVariableTestComponent");

            var networkVariableTestComponent = NetworkManagerHelper.AddComponentToObject<NetworkVariableTestComponent>(gameObjectId);

            NetworkManagerHelper.SpawnNetworkObject(gameObjectId);

            // Start Testing
            networkVariableTestComponent.EnableTesting = true;

            var testsAreComplete = networkVariableTestComponent.IsTestComplete();

            // Wait for the NetworkVariable tests to complete
            while (!testsAreComplete)
            {
                yield return new WaitForSeconds(0.003f);
                testsAreComplete = networkVariableTestComponent.IsTestComplete();
            }

            // Stop Testing
            networkVariableTestComponent.EnableTesting = false;

            Assert.IsTrue(networkVariableTestComponent.DidAllValuesChange());

            // Disable this once we are done.
            networkVariableTestComponent.gameObject.SetActive(false);

            Assert.IsTrue(testsAreComplete);

            // This would normally go in Teardown, but since every other test but this one
            //  uses MultiInstanceHelper, and it does its own NetworkManager setup / teardown,
            //  for now we put this within this one test until we migrate it to MIH
            NetworkManagerHelper.ShutdownNetworkManager();
        }

        [Test]
        public void ClientWritePermissionTest([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;

            // client must not be allowed to write to a server auth variable
            Assert.Throws<InvalidOperationException>(() => m_Player1OnClient1.TheScalar.Value = k_TestVal1);
        }

        [Test]
        public void ServerWritePermissionTest([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;

            if (!m_TestWithHost)
            {
                // server must not be allowed to write to a client auth variable
                Assert.Throws<InvalidOperationException>(() => m_Player1OnServer.ClientVar.Value = k_TestVal1);
            }
            else
            {
                Assert.AreEqual(m_Player1OnServer.ClientVar.Value, k_TestVal1);
            }
        }

        [UnityTest]
        public IEnumerator ClientTest([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;

            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () =>
                {
                    m_Player1OnClient1.ClientVar.Value = k_TestVal2;
                    m_Player2OnClient2.ClientVar.Value = k_TestVal3;
                },
                () =>
                {
                    // the client's values should win on the objects it owns
                    return
                        m_Player1OnServer.ClientVar.Value == k_TestVal2 &&
                        m_Player2OnServer.ClientVar.Value == k_TestVal3 &&
                        m_Player1OnClient1.ClientVar.Value == k_TestVal2 &&
                        m_Player2OnClient2.ClientVar.Value == k_TestVal3;
                }
            );
        }

        [UnityTest]
        public IEnumerator FixedString32StructTest([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;
            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () =>
                {
                    var tmp = m_Player1OnServer.FixedStringStruct.Value;
                    tmp.FixedString = k_FixedStringTestValue;
                    m_Player1OnServer.FixedStringStruct.Value = tmp;

                    // we are writing to the private and public variables on player 1's object...
                },
                () =>
                {
                    var tmp = m_Player1OnClient1.FixedStringStruct.Value;

                    // ...and we should see the writes to the private var only on the server & the owner,
                    //  but the public variable everywhere
                    return
                        m_Player1OnClient1.FixedStringStruct.Value.FixedString == k_FixedStringTestValue;
                }
            );
        }

        [UnityTest]
        public IEnumerator PrivateClientTest([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;

            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () =>
                {
                    // we are writing to the private and public variables on player 1's object...
                    m_Player1OnClient1.ClientVarPrivate.Value = k_TestVal1;
                    m_Player1OnClient1.ClientVar.Value = k_TestVal2;
                },
                () =>
                {
                    // ...and we should see the writes to the private var only on the server & the owner,
                    //  but the public variable everywhere
                    return
                        m_Player1OnClient2.ClientVarPrivate.Value != k_TestVal1 &&
                        m_Player1OnClient1.ClientVarPrivate.Value == k_TestVal1 &&
                        m_Player1OnClient2.ClientVar.Value != k_TestVal2 &&
                        m_Player1OnClient1.ClientVar.Value == k_TestVal2 &&
                        m_Player1OnServer.ClientVarPrivate.Value == k_TestVal1 &&
                        m_Player1OnServer.ClientVar.Value == k_TestVal2;
                }
            );
        }

        [UnityTest]
        public IEnumerator NetworkListAdd([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;
            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () =>
                {
                    m_Player1OnServer.TheList.Add(k_TestVal1);
                    m_Player1OnServer.TheList.Add(k_TestVal2);
                },
                () =>
                {
                    return m_Player1OnServer.TheList.Count == 2 &&
                        m_Player1OnClient1.TheList.Count == 2 &&
                        m_Player1OnServer.ListDelegateTriggered &&
                        m_Player1OnClient1.ListDelegateTriggered &&
                        m_Player1OnServer.TheList[0] == k_TestVal1 &&
                        m_Player1OnClient1.TheList[0] == k_TestVal1 &&
                        m_Player1OnServer.TheList[1] == k_TestVal2 &&
                        m_Player1OnClient1.TheList[1] == k_TestVal2;
                }
            );
        }

        [UnityTest]
        public IEnumerator NetworkListRemove([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;
            // first put some stuff in; re-use the add test
            yield return NetworkListAdd(useHost);

            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () => m_Player1OnServer.TheList.RemoveAt(0),
                () =>
                {
                    return m_Player1OnServer.TheList.Count == 1 &&
                           m_Player1OnClient1.TheList.Count == 1 &&
                           m_Player1OnServer.ListDelegateTriggered &&
                           m_Player1OnClient1.ListDelegateTriggered &&
                           m_Player1OnServer.TheList[0] == k_TestVal2 &&
                           m_Player1OnClient1.TheList[0] == k_TestVal2;
                }
            );
        }

        [UnityTest]
        public IEnumerator NetworkListClear([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;

            // first put some stuff in; re-use the add test
            yield return NetworkListAdd(useHost);

            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () => m_Player1OnServer.TheList.Clear(),
                () =>
                {
                    return
                        m_Player1OnServer.ListDelegateTriggered &&
                        m_Player1OnClient1.ListDelegateTriggered &&
                        m_Player1OnServer.TheList.Count == 0 &&
                        m_Player1OnClient1.TheList.Count == 0;
                }
            );
        }

        [UnityTest]
        public IEnumerator NetworkSetAdd([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;

            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () =>
                {
                    m_Player1OnServer.TheSet.Add(k_TestVal1);
                    m_Player1OnServer.TheSet.Add(k_TestVal2);
                },
                () =>
                {
                    return m_Player1OnServer.TheSet.Count == 2 &&
                           m_Player1OnClient1.TheSet.Count == 2 &&
                           m_Player1OnServer.SetDelegateTriggered &&
                           m_Player1OnClient1.SetDelegateTriggered &&
                           m_Player1OnServer.TheSet.Contains(k_TestVal1) &&
                           m_Player1OnClient1.TheSet.Contains(k_TestVal1) &&
                           m_Player1OnServer.TheSet.Contains(k_TestVal2) &&
                           m_Player1OnClient1.TheSet.Contains(k_TestVal2);
                }
            );
        }

        [UnityTest]
        public IEnumerator NetworkSetRemove([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;

            // first put some stuff in; re-use the add test
            yield return NetworkSetAdd(useHost);

            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () =>
                {
                    m_Player1OnServer.TheSet.Remove(k_TestVal1);
                },
                () =>
                {
                    return m_Player1OnServer.TheSet.Count == 1 &&
                           m_Player1OnClient1.TheSet.Count == 1 &&
                           m_Player1OnServer.SetDelegateTriggered &&
                           m_Player1OnClient1.SetDelegateTriggered &&
                           m_Player1OnServer.TheSet.Contains(k_TestVal2) &&
                           m_Player1OnClient1.TheSet.Contains(k_TestVal2);
                }
            );
        }

        [UnityTest]
        public IEnumerator NetworkSetClear([Values(true, false)] bool useHost)
        {
            // first put some stuff in; re-use the add test
            yield return NetworkSetAdd(useHost);

            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () =>
                {
                    m_Player1OnServer.TheSet.Clear();
                },
                () =>
                {
                    return m_Player1OnServer.TheSet.Count == 0 &&
                           m_Player1OnClient1.TheSet.Count == 0 &&
                           m_Player1OnServer.SetDelegateTriggered &&
                           m_Player1OnClient1.SetDelegateTriggered;
                }
            );
        }

        [UnityTest]
        public IEnumerator NetworkDictionaryAdd([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;

            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () =>
                {
                    m_Player1OnServer.TheDictionary.Add(k_TestKey1, k_TestVal1);
                    m_Player1OnServer.TheDictionary.Add(k_TestKey2, k_TestVal2);
                },
                () =>
                {
                    return m_Player1OnServer.TheDictionary.Count == 2 &&
                           m_Player1OnClient1.TheDictionary.Count == 2 &&
                           m_Player1OnServer.DictionaryDelegateTriggered &&
                           m_Player1OnClient1.DictionaryDelegateTriggered &&
                           m_Player1OnServer.TheDictionary[k_TestKey1] == k_TestVal1 &&
                           m_Player1OnClient1.TheDictionary[k_TestKey1] == k_TestVal1 &&
                           m_Player1OnServer.TheDictionary[k_TestKey2] == k_TestVal2 &&
                           m_Player1OnClient1.TheDictionary[k_TestKey2] == k_TestVal2;
                }
            );
        }

        /* Note, not adding coverage for RemovePair, because we plan to remove
         *  this in the next PR
         */
        [UnityTest]
        public IEnumerator NetworkDictionaryRemoveByKey([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;

            // first put some stuff in; re-use the add test
            yield return NetworkDictionaryAdd(useHost);

            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () =>
                {
                    m_Player1OnServer.TheDictionary.Remove(k_TestKey2);
                },
                () =>
                {
                    return m_Player1OnServer.TheDictionary.Count == 1 &&
                           m_Player1OnClient1.TheDictionary.Count == 1 &&
                           m_Player1OnServer.DictionaryDelegateTriggered &&
                           m_Player1OnClient1.DictionaryDelegateTriggered &&
                           m_Player1OnServer.TheDictionary[k_TestKey1] == k_TestVal1 &&
                           m_Player1OnClient1.TheDictionary[k_TestKey1] == k_TestVal1;
                }
            );
        }

        [UnityTest]
        public IEnumerator NetworkDictionaryChangeValue([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;

            // first put some stuff in; re-use the add test
            yield return NetworkDictionaryAdd(useHost);

            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () =>
                {
                    m_Player1OnServer.TheDictionary[k_TestKey1] = k_TestVal3;
                },
                () =>
                {
                    return m_Player1OnServer.TheDictionary.Count == 2 &&
                           m_Player1OnClient1.TheDictionary.Count == 2 &&
                           m_Player1OnServer.DictionaryDelegateTriggered &&
                           m_Player1OnClient1.DictionaryDelegateTriggered &&
                           m_Player1OnServer.TheDictionary[k_TestKey1] == k_TestVal3 &&
                           m_Player1OnClient1.TheDictionary[k_TestKey1] == k_TestVal3;
                }
            );
        }

        [UnityTest]
        public IEnumerator NetworkDictionaryClear([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;

            // first put some stuff in; re-use the add test
            yield return NetworkDictionaryAdd(useHost);

            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () =>
                {
                    m_Player1OnServer.TheDictionary.Clear();
                },
                () =>
                {
                    return m_Player1OnServer.TheDictionary.Count == 0 &&
                           m_Player1OnClient1.TheDictionary.Count == 0 &&
                           m_Player1OnServer.DictionaryDelegateTriggered &&
                           m_Player1OnClient1.DictionaryDelegateTriggered;
                }
            );
        }

        [UnityTest]
        public IEnumerator TestNetworkVariableStruct([Values(true, false)] bool useHost)
        {
            m_TestWithHost = useHost;
            yield return MultiInstanceHelpers.RunAndWaitForCondition(
                () =>
                {
                    m_Player1OnServer.TheStruct.Value =
                        new TestStruct() { SomeInt = k_TestUInt, SomeBool = false };
                    m_Player1OnServer.TheStruct.SetDirty(true);
                },
                () =>
                {
                    return
                        m_Player1OnClient1.TheStruct.Value.SomeBool == false &&
                        m_Player1OnClient1.TheStruct.Value.SomeInt == k_TestUInt;
                }
            );
        }

        [UnityTearDown]
        public override IEnumerator Teardown()
        {
            yield return base.Teardown();
        }
    }
}
