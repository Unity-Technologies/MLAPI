using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Netcode.EditorTests
{
    internal class NetworkBehaviourTests
    {
        [Test]
        public void HasNetworkObjectTest()
        {
            var gameObject = new GameObject(nameof(HasNetworkObjectTest));
            var networkBehaviour = gameObject.AddComponent<EmptyNetworkBehaviour>();

            Assert.That(networkBehaviour.HasNetworkObject, Is.False);

            var networkObject = gameObject.AddComponent<NetworkObject>();

            Assert.That(networkBehaviour.HasNetworkObject, Is.True);

            Object.DestroyImmediate(networkObject);

            Assert.That(networkBehaviour.HasNetworkObject, Is.False);

            // Cleanup
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void AccessNetworkObjectTest()
        {
            var gameObject = new GameObject(nameof(AccessNetworkObjectTest));
            var networkBehaviour = gameObject.AddComponent<EmptyNetworkBehaviour>();

            Assert.That(networkBehaviour.NetworkObject, Is.Null);

            var networkObject = gameObject.AddComponent<NetworkObject>();

            Assert.That(networkBehaviour.NetworkObject, Is.EqualTo(networkObject));

            Object.DestroyImmediate(networkObject);

            Assert.That(networkBehaviour.NetworkObject, Is.Null);

            // Cleanup
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void GivenClassDerivesFromNetworkBehaviour_GetTypeNameReturnsCorrectValue()
        {
            var gameObject = new GameObject(nameof(GivenClassDerivesFromNetworkBehaviour_GetTypeNameReturnsCorrectValue));
            var networkBehaviour = gameObject.AddComponent<EmptyNetworkBehaviour>();

            Assert.AreEqual(nameof(EmptyNetworkBehaviour), networkBehaviour.__getTypeName());
        }

        [Test]
        public void GivenClassDerivesFromNetworkBehaviourDerivedClass_GetTypeNameReturnsCorrectValue()
        {
            var gameObject = new GameObject(nameof(GivenClassDerivesFromNetworkBehaviourDerivedClass_GetTypeNameReturnsCorrectValue));
            var networkBehaviour = gameObject.AddComponent<DerivedNetworkBehaviour>();

            Assert.AreEqual(nameof(DerivedNetworkBehaviour), networkBehaviour.__getTypeName());
        }

        // Note: in order to repro https://github.com/Unity-Technologies/com.unity.netcode.gameobjects/issues/1078
        // this child class must be defined before its parent to assure it is processed first by ILPP
        internal class DerivedNetworkBehaviour : EmptyNetworkBehaviour
        {

        }

        internal class EmptyNetworkBehaviour : NetworkBehaviour
        {

        }
    }
}
