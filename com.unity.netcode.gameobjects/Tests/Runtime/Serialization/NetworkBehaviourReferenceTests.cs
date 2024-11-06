using System;
using System.Collections;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// Unit tests to test:
    /// - Serializing NetworkObject to NetworkObjectReference
    /// - Deserializing NetworkObjectReference to NetworkObject
    /// - Implicit operators of NetworkObjectReference
    /// </summary>
    internal class NetworkBehaviourReferenceTests : IDisposable
    {
        private class TestNetworkBehaviour : NetworkBehaviour
        {
            public static bool ReceivedRPC;

            public NetworkVariable<NetworkBehaviourReference> TestVariable = new NetworkVariable<NetworkBehaviourReference>();

            public TestNetworkBehaviour RpcReceivedBehaviour;

            [ServerRpc]
            public void SendReferenceServerRpc(NetworkBehaviourReference value)
            {
                RpcReceivedBehaviour = (TestNetworkBehaviour)value;
                ReceivedRPC = true;
            }
        }

        [UnityTest]
        public IEnumerator TestRpc()
        {
            using var networkObjectContext = UnityObjectContext.CreateNetworkObject();
            var testNetworkBehaviour = networkObjectContext.Object.gameObject.AddComponent<TestNetworkBehaviour>();
            networkObjectContext.Object.Spawn();

            using var otherObjectContext = UnityObjectContext.CreateNetworkObject();
            otherObjectContext.Object.Spawn();

            testNetworkBehaviour.SendReferenceServerRpc(new NetworkBehaviourReference(testNetworkBehaviour));

            // wait for rpc completion
            float t = 0;
            while (testNetworkBehaviour.RpcReceivedBehaviour == null)
            {
                t += Time.deltaTime;
                if (t > 5f)
                {
                    new AssertionException("RPC with NetworkBehaviour reference hasn't been received");
                }

                yield return null;
            }

            // validate
            Assert.AreEqual(testNetworkBehaviour, testNetworkBehaviour.RpcReceivedBehaviour);
        }

        [UnityTest]
        public IEnumerator TestSerializeNull([Values] bool initializeWithNull)
        {
            TestNetworkBehaviour.ReceivedRPC = false;
            using var networkObjectContext = UnityObjectContext.CreateNetworkObject();
            var testNetworkBehaviour = networkObjectContext.Object.gameObject.AddComponent<TestNetworkBehaviour>();
            networkObjectContext.Object.Spawn();

            using var otherObjectContext = UnityObjectContext.CreateNetworkObject();
            otherObjectContext.Object.Spawn();

            // If not initializing with null, then use the default constructor with no assigned NetworkBehaviour
            if (!initializeWithNull)
            {
                testNetworkBehaviour.SendReferenceServerRpc(new NetworkBehaviourReference());
            }
            else // Otherwise, initialize and pass in null as the reference
            {
                testNetworkBehaviour.SendReferenceServerRpc(new NetworkBehaviourReference(null));
            }

            // wait for rpc completion
            float t = 0;
            while (!TestNetworkBehaviour.ReceivedRPC)
            {
                t += Time.deltaTime;
                if (t > 5f)
                {
                    new AssertionException("RPC with NetworkBehaviour reference hasn't been received");
                }

                yield return null;
            }

            // validate
            Assert.AreEqual(null, testNetworkBehaviour.RpcReceivedBehaviour);
        }

        [UnityTest]
        public IEnumerator TestRpcImplicitNetworkBehaviour()
        {
            using var networkObjectContext = UnityObjectContext.CreateNetworkObject();
            var testNetworkBehaviour = networkObjectContext.Object.gameObject.AddComponent<TestNetworkBehaviour>();
            networkObjectContext.Object.Spawn();

            using var otherObjectContext = UnityObjectContext.CreateNetworkObject();
            otherObjectContext.Object.Spawn();

            testNetworkBehaviour.SendReferenceServerRpc(testNetworkBehaviour);

            // wait for rpc completion
            float t = 0;
            while (testNetworkBehaviour.RpcReceivedBehaviour == null)
            {
                t += Time.deltaTime;
                if (t > 5f)
                {
                    new AssertionException("RPC with NetworkBehaviour reference hasn't been received");
                }

                yield return null;
            }

            // validate
            Assert.AreEqual(testNetworkBehaviour, testNetworkBehaviour.RpcReceivedBehaviour);
        }

        [Test]
        public void TestNetworkVariable()
        {
            using var networkObjectContext = UnityObjectContext.CreateNetworkObject();
            var testNetworkBehaviour = networkObjectContext.Object.gameObject.AddComponent<TestNetworkBehaviour>();
            networkObjectContext.Object.Spawn();

            using var otherObjectContext = UnityObjectContext.CreateNetworkObject();
            otherObjectContext.Object.Spawn();

            // check default value is null
            Assert.IsNull((NetworkBehaviour)testNetworkBehaviour.TestVariable.Value);

            testNetworkBehaviour.TestVariable.Value = testNetworkBehaviour;

            Assert.AreEqual((NetworkBehaviour)testNetworkBehaviour.TestVariable.Value, testNetworkBehaviour);
        }

        [Test]
        public void FailSerializeNonSpawnedNetworkObject()
        {
            using var networkObjectContext = UnityObjectContext.CreateNetworkObject();
            var component = networkObjectContext.Object.gameObject.AddComponent<TestNetworkBehaviour>();

            Assert.Throws<ArgumentException>(() =>
            {
                NetworkBehaviourReference outReference = component;
            });
        }

        [Test]
        public void FailSerializeGameObjectWithoutNetworkObject()
        {
            using var gameObjectContext = UnityObjectContext.CreateGameObject();
            var component = gameObjectContext.Object.gameObject.AddComponent<TestNetworkBehaviour>();

            Assert.Throws<ArgumentException>(() =>
            {
                NetworkBehaviourReference outReference = component;
            });
        }

        public void Dispose()
        {
            //Stop, shutdown, and destroy
            NetworkManagerHelper.ShutdownNetworkManager();
        }

        public NetworkBehaviourReferenceTests()
        {
            //Create, instantiate, and host
            NetworkManagerHelper.StartNetworkManager(out _);
        }
    }

    /// <summary>
    /// Integration tests for NetworkBehaviourReference
    /// </summary>
    internal class NetworkBehaviourReferenceIntegrationTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;

        internal class FakeMissingComponent : NetworkBehaviour
        {

        }

        internal class TestAddedComponent : NetworkBehaviour
        {

        }

        protected override void OnCreatePlayerPrefab()
        {
            m_PlayerPrefab.AddComponent<TestAddedComponent>();
            base.OnCreatePlayerPrefab();
        }

        /// <summary>
        /// This test validates that if a component does not exist the NetworkBehaviourReference will not throw an
        /// invalid cast exception.
        /// (It is a full integration test to assure the NetworkObjects are spawned)
        /// </summary>
        [UnityTest]
        public IEnumerator TestTryGetWithAndWithOutExistingComponent()
        {
            var networkBehaviourReference = new NetworkBehaviourReference(m_ClientNetworkManagers[0].LocalClient.PlayerObject.GetComponent<TestAddedComponent>());
            var missingComponent = (FakeMissingComponent)null;
            var testBehaviour = (TestAddedComponent)null;
            Assert.IsFalse(networkBehaviourReference.TryGet(out missingComponent));
            Assert.IsTrue(networkBehaviourReference.TryGet(out testBehaviour));
            yield return null;
        }
    }
}
