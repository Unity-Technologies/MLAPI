using System.Collections;
using NUnit.Framework;
using UnityEngine;

namespace Unity.Netcode.RuntimeTests
{
    /// <summary>
    /// Integration tests for NetworkTransform that will test both
    /// server and host operating modes and will test both authoritative
    /// models for each operating mode.
    /// </summary>
    [TestFixture(HostOrServer.DAHost, Authority.OwnerAuthority, RotationCompression.None, Rotation.Euler, Precision.Full)]
#if !MULTIPLAYER_TOOLS
    [TestFixture(HostOrServer.DAHost, Authority.OwnerAuthority, RotationCompression.None, Rotation.Euler, Precision.Half)]
    [TestFixture(HostOrServer.DAHost, Authority.OwnerAuthority, RotationCompression.None, Rotation.Quaternion, Precision.Full)]
    [TestFixture(HostOrServer.DAHost, Authority.OwnerAuthority, RotationCompression.None, Rotation.Quaternion, Precision.Half)]
    [TestFixture(HostOrServer.DAHost, Authority.OwnerAuthority, RotationCompression.QuaternionCompress, Rotation.Quaternion, Precision.Full)]
    [TestFixture(HostOrServer.DAHost, Authority.OwnerAuthority, RotationCompression.QuaternionCompress, Rotation.Quaternion, Precision.Half)]
#endif
    [TestFixture(HostOrServer.Host, Authority.ServerAuthority, RotationCompression.None, Rotation.Euler, Precision.Full)]
#if !MULTIPLAYER_TOOLS
    [TestFixture(HostOrServer.Host, Authority.ServerAuthority, RotationCompression.None, Rotation.Euler, Precision.Half)]
    [TestFixture(HostOrServer.Host, Authority.ServerAuthority, RotationCompression.None, Rotation.Quaternion, Precision.Full)]
    [TestFixture(HostOrServer.Host, Authority.ServerAuthority, RotationCompression.None, Rotation.Quaternion, Precision.Half)]
    [TestFixture(HostOrServer.Host, Authority.ServerAuthority, RotationCompression.QuaternionCompress, Rotation.Quaternion, Precision.Full)]
    [TestFixture(HostOrServer.Host, Authority.ServerAuthority, RotationCompression.QuaternionCompress, Rotation.Quaternion, Precision.Half)]
#endif
    [TestFixture(HostOrServer.Host, Authority.OwnerAuthority, RotationCompression.None, Rotation.Euler, Precision.Full)]
#if !MULTIPLAYER_TOOLS
    [TestFixture(HostOrServer.Host, Authority.OwnerAuthority, RotationCompression.None, Rotation.Euler, Precision.Half)]
    [TestFixture(HostOrServer.Host, Authority.OwnerAuthority, RotationCompression.None, Rotation.Quaternion, Precision.Full)]
    [TestFixture(HostOrServer.Host, Authority.OwnerAuthority, RotationCompression.None, Rotation.Quaternion, Precision.Half)]
    [TestFixture(HostOrServer.Host, Authority.OwnerAuthority, RotationCompression.QuaternionCompress, Rotation.Quaternion, Precision.Full)]
    [TestFixture(HostOrServer.Host, Authority.OwnerAuthority, RotationCompression.QuaternionCompress, Rotation.Quaternion, Precision.Half)]
#endif
    internal class NetworkTransformTests : NetworkTransformBase
    {
        protected const int k_TickRate = 60;
        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="hostOrServer">Determines if we are running as a server or host</param>
        /// <param name="authority">Determines if we are using server or owner authority</param>
        public NetworkTransformTests(HostOrServer testWithHost, Authority authority, RotationCompression rotationCompression, Rotation rotation, Precision precision) :
            base(testWithHost, authority, rotationCompression, rotation, precision)
        { }

        protected override bool m_EnableTimeTravel => true;
        protected override bool m_SetupIsACoroutine => false;
        protected override bool m_TearDownIsACoroutine => false;

        protected override uint GetTickRate()
        {
            return k_TickRate;
        }

        private bool m_UseParentingThreshold;
        private const float k_ParentingThreshold = 0.25f;

        protected override float GetDeltaVarianceThreshold()
        {
            if (m_UseParentingThreshold)
            {
                return k_ParentingThreshold;
            }
            return base.GetDeltaVarianceThreshold();
        }

