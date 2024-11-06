using NUnit.Framework;
using Unity.Netcode.Components;
using UnityEngine;

namespace Unity.Netcode.RuntimeTests
{
    [TestFixture(HostOrServer.Host, Authority.OwnerAuthority)]
    [TestFixture(HostOrServer.Host, Authority.ServerAuthority)]
    internal class NetworkTransformGeneral : NetworkTransformBase
    {
        public NetworkTransformGeneral(HostOrServer testWithHost, Authority authority) :
            base(testWithHost, authority, RotationCompression.None, Rotation.Euler, Precision.Full)
        { }

        protected override bool m_EnableTimeTravel => true;
        protected override bool m_SetupIsACoroutine => false;
        protected override bool m_TearDownIsACoroutine => false;

        /// <summary>
        /// Test to verify nonAuthority cannot change the transform directly
        /// </summary>
        [Test]
        public void VerifyNonAuthorityCantChangeTransform([Values] Interpolation interpolation)
        {
            m_AuthoritativeTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;
            m_NonAuthoritativeTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;

            Assert.AreEqual(Vector3.zero, m_NonAuthoritativeTransform.transform.position, "other side pos should be zero at first"); // sanity check

            m_NonAuthoritativeTransform.transform.position = new Vector3(4, 5, 6);

            TimeTravelAdvanceTick();
            TimeTravelAdvanceTick();

            Assert.AreEqual(Vector3.zero, m_NonAuthoritativeTransform.transform.position, "[Position] NonAuthority was able to change the position!");

            var nonAuthorityRotation = m_NonAuthoritativeTransform.transform.rotation;
            var originalNonAuthorityEulerRotation = nonAuthorityRotation.eulerAngles;
            var nonAuthorityEulerRotation = originalNonAuthorityEulerRotation;
            // Verify rotation is not marked dirty when rotated by half of the threshold
            nonAuthorityEulerRotation.y += 20.0f;
            nonAuthorityRotation.eulerAngles = nonAuthorityEulerRotation;
            m_NonAuthoritativeTransform.transform.rotation = nonAuthorityRotation;
            TimeTravelAdvanceTick();
            var nonAuthorityCurrentEuler = m_NonAuthoritativeTransform.transform.rotation.eulerAngles;
            Assert.True(originalNonAuthorityEulerRotation.Equals(nonAuthorityCurrentEuler), "[Rotation] NonAuthority was able to change the rotation!");

            var nonAuthorityScale = m_NonAuthoritativeTransform.transform.localScale;
            m_NonAuthoritativeTransform.transform.localScale = nonAuthorityScale * 100;

            TimeTravelAdvanceTick();

            Assert.True(nonAuthorityScale.Equals(m_NonAuthoritativeTransform.transform.localScale), "[Scale] NonAuthority was able to change the scale!");
        }

