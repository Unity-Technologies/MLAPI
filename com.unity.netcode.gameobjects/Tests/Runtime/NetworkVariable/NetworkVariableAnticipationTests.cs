using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Netcode.RuntimeTests
{
    internal class NetworkVariableAnticipationComponent : NetworkBehaviour
    {
        public AnticipatedNetworkVariable<int> SnapOnAnticipationFailVariable = new AnticipatedNetworkVariable<int>(0, StaleDataHandling.Ignore);
        public AnticipatedNetworkVariable<float> SmoothOnAnticipationFailVariable = new AnticipatedNetworkVariable<float>(0, StaleDataHandling.Reanticipate);
        public AnticipatedNetworkVariable<float> ReanticipateOnAnticipationFailVariable = new AnticipatedNetworkVariable<float>(0, StaleDataHandling.Reanticipate);

        public override void OnReanticipate(double lastRoundTripTime)
        {
            if (SmoothOnAnticipationFailVariable.ShouldReanticipate)
            {
                if (Mathf.Abs(SmoothOnAnticipationFailVariable.AuthoritativeValue - SmoothOnAnticipationFailVariable.PreviousAnticipatedValue) > Mathf.Epsilon)
                {
                    SmoothOnAnticipationFailVariable.Smooth(SmoothOnAnticipationFailVariable.PreviousAnticipatedValue, SmoothOnAnticipationFailVariable.AuthoritativeValue, 1, Mathf.Lerp);
                }
            }

            if (ReanticipateOnAnticipationFailVariable.ShouldReanticipate)
            {
                // Would love to test some stuff about anticipation based on time, but that is difficult to test accurately.
                // This reanticipating variable will just always anticipate a value 5 higher than the server value.
                ReanticipateOnAnticipationFailVariable.Anticipate(ReanticipateOnAnticipationFailVariable.AuthoritativeValue + 5);
            }
        }

        public bool SnapRpcResponseReceived = false;

        [Rpc(SendTo.Server)]
        public void SetSnapValueRpc(int i, RpcParams rpcParams = default)
        {
            SnapOnAnticipationFailVariable.AuthoritativeValue = i;
            SetSnapValueResponseRpc(RpcTarget.Single(rpcParams.Receive.SenderClientId, RpcTargetUse.Temp));
        }

        [Rpc(SendTo.SpecifiedInParams)]
        public void SetSnapValueResponseRpc(RpcParams rpcParams)
        {
            SnapRpcResponseReceived = true;
        }

        [Rpc(SendTo.Server)]
        public void SetSmoothValueRpc(float f)
        {
            SmoothOnAnticipationFailVariable.AuthoritativeValue = f;
        }

        [Rpc(SendTo.Server)]
        public void SetReanticipateValueRpc(float f)
        {
            ReanticipateOnAnticipationFailVariable.AuthoritativeValue = f;
        }
    }

    internal class NetworkVariableAnticipationTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        protected override bool m_EnableTimeTravel => true;
        protected override bool m_SetupIsACoroutine => false;
        protected override bool m_TearDownIsACoroutine => false;

        protected override void OnPlayerPrefabGameObjectCreated()
        {
            m_PlayerPrefab.AddComponent<NetworkVariableAnticipationComponent>();
        }

        public NetworkVariableAnticipationComponent GetTestComponent()
        {
            return m_ClientNetworkManagers[0].LocalClient.PlayerObject.GetComponent<NetworkVariableAnticipationComponent>();
        }

        public NetworkVariableAnticipationComponent GetServerComponent()
        {
            foreach (var obj in Object.FindObjectsByType<NetworkVariableAnticipationComponent>(FindObjectsSortMode.None))
            {
                if (obj.NetworkManager == m_ServerNetworkManager && obj.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId)
                {
                    return obj;
                }
            }

            return null;
        }

        public NetworkVariableAnticipationComponent GetOtherClientComponent()
        {
            foreach (var obj in Object.FindObjectsByType<NetworkVariableAnticipationComponent>(FindObjectsSortMode.None))
            {
                if (obj.NetworkManager == m_ClientNetworkManagers[1] && obj.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId)
                {
                    return obj;
                }
            }

            return null;
        }

        [Test]
        public void WhenAnticipating_ValueChangesImmediately()
        {
            var testComponent = GetTestComponent();

            testComponent.SnapOnAnticipationFailVariable.Anticipate(10);
            testComponent.SmoothOnAnticipationFailVariable.Anticipate(15);
            testComponent.ReanticipateOnAnticipationFailVariable.Anticipate(20);

            Assert.AreEqual(10, testComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(15, testComponent.SmoothOnAnticipationFailVariable.Value);
            Assert.AreEqual(20, testComponent.ReanticipateOnAnticipationFailVariable.Value);
        }

        [Test]
        public void WhenAnticipating_AuthoritativeValueDoesNotChange()
        {
            var testComponent = GetTestComponent();

            testComponent.SnapOnAnticipationFailVariable.Anticipate(10);
            testComponent.SmoothOnAnticipationFailVariable.Anticipate(15);
            testComponent.ReanticipateOnAnticipationFailVariable.Anticipate(20);

            Assert.AreEqual(0, testComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);
            Assert.AreEqual(0, testComponent.SmoothOnAnticipationFailVariable.AuthoritativeValue);
            Assert.AreEqual(0, testComponent.ReanticipateOnAnticipationFailVariable.AuthoritativeValue);
        }

        [Test]
        public void WhenAnticipating_ServerDoesNotChange()
        {
            var testComponent = GetTestComponent();

            testComponent.SnapOnAnticipationFailVariable.Anticipate(10);
            testComponent.SmoothOnAnticipationFailVariable.Anticipate(15);
            testComponent.ReanticipateOnAnticipationFailVariable.Anticipate(20);

            var serverComponent = GetServerComponent();

            Assert.AreEqual(0, serverComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);
            Assert.AreEqual(0, serverComponent.SmoothOnAnticipationFailVariable.AuthoritativeValue);
            Assert.AreEqual(0, serverComponent.ReanticipateOnAnticipationFailVariable.AuthoritativeValue);
            Assert.AreEqual(0, serverComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(0, serverComponent.SmoothOnAnticipationFailVariable.Value);
            Assert.AreEqual(0, serverComponent.ReanticipateOnAnticipationFailVariable.Value);

            TimeTravel(2, 120);

            Assert.AreEqual(0, serverComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);
            Assert.AreEqual(0, serverComponent.SmoothOnAnticipationFailVariable.AuthoritativeValue);
            Assert.AreEqual(0, serverComponent.ReanticipateOnAnticipationFailVariable.AuthoritativeValue);
            Assert.AreEqual(0, serverComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(0, serverComponent.SmoothOnAnticipationFailVariable.Value);
            Assert.AreEqual(0, serverComponent.ReanticipateOnAnticipationFailVariable.Value);
        }

        [Test]
        public void WhenAnticipating_OtherClientDoesNotChange()
        {
            var testComponent = GetTestComponent();

            testComponent.SnapOnAnticipationFailVariable.Anticipate(10);
            testComponent.SmoothOnAnticipationFailVariable.Anticipate(15);
            testComponent.ReanticipateOnAnticipationFailVariable.Anticipate(20);

            var otherClientComponent = GetOtherClientComponent();

            Assert.AreEqual(0, otherClientComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);
            Assert.AreEqual(0, otherClientComponent.SmoothOnAnticipationFailVariable.AuthoritativeValue);
            Assert.AreEqual(0, otherClientComponent.ReanticipateOnAnticipationFailVariable.AuthoritativeValue);
            Assert.AreEqual(0, otherClientComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(0, otherClientComponent.SmoothOnAnticipationFailVariable.Value);
            Assert.AreEqual(0, otherClientComponent.ReanticipateOnAnticipationFailVariable.Value);

            TimeTravel(2, 120);

            Assert.AreEqual(0, otherClientComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);
            Assert.AreEqual(0, otherClientComponent.SmoothOnAnticipationFailVariable.AuthoritativeValue);
            Assert.AreEqual(0, otherClientComponent.ReanticipateOnAnticipationFailVariable.AuthoritativeValue);
            Assert.AreEqual(0, otherClientComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(0, otherClientComponent.SmoothOnAnticipationFailVariable.Value);
            Assert.AreEqual(0, otherClientComponent.ReanticipateOnAnticipationFailVariable.Value);
        }

        [Test]
        public void WhenServerChangesSnapValue_ValuesAreUpdated()
        {
            var testComponent = GetTestComponent();

            testComponent.SnapOnAnticipationFailVariable.Anticipate(10);

            Assert.AreEqual(10, testComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(0, testComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);

            testComponent.SetSnapValueRpc(10);

            WaitForMessageReceivedWithTimeTravel<RpcMessage>(
                new List<NetworkManager> { m_ServerNetworkManager }
            );

            var serverComponent = GetServerComponent();
            Assert.AreEqual(10, serverComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(10, serverComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);

            var otherClientComponent = GetOtherClientComponent();
            Assert.AreEqual(0, otherClientComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(0, otherClientComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);

            WaitForMessageReceivedWithTimeTravel<NetworkVariableDeltaMessage>(m_ClientNetworkManagers.ToList());

            Assert.AreEqual(10, testComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(10, testComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);

            Assert.AreEqual(10, otherClientComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(10, otherClientComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);
        }

        [Test]
        public void WhenServerChangesSmoothValue_ValuesAreLerped()
        {
            var testComponent = GetTestComponent();

            testComponent.SmoothOnAnticipationFailVariable.Anticipate(15);

            Assert.AreEqual(15, testComponent.SmoothOnAnticipationFailVariable.Value, Mathf.Epsilon);
            Assert.AreEqual(0, testComponent.SmoothOnAnticipationFailVariable.AuthoritativeValue, Mathf.Epsilon);

            // Set to a different value to simulate a anticipation failure - will lerp between the anticipated value
            // and the actual one
            testComponent.SetSmoothValueRpc(20);

            WaitForMessageReceivedWithTimeTravel<RpcMessage>(
                new List<NetworkManager> { m_ServerNetworkManager }
            );

            var serverComponent = GetServerComponent();
            Assert.AreEqual(20, serverComponent.SmoothOnAnticipationFailVariable.Value, Mathf.Epsilon);
            Assert.AreEqual(20, serverComponent.SmoothOnAnticipationFailVariable.AuthoritativeValue, Mathf.Epsilon);

            var otherClientComponent = GetOtherClientComponent();
            Assert.AreEqual(0, otherClientComponent.SmoothOnAnticipationFailVariable.Value, Mathf.Epsilon);
            Assert.AreEqual(0, otherClientComponent.SmoothOnAnticipationFailVariable.AuthoritativeValue, Mathf.Epsilon);

            WaitForMessageReceivedWithTimeTravel<NetworkVariableDeltaMessage>(m_ClientNetworkManagers.ToList());

            Assert.AreEqual(15 + 1f / 60f * 5, testComponent.SmoothOnAnticipationFailVariable.Value, Mathf.Epsilon);
            Assert.AreEqual(20, testComponent.SmoothOnAnticipationFailVariable.AuthoritativeValue, Mathf.Epsilon);

            Assert.AreEqual(0 + 1f / 60f * 20, otherClientComponent.SmoothOnAnticipationFailVariable.Value, Mathf.Epsilon);
            Assert.AreEqual(20, otherClientComponent.SmoothOnAnticipationFailVariable.AuthoritativeValue, Mathf.Epsilon);

            for (var i = 1; i < 60; ++i)
            {
                TimeTravel(1f / 60f, 1);

                Assert.AreEqual(15 + 1f / 60f * 5 * (i + 1), testComponent.SmoothOnAnticipationFailVariable.Value, 0.00001);
                Assert.AreEqual(20, testComponent.SmoothOnAnticipationFailVariable.AuthoritativeValue, Mathf.Epsilon);

                Assert.AreEqual(0 + 1f / 60f * 20 * (i + 1), otherClientComponent.SmoothOnAnticipationFailVariable.Value, 0.00001);
                Assert.AreEqual(20, otherClientComponent.SmoothOnAnticipationFailVariable.AuthoritativeValue, Mathf.Epsilon);
            }
            TimeTravel(1f / 60f, 1);
            Assert.AreEqual(20, testComponent.SmoothOnAnticipationFailVariable.Value, Mathf.Epsilon);
            Assert.AreEqual(20, otherClientComponent.SmoothOnAnticipationFailVariable.Value, Mathf.Epsilon);
        }

        [Test]
        public void WhenServerChangesReanticipateValue_ValuesAreReanticipated()
        {
            var testComponent = GetTestComponent();

            testComponent.ReanticipateOnAnticipationFailVariable.Anticipate(15);

            Assert.AreEqual(15, testComponent.ReanticipateOnAnticipationFailVariable.Value, Mathf.Epsilon);
            Assert.AreEqual(0, testComponent.ReanticipateOnAnticipationFailVariable.AuthoritativeValue, Mathf.Epsilon);

            // Set to a different value to simulate a anticipation failure - will lerp between the anticipated value
            // and the actual one
            testComponent.SetReanticipateValueRpc(20);

            WaitForMessageReceivedWithTimeTravel<RpcMessage>(
                new List<NetworkManager> { m_ServerNetworkManager }
            );

            var serverComponent = GetServerComponent();
            Assert.AreEqual(20, serverComponent.ReanticipateOnAnticipationFailVariable.Value, Mathf.Epsilon);
            Assert.AreEqual(20, serverComponent.ReanticipateOnAnticipationFailVariable.AuthoritativeValue, Mathf.Epsilon);

            var otherClientComponent = GetOtherClientComponent();
            Assert.AreEqual(0, otherClientComponent.ReanticipateOnAnticipationFailVariable.Value, Mathf.Epsilon);
            Assert.AreEqual(0, otherClientComponent.ReanticipateOnAnticipationFailVariable.AuthoritativeValue, Mathf.Epsilon);

            WaitForMessageReceivedWithTimeTravel<NetworkVariableDeltaMessage>(m_ClientNetworkManagers.ToList());

            Assert.AreEqual(25, testComponent.ReanticipateOnAnticipationFailVariable.Value, Mathf.Epsilon);
            Assert.AreEqual(20, testComponent.ReanticipateOnAnticipationFailVariable.AuthoritativeValue, Mathf.Epsilon);

            Assert.AreEqual(25, otherClientComponent.ReanticipateOnAnticipationFailVariable.Value, Mathf.Epsilon);
            Assert.AreEqual(20, otherClientComponent.ReanticipateOnAnticipationFailVariable.AuthoritativeValue, Mathf.Epsilon);
        }

        [Test]
        public void WhenNonStaleDataArrivesToIgnoreVariable_ItIsNotIgnored([Values(10u, 30u, 60u)] uint tickRate, [Values(0u, 1u, 2u)] uint skipFrames)
        {
            m_ServerNetworkManager.NetworkConfig.TickRate = tickRate;
            m_ServerNetworkManager.NetworkTickSystem.TickRate = tickRate;

            for (var i = 0; i < skipFrames; ++i)
            {
                TimeTravel(1 / 60f, 1);
            }
            var testComponent = GetTestComponent();
            testComponent.SnapOnAnticipationFailVariable.Anticipate(10);

            Assert.AreEqual(10, testComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(0, testComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);
            testComponent.SetSnapValueRpc(20);
            WaitForMessageReceivedWithTimeTravel<RpcMessage>(new List<NetworkManager> { m_ServerNetworkManager });

            var serverComponent = GetServerComponent();

            Assert.AreEqual(20, serverComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(20, serverComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);

            WaitForMessageReceivedWithTimeTravel<NetworkVariableDeltaMessage>(m_ClientNetworkManagers.ToList());

            // Both values get updated
            Assert.AreEqual(20, testComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(20, testComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);

            // Other client got the server value and had made no anticipation, so it applies it to the anticipated value as well.
            var otherClientComponent = GetOtherClientComponent();
            Assert.AreEqual(20, otherClientComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(20, otherClientComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);
        }

        [Test]
        public void WhenStaleDataArrivesToIgnoreVariable_ItIsIgnored([Values(10u, 30u, 60u)] uint tickRate, [Values(0u, 1u, 2u)] uint skipFrames)
        {
            m_ServerNetworkManager.NetworkConfig.TickRate = tickRate;
            m_ServerNetworkManager.NetworkTickSystem.TickRate = tickRate;

            for (var i = 0; i < skipFrames; ++i)
            {
                TimeTravel(1 / 60f, 1);
            }
            var testComponent = GetTestComponent();
            testComponent.SnapOnAnticipationFailVariable.Anticipate(10);

            Assert.AreEqual(10, testComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(0, testComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);

            testComponent.SetSnapValueRpc(30);

            var serverComponent = GetServerComponent();
            serverComponent.SnapOnAnticipationFailVariable.AuthoritativeValue = 20;

            Assert.AreEqual(20, serverComponent.SnapOnAnticipationFailVariable.Value);
            Assert.AreEqual(20, serverComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);

            WaitForMessageReceivedWithTimeTravel<NetworkVariableDeltaMessage>(m_ClientNetworkManagers.ToList());

            if (testComponent.SnapRpcResponseReceived)
            {
                // In this case the tick rate is slow enough that the RPC was received and processed, so we check that.
                Assert.AreEqual(30, testComponent.SnapOnAnticipationFailVariable.Value);
                Assert.AreEqual(30, testComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);

                var otherClientComponent = GetOtherClientComponent();
                Assert.AreEqual(30, otherClientComponent.SnapOnAnticipationFailVariable.Value);
                Assert.AreEqual(30, otherClientComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);
            }
            else
            {
                // In this case, we got an update before the RPC was processed, so we should have ignored it.
                // Anticipated client received this data for a tick earlier than its anticipation, and should have prioritized the anticipated value
                Assert.AreEqual(10, testComponent.SnapOnAnticipationFailVariable.Value);
                // However, the authoritative value still gets updated
                Assert.AreEqual(20, testComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);

                // Other client got the server value and had made no anticipation, so it applies it to the anticipated value as well.
                var otherClientComponent = GetOtherClientComponent();
                Assert.AreEqual(20, otherClientComponent.SnapOnAnticipationFailVariable.Value);
                Assert.AreEqual(20, otherClientComponent.SnapOnAnticipationFailVariable.AuthoritativeValue);
            }
        }

        [Test]
        public void WhenStaleDataArrivesToReanticipatedVariable_ItIsAppliedAndReanticipated()
        {
            var testComponent = GetTestComponent();
            testComponent.ReanticipateOnAnticipationFailVariable.Anticipate(10);

            Assert.AreEqual(10, testComponent.ReanticipateOnAnticipationFailVariable.Value);
            Assert.AreEqual(0, testComponent.ReanticipateOnAnticipationFailVariable.AuthoritativeValue);

            var serverComponent = GetServerComponent();
            serverComponent.ReanticipateOnAnticipationFailVariable.AuthoritativeValue = 20;

            Assert.AreEqual(20, serverComponent.ReanticipateOnAnticipationFailVariable.Value);
            Assert.AreEqual(20, serverComponent.ReanticipateOnAnticipationFailVariable.AuthoritativeValue);

            WaitForMessageReceivedWithTimeTravel<NetworkVariableDeltaMessage>(m_ClientNetworkManagers.ToList());

            Assert.AreEqual(25, testComponent.ReanticipateOnAnticipationFailVariable.Value);
            // However, the authoritative value still gets updated
            Assert.AreEqual(20, testComponent.ReanticipateOnAnticipationFailVariable.AuthoritativeValue);

            // Other client got the server value and had made no anticipation, so it applies it to the anticipated value as well.
            var otherClientComponent = GetOtherClientComponent();
            Assert.AreEqual(25, otherClientComponent.ReanticipateOnAnticipationFailVariable.Value);
            Assert.AreEqual(20, otherClientComponent.ReanticipateOnAnticipationFailVariable.AuthoritativeValue);
        }
    }
}
