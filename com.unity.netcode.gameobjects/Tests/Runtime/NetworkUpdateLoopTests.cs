using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using UnityEngine.TestTools;

namespace Unity.Netcode.RuntimeTests
{
    internal class NetworkUpdateLoopTests
    {
        [Test]
        public void RegisterCustomLoopInTheMiddle()
        {
            // caching the current PlayerLoop (to prevent side-effects on other tests)
            var cachedPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            {
                // since current PlayerLoop already took NetworkUpdateLoop systems inside,
                // we are going to swap it with the default PlayerLoop temporarily for testing
                PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());

                NetworkUpdateLoop.RegisterLoopSystems();

                var curPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
                int initSubsystemCount = curPlayerLoop.subSystemList[0].subSystemList.Length;
                var newInitSubsystems = new PlayerLoopSystem[initSubsystemCount + 1];
                Array.Copy(curPlayerLoop.subSystemList[0].subSystemList, newInitSubsystems, initSubsystemCount);
                newInitSubsystems[initSubsystemCount] = new PlayerLoopSystem { type = typeof(NetworkUpdateLoopTests) };
                curPlayerLoop.subSystemList[0].subSystemList = newInitSubsystems;
                PlayerLoop.SetPlayerLoop(curPlayerLoop);

                NetworkUpdateLoop.UnregisterLoopSystems();

                // our custom `PlayerLoopSystem` with the type of `NetworkUpdateLoopTests` should still exist
                Assert.AreEqual(typeof(NetworkUpdateLoopTests), PlayerLoop.GetCurrentPlayerLoop().subSystemList[0].subSystemList.Last().type);
            }
            // replace the current PlayerLoop with the cached PlayerLoop after the test
            PlayerLoop.SetPlayerLoop(cachedPlayerLoop);
        }

        [UnityTest]
        public IEnumerator RegisterAndUnregisterSystems()
        {
            // caching the current PlayerLoop (it will have NetworkUpdateLoop systems registered)
            var cachedPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            {
                // since current PlayerLoop already took NetworkUpdateLoop systems inside,
                // we are going to swap it with the default PlayerLoop temporarily for testing
                PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());

                var oldPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();

                NetworkUpdateLoop.RegisterLoopSystems();

                int nextFrameNumber = Time.frameCount + 1;
                yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);

                NetworkUpdateLoop.UnregisterLoopSystems();

                var newPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();

