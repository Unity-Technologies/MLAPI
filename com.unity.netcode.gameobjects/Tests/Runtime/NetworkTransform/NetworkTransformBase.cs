using System.Collections;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Unity.Netcode.Components;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;


namespace Unity.Netcode.RuntimeTests
{
    internal class NetworkTransformBase : IntegrationTestWithApproximation
    {

        // The number of iterations to change position, rotation, and scale for NetworkTransformMultipleChangesOverTime
        protected const int k_PositionRotationScaleIterations = 3;
        protected const int k_PositionRotationScaleIterations3Axis = 8;

        protected float m_CurrentHalfPrecision = 0.0f;
        protected const float k_HalfPrecisionPosScale = 0.1256f;
        protected const float k_HalfPrecisionRot = 0.725f;


        protected NetworkObject m_AuthoritativePlayer;
        protected NetworkObject m_NonAuthoritativePlayer;
        protected NetworkObject m_ChildObject;
        protected NetworkObject m_SubChildObject;
        protected NetworkObject m_ParentObject;

        protected NetworkTransformTestComponent m_AuthoritativeTransform;
        protected NetworkTransformTestComponent m_NonAuthoritativeTransform;
        protected NetworkTransformTestComponent m_OwnerTransform;


        protected int m_OriginalTargetFrameRate;
        protected Axis m_CurrentAxis;
        protected bool m_AxisExcluded;
        protected float m_DetectedPotentialInterpolatedTeleport;

        protected StringBuilder m_InfoMessage = new StringBuilder();

        protected Rotation m_Rotation = Rotation.Euler;
        protected Precision m_Precision = Precision.Full;
        protected RotationCompression m_RotationCompression = RotationCompression.None;
        protected Authority m_Authority;

        // To test that local position, rotation, and scale remain the same when parented.
        protected Vector3 m_ChildObjectLocalPosition = new Vector3(5.0f, 0.0f, -5.0f);
        protected Vector3 m_ChildObjectLocalRotation = new Vector3(-35.0f, 90.0f, 270.0f);
        protected Vector3 m_ChildObjectLocalScale = new Vector3(0.1f, 0.5f, 0.4f);
        protected Vector3 m_SubChildObjectLocalPosition = new Vector3(2.0f, 1.0f, -1.0f);
        protected Vector3 m_SubChildObjectLocalRotation = new Vector3(5.0f, 15.0f, 124.0f);
        protected Vector3 m_SubChildObjectLocalScale = new Vector3(1.0f, 0.15f, 0.75f);
        protected NetworkObject m_AuthorityParentObject;
        protected NetworkTransformTestComponent m_AuthorityParentNetworkTransform;
        protected NetworkObject m_AuthorityChildObject;
        protected NetworkObject m_AuthoritySubChildObject;
        protected ChildObjectComponent m_AuthorityChildNetworkTransform;
        protected ChildObjectComponent m_AuthoritySubChildNetworkTransform;

        public enum Authority
        {
            ServerAuthority,
            OwnerAuthority
        }

        public enum Interpolation
        {
            DisableInterpolate,
            EnableInterpolate
        }

        public enum Precision
        {
            Half,
            Full
        }

        public enum Rotation
        {
            Euler,
            Quaternion
        }

        public enum RotationCompression
        {
            None,
            QuaternionCompress
        }

        public enum TransformSpace
        {
            World,
            Local
        }

        public enum OverrideState
        {
            Update,
            CommitToTransform,
            SetState
        }

        public enum Axis
        {
            X,
            Y,
            Z,
            XY,
            XZ,
            YZ,
            XYZ
        }

        protected enum ChildrenTransformCheckType
        {
            Connected_Clients,
            Late_Join_Client
        }

        protected override int NumberOfClients => OnNumberOfClients();

        protected override float GetDeltaVarianceThreshold()
        {
            if (m_Precision == Precision.Half || m_RotationCompression == RotationCompression.QuaternionCompress)
            {
                return m_CurrentHalfPrecision;
            }
            return 0.055f;
        }

        /// <summary>
        /// Override to provide the number of clients
        /// </summary>
        /// <returns></returns>
        protected virtual int OnNumberOfClients()
        {
            return 1;
        }

        /// <summary>
        /// Determines whether the test will use unreliable delivery for implicit state updates or not
        /// </summary>
        protected virtual bool UseUnreliableDeltas()
        {
            return false;
        }

        protected virtual void Setup()
        {
            NetworkTransformTestComponent.AuthorityInstance = null;
            m_Precision = Precision.Full;
            ChildObjectComponent.Reset();
        }

        protected virtual void Teardown()
        {
            m_EnableVerboseDebug = false;
            Object.DestroyImmediate(m_PlayerPrefab);
        }

        /// <summary>
        /// Handles the Setup for time travel enabled child derived tests
        /// </summary>
        protected override void OnInlineSetup()
        {
            Setup();
            base.OnInlineSetup();
        }

