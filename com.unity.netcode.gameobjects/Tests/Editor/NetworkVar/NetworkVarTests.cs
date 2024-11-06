using NUnit.Framework;
using UnityEngine;

namespace Unity.Netcode.EditorTests.NetworkVar
{
    internal class NetworkVarTests
    {
        internal class NetworkVarComponent : NetworkBehaviour
        {
            public NetworkVariable<int> NetworkVariable = new NetworkVariable<int>();
        }
        [Test]
        public void TestAssignmentUnchanged()
        {
            var gameObjectMan = new GameObject();
            var networkManager = gameObjectMan.AddComponent<NetworkManager>();
            networkManager.BehaviourUpdater = new NetworkBehaviourUpdater();
            var gameObject = new GameObject();
            var networkObject = gameObject.AddComponent<NetworkObject>();
            networkObject.NetworkManagerOwner = networkManager;
            var networkVarComponent = gameObject.AddComponent<NetworkVarComponent>();
            networkVarComponent.NetworkVariable.Initialize(networkVarComponent);
            networkVarComponent.NetworkVariable.Value = 314159265;
            networkVarComponent.NetworkVariable.OnValueChanged += (value, newValue) =>
            {
                Assert.Fail("OnValueChanged was invoked when setting the same value");
            };
            networkVarComponent.NetworkVariable.Value = 314159265;
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(gameObjectMan);
        }
        [Test]
        public void TestAssignmentChanged()
        {
            var gameObjectMan = new GameObject();
            var networkManager = gameObjectMan.AddComponent<NetworkManager>();
            networkManager.BehaviourUpdater = new NetworkBehaviourUpdater();
            var gameObject = new GameObject();
            var networkObject = gameObject.AddComponent<NetworkObject>();
            var networkVarComponent = gameObject.AddComponent<NetworkVarComponent>();
            networkObject.NetworkManagerOwner = networkManager;
            networkVarComponent.NetworkVariable.Initialize(networkVarComponent);
            networkVarComponent.NetworkVariable.Value = 314159265;
            var changed = false;
            networkVarComponent.NetworkVariable.OnValueChanged += (value, newValue) =>
            {
                changed = true;
            };
            networkVarComponent.NetworkVariable.Value = 314159266;
            Assert.True(changed);
            Object.DestroyImmediate(gameObject);
            Object.DestroyImmediate(gameObjectMan);
        }
    }
}
