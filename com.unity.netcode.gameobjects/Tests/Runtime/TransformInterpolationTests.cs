#if !MULTIPLAYER_TOOLS
using System.Collections;
using NUnit.Framework;
using Unity.Netcode.Components;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;


namespace Unity.Netcode.RuntimeTests
{
    internal class TransformInterpolationObject : NetworkTransform
    {
        public static bool TestComplete = false;
        // Set the minimum threshold which we will use as our margin of error
#if UNITY_EDITOR
        public const float MinThreshold = 0.005f;
#else
        // Add additional room for error on console tests
        public const float MinThreshold = 0.009999f;
#endif

        private const int k_TargetLocalSpaceToggles = 10;

        public bool CheckPosition;
        public bool IsMoving;
        public bool IsFixed;

        private float m_FrameRateFractional;
        private bool m_CurrentLocalSpace;

        private int m_LocalSpaceToggles;
        private int m_LastFrameCount;

        public bool ReachedTargetLocalSpaceTransitionCount()
        {
            TestComplete = m_LocalSpaceToggles >= k_TargetLocalSpaceToggles;
            return TestComplete;
        }

        protected override void OnInitialize(ref NetworkTransformState replicatedState)
        {
            m_LocalSpaceToggles = 0;
            m_FrameRateFractional = 1.0f / Application.targetFrameRate;
            PositionThreshold = MinThreshold;
            SetMaxInterpolationBound(1.0f);
            base.OnInitialize(ref replicatedState);
        }

        private int m_StartFrameCount;

        public void StartMoving()
        {
            m_StartFrameCount = Time.frameCount;
            IsMoving = true;
        }

        public void StopMoving()
        {
            IsMoving = false;
        }

        private const int k_MaxThresholdFailures = 4;
        private int m_ExceededThresholdCount;

        public override void OnUpdate()
        {
            base.OnUpdate();


            // Check the position of the nested object on the client
            if (CheckPosition)
            {
                if (transform.position.y < -MinThreshold || transform.position.y > Application.targetFrameRate + MinThreshold)
                {
                    // Temporary work around for this test.
                    // Really, this test needs to be completely re-written.
                    m_ExceededThresholdCount++;
                    // If we haven't corrected ourselves within the maximum number of updates then throw an error.
                    if (m_ExceededThresholdCount > k_MaxThresholdFailures)
                    {
                        Debug.LogError($"Interpolation failure. transform.position.y is {transform.position.y}. Should be between 0.0 and 100.0. Current threshold is [+/- {MinThreshold}].");
                    }
                }
                else
                {
                    // If corrected, then reset our count
                    m_ExceededThresholdCount = 0;
                }
            }
        }

        private void Update()
        {
            base.OnUpdate();

            if (!IsSpawned || !CanCommitToTransform || TestComplete)
            {
                return;
            }


            // Move the nested object on the server
            if (IsMoving)
            {
                Assert.True(CanCommitToTransform, $"Using non-authority instance to update transform!");

                if (m_LastFrameCount == Time.frameCount)
                {
                    Debug.Log($"Detected duplicate frame update count {Time.frameCount}. Ignoring this update.");
                    return;
                }

                m_LastFrameCount = Time.frameCount;

                // Leaving this here for reference.
                // If a system is running at a slower frame rate than expected, then the below code could toggle
                // the local to world space value at a higher frequency which might not provide enough updates to
                // handle interpolating between the transitions.
                //var y = Time.realtimeSinceStartup % 10.0f;
                //// change the space between local and global every second
                //GetComponent<NetworkTransform>().InLocalSpace = ((int)y % 2 == 0);

                // Reduce the total frame count down to the frame rate
                var y = (Time.frameCount - m_StartFrameCount) % Application.targetFrameRate;

                // change the space between local and global every time we hit the expected number of frames
                // (or every second if running at the target frame rate)
                InLocalSpace = y == 0 ? !InLocalSpace : InLocalSpace;

                if (m_CurrentLocalSpace != InLocalSpace)
                {
                    m_LocalSpaceToggles++;
                    m_CurrentLocalSpace = InLocalSpace;
                }

                transform.position = new Vector3(0.0f, (y * m_FrameRateFractional), 0.0f);
            }

            // On the server, make sure to keep the parent object at a fixed position
            if (IsFixed)
            {
                Assert.True(CanCommitToTransform, $"Using non-authority instance to update transform!");
                transform.position = new Vector3(1000.0f, 1000.0f, 1000.0f);
            }
        }
    }

    internal class TransformInterpolationTests : NetcodeIntegrationTest
    {
        protected override int NumberOfClients => 1;

