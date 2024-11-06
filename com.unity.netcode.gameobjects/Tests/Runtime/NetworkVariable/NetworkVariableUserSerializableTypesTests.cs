using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Unity.Netcode.RuntimeTests
{
    internal struct MyTypeOne
    {
        public int Value;
    }
    internal struct MyTypeTwo
    {
        public int Value;
    }
    internal struct MyTypeThree
    {
        public int Value;
    }

    /// <summary>
    /// Used to help track instances of any child derived class
    /// </summary>
    internal class WorkingUserNetworkVariableComponentBase : NetworkBehaviour
    {
        private static Dictionary<ulong, WorkingUserNetworkVariableComponentBase> s_Instances = new Dictionary<ulong, WorkingUserNetworkVariableComponentBase>();

        internal static T GetRelativeInstance<T>(ulong clientId) where T : NetworkBehaviour
        {
            if (s_Instances.ContainsKey(clientId))
            {
                return s_Instances[clientId] as T;
            }
            return null;
        }

        public static void Reset()
        {
            s_Instances.Clear();
        }

        public override void OnNetworkSpawn()
        {
            if (!s_Instances.ContainsKey(NetworkManager.LocalClientId))
            {
                s_Instances.Add(NetworkManager.LocalClientId, this);
            }
            else
            {
                Debug.LogWarning($"{name} is spawned but client id {NetworkManager.LocalClientId} instance already exists!");
            }
        }

        public override void OnNetworkDespawn()
        {
            if (s_Instances.ContainsKey(NetworkManager.LocalClientId))
            {
                s_Instances.Remove(NetworkManager.LocalClientId);
            }
            else
            {
                Debug.LogWarning($"{name} is was never spawned but client id {NetworkManager.LocalClientId} is trying to despawn it!");
            }
        }
    }

    internal class WorkingUserNetworkVariableComponent : WorkingUserNetworkVariableComponentBase
    {
        public NetworkVariable<MyTypeOne> NetworkVariable = new NetworkVariable<MyTypeOne>();
    }

    internal class WorkingUserNetworkVariableComponentUsingExtensionMethod : WorkingUserNetworkVariableComponentBase
    {
        public NetworkVariable<MyTypeTwo> NetworkVariable = new NetworkVariable<MyTypeTwo>();
    }
    internal class NonWorkingUserNetworkVariableComponent : NetworkBehaviour
    {
        public NetworkVariable<MyTypeThree> NetworkVariable = new NetworkVariable<MyTypeThree>();
    }

    internal static class NetworkVariableUserSerializableTypesTestsExtensionMethods
    {
        public static void WriteValueSafe(this FastBufferWriter writer, in MyTypeTwo value)
        {
            writer.WriteValueSafe(value.Value);
        }

        public static void ReadValueSafe(this FastBufferReader reader, out MyTypeTwo value)
        {
            value = new MyTypeTwo();
            reader.ReadValueSafe(out value.Value);
        }
    }

    internal class NetworkVariableUserSerializableTypesTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;

        public NetworkVariableUserSerializableTypesTests()
            : base(HostOrServer.Server)
        {
        }

        private GameObject m_WorkingPrefab;
        private GameObject m_ExtensionMethodPrefab;
        private GameObject m_NonWorkingPrefab;

        protected override IEnumerator OnSetup()
        {
            WorkingUserNetworkVariableComponentBase.Reset();

            UserNetworkVariableSerialization<MyTypeOne>.WriteValue = null;
            UserNetworkVariableSerialization<MyTypeOne>.ReadValue = null;
            UserNetworkVariableSerialization<MyTypeOne>.DuplicateValue = null;
            UserNetworkVariableSerialization<MyTypeTwo>.WriteValue = null;
            UserNetworkVariableSerialization<MyTypeTwo>.ReadValue = null;
            UserNetworkVariableSerialization<MyTypeTwo>.DuplicateValue = null;
            UserNetworkVariableSerialization<MyTypeThree>.WriteValue = null;
            UserNetworkVariableSerialization<MyTypeThree>.ReadValue = null;
            UserNetworkVariableSerialization<MyTypeThree>.DuplicateValue = null;
            return base.OnSetup();
        }

        protected override void OnServerAndClientsCreated()
        {
            m_WorkingPrefab = CreateNetworkObjectPrefab($"[{nameof(NetworkVariableUserSerializableTypesTests)}.{nameof(m_WorkingPrefab)}]");
            m_ExtensionMethodPrefab = CreateNetworkObjectPrefab($"[{nameof(NetworkVariableUserSerializableTypesTests)}.{nameof(m_ExtensionMethodPrefab)}]");
            m_NonWorkingPrefab = CreateNetworkObjectPrefab($"[{nameof(NetworkVariableUserSerializableTypesTests)}.{nameof(m_NonWorkingPrefab)}]");
            m_WorkingPrefab.AddComponent<WorkingUserNetworkVariableComponent>();
            m_ExtensionMethodPrefab.AddComponent<WorkingUserNetworkVariableComponentUsingExtensionMethod>();
            m_NonWorkingPrefab.AddComponent<NonWorkingUserNetworkVariableComponent>();
        }

        private bool CheckForClientInstance<T>() where T : WorkingUserNetworkVariableComponentBase
        {
            var instance = WorkingUserNetworkVariableComponentBase.GetRelativeInstance<T>(m_ClientNetworkManagers[0].LocalClientId);
            return instance != null && instance.IsSpawned;
        }

        [UnityTest]
        public IEnumerator WhenUsingAUserSerializableNetworkVariableWithUserSerialization_ReplicationWorks()
        {
            UserNetworkVariableSerialization<MyTypeOne>.WriteValue = (FastBufferWriter writer, in MyTypeOne value) =>
            {
                writer.WriteValueSafe(value.Value);
            };
            UserNetworkVariableSerialization<MyTypeOne>.ReadValue = (FastBufferReader reader, out MyTypeOne value) =>
            {
                value = new MyTypeOne();
                reader.ReadValueSafe(out value.Value);
            };
            UserNetworkVariableSerialization<MyTypeOne>.DuplicateValue = (in MyTypeOne value, ref MyTypeOne duplicatedValue) =>
            {
                duplicatedValue = value;
            };

            var serverObject = SpawnObject(m_WorkingPrefab, m_ServerNetworkManager);
            var serverNetworkObject = serverObject.GetComponent<NetworkObject>();

            // Wait for the client instance to be spawned, which removes the need to check for two NetworkVariableDeltaMessages
            yield return WaitForConditionOrTimeOut(() => CheckForClientInstance<WorkingUserNetworkVariableComponent>());
            AssertOnTimeout($"Timed out waiting for the client side object to spawn!");

            // Get server and client instances of the test component
            var clientInstance = WorkingUserNetworkVariableComponentBase.GetRelativeInstance<WorkingUserNetworkVariableComponent>(m_ClientNetworkManagers[0].LocalClientId);
            var serverInstance = serverNetworkObject.GetComponent<WorkingUserNetworkVariableComponent>();

            // Set the server side value
            serverInstance.NetworkVariable.Value = new MyTypeOne { Value = 20 };

            // Wait for the NetworkVariableDeltaMessage
            yield return NetcodeIntegrationTestHelpers.WaitForMessageOfTypeReceived<NetworkVariableDeltaMessage>(m_ClientNetworkManagers[0]);

            // Wait for the client side value to be updated to the server side value (can take an additional frame)
            yield return WaitForConditionOrTimeOut(() => clientInstance.NetworkVariable.Value.Value == serverInstance.NetworkVariable.Value.Value);
            Assert.AreEqual(serverNetworkObject.GetComponent<WorkingUserNetworkVariableComponent>().NetworkVariable.Value.Value, clientInstance.NetworkVariable.Value.Value);
            Assert.AreEqual(20, clientInstance.NetworkVariable.Value.Value);
        }

        [UnityTest]
        public IEnumerator WhenUsingAUserSerializableNetworkVariableWithUserSerializationViaExtensionMethod_ReplicationWorks()
        {
            UserNetworkVariableSerialization<MyTypeTwo>.WriteValue = NetworkVariableUserSerializableTypesTestsExtensionMethods.WriteValueSafe;
            UserNetworkVariableSerialization<MyTypeTwo>.ReadValue = NetworkVariableUserSerializableTypesTestsExtensionMethods.ReadValueSafe;
            UserNetworkVariableSerialization<MyTypeTwo>.DuplicateValue = (in MyTypeTwo value, ref MyTypeTwo duplicatedValue) =>
            {
                duplicatedValue = value;
            };

            var serverObject = SpawnObject(m_ExtensionMethodPrefab, m_ServerNetworkManager);
            var serverNetworkObject = serverObject.GetComponent<NetworkObject>();

            // Wait for the client instance to be spawned, which removes the need to check for two NetworkVariableDeltaMessages
            yield return WaitForConditionOrTimeOut(() => CheckForClientInstance<WorkingUserNetworkVariableComponentUsingExtensionMethod>());
            AssertOnTimeout($"Timed out waiting for the client side object to spawn!");

            // Get server and client instances of the test component
            var clientInstance = WorkingUserNetworkVariableComponentBase.GetRelativeInstance<WorkingUserNetworkVariableComponentUsingExtensionMethod>(m_ClientNetworkManagers[0].LocalClientId);
            var serverInstance = serverNetworkObject.GetComponent<WorkingUserNetworkVariableComponentUsingExtensionMethod>();
            // Set the server side value
            serverInstance.NetworkVariable.Value = new MyTypeTwo { Value = 20 };

            // Wait for the NetworkVariableDeltaMessage
            yield return NetcodeIntegrationTestHelpers.WaitForMessageOfTypeReceived<NetworkVariableDeltaMessage>(m_ClientNetworkManagers[0]);

            // Wait for the client side value to be updated to the server side value (can take an additional frame)
            yield return WaitForConditionOrTimeOut(() => clientInstance.NetworkVariable.Value.Value == serverInstance.NetworkVariable.Value.Value);
            AssertOnTimeout($"Timed out waiting for the client side object's value ({clientInstance.NetworkVariable.Value.Value}) to equal the server side objects value ({serverInstance.NetworkVariable.Value.Value})!");
            Assert.AreEqual(serverInstance.NetworkVariable.Value.Value, clientInstance.NetworkVariable.Value.Value);
            Assert.AreEqual(20, clientInstance.NetworkVariable.Value.Value);
        }

        [Test]
        public void WhenUsingAUserSerializableNetworkVariableWithoutUserSerialization_ReplicationFails()
        {
            var serverObject = Object.Instantiate(m_NonWorkingPrefab);
            var serverNetworkObject = serverObject.GetComponent<NetworkObject>();
            serverNetworkObject.NetworkManagerOwner = m_ServerNetworkManager;
            Assert.Throws<ArgumentException>(
                () =>
                {
                    serverNetworkObject.Spawn();
                }
            );
        }

        protected override IEnumerator OnTearDown()
        {
            // These have to get set to SOMETHING, otherwise we will get an exception thrown because Object.Destroy()
            // calls __initializeNetworkVariables, and the network variable initialization attempts to call FallbackSerializer<T>,
            // which throws an exception if any of these values are null. They don't have to DO anything, they just have to
            // be non-null to keep the test from failing during teardown.
            // None of this is related to what's being tested above, and in reality, these values being null is an invalid
            // use case. But one of the tests is explicitly testing that invalid use case, and the values are being set
            // to null in OnSetup to ensure test isolation. This wouldn't be a situation a user would have to think about
            // in a real world use case.
            UserNetworkVariableSerialization<MyTypeOne>.WriteValue = (FastBufferWriter writer, in MyTypeOne value) => { };
            UserNetworkVariableSerialization<MyTypeOne>.ReadValue = (FastBufferReader reader, out MyTypeOne value) => { value = new MyTypeOne(); };
            UserNetworkVariableSerialization<MyTypeOne>.DuplicateValue = (in MyTypeOne value, ref MyTypeOne duplicatedValue) =>
            {
                duplicatedValue = value;
            };
            UserNetworkVariableSerialization<MyTypeTwo>.WriteValue = (FastBufferWriter writer, in MyTypeTwo value) => { };
            UserNetworkVariableSerialization<MyTypeTwo>.ReadValue = (FastBufferReader reader, out MyTypeTwo value) => { value = new MyTypeTwo(); };
            UserNetworkVariableSerialization<MyTypeTwo>.DuplicateValue = (in MyTypeTwo value, ref MyTypeTwo duplicatedValue) =>
            {
                duplicatedValue = value;
            };
            UserNetworkVariableSerialization<MyTypeThree>.WriteValue = (FastBufferWriter writer, in MyTypeThree value) => { };
            UserNetworkVariableSerialization<MyTypeThree>.ReadValue = (FastBufferReader reader, out MyTypeThree value) => { value = new MyTypeThree(); };
            UserNetworkVariableSerialization<MyTypeThree>.DuplicateValue = (in MyTypeThree value, ref MyTypeThree duplicatedValue) =>
            {
                duplicatedValue = value;
            };
            return base.OnTearDown();
        }
    }
}