        /// <summary>
        /// Handles the Teardown for time travel enabled child derived tests
        /// </summary>
        protected override void OnInlineTearDown()
        {
            Teardown();
            base.OnInlineTearDown();
        }

        /// <summary>
        /// Handles the Setup for coroutine based derived tests
        /// </summary>
        protected override IEnumerator OnSetup()
        {
            Setup();
            return base.OnSetup();
        }

        /// <summary>
        /// Handles the Teardown for coroutine based derived tests
        /// </summary>
        protected override IEnumerator OnTearDown()
        {
            Teardown();
            return base.OnTearDown();
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="testWithHost">Determines if we are running as a server or host</param>
        /// <param name="authority">Determines if we are using server or owner authority</param>
        public NetworkTransformBase(HostOrServer testWithHost, Authority authority, RotationCompression rotationCompression, Rotation rotation, Precision precision) : base(testWithHost)
        {
            m_Authority = authority;
            m_Precision = precision;
            m_RotationCompression = rotationCompression;
            m_Rotation = rotation;
        }

        protected virtual int TargetFrameRate()
        {
            return 120;
        }

        protected override void OnOneTimeSetup()
        {
            m_OriginalTargetFrameRate = Application.targetFrameRate;
            Application.targetFrameRate = TargetFrameRate();
            base.OnOneTimeSetup();
        }

        protected override void OnOneTimeTearDown()
        {
            Application.targetFrameRate = m_OriginalTargetFrameRate;
            base.OnOneTimeTearDown();
        }

        protected override void OnCreatePlayerPrefab()
        {
            var networkTransformTestComponent = m_PlayerPrefab.AddComponent<NetworkTransformTestComponent>();
            networkTransformTestComponent.ServerAuthority = m_Authority == Authority.ServerAuthority;
        }

        protected override void OnServerAndClientsCreated()
        {
            var subChildObject = CreateNetworkObjectPrefab("SubChildObject");
            var subChildNetworkTransform = subChildObject.AddComponent<SubChildObjectComponent>();
            subChildNetworkTransform.ServerAuthority = m_Authority == Authority.ServerAuthority;
            m_SubChildObject = subChildObject.GetComponent<NetworkObject>();

            var childObject = CreateNetworkObjectPrefab("ChildObject");
            var childNetworkTransform = childObject.AddComponent<ChildObjectComponent>();
            childNetworkTransform.ServerAuthority = m_Authority == Authority.ServerAuthority;
            m_ChildObject = childObject.GetComponent<NetworkObject>();

            var parentObject = CreateNetworkObjectPrefab("ParentObject");
            var parentNetworkTransform = parentObject.AddComponent<NetworkTransformTestComponent>();
            parentNetworkTransform.ServerAuthority = m_Authority == Authority.ServerAuthority;
            m_ParentObject = parentObject.GetComponent<NetworkObject>();

            // Now apply local transform values
            m_ChildObject.transform.position = m_ChildObjectLocalPosition;
            var childRotation = m_ChildObject.transform.rotation;
            childRotation.eulerAngles = m_ChildObjectLocalRotation;
            m_ChildObject.transform.rotation = childRotation;
            m_ChildObject.transform.localScale = m_ChildObjectLocalScale;

            m_SubChildObject.transform.position = m_SubChildObjectLocalPosition;
            var subChildRotation = m_SubChildObject.transform.rotation;
            subChildRotation.eulerAngles = m_SubChildObjectLocalRotation;
            m_SubChildObject.transform.rotation = childRotation;
            m_SubChildObject.transform.localScale = m_SubChildObjectLocalScale;

            if (m_EnableVerboseDebug)
            {
                m_ServerNetworkManager.LogLevel = LogLevel.Developer;
                foreach (var clientNetworkManager in m_ClientNetworkManagers)
                {
                    clientNetworkManager.LogLevel = LogLevel.Developer;
                }
            }

            m_ServerNetworkManager.NetworkConfig.TickRate = GetTickRate();
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                clientNetworkManager.NetworkConfig.TickRate = GetTickRate();
            }
        }