        private GameObject m_PrefabToSpawn;

        private NetworkObject m_SpawnedAsNetworkObject;
        private NetworkObject m_SpawnedObjectOnClient;

        private NetworkObject m_BaseAsNetworkObject;
        private NetworkObject m_BaseOnClient;


        protected override void OnServerAndClientsCreated()
        {
            m_PrefabToSpawn = CreateNetworkObjectPrefab("InterpTestObject");
            var networkTransform = m_PrefabToSpawn.AddComponent<TransformInterpolationObject>();
        }

        private IEnumerator RefreshNetworkObjects()
        {
            var clientId = m_ClientNetworkManagers[0].LocalClientId;
            yield return WaitForConditionOrTimeOut(() => s_GlobalNetworkObjects.ContainsKey(clientId) &&
            s_GlobalNetworkObjects[clientId].ContainsKey(m_BaseAsNetworkObject.NetworkObjectId) &&
            s_GlobalNetworkObjects[clientId].ContainsKey(m_SpawnedAsNetworkObject.NetworkObjectId));

            Assert.False(s_GlobalTimeoutHelper.TimedOut, $"Timed out waiting for client side {nameof(NetworkObject)} ID of {m_SpawnedAsNetworkObject.NetworkObjectId}");

            m_BaseOnClient = s_GlobalNetworkObjects[clientId][m_BaseAsNetworkObject.NetworkObjectId];
            // make sure the objects are set with the right network manager
            m_BaseOnClient.NetworkManagerOwner = m_ClientNetworkManagers[0];

            m_SpawnedObjectOnClient = s_GlobalNetworkObjects[clientId][m_SpawnedAsNetworkObject.NetworkObjectId];
            // make sure the objects are set with the right network manager
            m_SpawnedObjectOnClient.NetworkManagerOwner = m_ClientNetworkManagers[0];
        }

        [UnityTest]
        public IEnumerator TransformInterpolationTest()
        {
            TransformInterpolationObject.TestComplete = false;
            // create an object
            var spawnedObject = Object.Instantiate(m_PrefabToSpawn);
            var baseObject = Object.Instantiate(m_PrefabToSpawn);
            baseObject.GetComponent<NetworkObject>().NetworkManagerOwner = m_ServerNetworkManager;
            baseObject.GetComponent<NetworkObject>().Spawn();

            m_SpawnedAsNetworkObject = spawnedObject.GetComponent<NetworkObject>();
            m_SpawnedAsNetworkObject.NetworkManagerOwner = m_ServerNetworkManager;

            m_BaseAsNetworkObject = baseObject.GetComponent<NetworkObject>();
            m_BaseAsNetworkObject.NetworkManagerOwner = m_ServerNetworkManager;

            m_SpawnedAsNetworkObject.TrySetParent(baseObject);

            m_SpawnedAsNetworkObject.Spawn();

            yield return RefreshNetworkObjects();

            m_SpawnedAsNetworkObject.TrySetParent(baseObject);
            var spawnedObjectNetworkTransform = spawnedObject.GetComponent<TransformInterpolationObject>();
            baseObject.GetComponent<TransformInterpolationObject>().IsFixed = true;
            spawnedObject.GetComponent<TransformInterpolationObject>().StartMoving();

            const float maxPlacementError = 0.01f;

            // Wait for the base object to place itself on both instances
            while (m_BaseOnClient.transform.position.y < 1000 - maxPlacementError ||
                   m_BaseOnClient.transform.position.y > 1000 + maxPlacementError ||
                   baseObject.transform.position.y < 1000 - maxPlacementError ||
                   baseObject.transform.position.y > 1000 + maxPlacementError)
            {
                yield return new WaitForSeconds(0.01f);
            }

            m_SpawnedObjectOnClient.GetComponent<TransformInterpolationObject>().CheckPosition = true;

            // Test that interpolation works correctly for ~10 seconds or 10 local to world space transitions while moving
            // Increasing this duration gives you the opportunity to go check in the Editor how the objects are setup
            // and how they move
            var timeOutHelper = new TimeoutFrameCountHelper(10);
            yield return WaitForConditionOrTimeOut(spawnedObjectNetworkTransform.ReachedTargetLocalSpaceTransitionCount, timeOutHelper);
            VerboseDebug($"[TransformInterpolationTest] Wait condition reached or timed out. Frame Count ({timeOutHelper.GetFrameCount()}) | Time Elapsed ({timeOutHelper.GetTimeElapsed()})");
            AssertOnTimeout($"Failed to reach desired local to world space transitions in the given time!", timeOutHelper);
        }
    }
}
#endif
