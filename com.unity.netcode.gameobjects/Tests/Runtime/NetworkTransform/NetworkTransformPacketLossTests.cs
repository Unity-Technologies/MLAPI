// TODO: Rewrite test to use the tools package. Debug simulator not available in UTP 2.X.
#if !UTP_TRANSPORT_2_0_ABOVE
using System.Collections;
using NUnit.Framework;
using Unity.Netcode.Components;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// Integration tests for NetworkTransform that will test both
    /// server and host operating modes and will test both authoritative
    /// models for each operating mode when packet loss and latency is
    /// present.
    /// </summary>
    [TestFixture(HostOrServer.DAHost, Authority.OwnerAuthority, RotationCompression.None, Rotation.Euler, Precision.Full)]
    [TestFixture(HostOrServer.DAHost, Authority.OwnerAuthority, RotationCompression.None, Rotation.Euler, Precision.Half)]
    [TestFixture(HostOrServer.DAHost, Authority.OwnerAuthority, RotationCompression.None, Rotation.Quaternion, Precision.Full)]
    [TestFixture(HostOrServer.DAHost, Authority.OwnerAuthority, RotationCompression.None, Rotation.Quaternion, Precision.Half)]
    [TestFixture(HostOrServer.DAHost, Authority.OwnerAuthority, RotationCompression.QuaternionCompress, Rotation.Quaternion, Precision.Full)]
    [TestFixture(HostOrServer.DAHost, Authority.OwnerAuthority, RotationCompression.QuaternionCompress, Rotation.Quaternion, Precision.Half)]
    [TestFixture(HostOrServer.Host, Authority.ServerAuthority, RotationCompression.None, Rotation.Euler, Precision.Full)]
    [TestFixture(HostOrServer.Host, Authority.ServerAuthority, RotationCompression.None, Rotation.Euler, Precision.Half)]
    [TestFixture(HostOrServer.Host, Authority.ServerAuthority, RotationCompression.None, Rotation.Quaternion, Precision.Full)]
    [TestFixture(HostOrServer.Host, Authority.ServerAuthority, RotationCompression.None, Rotation.Quaternion, Precision.Half)]
    [TestFixture(HostOrServer.Host, Authority.ServerAuthority, RotationCompression.QuaternionCompress, Rotation.Quaternion, Precision.Full)]
    [TestFixture(HostOrServer.Host, Authority.ServerAuthority, RotationCompression.QuaternionCompress, Rotation.Quaternion, Precision.Half)]
    internal class NetworkTransformPacketLossTests : NetworkTransformBase
    {
        private const int k_Latency = 50;
        private const int k_PacketLoss = 2;

        private Vector3 m_RandomPosition;
        private Vector3 m_TeleportOffset = new Vector3(-1024f, 0f, 0f);
        private bool m_Teleported;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="testWithHost">Determines if we are running as a server or host</param>
        /// <param name="authority">Determines if we are using server or owner authority</param>
        public NetworkTransformPacketLossTests(HostOrServer testWithHost, Authority authority, RotationCompression rotationCompression, Rotation rotation, Precision precision) :
            base(testWithHost, authority, rotationCompression, rotation, precision)
        { }

        protected override void OnServerAndClientsCreated()
        {
            base.OnServerAndClientsCreated();

            var unityTransport = m_ServerNetworkManager.NetworkConfig.NetworkTransport as Transports.UTP.UnityTransport;
            unityTransport.SetDebugSimulatorParameters(k_Latency, 0, k_PacketLoss);
        }

        /// <summary>
        /// Handles validating all children of the test objects have matching local and global space vaues.
        /// </summary>
        private IEnumerator AllChildrenLocalTransformValuesMatch(bool useSubChild, ChildrenTransformCheckType checkType)
        {
            // We don't assert on timeout here because we want to log this information during PostAllChildrenLocalTransformValuesMatch
            yield return WaitForConditionOrTimeOut(() => AllInstancesKeptLocalTransformValues(useSubChild));
            var success = true;
            if (s_GlobalTimeoutHelper.TimedOut)
            {
                var waitForMs = new WaitForSeconds(0.0025f);
                if (m_Precision == Precision.Half)
                {
                    m_CurrentHalfPrecision = 0.2156f;
                }
                // If we timed out, then wait for a full range of ticks to assure all data has been synchronized before declaring this a failed test.
                for (int j = 0; j < m_ServerNetworkManager.NetworkConfig.TickRate; j++)
                {
                    m_InfoMessage.Clear();
                    m_InfoMessage.AppendLine($"[{checkType}][{useSubChild}] Timed out waiting for all children to have the correct local space values:\n");
                    var instances = useSubChild ? ChildObjectComponent.SubInstances : ChildObjectComponent.Instances;
                    success = PostAllChildrenLocalTransformValuesMatch(useSubChild);
                    yield return waitForMs;
                }
            }

            if (!success)
            {
                Assert.True(success, m_InfoMessage.ToString());
            }
        }

        /// <summary>
        /// Validates that transform values remain the same when a NetworkTransform is
        /// parented under another NetworkTransform under all of the possible axial conditions
        /// as well as when the parent has a varying scale.
        /// </summary>
        [UnityTest]
        public IEnumerator ParentedNetworkTransformTest([Values] Interpolation interpolation, [Values] bool worldPositionStays, [Values(0.5f, 1.0f, 5.0f)] float scale)
        {
            ChildObjectComponent.EnableChildLog = m_EnableVerboseDebug;
            if (m_EnableVerboseDebug)
            {
                ChildObjectComponent.TestCount++;
            }
            // Get the NetworkManager that will have authority in order to spawn with the correct authority
            var isServerAuthority = m_Authority == Authority.ServerAuthority;
            var authorityNetworkManager = m_ServerNetworkManager;
            if (!isServerAuthority)
            {
                authorityNetworkManager = m_ClientNetworkManagers[0];
            }

            // Spawn a parent and children
            ChildObjectComponent.HasSubChild = true;
            var serverSideParent = SpawnObject(m_ParentObject.gameObject, authorityNetworkManager).GetComponent<NetworkObject>();
            var serverSideChild = SpawnObject(m_ChildObject.gameObject, authorityNetworkManager).GetComponent<NetworkObject>();
            var serverSideSubChild = SpawnObject(m_SubChildObject.gameObject, authorityNetworkManager).GetComponent<NetworkObject>();

            yield return s_DefaultWaitForTick;
            yield return s_DefaultWaitForTick;
            yield return s_DefaultWaitForTick;
            yield return s_DefaultWaitForTick;

            // Assure all of the child object instances are spawned before proceeding to parenting
            yield return WaitForConditionOrTimeOut(AllChildObjectInstancesAreSpawned);
            AssertOnTimeout("Timed out waiting for all child instances to be spawned!");

            // Get the authority parent and child instances
            m_AuthorityParentObject = NetworkTransformTestComponent.AuthorityInstance.NetworkObject;
            m_AuthorityChildObject = ChildObjectComponent.AuthorityInstance.NetworkObject;
            m_AuthoritySubChildObject = ChildObjectComponent.AuthoritySubInstance.NetworkObject;

            // The child NetworkTransform will use world space when world position stays and
            // local space when world position does not stay when parenting.
            ChildObjectComponent.AuthorityInstance.InLocalSpace = !worldPositionStays;
            ChildObjectComponent.AuthorityInstance.UseHalfFloatPrecision = m_Precision == Precision.Half;
            ChildObjectComponent.AuthorityInstance.UseQuaternionSynchronization = m_Rotation == Rotation.Quaternion;
            ChildObjectComponent.AuthorityInstance.UseQuaternionCompression = m_RotationCompression == RotationCompression.QuaternionCompress;

            ChildObjectComponent.AuthoritySubInstance.InLocalSpace = !worldPositionStays;
            ChildObjectComponent.AuthoritySubInstance.UseHalfFloatPrecision = m_Precision == Precision.Half;
            ChildObjectComponent.AuthoritySubInstance.UseQuaternionSynchronization = m_Rotation == Rotation.Quaternion;
            ChildObjectComponent.AuthoritySubInstance.UseQuaternionCompression = m_RotationCompression == RotationCompression.QuaternionCompress;

            // Set whether we are interpolating or not
            m_AuthorityParentNetworkTransform = m_AuthorityParentObject.GetComponent<NetworkTransformTestComponent>();
            m_AuthorityParentNetworkTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;
            m_AuthorityChildNetworkTransform = m_AuthorityChildObject.GetComponent<ChildObjectComponent>();
            m_AuthorityChildNetworkTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;
            m_AuthoritySubChildNetworkTransform = m_AuthoritySubChildObject.GetComponent<ChildObjectComponent>();
            m_AuthoritySubChildNetworkTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;


            // Apply a scale to the parent object to make sure the scale on the child is properly updated on
            // non-authority instances.
            var halfScale = scale * 0.5f;
            m_AuthorityParentObject.transform.localScale = GetRandomVector3(scale - halfScale, scale + halfScale);
            m_AuthorityChildObject.transform.localScale = GetRandomVector3(scale - halfScale, scale + halfScale);
            m_AuthoritySubChildObject.transform.localScale = GetRandomVector3(scale - halfScale, scale + halfScale);

            // Allow one tick for authority to update these changes

            yield return WaitForConditionOrTimeOut(PositionRotationScaleMatches);

            AssertOnTimeout("All transform values did not match prior to parenting!");

            // Parent the child under the parent with the current world position stays setting
            Assert.True(serverSideChild.TrySetParent(serverSideParent.transform, worldPositionStays), "[Server-Side Child] Failed to set child's parent!");

            // Parent the sub-child under the child with the current world position stays setting
            Assert.True(serverSideSubChild.TrySetParent(serverSideChild.transform, worldPositionStays), "[Server-Side SubChild] Failed to set sub-child's parent!");

            // This waits for all child instances to be parented
            yield return WaitForConditionOrTimeOut(AllChildObjectInstancesHaveChild);
            AssertOnTimeout("Timed out waiting for all instances to have parented a child!");
            var latencyWait = new WaitForSeconds(k_Latency * 0.003f);
            // Wait for at least 3x designated latency period
            yield return latencyWait;

            // This validates each child instance has preserved their local space values
            yield return AllChildrenLocalTransformValuesMatch(false, ChildrenTransformCheckType.Connected_Clients);

            // This validates each sub-child instance has preserved their local space values
            yield return AllChildrenLocalTransformValuesMatch(true, ChildrenTransformCheckType.Connected_Clients);

            // Verify that a late joining client will synchronize to the parented NetworkObjects properly
            yield return CreateAndStartNewClient();

            // Assure all of the child object instances are spawned (basically for the newly connected client)
            yield return WaitForConditionOrTimeOut(AllChildObjectInstancesAreSpawned);
            AssertOnTimeout("Timed out waiting for all child instances to be spawned!");

            // This waits for all child instances to be parented
            yield return WaitForConditionOrTimeOut(AllChildObjectInstancesHaveChild);
            AssertOnTimeout("Timed out waiting for all instances to have parented a child!");

            // Wait for at least 3x designated latency period
            yield return latencyWait;

            // This validates each child instance has preserved their local space values
            yield return AllChildrenLocalTransformValuesMatch(false, ChildrenTransformCheckType.Late_Join_Client);

            // This validates each sub-child instance has preserved their local space values
            yield return AllChildrenLocalTransformValuesMatch(true, ChildrenTransformCheckType.Late_Join_Client);
        }

        /// <summary>
        /// This validates that multiple changes can occur within the same tick or over
        /// several ticks while still keeping non-authoritative instances synchronized.
        /// </summary>
        /// <remarks>
        /// When testing < 3 axis: Interpolation is disabled and only 3 delta updates are applied per unique test
        /// When testing 3 axis: Interpolation is enabled, sometimes an axis is intentionally excluded during a
        /// delta update, and it runs through 8 delta updates per unique test.
        /// </remarks>
        [UnityTest]
        public IEnumerator NetworkTransformMultipleChangesOverTime([Values] TransformSpace testLocalTransform, [Values] Axis axis)
        {
            yield return s_DefaultWaitForTick;
            // Just test for OverrideState.Update (they are already being tested for functionality in normal NetworkTransformTests)
            var overideState = OverrideState.Update;
            var tickRelativeTime = new WaitForSeconds(1.0f / m_ServerNetworkManager.NetworkConfig.TickRate);
            m_AuthoritativeTransform.InLocalSpace = testLocalTransform == TransformSpace.Local;
            bool axisX = axis == Axis.X || axis == Axis.XY || axis == Axis.XZ || axis == Axis.XYZ;
            bool axisY = axis == Axis.Y || axis == Axis.XY || axis == Axis.YZ || axis == Axis.XYZ;
            bool axisZ = axis == Axis.Z || axis == Axis.XZ || axis == Axis.YZ || axis == Axis.XYZ;

            var axisCount = axisX ? 1 : 0;
            axisCount += axisY ? 1 : 0;
            axisCount += axisZ ? 1 : 0;

            m_AuthoritativeTransform.StatePushed = false;
            // Enable interpolation when all 3 axis are selected to make sure we are synchronizing properly
            // when interpolation is enabled.
            m_AuthoritativeTransform.Interpolate = axisCount == 3 ? true : false;

            m_CurrentAxis = axis;

            // Authority dictates what is synchronized and what the precision is going to be
            // so we only need to set this on the authoritative side.
            m_AuthoritativeTransform.UseHalfFloatPrecision = m_Precision == Precision.Half;
            m_AuthoritativeTransform.UseQuaternionSynchronization = m_Rotation == Rotation.Quaternion;
            m_AuthoritativeTransform.UseQuaternionCompression = m_RotationCompression == RotationCompression.QuaternionCompress;

            m_AuthoritativeTransform.SyncPositionX = axisX;
            m_AuthoritativeTransform.SyncPositionY = axisY;
            m_AuthoritativeTransform.SyncPositionZ = axisZ;

            if (!m_AuthoritativeTransform.UseQuaternionSynchronization)
            {
                m_AuthoritativeTransform.SyncRotAngleX = axisX;
                m_AuthoritativeTransform.SyncRotAngleY = axisY;
                m_AuthoritativeTransform.SyncRotAngleZ = axisZ;
            }
            else
            {
                // This is not required for usage (setting the value should not matter when quaternion synchronization is enabled)
                // but is required for this test so we don't get a failure on an axis that is marked to not be synchronized when
                // validating the authority's values on non-authority instances.
                m_AuthoritativeTransform.SyncRotAngleX = true;
                m_AuthoritativeTransform.SyncRotAngleY = true;
                m_AuthoritativeTransform.SyncRotAngleZ = true;
            }

            m_AuthoritativeTransform.SyncScaleX = axisX;
            m_AuthoritativeTransform.SyncScaleY = axisY;
            m_AuthoritativeTransform.SyncScaleZ = axisZ;

            var positionStart = GetRandomVector3(0.25f, 1.75f);
            var rotationStart = GetRandomVector3(1f, 15f);
            var scaleStart = GetRandomVector3(0.25f, 2.0f);
            var position = positionStart;
            var rotation = rotationStart;
            var scale = scaleStart;
            var success = false;


            // Wait for the deltas to be pushed
            yield return WaitForConditionOrTimeOut(() => m_AuthoritativeTransform.StatePushed);

            // Just in case we drop the first few state updates
            if (s_GlobalTimeoutHelper.TimedOut)
            {
                // Set the local state to not reflect the authority state's local space settings
                // to trigger the state update (it would eventually get there, but this is an integration test)
                var state = m_AuthoritativeTransform.LocalAuthoritativeNetworkState;
                state.InLocalSpace = !m_AuthoritativeTransform.InLocalSpace;
                m_AuthoritativeTransform.LocalAuthoritativeNetworkState = state;
                // Wait for the deltas to be pushed
                yield return WaitForConditionOrTimeOut(() => m_AuthoritativeTransform.StatePushed);
            }
            AssertOnTimeout("State was never pushed!");

            // Allow the precision settings to propagate first as changing precision
            // causes a teleport event to occur
            yield return s_DefaultWaitForTick;
            yield return s_DefaultWaitForTick;
            yield return s_DefaultWaitForTick;
            yield return s_DefaultWaitForTick;
            yield return s_DefaultWaitForTick;
            var iterations = axisCount == 3 ? k_PositionRotationScaleIterations3Axis : k_PositionRotationScaleIterations;

            // Move and rotate within the same tick, validate the non-authoritative instance updates
            // to each set of changes.  Repeat several times.
            for (int i = 0; i < iterations; i++)
            {
                // Always reset this per delta update pass
                m_AxisExcluded = false;
                var deltaPositionDelta = GetRandomVector3(-1.5f, 1.5f);
                var deltaRotationDelta = GetRandomVector3(-3.5f, 3.5f);
                var deltaScaleDelta = GetRandomVector3(-0.5f, 0.5f);

                m_NonAuthoritativeTransform.StateUpdated = false;
                m_AuthoritativeTransform.StatePushed = false;

                // With two or more axis, excluding one of them while chaging another will validate that
                // full precision updates are maintaining their target state value(s) to interpolate towards
                if (axisCount == 3)
                {
                    position += RandomlyExcludeAxis(deltaPositionDelta);
                    rotation += RandomlyExcludeAxis(deltaRotationDelta);
                    scale += RandomlyExcludeAxis(deltaScaleDelta);
                }
                else
                {
                    position += deltaPositionDelta;
                    rotation += deltaRotationDelta;
                    scale += deltaScaleDelta;
                }

                // Apply delta between ticks
                MoveRotateAndScaleAuthority(position, rotation, scale, overideState);

                // Wait for the deltas to be pushed (unlike the original test, we don't wait for state to be updated as that could be dropped here)
                yield return WaitForConditionOrTimeOut(() => m_AuthoritativeTransform.StatePushed);
                AssertOnTimeout($"[Non-Interpolate {i}] Timed out waiting for state to be pushed ({m_AuthoritativeTransform.StatePushed})!");

                // For 3 axis, we will skip validating that the non-authority interpolates to its target point at least once.
                // This will validate that non-authoritative updates are maintaining their target state axis values if only 2
                // of the axis are being updated to assure interpolation maintains the targeted axial value per axis.
                // For 2 and 1 axis tests we always validate per delta update
                if (m_AxisExcluded || axisCount < 3)
                {
                    // Wait for deltas to synchronize on non-authoritative side
                    yield return WaitForConditionOrTimeOut(PositionRotationScaleMatches);
                    // Provide additional debug info about what failed (if it fails)
                    if (s_GlobalTimeoutHelper.TimedOut)
                    {
                        Debug.Log("[Synch Issue Start - 1]");
                        // If we timed out, then wait for a full range of ticks (plus 1) to assure it sent synchronization data.
                        for (int j = 0; j < m_ServerNetworkManager.NetworkConfig.TickRate * 2; j++)
                        {
                            success = PositionRotationScaleMatches();
                            if (success)
                            {
                                // If we matched, then something was dropped and recovered when synchronized
                                break;
                            }
                            yield return s_DefaultWaitForTick;
                        }

                        // Only if we still didn't match
                        if (!success)
                        {
                            m_EnableVerboseDebug = true;
                            success = PositionRotationScaleMatches();
                            m_EnableVerboseDebug = false;
                            Debug.Log("[Synch Issue END - 1]");
                            AssertOnTimeout($"[Non-Interpolate {i}] Timed out waiting for non-authority to match authority's position or rotation");
                        }
                    }
                }
            }

            if (axisCount == 3)
            {
                // As a final test, wait for deltas to synchronize on non-authoritative side to assure it interpolates to the correct values
                yield return WaitForConditionOrTimeOut(PositionRotationScaleMatches);
                // Provide additional debug info about what failed (if it fails)
                if (s_GlobalTimeoutHelper.TimedOut)
                {
                    Debug.Log("[Synch Issue Start - 2]");
                    // If we timed out, then wait for a full range of ticks (plus 1) to assure it sent synchronization data.
                    for (int j = 0; j < m_ServerNetworkManager.NetworkConfig.TickRate * 2; j++)
                    {
                        success = PositionRotationScaleMatches();
                        if (success)
                        {
                            // If we matched, then something was dropped and recovered when synchronized
                            break;
                        }
                        yield return s_DefaultWaitForTick;
                    }

                    // Only if we still didn't match
                    if (!success)
                    {
                        m_EnableVerboseDebug = true;
                        PositionRotationScaleMatches();
                        m_EnableVerboseDebug = false;
                        Debug.Log("[Synch Issue END - 2]");
                        AssertOnTimeout("Timed out waiting for non-authority to match authority's position or rotation");

                    }
                }

            }
        }

        /// <summary>
        /// Tests changing all axial values one at a time with packet loss
        /// These tests are performed:
        /// - While in local space and world space
        /// - While interpolation is enabled and disabled
        /// </summary>
        [UnityTest]
        public IEnumerator TestAuthoritativeTransformChangeOneAtATime([Values] TransformSpace testLocalTransform, [Values] Interpolation interpolation)
        {
            // Just test for OverrideState.Update (they are already being tested for functionality in normal NetworkTransformTests)
            m_AuthoritativeTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;
            m_AuthoritativeTransform.InLocalSpace = testLocalTransform == TransformSpace.Local;
            m_AuthoritativeTransform.UseQuaternionCompression = m_RotationCompression == RotationCompression.QuaternionCompress;
            m_AuthoritativeTransform.UseHalfFloatPrecision = m_Precision == Precision.Half;
            m_AuthoritativeTransform.UseQuaternionSynchronization = m_Rotation == Rotation.Quaternion;
            m_NonAuthoritativeTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;


            // test position
            var authPlayerTransform = m_AuthoritativeTransform.transform;

            Assert.AreEqual(Vector3.zero, m_NonAuthoritativeTransform.transform.position, "server side pos should be zero at first"); // sanity check

            m_AuthoritativeTransform.transform.position = GetRandomVector3(2f, 30f);

            yield return WaitForConditionOrTimeOut(() => PositionsMatch());
            AssertOnTimeout($"Timed out waiting for positions to match {m_AuthoritativeTransform.transform.position} | {m_NonAuthoritativeTransform.transform.position}");

            // test rotation
            Assert.AreEqual(Quaternion.identity, m_NonAuthoritativeTransform.transform.rotation, "wrong initial value for rotation"); // sanity check

            m_AuthoritativeTransform.transform.rotation = Quaternion.Euler(GetRandomVector3(5, 60)); // using euler angles instead of quaternions directly to really see issues users might encounter

            // Make sure the values match
            yield return WaitForConditionOrTimeOut(() => RotationsMatch());
            AssertOnTimeout($"Timed out waiting for rotations to match");

            m_AuthoritativeTransform.StatePushed = false;
            m_AuthoritativeTransform.transform.localScale = GetRandomVector3(1, 6);

            // Make sure the scale values match
            yield return WaitForConditionOrTimeOut(() => ScaleValuesMatch());
            AssertOnTimeout($"Timed out waiting for scale values to match");
        }

        [UnityTest]
        public IEnumerator TestSameFrameDeltaStateAndTeleport([Values] TransformSpace testLocalTransform, [Values] Interpolation interpolation)
        {
            m_AuthoritativeTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;

            m_NonAuthoritativeTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;

            m_AuthoritativeTransform.InLocalSpace = testLocalTransform == TransformSpace.Local;

            // test position
            var authPlayerTransform = m_AuthoritativeTransform.transform;

            Assert.AreEqual(Vector3.zero, m_NonAuthoritativeTransform.transform.position, "server side pos should be zero at first"); // sanity check

            m_AuthoritativeTransform.AuthorityPushedTransformState += OnAuthorityPushedTransformState;
            m_RandomPosition = GetRandomVector3(2f, 30f);
            m_AuthoritativeTransform.transform.position = m_RandomPosition;
            m_Teleported = false;
            yield return WaitForConditionOrTimeOut(() => m_Teleported);
            AssertOnTimeout($"Timed out waiting for random position to be pushed!");

            yield return WaitForConditionOrTimeOut(() => PositionsMatch());
            AssertOnTimeout($"Timed out waiting for positions to match {m_AuthoritativeTransform.transform.position} | {m_NonAuthoritativeTransform.transform.position}");

            var authPosition = m_AuthoritativeTransform.GetSpaceRelativePosition();
            var nonAuthPosition = m_NonAuthoritativeTransform.GetSpaceRelativePosition();

            var finalPosition = m_TeleportOffset + m_RandomPosition;
            Assert.True(Approximately(authPosition, finalPosition), $"Authority did not set its position ({authPosition}) to the teleport position ({finalPosition})!");
            Assert.True(Approximately(nonAuthPosition, finalPosition), $"NonAuthority did not set its position ({nonAuthPosition}) to the teleport position ({finalPosition})!");
        }

        /// <summary>
        /// For the TestSameFrameDeltaStateAndTeleport test, we want to teleport on the same frame that we had a delta state update when
        /// using unreliable delta state updates (i.e. we want the unreliable packet to be sent first and then the teleport to be sent on
        /// the next tick. Store off both states when invoked
        /// </summary>
        /// <param name="networkTransformState"></param>
        private void OnAuthorityPushedTransformState(ref NetworkTransform.NetworkTransformState networkTransformState)
        {
            // Match the first position update
            if (Approximately(m_RandomPosition, networkTransformState.GetPosition()))
            {
                // Teleport to the m_RandomPosition plus the
                m_AuthoritativeTransform.SetState(m_TeleportOffset + m_RandomPosition, null, null, false);
                m_AuthoritativeTransform.AuthorityPushedTransformState -= OnAuthorityPushedTransformState;
                m_Teleported = true;
            }
        }
    }
}
#endif
