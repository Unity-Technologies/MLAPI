using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode.Components;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Netcode.RuntimeTests
{
    internal class NetworkTransformAnticipationComponent : NetworkBehaviour
    {
        [Rpc(SendTo.Server)]
        public void MoveRpc(Vector3 newPosition)
        {
            transform.position = newPosition;
        }

        [Rpc(SendTo.Server)]
        public void ScaleRpc(Vector3 newScale)
        {
            transform.localScale = newScale;
        }

        [Rpc(SendTo.Server)]
        public void RotateRpc(Quaternion newRotation)
        {
            transform.rotation = newRotation;
        }

        public bool ShouldSmooth = false;
        public bool ShouldMove = false;

        public override void OnReanticipate(double lastRoundTripTime)
        {
            var transform_ = GetComponent<AnticipatedNetworkTransform>();
            if (transform_.ShouldReanticipate)
            {
                if (ShouldSmooth)
                {
                    transform_.Smooth(transform_.PreviousAnticipatedState, transform_.AuthoritativeState, 1);
                }

                if (ShouldMove)
                {
                    transform_.AnticipateMove(transform_.AuthoritativeState.Position + new Vector3(0, 5, 0));

                }
            }
        }
    }

    internal class NetworkTransformAnticipationTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        protected override bool m_EnableTimeTravel => true;
        protected override bool m_SetupIsACoroutine => false;
        protected override bool m_TearDownIsACoroutine => false;

        protected override void OnPlayerPrefabGameObjectCreated()
        {
            m_PlayerPrefab.AddComponent<AnticipatedNetworkTransform>();
            m_PlayerPrefab.AddComponent<NetworkTransformAnticipationComponent>();
        }

        protected override void OnTimeTravelServerAndClientsConnected()
        {
            var serverComponent = GetServerComponent();
            var testComponent = GetTestComponent();
            var otherClientComponent = GetOtherClientComponent();

            serverComponent.transform.position = Vector3.zero;
            serverComponent.transform.localScale = Vector3.one;
            serverComponent.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            testComponent.transform.position = Vector3.zero;
            testComponent.transform.localScale = Vector3.one;
            testComponent.transform.rotation = Quaternion.LookRotation(Vector3.forward);
            otherClientComponent.transform.position = Vector3.zero;
            otherClientComponent.transform.localScale = Vector3.one;
            otherClientComponent.transform.rotation = Quaternion.LookRotation(Vector3.forward);
        }

        public AnticipatedNetworkTransform GetTestComponent()
        {
            return m_ClientNetworkManagers[0].LocalClient.PlayerObject.GetComponent<AnticipatedNetworkTransform>();
        }

        public AnticipatedNetworkTransform GetServerComponent()
        {
            foreach (var obj in Object.FindObjectsByType<AnticipatedNetworkTransform>(FindObjectsSortMode.None))
            {
                if (obj.NetworkManager == m_ServerNetworkManager && obj.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId)
                {
                    return obj;
                }
            }

            return null;
        }

        public AnticipatedNetworkTransform GetOtherClientComponent()
        {
            foreach (var obj in Object.FindObjectsByType<AnticipatedNetworkTransform>(FindObjectsSortMode.None))
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

            testComponent.AnticipateMove(new Vector3(0, 1, 2));
            testComponent.AnticipateScale(new Vector3(1, 2, 3));
            testComponent.AnticipateRotate(Quaternion.LookRotation(new Vector3(2, 3, 4)));

            Assert.AreEqual(new Vector3(0, 1, 2), testComponent.transform.position);
            Assert.AreEqual(new Vector3(1, 2, 3), testComponent.transform.localScale);
            Assert.AreEqual(Quaternion.LookRotation(new Vector3(2, 3, 4)), testComponent.transform.rotation);

            Assert.AreEqual(new Vector3(0, 1, 2), testComponent.AnticipatedState.Position);
            Assert.AreEqual(new Vector3(1, 2, 3), testComponent.AnticipatedState.Scale);
            Assert.AreEqual(Quaternion.LookRotation(new Vector3(2, 3, 4)), testComponent.AnticipatedState.Rotation);

        }

        [Test]
        public void WhenAnticipating_AuthoritativeValueDoesNotChange()
        {
            var testComponent = GetTestComponent();

            var startPosition = testComponent.transform.position;
            var startScale = testComponent.transform.localScale;
            var startRotation = testComponent.transform.rotation;

            testComponent.AnticipateMove(new Vector3(0, 1, 2));
            testComponent.AnticipateScale(new Vector3(1, 2, 3));
            testComponent.AnticipateRotate(Quaternion.LookRotation(new Vector3(2, 3, 4)));

            Assert.AreEqual(startPosition, testComponent.AuthoritativeState.Position);
            Assert.AreEqual(startScale, testComponent.AuthoritativeState.Scale);
            Assert.AreEqual(startRotation, testComponent.AuthoritativeState.Rotation);
        }

        [Test]
        public void WhenAnticipating_ServerDoesNotChange()
        {
            var testComponent = GetTestComponent();

            var startPosition = testComponent.transform.position;
            var startScale = testComponent.transform.localScale;
            var startRotation = testComponent.transform.rotation;

            testComponent.AnticipateMove(new Vector3(0, 1, 2));
            testComponent.AnticipateScale(new Vector3(1, 2, 3));
            testComponent.AnticipateRotate(Quaternion.LookRotation(new Vector3(2, 3, 4)));

            var serverComponent = GetServerComponent();

            Assert.AreEqual(startPosition, serverComponent.AuthoritativeState.Position);
            Assert.AreEqual(startScale, serverComponent.AuthoritativeState.Scale);
            Assert.AreEqual(startRotation, serverComponent.AuthoritativeState.Rotation);
            Assert.AreEqual(startPosition, serverComponent.AnticipatedState.Position);
            Assert.AreEqual(startScale, serverComponent.AnticipatedState.Scale);
            Assert.AreEqual(startRotation, serverComponent.AnticipatedState.Rotation);

            TimeTravel(2, 120);

            Assert.AreEqual(startPosition, serverComponent.AuthoritativeState.Position);
            Assert.AreEqual(startScale, serverComponent.AuthoritativeState.Scale);
            Assert.AreEqual(startRotation, serverComponent.AuthoritativeState.Rotation);
            Assert.AreEqual(startPosition, serverComponent.AnticipatedState.Position);
            Assert.AreEqual(startScale, serverComponent.AnticipatedState.Scale);
            Assert.AreEqual(startRotation, serverComponent.AnticipatedState.Rotation);
        }

        [Test]
        public void WhenAnticipating_OtherClientDoesNotChange()
        {
            var testComponent = GetTestComponent();

            var startPosition = testComponent.transform.position;
            var startScale = testComponent.transform.localScale;
            var startRotation = testComponent.transform.rotation;

            testComponent.AnticipateMove(new Vector3(0, 1, 2));
            testComponent.AnticipateScale(new Vector3(1, 2, 3));
            testComponent.AnticipateRotate(Quaternion.LookRotation(new Vector3(2, 3, 4)));

            var otherClientComponent = GetOtherClientComponent();

            Assert.AreEqual(startPosition, otherClientComponent.AuthoritativeState.Position);
            Assert.AreEqual(startScale, otherClientComponent.AuthoritativeState.Scale);
            Assert.AreEqual(startRotation, otherClientComponent.AuthoritativeState.Rotation);
            Assert.AreEqual(startPosition, otherClientComponent.AnticipatedState.Position);
            Assert.AreEqual(startScale, otherClientComponent.AnticipatedState.Scale);
            Assert.AreEqual(startRotation, otherClientComponent.AnticipatedState.Rotation);

            TimeTravel(2, 120);

            Assert.AreEqual(startPosition, otherClientComponent.AuthoritativeState.Position);
            Assert.AreEqual(startScale, otherClientComponent.AuthoritativeState.Scale);
            Assert.AreEqual(startRotation, otherClientComponent.AuthoritativeState.Rotation);
            Assert.AreEqual(startPosition, otherClientComponent.AnticipatedState.Position);
            Assert.AreEqual(startScale, otherClientComponent.AnticipatedState.Scale);
            Assert.AreEqual(startRotation, otherClientComponent.AnticipatedState.Rotation);
        }

        [Test]
        public void WhenServerChangesSnapValue_ValuesAreUpdated()
        {
            var testComponent = GetTestComponent();
            var serverComponent = GetServerComponent();
            serverComponent.Interpolate = false;

            testComponent.AnticipateMove(new Vector3(0, 1, 2));
            testComponent.AnticipateScale(new Vector3(1, 2, 3));
            testComponent.AnticipateRotate(Quaternion.LookRotation(new Vector3(2, 3, 4)));

            var rpcComponent = testComponent.GetComponent<NetworkTransformAnticipationComponent>();
            rpcComponent.MoveRpc(new Vector3(2, 3, 4));

            WaitForMessageReceivedWithTimeTravel<RpcMessage>(new List<NetworkManager> { m_ServerNetworkManager });
            var otherClientComponent = GetOtherClientComponent();

            WaitForConditionOrTimeOutWithTimeTravel(() => testComponent.AuthoritativeState.Position == serverComponent.transform.position && otherClientComponent.AuthoritativeState.Position == serverComponent.transform.position);

            Assert.AreEqual(serverComponent.transform.position, testComponent.transform.position);
            Assert.AreEqual(serverComponent.transform.position, testComponent.AnticipatedState.Position);
            Assert.AreEqual(serverComponent.transform.position, testComponent.AuthoritativeState.Position);

            Assert.AreEqual(serverComponent.transform.position, otherClientComponent.transform.position);
            Assert.AreEqual(serverComponent.transform.position, otherClientComponent.AnticipatedState.Position);
            Assert.AreEqual(serverComponent.transform.position, otherClientComponent.AuthoritativeState.Position);
        }

        public void AssertQuaternionsAreEquivalent(Quaternion a, Quaternion b)
        {
            var aAngles = a.eulerAngles;
            var bAngles = b.eulerAngles;
            Assert.AreEqual(aAngles.x, bAngles.x, 0.001, $"Quaternions were not equal. Expected: {a}, but was {b}");
            Assert.AreEqual(aAngles.y, bAngles.y, 0.001, $"Quaternions were not equal. Expected: {a}, but was {b}");
            Assert.AreEqual(aAngles.z, bAngles.z, 0.001, $"Quaternions were not equal. Expected: {a}, but was {b}");
        }
        public void AssertVectorsAreEquivalent(Vector3 a, Vector3 b)
        {
            Assert.AreEqual(a.x, b.x, 0.001, $"Vectors were not equal. Expected: {a}, but was {b}");
            Assert.AreEqual(a.y, b.y, 0.001, $"Vectors were not equal. Expected: {a}, but was {b}");
            Assert.AreEqual(a.z, b.z, 0.001, $"Vectors were not equal. Expected: {a}, but was {b}");
        }

        [Test]
        public void WhenServerChangesSmoothValue_ValuesAreLerped()
        {
            var testComponent = GetTestComponent();
            var otherClientComponent = GetOtherClientComponent();

            testComponent.StaleDataHandling = StaleDataHandling.Ignore;
            otherClientComponent.StaleDataHandling = StaleDataHandling.Ignore;

            var serverComponent = GetServerComponent();
            serverComponent.Interpolate = false;

            testComponent.GetComponent<NetworkTransformAnticipationComponent>().ShouldSmooth = true;
            otherClientComponent.GetComponent<NetworkTransformAnticipationComponent>().ShouldSmooth = true;

            var startPosition = testComponent.transform.position;
            var startScale = testComponent.transform.localScale;
            var startRotation = testComponent.transform.rotation;
            var anticipePosition = new Vector3(0, 1, 2);
            var anticipeScale = new Vector3(1, 2, 3);
            var anticipeRotation = Quaternion.LookRotation(new Vector3(2, 3, 4));
            var serverSetPosition = new Vector3(3, 4, 5);
            var serverSetScale = new Vector3(4, 5, 6);
            var serverSetRotation = Quaternion.LookRotation(new Vector3(5, 6, 7));

            testComponent.AnticipateMove(anticipePosition);
            testComponent.AnticipateScale(anticipeScale);
            testComponent.AnticipateRotate(anticipeRotation);

            var rpcComponent = testComponent.GetComponent<NetworkTransformAnticipationComponent>();
            rpcComponent.MoveRpc(serverSetPosition);
            rpcComponent.RotateRpc(serverSetRotation);
            rpcComponent.ScaleRpc(serverSetScale);

            WaitForMessagesReceivedWithTimeTravel(new List<Type>
            {
                typeof(RpcMessage),
                typeof(RpcMessage),
                typeof(RpcMessage),
            }, new List<NetworkManager> { m_ServerNetworkManager });

            WaitForMessageReceivedWithTimeTravel<NetworkTransformMessage>(m_ClientNetworkManagers.ToList());
            var percentChanged = 1f / 60f;

            AssertVectorsAreEquivalent(Vector3.Lerp(anticipePosition, serverSetPosition, percentChanged), testComponent.transform.position);
            AssertVectorsAreEquivalent(Vector3.Lerp(anticipeScale, serverSetScale, percentChanged), testComponent.transform.localScale);
            AssertQuaternionsAreEquivalent(Quaternion.Lerp(anticipeRotation, serverSetRotation, percentChanged), testComponent.transform.rotation);

            AssertVectorsAreEquivalent(Vector3.Lerp(anticipePosition, serverSetPosition, percentChanged), testComponent.AnticipatedState.Position);
            AssertVectorsAreEquivalent(Vector3.Lerp(anticipeScale, serverSetScale, percentChanged), testComponent.AnticipatedState.Scale);
            AssertQuaternionsAreEquivalent(Quaternion.Lerp(anticipeRotation, serverSetRotation, percentChanged), testComponent.AnticipatedState.Rotation);

            AssertVectorsAreEquivalent(serverSetPosition, testComponent.AuthoritativeState.Position);
            AssertVectorsAreEquivalent(serverSetScale, testComponent.AuthoritativeState.Scale);
            AssertQuaternionsAreEquivalent(serverSetRotation, testComponent.AuthoritativeState.Rotation);

            AssertVectorsAreEquivalent(Vector3.Lerp(startPosition, serverSetPosition, percentChanged), otherClientComponent.transform.position);
            AssertVectorsAreEquivalent(Vector3.Lerp(startScale, serverSetScale, percentChanged), otherClientComponent.transform.localScale);
            AssertQuaternionsAreEquivalent(Quaternion.Lerp(startRotation, serverSetRotation, percentChanged), otherClientComponent.transform.rotation);

            AssertVectorsAreEquivalent(Vector3.Lerp(startPosition, serverSetPosition, percentChanged), otherClientComponent.AnticipatedState.Position);
            AssertVectorsAreEquivalent(Vector3.Lerp(startScale, serverSetScale, percentChanged), otherClientComponent.AnticipatedState.Scale);
            AssertQuaternionsAreEquivalent(Quaternion.Lerp(startRotation, serverSetRotation, percentChanged), otherClientComponent.AnticipatedState.Rotation);

            AssertVectorsAreEquivalent(serverSetPosition, otherClientComponent.AuthoritativeState.Position);
            AssertVectorsAreEquivalent(serverSetScale, otherClientComponent.AuthoritativeState.Scale);
            AssertQuaternionsAreEquivalent(serverSetRotation, otherClientComponent.AuthoritativeState.Rotation);

            for (var i = 1; i < 60; ++i)
            {
                TimeTravel(1f / 60f, 1);
                percentChanged = 1f / 60f * (i + 1);

                AssertVectorsAreEquivalent(Vector3.Lerp(anticipePosition, serverSetPosition, percentChanged), testComponent.transform.position);
                AssertVectorsAreEquivalent(Vector3.Lerp(anticipeScale, serverSetScale, percentChanged), testComponent.transform.localScale);
                AssertQuaternionsAreEquivalent(Quaternion.Lerp(anticipeRotation, serverSetRotation, percentChanged), testComponent.transform.rotation);

                AssertVectorsAreEquivalent(Vector3.Lerp(anticipePosition, serverSetPosition, percentChanged), testComponent.AnticipatedState.Position);
                AssertVectorsAreEquivalent(Vector3.Lerp(anticipeScale, serverSetScale, percentChanged), testComponent.AnticipatedState.Scale);
                AssertQuaternionsAreEquivalent(Quaternion.Lerp(anticipeRotation, serverSetRotation, percentChanged), testComponent.AnticipatedState.Rotation);

                AssertVectorsAreEquivalent(serverSetPosition, testComponent.AuthoritativeState.Position);
                AssertVectorsAreEquivalent(serverSetScale, testComponent.AuthoritativeState.Scale);
                AssertQuaternionsAreEquivalent(serverSetRotation, testComponent.AuthoritativeState.Rotation);

                AssertVectorsAreEquivalent(Vector3.Lerp(startPosition, serverSetPosition, percentChanged), otherClientComponent.transform.position);
                AssertVectorsAreEquivalent(Vector3.Lerp(startScale, serverSetScale, percentChanged), otherClientComponent.transform.localScale);
                AssertQuaternionsAreEquivalent(Quaternion.Lerp(startRotation, serverSetRotation, percentChanged), otherClientComponent.transform.rotation);

                AssertVectorsAreEquivalent(Vector3.Lerp(startPosition, serverSetPosition, percentChanged), otherClientComponent.AnticipatedState.Position);
                AssertVectorsAreEquivalent(Vector3.Lerp(startScale, serverSetScale, percentChanged), otherClientComponent.AnticipatedState.Scale);
                AssertQuaternionsAreEquivalent(Quaternion.Lerp(startRotation, serverSetRotation, percentChanged), otherClientComponent.AnticipatedState.Rotation);

                AssertVectorsAreEquivalent(serverSetPosition, otherClientComponent.AuthoritativeState.Position);
                AssertVectorsAreEquivalent(serverSetScale, otherClientComponent.AuthoritativeState.Scale);
                AssertQuaternionsAreEquivalent(serverSetRotation, otherClientComponent.AuthoritativeState.Rotation);
            }
            TimeTravel(1f / 60f, 1);

            AssertVectorsAreEquivalent(serverSetPosition, testComponent.transform.position);
            AssertVectorsAreEquivalent(serverSetScale, testComponent.transform.localScale);
            AssertQuaternionsAreEquivalent(serverSetRotation, testComponent.transform.rotation);

            AssertVectorsAreEquivalent(serverSetPosition, testComponent.AnticipatedState.Position);
            AssertVectorsAreEquivalent(serverSetScale, testComponent.AnticipatedState.Scale);
            AssertQuaternionsAreEquivalent(serverSetRotation, testComponent.AnticipatedState.Rotation);

            AssertVectorsAreEquivalent(serverSetPosition, testComponent.AuthoritativeState.Position);
            AssertVectorsAreEquivalent(serverSetScale, testComponent.AuthoritativeState.Scale);
            AssertQuaternionsAreEquivalent(serverSetRotation, testComponent.AuthoritativeState.Rotation);

            AssertVectorsAreEquivalent(serverSetPosition, otherClientComponent.transform.position);
            AssertVectorsAreEquivalent(serverSetScale, otherClientComponent.transform.localScale);
            AssertQuaternionsAreEquivalent(serverSetRotation, otherClientComponent.transform.rotation);

            AssertVectorsAreEquivalent(serverSetPosition, otherClientComponent.AnticipatedState.Position);
            AssertVectorsAreEquivalent(serverSetScale, otherClientComponent.AnticipatedState.Scale);
            AssertQuaternionsAreEquivalent(serverSetRotation, otherClientComponent.AnticipatedState.Rotation);

            AssertVectorsAreEquivalent(serverSetPosition, otherClientComponent.AuthoritativeState.Position);
            AssertVectorsAreEquivalent(serverSetScale, otherClientComponent.AuthoritativeState.Scale);
            AssertQuaternionsAreEquivalent(serverSetRotation, otherClientComponent.AuthoritativeState.Rotation);
        }

        [Test]
        public void WhenServerChangesReanticipeValue_ValuesAreReanticiped()
        {
            var testComponent = GetTestComponent();
            var otherClientComponent = GetOtherClientComponent();

            testComponent.GetComponent<NetworkTransformAnticipationComponent>().ShouldMove = true;
            otherClientComponent.GetComponent<NetworkTransformAnticipationComponent>().ShouldMove = true;

            var serverComponent = GetServerComponent();
            serverComponent.Interpolate = false;
            serverComponent.transform.position = new Vector3(0, 1, 2);
            var rpcComponent = testComponent.GetComponent<NetworkTransformAnticipationComponent>();
            rpcComponent.MoveRpc(new Vector3(0, 1, 2));

            WaitForMessageReceivedWithTimeTravel<RpcMessage>(new List<NetworkManager> { m_ServerNetworkManager });

            WaitForMessageReceivedWithTimeTravel<NetworkTransformMessage>(m_ClientNetworkManagers.ToList());

            Assert.AreEqual(new Vector3(0, 6, 2), testComponent.transform.position);
            Assert.AreEqual(new Vector3(0, 6, 2), testComponent.AnticipatedState.Position);
            Assert.AreEqual(new Vector3(0, 1, 2), testComponent.AuthoritativeState.Position);

            Assert.AreEqual(new Vector3(0, 6, 2), otherClientComponent.transform.position);
            Assert.AreEqual(new Vector3(0, 6, 2), otherClientComponent.AnticipatedState.Position);
            Assert.AreEqual(new Vector3(0, 1, 2), otherClientComponent.AuthoritativeState.Position);
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

            var serverComponent = GetServerComponent();
            serverComponent.Interpolate = false;

            var testComponent = GetTestComponent();
            testComponent.StaleDataHandling = StaleDataHandling.Ignore;
            testComponent.Interpolate = false;

            var otherClientComponent = GetOtherClientComponent();
            otherClientComponent.StaleDataHandling = StaleDataHandling.Ignore;
            otherClientComponent.Interpolate = false;

            var rpcComponent = testComponent.GetComponent<NetworkTransformAnticipationComponent>();
            rpcComponent.MoveRpc(new Vector3(1, 2, 3));

            WaitForMessageReceivedWithTimeTravel<RpcMessage>(new List<NetworkManager> { m_ServerNetworkManager });

            testComponent.AnticipateMove(new Vector3(0, 5, 0));
            rpcComponent.MoveRpc(new Vector3(4, 5, 6));

            // Depending on tick rate, one of these two things will happen.
            // The assertions are different based on this... either the tick rate is slow enough that the second RPC is received
            // before the next update and we move to 4, 5, 6, or the tick rate is fast enough that the next update is sent out
            // before the RPC is received and we get the update for the move to 1, 2, 3. Both are valid, what we want to assert
            // here is that the anticipated state never becomes 1, 2, 3.
            WaitForConditionOrTimeOutWithTimeTravel(() => testComponent.AuthoritativeState.Position == new Vector3(1, 2, 3) || testComponent.AuthoritativeState.Position == new Vector3(4, 5, 6));

            if (testComponent.AnticipatedState.Position == new Vector3(4, 5, 6))
            {
                // Anticiped client received this data for a time earlier than its anticipation, and should have prioritized the anticiped value
                Assert.AreEqual(new Vector3(4, 5, 6), testComponent.transform.position);
                Assert.AreEqual(new Vector3(4, 5, 6), testComponent.AnticipatedState.Position);
                // However, the authoritative value still gets updated
                Assert.AreEqual(new Vector3(4, 5, 6), testComponent.AuthoritativeState.Position);

                // Other client got the server value and had made no anticipation, so it applies it to the anticiped value as well.
                Assert.AreEqual(new Vector3(4, 5, 6), otherClientComponent.transform.position);
                Assert.AreEqual(new Vector3(4, 5, 6), otherClientComponent.AnticipatedState.Position);
                Assert.AreEqual(new Vector3(4, 5, 6), otherClientComponent.AuthoritativeState.Position);
            }
            else
            {
                // Anticiped client received this data for a time earlier than its anticipation, and should have prioritized the anticiped value
                Assert.AreEqual(new Vector3(0, 5, 0), testComponent.transform.position);
                Assert.AreEqual(new Vector3(0, 5, 0), testComponent.AnticipatedState.Position);
                // However, the authoritative value still gets updated
                Assert.AreEqual(new Vector3(1, 2, 3), testComponent.AuthoritativeState.Position);

                // Other client got the server value and had made no anticipation, so it applies it to the anticiped value as well.
                Assert.AreEqual(new Vector3(1, 2, 3), otherClientComponent.transform.position);
                Assert.AreEqual(new Vector3(1, 2, 3), otherClientComponent.AnticipatedState.Position);
                Assert.AreEqual(new Vector3(1, 2, 3), otherClientComponent.AuthoritativeState.Position);
            }
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

            var serverComponent = GetServerComponent();
            serverComponent.Interpolate = false;

            var testComponent = GetTestComponent();
            testComponent.StaleDataHandling = StaleDataHandling.Ignore;
            testComponent.Interpolate = false;

            var otherClientComponent = GetOtherClientComponent();
            otherClientComponent.StaleDataHandling = StaleDataHandling.Ignore;
            otherClientComponent.Interpolate = false;

            testComponent.AnticipateMove(new Vector3(0, 5, 0));
            var rpcComponent = testComponent.GetComponent<NetworkTransformAnticipationComponent>();
            rpcComponent.MoveRpc(new Vector3(1, 2, 3));

            WaitForMessageReceivedWithTimeTravel<RpcMessage>(new List<NetworkManager> { m_ServerNetworkManager });

            WaitForConditionOrTimeOutWithTimeTravel(() => testComponent.AuthoritativeState.Position == serverComponent.transform.position && otherClientComponent.AuthoritativeState.Position == serverComponent.transform.position);

            // Anticiped client received this data for a time earlier than its anticipation, and should have prioritized the anticiped value
            Assert.AreEqual(new Vector3(1, 2, 3), testComponent.transform.position);
            Assert.AreEqual(new Vector3(1, 2, 3), testComponent.AnticipatedState.Position);
            // However, the authoritative value still gets updated
            Assert.AreEqual(new Vector3(1, 2, 3), testComponent.AuthoritativeState.Position);

            // Other client got the server value and had made no anticipation, so it applies it to the anticiped value as well.
            Assert.AreEqual(new Vector3(1, 2, 3), otherClientComponent.transform.position);
            Assert.AreEqual(new Vector3(1, 2, 3), otherClientComponent.AnticipatedState.Position);
            Assert.AreEqual(new Vector3(1, 2, 3), otherClientComponent.AuthoritativeState.Position);
        }
    }
}
