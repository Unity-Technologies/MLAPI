using System.Collections;
using System.Text;
using NUnit.Framework;
using TestProject.ManualTests;
using Unity.Netcode.Components;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace TestProject.RuntimeTests
{
    [TestFixture(Interpolation.Interpolation, Precision.Full, NetworkTransform.AuthorityModes.Server, NestedTickSynchronization.TickSynchronized)]
    [TestFixture(Interpolation.Interpolation, Precision.Full, NetworkTransform.AuthorityModes.Owner, NestedTickSynchronization.TickSynchronized)]
    [TestFixture(Interpolation.Interpolation, Precision.Half, NetworkTransform.AuthorityModes.Server, NestedTickSynchronization.TickSynchronized)]
    [TestFixture(Interpolation.Interpolation, Precision.Half, NetworkTransform.AuthorityModes.Owner, NestedTickSynchronization.TickSynchronized)]
    [TestFixture(Interpolation.Interpolation, Precision.Compressed, NetworkTransform.AuthorityModes.Server, NestedTickSynchronization.TickSynchronized)]
    [TestFixture(Interpolation.Interpolation, Precision.Compressed, NetworkTransform.AuthorityModes.Owner, NestedTickSynchronization.TickSynchronized)]
    [TestFixture(Interpolation.NoInterpolation, Precision.Full, NetworkTransform.AuthorityModes.Server, NestedTickSynchronization.TickSynchronized)]
    [TestFixture(Interpolation.NoInterpolation, Precision.Full, NetworkTransform.AuthorityModes.Owner, NestedTickSynchronization.TickSynchronized)]
    [TestFixture(Interpolation.NoInterpolation, Precision.Half, NetworkTransform.AuthorityModes.Server, NestedTickSynchronization.TickSynchronized)]
    [TestFixture(Interpolation.NoInterpolation, Precision.Half, NetworkTransform.AuthorityModes.Owner, NestedTickSynchronization.TickSynchronized)]
    [TestFixture(Interpolation.NoInterpolation, Precision.Compressed, NetworkTransform.AuthorityModes.Server, NestedTickSynchronization.TickSynchronized)]
    [TestFixture(Interpolation.NoInterpolation, Precision.Compressed, NetworkTransform.AuthorityModes.Owner, NestedTickSynchronization.TickSynchronized)]

    [TestFixture(Interpolation.Interpolation, Precision.Full, NetworkTransform.AuthorityModes.Server, NestedTickSynchronization.NormalSynchronize)]
    [TestFixture(Interpolation.Interpolation, Precision.Full, NetworkTransform.AuthorityModes.Owner, NestedTickSynchronization.NormalSynchronize)]
    [TestFixture(Interpolation.Interpolation, Precision.Half, NetworkTransform.AuthorityModes.Server, NestedTickSynchronization.NormalSynchronize)]
    [TestFixture(Interpolation.Interpolation, Precision.Half, NetworkTransform.AuthorityModes.Owner, NestedTickSynchronization.NormalSynchronize)]
    [TestFixture(Interpolation.Interpolation, Precision.Compressed, NetworkTransform.AuthorityModes.Server, NestedTickSynchronization.NormalSynchronize)]
    [TestFixture(Interpolation.Interpolation, Precision.Compressed, NetworkTransform.AuthorityModes.Owner, NestedTickSynchronization.NormalSynchronize)]
    [TestFixture(Interpolation.NoInterpolation, Precision.Full, NetworkTransform.AuthorityModes.Server, NestedTickSynchronization.NormalSynchronize)]
    [TestFixture(Interpolation.NoInterpolation, Precision.Full, NetworkTransform.AuthorityModes.Owner, NestedTickSynchronization.NormalSynchronize)]
    [TestFixture(Interpolation.NoInterpolation, Precision.Half, NetworkTransform.AuthorityModes.Server, NestedTickSynchronization.NormalSynchronize)]
    [TestFixture(Interpolation.NoInterpolation, Precision.Half, NetworkTransform.AuthorityModes.Owner, NestedTickSynchronization.NormalSynchronize)]
    [TestFixture(Interpolation.NoInterpolation, Precision.Compressed, NetworkTransform.AuthorityModes.Server, NestedTickSynchronization.NormalSynchronize)]
    [TestFixture(Interpolation.NoInterpolation, Precision.Compressed, NetworkTransform.AuthorityModes.Owner, NestedTickSynchronization.NormalSynchronize)]
    public class NestedNetworkTransformTests : IntegrationTestWithApproximation
    {
        private const string k_TestScene = "NestedNetworkTransformTestScene";
        private const string k_PlayerToLoad = "PlayerNestedTransforms";

        protected override int NumberOfClients => 0;

        protected override bool m_EnableTimeTravel => true;
        protected override bool m_SetupIsACoroutine => true;
        protected override bool m_TearDownIsACoroutine => false;

        private float m_OriginalVarianceThreshold;

        private Scene m_BaseSceneLoaded;
        private Scene m_OriginalActiveScene;

        private Object m_PlayerPrefabResource;
        private Interpolation m_Interpolation;
        private Precision m_Precision;
        private NetworkTransform.AuthorityModes m_Authority;
        private NestedTickSynchronization m_NestedTickSynchronization;

        public enum Interpolation
        {
            NoInterpolation,
            Interpolation
        }

        public enum Precision
        {
            Compressed,
            Half,
            Full
        }

        public enum AuthoritativeModel
        {
            Server,
            Owner
        }

        public enum NestedTickSynchronization
        {
            NormalSynchronize,
            TickSynchronized,
        }


        public NestedNetworkTransformTests(Interpolation interpolation, Precision precision, NetworkTransform.AuthorityModes authoritativeModel, NestedTickSynchronization nestedTickSynchronization)
        {
            m_Interpolation = interpolation;
            m_Precision = precision;
            m_Authority = authoritativeModel;
            m_NestedTickSynchronization = nestedTickSynchronization;
        }

        public NestedNetworkTransformTests()
        {

        }

        protected override void OnOneTimeSetup()
        {
            m_OriginalVarianceThreshold = base.GetDeltaVarianceThreshold();
            ChildMover.RandomizeScale = true;
            // Preserve the test runner scene that is currently the active scene
            m_OriginalActiveScene = SceneManager.GetActiveScene();
            // Load our test's scene used by all client players (it gets set as the currently active scene when loaded)
            m_PlayerPrefabResource = Resources.Load(k_PlayerToLoad);
            Assert.NotNull(m_PlayerPrefabResource, $"Failed to load resource {k_PlayerToLoad}");

            // Migrate the resource into the DDOL to prevent the server from thinking it is in-scene placed.
            Object.DontDestroyOnLoad(m_PlayerPrefabResource);
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.LoadScene(k_TestScene, LoadSceneMode.Additive);
            base.OnOneTimeSetup();
        }

        protected override void OnOneTimeTearDown()
        {
            ChildMover.RandomizeScale = false;
            // Set test runner's scene back to the currently active scene
            if (m_OriginalActiveScene.IsValid() && m_OriginalActiveScene.isLoaded)
            {
                SceneManager.SetActiveScene(m_OriginalActiveScene);
            }
            // Unload our base scene if it is still loaded
            if (m_BaseSceneLoaded.IsValid() && m_BaseSceneLoaded.isLoaded)
            {
                SceneManager.UnloadSceneAsync(m_BaseSceneLoaded);
            }
            base.OnOneTimeTearDown();
        }

        protected override IEnumerator OnSetup()
        {
            yield return WaitForConditionOrTimeOut(() => m_BaseSceneLoaded.IsValid() && m_BaseSceneLoaded.isLoaded);
            AssertOnTimeout($"Timed out waiting for scene {k_TestScene} to load!");
        }

        private void SceneManager_sceneLoaded(Scene sceneLoaded, LoadSceneMode loadSceneMode)
        {
            if (loadSceneMode == LoadSceneMode.Additive && sceneLoaded.name == k_TestScene)
            {
                m_BaseSceneLoaded = sceneLoaded;
                SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
                SceneManager.SetActiveScene(sceneLoaded);
            }
        }

        protected override void OnInlineTearDown()
        {
            // This prevents us from trying to destroy the resource loaded
            m_PlayerPrefab = null;
        }

        private void ConfigureNetworkTransform(IntegrationNetworkTransform networkTransform)
        {
            networkTransform.Interpolate = m_Interpolation == Interpolation.Interpolation;
            networkTransform.UseQuaternionSynchronization = true;
            networkTransform.UseHalfFloatPrecision = m_Precision == Precision.Half || m_Precision == Precision.Compressed;
            networkTransform.UseQuaternionCompression = m_Precision == Precision.Compressed;
            networkTransform.AuthorityMode = m_Authority;
            networkTransform.TickSyncChildren = m_NestedTickSynchronization == NestedTickSynchronization.TickSynchronized;
        }


        protected override void OnCreatePlayerPrefab()
        {
            // Destroy the default player prefab
            Object.DestroyImmediate(m_PlayerPrefab);
            // Assign the network prefab resource loaded
            m_PlayerPrefab = m_PlayerPrefabResource as GameObject;
            m_PlayerPrefab.name = "Player";

            ConfigureNetworkTransform(m_PlayerPrefab.GetComponent<IntegrationNetworkTransform>());
            var networkTransforms = m_PlayerPrefab.GetComponentsInChildren<IntegrationNetworkTransform>();
            foreach (var networkTransform in networkTransforms)
            {
                ConfigureNetworkTransform(networkTransform);
                if (networkTransform.TickSyncChildren && networkTransform.gameObject != m_PlayerPrefab)
                {
                    // Skew the thresholds of the children
                    networkTransform.PositionThreshold *= Random.Range(0.75f, 0.90f);
                    networkTransform.RotAngleThreshold *= Random.Range(0.75f, 0.90f);
                }
            }

            base.OnCreatePlayerPrefab();
        }

        /// <summary>
        /// Prevent the server from telling the clients to load our test scene
        /// </summary>
        private bool VerifySceneServer(int sceneIndex, string sceneName, LoadSceneMode loadSceneMode)
        {
            if (sceneName == k_TestScene)
            {
                return false;
            }
            return true;
        }

        // Acceptable variances when using half precision
        private const float k_PositionScaleVariance = 0.1255f;
        private const float k_RotationVariance = 0.1255f;
        private const float k_RotationVarianceCompressed = 0.555f;

        private float m_CurrentVariance = k_PositionScaleVariance;
        protected override float GetDeltaVarianceThreshold()
        {
            return m_CurrentVariance;
        }

        private StringBuilder m_ValidationErrors;

        /// <summary>
        /// Validates all transform instance values match the authority's
        /// </summary>
        private bool ValidateNetworkTransforms()
        {
            m_ValidationErrors.Clear();
            foreach (var connectedClient in m_ServerNetworkManager.ConnectedClientsIds)
            {
                var authorityId = m_Authority == NetworkTransform.AuthorityModes.Server ? m_ServerNetworkManager.LocalClientId : connectedClient;
                var playerToValidate = m_PlayerNetworkObjects[authorityId][connectedClient];
                var playerNetworkTransforms = playerToValidate.GetComponentsInChildren<IntegrationNetworkTransform>();
                foreach (var playerRelative in m_PlayerNetworkObjects)
                {
                    if (playerRelative.Key == authorityId)
                    {
                        continue;
                    }
                    var relativeClonedTransforms = playerRelative.Value[connectedClient].GetComponentsInChildren<IntegrationNetworkTransform>();
                    // TODO: Determine why MAC OS X on 2020.3 has precision issues when interpolating using full precision but no other platform does nor does MAC OS X on later versions of Unity.
#if UNITY_2021_3_OR_NEWER
                    m_CurrentVariance = m_Precision == Precision.Full ? m_OriginalVarianceThreshold : k_PositionScaleVariance;
                    m_CurrentVariance += m_Interpolation == Interpolation.Interpolation && m_Precision != Precision.Full ? 0.10f : m_Interpolation == Interpolation.Interpolation ? 0.10f : 0.0f;
#else
                    m_CurrentVariance = SystemInfo.operatingSystem.Contains("Mac OS X") ? k_PositionScaleVariance : m_Precision == Precision.Full ? m_OriginalVarianceThreshold : k_PositionScaleVariance;
                    m_CurrentVariance += m_Interpolation == Interpolation.Interpolation && m_Precision != Precision.Full ? 0.10f : m_Interpolation == Interpolation.Interpolation ? 0.10f : 0.0f;
#endif
                    for (int i = 0; i < playerNetworkTransforms.Length; i++)
                    {
                        var playerCurrentPosition = playerNetworkTransforms[i].PushedPosition;
                        var clonePosition = relativeClonedTransforms[i].GetSpaceRelativePosition();
                        var playerGameObjectName = playerNetworkTransforms[i].gameObject.name;
                        var cloneGameObjectName = relativeClonedTransforms[i].gameObject.name;
                        if (!Approximately(playerCurrentPosition, clonePosition))
                        {
                            // Now check to see if the non-authority's current target (state) position is not within aproximate range
                            var statePosition = relativeClonedTransforms[i].GetSpaceRelativePosition(true);
                            if (!Approximately(statePosition, clonePosition))
                            {
                                m_ValidationErrors.Append($"[Position][Client-{connectedClient}-{playerGameObjectName} {GetVector3Values(ref playerCurrentPosition)}][Failing on Client-{playerRelative.Key} for Clone-{relativeClonedTransforms[i].OwnerClientId}-{cloneGameObjectName} {GetVector3Values(ref clonePosition)}]\n");
                                m_ValidationErrors.Append($"[Authority Pushed Position {playerCurrentPosition}] [NonAuthority State Position {GetVector3Values(ref statePosition)}] - [NonAuthority Position {GetVector3Values(ref clonePosition)}]\n");
                            }
                        }

                        if (!Approximately(playerNetworkTransforms[i].transform.localScale, relativeClonedTransforms[i].transform.localScale))
                        {
                            var playerScale = playerNetworkTransforms[i].transform.localScale;
                            var relativeScale = relativeClonedTransforms[i].transform.localScale;
                            m_ValidationErrors.Append($"[Scale][Client-{connectedClient} {GetVector3Values(ref playerScale)}][Failing on Client-{playerRelative.Key} for Clone-{relativeClonedTransforms[i].OwnerClientId} {GetVector3Values(ref relativeScale)}]\n");
                        }

                        // TODO: Determine why MAC OS X on 2020.3 has precision issues when interpolating using full precision but no other platform does nor does MAC OS X on later versions of Unity.
#if UNITY_2021_3_OR_NEWER
                        m_CurrentVariance = m_Precision == Precision.Full ? m_OriginalVarianceThreshold : k_RotationVariance;
                        if (m_Precision == Precision.Compressed)
                        {
                            m_CurrentVariance = k_RotationVarianceCompressed;
                        }
                        m_CurrentVariance += m_Interpolation == Interpolation.Interpolation && m_Precision != Precision.Full ? 0.10333f : 0.0f;
#else
                        m_CurrentVariance = SystemInfo.operatingSystem.Contains("Mac OS X") ? k_RotationVariance : m_Precision == Precision.Full ? m_OriginalVarianceThreshold : k_RotationVariance;
                        if (m_Precision == Precision.Compressed)
                        {
                            m_CurrentVariance = k_RotationVarianceCompressed;
                        }
                        m_CurrentVariance += m_Interpolation == Interpolation.Interpolation && m_Precision != Precision.Full ? 0.10333f : 0.0f;
#endif
                        var playerCurrentRotation = playerNetworkTransforms[i].PushedRotation;
                        var cloneRotation = relativeClonedTransforms[i].GetSpaceRelativeRotation();
                        if (!ApproximatelyEuler(playerCurrentRotation.eulerAngles, cloneRotation.eulerAngles))
                        {
                            // Double check Euler as quaternions can have inverted Vector4 values from one another but be the same when comparing Euler
                            if (!ApproximatelyEuler(playerCurrentRotation.eulerAngles, cloneRotation.eulerAngles))
                            {
                                var deltaEuler = new Vector3(Mathf.DeltaAngle(playerCurrentRotation.eulerAngles.x, cloneRotation.eulerAngles.x),
                                    Mathf.DeltaAngle(playerCurrentRotation.eulerAngles.y, cloneRotation.eulerAngles.y),
                                    Mathf.DeltaAngle(playerCurrentRotation.eulerAngles.z, cloneRotation.eulerAngles.z));
                                var eulerPlayer = playerCurrentRotation.eulerAngles;
                                var eulerClone = cloneRotation.eulerAngles;

                                m_ValidationErrors.Append($"[Rotation][Client-{connectedClient} ({GetVector3Values(ref eulerPlayer)})][Failing on Client-{playerRelative.Key} for Clone-{relativeClonedTransforms[i].OwnerClientId}-{cloneGameObjectName} ({GetVector3Values(ref eulerClone)})]\n");
                                m_ValidationErrors.Append($"[Rotation Delta] ({deltaEuler.x}, {deltaEuler.y}, {deltaEuler.z})\n");
                            }
                        }
                    }
                }
            }

            // If we had any variance failures logged, then we failed
            return m_ValidationErrors.Length == 0;
        }
        private const int k_IterationsToTest = 10;
        private const int k_ClientsToSpawn = 2;  // Really it will be 3 including the host

        // Number of failures in a row with no correction in precision for the test to fail
        private const int k_MaximumPrecisionFailures = 5;

        public int CalculateTimeOutFrames(float timeOutPeriod, uint tickRate = k_DefaultTickRate)
        {
            // Calculate the expected number of frame updates that should occur during the tick count wait period
            var frameFrequency = 1.0f / (Application.targetFrameRate >= 60 && Application.targetFrameRate <= 100 ? Application.targetFrameRate : 60.0f);
            var tickFrequency = 1.0f / tickRate;
            var framesPerTick = tickFrequency / frameFrequency;
            var totalExpectedTicks = timeOutPeriod / tickFrequency;

            return (int)(framesPerTick * totalExpectedTicks);
        }

        [Test]
        public void NestedNetworkTransformSynchronization()
        {
            var timeStarted = Time.realtimeSinceStartup;
            var startFrameCount = Time.frameCount;
            m_EnableVerboseDebug = true;
            AutomatedPlayerMover.StopMovement = false;
            ChildMoverManager.StopMovement = false;
            m_ValidationErrors = new StringBuilder();

            var pauseFrames = CalculateTimeOutFrames(1f);
            var synchTimeOut = CalculateTimeOutFrames(m_Interpolation == Interpolation.Interpolation ? k_DefaultTickRate * 3 : k_DefaultTickRate * 2);

            m_ServerNetworkManager.SceneManager.VerifySceneBeforeLoading = VerifySceneServer;

            // We want it to timeout and it is "ok", just assuring that (n) frames have passed
            TimeTravel(1f / k_DefaultTickRate, pauseFrames);
            // ** Do NOT AssertOnTimeout here **

            var precisionFailures = 0;
            var clientCount = 0;

            // 5 of the iterations are spent spawning
            // All iterations test transform values against the authority and non-authority instances
            for (int i = 0; i < k_IterationsToTest; i++)
            {
                ChildMoverManager.StopMovement = true;
                AutomatedPlayerMover.StopMovement = true;
                var success = WaitForConditionOrTimeOutWithTimeTravel(ValidateNetworkTransforms, synchTimeOut);
                // Do one last validation pass to make sure one (or more) transforms does not fall within the acceptable ranges
                if (!ValidateNetworkTransforms())
                {
                    // If not, then log errors to console and clear the current pass validation errors
                    if (m_ValidationErrors.Length > 0)
                    {
                        VerboseDebug($"{m_ValidationErrors}");
                    }
                    precisionFailures++;
                    // With half float values, we can sometimes have values that exceed typical thresholds but they
                    // should correct themselves over time. This is to allow enough passes to allow for this correction
                    // to occur for delta position (especially), half float quaternions, and quaternion compression.
                    // If we have 5 precision failures in a row and fail to correct, then fail this test
                    if (precisionFailures > k_MaximumPrecisionFailures)
                    {
                        m_EnableVerboseDebug = true;
                        DisplayFrameAndTimeInfo(timeStarted, startFrameCount, false);
                        Assert.True(success, $"[{m_Interpolation}][{m_Precision}][{m_Authority}][Iteration: {i}]\n[Precision Failure] Exceeded Precision Failure Count " +
                            $"({precisionFailures})\n Timed out waiting for all nested NetworkTransform cloned instances to match!\n{m_ValidationErrors}");
                        Assert.IsTrue(false, $"Timed out waiting for all nested NetworkTransform cloned instances to match:\n{m_ValidationErrors}");
                    }
                    else
                    {
                        VerboseDebug($"[Precision Failure] {precisionFailures}");
                    }
                }
                else
                {
                    if (precisionFailures > 0)
                    {
                        VerboseDebug($"[{i}][Precision Corrected] Synchronized After");
                        precisionFailures = 0;
                    }
                }

                if (clientCount < k_ClientsToSpawn)
                {
                    CreateAndStartNewClientWithTimeTravel();
                    clientCount++;

                    success = WaitForConditionOrTimeOutWithTimeTravel(NewClientInstanceIsOnAllClients, synchTimeOut);
                    if (!success)
                    {
                        DisplayFrameAndTimeInfo(timeStarted, startFrameCount, false);
                    }
                    Assert.True(success, $"Timed out waiting for all players to have spawned instances of all other players!");
                }


                AutomatedPlayerMover.StopMovement = false;
                ChildMoverManager.StopMovement = false;
                // Now let everything move around a bit
                // We want it to timeout and it is "ok", just assuring that (n) frames have passed
                TimeTravel(1f / k_DefaultTickRate, pauseFrames);
                // ** Do NOT AssertOnTimeout here **

                // If we are just about to end this test but there are running precision
                // failures, we need to keep running until it corrects itself or fails
                if (precisionFailures > 0 && i == (k_IterationsToTest - 1))
                {
                    VerboseDebug($"Extending test iterations with {precisionFailures} precision failures still pending correction.");
                    i--;
                }
            }

            DisplayFrameAndTimeInfo(timeStarted, startFrameCount);
        }

        private void DisplayFrameAndTimeInfo(float startTime, int startFrameCount, bool completed = true)
        {
            var completedOrFailed = completed ? "Completed" : "Failed";
            var duration = Time.realtimeSinceStartup - startTime;
            var frameCount = Time.frameCount - startFrameCount;
            var avgFPS = frameCount / duration;
            Debug.Log($"[NestedNetworkTransformSynchronization][{completedOrFailed}] Duration: ({duration}) | Frame Count: ({frameCount}) | Average FPS: ({avgFPS}) | Expected FPS ({Application.targetFrameRate})");
        }

        /// <summary>
        /// Make sure all clients have spawned the existing and new players
        /// </summary>
        private bool NewClientInstanceIsOnAllClients()
        {
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                var clientId = clientNetworkManager.LocalClientId;
                foreach (var playerObjects in m_PlayerNetworkObjects)
                {
                    if (!playerObjects.Value.ContainsKey(clientId))
                    {
                        return false;
                    }
                    if (!playerObjects.Value[clientId].IsSpawned)
                    {
                        return false;
                    }
                }
            }
            return true;
        }

    }
}