        /// <summary>
        /// Validates that rotation checks don't produce false positive
        /// results when rolling over between 0 and 360 degrees
        /// </summary>
        [Test]
        public void TestRotationThresholdDeltaCheck([Values] Interpolation interpolation)
        {
            m_AuthoritativeTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;
            m_NonAuthoritativeTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;
            m_NonAuthoritativeTransform.RotAngleThreshold = m_AuthoritativeTransform.RotAngleThreshold = 5.0f;
            var halfThreshold = m_AuthoritativeTransform.RotAngleThreshold * 0.5001f;

            // Apply the current state prior to getting reference rotations which assures we have
            // applied the most current rotation deltas and that all bitset flags are updated
            var results = m_AuthoritativeTransform.ApplyState();
            TimeTravelAdvanceTick();

            // Get the current rotation values;
            var authorityRotation = m_AuthoritativeTransform.transform.rotation;
            var authorityEulerRotation = authorityRotation.eulerAngles;

            // Verify rotation is not marked dirty when rotated by half of the threshold
            authorityEulerRotation.y += halfThreshold;
            authorityRotation.eulerAngles = authorityEulerRotation;
            m_AuthoritativeTransform.transform.rotation = authorityRotation;
            results = m_AuthoritativeTransform.ApplyState();
            Assert.IsFalse(results.isRotationDirty, $"Rotation is dirty when rotation threshold is {m_AuthoritativeTransform.RotAngleThreshold} degrees and only adjusted by {halfThreshold} degrees!");
            // Now allow the delta state to be processed and sent (just allow for two ticks to cover edge cases with time travel and the testing environment)
            TimeTravelAdvanceTick();
            TimeTravelAdvanceTick();

            // Verify rotation is marked dirty when rotated by another half threshold value
            authorityEulerRotation.y += halfThreshold;
            authorityRotation.eulerAngles = authorityEulerRotation;
            m_AuthoritativeTransform.transform.rotation = authorityRotation;
            results = m_AuthoritativeTransform.ApplyState();
            Assert.IsTrue(results.isRotationDirty, $"Rotation was not dirty when rotated by the threshold value: {m_AuthoritativeTransform.RotAngleThreshold} degrees!");
            // Now allow the delta state to be processed and sent (just allow for two ticks to cover edge cases with time travel and the testing environment)
            TimeTravelAdvanceTick();
            TimeTravelAdvanceTick();

            //Reset rotation back to zero on all axis
            authorityRotation.eulerAngles = authorityEulerRotation = Vector3.zero;
            m_AuthoritativeTransform.transform.rotation = authorityRotation;
            // Now allow the delta state to be processed and sent (just allow for two ticks to cover edge cases with time travel and the testing environment)
            TimeTravelAdvanceTick();
            TimeTravelAdvanceTick();

            // Rotate by 360 minus halfThreshold (which is really just negative halfThreshold) and verify rotation is not marked dirty
            authorityEulerRotation.y = 360 - halfThreshold;
            authorityRotation.eulerAngles = authorityEulerRotation;
            m_AuthoritativeTransform.transform.rotation = authorityRotation;
            results = m_AuthoritativeTransform.ApplyState();
            Assert.IsFalse(results.isRotationDirty, $"Rotation is dirty when rotation threshold is {m_AuthoritativeTransform.RotAngleThreshold} degrees and only adjusted by " +
                $"{Mathf.DeltaAngle(0, authorityEulerRotation.y)} degrees!");
            // Now allow the delta state to be processed and sent (just allow for two ticks to cover edge cases with time travel and the testing environment)
            TimeTravelAdvanceTick();
            TimeTravelAdvanceTick();

            // Now apply one more minor decrement that should trigger a dirty flag
            authorityEulerRotation.y -= halfThreshold;
            authorityRotation.eulerAngles = authorityEulerRotation;
            m_AuthoritativeTransform.transform.rotation = authorityRotation;
            results = m_AuthoritativeTransform.ApplyState();
            Assert.IsTrue(results.isRotationDirty, $"Rotation was not dirty when rotated by {Mathf.DeltaAngle(0, authorityEulerRotation.y)} degrees!");

            //Reset rotation back to zero on all axis
            authorityRotation.eulerAngles = authorityEulerRotation = Vector3.zero;
            m_AuthoritativeTransform.transform.rotation = authorityRotation;
            // Now allow the delta state to be processed and sent (just allow for two ticks to cover edge cases with time travel and the testing environment)
            TimeTravelAdvanceTick();
            TimeTravelAdvanceTick();

            // Minor decrement again under the threshold value
            authorityEulerRotation.y -= halfThreshold;
            authorityRotation.eulerAngles = authorityEulerRotation;
            m_AuthoritativeTransform.transform.rotation = authorityRotation;
            results = m_AuthoritativeTransform.ApplyState();
            Assert.IsFalse(results.isRotationDirty, $"Rotation is dirty when rotation threshold is {m_AuthoritativeTransform.RotAngleThreshold} degrees and only adjusted by " +
                $"{Mathf.DeltaAngle(0, authorityEulerRotation.y)} degrees!");
            // Now allow the delta state to be processed and sent (just allow for two ticks to cover edge cases with time travel and the testing environment)
            TimeTravelAdvanceTick();
            TimeTravelAdvanceTick();

            // Now decrement another half threshold which should trigger the dirty flag
            authorityEulerRotation.y -= halfThreshold;
            authorityRotation.eulerAngles = authorityEulerRotation;
            m_AuthoritativeTransform.transform.rotation = authorityRotation;
            results = m_AuthoritativeTransform.ApplyState();
            Assert.IsTrue(results.isRotationDirty, $"Rotation was not dirty when rotated by {Mathf.DeltaAngle(0, authorityEulerRotation.y)} degrees!");
        }