        protected virtual void OnClientsAndServerConnectedSetup()
        {
            // Get the client player representation on both the server and the client side
            var serverSideClientPlayer = m_PlayerNetworkObjects[0][m_ClientNetworkManagers[0].LocalClientId];
            var clientSideClientPlayer = m_PlayerNetworkObjects[m_ClientNetworkManagers[0].LocalClientId][m_ClientNetworkManagers[0].LocalClientId];

            m_AuthoritativePlayer = m_Authority == Authority.ServerAuthority ? serverSideClientPlayer : clientSideClientPlayer;
            m_NonAuthoritativePlayer = m_Authority == Authority.ServerAuthority ? clientSideClientPlayer : serverSideClientPlayer;

            // Get the NetworkTransformTestComponent to make sure the client side is ready before starting test
            m_AuthoritativeTransform = m_AuthoritativePlayer.GetComponent<NetworkTransformTestComponent>();
            m_NonAuthoritativeTransform = m_NonAuthoritativePlayer.GetComponent<NetworkTransformTestComponent>();

            // Setup whether we are or are not using unreliable deltas
            m_AuthoritativeTransform.UseUnreliableDeltas = UseUnreliableDeltas();
            m_NonAuthoritativeTransform.UseUnreliableDeltas = UseUnreliableDeltas();

            m_AuthoritativeTransform.UseHalfFloatPrecision = m_Precision == Precision.Half;
            m_AuthoritativeTransform.UseQuaternionSynchronization = m_Rotation == Rotation.Quaternion;
            m_AuthoritativeTransform.UseQuaternionCompression = m_RotationCompression == RotationCompression.QuaternionCompress;
            m_NonAuthoritativeTransform.UseHalfFloatPrecision = m_Precision == Precision.Half;
            m_NonAuthoritativeTransform.UseQuaternionSynchronization = m_Rotation == Rotation.Quaternion;
            m_NonAuthoritativeTransform.UseQuaternionCompression = m_RotationCompression == RotationCompression.QuaternionCompress;


            m_OwnerTransform = m_AuthoritativeTransform.IsOwner ? m_AuthoritativeTransform : m_NonAuthoritativeTransform;
        }

        protected override void OnTimeTravelServerAndClientsConnected()
        {
            OnClientsAndServerConnectedSetup();

            // Wait for the client-side to notify it is finished initializing and spawning.
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => m_NonAuthoritativeTransform.ReadyToReceivePositionUpdate == true);
            Assert.True(success, "Timed out waiting for client-side to notify it is ready!");

            Assert.True(m_AuthoritativeTransform.CanCommitToTransform);
            Assert.False(m_NonAuthoritativeTransform.CanCommitToTransform);
            // Just wait for at least one tick for NetworkTransforms to finish synchronization
            TimeTravelAdvanceTick();
        }

        /// <summary>
        /// Handles the OnServerAndClientsConnected for coroutine based derived tests
        /// </summary>
        protected override IEnumerator OnServerAndClientsConnected()
        {
            // Wait for the client-side to notify it is finished initializing and spawning.
            yield return WaitForClientsConnectedOrTimeOut();
            AssertOnTimeout("Timed out waiting for client-side to notify it is ready!");
            OnClientsAndServerConnectedSetup();
            yield return base.OnServerAndClientsConnected();
        }

        /// <summary>
        /// Handles setting a new client being connected
        /// </summary>
        protected override void OnNewClientCreated(NetworkManager networkManager)
        {
            networkManager.NetworkConfig.Prefabs = m_ServerNetworkManager.NetworkConfig.Prefabs;
            networkManager.NetworkConfig.TickRate = GetTickRate();
            if (m_EnableVerboseDebug)
            {
                networkManager.LogLevel = LogLevel.Developer;
            }
            base.OnNewClientCreated(networkManager);
        }


