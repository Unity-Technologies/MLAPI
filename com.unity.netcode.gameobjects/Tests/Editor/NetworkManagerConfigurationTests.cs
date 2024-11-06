using System.Collections.Generic;
using NUnit.Framework;
using Unity.Netcode.Editor;
using Unity.Netcode.Transports.UTP;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Unity.Netcode.EditorTests
{
    internal class NetworkManagerConfigurationTests
    {
        [SetUp]
        public void OnSetup()
        {
            ILPPMessageProvider.IntegrationTestNoMessages = true;
        }

        [TearDown]
        public void OnTearDown()
        {
            ILPPMessageProvider.IntegrationTestNoMessages = false;
        }

        /// <summary>
        /// Does a simple check to make sure the nested network manager will
        /// notify the user when in the editor.  This is just a unit test to
        /// validate this is functioning
        /// </summary>
        [Test]
        public void NestedNetworkManagerCheck()
        {
            var parent = new GameObject("ParentObject");
            var networkManagerObject = new GameObject(nameof(NestedNetworkManagerCheck));
            var networkManager = networkManagerObject.AddComponent<NetworkManager>();

            // Make our NetworkManager's GameObject nested
            networkManagerObject.transform.parent = parent.transform;

            // Pre-generate the error message we are expecting to see
            var messageToCheck = NetworkManager.GenerateNestedNetworkManagerMessage(networkManagerObject.transform);

            // Trap for the nested NetworkManager exception
            LogAssert.Expect(LogType.Error, messageToCheck);

            // Since this is an in-editor test, we must force this invocation
            NetworkManagerHelper.Singleton.NotifyUserOfNestedNetworkManager(networkManager, false, true);

            // Clean up
            Object.DestroyImmediate(parent);
        }

        public enum NetworkObjectPlacement
        {
            Root,   // Added to the same root GameObject
            Child   // Added to a child GameObject
        }

        [Test]
        public void NetworkObjectNotAllowed([Values] NetworkObjectPlacement networkObjectPlacement)
        {
            var gameObject = new GameObject(nameof(NetworkManager));
            var targetforNetworkObject = gameObject;

            if (networkObjectPlacement == NetworkObjectPlacement.Child)
            {
                var childGameObject = new GameObject($"{nameof(NetworkManager)}-Child");
                childGameObject.transform.parent = targetforNetworkObject.transform;
                targetforNetworkObject = childGameObject;
            }

            var networkManager = gameObject.AddComponent<NetworkManager>();

            // Trap for the error message generated when a NetworkObject is discovered on the same GameObject or any children under it
            LogAssert.Expect(LogType.Error, NetworkManagerHelper.Singleton.NetworkManagerAndNetworkObjectNotAllowedMessage());

            // Add the NetworkObject
            var networkObject = targetforNetworkObject.AddComponent<NetworkObject>();

            // Since this is an in-editor test, we must force this invocation
            NetworkManagerHelper.Singleton.CheckAndNotifyUserNetworkObjectRemoved(networkManager, true);

            // Validate that the NetworkObject has been removed
            if (networkObjectPlacement == NetworkObjectPlacement.Root)
            {
                Assert.IsNull(networkManager.gameObject.GetComponent<NetworkObject>(), $"There is still a {nameof(NetworkObject)} on {nameof(NetworkManager)}'s GameObject!");
            }
            else
            {
                Assert.IsNull(networkManager.gameObject.GetComponentInChildren<NetworkObject>(), $"There is still a {nameof(NetworkObject)} on {nameof(NetworkManager)}'s child GameObject!");
            }

            // Clean up
            Object.DestroyImmediate(gameObject);
        }

        [Test]
        public void NestedNetworkObjectPrefabCheck()
        {
            // Setup
            var networkManagerObject = new GameObject(nameof(NestedNetworkObjectPrefabCheck));
            var networkManager = networkManagerObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig();

            var parent = new GameObject("Parent").AddComponent<NetworkObject>();
            var child = new GameObject("Child").AddComponent<NetworkObject>();

            // Set parent
            child.transform.SetParent(parent.transform);

            // Make it a prefab, warning only applies to prefabs
            networkManager.AddNetworkPrefab(parent.gameObject);

            // Mark scene as dirty to ensure OnValidate actually runs
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());

            // Force OnValidate
            networkManager.OnValidate();

            // Expect a warning
            LogAssert.Expect(LogType.Warning, $"[Netcode] {NetworkPrefabHandler.PrefabDebugHelper(networkManager.NetworkConfig.Prefabs.Prefabs[0])} has child {nameof(NetworkObject)}(s) but they will not be spawned across the network (unsupported {nameof(NetworkPrefab)} setup)");

            // Clean up
            Object.DestroyImmediate(networkManagerObject);
            Object.DestroyImmediate(parent);

        }

        [Test]
        public void WhenNetworkConfigContainsOldPrefabList_TheyMigrateProperlyToTheNewList()
        {
            var networkConfig = new NetworkConfig();

            var regularPrefab = new GameObject("Regular Prefab").AddComponent<NetworkObject>();
            var overriddenPrefab = new GameObject("Overridden Prefab").AddComponent<NetworkObject>();
            var overridingTargetPrefab = new GameObject("Overriding Target Prefab").AddComponent<NetworkObject>();
            var sourcePrefabToOverride = new GameObject("Overriding Source Prefab").AddComponent<NetworkObject>();

            regularPrefab.GlobalObjectIdHash = 1;
            overriddenPrefab.GlobalObjectIdHash = 2;
            overridingTargetPrefab.GlobalObjectIdHash = 3;
            sourcePrefabToOverride.GlobalObjectIdHash = 4;

            networkConfig.OldPrefabList = new List<NetworkPrefab>
            {
                new NetworkPrefab { Prefab = regularPrefab.gameObject },
                new NetworkPrefab { Prefab = overriddenPrefab.gameObject, Override = NetworkPrefabOverride.Prefab, OverridingTargetPrefab = overridingTargetPrefab.gameObject, SourcePrefabToOverride = sourcePrefabToOverride.gameObject, SourceHashToOverride = 123456 }
            };

            networkConfig.InitializePrefabs();

            Assert.IsNull(networkConfig.OldPrefabList);
            Assert.IsNotNull(networkConfig.Prefabs);
            Assert.IsNotNull(networkConfig.Prefabs.Prefabs);
            Assert.AreEqual(2, networkConfig.Prefabs.Prefabs.Count);

            Assert.AreSame(regularPrefab.gameObject, networkConfig.Prefabs.Prefabs[0].Prefab);
            Assert.AreEqual(NetworkPrefabOverride.None, networkConfig.Prefabs.Prefabs[0].Override);
            Assert.IsNull(networkConfig.Prefabs.Prefabs[0].SourcePrefabToOverride);
            Assert.IsNull(networkConfig.Prefabs.Prefabs[0].OverridingTargetPrefab);

            Assert.AreSame(overriddenPrefab.gameObject, networkConfig.Prefabs.Prefabs[1].Prefab);
            Assert.AreEqual(NetworkPrefabOverride.Prefab, networkConfig.Prefabs.Prefabs[1].Override);
            Assert.AreEqual(123456, networkConfig.Prefabs.Prefabs[1].SourceHashToOverride);
            Assert.AreSame(sourcePrefabToOverride.gameObject, networkConfig.Prefabs.Prefabs[1].SourcePrefabToOverride);
            Assert.AreSame(overridingTargetPrefab.gameObject, networkConfig.Prefabs.Prefabs[1].OverridingTargetPrefab);
        }

        [Test]
        public void WhenModifyingPrefabListUsingNetworkManagerAPI_ModificationIsLocal()
        {
            // Setup
            var networkManagerObject = new GameObject(nameof(NestedNetworkObjectPrefabCheck));
            var networkManager = networkManagerObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = networkManager.gameObject.AddComponent<UnityTransport>()
            };

            var networkManagerObject2 = new GameObject(nameof(NestedNetworkObjectPrefabCheck));
            var networkManager2 = networkManagerObject2.AddComponent<NetworkManager>();
            networkManager2.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = networkManager.gameObject.AddComponent<UnityTransport>()
            };

            try
            {
                var object1 = new GameObject("Object 1").AddComponent<NetworkObject>();

                var object2 = new GameObject("Object 2").AddComponent<NetworkObject>();
                var object3 = new GameObject("Object 3").AddComponent<NetworkObject>();

                object1.GlobalObjectIdHash = 1;
                object2.GlobalObjectIdHash = 2;
                object3.GlobalObjectIdHash = 3;

                var sharedList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                sharedList.List.Add(new NetworkPrefab { Prefab = object1.gameObject });

                networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { sharedList };
                networkManager2.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { sharedList };

                networkManager.Initialize(true);
                networkManager2.Initialize(false);

                networkManager.AddNetworkPrefab(object2.gameObject);
                networkManager2.AddNetworkPrefab(object3.gameObject);

                Assert.IsTrue(networkManager.NetworkConfig.Prefabs.Contains(object1.gameObject));
                Assert.IsTrue(networkManager2.NetworkConfig.Prefabs.Contains(object1.gameObject));
                Assert.IsTrue(networkManager.NetworkConfig.Prefabs.Contains(object2.gameObject));
                Assert.IsFalse(networkManager2.NetworkConfig.Prefabs.Contains(object2.gameObject));
                Assert.IsTrue(networkManager2.NetworkConfig.Prefabs.Contains(object3.gameObject));
                Assert.IsFalse(networkManager.NetworkConfig.Prefabs.Contains(object3.gameObject));

                Assert.IsTrue(sharedList.Contains(object1.gameObject));
                Assert.IsFalse(sharedList.Contains(object2.gameObject));
                Assert.IsFalse(sharedList.Contains(object3.gameObject));
            }
            finally
            {
                networkManager.ShutdownInternal();
                networkManager2.ShutdownInternal();
                // Shutdown doesn't get called correctly because we called Initialize()
                // instead of calling StartHost/StartClient/StartServer. See MTT-860 for
                // why.
                networkManager.NetworkConfig?.NetworkTransport.Shutdown();
                networkManager2.NetworkConfig?.NetworkTransport.Shutdown();
            }
        }

        [Test]
        public void WhenModifyingPrefabListUsingPrefabsAPI_ModificationIsLocal()
        {
            // Setup
            var networkManagerObject = new GameObject(nameof(NestedNetworkObjectPrefabCheck));
            var networkManager = networkManagerObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = networkManager.gameObject.AddComponent<UnityTransport>()
            };

            var networkManagerObject2 = new GameObject(nameof(NestedNetworkObjectPrefabCheck));
            var networkManager2 = networkManagerObject2.AddComponent<NetworkManager>();
            networkManager2.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = networkManager.gameObject.AddComponent<UnityTransport>()
            };

            try
            {
                var object1 = new GameObject("Object 1").AddComponent<NetworkObject>();
                var object2 = new GameObject("Object 2").AddComponent<NetworkObject>();
                var object3 = new GameObject("Object 3").AddComponent<NetworkObject>();

                object1.GlobalObjectIdHash = 1;
                object2.GlobalObjectIdHash = 2;
                object3.GlobalObjectIdHash = 3;

                var sharedList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                sharedList.List.Add(new NetworkPrefab { Prefab = object1.gameObject });

                networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { sharedList };
                networkManager2.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { sharedList };

                networkManager.Initialize(true);
                networkManager2.Initialize(false);

                networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = object2.gameObject });
                networkManager2.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = object3.gameObject });

                Assert.IsTrue(networkManager.NetworkConfig.Prefabs.Contains(object1.gameObject));
                Assert.IsTrue(networkManager2.NetworkConfig.Prefabs.Contains(object1.gameObject));
                Assert.IsTrue(networkManager.NetworkConfig.Prefabs.Contains(object2.gameObject));
                Assert.IsFalse(networkManager2.NetworkConfig.Prefabs.Contains(object2.gameObject));
                Assert.IsTrue(networkManager2.NetworkConfig.Prefabs.Contains(object3.gameObject));
                Assert.IsFalse(networkManager.NetworkConfig.Prefabs.Contains(object3.gameObject));

                Assert.IsTrue(sharedList.Contains(object1.gameObject));
                Assert.IsFalse(sharedList.Contains(object2.gameObject));
                Assert.IsFalse(sharedList.Contains(object3.gameObject));
            }
            finally
            {
                networkManager.ShutdownInternal();
                networkManager2.ShutdownInternal();
                // Shutdown doesn't get called correctly because we called Initialize()
                // instead of calling StartHost/StartClient/StartServer. See MTT-860 for
                // why.
                networkManager.NetworkConfig?.NetworkTransport.Shutdown();
                networkManager2.NetworkConfig?.NetworkTransport.Shutdown();
            }
        }

        [Test]
        public void WhenModifyingPrefabListUsingPrefabsListAPI_ModificationIsShared()
        {
            // Setup
            var networkManagerObject = new GameObject(nameof(NestedNetworkObjectPrefabCheck));
            var networkManager = networkManagerObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = networkManager.gameObject.AddComponent<UnityTransport>()
            };

            var networkManagerObject2 = new GameObject(nameof(NestedNetworkObjectPrefabCheck));
            var networkManager2 = networkManagerObject2.AddComponent<NetworkManager>();
            networkManager2.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = networkManager.gameObject.AddComponent<UnityTransport>()
            };

            try
            {
                var object1 = new GameObject("Object 1").AddComponent<NetworkObject>();
                var object2 = new GameObject("Object 2").AddComponent<NetworkObject>();
                var object3 = new GameObject("Object 3").AddComponent<NetworkObject>();

                object1.GlobalObjectIdHash = 1;
                object2.GlobalObjectIdHash = 2;
                object3.GlobalObjectIdHash = 3;

                var sharedList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                sharedList.List.Add(new NetworkPrefab { Prefab = object1.gameObject });

                networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { sharedList };
                networkManager2.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { sharedList };

                networkManager.Initialize(true);
                networkManager2.Initialize(false);

                networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists[0].Add(new NetworkPrefab { Prefab = object2.gameObject });
                networkManager2.NetworkConfig.Prefabs.NetworkPrefabsLists[0].Add(new NetworkPrefab { Prefab = object3.gameObject });

                Assert.IsTrue(networkManager.NetworkConfig.Prefabs.Contains(object1.gameObject));
                Assert.IsTrue(networkManager2.NetworkConfig.Prefabs.Contains(object1.gameObject));
                Assert.IsTrue(networkManager.NetworkConfig.Prefabs.Contains(object2.gameObject));
                Assert.IsTrue(networkManager2.NetworkConfig.Prefabs.Contains(object2.gameObject));
                Assert.IsTrue(networkManager2.NetworkConfig.Prefabs.Contains(object3.gameObject));
                Assert.IsTrue(networkManager.NetworkConfig.Prefabs.Contains(object3.gameObject));

                Assert.IsTrue(sharedList.Contains(object1.gameObject));
                Assert.IsTrue(sharedList.Contains(object2.gameObject));
                Assert.IsTrue(sharedList.Contains(object3.gameObject));
            }
            finally
            {
                networkManager.ShutdownInternal();
                networkManager2.ShutdownInternal();
                // Shutdown doesn't get called correctly because we called Initialize()
                // instead of calling StartHost/StartClient/StartServer. See MTT-860 for
                // why.
                networkManager.NetworkConfig?.NetworkTransport.Shutdown();
                networkManager2.NetworkConfig?.NetworkTransport.Shutdown();
            }
        }

        [Test]
        public void WhenCallingInitializeAfterAddingAPrefabUsingPrefabsAPI_ThePrefabStillExists()
        {
            // Setup
            var networkManagerObject = new GameObject(nameof(NestedNetworkObjectPrefabCheck));
            var networkManager = networkManagerObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = networkManager.gameObject.AddComponent<UnityTransport>()
            };

            var networkManagerObject2 = new GameObject(nameof(NestedNetworkObjectPrefabCheck));
            var networkManager2 = networkManagerObject2.AddComponent<NetworkManager>();
            networkManager2.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = networkManager.gameObject.AddComponent<UnityTransport>()
            };

            try
            {
                var object1 = new GameObject("Object 1").AddComponent<NetworkObject>();
                var object2 = new GameObject("Object 2").AddComponent<NetworkObject>();
                var object3 = new GameObject("Object 3").AddComponent<NetworkObject>();

                object1.GlobalObjectIdHash = 1;
                object2.GlobalObjectIdHash = 2;
                object3.GlobalObjectIdHash = 3;

                var sharedList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                sharedList.List.Add(new NetworkPrefab { Prefab = object1.gameObject });

                networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { sharedList };
                networkManager2.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { sharedList };

                networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = object2.gameObject });
                networkManager2.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = object3.gameObject });

                networkManager.Initialize(true);
                networkManager2.Initialize(false);

                Assert.IsTrue(networkManager.NetworkConfig.Prefabs.Contains(object1.gameObject));
                Assert.IsTrue(networkManager2.NetworkConfig.Prefabs.Contains(object1.gameObject));
                Assert.IsTrue(networkManager.NetworkConfig.Prefabs.Contains(object2.gameObject));
                Assert.IsFalse(networkManager2.NetworkConfig.Prefabs.Contains(object2.gameObject));
                Assert.IsTrue(networkManager2.NetworkConfig.Prefabs.Contains(object3.gameObject));
                Assert.IsFalse(networkManager.NetworkConfig.Prefabs.Contains(object3.gameObject));

                Assert.IsTrue(sharedList.Contains(object1.gameObject));
                Assert.IsFalse(sharedList.Contains(object2.gameObject));
                Assert.IsFalse(sharedList.Contains(object3.gameObject));
            }
            finally
            {
                networkManager.ShutdownInternal();
                networkManager2.ShutdownInternal();
                // Shutdown doesn't get called correctly because we called Initialize()
                // instead of calling StartHost/StartClient/StartServer. See MTT-860 for
                // why.
                networkManager.NetworkConfig?.NetworkTransport.Shutdown();
                networkManager2.NetworkConfig?.NetworkTransport.Shutdown();
            }
        }

        [Test]
        public void WhenShuttingDownAndReinitializingPrefabs_RuntimeAddedPrefabsStillExists()
        {
            // Setup
            var networkManagerObject = new GameObject(nameof(NestedNetworkObjectPrefabCheck));
            var networkManager = networkManagerObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = networkManager.gameObject.AddComponent<UnityTransport>()
            };

            var networkManagerObject2 = new GameObject(nameof(NestedNetworkObjectPrefabCheck));
            var networkManager2 = networkManagerObject2.AddComponent<NetworkManager>();
            networkManager2.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = networkManager.gameObject.AddComponent<UnityTransport>()
            };

            try
            {
                var object1 = new GameObject("Object 1").AddComponent<NetworkObject>();
                var object2 = new GameObject("Object 2").AddComponent<NetworkObject>();
                var object3 = new GameObject("Object 3").AddComponent<NetworkObject>();

                object1.GlobalObjectIdHash = 1;
                object2.GlobalObjectIdHash = 2;
                object3.GlobalObjectIdHash = 3;

                var sharedList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                sharedList.List.Add(new NetworkPrefab { Prefab = object1.gameObject });

                networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { sharedList };
                networkManager2.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { sharedList };

                networkManager.Initialize(true);
                networkManager2.Initialize(false);

                networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = object2.gameObject });
                networkManager2.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = object3.gameObject });

                networkManager.ShutdownInternal();
                networkManager2.ShutdownInternal();
                // Shutdown doesn't get called correctly because we called Initialize()
                // instead of calling StartHost/StartClient/StartServer. See MTT-860 for
                // why.
                networkManager.NetworkConfig?.NetworkTransport.Shutdown();
                networkManager2.NetworkConfig?.NetworkTransport.Shutdown();

                networkManager.Initialize(true);
                networkManager2.Initialize(false);

                Assert.IsTrue(networkManager.NetworkConfig.Prefabs.Contains(object1.gameObject));
                Assert.IsTrue(networkManager2.NetworkConfig.Prefabs.Contains(object1.gameObject));
                Assert.IsTrue(networkManager.NetworkConfig.Prefabs.Contains(object2.gameObject));
                Assert.IsFalse(networkManager2.NetworkConfig.Prefabs.Contains(object2.gameObject));
                Assert.IsTrue(networkManager2.NetworkConfig.Prefabs.Contains(object3.gameObject));
                Assert.IsFalse(networkManager.NetworkConfig.Prefabs.Contains(object3.gameObject));

                Assert.IsTrue(sharedList.Contains(object1.gameObject));
                Assert.IsFalse(sharedList.Contains(object2.gameObject));
                Assert.IsFalse(sharedList.Contains(object3.gameObject));
            }
            finally
            {
                networkManager.ShutdownInternal();
                networkManager2.ShutdownInternal();
                // Shutdown doesn't get called correctly because we called Initialize()
                // instead of calling StartHost/StartClient/StartServer. See MTT-860 for
                // why.
                networkManager.NetworkConfig?.NetworkTransport.Shutdown();
                networkManager2.NetworkConfig?.NetworkTransport.Shutdown();
            }
        }

        [Test]
        public void WhenCallingInitializeMultipleTimes_NothingBreaks()
        {
            // Setup
            var networkManagerObject = new GameObject(nameof(NestedNetworkObjectPrefabCheck));
            var networkManager = networkManagerObject.AddComponent<NetworkManager>();
            networkManager.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = networkManager.gameObject.AddComponent<UnityTransport>()
            };

            var networkManagerObject2 = new GameObject(nameof(NestedNetworkObjectPrefabCheck));
            var networkManager2 = networkManagerObject2.AddComponent<NetworkManager>();
            networkManager2.NetworkConfig = new NetworkConfig
            {
                NetworkTransport = networkManager.gameObject.AddComponent<UnityTransport>()
            };

            try
            {
                var object1 = new GameObject("Object 1").AddComponent<NetworkObject>();
                var object2 = new GameObject("Object 2").AddComponent<NetworkObject>();
                var object3 = new GameObject("Object 3").AddComponent<NetworkObject>();

                object1.GlobalObjectIdHash = 1;
                object2.GlobalObjectIdHash = 2;
                object3.GlobalObjectIdHash = 3;

                var sharedList = ScriptableObject.CreateInstance<NetworkPrefabsList>();
                sharedList.List.Add(new NetworkPrefab { Prefab = object1.gameObject });

                networkManager.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { sharedList };
                networkManager2.NetworkConfig.Prefabs.NetworkPrefabsLists = new List<NetworkPrefabsList> { sharedList };

                networkManager.Initialize(true);
                networkManager2.Initialize(false);

                networkManager.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = object2.gameObject });
                networkManager2.NetworkConfig.Prefabs.Add(new NetworkPrefab { Prefab = object3.gameObject });

                networkManager.NetworkConfig.Prefabs.Initialize();
                networkManager2.NetworkConfig.Prefabs.Initialize();

                Assert.IsTrue(networkManager.NetworkConfig.Prefabs.Contains(object1.gameObject));
                Assert.IsTrue(networkManager2.NetworkConfig.Prefabs.Contains(object1.gameObject));
                Assert.IsTrue(networkManager.NetworkConfig.Prefabs.Contains(object2.gameObject));
                Assert.IsFalse(networkManager2.NetworkConfig.Prefabs.Contains(object2.gameObject));
                Assert.IsTrue(networkManager2.NetworkConfig.Prefabs.Contains(object3.gameObject));
                Assert.IsFalse(networkManager.NetworkConfig.Prefabs.Contains(object3.gameObject));

                Assert.IsTrue(sharedList.Contains(object1.gameObject));
                Assert.IsFalse(sharedList.Contains(object2.gameObject));
                Assert.IsFalse(sharedList.Contains(object3.gameObject));
            }
            finally
            {
                networkManager.ShutdownInternal();
                networkManager2.ShutdownInternal();
                // Shutdown doesn't get called correctly because we called Initialize()
                // instead of calling StartHost/StartClient/StartServer. See MTT-860 for
                // why.
                networkManager.NetworkConfig?.NetworkTransport.Shutdown();
                networkManager2.NetworkConfig?.NetworkTransport.Shutdown();
            }
        }
    }
}