        protected override IEnumerator OnSetup()
        {
            m_UseParentingThreshold = false;
            return base.OnSetup();
        }

        /// <summary>
        /// Handles validating the local space values match the original local space values.
        /// If not, it generates a message containing the axial values that did not match
        /// the target/start local space values.
        /// </summary>
        private void AllChildrenLocalTransformValuesMatch(bool useSubChild, ChildrenTransformCheckType checkType)
        {
            // We don't assert on timeout here because we want to log this information during PostAllChildrenLocalTransformValuesMatch
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => AllInstancesKeptLocalTransformValues(useSubChild), (int)GetTickRate() * 2);
            m_InfoMessage.Clear();
            m_InfoMessage.AppendLine($"[{checkType}][{useSubChild}] Timed out waiting for all children to have the correct local space values:\n");
            if (!success)
            {
                // If we timed out, then wait for 4 ticks to assure all data has been synchronized before declaring this a failed test.
                for (int j = 0; j < 4; j++)
                {
                    var instances = useSubChild ? ChildObjectComponent.SubInstances : ChildObjectComponent.Instances;
                    success = PostAllChildrenLocalTransformValuesMatch(useSubChild);
                    TimeTravelAdvanceTick();
                }
            }

            if (!success)
            {
                Assert.True(success, m_InfoMessage.ToString());
            }
        }

#if !MULTIPLAYER_TOOLS

        private void UpdateTransformLocal(Components.NetworkTransform networkTransformTestComponent)
        {
            networkTransformTestComponent.transform.localPosition += GetRandomVector3(0.5f, 2.0f);
            var rotation = networkTransformTestComponent.transform.localRotation;
            var eulerRotation = rotation.eulerAngles;
            eulerRotation += GetRandomVector3(0.5f, 5.0f);
            rotation.eulerAngles = eulerRotation;
            networkTransformTestComponent.transform.localRotation = rotation;
        }

        private void UpdateTransformWorld(Components.NetworkTransform networkTransformTestComponent)
        {
            networkTransformTestComponent.transform.position += GetRandomVector3(0.5f, 2.0f);
            var rotation = networkTransformTestComponent.transform.rotation;
            var eulerRotation = rotation.eulerAngles;
            eulerRotation += GetRandomVector3(0.5f, 5.0f);
            rotation.eulerAngles = eulerRotation;
            networkTransformTestComponent.transform.rotation = rotation;
        }