        private bool ValidateBitSetValues()
        {
            var serverState = m_AuthoritativeTransform.AuthorityLastSentState;
            var clientState = m_NonAuthoritativeTransform.LocalAuthoritativeNetworkState;
            if (serverState.HasPositionX == clientState.HasPositionX && serverState.HasPositionY == clientState.HasPositionY && serverState.HasPositionZ == clientState.HasPositionZ &&
                serverState.HasRotAngleX == clientState.HasRotAngleX && serverState.HasRotAngleY == clientState.HasRotAngleY && serverState.HasRotAngleZ == clientState.HasRotAngleZ &&
                serverState.HasScaleX == clientState.HasScaleX && serverState.HasScaleY == clientState.HasScaleY && serverState.HasScaleZ == clientState.HasScaleZ)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Test to make sure that the bitset value is updated properly
        /// </summary>
        [Test]
        public void TestBitsetValue([Values] Interpolation interpolation)
        {
            m_AuthoritativeTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;
            m_NonAuthoritativeTransform.RotAngleThreshold = m_AuthoritativeTransform.RotAngleThreshold = 0.1f;
            m_AuthoritativeTransform.transform.rotation = Quaternion.Euler(1, 2, 3);
            TimeTravelAdvanceTick();
            var success = WaitForConditionOrTimeOutWithTimeTravel(ValidateBitSetValues);
            Assert.True(success, $"Timed out waiting for Authoritative Bitset state to equal NonAuthoritative replicated Bitset state!");
            success = WaitForConditionOrTimeOutWithTimeTravel(() => RotationsMatch());
            Assert.True(success, $"[Timed-Out] Authoritative rotation {m_AuthoritativeTransform.transform.rotation.eulerAngles} != Non-Authoritative rotation {m_NonAuthoritativeTransform.transform.rotation.eulerAngles}");
        }

        /// <summary>
        /// This validates that you can perform multiple explicit <see cref="NetworkTransform.SetState(Vector3?, Quaternion?, Vector3?, bool)"/> calls
        /// within the same fractional tick period without the loss of the states applied.
        /// </summary>
        [Test]
        public void TestMultipleExplicitSetStates([Values] Interpolation interpolation)
        {
            var interpolate = interpolation == Interpolation.EnableInterpolate;
            m_AuthoritativeTransform.Interpolate = interpolate;
            var updatedPosition = GetRandomVector3(-5.0f, 5.0f);
            m_AuthoritativeTransform.SetState(updatedPosition, null, null, !interpolate);
            // Advance to next frame
            TimeTravel(0.001f, 1);

            updatedPosition += GetRandomVector3(-5.0f, 5.0f);
            m_AuthoritativeTransform.SetState(updatedPosition, null, null, !interpolate);
            TimeTravelAdvanceTick();

            var success = WaitForConditionOrTimeOutWithTimeTravel(() => PositionsMatch());
            Assert.True(success, $"[Timed-Out] Authoritative position {m_AuthoritativeTransform.transform.position} != Non-Authoritative position {m_NonAuthoritativeTransform.transform.position}");
            Assert.True(Approximately(updatedPosition, m_NonAuthoritativeTransform.transform.position), $"NonAuthority position {m_NonAuthoritativeTransform.transform.position} does not equal the calculated position {updatedPosition}!");

            var updatedRotation = m_AuthoritativeTransform.transform.rotation;
            updatedRotation.eulerAngles += GetRandomVector3(-30.0f, 30.0f);
            m_AuthoritativeTransform.SetState(null, updatedRotation, null, !interpolate);
            // Advance to next frame
            TimeTravel(0.001f, 1);

            updatedRotation.eulerAngles += GetRandomVector3(-30.0f, 30.0f);
            m_AuthoritativeTransform.SetState(null, updatedRotation, null, !interpolate);
            TimeTravelAdvanceTick();

            success = WaitForConditionOrTimeOutWithTimeTravel(() => RotationsMatch());
            Assert.True(success, $"[Timed-Out] Authoritative rotation {m_AuthoritativeTransform.transform.rotation.eulerAngles} != Non-Authoritative rotation {m_NonAuthoritativeTransform.transform.rotation.eulerAngles}");
            Assert.True(Approximately(updatedRotation.eulerAngles, m_NonAuthoritativeTransform.transform.rotation.eulerAngles), $"NonAuthority rotation {m_NonAuthoritativeTransform.transform.rotation.eulerAngles} does not equal the calculated rotation {updatedRotation.eulerAngles}!");

            var updatedScale = m_AuthoritativeTransform.transform.localScale;
            updatedScale += GetRandomVector3(-2.0f, 2.0f);
            m_AuthoritativeTransform.SetState(null, null, updatedScale, !interpolate);
            // Advance to next frame
            TimeTravel(0.001f, 1);

            updatedScale += GetRandomVector3(-2.0f, 2.0f);
            m_AuthoritativeTransform.SetState(null, null, updatedScale, !interpolate);
            TimeTravelAdvanceTick();

            success = WaitForConditionOrTimeOutWithTimeTravel(() => ScaleValuesMatch());
            Assert.True(success, $"[Timed-Out] Authoritative rotation {m_AuthoritativeTransform.transform.localScale} != Non-Authoritative rotation {m_NonAuthoritativeTransform.transform.localScale}");
            Assert.True(Approximately(updatedScale, m_NonAuthoritativeTransform.transform.localScale), $"NonAuthority scale {m_NonAuthoritativeTransform.transform.localScale} does not equal the calculated scale {updatedScale}!");

            // Now test explicitly setting all axis of transform multiple times during a fractional tick period
            updatedPosition += GetRandomVector3(-5.0f, 5.0f);
            updatedRotation.eulerAngles += GetRandomVector3(-30.0f, 30.0f);
            updatedScale += GetRandomVector3(-2.0f, 2.0f);
            m_AuthoritativeTransform.SetState(updatedPosition, updatedRotation, updatedScale, !interpolate);
            // Advance to next frame
            TimeTravel(0.001f, 1);

            updatedPosition += GetRandomVector3(-5.0f, 5.0f);
            updatedRotation.eulerAngles += GetRandomVector3(-30.0f, 30.0f);
            updatedScale += GetRandomVector3(-2.0f, 2.0f);
            m_AuthoritativeTransform.SetState(updatedPosition, updatedRotation, updatedScale, !interpolate);
            // Advance to next frame
            TimeTravel(0.001f, 1);

            updatedPosition += GetRandomVector3(-5.0f, 5.0f);
            updatedRotation.eulerAngles += GetRandomVector3(-30.0f, 30.0f);
            updatedScale += GetRandomVector3(-2.0f, 2.0f);
            m_AuthoritativeTransform.SetState(updatedPosition, updatedRotation, updatedScale, !interpolate);
            // Advance to next frame
            TimeTravel(0.001f, 1);

            TimeTravelAdvanceTick();

            success = WaitForConditionOrTimeOutWithTimeTravel(() => PositionsMatch() && RotationsMatch() && ScaleValuesMatch());
            Assert.True(success, $"[Timed-Out] Authoritative transform != Non-Authoritative transform!");
            Assert.True(Approximately(updatedPosition, m_NonAuthoritativeTransform.transform.position), $"NonAuthority position {m_NonAuthoritativeTransform.transform.position} does not equal the calculated position {updatedPosition}!");
            Assert.True(Approximately(updatedRotation.eulerAngles, m_NonAuthoritativeTransform.transform.rotation.eulerAngles), $"NonAuthority rotation {m_NonAuthoritativeTransform.transform.rotation.eulerAngles} does not equal the calculated rotation {updatedRotation.eulerAngles}!");
            Assert.True(Approximately(updatedScale, m_NonAuthoritativeTransform.transform.localScale), $"NonAuthority scale {m_NonAuthoritativeTransform.transform.localScale} does not equal the calculated scale {updatedScale}!");
        }

        /// <summary>
        /// This test validates the <see cref="NetworkTransform.SetState(Vector3?, Quaternion?, Vector3?, bool)"/> method
        /// usage for the non-authoritative side.  It will either be the owner or the server making/requesting state changes.
        /// This validates that:
        /// - The owner authoritative mode can still be controlled by the server (i.e. owner authoritative with server authority override capabilities)
        /// - The server authoritative mode can still be directed by the client owner.
        /// </summary>
        /// <remarks>
        /// This also tests that the original server authoritative model with client-owner driven NetworkTransforms is preserved.
        /// </remarks>
        [Test]
        public void NonAuthorityOwnerSettingStateTest([Values] Interpolation interpolation)
        {
            var interpolate = interpolation != Interpolation.EnableInterpolate;
            m_AuthoritativeTransform.Interpolate = interpolate;
            m_NonAuthoritativeTransform.Interpolate = interpolate;
            m_NonAuthoritativeTransform.RotAngleThreshold = m_AuthoritativeTransform.RotAngleThreshold = 0.1f;

            // Test one parameter at a time first
            var newPosition = new Vector3(125f, 35f, 65f);
            var newRotation = Quaternion.Euler(1, 2, 3);
            var newScale = new Vector3(2.0f, 2.0f, 2.0f);
            m_NonAuthoritativeTransform.SetState(newPosition, null, null, interpolate);
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => PositionsMatchesValue(newPosition));
            Assert.True(success, $"Timed out waiting for non-authoritative position state request to be applied!");
            Assert.True(Approximately(newPosition, m_AuthoritativeTransform.transform.position), "Authoritative position does not match!");
            Assert.True(Approximately(newPosition, m_NonAuthoritativeTransform.transform.position), "Non-Authoritative position does not match!");

            m_NonAuthoritativeTransform.SetState(null, newRotation, null, interpolate);
            success = WaitForConditionOrTimeOutWithTimeTravel(() => RotationMatchesValue(newRotation.eulerAngles));
            Assert.True(success, $"Timed out waiting for non-authoritative rotation state request to be applied!");
            Assert.True(Approximately(newRotation.eulerAngles, m_AuthoritativeTransform.transform.rotation.eulerAngles), "Authoritative rotation does not match!");
            Assert.True(Approximately(newRotation.eulerAngles, m_NonAuthoritativeTransform.transform.rotation.eulerAngles), "Non-Authoritative rotation does not match!");

            m_NonAuthoritativeTransform.SetState(null, null, newScale, interpolate);
            success = WaitForConditionOrTimeOutWithTimeTravel(() => ScaleMatchesValue(newScale));
            Assert.True(success, $"Timed out waiting for non-authoritative scale state request to be applied!");
            Assert.True(Approximately(newScale, m_AuthoritativeTransform.transform.localScale), "Authoritative scale does not match!");
            Assert.True(Approximately(newScale, m_NonAuthoritativeTransform.transform.localScale), "Non-Authoritative scale does not match!");

            // Test all parameters at once
            newPosition = new Vector3(55f, 95f, -25f);
            newRotation = Quaternion.Euler(20, 5, 322);
            newScale = new Vector3(0.5f, 0.5f, 0.5f);

            m_NonAuthoritativeTransform.SetState(newPosition, newRotation, newScale, interpolate);
            success = WaitForConditionOrTimeOutWithTimeTravel(() => PositionRotationScaleMatches(newPosition, newRotation.eulerAngles, newScale));
            Assert.True(success, $"Timed out waiting for non-authoritative position, rotation, and scale state request to be applied!");
            Assert.True(Approximately(newPosition, m_AuthoritativeTransform.transform.position), "Authoritative position does not match!");
            Assert.True(Approximately(newPosition, m_NonAuthoritativeTransform.transform.position), "Non-Authoritative position does not match!");
            Assert.True(Approximately(newRotation.eulerAngles, m_AuthoritativeTransform.transform.rotation.eulerAngles), "Authoritative rotation does not match!");
            Assert.True(Approximately(newRotation.eulerAngles, m_NonAuthoritativeTransform.transform.rotation.eulerAngles), "Non-Authoritative rotation does not match!");
            Assert.True(Approximately(newScale, m_AuthoritativeTransform.transform.localScale), "Authoritative scale does not match!");
            Assert.True(Approximately(newScale, m_NonAuthoritativeTransform.transform.localScale), "Non-Authoritative scale does not match!");
        }
    }
}
