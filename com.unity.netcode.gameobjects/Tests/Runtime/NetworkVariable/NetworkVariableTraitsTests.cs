using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Netcode.RuntimeTests
{
    internal class NetworkVariableTraitsComponent : NetworkBehaviour
    {
        public NetworkVariable<float> TheVariable = new NetworkVariable<float>();
    }

    internal class NetworkVariableTraitsTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        protected override bool m_EnableTimeTravel => true;
        protected override bool m_SetupIsACoroutine => false;
        protected override bool m_TearDownIsACoroutine => false;

        protected override void OnPlayerPrefabGameObjectCreated()
        {
            m_PlayerPrefab.AddComponent<NetworkVariableTraitsComponent>();
        }

        public NetworkVariableTraitsComponent GetTestComponent()
        {
            return m_ClientNetworkManagers[0].LocalClient.PlayerObject.GetComponent<NetworkVariableTraitsComponent>();
        }

        public NetworkVariableTraitsComponent GetServerComponent()
        {
            foreach (var obj in Object.FindObjectsByType<NetworkVariableTraitsComponent>(FindObjectsSortMode.None))
            {
                if (obj.NetworkManager == m_ServerNetworkManager && obj.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId)
                {
                    return obj;
                }
            }

            return null;
        }

        [Test]
        public void WhenNewValueIsLessThanThreshold_VariableIsNotSerialized()
        {
            var serverComponent = GetServerComponent();
            var testComponent = GetTestComponent();
            serverComponent.TheVariable.CheckExceedsDirtinessThreshold = (in float value, in float newValue) => Mathf.Abs(newValue - value) >= 0.1;

            serverComponent.TheVariable.Value = 0.05f;

            TimeTravel(2, 120);

            Assert.AreEqual(0.05f, serverComponent.TheVariable.Value); ;
            Assert.AreEqual(0, testComponent.TheVariable.Value); ;
        }
        [Test]
        public void WhenNewValueIsGreaterThanThreshold_VariableIsSerialized()
        {
            var serverComponent = GetServerComponent();
            var testComponent = GetTestComponent();
            serverComponent.TheVariable.CheckExceedsDirtinessThreshold = (in float value, in float newValue) => Mathf.Abs(newValue - value) >= 0.1;

            serverComponent.TheVariable.Value = 0.15f;

            TimeTravel(2, 120);

            Assert.AreEqual(0.15f, serverComponent.TheVariable.Value); ;
            Assert.AreEqual(0.15f, testComponent.TheVariable.Value); ;
        }

        [Test]
        public void WhenNewValueIsLessThanThresholdButMaxTimeHasPassed_VariableIsSerialized()
        {
            var serverComponent = GetServerComponent();
            var testComponent = GetTestComponent();
            serverComponent.TheVariable.CheckExceedsDirtinessThreshold = (in float value, in float newValue) => Mathf.Abs(newValue - value) >= 0.1;
            serverComponent.TheVariable.SetUpdateTraits(new NetworkVariableUpdateTraits { MaxSecondsBetweenUpdates = 2 });
            serverComponent.TheVariable.LastUpdateSent = m_ServerNetworkManager.NetworkTimeSystem.LocalTime;

            serverComponent.TheVariable.Value = 0.05f;

            TimeTravel(1 / 60f * 119, 119);

            Assert.AreEqual(0.05f, serverComponent.TheVariable.Value); ;
            Assert.AreEqual(0, testComponent.TheVariable.Value); ;

            TimeTravel(1 / 60f * 4, 4);

            Assert.AreEqual(0.05f, serverComponent.TheVariable.Value); ;
            Assert.AreEqual(0.05f, testComponent.TheVariable.Value); ;
        }

        [Test]
        public void WhenNewValueIsGreaterThanThresholdButMinTimeHasNotPassed_VariableIsNotSerialized()
        {
            var serverComponent = GetServerComponent();
            var testComponent = GetTestComponent();
            serverComponent.TheVariable.CheckExceedsDirtinessThreshold = (in float value, in float newValue) => Mathf.Abs(newValue - value) >= 0.1;
            serverComponent.TheVariable.SetUpdateTraits(new NetworkVariableUpdateTraits { MinSecondsBetweenUpdates = 2 });
            serverComponent.TheVariable.LastUpdateSent = m_ServerNetworkManager.NetworkTimeSystem.LocalTime;

            serverComponent.TheVariable.Value = 0.15f;

            TimeTravel(1 / 60f * 119, 119);

            Assert.AreEqual(0.15f, serverComponent.TheVariable.Value); ;
            Assert.AreEqual(0, testComponent.TheVariable.Value); ;

            TimeTravel(1 / 60f * 4, 4);

            Assert.AreEqual(0.15f, serverComponent.TheVariable.Value); ;
            Assert.AreEqual(0.15f, testComponent.TheVariable.Value); ;
        }

        [Test]
        public void WhenNoThresholdIsSetButMinTimeHasNotPassed_VariableIsNotSerialized()
        {
            var serverComponent = GetServerComponent();
            var testComponent = GetTestComponent();
            serverComponent.TheVariable.SetUpdateTraits(new NetworkVariableUpdateTraits { MinSecondsBetweenUpdates = 2 });
            serverComponent.TheVariable.LastUpdateSent = m_ServerNetworkManager.NetworkTimeSystem.LocalTime;

            serverComponent.TheVariable.Value = 0.15f;

            TimeTravel(1 / 60f * 119, 119);

            Assert.AreEqual(0.15f, serverComponent.TheVariable.Value); ;
            Assert.AreEqual(0, testComponent.TheVariable.Value); ;

            TimeTravel(1 / 60f * 4, 4);

            Assert.AreEqual(0.15f, serverComponent.TheVariable.Value); ;
            Assert.AreEqual(0.15f, testComponent.TheVariable.Value); ;
        }
    }
}