        /// <summary>
        /// This test validates the SwitchTransformSpaceWhenParented setting under all network topologies        
        /// </summary>
        [Test]
        public void SwitchTransformSpaceWhenParentedTest([Values(0.5f, 1.0f, 5.0f)] float scale)
        {
            m_UseParentingThreshold = true;
            // Get the NetworkManager that will have authority in order to spawn with the correct authority
            var isServerAuthority = m_Authority == Authority.ServerAuthority;
            var authorityNetworkManager = m_ServerNetworkManager;
            if (!isServerAuthority)
            {
                authorityNetworkManager = m_ClientNetworkManagers[0];
            }

            var childAuthorityNetworkManager = m_ClientNetworkManagers[0];
            if (!isServerAuthority)
            {
                childAuthorityNetworkManager = m_ServerNetworkManager;
            }

            // Spawn a parent and children
            ChildObjectComponent.HasSubChild = true;
            // Modify our prefabs for this specific test
            m_ParentObject.GetComponent<NetworkTransformTestComponent>().TickSyncChildren = true;
            m_ChildObject.GetComponent<ChildObjectComponent>().SwitchTransformSpaceWhenParented = true;
            m_ChildObject.GetComponent<ChildObjectComponent>().TickSyncChildren = true;
            m_SubChildObject.GetComponent<ChildObjectComponent>().SwitchTransformSpaceWhenParented = true;
            m_SubChildObject.GetComponent<ChildObjectComponent>().TickSyncChildren = true;
            m_ChildObject.AllowOwnerToParent = true;
            m_SubChildObject.AllowOwnerToParent = true;


            var authoritySideParent = SpawnObject(m_ParentObject.gameObject, authorityNetworkManager).GetComponent<NetworkObject>();
            var authoritySideChild = SpawnObject(m_ChildObject.gameObject, childAuthorityNetworkManager).GetComponent<NetworkObject>();
            var authoritySideSubChild = SpawnObject(m_SubChildObject.gameObject, childAuthorityNetworkManager).GetComponent<NetworkObject>();

            // Assure all of the child object instances are spawned before proceeding to parenting
            var success = WaitForConditionOrTimeOutWithTimeTravel(AllChildObjectInstancesAreSpawned);
            Assert.True(success, "Timed out waiting for all child instances to be spawned!");

            // Get the owner instance if in client-server mode with owner authority
            if (m_Authority == Authority.OwnerAuthority && !m_DistributedAuthority)
            {
                authoritySideParent = s_GlobalNetworkObjects[authoritySideParent.OwnerClientId][authoritySideParent.NetworkObjectId];
                authoritySideChild = s_GlobalNetworkObjects[authoritySideChild.OwnerClientId][authoritySideChild.NetworkObjectId];
                authoritySideSubChild = s_GlobalNetworkObjects[authoritySideSubChild.OwnerClientId][authoritySideSubChild.NetworkObjectId];
            }

            // Get the authority parent and child instances
            m_AuthorityParentObject = NetworkTransformTestComponent.AuthorityInstance.NetworkObject;
            m_AuthorityChildObject = ChildObjectComponent.AuthorityInstance.NetworkObject;
            m_AuthoritySubChildObject = ChildObjectComponent.AuthoritySubInstance.NetworkObject;

            // The child NetworkTransform will use world space when world position stays and
            // local space when world position does not stay when parenting.
            ChildObjectComponent.AuthorityInstance.UseHalfFloatPrecision = m_Precision == Precision.Half;
            ChildObjectComponent.AuthorityInstance.UseQuaternionSynchronization = m_Rotation == Rotation.Quaternion;
            ChildObjectComponent.AuthorityInstance.UseQuaternionCompression = m_RotationCompression == RotationCompression.QuaternionCompress;

            ChildObjectComponent.AuthoritySubInstance.UseHalfFloatPrecision = m_Precision == Precision.Half;
            ChildObjectComponent.AuthoritySubInstance.UseQuaternionSynchronization = m_Rotation == Rotation.Quaternion;
            ChildObjectComponent.AuthoritySubInstance.UseQuaternionCompression = m_RotationCompression == RotationCompression.QuaternionCompress;

            // Set whether we are interpolating or not
            m_AuthorityParentNetworkTransform = m_AuthorityParentObject.GetComponent<NetworkTransformTestComponent>();
            m_AuthorityParentNetworkTransform.Interpolate = true;
            m_AuthorityChildNetworkTransform = m_AuthorityChildObject.GetComponent<ChildObjectComponent>();
            m_AuthorityChildNetworkTransform.Interpolate = true;
            m_AuthoritySubChildNetworkTransform = m_AuthoritySubChildObject.GetComponent<ChildObjectComponent>();
            m_AuthoritySubChildNetworkTransform.Interpolate = true;

            // Apply a scale to the parent object to make sure the scale on the child is properly updated on
            // non-authority instances.
            var halfScale = scale * 0.5f;
            m_AuthorityParentObject.transform.localScale = GetRandomVector3(scale - halfScale, scale + halfScale);
            m_AuthorityChildObject.transform.localScale = GetRandomVector3(scale - halfScale, scale + halfScale);
            m_AuthoritySubChildObject.transform.localScale = GetRandomVector3(scale - halfScale, scale + halfScale);

            // Allow one tick for authority to update these changes
            TimeTravelAdvanceTick();
            success = WaitForConditionOrTimeOutWithTimeTravel(PositionRotationScaleMatches);

            Assert.True(success, "All transform values did not match prior to parenting!");

            success = WaitForConditionOrTimeOutWithTimeTravel(PositionRotationScaleMatches);

            Assert.True(success, "All transform values did not match prior to parenting!");

            // Move things around while parenting and removing the parent
            // Not the absolute "perfect" test, but it validates the clients all synchronize
            // parenting and transform values.
            for (int i = 0; i < 30; i++)
            {
                // Provide two network ticks for interpolation to finalize
                TimeTravelAdvanceTick();
                TimeTravelAdvanceTick();

                // This validates each child instance has preserved their local space values
                AllChildrenLocalTransformValuesMatch(false, ChildrenTransformCheckType.Connected_Clients);

                // This validates each sub-child instance has preserved their local space values
                AllChildrenLocalTransformValuesMatch(true, ChildrenTransformCheckType.Connected_Clients);
                // Parent while in motion
                if (i == 5)
                {
                    // Parent the child under the parent with the current world position stays setting
                    Assert.True(authoritySideChild.TrySetParent(authoritySideParent.transform), $"[Child][Client-{authoritySideChild.NetworkManagerOwner.LocalClientId}] Failed to set child's parent!");

                    // This waits for all child instances to be parented
                    success = WaitForConditionOrTimeOutWithTimeTravel(AllFirstLevelChildObjectInstancesHaveChild, 300);
                    Assert.True(success, "Timed out waiting for all instances to have parented a child!");
                }

                if (i == 10)
                {
                    // Parent the sub-child under the child with the current world position stays setting
                    Assert.True(authoritySideSubChild.TrySetParent(authoritySideChild.transform), $"[Sub-Child][Client-{authoritySideSubChild.NetworkManagerOwner.LocalClientId}] Failed to set sub-child's parent!");

                    // This waits for all child instances to be parented
                    success = WaitForConditionOrTimeOutWithTimeTravel(AllChildObjectInstancesHaveChild, 300);
                    Assert.True(success, "Timed out waiting for all instances to have parented a child!");
                }

                if (i == 15)
                {
                    // Verify that a late joining client will synchronize to the parented NetworkObjects properly
                    CreateAndStartNewClientWithTimeTravel();

                    // Assure all of the child object instances are spawned (basically for the newly connected client)
                    success = WaitForConditionOrTimeOutWithTimeTravel(AllChildObjectInstancesAreSpawned, 300);
                    Assert.True(success, "Timed out waiting for all child instances to be spawned!");

                    // This waits for all child instances to be parented
                    success = WaitForConditionOrTimeOutWithTimeTravel(AllChildObjectInstancesHaveChild, 300);
                    Assert.True(success, "Timed out waiting for all instances to have parented a child!");

                    // This validates each child instance has preserved their local space values
                    AllChildrenLocalTransformValuesMatch(false, ChildrenTransformCheckType.Late_Join_Client);

                    // This validates each sub-child instance has preserved their local space values
                    AllChildrenLocalTransformValuesMatch(true, ChildrenTransformCheckType.Late_Join_Client);
                }

                if (i == 20)
                {
                    // Remove the parent
                    Assert.True(authoritySideSubChild.TryRemoveParent(), $"[Sub-Child][Client-{authoritySideSubChild.NetworkManagerOwner.LocalClientId}] Failed to set sub-child's parent!");

                    // This waits for all child instances to have the parent removed
                    success = WaitForConditionOrTimeOutWithTimeTravel(AllSubChildObjectInstancesHaveNoParent, 300);
                    Assert.True(success, "Timed out waiting for all instances remove the parent!");
                }

                if (i == 25)
                {
                    // Parent the child under the parent with the current world position stays setting
                    Assert.True(authoritySideChild.TryRemoveParent(), $"[Child][Client-{authoritySideChild.NetworkManagerOwner.LocalClientId}] Failed to remove parent!");

                    // This waits for all child instances to be parented
                    success = WaitForConditionOrTimeOutWithTimeTravel(AllFirstLevelChildObjectInstancesHaveNoParent, 300);
                    Assert.True(success, "Timed out waiting for all instances remove the parent!");
                }
                UpdateTransformWorld(m_AuthorityParentNetworkTransform);
                if (m_AuthorityChildNetworkTransform.InLocalSpace)
                {
                    UpdateTransformLocal(m_AuthorityChildNetworkTransform);
                }
                else
                {
                    UpdateTransformWorld(m_AuthorityChildNetworkTransform);
                }

                if (m_AuthoritySubChildNetworkTransform.InLocalSpace)
                {
                    UpdateTransformLocal(m_AuthoritySubChildNetworkTransform);
                }
                else
                {
                    UpdateTransformWorld(m_AuthoritySubChildNetworkTransform);
                }
            }

            success = WaitForConditionOrTimeOutWithTimeTravel(PositionRotationScaleMatches, 300);

            Assert.True(success, "All transform values did not match prior to parenting!");

            // Revert the modifications made for this specific test
            m_ParentObject.GetComponent<NetworkTransformTestComponent>().TickSyncChildren = false;
            m_ChildObject.GetComponent<ChildObjectComponent>().SwitchTransformSpaceWhenParented = false;
            m_ChildObject.GetComponent<ChildObjectComponent>().TickSyncChildren = false;
            m_ChildObject.AllowOwnerToParent = false;
            m_SubChildObject.AllowOwnerToParent = false;
            m_SubChildObject.GetComponent<ChildObjectComponent>().SwitchTransformSpaceWhenParented = false;
            m_SubChildObject.GetComponent<ChildObjectComponent>().TickSyncChildren = false;
        }


