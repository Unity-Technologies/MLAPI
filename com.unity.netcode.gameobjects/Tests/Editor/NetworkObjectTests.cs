using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Netcode.Editor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.EditorTests
{
    internal class NetworkObjectTests
    {
        [Test]
        public void NetworkManagerOverrideTest()
        {
            // Create "bait"
            var singletonNetworkManager = new GameObject(nameof(NetworkManager)).AddComponent<NetworkManager>();
            singletonNetworkManager.SetSingleton();

            // Create override
            var networkManager = new GameObject(nameof(NetworkManager)).AddComponent<NetworkManager>();

            // NetworkObject
            var gameObject = new GameObject(nameof(NetworkManagerOverrideTest));
            var networkObject = gameObject.AddComponent<NetworkObject>();

            // Set override
            networkObject.NetworkManagerOwner = networkManager;

            Debug.Assert(networkObject.NetworkManager == networkManager);

            Object.DestroyImmediate(singletonNetworkManager.gameObject);
            Object.DestroyImmediate(networkManager.gameObject);
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        [TestCase(0)]
        [TestCase(1)]
        [TestCase(2)]
        public void GetBehaviourIndexNone(int index)
        {
            var gameObject = new GameObject(nameof(GetBehaviourIndexNone));
            var networkObject = gameObject.AddComponent<NetworkObject>();

            LogAssert.Expect(LogType.Error, new Regex(".*out of bounds.*"));

            Assert.That(networkObject.GetNetworkBehaviourAtOrderIndex((ushort)index), Is.Null);

            // Cleanup
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void GetBehaviourIndexOne()
        {
            var gameObject = new GameObject(nameof(GetBehaviourIndexOne));
            var networkObject = gameObject.AddComponent<NetworkObject>();
            var networkBehaviour = gameObject.AddComponent<EmptyNetworkBehaviour>();

            LogAssert.Expect(LogType.Error, new Regex(".*out of bounds.*"));

            Assert.That(networkObject.GetNetworkBehaviourAtOrderIndex(0), Is.EqualTo(networkBehaviour));
            Assert.That(networkObject.GetNetworkBehaviourAtOrderIndex(1), Is.Null);

            // Cleanup
            Object.DestroyImmediate(gameObject);
        }

        /// <summary>
        /// Verifies that a NetworkObject component that is positioned after a NetworkBehaviour component will
        /// be migrated to a component index value that is before the lowest NetworkBehaviour component index value.
        /// (The lowest NetworkBehaviour component's index value will also change when this happens)
        /// </summary>
        [Test]
        public void NetworkObjectComponentOrder()
        {
            var gameObject = new GameObject(nameof(GetBehaviourIndexOne));
            // Add the Networkbehaviour first
            var networkBehaviour = gameObject.AddComponent<EmptyNetworkBehaviour>();
            // Add an empty MonoBehaviour inbetween the NetworkBehaviour and NetworkObject
            gameObject.AddComponent<EmptyMonoBehaviour>();
            // Add the NetworkObject
            var networkObject = gameObject.AddComponent<NetworkObject>();
            var componentIndices = GetIndices(gameObject);

            // Verify the NetworkObject procedes the NetworkBehaviour
            Assert.True(componentIndices.NetworkObjectIndex > componentIndices.NetworkBehaviourIndex, $"[Initial Setup] NetworkObject index ({componentIndices.NetworkObjectIndex}) is not greater than the NetworkBehaviour index ({componentIndices.NetworkBehaviourIndex})!");

            // Force-Invoke the CheckForNetworkObject method in order to verify the NetworkObject is moved
            NetworkBehaviourEditor.CheckForNetworkObject(gameObject);
            var adjustedIndices = GetIndices(gameObject);

            Assert.True(ValidateComponentIndices(componentIndices, GetIndices(gameObject)), "NetworkObject did not get migrated below the NetworkBehaviour!");

            // Cleanup
            Object.DestroyImmediate(gameObject);
        }

        private bool ValidateComponentIndices(ComponentIndices previous, ComponentIndices current)
        {
            if (previous.NetworkObjectIndex != current.NetworkObjectIndex && previous.NetworkBehaviourIndex != current.NetworkBehaviourIndex)
            {
                if (current.NetworkObjectIndex < previous.NetworkObjectIndex && current.NetworkObjectIndex < current.NetworkBehaviourIndex)
                {
                    return true;
                }
            }
            return false;
        }

        private ComponentIndices GetIndices(GameObject gameObject)
        {
            // Get the index/order values for the added NetworkBehaviour and NetworkObject
            var components = gameObject.GetComponents<MonoBehaviour>();
            var componentIndices = new ComponentIndices()
            {
                NetworkObjectIndex = -1,
                NetworkBehaviourIndex = -1
            };
            for (int i = 0; i < components.Length; i++)
            {
                if (componentIndices.NetworkObjectIndex != -1 && componentIndices.NetworkBehaviourIndex != -1)
                {
                    break;
                }
                var component = components[i];
                var networkObjectComponent = component as NetworkObject;
                if (networkObjectComponent != null)
                {
                    componentIndices.NetworkObjectIndex = i;
                    continue;
                }
                var networkBehaviourComponent = component as EmptyNetworkBehaviour;
                if (networkBehaviourComponent != null)
                {
                    componentIndices.NetworkBehaviourIndex = i;
                    continue;
                }
            }

            return componentIndices;
        }

        private struct ComponentIndices
        {
            public int NetworkObjectIndex;
            public int NetworkBehaviourIndex;
        }

        internal class EmptyNetworkBehaviour : NetworkBehaviour
        {

        }

        internal class EmptyMonoBehaviour : MonoBehaviour
        {

        }
    }
}
