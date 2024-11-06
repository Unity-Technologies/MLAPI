using System.Collections;
using System.Text;
using NUnit.Framework;
using TestProject.ManualTests;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace TestProject.RuntimeTests
{
    [TestFixture(NetworkTopologyTypes.DistributedAuthority)]
    [TestFixture(NetworkTopologyTypes.ClientServer)]
    public class ParentingInSceneObjectsTests : IntegrationTestWithApproximation
    {
        private const string k_BaseSceneToLoad = "UnitTestBaseScene";
        private const string k_TestSceneToLoad = "ParentingInSceneObjects";
        private const int k_NumIterationsDeparentReparent = 100;
        private const float k_AproximateThresholdValue = 0.1f;

        private bool m_InitialClientsLoadedScene;
        private StringBuilder m_ErrorValidationLog = new StringBuilder(0x2000);

        protected override int NumberOfClients => 2;

        public ParentingInSceneObjectsTests(NetworkTopologyTypes networkTopologyType) : base(networkTopologyType) { }

        protected override void OnOneTimeSetup()
        {
            NetworkManagerTestDisabler.IsIntegrationTest = true;
            ManualTestAssetsDestroyer.IsIntegrationTest = true;
            base.OnOneTimeSetup();
        }

        protected override void OnOneTimeTearDown()
        {
            NetworkManagerTestDisabler.IsIntegrationTest = false;
            ManualTestAssetsDestroyer.IsIntegrationTest = false;
            base.OnOneTimeTearDown();
        }

        private Scene m_BaseSceneLoaded;

        protected override IEnumerator OnSetup()
        {
            InSceneParentChildHandler.ResetInstancesTracking(m_EnableVerboseDebug);
            InSceneParentedUnderGameObjectHandler.Instances.Clear();
            ParentingAutoSyncManager.Reset();
            return base.OnSetup();
        }

        private void SceneManager_sceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name == k_BaseSceneToLoad)
            {
                m_BaseSceneLoaded = scene;
                SceneManager.sceneLoaded -= SceneManager_sceneLoaded;
            }
        }

        protected override IEnumerator OnTearDown()
        {
            ParentingAutoSyncManager.Reset();

            if (m_BaseSceneLoaded.IsValid() && m_BaseSceneLoaded.isLoaded)
            {
                SceneManager.UnloadSceneAsync(m_BaseSceneLoaded);
            }
            yield return base.OnTearDown();
        }


        private void GeneratePositionDoesNotMatch(InSceneParentChildHandler serverHandler, InSceneParentChildHandler clientHandler)
        {
            m_ErrorValidationLog.AppendLine($"[Client-{clientHandler.NetworkManager.LocalClientId}] {nameof(NetworkObject)}-{clientHandler.NetworkObjectId}'s " +
                $"position {GetVector3Values(clientHandler.transform.position)} does not equal the server-side position {GetVector3Values(serverHandler.transform.position)}");
        }

        private void GenerateRotationDoesNotMatch(InSceneParentChildHandler serverHandler, InSceneParentChildHandler clientHandler)
        {
            m_ErrorValidationLog.AppendLine($"[Client-{clientHandler.NetworkManager.LocalClientId}] {nameof(NetworkObject)}-{clientHandler.NetworkObjectId}'s " +
                $"rotation {GetVector3Values(clientHandler.transform.eulerAngles)} does not equal the server-side rotation {GetVector3Values(serverHandler.transform.eulerAngles)}");
        }

        private void GenerateScaleDoesNotMatch(InSceneParentChildHandler serverHandler, InSceneParentChildHandler clientHandler)
        {
            m_ErrorValidationLog.AppendLine($"[Client-{clientHandler.NetworkManager.LocalClientId}] {nameof(NetworkObject)}-{clientHandler.NetworkObjectId}'s " +
                $"scale {GetVector3Values(clientHandler.transform.localScale)} does not equal the server-side scale {GetVector3Values(serverHandler.transform.localScale)}");
        }

        private void GenerateParentIsNotCorrect(InSceneParentChildHandler handler, bool shouldHaveParent, bool isStillSpawnedCheck = false)
        {
            var serverOrClient = handler.NetworkManager.IsServer ? "Server" : "Client";
            var shouldNotBeSpawned = isStillSpawnedCheck ? " and is still spawned!" : string.Empty;
            if (!shouldHaveParent)
            {
                m_ErrorValidationLog.AppendLine($"[{serverOrClient}-{handler.NetworkManager.LocalClientId}] {nameof(NetworkObject)}-{handler.NetworkObjectId}'s still has the parent {handler.transform.parent.name} when it should be null{shouldNotBeSpawned}!");
            }
            else
            {
                m_ErrorValidationLog.AppendLine($"[{serverOrClient}-{handler.NetworkManager.LocalClientId}] {nameof(NetworkObject)}-{handler.NetworkObjectId}'s does not have a parent when it should!");
            }
        }

        private void GenerateTransformInfo(InSceneParentChildHandler serverHandler, InSceneParentChildHandler clientHandler, ulong failingClient)
        {
            if (clientHandler.NetworkManager.LocalClientId != failingClient)
            {
                return;
            }
            var serverChild = serverHandler.transform;
            var clientChild = clientHandler.transform;
            m_ErrorValidationLog.AppendLine($"[server-child][{serverChild.name}] Pos: {serverChild.position} | Rot: {serverChild.eulerAngles} | Scale: {serverChild.localScale}");
            m_ErrorValidationLog.AppendLine($"[client-child][{clientChild.name}] Pos: {clientChild.position} | Rot: {clientChild.eulerAngles} | Scale: {clientChild.localScale}");
            var clientParent = clientHandler.transform.parent;
            var serverParent = serverHandler.transform.parent;
            while (serverParent && clientParent)
            {
                m_ErrorValidationLog.AppendLine($"[server-parent][{serverParent.name}] Pos: {serverParent.position} | Rot: {serverParent.eulerAngles} | Scale: {serverParent.localScale}");
                m_ErrorValidationLog.AppendLine($"[client-parent][{clientParent.name}] Pos: {clientParent.position} | Rot: {clientParent.eulerAngles} | Scale: {clientParent.localScale}");
                clientParent = clientParent.parent;
                serverParent = serverParent.parent;
            }
        }

        private bool ValidateClientsAgainstAuthorityTransformValues()
        {
            // We reset this each time because we are only interested in the last time it checked and failed
            m_ErrorValidationLog.Clear();
            var passed = true;
            var failingClient = (ulong)0;
            foreach (var instance in InSceneParentChildHandler.AuthorityRelativeInstances)
            {
                var serverInstanceTransform = instance.Value.transform;
                foreach (var clientInstances in InSceneParentChildHandler.ClientRelativeInstances)
                {
                    Assert.True(clientInstances.Value.ContainsKey(instance.Key), $"Client-{clientInstances.Key} did not spawn NetworkObject-{instance.Key}!");
                    var clientInstance = clientInstances.Value[instance.Key];
                    var clientInstanceTransform = clientInstance.transform;
                    if (!Approximately(serverInstanceTransform.position, clientInstanceTransform.position))
                    {
                        GeneratePositionDoesNotMatch(instance.Value, clientInstance);
                        passed = false;
                        failingClient = clientInstance.NetworkManager.LocalClientId;
                    }

                    if (!Approximately(serverInstanceTransform.eulerAngles, clientInstanceTransform.eulerAngles))
                    {
                        GenerateRotationDoesNotMatch(instance.Value, clientInstance);
                        passed = false;
                        failingClient = clientInstance.NetworkManager.LocalClientId;
                    }

                    if (!Approximately(serverInstanceTransform.localScale, clientInstanceTransform.localScale))
                    {
                        GenerateScaleDoesNotMatch(instance.Value, clientInstance);
                        passed = false;
                        failingClient = clientInstance.NetworkManager.LocalClientId;
                    }

                    if (!passed && LogTransform)
                    {
                        GenerateTransformInfo(instance.Value, clientInstance, failingClient);
                    }
                }
            }
            return passed;
        }

        internal bool LogTransform;

        private bool ValidateAllChildrenParentingStatus(bool checkForParent)
        {
            m_ErrorValidationLog.Clear();
            foreach (var instance in InSceneParentChildHandler.AuthorityRelativeInstances)
            {
                if (!instance.Value.IsRootParent)
                {
                    if (checkForParent && instance.Value.transform.parent == null)
                    {
                        GenerateParentIsNotCorrect(instance.Value, checkForParent);
                        return false;
                    }
                    else if (!checkForParent && instance.Value.transform.parent != null)
                    {
                        GenerateParentIsNotCorrect(instance.Value, checkForParent);
                        return false;
                    }
                }
            }

            foreach (var clientInstances in InSceneParentChildHandler.ClientRelativeInstances)
            {
                foreach (var instance in clientInstances.Value)
                {
                    if (!instance.Value.IsRootParent)
                    {
                        if (checkForParent && instance.Value.transform.parent == null)
                        {
                            GenerateParentIsNotCorrect(instance.Value, checkForParent);
                            return false;
                        }
                        else if (!checkForParent && instance.Value.transform.parent != null)
                        {
                            GenerateParentIsNotCorrect(instance.Value, checkForParent);
                            return false;
                        }
                    }
                }
            }

            return true;
        }

        protected override float GetDeltaVarianceThreshold()
        {
            return k_AproximateThresholdValue;
        }

        public enum ParentingSpace
        {
            WorldPositionStays,
            WorldPositionDoesNotStay
        }

        /// <summary>
        /// This tests various nested children scenarios where it tests:
        /// Children have their parent removed
        /// The associated children and root parent have their position, rotation, and scale changed
        /// Children are placed back under their respective parents
        /// Verifying a late joining client's cloned version of the in-scene placed NetworkObject children
        /// have the correct transform values and parent set (or no parent)
        /// That users can remove parents, assign parents, and set transform values in the same pass.
        /// All of the above is tested with WorldPositionStays set to both true and false.
        /// </summary>
        [UnityTest]
        public IEnumerator InSceneParentingTest([Values] ParentingSpace parentingSpace)
        {
            InSceneParentChildHandler.WorldPositionStays = parentingSpace == ParentingSpace.WorldPositionStays;
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.LoadScene(k_BaseSceneToLoad, LoadSceneMode.Additive);
            m_InitialClientsLoadedScene = false;
            m_ServerNetworkManager.SceneManager.OnSceneEvent += SceneManager_OnSceneEvent;

            var sceneEventStartedStatus = m_ServerNetworkManager.SceneManager.LoadScene(k_TestSceneToLoad, LoadSceneMode.Additive);
            Assert.True(sceneEventStartedStatus == SceneEventProgressStatus.Started, $"Failed to load scene {k_TestSceneToLoad} with a return status of {sceneEventStartedStatus}.");
            yield return WaitForConditionOrTimeOut(() => m_InitialClientsLoadedScene);
            AssertOnTimeout($"Timed out waiting for all clients to load scene {k_TestSceneToLoad}!");

            var debugWait = new WaitForSeconds(2);
            // [Currently Connected Clients]
            // remove the parents, change all transform values, and re-parent
            InSceneParentChildHandler.AuthorityRootParent.DeparentSetValuesAndReparent();
            yield return WaitForConditionOrTimeOut(ValidateClientsAgainstAuthorityTransformValues);


            if (s_GlobalTimeoutHelper.TimedOut)
            {
                InSceneParentChildHandler.AuthorityRootParent.CheckChildren();
                yield return debugWait;
            }
            AssertOnTimeout($"Timed out waiting for all clients transform values to match the authority transform values!\n {m_ErrorValidationLog}");

            // [Late Join Client #1]
            // Make sure the late joining client synchronizes properly
            yield return CreateAndStartNewClient();
            yield return WaitForConditionOrTimeOut(ValidateClientsAgainstAuthorityTransformValues);
            if (s_GlobalTimeoutHelper.TimedOut)
            {
                InSceneParentChildHandler.AuthorityRootParent.CheckChildren();
                yield return debugWait;
            }
            AssertOnTimeout($"[Late Join 1] Timed out waiting for the late joining client's transform values to match the authority transform values!\n {m_ErrorValidationLog}");

            // Remove the parents from all of the children
            InSceneParentChildHandler.AuthorityRootParent.DeparentAllChildren();
            yield return WaitForConditionOrTimeOut(ValidateClientsAgainstAuthorityTransformValues);
            if (s_GlobalTimeoutHelper.TimedOut)
            {
                InSceneParentChildHandler.AuthorityRootParent.CheckChildren();
                yield return debugWait;
            }
            AssertOnTimeout($"[Late Join 1] Timed out waiting for all clients transform values to match the authority transform values!\n {m_ErrorValidationLog}");

            yield return WaitForConditionOrTimeOut(() => ValidateAllChildrenParentingStatus(false));
            AssertOnTimeout($"[Late Join 1] Timed out waiting for all children to be removed from their parent!\n {m_ErrorValidationLog}");

            // [Late Join Client #2]
            // Make sure the late joining client synchronizes properly with all children having their parent removed
            yield return CreateAndStartNewClient();
            yield return WaitForConditionOrTimeOut(ValidateClientsAgainstAuthorityTransformValues);
            if (s_GlobalTimeoutHelper.TimedOut)
            {
                InSceneParentChildHandler.AuthorityRootParent.CheckChildren();
                yield return debugWait;
            }
            AssertOnTimeout($"[Late Join 2] Timed out waiting for the late joining client's transform values to match the authority transform values!\n {m_ErrorValidationLog}");

            // Just a sanity check that late joining client #2 has no child parented
            yield return WaitForConditionOrTimeOut(() => ValidateAllChildrenParentingStatus(false));
            if (s_GlobalTimeoutHelper.TimedOut)
            {
                InSceneParentChildHandler.AuthorityRootParent.CheckChildren();
                yield return debugWait;
            }
            AssertOnTimeout($"[Late Join 2] Timed out verifying late joined client's children objects to have no parent!\n {m_ErrorValidationLog}");

            // Finally, re-parent all of the children to make sure late joining client #2 synchronizes properly
            InSceneParentChildHandler.AuthorityRootParent.ReParentAllChildren();
            yield return WaitForConditionOrTimeOut(ValidateClientsAgainstAuthorityTransformValues);
            if (s_GlobalTimeoutHelper.TimedOut)
            {
                InSceneParentChildHandler.AuthorityRootParent.CheckChildren();
                yield return debugWait;
            }
            AssertOnTimeout($"[Late Join 2] Timed out waiting for all clients' transform values to match the authority transform values!\n {m_ErrorValidationLog}");
            yield return WaitForConditionOrTimeOut(() => ValidateAllChildrenParentingStatus(true));
            if (s_GlobalTimeoutHelper.TimedOut)
            {
                InSceneParentChildHandler.AuthorityRootParent.CheckChildren();
                yield return debugWait;
            }
            AssertOnTimeout($"[Late Join 2] Timed out waiting for all clients' to re-parent all targeted child objects!\n {m_ErrorValidationLog}");

            // Now run through many iterations where we remove the parents, set the parents, and while
            // the parents are being set the InSceneParentChildHandler assigns new position, rotation, and scale values
            // in the OnNetworkObjectParentChanged overridden method on the server side only
            for (int i = 0; i < k_NumIterationsDeparentReparent; i++)
            {
                InSceneParentChildHandler.AuthorityRootParent.DeparentSetValuesAndReparent();

                yield return WaitForConditionOrTimeOut(ValidateClientsAgainstAuthorityTransformValues);
                if (s_GlobalTimeoutHelper.TimedOut)
                {
                    InSceneParentChildHandler.AuthorityRootParent.CheckChildren();
                    yield return debugWait;
                }
                AssertOnTimeout($"[Final Pass][Deparent-Reparent-{i}] Timed out waiting for all clients transform values to match the server transform values!\n {m_ErrorValidationLog}");

                yield return WaitForConditionOrTimeOut(() => ValidateAllChildrenParentingStatus(true));
                if (s_GlobalTimeoutHelper.TimedOut)
                {
                    InSceneParentChildHandler.AuthorityRootParent.CheckChildren();
                    yield return debugWait;
                }
                AssertOnTimeout($"[Final Pass][Deparent-Reparent-{i}] Timed out waiting for all children to be removed from their parent!\n {m_ErrorValidationLog}");
            }

            // In the final pass, we remove the second generation nested child
            var firstGenChild = InSceneParentChildHandler.AuthorityRootParent.transform.GetChild(0);
            var secondGenChild = firstGenChild.GetChild(0);
            var secondGenChildNetworkObject = secondGenChild.GetComponent<NetworkObject>();
            Assert.True(secondGenChildNetworkObject.TrySetParent((NetworkObject)null, false), $"[Final Pass] Failed to remove the parent from the second generation child!");

            // Validate all transform values match
            yield return WaitForConditionOrTimeOut(ValidateClientsAgainstAuthorityTransformValues);
            AssertOnTimeout($"[Final Pass] Timed out waiting for all clients transform values to match the server transform values after the second generation child's parent was removed!\n {m_ErrorValidationLog}");

            // Now run through one last de-parent, re-parent, and set new values pass to make sure everything still synchronizes
            InSceneParentChildHandler.AuthorityRootParent.DeparentSetValuesAndReparent();

            yield return WaitForConditionOrTimeOut(ValidateClientsAgainstAuthorityTransformValues);
            AssertOnTimeout($"[Final Pass - Last Test] Timed out waiting for all clients transform values to match the server transform values!\n {m_ErrorValidationLog}");

            yield return WaitForConditionOrTimeOut(() => ValidateAllChildrenParentingStatus(true));
            AssertOnTimeout($"[Final Pass - Last Test] Timed out waiting for all children to be removed from their parent!\n {m_ErrorValidationLog}");
        }

        /// <summary>
        ///  Validates the root parent is despawned and its child is moved to the root (null)
        /// </summary>
        private bool ValidateRootParentDespawnedAndChildAtRoot()
        {
            m_ErrorValidationLog.Clear();

            var childOfRoot_ServerSide = InSceneParentChildHandler.AuthorityRootParent.GetChild();
            if (InSceneParentChildHandler.AuthorityRootParent.IsSpawned)
            {
                m_ErrorValidationLog.Append("Server-Side root parent is still spawned!");
                GenerateParentIsNotCorrect(childOfRoot_ServerSide, false, InSceneParentChildHandler.AuthorityRootParent.IsSpawned);
                return false;
            }

            if (childOfRoot_ServerSide.transform.parent != null)
            {
                m_ErrorValidationLog.Append("Server-Side root parent is not null!");
                return false;
            }

            foreach (var clientInstances in InSceneParentChildHandler.ClientRelativeInstances)
            {
                foreach (var instance in clientInstances.Value)
                {
                    if (instance.Value.IsRootParent)
                    {
                        var childHandler = instance.Value.GetChild();

                        if (instance.Value.IsSpawned)
                        {
                            m_ErrorValidationLog.Append("Client-Side is still spawned!");
                            return false;
                        }
                        if (childHandler != null && childHandler.transform.parent != null)
                        {
                            m_ErrorValidationLog.Append("Client-Side still has parent!");
                            return false;
                        }
                    }
                }
            }
            return true;
        }

        [UnityTest]
        public IEnumerator DespawnParentTest([Values] ParentingSpace parentingSpace)
        {
            InSceneParentChildHandler.WorldPositionStays = parentingSpace == ParentingSpace.WorldPositionStays;
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.LoadScene(k_BaseSceneToLoad, LoadSceneMode.Additive);
            m_InitialClientsLoadedScene = false;
            m_ServerNetworkManager.SceneManager.OnSceneEvent += SceneManager_OnSceneEvent;
            m_ServerNetworkManager.SceneManager.ClientSynchronizationMode = LoadSceneMode.Additive;
            var sceneEventStartedStatus = m_ServerNetworkManager.SceneManager.LoadScene(k_TestSceneToLoad, LoadSceneMode.Additive);
            Assert.True(sceneEventStartedStatus == SceneEventProgressStatus.Started, $"Failed to load scene {k_TestSceneToLoad} with a return status of {sceneEventStartedStatus}.");
            yield return WaitForConditionOrTimeOut(() => m_InitialClientsLoadedScene);
            AssertOnTimeout($"Timed out waiting for all clients to load scene {k_TestSceneToLoad}!");

            // [Currently Connected Clients]
            // remove the parents, change all transform values, and re-parent
            InSceneParentChildHandler.AuthorityRootParent.DeparentSetValuesAndReparent();
            yield return WaitForConditionOrTimeOut(ValidateClientsAgainstAuthorityTransformValues);
            AssertOnTimeout($"Timed out waiting for all clients transform values to match the server transform values!\n {m_ErrorValidationLog}");

            // Now despawn the root parent
            InSceneParentChildHandler.AuthorityRootParent.NetworkObject.Despawn(false);

            // Verify all clients despawned the parent object and the child of the parent has root as its parent
            yield return WaitForConditionOrTimeOut(ValidateRootParentDespawnedAndChildAtRoot);
            AssertOnTimeout($"{m_ErrorValidationLog}");
        }

        private void SceneManager_OnSceneEvent(SceneEvent sceneEvent)
        {
            if (sceneEvent.SceneName != k_TestSceneToLoad)
            {
                return;
            }

            if (sceneEvent.ClientId == m_ServerNetworkManager.LocalClientId && sceneEvent.SceneEventType == SceneEventType.LoadEventCompleted)
            {
                m_InitialClientsLoadedScene = true;
            }
        }

        /// <summary>
        /// This verifies in-scene placed NetworkObject's nested under a GameObject without a NetworkObject
        /// component will preserve that parent hierarchy.
        /// </summary>
        [UnityTest]
        public IEnumerator InSceneNestedUnderGameObjectTest()
        {
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.LoadScene(k_BaseSceneToLoad, LoadSceneMode.Additive);
            m_InitialClientsLoadedScene = false;
            m_ServerNetworkManager.SceneManager.OnSceneEvent += SceneManager_OnSceneEvent;

            var sceneEventStartedStatus = m_ServerNetworkManager.SceneManager.LoadScene(k_TestSceneToLoad, LoadSceneMode.Additive);
            Assert.True(sceneEventStartedStatus == SceneEventProgressStatus.Started, $"Failed to load scene {k_TestSceneToLoad} with a return status of {sceneEventStartedStatus}.");
            yield return WaitForConditionOrTimeOut(() => m_InitialClientsLoadedScene);
            AssertOnTimeout($"Timed out waiting for all clients to load scene {k_TestSceneToLoad}!");

            foreach (var instance in InSceneParentedUnderGameObjectHandler.Instances)
            {
                Assert.False(instance.transform.parent == null, $"{instance.name}'s parent is null when it should not be!");
            }
        }


        /// <summary>
        /// Validates that all client transforms match all server transforms for the InSceneNestedAutoSyncObjectTest
        /// </summary>
        private bool AllClientInstancesMatchServerInstance()
        {
            m_ErrorValidationLog.Clear();
            if (ParentingAutoSyncManager.ServerInstance == null)
            {
                m_ErrorValidationLog.AppendLine("ServerInstance is null");
                return false;
            }
            for (int i = 0; i < ParentingAutoSyncManager.ServerInstance.NetworkObjectAutoSyncOnTransforms.Count; i++)
            {
                var serverTransformToTest = ParentingAutoSyncManager.ServerInstance.NetworkObjectAutoSyncOnTransforms[i];
                for (int j = 0; j < m_ClientNetworkManagers.Length; j++)
                {
                    var clientRelativeAutoSyncManager = ParentingAutoSyncManager.ClientInstances[m_ClientNetworkManagers[j].LocalClientId];
                    var clientTransformToTest = clientRelativeAutoSyncManager.NetworkObjectAutoSyncOnTransforms[i];
                    if (!Approximately(clientTransformToTest.position, serverTransformToTest.position))
                    {
                        m_ErrorValidationLog.AppendLine($"[Client-{clientRelativeAutoSyncManager.NetworkManager.LocalClientId}][AutoSync On] {nameof(NetworkObject)}-{clientRelativeAutoSyncManager.NetworkObjectId}'s position {clientRelativeAutoSyncManager.transform.position} does not equal the server-side position {serverTransformToTest.transform.position}");
                        return false;
                    }

                    if (!Approximately(clientTransformToTest.rotation, serverTransformToTest.rotation))
                    {
                        m_ErrorValidationLog.AppendLine($"[Client-{clientRelativeAutoSyncManager.NetworkManager.LocalClientId}][AutoSync On] {nameof(NetworkObject)}-{clientRelativeAutoSyncManager.NetworkObjectId}'s rotation {clientRelativeAutoSyncManager.transform.eulerAngles} does not equal the server-side position {serverTransformToTest.transform.eulerAngles}");
                        return false;
                    }

                    if (!Approximately(clientTransformToTest.localScale, serverTransformToTest.localScale))
                    {
                        m_ErrorValidationLog.AppendLine($"[Client-{clientRelativeAutoSyncManager.NetworkManager.LocalClientId}][AutoSync On] {nameof(NetworkObject)}-{clientRelativeAutoSyncManager.NetworkObjectId}'s scale {clientRelativeAutoSyncManager.transform.localScale} does not equal the server-side position {serverTransformToTest.transform.localScale}");
                        return false;
                    }
                }
            }

            for (int i = 0; i < ParentingAutoSyncManager.ServerInstance.NetworkObjectAutoSyncOffTransforms.Count; i++)
            {
                var serverTransformToTest = ParentingAutoSyncManager.ServerInstance.NetworkObjectAutoSyncOffTransforms[i];
                for (int j = 0; j < m_ClientNetworkManagers.Length; j++)
                {
                    var clientRelativeAutoSyncManager = ParentingAutoSyncManager.ClientInstances[m_ClientNetworkManagers[j].LocalClientId];
                    var clientTransformToTest = clientRelativeAutoSyncManager.NetworkObjectAutoSyncOffTransforms[i];
                    if (!Approximately(clientTransformToTest.position, serverTransformToTest.position))
                    {
                        m_ErrorValidationLog.AppendLine($"[Client-{clientRelativeAutoSyncManager.NetworkManager.LocalClientId}][AutoSync Off] {nameof(NetworkObject)}-{clientRelativeAutoSyncManager.NetworkObjectId}'s position {clientRelativeAutoSyncManager.transform.position} does not equal the server-side position {serverTransformToTest.transform.position}");
                        return false;
                    }

                    if (!Approximately(clientTransformToTest.rotation, serverTransformToTest.rotation))
                    {
                        m_ErrorValidationLog.AppendLine($"[Client-{clientRelativeAutoSyncManager.NetworkManager.LocalClientId}][AutoSync Off] {nameof(NetworkObject)}-{clientRelativeAutoSyncManager.NetworkObjectId}'s rotation {clientRelativeAutoSyncManager.transform.eulerAngles} does not equal the server-side position {serverTransformToTest.transform.eulerAngles}");
                        return false;
                    }

                    if (!Approximately(clientTransformToTest.localScale, serverTransformToTest.localScale))
                    {
                        m_ErrorValidationLog.AppendLine($"[Client-{clientRelativeAutoSyncManager.NetworkManager.LocalClientId}][AutoSync Off] {nameof(NetworkObject)}-{clientRelativeAutoSyncManager.NetworkObjectId}'s scale {clientRelativeAutoSyncManager.transform.localScale} does not equal the server-side position {serverTransformToTest.transform.localScale}");
                        return false;
                    }
                }
            }

            for (int i = 0; i < ParentingAutoSyncManager.ServerInstance.GameObjectAutoSyncOnTransforms.Count; i++)
            {
                var serverTransformToTest = ParentingAutoSyncManager.ServerInstance.GameObjectAutoSyncOnTransforms[i];
                for (int j = 0; j < m_ClientNetworkManagers.Length; j++)
                {
                    var clientRelativeAutoSyncManager = ParentingAutoSyncManager.ClientInstances[m_ClientNetworkManagers[j].LocalClientId];
                    var clientTransformToTest = clientRelativeAutoSyncManager.GameObjectAutoSyncOnTransforms[i];
                    if (!Approximately(clientTransformToTest.position, serverTransformToTest.position))
                    {
                        m_ErrorValidationLog.AppendLine($"[Client-{clientRelativeAutoSyncManager.NetworkManager.LocalClientId}][GO-AutoSync On] {nameof(NetworkObject)}-{clientRelativeAutoSyncManager.NetworkObjectId}'s position {clientRelativeAutoSyncManager.transform.position} does not equal the server-side position {serverTransformToTest.transform.position}");
                        return false;
                    }

                    if (!Approximately(clientTransformToTest.rotation, serverTransformToTest.rotation))
                    {
                        m_ErrorValidationLog.AppendLine($"[Client-{clientRelativeAutoSyncManager.NetworkManager.LocalClientId}][GO-AutoSync On] {nameof(NetworkObject)}-{clientRelativeAutoSyncManager.NetworkObjectId}'s rotation {clientRelativeAutoSyncManager.transform.eulerAngles} does not equal the server-side position {serverTransformToTest.transform.eulerAngles}");
                        return false;
                    }

                    if (!Approximately(clientTransformToTest.localScale, serverTransformToTest.localScale))
                    {
                        m_ErrorValidationLog.AppendLine($"[Client-{clientRelativeAutoSyncManager.NetworkManager.LocalClientId}][GO-AutoSync On] {nameof(NetworkObject)}-{clientRelativeAutoSyncManager.NetworkObjectId}'s scale {clientRelativeAutoSyncManager.transform.localScale} does not equal the server-side position {serverTransformToTest.transform.localScale}");
                        return false;
                    }
                }
            }

            for (int i = 0; i < ParentingAutoSyncManager.ServerInstance.GameObjectAutoSyncOffTransforms.Count; i++)
            {
                var serverTransformToTest = ParentingAutoSyncManager.ServerInstance.GameObjectAutoSyncOffTransforms[i];
                for (int j = 0; j < m_ClientNetworkManagers.Length; j++)
                {
                    var clientRelativeAutoSyncManager = ParentingAutoSyncManager.ClientInstances[m_ClientNetworkManagers[j].LocalClientId];
                    var clientTransformToTest = clientRelativeAutoSyncManager.GameObjectAutoSyncOffTransforms[i];
                    if (!Approximately(clientTransformToTest.position, serverTransformToTest.position))
                    {
                        m_ErrorValidationLog.AppendLine($"[Client-{clientRelativeAutoSyncManager.NetworkManager.LocalClientId}][GO-AutoSync Off] {nameof(NetworkObject)}-{clientRelativeAutoSyncManager.NetworkObjectId}'s position {clientRelativeAutoSyncManager.transform.position} does not equal the server-side position {serverTransformToTest.transform.position}");
                        return false;
                    }

                    if (!Approximately(clientTransformToTest.rotation, serverTransformToTest.rotation))
                    {
                        m_ErrorValidationLog.AppendLine($"[Client-{clientRelativeAutoSyncManager.NetworkManager.LocalClientId}][GO-AutoSync Off] {nameof(NetworkObject)}-{clientRelativeAutoSyncManager.NetworkObjectId}'s rotation {clientRelativeAutoSyncManager.transform.eulerAngles} does not equal the server-side position {serverTransformToTest.transform.eulerAngles}");
                        return false;
                    }

                    if (!Approximately(clientTransformToTest.localScale, serverTransformToTest.localScale))
                    {
                        m_ErrorValidationLog.AppendLine($"[Client-{clientRelativeAutoSyncManager.NetworkManager.LocalClientId}][GO-AutoSync Off] {nameof(NetworkObject)}-{clientRelativeAutoSyncManager.NetworkObjectId}'s scale {clientRelativeAutoSyncManager.transform.localScale} does not equal the server-side position {serverTransformToTest.transform.localScale}");
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Validates that both nested in-scene NetworkObjects and in-scene NetworkObjects
        /// nested under GameObjects synchronize clients with the appropriate transform
        /// space values (world vs local).
        /// </summary>
        [UnityTest]
        public IEnumerator InSceneNestedAutoSyncObjectTest()
        {
            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.LoadScene(k_BaseSceneToLoad, LoadSceneMode.Additive);
            m_InitialClientsLoadedScene = false;
            m_ServerNetworkManager.SceneManager.OnSceneEvent += SceneManager_OnSceneEvent;

            var sceneEventStartedStatus = m_ServerNetworkManager.SceneManager.LoadScene(k_TestSceneToLoad, LoadSceneMode.Additive);
            Assert.True(sceneEventStartedStatus == SceneEventProgressStatus.Started, $"Failed to load scene {k_TestSceneToLoad} with a return status of {sceneEventStartedStatus}.");
            yield return WaitForConditionOrTimeOut(() => m_InitialClientsLoadedScene);
            AssertOnTimeout($"Timed out waiting for all clients to load scene {k_TestSceneToLoad}!");

            yield return WaitForConditionOrTimeOut(AllClientInstancesMatchServerInstance);
            AssertOnTimeout($"Timed out waiting for all client transforms to match the server-side values in test scene {k_TestSceneToLoad}!\n {m_ErrorValidationLog.ToString()}");
        }
    }
}