        /// <summary>
        /// Returns true when the server-host and all clients have
        /// instantiated the child object to be used in <see cref="NetworkTransformParentingLocalSpaceOffsetTests"/>
        /// </summary>
        /// <returns></returns>
        protected bool AllChildObjectInstancesAreSpawned()
        {
            if (ChildObjectComponent.AuthorityInstance == null)
            {
                return false;
            }

            if (ChildObjectComponent.HasSubChild && ChildObjectComponent.AuthoritySubInstance == null)
            {
                return false;
            }

            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                if (!ChildObjectComponent.ClientInstances.ContainsKey(clientNetworkManager.LocalClientId))
                {
                    return false;
                }
            }
            return true;
        }

        protected bool AllFirstLevelChildObjectInstancesHaveChild()
        {
            foreach (var instance in ChildObjectComponent.ClientInstances.Values)
            {
                if (instance.transform.parent == null)
                {
                    return false;
                }
            }
            return true;
        }

        protected bool AllChildObjectInstancesHaveChild()
        {
            foreach (var instance in ChildObjectComponent.ClientInstances.Values)
            {
                if (instance.transform.parent == null)
                {
                    return false;
                }
            }
            if (ChildObjectComponent.HasSubChild)
            {
                foreach (var instance in ChildObjectComponent.ClientSubChildInstances.Values)
                {
                    if (instance.transform.parent == null)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        protected bool AllFirstLevelChildObjectInstancesHaveNoParent()
        {
            foreach (var instance in ChildObjectComponent.ClientInstances.Values)
            {
                if (instance.transform.parent != null)
                {
                    return false;
                }
            }
            return true;
        }

        protected bool AllSubChildObjectInstancesHaveNoParent()
        {
            if (ChildObjectComponent.HasSubChild)
            {
                foreach (var instance in ChildObjectComponent.ClientSubChildInstances.Values)
                {
                    if (instance.transform.parent != null)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// A wait condition specific method that assures the local space coordinates
        /// are not impacted by NetworkTransform when parented.
        /// </summary>
        protected bool AllInstancesKeptLocalTransformValues(bool useSubChild)
        {
            var authorityObjectLocalPosition = useSubChild ? m_AuthoritySubChildObject.transform.localPosition : m_AuthorityChildObject.transform.localPosition;
            var authorityObjectLocalRotation = useSubChild ? m_AuthoritySubChildObject.transform.localRotation.eulerAngles : m_AuthorityChildObject.transform.localRotation.eulerAngles;
            var authorityObjectLocalScale = useSubChild ? m_AuthoritySubChildObject.transform.localScale : m_AuthorityChildObject.transform.localScale;
            var instances = useSubChild ? ChildObjectComponent.SubInstances : ChildObjectComponent.Instances;
            foreach (var childInstance in instances)
            {
                var childLocalPosition = childInstance.transform.localPosition;
                var childLocalRotation = childInstance.transform.localRotation.eulerAngles;
                var childLocalScale = childInstance.transform.localScale;
                // Adjust approximation based on precision
                if (m_Precision == Precision.Half)
                {
                    m_CurrentHalfPrecision = k_HalfPrecisionPosScale;
                }
                if (!Approximately(childLocalPosition, authorityObjectLocalPosition))
                {
                    return false;
                }
                if (!Approximately(childLocalScale, authorityObjectLocalScale))
                {
                    return false;
                }
                // Adjust approximation based on precision
                if (m_Precision == Precision.Half || m_RotationCompression == RotationCompression.QuaternionCompress)
                {
                    m_CurrentHalfPrecision = k_HalfPrecisionRot;
                }
                if (!ApproximatelyEuler(childLocalRotation, authorityObjectLocalRotation))
                {
                    return false;
                }
            }
            return true;
        }

        protected bool PostAllChildrenLocalTransformValuesMatch(bool useSubChild)
        {
            var success = !s_GlobalTimeoutHelper.TimedOut;
            var authorityObjectLocalPosition = useSubChild ? m_AuthoritySubChildObject.transform.localPosition : m_AuthorityChildObject.transform.localPosition;
            var authorityObjectLocalRotation = useSubChild ? m_AuthoritySubChildObject.transform.localRotation.eulerAngles : m_AuthorityChildObject.transform.localRotation.eulerAngles;
            var authorityObjectLocalScale = useSubChild ? m_AuthoritySubChildObject.transform.localScale : m_AuthorityChildObject.transform.localScale;

            if (s_GlobalTimeoutHelper.TimedOut)
            {
                // If we timed out, then wait for a full range of ticks (plus 1) to assure it sent synchronization data.
                for (int j = 0; j < m_ServerNetworkManager.NetworkConfig.TickRate; j++)
                {
                    var instances = useSubChild ? ChildObjectComponent.SubInstances : ChildObjectComponent.Instances;
                    foreach (var childInstance in instances)
                    {
                        var childParentName = "invalid";
                        try
                        {
                            childParentName = useSubChild ? childInstance.transform.parent.parent.name : childInstance.transform.name;
                        }
                        catch (System.Exception ex)
                        {
                            Debug.Log(ex.Message);
                        }
                        var childLocalPosition = childInstance.transform.localPosition;
                        var childLocalRotation = childInstance.transform.localRotation.eulerAngles;
                        var childLocalScale = childInstance.transform.localScale;
                        // Adjust approximation based on precision
                        if (m_Precision == Precision.Half || m_RotationCompression == RotationCompression.QuaternionCompress)
                        {
                            m_CurrentHalfPrecision = k_HalfPrecisionPosScale;
                        }
                        if (!Approximately(childLocalPosition, authorityObjectLocalPosition))
                        {
                            m_InfoMessage.AppendLine($"[{childParentName}][{childInstance.name}] Child's Local Position ({GetVector3Values(childLocalPosition)}) | Authority Local Position ({GetVector3Values(authorityObjectLocalPosition)})");
                            success = false;
                        }
                        if (!Approximately(childLocalScale, authorityObjectLocalScale))
                        {
                            m_InfoMessage.AppendLine($"[{childParentName}][{childInstance.name}] Child's Local Scale ({GetVector3Values(childLocalScale)}) | Authority Local Scale ({GetVector3Values(authorityObjectLocalScale)})");
                            success = false;
                        }

                        // Adjust approximation based on precision
                        if (m_Precision == Precision.Half || m_RotationCompression == RotationCompression.QuaternionCompress)
                        {
                            m_CurrentHalfPrecision = k_HalfPrecisionRot;
                        }
                        if (!ApproximatelyEuler(childLocalRotation, authorityObjectLocalRotation))
                        {
                            m_InfoMessage.AppendLine($"[{childParentName}][{childInstance.name}] Child's Local Rotation ({GetVector3Values(childLocalRotation)}) | Authority Local Rotation ({GetVector3Values(authorityObjectLocalRotation)})");
                            success = false;
                        }
                    }
                }
            }
            return success;
        }



        /// <summary>
        /// Validates that moving, rotating, and scaling the authority side with a single
        /// tick will properly synchronize the non-authoritative side with the same values.
        /// </summary>
        protected void MoveRotateAndScaleAuthority(Vector3 position, Vector3 rotation, Vector3 scale, OverrideState overrideState)
        {
            switch (overrideState)
            {
                case OverrideState.SetState:
                    {
                        var authoritativeRotation = m_AuthoritativeTransform.GetSpaceRelativeRotation();
                        authoritativeRotation.eulerAngles = rotation;
                        if (m_Authority == Authority.OwnerAuthority)
                        {
                            // Under the scenario where the owner is not the server, and non-auth is the server we set the state from the server
                            // to be updated to the owner.
                            if (m_AuthoritativeTransform.IsOwner && !m_AuthoritativeTransform.IsServer && m_NonAuthoritativeTransform.IsServer)
                            {
                                m_NonAuthoritativeTransform.SetState(position, authoritativeRotation, scale);
                            }
                            else
                            {
                                m_AuthoritativeTransform.SetState(position, authoritativeRotation, scale);
                            }
                        }
                        else
                        {
                            m_AuthoritativeTransform.SetState(position, authoritativeRotation, scale);
                        }

                        break;
                    }
                case OverrideState.Update:
                default:
                    {
                        m_AuthoritativeTransform.transform.position = position;

                        var authoritativeRotation = m_AuthoritativeTransform.GetSpaceRelativeRotation();
                        authoritativeRotation.eulerAngles = rotation;
                        m_AuthoritativeTransform.transform.rotation = authoritativeRotation;
                        m_AuthoritativeTransform.transform.localScale = scale;
                        break;
                    }
            }
        }

        /// <summary>
        /// Randomly determine if an axis should be excluded.
        /// If so, then randomly pick one of the axis to be excluded.
        /// </summary>
        protected Vector3 RandomlyExcludeAxis(Vector3 delta)
        {
            if (Random.Range(0.0f, 1.0f) >= 0.5f)
            {
                m_AxisExcluded = true;
                var axisToIgnore = Random.Range(0, 2);
                switch (axisToIgnore)
                {
                    case 0:
                        {
                            delta.x = 0;
                            break;
                        }
                    case 1:
                        {
                            delta.y = 0;
                            break;
                        }
                    case 2:
                        {
                            delta.z = 0;
                            break;
                        }
                }
            }
            return delta;
        }

        protected bool PositionRotationScaleMatches()
        {
            return RotationsMatch() && PositionsMatch() && ScaleValuesMatch();
        }

        protected bool PositionRotationScaleMatches(Vector3 position, Vector3 eulerRotation, Vector3 scale)
        {
            return PositionsMatchesValue(position) && RotationMatchesValue(eulerRotation) && ScaleMatchesValue(scale);
        }

        protected bool PositionsMatchesValue(Vector3 positionToMatch)
        {
            var authorityPosition = m_AuthoritativeTransform.transform.position;
            var nonAuthorityPosition = m_NonAuthoritativeTransform.transform.position;
            var auhtorityIsEqual = Approximately(authorityPosition, positionToMatch);
            var nonauthorityIsEqual = Approximately(nonAuthorityPosition, positionToMatch);

            if (!auhtorityIsEqual)
            {
                VerboseDebug($"Authority position {authorityPosition} != position to match: {positionToMatch}!");
            }
            if (!nonauthorityIsEqual)
            {
                VerboseDebug($"NonAuthority position {nonAuthorityPosition} != position to match: {positionToMatch}!");
            }
            return auhtorityIsEqual && nonauthorityIsEqual;
        }

        protected bool RotationMatchesValue(Vector3 rotationEulerToMatch)
        {
            var authorityRotationEuler = m_AuthoritativeTransform.transform.rotation.eulerAngles;
            var nonAuthorityRotationEuler = m_NonAuthoritativeTransform.transform.rotation.eulerAngles;
            var auhtorityIsEqual = Approximately(authorityRotationEuler, rotationEulerToMatch);
            var nonauthorityIsEqual = Approximately(nonAuthorityRotationEuler, rotationEulerToMatch);

            if (!auhtorityIsEqual)
            {
                VerboseDebug($"Authority rotation {authorityRotationEuler} != rotation to match: {rotationEulerToMatch}!");
            }
            if (!nonauthorityIsEqual)
            {
                VerboseDebug($"NonAuthority rotation {nonAuthorityRotationEuler} != rotation to match: {rotationEulerToMatch}!");
            }
            return auhtorityIsEqual && nonauthorityIsEqual;
        }

        protected bool ScaleMatchesValue(Vector3 scaleToMatch)
        {
            var authorityScale = m_AuthoritativeTransform.transform.localScale;
            var nonAuthorityScale = m_NonAuthoritativeTransform.transform.localScale;
            var auhtorityIsEqual = Approximately(authorityScale, scaleToMatch);
            var nonauthorityIsEqual = Approximately(nonAuthorityScale, scaleToMatch);

            if (!auhtorityIsEqual)
            {
                VerboseDebug($"Authority scale {authorityScale} != scale to match: {scaleToMatch}!");
            }
            if (!nonauthorityIsEqual)
            {
                VerboseDebug($"NonAuthority scale {nonAuthorityScale} != scale to match: {scaleToMatch}!");
            }
            return auhtorityIsEqual && nonauthorityIsEqual;
        }

        protected bool TeleportPositionMatches(Vector3 nonAuthorityOriginalPosition)
        {
            var nonAuthorityPosition = m_NonAuthoritativeTransform.transform.position;
            var authorityPosition = m_AuthoritativeTransform.transform.position;
            var targetDistance = Mathf.Abs(Vector3.Distance(nonAuthorityOriginalPosition, authorityPosition));
            var nonAuthorityCurrentDistance = Mathf.Abs(Vector3.Distance(nonAuthorityPosition, nonAuthorityOriginalPosition));
            // If we are not within our target distance range
            if (!Approximately(targetDistance, nonAuthorityCurrentDistance))
            {
                // Apply the non-authority's distance that is checked at the end of the teleport test
                m_DetectedPotentialInterpolatedTeleport = nonAuthorityCurrentDistance;
                return false;
            }
            else
            {
                // Otherwise, if we are within our target distance range then reset any already set value
                m_DetectedPotentialInterpolatedTeleport = 0.0f;
            }
            var xIsEqual = Approximately(authorityPosition.x, nonAuthorityPosition.x);
            var yIsEqual = Approximately(authorityPosition.y, nonAuthorityPosition.y);
            var zIsEqual = Approximately(authorityPosition.z, nonAuthorityPosition.z);
            if (!xIsEqual || !yIsEqual || !zIsEqual)
            {
                VerboseDebug($"[{m_AuthoritativeTransform.gameObject.name}] Authority position {authorityPosition} != [{m_NonAuthoritativeTransform.gameObject.name}] NonAuthority position {nonAuthorityPosition}");
            }
            return xIsEqual && yIsEqual && zIsEqual;
        }

        protected bool RotationsMatch(bool printDeltas = false)
        {
            m_CurrentHalfPrecision = k_HalfPrecisionRot;
            var authorityEulerRotation = m_AuthoritativeTransform.GetSpaceRelativeRotation().eulerAngles;
            var nonAuthorityEulerRotation = m_NonAuthoritativeTransform.GetSpaceRelativeRotation().eulerAngles;
            var xIsEqual = ApproximatelyEuler(authorityEulerRotation.x, nonAuthorityEulerRotation.x) || !m_AuthoritativeTransform.SyncRotAngleX;
            var yIsEqual = ApproximatelyEuler(authorityEulerRotation.y, nonAuthorityEulerRotation.y) || !m_AuthoritativeTransform.SyncRotAngleY;
            var zIsEqual = ApproximatelyEuler(authorityEulerRotation.z, nonAuthorityEulerRotation.z) || !m_AuthoritativeTransform.SyncRotAngleZ;
            if (!xIsEqual || !yIsEqual || !zIsEqual)
            {
                VerboseDebug($"[{m_AuthoritativeTransform.gameObject.name}][X-{xIsEqual} | Y-{yIsEqual} | Z-{zIsEqual}][{m_CurrentAxis}]" +
                    $"[Sync: X-{m_AuthoritativeTransform.SyncRotAngleX} |  Y-{m_AuthoritativeTransform.SyncRotAngleY} |  Z-{m_AuthoritativeTransform.SyncRotAngleZ}] Authority rotation {authorityEulerRotation} != [{m_NonAuthoritativeTransform.gameObject.name}] NonAuthority rotation {nonAuthorityEulerRotation}");
            }
            if (printDeltas)
            {
                Debug.Log($"[Rotation Match] Euler Delta {EulerDelta(authorityEulerRotation, nonAuthorityEulerRotation)}");
            }
            return xIsEqual && yIsEqual && zIsEqual;
        }

        protected bool PositionsMatch(bool printDeltas = false)
        {
            m_CurrentHalfPrecision = k_HalfPrecisionPosScale;
            var authorityPosition = m_AuthoritativeTransform.GetSpaceRelativePosition();
            var nonAuthorityPosition = m_NonAuthoritativeTransform.GetSpaceRelativePosition();
            var xIsEqual = Approximately(authorityPosition.x, nonAuthorityPosition.x) || !m_AuthoritativeTransform.SyncPositionX;
            var yIsEqual = Approximately(authorityPosition.y, nonAuthorityPosition.y) || !m_AuthoritativeTransform.SyncPositionY;
            var zIsEqual = Approximately(authorityPosition.z, nonAuthorityPosition.z) || !m_AuthoritativeTransform.SyncPositionZ;
            if (!xIsEqual || !yIsEqual || !zIsEqual)
            {
                VerboseDebug($"[{m_AuthoritativeTransform.gameObject.name}] Authority position {authorityPosition} != [{m_NonAuthoritativeTransform.gameObject.name}] NonAuthority position {nonAuthorityPosition}");
            }
            return xIsEqual && yIsEqual && zIsEqual;
        }

        protected bool ScaleValuesMatch(bool printDeltas = false)
        {
            m_CurrentHalfPrecision = k_HalfPrecisionPosScale;
            var authorityScale = m_AuthoritativeTransform.transform.localScale;
            var nonAuthorityScale = m_NonAuthoritativeTransform.transform.localScale;
            var xIsEqual = Approximately(authorityScale.x, nonAuthorityScale.x) || !m_AuthoritativeTransform.SyncScaleX;
            var yIsEqual = Approximately(authorityScale.y, nonAuthorityScale.y) || !m_AuthoritativeTransform.SyncScaleY;
            var zIsEqual = Approximately(authorityScale.z, nonAuthorityScale.z) || !m_AuthoritativeTransform.SyncScaleZ;
            if (!xIsEqual || !yIsEqual || !zIsEqual)
            {
                VerboseDebug($"[{m_AuthoritativeTransform.gameObject.name}] Authority scale {authorityScale} != [{m_NonAuthoritativeTransform.gameObject.name}] NonAuthority scale {nonAuthorityScale}");
            }
            return xIsEqual && yIsEqual && zIsEqual;
        }

        private void PrintPositionRotationScaleDeltas()
        {
            RotationsMatch(true);
            PositionsMatch(true);
            ScaleValuesMatch(true);
        }
    }

    /// <summary>
    /// Helper component for all NetworkTransformTests
    /// </summary>
    internal class NetworkTransformTestComponent : NetworkTransform
    {
        public bool ServerAuthority;
        public bool ReadyToReceivePositionUpdate = false;

        public NetworkTransformState AuthorityLastSentState;
        public bool StatePushed { get; internal set; }

        public delegate void AuthorityPushedTransformStateDelegateHandler(ref NetworkTransformState networkTransformState);

        public event AuthorityPushedTransformStateDelegateHandler AuthorityPushedTransformState;

        protected override void OnAuthorityPushTransformState(ref NetworkTransformState networkTransformState)
        {
            StatePushed = true;
            AuthorityLastSentState = networkTransformState;
            AuthorityPushedTransformState?.Invoke(ref networkTransformState);
            base.OnAuthorityPushTransformState(ref networkTransformState);
        }


        public bool StateUpdated { get; internal set; }
        protected override void OnNetworkTransformStateUpdated(ref NetworkTransformState oldState, ref NetworkTransformState newState)
        {
            StateUpdated = true;
            base.OnNetworkTransformStateUpdated(ref oldState, ref newState);
        }

        protected override bool OnIsServerAuthoritative()
        {
            return ServerAuthority;
        }

        public static NetworkTransformTestComponent AuthorityInstance;

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();

            if (CanCommitToTransform)
            {
                AuthorityInstance = this;
            }

            ReadyToReceivePositionUpdate = true;
        }

        public void CommitToTransform()
        {
            TryCommitTransformToServer(transform, NetworkManager.LocalTime.Time);
        }

        public (bool isDirty, bool isPositionDirty, bool isRotationDirty, bool isScaleDirty) ApplyState()
        {
            var transformState = ApplyLocalNetworkState(transform);
            return (transformState.IsDirty, transformState.HasPositionChange, transformState.HasRotAngleChange, transformState.HasScaleChange);
        }
    }

    /// <summary>
    /// Helper component for NetworkTransform parenting tests when
    /// a child is a parent of another child (i.e. "sub child")
    /// </summary>
    internal class SubChildObjectComponent : ChildObjectComponent
    {
        protected override bool IsSubChild()
        {
            return true;
        }
    }

    /// <summary>
    /// Helper component for NetworkTransform parenting tests
    /// </summary>
    internal class ChildObjectComponent : NetworkTransform
    {
        public static int TestCount;
        public static bool EnableChildLog;
        public static readonly List<ChildObjectComponent> Instances = new List<ChildObjectComponent>();
        public static readonly List<ChildObjectComponent> SubInstances = new List<ChildObjectComponent>();
        public static ChildObjectComponent AuthorityInstance { get; internal set; }
        public static ChildObjectComponent AuthoritySubInstance { get; internal set; }
        public static readonly Dictionary<ulong, NetworkObject> ClientInstances = new Dictionary<ulong, NetworkObject>();
        public static readonly Dictionary<ulong, NetworkObject> ClientSubChildInstances = new Dictionary<ulong, NetworkObject>();

        public static readonly List<ChildObjectComponent> InstancesWithLogging = new List<ChildObjectComponent>();

        public static bool HasSubChild;

        private StringBuilder m_ChildTransformLog = new StringBuilder();
        private StringBuilder m_ChildStateLog = new StringBuilder();

        public static void Reset()
        {
            AuthorityInstance = null;
            AuthoritySubInstance = null;
            HasSubChild = false;
            ClientInstances.Clear();
            ClientSubChildInstances.Clear();
            Instances.Clear();
            SubInstances.Clear();
        }

        public bool ServerAuthority;

        protected virtual bool IsSubChild()
        {
            return false;
        }

        protected override bool OnIsServerAuthoritative()
        {
            return ServerAuthority;
        }

        public override void OnNetworkSpawn()
        {
            LogTransform();

            base.OnNetworkSpawn();

            LogTransform();
            if (CanCommitToTransform)
            {
                if (!IsSubChild())
                {
                    AuthorityInstance = this;
                }
                else
                {
                    AuthoritySubInstance = this;
                }
            }
            else
            {
                if (!IsSubChild())
                {
                    Instances.Add(this);
                }
                else
                {
                    SubInstances.Add(this);
                }
            }
            if (HasSubChild && IsSubChild())
            {
                ClientSubChildInstances.Add(NetworkManager.LocalClientId, NetworkObject);
            }
            else
            {
                ClientInstances.Add(NetworkManager.LocalClientId, NetworkObject);
            }
        }

        public override void OnNetworkDespawn()
        {
            LogToConsole();
            base.OnNetworkDespawn();
        }

        public override void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject)
        {
            base.OnNetworkObjectParentChanged(parentNetworkObject);

            LogTransform();
        }

        protected override void OnAuthorityPushTransformState(ref NetworkTransformState networkTransformState)
        {
            base.OnAuthorityPushTransformState(ref networkTransformState);

            LogState(ref networkTransformState, true);
        }

        protected override void OnNetworkTransformStateUpdated(ref NetworkTransformState oldState, ref NetworkTransformState newState)
        {
            base.OnNetworkTransformStateUpdated(ref oldState, ref newState);
            LogState(ref newState, false);
        }

        protected override void OnSynchronize<T>(ref BufferSerializer<T> serializer)
        {
            base.OnSynchronize(ref serializer);
            var localState = SynchronizeState;
            LogState(ref localState, serializer.IsWriter);
        }

        private void LogTransform()
        {
            if (!EnableChildLog)
            {
                return;
            }
            if (m_ChildTransformLog.Length == 0)
            {
                m_ChildTransformLog.AppendLine($"[{TestCount}][{name}] Begin Child Transform Log (Authority: {CanCommitToTransform})-------------->");
            }
            m_ChildTransformLog.AppendLine($"POS-SR:{GetSpaceRelativePosition()} POS-W: {transform.position} POS-L: {transform.position}");
            m_ChildTransformLog.AppendLine($"SCA-SR:{GetScale()} SCA-LS: {transform.lossyScale} SCA-L: {transform.localScale}");
        }

        private void LogState(ref NetworkTransformState state, bool isPush)
        {
            if (!EnableChildLog)
            {
                return;
            }
            if (m_ChildStateLog.Length == 0)
            {
                m_ChildStateLog.AppendLine($"[{TestCount}][{name}] Begin Child State Log (Authority: {CanCommitToTransform})-------------->");
            }
            var tick = 0;
            if (NetworkManager != null && !NetworkManager.ShutdownInProgress)
            {
                tick = NetworkManager.ServerTime.Tick;
            }

            m_ChildStateLog.AppendLine($"[{state.NetworkTick}][{tick}] Tele:{state.IsTeleportingNextFrame} Sync: {state.IsSynchronizing} Reliable: {state.IsReliableStateUpdate()} IsParented: {state.IsParented} HasPos: {state.HasPositionChange} Pos: {state.GetPosition()}");
            m_ChildStateLog.AppendLine($"Lossy:{state.LossyScale} Scale: {state.GetScale()} Rotation: {state.GetRotation()}");
        }

        private void LogToConsole()
        {
            if (!EnableChildLog)
            {
                return;
            }
            LogBuilder(m_ChildTransformLog);
            LogBuilder(m_ChildStateLog);
        }

        private void LogBuilder(StringBuilder builder)
        {
            if (builder.Length == 0)
            {
                return;
            }
            var contents = builder.ToString();
            var lines = contents.Split('\n');
            if (lines.Length > 45)
            {
                var count = 0;
                var tempBuilder = new StringBuilder();

                for (int i = 0; i < lines.Length; i++)
                {
                    if ((i % 45) == 0)
                    {
                        if (count > 0)
                        {
                            Debug.Log(tempBuilder.ToString());
                            tempBuilder.Clear();
                        }
                        tempBuilder.AppendLine($"{count}{lines[i]}");
                        count++;
                    }
                    else
                    {
                        tempBuilder.AppendLine($"{lines[i]}");
                    }
                }
            }
            else
            {
                Debug.Log(builder.ToString());
            }
        }
    }


} // Unity.Netcode.RuntimeTests