        /// <summary>
        /// Validates that transform values remain the same when a NetworkTransform is
        /// parented under another NetworkTransform under all of the possible axial conditions
        /// as well as when the parent has a varying scale.
        /// </summary>
        [Test]
        public void ParentedNetworkTransformTest([Values] Interpolation interpolation, [Values] bool worldPositionStays, [Values(0.5f, 1.0f, 5.0f)] float scale)
        {
            m_UseParentingThreshold = true;
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

            // Assure all of the child object instances are spawned before proceeding to parenting
            var success = WaitForConditionOrTimeOutWithTimeTravel(AllChildObjectInstancesAreSpawned);
            Assert.True(success, "Timed out waiting for all child instances to be spawned!");

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
            TimeTravelAdvanceTick();
            success = WaitForConditionOrTimeOutWithTimeTravel(PositionRotationScaleMatches);

            Assert.True(success, "All transform values did not match prior to parenting!");

            success = WaitForConditionOrTimeOutWithTimeTravel(PositionRotationScaleMatches);

            Assert.True(success, "All transform values did not match prior to parenting!");

            // Parent the child under the parent with the current world position stays setting
            Assert.True(serverSideChild.TrySetParent(serverSideParent.transform, worldPositionStays), "[Server-Side Child] Failed to set child's parent!");

            // Parent the sub-child under the child with the current world position stays setting
            Assert.True(serverSideSubChild.TrySetParent(serverSideChild.transform, worldPositionStays), "[Server-Side SubChild] Failed to set sub-child's parent!");

            // This waits for all child instances to be parented
            success = WaitForConditionOrTimeOutWithTimeTravel(AllChildObjectInstancesHaveChild);
            Assert.True(success, "Timed out waiting for all instances to have parented a child!");

            // Provide two network ticks for interpolation to finalize
            TimeTravelAdvanceTick();
            TimeTravelAdvanceTick();

            // This validates each child instance has preserved their local space values
            AllChildrenLocalTransformValuesMatch(false, ChildrenTransformCheckType.Connected_Clients);

            // This validates each sub-child instance has preserved their local space values
            AllChildrenLocalTransformValuesMatch(true, ChildrenTransformCheckType.Connected_Clients);

            // Verify that a late joining client will synchronize to the parented NetworkObjects properly
            CreateAndStartNewClientWithTimeTravel();

            // Assure all of the child object instances are spawned (basically for the newly connected client)
            success = WaitForConditionOrTimeOutWithTimeTravel(AllChildObjectInstancesAreSpawned);
            Assert.True(success, "Timed out waiting for all child instances to be spawned!");

            // This waits for all child instances to be parented
            success = WaitForConditionOrTimeOutWithTimeTravel(AllChildObjectInstancesHaveChild);
            Assert.True(success, "Timed out waiting for all instances to have parented a child!");

            // This validates each child instance has preserved their local space values
            AllChildrenLocalTransformValuesMatch(false, ChildrenTransformCheckType.Late_Join_Client);

            // This validates each sub-child instance has preserved their local space values
            AllChildrenLocalTransformValuesMatch(true, ChildrenTransformCheckType.Late_Join_Client);
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
        [Test]
        public void NetworkTransformMultipleChangesOverTime([Values] TransformSpace testLocalTransform, [Values] OverrideState overideState, [Values] Axis axis)
        {
            m_AuthoritativeTransform.InLocalSpace = testLocalTransform == TransformSpace.Local;
            bool axisX = axis == Axis.X || axis == Axis.XY || axis == Axis.XZ || axis == Axis.XYZ;
            bool axisY = axis == Axis.Y || axis == Axis.XY || axis == Axis.YZ || axis == Axis.XYZ;
            bool axisZ = axis == Axis.Z || axis == Axis.XZ || axis == Axis.YZ || axis == Axis.XYZ;

            var axisCount = axisX ? 1 : 0;
            axisCount += axisY ? 1 : 0;
            axisCount += axisZ ? 1 : 0;

            // Enable interpolation when all 3 axis are selected to make sure we are synchronizing properly
            // when interpolation is enabled.
            m_AuthoritativeTransform.Interpolate = axisCount == 3 ? true : false;

            m_CurrentAxis = axis;

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

            m_AuthoritativeTransform.StatePushed = false;
            // Wait for the deltas to be pushed
            WaitForConditionOrTimeOutWithTimeTravel(() => m_AuthoritativeTransform.StatePushed);
            // Allow the precision settings to propagate first as changing precision
            // causes a teleport event to occur
            TimeTravelAdvanceTick();
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

                // Wait for the deltas to be pushed
                Assert.True(WaitForConditionOrTimeOutWithTimeTravel(() => m_AuthoritativeTransform.StatePushed && m_NonAuthoritativeTransform.StateUpdated), $"[Non-Interpolate {i}] Timed out waiting for state to be pushed ({m_AuthoritativeTransform.StatePushed}) or state to be updated ({m_NonAuthoritativeTransform.StateUpdated})!");

                // For 3 axis, we will skip validating that the non-authority interpolates to its target point at least once.
                // This will validate that non-authoritative updates are maintaining their target state axis values if only 2
                // of the axis are being updated to assure interpolation maintains the targeted axial value per axis.
                // For 2 and 1 axis tests we always validate per delta update
                if (m_AxisExcluded || axisCount < 3)
                {
                    // Wait for deltas to synchronize on non-authoritative side
                    success = WaitForConditionOrTimeOutWithTimeTravel(PositionRotationScaleMatches);
                    // Provide additional debug info about what failed (if it fails)
                    if (!success)
                    {
                        m_EnableVerboseDebug = true;
                        success = PositionRotationScaleMatches();
                        m_EnableVerboseDebug = false;
                    }
                    Assert.True(success, $"[Non-Interpolate {i}] Timed out waiting for non-authority to match authority's position or rotation");
                }
            }

            if (axisCount == 3)
            {
                // As a final test, wait for deltas to synchronize on non-authoritative side to assure it interpolates to th
                success = WaitForConditionOrTimeOutWithTimeTravel(PositionRotationScaleMatches);
                // Provide additional debug info about what failed (if it fails)
                if (!success)
                {
                    m_EnableVerboseDebug = true;
                    success = PositionRotationScaleMatches();
                    m_EnableVerboseDebug = false;
                }
                Assert.True(success, $"Timed out waiting for non-authority to match authority's position or rotation");
            }
        }

