#if !NGO_MINIMALPROJECT
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    [TestFixtureSource(nameof(TestDataSource))]
    internal class NetworkVariableInheritanceTests : NetcodeIntegrationTest
    {
        public NetworkVariableInheritanceTests(HostOrServer hostOrServer)
            : base(hostOrServer)
        {
        }

        protected override int NumberOfClients => 2;

        public static IEnumerable<TestFixtureData> TestDataSource() =>
            Enum.GetValues(typeof(HostOrServer)).OfType<HostOrServer>().Select(x => new TestFixtureData(x));

        internal class ComponentA : NetworkBehaviour
        {
            public NetworkVariable<int> PublicFieldA = new NetworkVariable<int>(1);
            protected NetworkVariable<int> m_ProtectedFieldA = new NetworkVariable<int>(2);
            private NetworkVariable<int> m_PrivateFieldA = new NetworkVariable<int>(3);

            public void ChangeValuesA(int pub, int pro, int pri)
            {
                PublicFieldA.Value = pub;
                m_ProtectedFieldA.Value = pro;
                m_PrivateFieldA.Value = pri;
            }

            public bool CompareValuesA(ComponentA other)
            {
                return PublicFieldA.Value == other.PublicFieldA.Value &&
                    m_ProtectedFieldA.Value == other.m_ProtectedFieldA.Value &&
                    m_PrivateFieldA.Value == other.m_PrivateFieldA.Value;
            }
        }

        internal class ComponentB : ComponentA
        {
            public NetworkVariable<int> PublicFieldB = new NetworkVariable<int>(11);
            protected NetworkVariable<int> m_ProtectedFieldB = new NetworkVariable<int>(22);
            private NetworkVariable<int> m_PrivateFieldB = new NetworkVariable<int>(33);

            public void ChangeValuesB(int pub, int pro, int pri)
            {
                PublicFieldB.Value = pub;
                m_ProtectedFieldB.Value = pro;
                m_PrivateFieldB.Value = pri;
            }

            public bool CompareValuesB(ComponentB other)
            {
                return PublicFieldB.Value == other.PublicFieldB.Value &&
                    m_ProtectedFieldB.Value == other.m_ProtectedFieldB.Value &&
                    m_PrivateFieldB.Value == other.m_PrivateFieldB.Value;
            }
        }

        internal class ComponentC : ComponentB
        {
            public NetworkVariable<int> PublicFieldC = new NetworkVariable<int>(111);
            protected NetworkVariable<int> m_ProtectedFieldC = new NetworkVariable<int>(222);
            private NetworkVariable<int> m_PrivateFieldC = new NetworkVariable<int>(333);

            public void ChangeValuesC(int pub, int pro, int pri)
            {
                PublicFieldC.Value = pub;
                m_ProtectedFieldA.Value = pro;
                m_PrivateFieldC.Value = pri;
            }

            public bool CompareValuesC(ComponentC other)
            {
                return PublicFieldC.Value == other.PublicFieldC.Value &&
                    m_ProtectedFieldC.Value == other.m_ProtectedFieldC.Value &&
                    m_PrivateFieldC.Value == other.m_PrivateFieldC.Value;
            }
        }

        private GameObject m_TestObjectPrefab;
        private ulong m_TestObjectId = 0;

        protected override void OnOneTimeSetup()
        {
            NetworkVariableBase.IgnoreInitializeWarning = true;
            base.OnOneTimeSetup();
        }

        protected override void OnOneTimeTearDown()
        {
            NetworkVariableBase.IgnoreInitializeWarning = false;
            base.OnOneTimeTearDown();
        }

        protected override void OnServerAndClientsCreated()
        {
            m_TestObjectPrefab = CreateNetworkObjectPrefab($"[{nameof(NetworkVariableInheritanceTests)}.{nameof(m_TestObjectPrefab)}]");
            m_TestObjectPrefab.AddComponent<ComponentA>();
            m_TestObjectPrefab.AddComponent<ComponentB>();
            m_TestObjectPrefab.AddComponent<ComponentC>();
        }

        protected override IEnumerator OnServerAndClientsConnected()
        {
            var serverTestObject = SpawnObject(m_TestObjectPrefab, m_ServerNetworkManager).GetComponent<NetworkObject>();
            m_TestObjectId = serverTestObject.NetworkObjectId;

            var serverTestComponentA = serverTestObject.GetComponent<ComponentA>();
            var serverTestComponentB = serverTestObject.GetComponent<ComponentB>();
            var serverTestComponentC = serverTestObject.GetComponent<ComponentC>();

            serverTestComponentA.ChangeValuesA(1000, 2000, 3000);
            serverTestComponentB.ChangeValuesA(1000, 2000, 3000);
            serverTestComponentB.ChangeValuesB(1100, 2200, 3300);
            serverTestComponentC.ChangeValuesA(1000, 2000, 3000);
            serverTestComponentC.ChangeValuesB(1100, 2200, 3300);
            serverTestComponentC.ChangeValuesC(1110, 2220, 3330);

            yield return WaitForTicks(m_ServerNetworkManager, 2);
        }

        private bool CheckTestObjectComponentValuesOnAll()
        {
            var serverTestObject = m_ServerNetworkManager.SpawnManager.SpawnedObjects[m_TestObjectId];
            var serverTestComponentA = serverTestObject.GetComponent<ComponentA>();
            var serverTestComponentB = serverTestObject.GetComponent<ComponentB>();
            var serverTestComponentC = serverTestObject.GetComponent<ComponentC>();
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                var clientTestObject = clientNetworkManager.SpawnManager.SpawnedObjects[m_TestObjectId];
                var clientTestComponentA = clientTestObject.GetComponent<ComponentA>();
                var clientTestComponentB = clientTestObject.GetComponent<ComponentB>();
                var clientTestComponentC = clientTestObject.GetComponent<ComponentC>();
                if (!serverTestComponentA.CompareValuesA(clientTestComponentA) ||
                    !serverTestComponentB.CompareValuesA(clientTestComponentB) ||
                    !serverTestComponentB.CompareValuesB(clientTestComponentB) ||
                    !serverTestComponentC.CompareValuesA(clientTestComponentC) ||
                    !serverTestComponentC.CompareValuesB(clientTestComponentC) ||
                    !serverTestComponentC.CompareValuesC(clientTestComponentC))
                {
                    return false;
                }
            }

            return true;
        }

        [UnityTest]
        public IEnumerator TestInheritedFields()
        {
            yield return WaitForConditionOrTimeOut(CheckTestObjectComponentValuesOnAll);
            Assert.IsFalse(s_GlobalTimeoutHelper.TimedOut, nameof(CheckTestObjectComponentValuesOnAll));
        }
    }
}
#endif