                // recursively compare old and new PlayerLoop systems and their subsystems
                AssertAreEqualPlayerLoopSystems(newPlayerLoop, oldPlayerLoop);
            }
            // replace the current PlayerLoop with the cached PlayerLoop after the test
            PlayerLoop.SetPlayerLoop(cachedPlayerLoop);
        }

        private void AssertAreEqualPlayerLoopSystems(PlayerLoopSystem leftPlayerLoop, PlayerLoopSystem rightPlayerLoop)
        {
            Assert.AreEqual(leftPlayerLoop.type, rightPlayerLoop.type);
            Assert.AreEqual(leftPlayerLoop.subSystemList?.Length ?? 0, rightPlayerLoop.subSystemList?.Length ?? 0);
            for (int i = 0; i < (leftPlayerLoop.subSystemList?.Length ?? 0); i++)
            {
                AssertAreEqualPlayerLoopSystems(leftPlayerLoop.subSystemList[i], rightPlayerLoop.subSystemList[i]);
            }
        }

        [Test]
        public void UpdateStageSystems()
        {
            var currentPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            for (int i = 0; i < currentPlayerLoop.subSystemList.Length; i++)
            {
                var playerLoopSystem = currentPlayerLoop.subSystemList[i];
                var subsystems = playerLoopSystem.subSystemList.ToList();

                if (playerLoopSystem.type == typeof(Initialization))
                {
                    Assert.True(
                        subsystems.Exists(s => s.type == typeof(NetworkUpdateLoop.NetworkInitialization)),
                        nameof(NetworkUpdateLoop.NetworkInitialization));
                }
                else if (playerLoopSystem.type == typeof(EarlyUpdate))
                {
                    Assert.True(
                        subsystems.Exists(s => s.type == typeof(NetworkUpdateLoop.NetworkEarlyUpdate)),
                        nameof(NetworkUpdateLoop.NetworkEarlyUpdate));
                }
                else if (playerLoopSystem.type == typeof(FixedUpdate))
                {
                    Assert.True(
                        subsystems.Exists(s => s.type == typeof(NetworkUpdateLoop.NetworkFixedUpdate)),
                        nameof(NetworkUpdateLoop.NetworkFixedUpdate));
                }
                else if (playerLoopSystem.type == typeof(PreUpdate))
                {
                    Assert.True(
                        subsystems.Exists(s => s.type == typeof(NetworkUpdateLoop.NetworkPreUpdate)),
                        nameof(NetworkUpdateLoop.NetworkPreUpdate));
                }
                else if (playerLoopSystem.type == typeof(Update))
                {
                    Assert.True(
                        subsystems.Exists(s => s.type == typeof(NetworkUpdateLoop.NetworkUpdate)),
                        nameof(NetworkUpdateLoop.NetworkUpdate));
                }
                else if (playerLoopSystem.type == typeof(PreLateUpdate))
                {
                    Assert.True(
                        subsystems.Exists(s => s.type == typeof(NetworkUpdateLoop.NetworkPreLateUpdate)),
                        nameof(NetworkUpdateLoop.NetworkPreLateUpdate));
                }
                else if (playerLoopSystem.type == typeof(PostLateUpdate))
                {
                    Assert.True(
                        subsystems.Exists(s => s.type == typeof(NetworkUpdateLoop.NetworkPostLateUpdate)),
                        nameof(NetworkUpdateLoop.NetworkPostLateUpdate));
                }
            }
        }

        private struct NetworkUpdateCallbacks
        {
            public Action OnInitialization;
            public Action OnEarlyUpdate;
            public Action OnFixedUpdate;
            public Action OnPreUpdate;
            public Action OnUpdate;
            public Action OnPreLateUpdate;
            public Action OnPostLateUpdate;
        }

        private class MyPlainScript : IDisposable, INetworkUpdateSystem
        {
            public NetworkUpdateCallbacks UpdateCallbacks;

            public void Initialize()
            {
                this.RegisterNetworkUpdate(NetworkUpdateStage.EarlyUpdate);
                this.RegisterNetworkUpdate(NetworkUpdateStage.PreLateUpdate);
            }

            public void NetworkUpdate(NetworkUpdateStage updateStage)
            {
                switch (updateStage)
                {
                    case NetworkUpdateStage.Initialization:
                        UpdateCallbacks.OnInitialization();
                        break;
                    case NetworkUpdateStage.EarlyUpdate:
                        UpdateCallbacks.OnEarlyUpdate();
                        break;
                    case NetworkUpdateStage.FixedUpdate:
                        UpdateCallbacks.OnFixedUpdate();
                        break;
                    case NetworkUpdateStage.PreUpdate:
                        UpdateCallbacks.OnPreUpdate();
                        break;
                    case NetworkUpdateStage.Update:
                        UpdateCallbacks.OnUpdate();
                        break;
                    case NetworkUpdateStage.PreLateUpdate:
                        UpdateCallbacks.OnPreLateUpdate();
                        break;
                    case NetworkUpdateStage.PostLateUpdate:
                        UpdateCallbacks.OnPostLateUpdate();
                        break;
                }
            }

            public void Dispose()
            {
                this.UnregisterAllNetworkUpdates();
            }
        }

        [UnityTest]
        public IEnumerator UpdateStagesPlain()
        {
            const int kNetInitializationIndex = 0;
            const int kNetEarlyUpdateIndex = 1;
            const int kNetFixedUpdateIndex = 2;
            const int kNetPreUpdateIndex = 3;
            const int kNetUpdateIndex = 4;
            const int kNetPreLateUpdateIndex = 5;
            const int kNetPostLateUpdateIndex = 6;
            int[] netUpdates = new int[7];

            bool isTesting = false;
            using var plainScript = new MyPlainScript();
            plainScript.UpdateCallbacks = new NetworkUpdateCallbacks
            {
                OnInitialization = () =>
                {
                    if (isTesting)
                    {
                        netUpdates[kNetInitializationIndex]++;
                    }
                },
                OnEarlyUpdate = () =>
                {
                    if (isTesting)
                    {
                        netUpdates[kNetEarlyUpdateIndex]++;
                    }
                },
                OnFixedUpdate = () =>
                {
                    if (isTesting)
                    {
                        netUpdates[kNetFixedUpdateIndex]++;
                    }
                },
                OnPreUpdate = () =>
                {
                    if (isTesting)
                    {
                        netUpdates[kNetPreUpdateIndex]++;
                    }
                },
                OnUpdate = () =>
                {
                    if (isTesting)
                    {
                        netUpdates[kNetUpdateIndex]++;
                    }
                },
                OnPreLateUpdate = () =>
                {
                    if (isTesting)
                    {
                        netUpdates[kNetPreLateUpdateIndex]++;
                    }
                },
                OnPostLateUpdate = () =>
                {
                    if (isTesting)
                    {
                        netUpdates[kNetPostLateUpdateIndex]++;
                    }
                }
            };

            plainScript.Initialize();
            int nextFrameNumber = Time.frameCount + 1;
            yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
            isTesting = true;

            const int kRunTotalFrames = 16;
            int waitFrameNumber = Time.frameCount + kRunTotalFrames;
            yield return new WaitUntil(() => Time.frameCount >= waitFrameNumber);

            Assert.AreEqual(0, netUpdates[kNetInitializationIndex]);
            Assert.AreEqual(kRunTotalFrames, netUpdates[kNetEarlyUpdateIndex]);
            Assert.AreEqual(0, netUpdates[kNetFixedUpdateIndex]);
            Assert.AreEqual(0, netUpdates[kNetPreUpdateIndex]);
            Assert.AreEqual(0, netUpdates[kNetUpdateIndex]);
            Assert.AreEqual(kRunTotalFrames, netUpdates[kNetPreLateUpdateIndex]);
            Assert.AreEqual(0, netUpdates[kNetPostLateUpdateIndex]);
        }

        private struct MonoBehaviourCallbacks
        {
            public Action OnFixedUpdate;
            public Action OnUpdate;
            public Action OnLateUpdate;
        }

        private class MyGameScript : MonoBehaviour, INetworkUpdateSystem
        {
            public NetworkUpdateCallbacks UpdateCallbacks;
            public MonoBehaviourCallbacks BehaviourCallbacks;

            private void Awake()
            {
                this.RegisterNetworkUpdate(NetworkUpdateStage.FixedUpdate);
                this.RegisterNetworkUpdate(NetworkUpdateStage.PreUpdate);
                this.RegisterNetworkUpdate(NetworkUpdateStage.PreLateUpdate);
                this.RegisterNetworkUpdate(NetworkUpdateStage.PostLateUpdate);

                // intentionally try to register for 'PreUpdate' stage twice
                // it should be ignored and the instance should not be registered twice
                // otherwise test would fail because it would call 'OnPreUpdate()' twice
                // which would ultimately increment 'netUpdates[idx]' integer twice
                // and cause 'Assert.AreEqual()' to fail the test
                this.RegisterNetworkUpdate(NetworkUpdateStage.PreUpdate);
            }

            public void NetworkUpdate(NetworkUpdateStage updateStage)
            {
                switch (updateStage)
                {
                    case NetworkUpdateStage.FixedUpdate:
                        UpdateCallbacks.OnFixedUpdate();
                        break;
                    case NetworkUpdateStage.PreUpdate:
                        UpdateCallbacks.OnPreUpdate();
                        break;
                    case NetworkUpdateStage.PreLateUpdate:
                        UpdateCallbacks.OnPreLateUpdate();
                        break;
                    case NetworkUpdateStage.PostLateUpdate:
                        UpdateCallbacks.OnPostLateUpdate();
                        break;
                }
            }

            private void FixedUpdate()
            {
                BehaviourCallbacks.OnFixedUpdate();
            }

            private void Update()
            {
                BehaviourCallbacks.OnUpdate();
            }

            private void LateUpdate()
            {
                BehaviourCallbacks.OnLateUpdate();
            }

            private void OnDestroy()
            {
                this.UnregisterAllNetworkUpdates();
            }
        }

        [UnityTest]
        public IEnumerator UpdateStagesMixed()
        {
            const int kNetFixedUpdateIndex = 0;
            const int kNetPreUpdateIndex = 1;
            const int kNetPreLateUpdateIndex = 2;
            const int kNetPostLateUpdateIndex = 3;
            int[] netUpdates = new int[4];
            const int kMonoFixedUpdateIndex = 0;
            const int kMonoUpdateIndex = 1;
            const int kMonoLateUpdateIndex = 2;
            int[] monoUpdates = new int[3];

            bool isTesting = false;
            {
                var gameObject = new GameObject($"{nameof(NetworkUpdateLoopTests)}.{nameof(UpdateStagesMixed)} (Dummy)");
                var gameScript = gameObject.AddComponent<MyGameScript>();
                gameScript.UpdateCallbacks = new NetworkUpdateCallbacks
                {
                    OnFixedUpdate = () =>
                    {
                        if (isTesting)
                        {
                            netUpdates[kNetFixedUpdateIndex]++;
                            Assert.AreEqual(monoUpdates[kMonoFixedUpdateIndex] + 1, netUpdates[kNetFixedUpdateIndex]);
                        }
                    },
                    OnPreUpdate = () =>
                    {
                        if (isTesting)
                        {
                            netUpdates[kNetPreUpdateIndex]++;
                            Assert.AreEqual(monoUpdates[kMonoUpdateIndex] + 1, netUpdates[kNetPreUpdateIndex]);
                        }
                    },
                    OnPreLateUpdate = () =>
                    {
                        if (isTesting)
                        {
                            netUpdates[kNetPreLateUpdateIndex]++;
                            Assert.AreEqual(monoUpdates[kMonoLateUpdateIndex] + 1, netUpdates[kNetPreLateUpdateIndex]);
                        }
                    },
                    OnPostLateUpdate = () =>
                    {
                        if (isTesting)
                        {
                            netUpdates[kNetPostLateUpdateIndex]++;
                            Assert.AreEqual(netUpdates[kNetPostLateUpdateIndex], netUpdates[kNetPreLateUpdateIndex]);
                        }
                    }
                };
                gameScript.BehaviourCallbacks = new MonoBehaviourCallbacks
                {
                    OnFixedUpdate = () =>
                    {
                        if (isTesting)
                        {
                            monoUpdates[kMonoFixedUpdateIndex]++;
                            Assert.AreEqual(netUpdates[kNetFixedUpdateIndex], monoUpdates[kMonoFixedUpdateIndex]);
                        }
                    },
                    OnUpdate = () =>
                    {
                        if (isTesting)
                        {
                            monoUpdates[kMonoUpdateIndex]++;
                            Assert.AreEqual(netUpdates[kNetPreUpdateIndex], monoUpdates[kMonoUpdateIndex]);
                        }
                    },
                    OnLateUpdate = () =>
                    {
                        if (isTesting)
                        {
                            monoUpdates[kMonoLateUpdateIndex]++;
                            Assert.AreEqual(netUpdates[kNetPreLateUpdateIndex], monoUpdates[kMonoLateUpdateIndex]);
                        }
                    }
                };

                int nextFrameNumber = Time.frameCount + 1;
                yield return new WaitUntil(() => Time.frameCount >= nextFrameNumber);
                isTesting = true;

                const int kRunTotalFrames = 16;
                int waitFrameNumber = Time.frameCount + kRunTotalFrames;
                yield return new WaitUntil(() => Time.frameCount >= waitFrameNumber);

                Assert.AreEqual(kRunTotalFrames, netUpdates[kNetPreUpdateIndex]);
                Assert.AreEqual(netUpdates[kNetPreUpdateIndex], monoUpdates[kMonoUpdateIndex]);

                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }
    }
}
