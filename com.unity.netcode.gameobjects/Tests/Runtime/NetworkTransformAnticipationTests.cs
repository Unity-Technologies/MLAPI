using NUnit.Framework;
using Unity.Netcode.Components;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Netcode.RuntimeTests
{
    public class NetworkTransformAnticipationTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 2;

        protected override bool m_EnableTimeTravel => true;
        protected override bool m_SetupIsACoroutine => false;
        protected override bool m_TearDownIsACoroutine => false;

        protected override void OnPlayerPrefabGameObjectCreated()
        {
            m_PlayerPrefab.AddComponent<AnticipatedNetworkTransform>();
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

            Assert.AreEqual(startPosition, testComponent.AuthorityState.Position);
            Assert.AreEqual(startScale, testComponent.AuthorityState.Scale);
            Assert.AreEqual(startRotation, testComponent.AuthorityState.Rotation);
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

            Assert.AreEqual(startPosition, serverComponent.AuthorityState.Position);
            Assert.AreEqual(startScale, serverComponent.AuthorityState.Scale);
            Assert.AreEqual(startRotation, serverComponent.AuthorityState.Rotation);
            Assert.AreEqual(startPosition, serverComponent.AnticipatedState.Position);
            Assert.AreEqual(startScale, serverComponent.AnticipatedState.Scale);
            Assert.AreEqual(startRotation, serverComponent.AnticipatedState.Rotation);

            TimeTravel(2, 120);

            Assert.AreEqual(startPosition, serverComponent.AuthorityState.Position);
            Assert.AreEqual(startScale, serverComponent.AuthorityState.Scale);
            Assert.AreEqual(startRotation, serverComponent.AuthorityState.Rotation);
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

            Assert.AreEqual(startPosition, otherClientComponent.AuthorityState.Position);
            Assert.AreEqual(startScale, otherClientComponent.AuthorityState.Scale);
            Assert.AreEqual(startRotation, otherClientComponent.AuthorityState.Rotation);
            Assert.AreEqual(startPosition, otherClientComponent.AnticipatedState.Position);
            Assert.AreEqual(startScale, otherClientComponent.AnticipatedState.Scale);
            Assert.AreEqual(startRotation, otherClientComponent.AnticipatedState.Rotation);

            TimeTravel(2, 120);

            Assert.AreEqual(startPosition, otherClientComponent.AuthorityState.Position);
            Assert.AreEqual(startScale, otherClientComponent.AuthorityState.Scale);
            Assert.AreEqual(startRotation, otherClientComponent.AuthorityState.Rotation);
            Assert.AreEqual(startPosition, otherClientComponent.AnticipatedState.Position);
            Assert.AreEqual(startScale, otherClientComponent.AnticipatedState.Scale);
            Assert.AreEqual(startRotation, otherClientComponent.AnticipatedState.Rotation);
        }

        [Test]
        public void WhenServerChangesSnapValue_ValuesAreUpdated()
        {
            var testComponent = GetTestComponent();

            testComponent.AnticipateMove(new Vector3(0, 1, 2));
            testComponent.AnticipateScale(new Vector3(1, 2, 3));
            testComponent.AnticipateRotate(Quaternion.LookRotation(new Vector3(2, 3, 4)));

            TimeTravelToNextTick();

            var serverComponent = GetServerComponent();
            serverComponent.Interpolate = false;
            serverComponent.transform.position = new Vector3(2, 3, 4);
            var otherClientComponent = GetOtherClientComponent();

            WaitForConditionOrTimeOutWithTimeTravel(() => testComponent.AuthorityState.Position == serverComponent.transform.position && otherClientComponent.AuthorityState.Position == serverComponent.transform.position);

            Assert.AreEqual(serverComponent.transform.position, testComponent.transform.position);
            Assert.AreEqual(serverComponent.transform.position, testComponent.AnticipatedState.Position);
            Assert.AreEqual(serverComponent.transform.position, testComponent.AuthorityState.Position);

            Assert.AreEqual(serverComponent.transform.position, otherClientComponent.transform.position);
            Assert.AreEqual(serverComponent.transform.position, otherClientComponent.AnticipatedState.Position);
            Assert.AreEqual(serverComponent.transform.position, otherClientComponent.AuthorityState.Position);
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

            testComponent.OnReanticipate = (transform, anticipedValue, anticipationTick, authorityValue, authorityTick) =>
            {
                transform.Smooth(anticipedValue, authorityValue, 1);
            };
            otherClientComponent.OnReanticipate = testComponent.OnReanticipate;

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

            TimeTravelToNextTick();

            serverComponent.transform.position = serverSetPosition;
            serverComponent.transform.localScale = serverSetScale;
            serverComponent.transform.rotation = serverSetRotation;

            WaitForConditionOrTimeOutWithTimeTravel(() => testComponent.AuthorityState.Position == serverSetPosition && otherClientComponent.AuthorityState.Position == serverSetPosition);

            var percentChanged = 1f / 60f;

            AssertVectorsAreEquivalent(Vector3.Lerp(anticipePosition, serverSetPosition, percentChanged), testComponent.transform.position);
            AssertVectorsAreEquivalent(Vector3.Lerp(anticipeScale, serverSetScale, percentChanged), testComponent.transform.localScale);
            AssertQuaternionsAreEquivalent(Quaternion.Slerp(anticipeRotation, serverSetRotation, percentChanged), testComponent.transform.rotation);

            AssertVectorsAreEquivalent(Vector3.Lerp(anticipePosition, serverSetPosition, percentChanged), testComponent.AnticipatedState.Position);
            AssertVectorsAreEquivalent(Vector3.Lerp(anticipeScale, serverSetScale, percentChanged), testComponent.AnticipatedState.Scale);
            AssertQuaternionsAreEquivalent(Quaternion.Slerp(anticipeRotation, serverSetRotation, percentChanged), testComponent.AnticipatedState.Rotation);

            AssertVectorsAreEquivalent(serverSetPosition, testComponent.AuthorityState.Position);
            AssertVectorsAreEquivalent(serverSetScale, testComponent.AuthorityState.Scale);
            AssertQuaternionsAreEquivalent(serverSetRotation, testComponent.AuthorityState.Rotation);

            AssertVectorsAreEquivalent(Vector3.Lerp(startPosition, serverSetPosition, percentChanged), otherClientComponent.transform.position);
            AssertVectorsAreEquivalent(Vector3.Lerp(startScale, serverSetScale, percentChanged), otherClientComponent.transform.localScale);
            AssertQuaternionsAreEquivalent(Quaternion.Slerp(startRotation, serverSetRotation, percentChanged), otherClientComponent.transform.rotation);

            AssertVectorsAreEquivalent(Vector3.Lerp(startPosition, serverSetPosition, percentChanged), otherClientComponent.AnticipatedState.Position);
            AssertVectorsAreEquivalent(Vector3.Lerp(startScale, serverSetScale, percentChanged), otherClientComponent.AnticipatedState.Scale);
            AssertQuaternionsAreEquivalent(Quaternion.Slerp(startRotation, serverSetRotation, percentChanged), otherClientComponent.AnticipatedState.Rotation);

            AssertVectorsAreEquivalent(serverSetPosition, otherClientComponent.AuthorityState.Position);
            AssertVectorsAreEquivalent(serverSetScale, otherClientComponent.AuthorityState.Scale);
            AssertQuaternionsAreEquivalent(serverSetRotation, otherClientComponent.AuthorityState.Rotation);

            for (var i = 1; i < 60; ++i)
            {
                TimeTravel(1f / 60f, 1);
                percentChanged = 1f / 60f * (i + 1);

                AssertVectorsAreEquivalent(Vector3.Lerp(anticipePosition, serverSetPosition, percentChanged), testComponent.transform.position);
                AssertVectorsAreEquivalent(Vector3.Lerp(anticipeScale, serverSetScale, percentChanged), testComponent.transform.localScale);
                AssertQuaternionsAreEquivalent(Quaternion.Slerp(anticipeRotation, serverSetRotation, percentChanged), testComponent.transform.rotation);

                AssertVectorsAreEquivalent(Vector3.Lerp(anticipePosition, serverSetPosition, percentChanged), testComponent.AnticipatedState.Position);
                AssertVectorsAreEquivalent(Vector3.Lerp(anticipeScale, serverSetScale, percentChanged), testComponent.AnticipatedState.Scale);
                AssertQuaternionsAreEquivalent(Quaternion.Slerp(anticipeRotation, serverSetRotation, percentChanged), testComponent.AnticipatedState.Rotation);

                AssertVectorsAreEquivalent(serverSetPosition, testComponent.AuthorityState.Position);
                AssertVectorsAreEquivalent(serverSetScale, testComponent.AuthorityState.Scale);
                AssertQuaternionsAreEquivalent(serverSetRotation, testComponent.AuthorityState.Rotation);

                AssertVectorsAreEquivalent(Vector3.Lerp(startPosition, serverSetPosition, percentChanged), otherClientComponent.transform.position);
                AssertVectorsAreEquivalent(Vector3.Lerp(startScale, serverSetScale, percentChanged), otherClientComponent.transform.localScale);
                AssertQuaternionsAreEquivalent(Quaternion.Slerp(startRotation, serverSetRotation, percentChanged), otherClientComponent.transform.rotation);

                AssertVectorsAreEquivalent(Vector3.Lerp(startPosition, serverSetPosition, percentChanged), otherClientComponent.AnticipatedState.Position);
                AssertVectorsAreEquivalent(Vector3.Lerp(startScale, serverSetScale, percentChanged), otherClientComponent.AnticipatedState.Scale);
                AssertQuaternionsAreEquivalent(Quaternion.Slerp(startRotation, serverSetRotation, percentChanged), otherClientComponent.AnticipatedState.Rotation);

                AssertVectorsAreEquivalent(serverSetPosition, otherClientComponent.AuthorityState.Position);
                AssertVectorsAreEquivalent(serverSetScale, otherClientComponent.AuthorityState.Scale);
                AssertQuaternionsAreEquivalent(serverSetRotation, otherClientComponent.AuthorityState.Rotation);
            }
            TimeTravel(1f / 60f, 1);

            AssertVectorsAreEquivalent(serverSetPosition, testComponent.transform.position);
            AssertVectorsAreEquivalent(serverSetScale, testComponent.transform.localScale);
            AssertQuaternionsAreEquivalent(serverSetRotation, testComponent.transform.rotation);

            AssertVectorsAreEquivalent(serverSetPosition, testComponent.AnticipatedState.Position);
            AssertVectorsAreEquivalent(serverSetScale, testComponent.AnticipatedState.Scale);
            AssertQuaternionsAreEquivalent(serverSetRotation, testComponent.AnticipatedState.Rotation);

            AssertVectorsAreEquivalent(serverSetPosition, testComponent.AuthorityState.Position);
            AssertVectorsAreEquivalent(serverSetScale, testComponent.AuthorityState.Scale);
            AssertQuaternionsAreEquivalent(serverSetRotation, testComponent.AuthorityState.Rotation);

            AssertVectorsAreEquivalent(serverSetPosition, otherClientComponent.transform.position);
            AssertVectorsAreEquivalent(serverSetScale, otherClientComponent.transform.localScale);
            AssertQuaternionsAreEquivalent(serverSetRotation, otherClientComponent.transform.rotation);

            AssertVectorsAreEquivalent(serverSetPosition, otherClientComponent.AnticipatedState.Position);
            AssertVectorsAreEquivalent(serverSetScale, otherClientComponent.AnticipatedState.Scale);
            AssertQuaternionsAreEquivalent(serverSetRotation, otherClientComponent.AnticipatedState.Rotation);

            AssertVectorsAreEquivalent(serverSetPosition, otherClientComponent.AuthorityState.Position);
            AssertVectorsAreEquivalent(serverSetScale, otherClientComponent.AuthorityState.Scale);
            AssertQuaternionsAreEquivalent(serverSetRotation, otherClientComponent.AuthorityState.Rotation);
        }

        [Test]
        public void WhenServerChangesReanticipeValue_ValuesAreReanticiped()
        {
            var testComponent = GetTestComponent();
            var otherClientComponent = GetOtherClientComponent();
            testComponent.OnReanticipate = (transform, anticipedValue, anticipationTick, authorityValue, authorityTick) =>
            {
                transform.AnticipateMove(authorityValue.Position + new Vector3(0, 5, 0));
            };
            otherClientComponent.OnReanticipate = testComponent.OnReanticipate;

            var serverComponent = GetServerComponent();
            serverComponent.Interpolate = false;
            serverComponent.transform.position = new Vector3(0, 1, 2);

            WaitForConditionOrTimeOutWithTimeTravel(() => testComponent.AuthorityState.Position == serverComponent.transform.position && otherClientComponent.AuthorityState.Position == serverComponent.transform.position);

            Assert.AreEqual(new Vector3(0, 6, 2), testComponent.transform.position);
            Assert.AreEqual(new Vector3(0, 6, 2), testComponent.AnticipatedState.Position);
            Assert.AreEqual(new Vector3(0, 1, 2), testComponent.AuthorityState.Position);

            Assert.AreEqual(new Vector3(0, 6, 2), otherClientComponent.transform.position);
            Assert.AreEqual(new Vector3(0, 6, 2), otherClientComponent.AnticipatedState.Position);
            Assert.AreEqual(new Vector3(0, 1, 2), otherClientComponent.AuthorityState.Position);
        }

        [Test]
        public void WhenStaleDataArrivesToIgnoreVariable_ItIsIgnored()
        {
            var serverComponent = GetServerComponent();
            serverComponent.Interpolate = false;

            var testComponent = GetTestComponent();
            testComponent.StaleDataHandling = StaleDataHandling.Ignore;
            testComponent.Interpolate = false;

            var otherClientComponent = GetOtherClientComponent();
            otherClientComponent.StaleDataHandling = StaleDataHandling.Ignore;
            otherClientComponent.Interpolate = false;

            testComponent.AnticipateMove(new Vector3(0, 5, 0));
            serverComponent.transform.position = new Vector3(1, 2, 3);

            WaitForConditionOrTimeOutWithTimeTravel(() => testComponent.AuthorityState.Position == serverComponent.transform.position && otherClientComponent.AuthorityState.Position == serverComponent.transform.position);

            // Anticiped client received this data for a tick earlier than its anticipation, and should have prioritized the anticiped value
            Assert.AreEqual(new Vector3(0, 5, 0), testComponent.transform.position);
            Assert.AreEqual(new Vector3(0, 5, 0), testComponent.AnticipatedState.Position);
            // However, the authoritative value still gets updated
            Assert.AreEqual(new Vector3(1, 2, 3), testComponent.AuthorityState.Position);

            // Other client got the server value and had made no anticipation, so it applies it to the anticiped value as well.
            Assert.AreEqual(new Vector3(1, 2, 3), otherClientComponent.transform.position);
            Assert.AreEqual(new Vector3(1, 2, 3), otherClientComponent.AnticipatedState.Position);
            Assert.AreEqual(new Vector3(1, 2, 3), otherClientComponent.AuthorityState.Position);
        }
    }
}
