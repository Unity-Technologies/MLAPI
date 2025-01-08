using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Unity.Netcode;
using Unity.Netcode.TestHelpers.Runtime;
using UnityEngine;
using UnityEngine.TestTools;


namespace TestProject.RuntimeTests
{
    /// <summary>
    /// Tests Various Features of The NetworkAnimator
    /// !! NOTE !!
    /// This test depends upon the following assets:
    /// Assets\Tests\Animation\Resources\AnimatorObject.prefab
    /// Assets\Tests\Manual\NetworkAnimatorTests\CubeAnimatorController.controller (referenced in AnimatorObject)
    /// Possibly we could build this at runtime, but for now it uses the same animator controller as the manual
    /// test does.
    /// </summary>
    [TestFixture(HostOrServer.Host)]
    [TestFixture(HostOrServer.Server)]
    public class NetworkAnimatorTests : NetcodeIntegrationTest
    {
        private const string k_AnimatorObjectName = "AnimatorObject";
        private const string k_OwnerAnimatorObjectName = "OwnerAnimatorObject";
        private const string k_OwnerPlayerCheerName = "PlayerCheerOwnerAuth";
        private const string k_PlayerCheerName = "PlayerCheerServerAuth";

        protected override bool m_EnableTimeTravel => true;
        protected override bool m_SetupIsACoroutine => false;
        protected override bool m_TearDownIsACoroutine => false;

        protected override int NumberOfClients => 3;
        private GameObject m_AnimationTestPrefab => m_AnimatorObjectPrefab ? m_AnimatorObjectPrefab as GameObject : null;
        private GameObject m_AnimationOwnerTestPrefab => m_OwnerAnimatorObjectPrefab ? m_OwnerAnimatorObjectPrefab as GameObject : null;

        private GameObject m_AnimationCheerTestPrefab => m_PlayerCheerPrefab ? m_PlayerCheerPrefab as GameObject : null;
        private GameObject m_AnimationCheerOwnerTestPrefab => m_OwnerPlayerCheerPrefab ? m_OwnerPlayerCheerPrefab as GameObject : null;

        private AnimatorTestHelper.ParameterValues m_ParameterValues;
        private Object m_AnimatorObjectPrefab;
        private Object m_OwnerAnimatorObjectPrefab;

        private Object m_PlayerCheerPrefab;
        private Object m_OwnerPlayerCheerPrefab;

        public NetworkAnimatorTests(HostOrServer hostOrServer)
        {
            m_UseHost = hostOrServer == HostOrServer.Host;
        }

        protected override void OnOneTimeSetup()
        {
            m_AnimatorObjectPrefab = Resources.Load(k_AnimatorObjectName);
            Assert.NotNull(m_AnimatorObjectPrefab, $"Failed to load resource {k_AnimatorObjectName}!");

            m_OwnerAnimatorObjectPrefab = Resources.Load(k_OwnerAnimatorObjectName);
            Assert.NotNull(m_OwnerAnimatorObjectPrefab, $"Failed to load resource {k_OwnerAnimatorObjectName}!");

            m_OwnerPlayerCheerPrefab = Resources.Load(k_OwnerPlayerCheerName);
            Assert.NotNull(m_OwnerPlayerCheerPrefab, $"Failed to load resource {k_OwnerPlayerCheerName}!");

            m_PlayerCheerPrefab = Resources.Load(k_PlayerCheerName);
            Assert.NotNull(m_PlayerCheerPrefab, $"Failed to load resource {k_PlayerCheerName}!");

            base.OnOneTimeSetup();
        }

        protected override void OnInlineSetup()
        {
            AnimatorTestHelper.Initialize();
            CheckStateEnterCount.ResetTest();
            TriggerTest.ResetTest();
            StateSyncTest.ResetTest();
        }

        protected override void OnInlineTearDown()
        {
            m_EnableVerboseDebug = false;
        }

        protected override void OnServerAndClientsCreated()
        {
            // Server authority prefab
            var networkObjectServer = (m_AnimatorObjectPrefab as GameObject).GetComponent<NetworkObject>();
            networkObjectServer.NetworkManagerOwner = m_ServerNetworkManager;
            networkObjectServer.name = "ServerAuth";
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObjectServer);
            var networkAnimatorServerAuthPrefab = new NetworkPrefab() { Prefab = networkObjectServer.gameObject };
            m_ServerNetworkManager.NetworkConfig.Prefabs.Add(networkAnimatorServerAuthPrefab);

            // Owner authority prefab
            var networkObjectOwner = (m_OwnerAnimatorObjectPrefab as GameObject).GetComponent<NetworkObject>();
            networkObjectOwner.NetworkManagerOwner = m_ServerNetworkManager;
            networkObjectOwner.name = "OwnerAuthority";
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObjectOwner);
            var networkAnimatorOwnerAuthPrefab = new NetworkPrefab() { Prefab = networkObjectOwner.gameObject };
            m_ServerNetworkManager.NetworkConfig.Prefabs.Add(networkAnimatorOwnerAuthPrefab);