        /// <summary>
        /// Checks scale of a late joining client for all instances of the late joining client's player
        /// </summary>
        [Test]
        public void LateJoiningPlayerInitialScaleValues([Values] TransformSpace testLocalTransform, [Values] Interpolation interpolation, [Values] OverrideState overideState)
        {
            var overrideUpdate = overideState == OverrideState.CommitToTransform;
            m_AuthoritativeTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;
            m_NonAuthoritativeTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;
            m_AuthoritativeTransform.InLocalSpace = testLocalTransform == TransformSpace.Local;

            var position = GetRandomVector3(0.25f, 1.75f);
            var rotation = GetRandomVector3(1f, 45f);
            var scale = GetRandomVector3(0.25f, 2.0f);

            // Make some changes to the currently connected clients
            m_NonAuthoritativeTransform.StateUpdated = false;
            m_AuthoritativeTransform.StatePushed = false;
            MoveRotateAndScaleAuthority(position, rotation, scale, overideState);

            // Wait for the deltas to be pushed and updated
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => m_AuthoritativeTransform.StatePushed && m_NonAuthoritativeTransform.StateUpdated);
            Assert.True(success, $"[Interpolation {k_PositionRotationScaleIterations}] Timed out waiting for state to be pushed ({m_AuthoritativeTransform.StatePushed}) or state to be updated ({m_NonAuthoritativeTransform.StateUpdated})!");

