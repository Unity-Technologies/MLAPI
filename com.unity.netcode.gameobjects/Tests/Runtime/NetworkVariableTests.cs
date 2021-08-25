using System;
using System.Collections;
using UnityEngine;
using UnityEngine.TestTools;
using NUnit.Framework;

namespace Unity.Netcode.RuntimeTests
{
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

    public class TestClass : INetworkSerializable
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
        public readonly NetworkList<int> TheList = new NetworkList<int>(
            new NetworkVariableSettings { WritePermission = NetworkVariableWritePermission.ServerOnly }
        );

        public readonly NetworkSet<int> TheSet = new NetworkSet<int>(
            new NetworkVariableSettings { WritePermission = NetworkVariableWritePermission.ServerOnly }
        );

        public readonly NetworkDictionary<int, int> TheDictionary = new NetworkDictionary<int, int>(
            new NetworkVariableSettings { WritePermission = NetworkVariableWritePermission.ServerOnly }
        );

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
        public readonly NetworkVariable<TestClass> TheClass = new NetworkVariable<TestClass>(new TestClass());

        public bool ListDelegateTriggered;
        public bool SetDelegateTriggered;
        public bool DictionaryDelegateTriggered;
    }

    namespace Unity.Netcode.RuntimeTests
    {
        public class NetworkVariableTests : BaseMultiInstanceTest
        {
            protected override int NbClients => 1;

            private const uint k_TestUInt = 0xdeadbeef;

            private const int k_TestVal1 = 111;
            private const int k_TestVal2 = 222;
            private const int k_TestVal3 = 333;

            private const int k_TestKey1 = 0x0f0f;
            private const int k_TestKey2 = 0xf0f0;

            private NetworkVariableTest m_ServerComp;
            private NetworkVariableTest m_ClientComp;

            private readonly bool m_TestWithHost = false;

            [UnitySetUp]
            public override IEnumerator Setup()
            {
                yield return StartSomeClientsAndServerWithPlayers(useHost: m_TestWithHost, nbClients: NbClients,
                    updatePlayerPrefab: playerPrefab =>
                    {
                        var networkTransform = playerPrefab.AddComponent<NetworkVariableTest>();
                    });

                // This is the *SERVER VERSION* of the *CLIENT PLAYER*
                var serverClientPlayerResult = new MultiInstanceHelpers.CoroutineResultWrapper<NetworkObject>();

                yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation(
                    x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId,
                    m_ServerNetworkManager, serverClientPlayerResult));

                // This is the *CLIENT VERSION* of the *CLIENT PLAYER*
                var clientClientPlayerResult = new MultiInstanceHelpers.CoroutineResultWrapper<NetworkObject>();
                yield return MultiInstanceHelpers.Run(MultiInstanceHelpers.GetNetworkObjectByRepresentation(
                    x => x.IsPlayerObject && x.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId,
                    m_ClientNetworkManagers[0], clientClientPlayerResult));

                var serverSideClientPlayer = serverClientPlayerResult.Result;
                var clientSideClientPlayer = clientClientPlayerResult.Result;

                m_ServerComp = serverSideClientPlayer.GetComponent<NetworkVariableTest>();
                m_ClientComp = clientSideClientPlayer.GetComponent<NetworkVariableTest>();

                m_ServerComp.TheList.Clear();
                m_ServerComp.TheSet.Clear();
                m_ServerComp.TheDictionary.Clear();

                if (m_ServerComp.TheList.Count > 0 || m_ServerComp.TheSet.Count > 0 || m_ServerComp.TheDictionary.Count > 0)
                {
                    throw new Exception("at least one server network container not empty at start");
                }
                if (m_ClientComp.TheList.Count > 0 || m_ClientComp.TheSet.Count > 0 || m_ClientComp.TheDictionary.Count > 0)
                {
                    throw new Exception("at least one client network container not empty at start");
                }
            }

            /// <summary>
            /// Runs generalized tests on all predefined NetworkVariable types
            /// </summary>
            [UnityTest]
            public IEnumerator AllNetworkVariableTypes()
            {
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

            [UnityTest]
            public IEnumerator NetworkListAdd()
            {
                yield return MultiInstanceHelpers.RunAndWaitForCondition(
                    () =>
                    {
                        m_ServerComp.TheList.Add(k_TestVal1);
                        m_ServerComp.TheList.Add(k_TestVal2);
                    },
                    () =>
                    {
                        return m_ServerComp.TheList.Count == 2 &&
                            m_ClientComp.TheList.Count == 2 &&
                            m_ServerComp.ListDelegateTriggered &&
                            m_ClientComp.ListDelegateTriggered &&
                            m_ServerComp.TheList[0] == k_TestVal1 &&
                            m_ClientComp.TheList[0] == k_TestVal1 &&
                            m_ServerComp.TheList[1] == k_TestVal2 &&
                            m_ClientComp.TheList[1] == k_TestVal2;
                    }
                );
            }

            [UnityTest]
            public IEnumerator NetworkListRemove()
            {
                // first put some stuff in; re-use the add test
                yield return NetworkListAdd();

                yield return MultiInstanceHelpers.RunAndWaitForCondition(
                    () => m_ServerComp.TheList.RemoveAt(0),
                    () =>
                    {
                        return m_ServerComp.TheList.Count == 1 &&
                               m_ClientComp.TheList.Count == 1 &&
                               m_ServerComp.ListDelegateTriggered &&
                               m_ClientComp.ListDelegateTriggered &&
                               m_ServerComp.TheList[0] == k_TestVal2 &&
                               m_ClientComp.TheList[0] == k_TestVal2;
                    }
                );
            }

            [UnityTest]
            public IEnumerator NetworkListClear()
            {
                // first put some stuff in; re-use the add test
                yield return NetworkListAdd();

                yield return MultiInstanceHelpers.RunAndWaitForCondition(
                    () => m_ServerComp.TheList.Clear(),
                    () =>
                    {
                        return
                            m_ServerComp.ListDelegateTriggered &&
                            m_ClientComp.ListDelegateTriggered &&
                            m_ServerComp.TheList.Count == 0 &&
                            m_ClientComp.TheList.Count == 0;
                    }
                );
            }

            [UnityTest]
            public IEnumerator NetworkSetAdd()
            {
                yield return MultiInstanceHelpers.RunAndWaitForCondition(
                    () =>
                    {
                        m_ServerComp.TheSet.Add(k_TestVal1);
                        m_ServerComp.TheSet.Add(k_TestVal2);
                    },
                    () =>
                    {
                        return m_ServerComp.TheSet.Count == 2 &&
                               m_ClientComp.TheSet.Count == 2 &&
                               m_ServerComp.SetDelegateTriggered &&
                               m_ClientComp.SetDelegateTriggered &&
                               m_ServerComp.TheSet.Contains(k_TestVal1) &&
                               m_ClientComp.TheSet.Contains(k_TestVal1) &&
                               m_ServerComp.TheSet.Contains(k_TestVal2) &&
                               m_ClientComp.TheSet.Contains(k_TestVal2);
                    }
                );
            }

            [UnityTest]
            public IEnumerator NetworkSetRemove()
            {
                // first put some stuff in; re-use the add test
                yield return NetworkSetAdd();

                yield return MultiInstanceHelpers.RunAndWaitForCondition(
                    () =>
                    {
                        m_ServerComp.TheSet.Remove(k_TestVal1);
                    },
                    () =>
                    {
                        return m_ServerComp.TheSet.Count == 1 &&
                               m_ClientComp.TheSet.Count == 1 &&
                               m_ServerComp.SetDelegateTriggered &&
                               m_ClientComp.SetDelegateTriggered &&
                               m_ServerComp.TheSet.Contains(k_TestVal2) &&
                               m_ClientComp.TheSet.Contains(k_TestVal2);
                    }
                );
            }

            [UnityTest]
            public IEnumerator NetworkSetClear()
            {
                // first put some stuff in; re-use the add test
                yield return NetworkSetAdd();

                yield return MultiInstanceHelpers.RunAndWaitForCondition(
                    () =>
                    {
                        m_ServerComp.TheSet.Clear();
                    },
                    () =>
                    {
                        return m_ServerComp.TheSet.Count == 0 &&
                               m_ClientComp.TheSet.Count == 0 &&
                               m_ServerComp.SetDelegateTriggered &&
                               m_ClientComp.SetDelegateTriggered;
                    }
                );
            }

            [UnityTest]
            public IEnumerator NetworkDictionaryAdd()
            {
                yield return MultiInstanceHelpers.RunAndWaitForCondition(
                    () =>
                    {
                        m_ServerComp.TheDictionary.Add(k_TestKey1, k_TestVal1);
                        m_ServerComp.TheDictionary.Add(k_TestKey2, k_TestVal2);
                    },
                    () =>
                    {
                        return m_ServerComp.TheDictionary.Count == 2 &&
                               m_ClientComp.TheDictionary.Count == 2 &&
                               m_ServerComp.DictionaryDelegateTriggered &&
                               m_ClientComp.DictionaryDelegateTriggered &&
                               m_ServerComp.TheDictionary[k_TestKey1] == k_TestVal1 &&
                               m_ClientComp.TheDictionary[k_TestKey1] == k_TestVal1 &&
                               m_ServerComp.TheDictionary[k_TestKey2] == k_TestVal2 &&
                               m_ClientComp.TheDictionary[k_TestKey2] == k_TestVal2;
                    }
                );
            }

            /* Note, not adding coverage for RemovePair, because we plan to remove
             *  this in the next PR
             */
            [UnityTest]
            public IEnumerator NetworkDictionaryRemoveByKey()
            {
                // first put some stuff in; re-use the add test
                yield return NetworkDictionaryAdd();

                yield return MultiInstanceHelpers.RunAndWaitForCondition(
                    () =>
                    {
                        m_ServerComp.TheDictionary.Remove(k_TestKey2);
                    },
                    () =>
                    {
                        return m_ServerComp.TheDictionary.Count == 1 &&
                               m_ClientComp.TheDictionary.Count == 1 &&
                               m_ServerComp.DictionaryDelegateTriggered &&
                               m_ClientComp.DictionaryDelegateTriggered &&
                               m_ServerComp.TheDictionary[k_TestKey1] == k_TestVal1 &&
                               m_ClientComp.TheDictionary[k_TestKey1] == k_TestVal1;
                    }
                );
            }

            [UnityTest]
            public IEnumerator NetworkDictionaryChangeValue()
            {
                // first put some stuff in; re-use the add test
                yield return NetworkDictionaryAdd();

                yield return MultiInstanceHelpers.RunAndWaitForCondition(
                    () =>
                    {
                        m_ServerComp.TheDictionary[k_TestKey1] = k_TestVal3;
                    },
                    () =>
                    {
                        return m_ServerComp.TheDictionary.Count == 2 &&
                               m_ClientComp.TheDictionary.Count == 2 &&
                               m_ServerComp.DictionaryDelegateTriggered &&
                               m_ClientComp.DictionaryDelegateTriggered &&
                               m_ServerComp.TheDictionary[k_TestKey1] == k_TestVal3 &&
                               m_ClientComp.TheDictionary[k_TestKey1] == k_TestVal3;
                    }
                );
            }

            [UnityTest]
            public IEnumerator NetworkDictionaryClear()
            {
                // first put some stuff in; re-use the add test
                yield return NetworkDictionaryAdd();

                yield return MultiInstanceHelpers.RunAndWaitForCondition(
                    () =>
                    {
                        m_ServerComp.TheDictionary.Clear();
                    },
                    () =>
                    {
                        return m_ServerComp.TheDictionary.Count == 0 &&
                               m_ClientComp.TheDictionary.Count == 0 &&
                               m_ServerComp.DictionaryDelegateTriggered &&
                               m_ClientComp.DictionaryDelegateTriggered;
                    }
                );
            }

            [UnityTest]
            public IEnumerator TestNetworkVariableClass()
            {
                yield return MultiInstanceHelpers.RunAndWaitForCondition(
                    () =>
                    {
                        m_ServerComp.TheClass.Value.SomeBool = false;
                        m_ServerComp.TheClass.Value.SomeInt = k_TestUInt;
                        m_ServerComp.TheClass.SetDirty(true);
                    },
                    () =>
                    {
                        return
                            m_ClientComp.TheClass.Value.SomeBool == false &&
                            m_ClientComp.TheClass.Value.SomeInt == k_TestUInt;
                    }
                );
            }

            [UnityTest]
            public IEnumerator TestNetworkVariableStruct()
            {
                yield return MultiInstanceHelpers.RunAndWaitForCondition(
                    () =>
                    {
                        m_ServerComp.TheStruct.Value =
                            new TestStruct() { SomeInt = k_TestUInt, SomeBool = false };
                        m_ServerComp.TheStruct.SetDirty(true);
                    },
                    () =>
                    {
                        return
                            m_ClientComp.TheStruct.Value.SomeBool == false &&
                            m_ClientComp.TheStruct.Value.SomeInt == k_TestUInt;
                    }
                );
            }

            [UnityTearDown]
            public override IEnumerator Teardown()
            {
                yield return base.Teardown();
                UnityEngine.Object.Destroy(m_PlayerPrefab);
            }
        }
    }
}