            // Server authority player cheer prefab
            var networkObjectPlayerCheerServer = (m_PlayerCheerPrefab as GameObject).GetComponent<NetworkObject>();
            networkObjectPlayerCheerServer.NetworkManagerOwner = m_ServerNetworkManager;
            networkObjectPlayerCheerServer.name = "PCServerAuth";
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObjectPlayerCheerServer);
            var networkAnimatorPlayerCheerServerAuthPrefab = new NetworkPrefab() { Prefab = networkObjectPlayerCheerServer.gameObject };
            m_ServerNetworkManager.NetworkConfig.Prefabs.Add(networkAnimatorPlayerCheerServerAuthPrefab);

            var networkObjectPlayerCheerOwner = (m_OwnerPlayerCheerPrefab as GameObject).GetComponent<NetworkObject>();
            networkObjectPlayerCheerOwner.NetworkManagerOwner = m_ServerNetworkManager;
            networkObjectPlayerCheerOwner.name = "PCOwnerAuth";
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObjectPlayerCheerOwner);
            var networkAnimatorPlayerCheerOwnerAuthPrefab = new NetworkPrefab() { Prefab = networkObjectPlayerCheerOwner.gameObject };
            m_ServerNetworkManager.NetworkConfig.Prefabs.Add(networkAnimatorPlayerCheerOwnerAuthPrefab);

            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                clientNetworkManager.NetworkConfig.Prefabs.Add(networkAnimatorServerAuthPrefab);
                clientNetworkManager.NetworkConfig.Prefabs.Add(networkAnimatorOwnerAuthPrefab);
                clientNetworkManager.NetworkConfig.Prefabs.Add(networkAnimatorPlayerCheerServerAuthPrefab);
                clientNetworkManager.NetworkConfig.Prefabs.Add(networkAnimatorPlayerCheerOwnerAuthPrefab);
            }

            base.OnServerAndClientsCreated();
        }

        private bool ParameterValuesMatch(OwnerShipMode ownerShipMode, AuthoritativeMode authoritativeMode, bool debugInfo = false)
        {
            var serverParameters = AnimatorTestHelper.ServerSideInstance.GetParameterValues();
            if (!serverParameters.ValuesMatch(m_ParameterValues, debugInfo))
            {
                return false;
            }
            foreach (var animatorTestHelper in AnimatorTestHelper.ClientSideInstances)
            {
                var clientParameters = animatorTestHelper.Value.GetParameterValues();
                if (!clientParameters.ValuesMatch(m_ParameterValues, debugInfo))
                {
                    return false;
                }
            }

            return true;
        }

        public enum OwnerShipMode
        {
            ServerOwner,
            ClientOwner
        }

        public enum AuthoritativeMode
        {
            ServerAuth,
            OwnerAuth,
            PCServerAuth,
            PCOwnerAuth
        }

        private GameObject SpawnPrefab(bool isClientOwner, AuthoritativeMode authoritativeMode)
        {
            var networkManager = isClientOwner ? m_ClientNetworkManagers[0] : m_ServerNetworkManager;
            var gameObject = (GameObject)null;
            switch (authoritativeMode)
            {
                case AuthoritativeMode.ServerAuth:
                    {
                        Assert.NotNull(m_AnimatorObjectPrefab);
                        gameObject = SpawnObject(m_AnimatorObjectPrefab as GameObject, networkManager);
                        break;
                    }
                case AuthoritativeMode.OwnerAuth:
                    {
                        Assert.NotNull(m_OwnerAnimatorObjectPrefab);
                        gameObject = SpawnObject(m_OwnerAnimatorObjectPrefab as GameObject, networkManager);
                        break;
                    }
                case AuthoritativeMode.PCServerAuth:
                    {
                        Assert.NotNull(m_PlayerCheerPrefab);
                        gameObject = SpawnObject(m_PlayerCheerPrefab as GameObject, networkManager);
                        break;
                    }
                case AuthoritativeMode.PCOwnerAuth:
                    {
                        Assert.NotNull(m_OwnerPlayerCheerPrefab);
                        gameObject = SpawnObject(m_OwnerPlayerCheerPrefab as GameObject, networkManager);
                        break;
                    }
            }
            return gameObject;
        }

        private string GetNetworkAnimatorName(AuthoritativeMode authoritativeMode)
        {
            var name = string.Empty;
            switch (authoritativeMode)
            {
                case AuthoritativeMode.ServerAuth:
                    {
                        name = m_AnimationTestPrefab.name;
                        break;
                    }
                case AuthoritativeMode.OwnerAuth:
                    {
                        name = m_AnimationOwnerTestPrefab.name;
                        break;
                    }
                case AuthoritativeMode.PCServerAuth:
                    {
                        name = m_PlayerCheerPrefab.name;
                        break;
                    }
                case AuthoritativeMode.PCOwnerAuth:
                    {
                        name = m_OwnerPlayerCheerPrefab.name;
                        break;
                    }
            }
            return name;
        }

        /// <summary>
        /// Verifies that parameters are synchronized with currently connected clients
        /// when no transition or layer change has occurred.
        /// </summary>
        /// <param name="authoritativeMode">Server or Owner authoritative</param>
        [Test]
        public void ParameterUpdateTests([Values] OwnerShipMode ownerShipMode, [Values(AuthoritativeMode.ServerAuth, AuthoritativeMode.OwnerAuth)] AuthoritativeMode authoritativeMode)
        {
            VerboseDebug($" ++++++++++++++++++ Parameter Test [{ownerShipMode}] Starting ++++++++++++++++++ ");

            // Spawn our test animator object
            var objectInstance = SpawnPrefab(ownerShipMode == OwnerShipMode.ClientOwner, authoritativeMode);

            // Wait for it to spawn server-side
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => AnimatorTestHelper.ServerSideInstance != null);
            Assert.True(success, $"Timed out waiting for the server-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");

            // Wait for it to spawn client-side
            success = WaitForConditionOrTimeOutWithTimeTravel(() => AnimatorTestHelper.ClientSideInstances.ContainsKey(m_ClientNetworkManagers[0].LocalClientId));
            Assert.True(success, $"Timed out waiting for the client-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");

            // Create new parameter values
            m_ParameterValues = new AnimatorTestHelper.ParameterValues() { FloatValue = 1.0f, IntValue = 5, BoolValue = true };

            if (authoritativeMode == AuthoritativeMode.OwnerAuth)
            {
                var objectToUpdate = ownerShipMode == OwnerShipMode.ClientOwner ? AnimatorTestHelper.ClientSideInstances[m_ClientNetworkManagers[0].LocalClientId] : AnimatorTestHelper.ServerSideInstance;
                // Set the new parameter values via the owner
                objectToUpdate.UpdateParameters(m_ParameterValues);
            }
            else
            {
                // Set the new parameter values via the server
                AnimatorTestHelper.ServerSideInstance.UpdateParameters(m_ParameterValues);
            }

            // Wait for the client side to update to the new parameter values
            success = WaitForConditionOrTimeOutWithTimeTravel(() => ParameterValuesMatch(ownerShipMode, authoritativeMode, m_EnableVerboseDebug));
            Assert.True(success, $"Timed out waiting for the client-side parameters to match {m_ParameterValues}!");
            VerboseDebug($" ------------------ Parameter Test [{ownerShipMode}] Stopping ------------------ ");
        }


        private bool AllTriggersDetected(OwnerShipMode ownerShipMode)
        {
            if (ownerShipMode == OwnerShipMode.ClientOwner)
            {
                if (!TriggerTest.ClientsThatTriggered.Contains(m_ServerNetworkManager.LocalClientId))
                {
                    return false;
                }
            }

            foreach (var animatorTestHelper in AnimatorTestHelper.ClientSideInstances)
            {
                if (ownerShipMode == OwnerShipMode.ClientOwner && animatorTestHelper.Value.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId)
                {
                    continue;
                }
                if (!TriggerTest.ClientsThatTriggered.Contains(animatorTestHelper.Value.NetworkManager.LocalClientId))
                {
                    return false;
                }
            }
            return true;
        }

        private bool AllInstancesSameLayerWeight(OwnerShipMode ownerShipMode, int layer, float targetWeight)
        {

            if (ownerShipMode == OwnerShipMode.ClientOwner)
            {
                if (AnimatorTestHelper.ServerSideInstance.GetLayerWeight(layer) != targetWeight)
                {
                    return false;
                }
            }

            foreach (var animatorTestHelper in AnimatorTestHelper.ClientSideInstances)
            {
                if (ownerShipMode == OwnerShipMode.ClientOwner && animatorTestHelper.Value.OwnerClientId == m_ClientNetworkManagers[0].LocalClientId)
                {
                    continue;
                }
                if (animatorTestHelper.Value.GetLayerWeight(layer) != targetWeight)
                {
                    return false;
                }
            }
            return true;
        }

        private bool WaitForClientsToInitialize()
        {
            foreach (var networkManager in m_ClientNetworkManagers)
            {
                var clientId = networkManager.LocalClientId;
                if (!AnimatorTestHelper.ClientSideInstances.ContainsKey(clientId))
                {
                    return false;
                }
                if (!AnimatorTestHelper.ClientSideInstances[clientId].GetComponent<Animator>().isInitialized)
                {
                    return false;
                }
                VerboseDebug($"{networkManager.name} initialized and spawned {AnimatorTestHelper.ClientSideInstances[clientId]}.");
            }
            return true;
        }

        private bool AllClientsTransitioningAnyState()
        {
            foreach (var networkManager in m_ClientNetworkManagers)
            {
                var clientId = networkManager.LocalClientId;
                if (!AnimatorTestHelper.ClientSideInstances.ContainsKey(clientId))
                {
                    return false;
                }
                var animator = AnimatorTestHelper.ClientSideInstances[clientId].GetComponent<Animator>();
                if (!animator.isInitialized)
                {
                    return false;
                }
                var transitionInfo = animator.GetAnimatorTransitionInfo(0);
                if (!transitionInfo.anyState)
                {
                    return false;
                }
                VerboseDebug($"{networkManager.name} transitioning from AnyState or CrossFade.");
            }
            return true;
        }

        /// <summary>
        /// Verifies that cross fading is synchronized with currently connected clients
        /// </summary>
        [UnityTest]
        public IEnumerator CrossFadeTransitionTests([Values] OwnerShipMode ownerShipMode, [Values(AuthoritativeMode.ServerAuth, AuthoritativeMode.OwnerAuth)] AuthoritativeMode authoritativeMode)
        {
            CrossFadeTransitionDetect.ResetTest();
            CrossFadeTransitionDetect.SetTargetAnimationState(AnimatorTestHelper.TargetCrossFadeState);
            VerboseDebug($" ++++++++++++++++++ Cross Fade Transition Test [{ownerShipMode}] Starting ++++++++++++++++++ ");
            CrossFadeTransitionDetect.IsVerboseDebug = m_EnableVerboseDebug;

            // Spawn our test animator object
            var objectInstance = SpawnPrefab(ownerShipMode == OwnerShipMode.ClientOwner, authoritativeMode);

            // Wait for it to spawn server-side
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => AnimatorTestHelper.ServerSideInstance != null);
            Assert.True(success, $"Timed out waiting for the server-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");

            // Wait for it to spawn client-side
            success = WaitForConditionOrTimeOutWithTimeTravel(WaitForClientsToInitialize);
            Assert.True(success, $"Timed out waiting for the client-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");
            var animatorTestHelper = ownerShipMode == OwnerShipMode.ClientOwner ? AnimatorTestHelper.ClientSideInstances[m_ClientNetworkManagers[0].LocalClientId] : AnimatorTestHelper.ServerSideInstance;
            var layerCount = animatorTestHelper.GetAnimator().layerCount;

            var animationStateCount = animatorTestHelper.GetAnimatorStateCount();
            Assert.True(layerCount == animationStateCount, $"AnimationState count {animationStateCount} does not equal the layer count {layerCount}!");

            if (authoritativeMode == AuthoritativeMode.ServerAuth)
            {
                animatorTestHelper = AnimatorTestHelper.ServerSideInstance;
            }

            CrossFadeTransitionDetect.ClientIds.Add(m_ServerNetworkManager.LocalClientId);
            foreach (var client in m_ClientNetworkManagers)
            {
                CrossFadeTransitionDetect.ClientIds.Add(client.LocalClientId);
            }

            animatorTestHelper.TestCrossFade();

            // Verify the host and all clients performed a cross fade transition
            yield return WaitForConditionOrTimeOut(CrossFadeTransitionDetect.AllClientsTransitioned);
            AssertOnTimeout($"Timed out waiting for all clients to transition from synchronized cross fade!");
        }

        private bool AllTriggersDetectedOnObserversOnly(OwnerShipMode ownerShipMode, ulong nonObserverId)
        {
            if (ownerShipMode == OwnerShipMode.ClientOwner)
            {
                if (!TriggerTest.ClientsThatTriggered.Contains(m_ServerNetworkManager.LocalClientId))
                {
                    return false;
                }
            }

            foreach (var animatorTestHelper in AnimatorTestHelper.ClientSideInstances)
            {
                var currentClientId = animatorTestHelper.Value.NetworkManager.LocalClientId;
                if (currentClientId == nonObserverId || (ownerShipMode == OwnerShipMode.ClientOwner && currentClientId == animatorTestHelper.Value.OwnerClientId))
                {
                    continue;
                }

                if (!TriggerTest.ClientsThatTriggered.Contains(currentClientId))
                {
                    return false;
                }
            }

            // Should return false always
            return !TriggerTest.ClientsThatTriggered.Contains(nonObserverId);
        }

        private bool AllObserversSameLayerWeight(OwnerShipMode ownerShipMode, int layer, float targetWeight, ulong nonObserverId)
        {

            if (ownerShipMode == OwnerShipMode.ClientOwner)
            {
                if (AnimatorTestHelper.ServerSideInstance.GetLayerWeight(layer) != targetWeight)
                {
                    return false;
                }
            }

            foreach (var animatorTestHelper in AnimatorTestHelper.ClientSideInstances)
            {
                var currentClientId = animatorTestHelper.Value.NetworkManager.LocalClientId;
                if (ownerShipMode == OwnerShipMode.ClientOwner && animatorTestHelper.Value.OwnerClientId == currentClientId)
                {
                    continue;
                }
                if (currentClientId == nonObserverId)
                {
                    if (animatorTestHelper.Value.GetLayerWeight(layer) == targetWeight)
                    {
                        return false;
                    }
                }
                else
                if (animatorTestHelper.Value.GetLayerWeight(layer) != targetWeight)
                {
                    return false;
                }
            }
            return true;
        }

        [UnityTest]
        public IEnumerator OnlyObserversAnimateTest([Values] OwnerShipMode ownerShipMode, [Values(AuthoritativeMode.ServerAuth, AuthoritativeMode.OwnerAuth)] AuthoritativeMode authoritativeMode)
        {
            // Spawn our test animator object
            var objectInstance = SpawnPrefab(ownerShipMode == OwnerShipMode.ClientOwner, authoritativeMode);
            var networkObject = objectInstance.GetComponent<NetworkObject>();
            // Wait for it to spawn server-side
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => AnimatorTestHelper.ServerSideInstance != null);
            Assert.True(success, $"Timed out waiting for the server-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");

            // Wait for it to spawn client-side
            success = WaitForConditionOrTimeOutWithTimeTravel(WaitForClientsToInitialize);
            Assert.True(success, $"Timed out waiting for the server-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");

            var animatorTestHelper = ownerShipMode == OwnerShipMode.ClientOwner ? AnimatorTestHelper.ClientSideInstances[m_ClientNetworkManagers[0].LocalClientId] : AnimatorTestHelper.ServerSideInstance;

            networkObject.NetworkHide(m_ClientNetworkManagers[1].LocalClientId);

            yield return WaitForConditionOrTimeOut(() => !m_ClientNetworkManagers[1].SpawnManager.SpawnedObjects.ContainsKey(networkObject.NetworkObjectId));
            AssertOnTimeout($"Client-{m_ClientNetworkManagers[1].LocalClientId} timed out waiting to hide {networkObject.name}!");

            if (authoritativeMode == AuthoritativeMode.ServerAuth)
            {
                animatorTestHelper = AnimatorTestHelper.ServerSideInstance;
            }
            animatorTestHelper.SetTrigger();
            // Wait for all triggers to fire
            yield return WaitForConditionOrTimeOut(() => AllTriggersDetectedOnObserversOnly(ownerShipMode, m_ClientNetworkManagers[1].LocalClientId));
            AssertOnTimeout($"Timed out waiting for all triggers to match!");

            animatorTestHelper.SetLayerWeight(1, 0.75f);
            // Wait for all instances to update their weight value for layer 1
            success = WaitForConditionOrTimeOutWithTimeTravel(() => AllObserversSameLayerWeight(ownerShipMode, 1, 0.75f, m_ClientNetworkManagers[1].LocalClientId));
            Assert.True(success, $"Timed out waiting for all instances to match weight 0.75 on layer 1!");
        }

        /// <summary>
        /// Verifies that triggers are synchronized with currently connected clients
        /// </summary>
        /// <param name="authoritativeMode">Server or Owner authoritative</param>
        [UnityTest]
        public IEnumerator TriggerUpdateTests([Values] OwnerShipMode ownerShipMode, [Values(AuthoritativeMode.ServerAuth, AuthoritativeMode.OwnerAuth)] AuthoritativeMode authoritativeMode)
        {
            CheckStateEnterCount.ResetTest();

            VerboseDebug($" ++++++++++++++++++ Trigger Test [{TriggerTest.Iteration}][{ownerShipMode}] Starting ++++++++++++++++++ ");
            TriggerTest.IsVerboseDebug = m_EnableVerboseDebug;
            AnimatorTestHelper.IsTriggerTest = m_EnableVerboseDebug;

            // Spawn our test animator object
            var objectInstance = SpawnPrefab(ownerShipMode == OwnerShipMode.ClientOwner, authoritativeMode);

            // Wait for it to spawn server-side
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => AnimatorTestHelper.ServerSideInstance != null);
            Assert.True(success, $"Timed out waiting for the server-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");

            // Wait for it to spawn client-side
            success = WaitForConditionOrTimeOutWithTimeTravel(WaitForClientsToInitialize);
            Assert.True(success, $"Timed out waiting for the client-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");
            var animatorTestHelper = ownerShipMode == OwnerShipMode.ClientOwner ? AnimatorTestHelper.ClientSideInstances[m_ClientNetworkManagers[0].LocalClientId] : AnimatorTestHelper.ServerSideInstance;
            var layerCount = animatorTestHelper.GetAnimator().layerCount;

            var animationStateCount = animatorTestHelper.GetAnimatorStateCount();
            Assert.True(layerCount == animationStateCount, $"AnimationState count {animationStateCount} does not equal the layer count {layerCount}!");



            if (authoritativeMode == AuthoritativeMode.ServerAuth)
            {
                animatorTestHelper = AnimatorTestHelper.ServerSideInstance;
            }

            if (m_EnableVerboseDebug)
            {
                var retryTrigger = true;
                var timeOutHelper = new TimeoutHelper(1.0f);
                var count = 0;
                while (retryTrigger)
                {
                    VerboseDebug($"Current Trigger State: {animatorTestHelper.GetCurrentTriggerState()}");
                    VerboseDebug($"Setting Trigger");
                    animatorTestHelper.SetTrigger();
                    VerboseDebug($"New Trigger State: {animatorTestHelper.GetCurrentTriggerState()}");
                    // Wait for all triggers to fire
                    yield return WaitForConditionOrTimeOut(() => AllTriggersDetected(ownerShipMode), timeOutHelper);
                    retryTrigger = timeOutHelper.TimedOut;
                    if (retryTrigger)
                    {
                        count++;
                        Debug.LogWarning($"[{ownerShipMode}][{count}] Resending trigger!");
                    }
                }
            }
            else
            {
                animatorTestHelper.SetTrigger();
                // Wait for all triggers to fire
                yield return WaitForConditionOrTimeOut(() => AllTriggersDetected(ownerShipMode));
                AssertOnTimeout($"Timed out waiting for all triggers to match!");
            }

            TimeTravelToNextTick();

            var clientIdList = new List<ulong>();
            foreach (var client in m_ClientNetworkManagers)
            {
                clientIdList.Add(client.LocalClientId);
            }

            // Verify we only entered each state once
            success = WaitForConditionOrTimeOutWithTimeTravel(() => CheckStateEnterCount.AllStatesEnteredMatch(clientIdList));
            Assert.True(success, $"Timed out waiting for all states entered to match!");

            // Now, update some states for several seconds to assure the AnimationState count does not grow
            var waitForSeconds = new WaitForSeconds(0.25f);
            bool rotateToggle = true;
            for (int i = 0; i < 10; i++)
            {
                animatorTestHelper.SetBool("Rotate", rotateToggle);
                animatorTestHelper.SetTrigger("Pulse");
                animationStateCount = animatorTestHelper.GetAnimatorStateCount();
                Assert.True(layerCount == animationStateCount, $"AnimationState count {animationStateCount} does not equal the layer count {layerCount}!");
                yield return waitForSeconds;
                rotateToggle = !rotateToggle;
            }

            CheckStateEnterCount.ResetTest();
            if (m_EnableVerboseDebug)
            {
                var retryTrigger = true;
                var timeOutHelper = new TimeoutHelper(1.0f);
                var count = 0;
                while (retryTrigger)
                {
                    VerboseDebug($"Current Trigger State: {animatorTestHelper.GetCurrentTriggerState()}");
                    VerboseDebug($"Setting Attack Trigger ");
                    var animator = animatorTestHelper.GetAnimator();
                    animator.SetInteger("WeaponType", 1);
                    animatorTestHelper.SetTrigger("Attack");
                    VerboseDebug($"New Trigger State: {animatorTestHelper.GetCurrentTriggerState()}");
                    // Wait for all triggers to fire
                    yield return WaitForConditionOrTimeOut(() => AllTriggersDetected(ownerShipMode), timeOutHelper);
                    retryTrigger = timeOutHelper.TimedOut;
                    if (retryTrigger)
                    {
                        count++;
                        Debug.LogWarning($"[{ownerShipMode}][{count}] Resending trigger!");
                    }
                }
            }
            else
            {
                var animator = animatorTestHelper.GetAnimator();
                animator.SetInteger("WeaponType", 1);
                animatorTestHelper.SetTrigger("Attack");
                // Wait for all triggers to fire
                yield return WaitForConditionOrTimeOut(() => AllTriggersDetected(ownerShipMode));
                AssertOnTimeout($"Timed out waiting for all triggers to match!");
            }

            AnimatorTestHelper.IsTriggerTest = false;
            VerboseDebug($" ------------------ Trigger Test [{TriggerTest.Iteration}][{ownerShipMode}] Stopping ------------------ ");
        }

        protected override void OnNewClientCreated(NetworkManager networkManager)
        {
            var networkPrefab = new NetworkPrefab() { Prefab = m_AnimationTestPrefab };
            networkManager.NetworkConfig.Prefabs.Add(networkPrefab);
            networkPrefab = new NetworkPrefab() { Prefab = m_AnimationOwnerTestPrefab };
            networkManager.NetworkConfig.Prefabs.Add(networkPrefab);

            networkPrefab = new NetworkPrefab() { Prefab = m_AnimationCheerTestPrefab };
            networkManager.NetworkConfig.Prefabs.Add(networkPrefab);
            networkPrefab = new NetworkPrefab() { Prefab = m_AnimationCheerOwnerTestPrefab };
            networkManager.NetworkConfig.Prefabs.Add(networkPrefab);
        }

        /// <summary>
        /// Verifies that triggers are synchronized with currently connected clients
        /// </summary>
        /// <param name="authoritativeMode">Server or Owner authoritative</param>
        [Test]
        public void WeightUpdateTests([Values] OwnerShipMode ownerShipMode, [Values(AuthoritativeMode.ServerAuth, AuthoritativeMode.OwnerAuth)] AuthoritativeMode authoritativeMode)
        {
            CheckStateEnterCount.ResetTest();
            TriggerTest.ResetTest();
            VerboseDebug($" ++++++++++++++++++ Weight Test [{ownerShipMode}] Starting ++++++++++++++++++ ");
            TriggerTest.IsVerboseDebug = m_EnableVerboseDebug;
            AnimatorTestHelper.IsTriggerTest = m_EnableVerboseDebug;

            // Spawn our test animator object
            var objectInstance = SpawnPrefab(ownerShipMode == OwnerShipMode.ClientOwner, authoritativeMode);

            // Wait for it to spawn server-side
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => AnimatorTestHelper.ServerSideInstance != null);
            Assert.True(success, $"Timed out waiting for the server-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");

            // Wait for it to spawn client-side
            success = WaitForConditionOrTimeOutWithTimeTravel(WaitForClientsToInitialize);
            Assert.True(success, $"Timed out waiting for the client-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");
            var animatorTestHelper = ownerShipMode == OwnerShipMode.ClientOwner ? AnimatorTestHelper.ClientSideInstances[m_ClientNetworkManagers[0].LocalClientId] : AnimatorTestHelper.ServerSideInstance;
            var layerCount = animatorTestHelper.GetAnimator().layerCount;

            var animationStateCount = animatorTestHelper.GetAnimatorStateCount();
            Assert.True(layerCount == animationStateCount, $"AnimationState count {animationStateCount} does not equal the layer count {layerCount}!");

            if (authoritativeMode == AuthoritativeMode.ServerAuth)
            {
                animatorTestHelper = AnimatorTestHelper.ServerSideInstance;
            }

            var originalWeight = animatorTestHelper.GetLayerWeight(1);

            animatorTestHelper.SetLayerWeight(1, 0.75f);
            // Wait for all instances to update their weight value for layer 1
            success = WaitForConditionOrTimeOutWithTimeTravel(() => AllInstancesSameLayerWeight(ownerShipMode, 1, 0.75f));
            Assert.True(success, $"Timed out waiting for all instances to match weight 0.75 on layer 1!");

            animatorTestHelper.SetLayerWeight(1, originalWeight);
            // Wait for all instances to update their weight value for layer 1
            success = WaitForConditionOrTimeOutWithTimeTravel(() => AllInstancesSameLayerWeight(ownerShipMode, 1, originalWeight));
            Assert.True(success, $"Timed out waiting for all instances to match weight {originalWeight} on layer 1!");

            // Now set the layer weight to 0
            animatorTestHelper.SetLayerWeight(1, 0.0f);

            // Now late join a client
            CreateAndStartNewClientWithTimeTravel();

            // Verify the late joined client is synchronized to the changed weight
            success = WaitForConditionOrTimeOutWithTimeTravel(() => AllInstancesSameLayerWeight(ownerShipMode, 1, 0.0f));
            Assert.True(success, $"[Late-Join] Timed out waiting for all instances to match weight 0 on layer 1!");

            animatorTestHelper.SetLayerWeight(1, originalWeight);
            // Wait for all instances to update their weight value for layer 1
            success = WaitForConditionOrTimeOutWithTimeTravel(() => AllInstancesSameLayerWeight(ownerShipMode, 1, originalWeight));
            Assert.True(success, $"Timed out waiting for all instances to match weight {originalWeight} on layer 1!");

            AnimatorTestHelper.IsTriggerTest = false;
            VerboseDebug($" ------------------ Weight Test [{ownerShipMode}] Stopping ------------------ ");
        }

        /// <summary>
        /// Verifies that late joining clients are synchronized to an
        /// animator's trigger state.
        /// </summary>
        /// <param name="authoritativeMode">Server or Owner authoritative</param>
        [UnityTest]
        public IEnumerator LateJoinTriggerSynchronizationTest([Values] OwnerShipMode ownerShipMode, [Values] AuthoritativeMode authoritativeMode)
        {
            VerboseDebug($" ++++++++++++++++++ Late Join Trigger Test [{TriggerTest.Iteration}][{ownerShipMode}] Starting ++++++++++++++++++ ");
            TriggerTest.IsVerboseDebug = m_EnableVerboseDebug;
            CheckStateEnterCount.IsVerboseDebug = m_EnableVerboseDebug;
            AnimatorTestHelper.IsTriggerTest = m_EnableVerboseDebug;
            bool isClientOwner = ownerShipMode == OwnerShipMode.ClientOwner;

            // Spawn our test animator object
            var objectInstance = SpawnPrefab(ownerShipMode == OwnerShipMode.ClientOwner, authoritativeMode);

            // Wait for it to spawn server-side
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => AnimatorTestHelper.ServerSideInstance != null);
            Assert.True(success, $"Timed out waiting for the server-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");

            // Wait for it to spawn client-side
            success = WaitForConditionOrTimeOutWithTimeTravel(WaitForClientsToInitialize);
            Assert.True(success, $"Timed out waiting for the client-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");

            var triggerName = authoritativeMode == AuthoritativeMode.OwnerAuth || authoritativeMode == AuthoritativeMode.ServerAuth ? "TestTrigger" : "Cheer";
            // Set the trigger based on the type of test
            if (authoritativeMode == AuthoritativeMode.OwnerAuth)
            {
                var objectToUpdate = ownerShipMode == OwnerShipMode.ClientOwner ? AnimatorTestHelper.ClientSideInstances[m_ClientNetworkManagers[0].LocalClientId] : AnimatorTestHelper.ServerSideInstance;
                // Set the animation trigger via the owner
                objectToUpdate.SetTrigger(triggerName);
            }
            else
            {
                // Set the animation trigger via the server
                AnimatorTestHelper.ServerSideInstance.SetTrigger(triggerName, m_EnableVerboseDebug);
            }

            // Wait for all triggers to fire
            yield return WaitForConditionOrTimeOut(() => AllTriggersDetected(ownerShipMode));
            AssertOnTimeout($"Timed out waiting for all triggers to match!");

            if (authoritativeMode == AuthoritativeMode.OwnerAuth || authoritativeMode == AuthoritativeMode.ServerAuth)
            {
                // Create new parameter values
                m_ParameterValues = new AnimatorTestHelper.ParameterValues() { FloatValue = 1.0f, IntValue = 5, BoolValue = true };

                if (authoritativeMode == AuthoritativeMode.OwnerAuth)
                {
                    var objectToUpdate = ownerShipMode == OwnerShipMode.ClientOwner ? AnimatorTestHelper.ClientSideInstances[m_ClientNetworkManagers[0].LocalClientId] : AnimatorTestHelper.ServerSideInstance;
                    // Set the new parameter values
                    objectToUpdate.UpdateParameters(m_ParameterValues);
                }
                else
                {
                    // Set the new parameter values
                    AnimatorTestHelper.ServerSideInstance.UpdateParameters(m_ParameterValues);
                }

                // Wait for the client side to update to the new parameter values
                success = WaitForConditionOrTimeOutWithTimeTravel(() => ParameterValuesMatch(ownerShipMode, authoritativeMode, m_EnableVerboseDebug));
                Assert.True(success, $"Timed out waiting for the client-side parameters to match {m_ParameterValues.ValuesToString()}!");
            }

            CreateAndStartNewClientWithTimeTravel();

            Assert.IsTrue(m_ClientNetworkManagers.Length == NumberOfClients + 1, $"Newly created and connected client was not added to {nameof(m_ClientNetworkManagers)}!");

            // Wait for it to spawn client-side
            success = WaitForConditionOrTimeOutWithTimeTravel(WaitForClientsToInitialize);
            Assert.True(success, $"Timed out waiting for the late joining client-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");

            // Make sure the AnimatorTestHelper client side instances is the same as the TotalClients
            var calculatedClients = (AnimatorTestHelper.ClientSideInstances.Count + (m_UseHost ? 1 : 0));
            Assert.True(calculatedClients == TotalClients, $"Number of client");

            // Now check that the late joining client and all other clients are synchronized to the trigger
            yield return WaitForConditionOrTimeOut(() => AllTriggersDetected(ownerShipMode));

            var message = string.Empty;
            if (s_GlobalTimeoutHelper.TimedOut)
            {
                message = "\n Clients that triggered:";
                foreach (var id in TriggerTest.ClientsThatTriggered)
                {
                    message += $" ({id})";
                }
            }
            AssertOnTimeout($"Timed out waiting for the late joining client's triggers to match!{message}", s_GlobalTimeoutHelper);
            // Now check that the late joining client and all other clients are synchronized to the updated parameter values
            if (authoritativeMode == AuthoritativeMode.OwnerAuth || authoritativeMode == AuthoritativeMode.ServerAuth)
            {
                success = WaitForConditionOrTimeOutWithTimeTravel(() => ParameterValuesMatch(ownerShipMode, authoritativeMode, m_EnableVerboseDebug));
                Assert.True(success, $"Timed out waiting for the client-side parameters to match {m_ParameterValues.ValuesToString()}!");
            }
            var newlyJoinedClient = m_ClientNetworkManagers[NumberOfClients];
            StopOneClientWithTimeTravel(newlyJoinedClient);
            VerboseDebug($" ------------------ Late Join Trigger Test [{TriggerTest.Iteration}][{ownerShipMode}] Stopping ------------------ ");
        }

        /// <summary>
        /// Verifies that late joining clients are synchronized to all of the
        /// states of an animator.
        /// </summary>
        /// <param name="authoritativeMode">Server or Owner authoritative</param>
        [UnityTest]
        public IEnumerator LateJoinSynchronizationTest([Values] OwnerShipMode ownerShipMode, [Values(AuthoritativeMode.ServerAuth, AuthoritativeMode.OwnerAuth)] AuthoritativeMode authoritativeMode)
        {
            VerboseDebug($" ++++++++++++++++++ Late Join Synchronization Test [{TriggerTest.Iteration}][{ownerShipMode}] Starting ++++++++++++++++++ ");

            StateSyncTest.IsVerboseDebug = m_EnableVerboseDebug;
            TriggerTest.IsVerboseDebug = m_EnableVerboseDebug;
            AnimatorTestHelper.IsTriggerTest = m_EnableVerboseDebug;
            bool isClientOwner = ownerShipMode == OwnerShipMode.ClientOwner;

            // Spawn our test animator object
            var objectInstance = SpawnPrefab(ownerShipMode == OwnerShipMode.ClientOwner, authoritativeMode);

            // Wait for it to spawn server-side
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => AnimatorTestHelper.ServerSideInstance != null);
            Assert.True(success, $"Timed out waiting for the server-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");

            // Wait for it to spawn client-side
            success = WaitForConditionOrTimeOutWithTimeTravel(WaitForClientsToInitialize);
            Assert.True(success, $"Timed out waiting for the client-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");

            // Set the late join parameter based on the type of test
            if (authoritativeMode == AuthoritativeMode.OwnerAuth)
            {
                var objectToUpdate = ownerShipMode == OwnerShipMode.ClientOwner ? AnimatorTestHelper.ClientSideInstances[m_ClientNetworkManagers[0].LocalClientId] : AnimatorTestHelper.ServerSideInstance;
                // Set the late join parameter via the owner
                objectToUpdate.SetLateJoinParam(true);
            }
            else
            {
                // Set the late join parameter to kick off the late join synchronization state
                // (it rotates to 180 degrees and then stops animating until the value is reset)
                AnimatorTestHelper.ServerSideInstance.SetLateJoinParam(true);
            }

            var firstClientAnimatorTestHelper = AnimatorTestHelper.ClientSideInstances[m_ClientNetworkManagers[0].LocalClientId];

            // Wait for the 1st client to rotate to the 180.0f degree point
            yield return WaitForConditionOrTimeOut(() => Mathf.Approximately(firstClientAnimatorTestHelper.transform.rotation.eulerAngles.y, 180.0f));
            AssertOnTimeout($"Timed out waiting for client-side cube to reach 180.0f!");

            AnimatorTestHelper.ServerSideInstance.GetNetworkAnimator().SynchronizationStateInfo = new List<AnimatorStateInfo>();
            // Create and join a new client (late joining client)
            CreateAndStartNewClientWithTimeTravel();

            Assert.IsTrue(m_ClientNetworkManagers.Length == NumberOfClients + 1, $"Newly created and connected client was not added to {nameof(m_ClientNetworkManagers)}!");

            // Wait for the client to have spawned and the spawned prefab to be instantiated
            success = WaitForConditionOrTimeOutWithTimeTravel(WaitForClientsToInitialize);
            Assert.True(success, $"Timed out waiting for the late joining client-side instance of {GetNetworkAnimatorName(authoritativeMode)} to be spawned!");

            // Make sure the AnimatorTestHelper client side instances is the same as the TotalClients
            var calculatedClients = (AnimatorTestHelper.ClientSideInstances.Count + (m_UseHost ? 1 : 0));
            Assert.True(calculatedClients == TotalClients, $"Number of client");

            var lateJoinObjectInstance = AnimatorTestHelper.ClientSideInstances[m_ClientNetworkManagers[NumberOfClients].LocalClientId];
            yield return WaitForConditionOrTimeOut(() => Mathf.Approximately(lateJoinObjectInstance.transform.rotation.eulerAngles.y, 180.0f));
            AssertOnTimeout($"[Late Join] Timed out waiting for cube to reach 180.0f!");

            // Validate the fix by making sure the late joining client was synchronized to the server's Animator's states
            success = WaitForConditionOrTimeOutWithTimeTravel(LateJoinClientSynchronized);
            Assert.True(success, "[Late Join] Timed out waiting for newly joined client to have expected state synchronized!");

            var newlyJoinedClient = m_ClientNetworkManagers[NumberOfClients];
            StopOneClientWithTimeTravel(newlyJoinedClient);
            VerboseDebug($" ------------------ Late Join Synchronization Test [{TriggerTest.Iteration}][{ownerShipMode}] Stopping ------------------ ");
        }

        /// <summary>
        /// Used by: LateJoinSynchronizationTest
        /// Wait condition method that compares the states of the late joined client
        /// and the server.
        /// </summary>
        private bool LateJoinClientSynchronized()
        {
            if (!StateSyncTest.StatesEntered.ContainsKey(m_ClientNetworkManagers[NumberOfClients].LocalClientId))
            {
                VerboseDebug($"Late join client has not had any states synchronized yet!");
                return false;
            }

            var serverStates = StateSyncTest.StatesEntered[m_ServerNetworkManager.LocalClientId];
            var clientStates = StateSyncTest.StatesEntered[m_ClientNetworkManagers[NumberOfClients].LocalClientId];

            if (serverStates.Count() != clientStates.Count())
            {
                VerboseDebug($"[Count][Server] {serverStates.Count} | [Client-{m_ClientNetworkManagers[NumberOfClients].LocalClientId}]{clientStates.Count}");
                return false;
            }

            // We only check the last layer for this test as the other layers will have their normalized time slightly out of sync
            var index = 2;
            var serverAnimState = AnimatorTestHelper.ServerSideInstance.GetNetworkAnimator().SynchronizationStateInfo[index];// serverStates[index];
            if (clientStates[index].shortNameHash != serverAnimState.shortNameHash)
            {
                VerboseDebug($"[Hash Fail] Server({serverAnimState.shortNameHash}) | Client({clientStates[index].shortNameHash}) ");
                return false;
            }

            var clientNormalizedTime = clientStates[index].normalizedTime;
            var serverNormalizedTime = serverAnimState.normalizedTime;
            if (!Mathf.Approximately(clientNormalizedTime, serverNormalizedTime))
            {
                VerboseDebug($"[NormalizedTime Fail][{index}][{serverStates.Count}:{clientStates.Count}] Server({serverNormalizedTime}) | Client-{m_ClientNetworkManagers[1].LocalClientId}({clientNormalizedTime})");
                return false;
            }
            VerboseDebug($"[NormalizedTime][{index}][{serverStates.Count}:{clientStates.Count}] Server({serverNormalizedTime}) | Client-{m_ClientNetworkManagers[1].LocalClientId}({clientNormalizedTime})");

            return true;
        }

        private bool m_ClientDisconnected;
        /// <summary>
        /// This validates that NetworkAnimator properly removes its subscription to the
        /// OnClientConnectedCallback when it is despawned and destroyed during the
        /// shutdown sequence on both the server and the client.
        /// </summary>
        [Test]
        public void ShutdownWhileSpawnedAndStartBackUpTest()
        {
            VerboseDebug($" ++++++++++++++++++ Disconnect-Reconnect Server Test Starting ++++++++++++++++++ ");
            // Spawn our test animator object
            var objectInstance = SpawnPrefab(false, AuthoritativeMode.ServerAuth);
            var networkObjectInstance = objectInstance.GetComponent<NetworkObject>();
            var serverAnimatorTestHelper = objectInstance.GetComponent<AnimatorTestHelper>();

            // Wait for it to spawn server-side
            var success = WaitForConditionOrTimeOutWithTimeTravel(() => AnimatorTestHelper.ServerSideInstance != null);
            Assert.True(success, $"Timed out waiting for the server-side instance of {GetNetworkAnimatorName(AuthoritativeMode.ServerAuth)} to be spawned!");

            // Wait for it to spawn client-side
            success = WaitForConditionOrTimeOutWithTimeTravel(WaitForClientsToInitialize);
            Assert.True(success, $"Timed out waiting for the client-side instance of {GetNetworkAnimatorName(AuthoritativeMode.ServerAuth)} to be spawned!");

            var clientAnimatorTestHelper = s_GlobalNetworkObjects[m_ClientNetworkManagers[0].LocalClientId].Values.Where((c) => c.GetComponent<AnimatorTestHelper>() != null).First().GetComponent<AnimatorTestHelper>();
            Assert.IsNotNull(clientAnimatorTestHelper, $"Could not find the client side {nameof(AnimatorTestHelper)}!");
            VerboseDebug($" ++++++++++++++++++ Disconnect-Reconnect Shutting Down Clients and Server ++++++++++++++++++ ");
            clientAnimatorTestHelper.OnCheckIsServerIsClient += Client_OnCheckIsServerIsClient;

            // Now shutdown the clients to verify this fix.
            // The client-side spawned NetworkObject should get despawned
            // and invoke the Client_OnCheckIsServerIsClient action.
            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                clientNetworkManager.Shutdown(true);
            }

            // Wait for all clients to be disconnected
            success = WaitForConditionOrTimeOutWithTimeTravel(AllClientsDisconnected);
            Assert.True(success, $"Timed out waiting for the client to disconnect!");

            Assert.IsTrue(m_ClientTestHelperDespawned, $"Client-Side {nameof(AnimatorTestHelper)} did not have a valid IsClient setting!");

            serverAnimatorTestHelper.OnCheckIsServerIsClient += Server_OnCheckIsServerIsClient;

            // Now shutdown the server-side to verify this fix.
            // The server-side spawned NetworkObject should get despawned
            // and invoke the Server_OnCheckIsServerIsClient action.
            var playerPrefabIndex = 0;
            for (int i = 0; i < m_ServerNetworkManager.NetworkConfig.Prefabs.Prefabs.Count; i++)
            {
                if (m_ServerNetworkManager.NetworkConfig.Prefabs.Prefabs[i].Prefab.name.Contains("Player"))
                {
                    playerPrefabIndex = i;
                    break;
                }
            }

            m_ServerNetworkManager.Shutdown();

            TimeTravelToNextTick();

            WaitForConditionOrTimeOutWithTimeTravel(() => !m_ServerNetworkManager.ShutdownInProgress);

            Assert.IsTrue(m_ServerTestHelperDespawned, $"Server-Side {nameof(AnimatorTestHelper)} did not have a valid IsServer setting!");
            AssertOnTimeout($"Timed out waiting for the server to shutdown!");

            VerboseDebug($" ++++++++++++++++++ Disconnect-Reconnect Restarting Server and Client ++++++++++++++++++ ");
            // Since the dynamically generated PlayerPrefab is destroyed when the server shuts down,
            // we need to create a new one and assign it to NetworkPrefab index 0
            m_PlayerPrefab = new GameObject("Player");
            NetworkObject networkObject = m_PlayerPrefab.AddComponent<NetworkObject>();
            NetcodeIntegrationTestHelpers.MakeNetworkObjectTestPrefab(networkObject);
            m_ServerNetworkManager.NetworkConfig.Prefabs.Prefabs[playerPrefabIndex].Prefab = m_PlayerPrefab;

            // Now, restart the server and the client
            m_ServerNetworkManager.StartHost();

            foreach (var clientNetworkManager in m_ClientNetworkManagers)
            {
                clientNetworkManager.NetworkConfig.Prefabs.Prefabs[playerPrefabIndex].Prefab = m_PlayerPrefab;
                clientNetworkManager.StartClient();
            }

            // Wait for the server and clients to start and connect
            success = WaitForClientsConnectedOrTimeOutWithTimeTravel();
            Assert.True(success, $"Client Failed to Connect!");
            VerboseDebug($" ++++++++++++++++++ Disconnect-Reconnect Server Test Stopping ++++++++++++++++++ ");
        }

        private bool m_ServerTestHelperDespawned;
        /// <summary>
        /// Server-Side
        /// This callback will be invoked as the spawned prefab is destroyed during shutdown
        /// </summary>
        private void Server_OnCheckIsServerIsClient(bool isServer, bool isClient)
        {
            // Validates this is still set when the NetworkObject is despawned during shutdown
            Assert.IsTrue(isServer);
            m_ServerTestHelperDespawned = true;
        }

        private bool m_ClientTestHelperDespawned;
        /// <summary>
        /// Client-Side
        /// This callback will be invoked as the spawned prefab is destroyed during shutdown
        /// </summary>
        private void Client_OnCheckIsServerIsClient(bool isServer, bool isClient)
        {
            // Validates this is still set when the NetworkObject is despawned during shutdown
            Assert.IsTrue(isClient);
            m_ClientTestHelperDespawned = true;
        }

        /// <summary>
        /// Wait condition to determine if all clients are disconnected and no longer listening.
        /// </summary>
        private bool AllClientsDisconnected()
        {
            foreach (var client in m_ClientNetworkManagers)
            {
                if (client.IsConnectedClient || client.IsListening || client.ShutdownInProgress)
                {
                    return false;
                }
            }
            return true;
        }
    }
}