            WaitForConditionOrTimeOutWithTimeTravel(PositionRotationScaleMatches);

            // Validate the use of the prefab's transform values as opposed to the replicated state (which now is only the last deltas)
            CreateAndStartNewClientWithTimeTravel();
            var newClientNetworkManager = m_ClientNetworkManagers[NumberOfClients];
            foreach (var playerRelativeEntry in m_PlayerNetworkObjects)
            {
                foreach (var playerInstanceEntry in playerRelativeEntry.Value)
                {
                    var playerInstance = playerInstanceEntry.Value;
                    if (newClientNetworkManager.LocalClientId == playerInstance.OwnerClientId)
                    {
                        Assert.IsTrue(Approximately(m_PlayerPrefab.transform.localScale, playerInstance.transform.localScale), $"{playerInstance.name}'s cloned instance's scale does not match original scale!\n" +
                            $"[ClientId-{playerRelativeEntry.Key} Relative] Player-{playerInstance.OwnerClientId}'s LocalScale ({playerInstance.transform.localScale}) vs Target Scale ({m_PlayerPrefab.transform.localScale})");
                    }
                }
            }
        }
#endif

        /// <summary>
        /// Tests changing all axial values one at a time.
        /// These tests are performed:
        /// - While in local space and world space
        /// - While interpolation is enabled and disabled
        /// - Using the TryCommitTransformToServer "override" that can be used
        /// from a child derived or external class.
        /// </summary>
        [Test]
        public void TestAuthoritativeTransformChangeOneAtATime([Values] TransformSpace testLocalTransform, [Values] Interpolation interpolation, [Values] OverrideState overideState)
        {
            var overrideUpdate = overideState == OverrideState.CommitToTransform;
            m_AuthoritativeTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;
            m_NonAuthoritativeTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;
            m_AuthoritativeTransform.InLocalSpace = testLocalTransform == TransformSpace.Local;

            // test position
            var authPlayerTransform = overrideUpdate ? m_OwnerTransform.transform : m_AuthoritativeTransform.transform;

            Assert.AreEqual(Vector3.zero, m_NonAuthoritativeTransform.transform.position, "server side pos should be zero at first"); // sanity check

            TimeTravelAdvanceTick();
            TimeTravelToNextTick();

            m_AuthoritativeTransform.StatePushed = false;
            var nextPosition = GetRandomVector3(2f, 30f);

            switch (overideState)
            {
                case OverrideState.Update:
                    {
                        m_AuthoritativeTransform.transform.position = nextPosition;
                        break;
                    }
                case OverrideState.SetState:
                    {
                        m_OwnerTransform.SetState(nextPosition, null, null);
                        break;
                    }
                case OverrideState.CommitToTransform:
                    {
                        m_OwnerTransform.transform.position = nextPosition;
                        m_OwnerTransform.CommitToTransform();
                        break;
                    }
            }

            bool success;
            if (overideState == OverrideState.CommitToTransform)
            {
                // Wait for the deltas to be pushed
                success = WaitForConditionOrTimeOutWithTimeTravel(() => m_AuthoritativeTransform.StatePushed, 600);
                Assert.True(success, $"[Position] Timed out waiting for state to be pushed ({m_AuthoritativeTransform.StatePushed})!");
            }

            success = WaitForConditionOrTimeOutWithTimeTravel(() => PositionsMatch(), 600);
            Assert.True(success, $"Timed out waiting for positions to match {m_AuthoritativeTransform.transform.position} | {m_NonAuthoritativeTransform.transform.position}");

            // test rotation
            Assert.AreEqual(Quaternion.identity, m_NonAuthoritativeTransform.transform.rotation, "wrong initial value for rotation"); // sanity check

            m_AuthoritativeTransform.StatePushed = false;
            var nextRotation = Quaternion.Euler(GetRandomVector3(5, 60)); // using euler angles instead of quaternions directly to really see issues users might encounter
            switch (overideState)
            {
                case OverrideState.Update:
                    {
                        m_AuthoritativeTransform.transform.rotation = nextRotation;
                        break;
                    }
                case OverrideState.SetState:
                    {
                        m_OwnerTransform.SetState(null, nextRotation, null);
                        break;
                    }
                case OverrideState.CommitToTransform:
                    {
                        m_OwnerTransform.transform.rotation = nextRotation;
                        m_OwnerTransform.CommitToTransform();
                        break;
                    }
            }

            if (overideState == OverrideState.CommitToTransform)
            {
                // Wait for the deltas to be pushed
                success = WaitForConditionOrTimeOutWithTimeTravel(() => m_AuthoritativeTransform.StatePushed, 600);
                Assert.True(success, $"[Rotation] Timed out waiting for state to be pushed ({m_AuthoritativeTransform.StatePushed})!");
            }

            // Make sure the values match
            success = WaitForConditionOrTimeOutWithTimeTravel(() => RotationsMatch(), 600);
            Assert.True(success, $"Timed out waiting for rotations to match");

            m_AuthoritativeTransform.StatePushed = false;
            var nextScale = GetRandomVector3(1, 6);

            switch (overideState)
            {
                case OverrideState.Update:
                    {
                        m_AuthoritativeTransform.transform.localScale = nextScale;
                        break;
                    }
                case OverrideState.SetState:
                    {
                        m_OwnerTransform.SetState(null, null, nextScale);
                        break;
                    }
                case OverrideState.CommitToTransform:
                    {
                        m_OwnerTransform.transform.localScale = nextScale;
                        m_OwnerTransform.CommitToTransform();
                        break;
                    }
            }

            if (overideState == OverrideState.CommitToTransform)
            {
                // Wait for the deltas to be pushed
                success = WaitForConditionOrTimeOutWithTimeTravel(() => m_AuthoritativeTransform.StatePushed, 600);
                Assert.True(success, $"[Rotation] Timed out waiting for state to be pushed ({m_AuthoritativeTransform.StatePushed})!");
            }

            // Make sure the scale values match
            success = WaitForConditionOrTimeOutWithTimeTravel(() => ScaleValuesMatch(), 600);
            Assert.True(success, $"Timed out waiting for scale values to match");
        }

        /// <summary>
        /// The tests teleporting with and without interpolation
        /// </summary>
        [Test]
        public void TeleportTest([Values] Interpolation interpolation)
        {
            m_AuthoritativeTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;
            m_NonAuthoritativeTransform.Interpolate = interpolation == Interpolation.EnableInterpolate;
            var authTransform = m_AuthoritativeTransform.transform;
            var nonAuthPosition = m_NonAuthoritativeTransform.transform.position;
            var currentTick = m_AuthoritativeTransform.NetworkManager.ServerTime.Tick;
            m_DetectedPotentialInterpolatedTeleport = 0.0f;
            var teleportDestination = GetRandomVector3(50.0f, 200.0f);
            m_NonAuthoritativeTransform.StateUpdated = false;
            m_AuthoritativeTransform.StatePushed = false;
            m_AuthoritativeTransform.Teleport(teleportDestination, authTransform.rotation, authTransform.localScale);

            // Wait for the deltas to be pushed and updated
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => m_AuthoritativeTransform.StatePushed && m_NonAuthoritativeTransform.StateUpdated);
            Assert.True(success, $"[Teleport] Timed out waiting for state to be pushed ({m_AuthoritativeTransform.StatePushed}) or state to be updated ({m_NonAuthoritativeTransform.StateUpdated})!");

            SimulateOneFrame();
            Assert.True(TeleportPositionMatches(nonAuthPosition), $"NonAuthoritative position ({m_NonAuthoritativeTransform.GetSpaceRelativePosition()}) is not the same as the destination position {teleportDestination}!");

            var targetDistance = 0.0f;
            if (!Approximately(m_DetectedPotentialInterpolatedTeleport, 0.0f))
            {
                targetDistance = Mathf.Abs(Vector3.Distance(nonAuthPosition, teleportDestination));
            }
            Assert.IsTrue(Approximately(m_DetectedPotentialInterpolatedTeleport, 0.0f), $"Detected possible interpolation on non-authority side! NonAuthority distance: {m_DetectedPotentialInterpolatedTeleport} | Target distance: {targetDistance}");
        }

    }
}
