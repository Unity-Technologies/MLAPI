using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;

namespace Unity.Netcode
{
    /// <summary>
    /// Defines the required interface of a network update system being executed by the <see cref="NetworkUpdateLoop"/>.
    /// </summary>
    public interface INetworkUpdateSystem
    {
        /// <summary>
        /// The update method that is being executed in the context of related <see cref="NetworkUpdateStage"/>.
        /// </summary>
        /// <param name="updateStage">The <see cref="NetworkUpdateStage"/> that is being executed.</param>
        void NetworkUpdate(NetworkUpdateStage updateStage);
    }

    /// <summary>
    /// Defines network update stages being executed by the network update loop.
    /// See for more details on update stages:
    /// https://docs.unity3d.com/ScriptReference/PlayerLoop.Initialization.html
    /// </summary>
    public enum NetworkUpdateStage : byte
    {
        /// <summary>
        /// Default value
        /// </summary>
        Unset = 0,
        /// <summary>
        /// Very first initialization update
        /// </summary>
        Initialization = 1,
        /// <summary>
        /// Invoked before Fixed update
        /// </summary>
        EarlyUpdate = 2,
        /// <summary>
        /// Fixed Update (i.e. state machine, physics, animations, etc)
        /// </summary>
        FixedUpdate = 3,
        /// <summary>
        /// Updated before the Monobehaviour.Update for all components is invoked
        /// </summary>
        PreUpdate = 4,
        /// <summary>
        /// Updated when the Monobehaviour.Update for all components is invoked
        /// </summary>
        Update = 5,
        /// <summary>
        /// Updated before the Monobehaviour.LateUpdate for all components is invoked
        /// </summary>
        PreLateUpdate = 6,
        /// <summary>
        /// Updated after Monobehaviour.LateUpdate, but BEFORE rendering
        /// </summary>
        // Yes, these numbers are out of order due to backward compatibility requirements.
        // The enum values are listed in the order they will be called.
        PostScriptLateUpdate = 8,
        /// <summary>
        /// Updated after the Monobehaviour.LateUpdate for all components is invoked
        /// and all rendering is complete
        /// </summary>
        PostLateUpdate = 7
    }

    /// <summary>
    /// Represents the network update loop injected into low-level player loop in Unity.
    /// </summary>
    public static class NetworkUpdateLoop
    {
        private static Dictionary<NetworkUpdateStage, HashSet<INetworkUpdateSystem>> s_UpdateSystem_Sets;
        private static Dictionary<NetworkUpdateStage, INetworkUpdateSystem[]> s_UpdateSystem_Arrays;
        private const int k_UpdateSystem_InitialArrayCapacity = 1024;

        private static NetworkUpdateStage[] _CACHED_ENUM = null;

        public static NetworkUpdateStage[] GetNetworkUpdateStageEnumValues()
        {
            if (_CACHED_ENUM == null)
            {
                _CACHED_ENUM = (NetworkUpdateStage[])Enum.GetValues(typeof(NetworkUpdateStage));
            }

            return _CACHED_ENUM;
        }

        static NetworkUpdateLoop()
        {
            s_UpdateSystem_Sets = new Dictionary<NetworkUpdateStage, HashSet<INetworkUpdateSystem>>();
            s_UpdateSystem_Arrays = new Dictionary<NetworkUpdateStage, INetworkUpdateSystem[]>();

            foreach (NetworkUpdateStage updateStage in GetNetworkUpdateStageEnumValues())
            {
                s_UpdateSystem_Sets.Add(updateStage, new HashSet<INetworkUpdateSystem>());
                s_UpdateSystem_Arrays.Add(updateStage, new INetworkUpdateSystem[k_UpdateSystem_InitialArrayCapacity]);
            }
        }

        /// <summary>
        /// Registers a network update system to be executed in all network update stages.
        /// </summary>
        /// <param name="updateSystem">The <see cref="INetworkUpdateSystem"/> implementation to register for all network updates</param>
        public static void RegisterAllNetworkUpdates(this INetworkUpdateSystem updateSystem)
        {
            foreach (NetworkUpdateStage updateStage in GetNetworkUpdateStageEnumValues())
            {
                RegisterNetworkUpdate(updateSystem, updateStage);
            }
        }

        /// <summary>
        /// Registers a network update system to be executed in a specific network update stage.
        /// </summary>
        /// <param name="updateSystem">The <see cref="INetworkUpdateSystem"/> implementation to register for all network updates</param>
        /// <param name="updateStage">The <see cref="NetworkUpdateStage"/> being registered for the <see cref="INetworkUpdateSystem"/> implementation</param>
        public static void RegisterNetworkUpdate(this INetworkUpdateSystem updateSystem, NetworkUpdateStage updateStage = NetworkUpdateStage.Update)
        {
            var sysSet = s_UpdateSystem_Sets[updateStage];
            if (!sysSet.Contains(updateSystem))
            {
                sysSet.Add(updateSystem);

                int setLen = sysSet.Count;
                var sysArr = s_UpdateSystem_Arrays[updateStage];
                int arrLen = sysArr.Length;

                if (setLen > arrLen)
                {
                    // double capacity
                    sysArr = s_UpdateSystem_Arrays[updateStage] = new INetworkUpdateSystem[arrLen *= 2];
                }

                sysSet.CopyTo(sysArr);

                if (setLen < arrLen)
                {
                    // null terminator
                    sysArr[setLen] = null;
                }
            }
        }

        /// <summary>
        /// Unregisters a network update system from all network update stages.
        /// </summary>
        /// <param name="updateSystem">The <see cref="INetworkUpdateSystem"/> implementation to deregister from all network updates</param>
        public static void UnregisterAllNetworkUpdates(this INetworkUpdateSystem updateSystem)
        {
            foreach (NetworkUpdateStage updateStage in GetNetworkUpdateStageEnumValues())
            {
                UnregisterNetworkUpdate(updateSystem, updateStage);
            }
        }

        /// <summary>
        /// Unregisters a network update system from a specific network update stage.
        /// </summary>
        /// <param name="updateSystem">The <see cref="INetworkUpdateSystem"/> implementation to deregister from all network updates</param>
        /// <param name="updateStage">The <see cref="NetworkUpdateStage"/> to be deregistered from the <see cref="INetworkUpdateSystem"/> implementation</param>
        public static void UnregisterNetworkUpdate(this INetworkUpdateSystem updateSystem, NetworkUpdateStage updateStage = NetworkUpdateStage.Update)
        {
            var sysSet = s_UpdateSystem_Sets[updateStage];
            if (sysSet.Contains(updateSystem))
            {
                sysSet.Remove(updateSystem);

                int setLen = sysSet.Count;
                var sysArr = s_UpdateSystem_Arrays[updateStage];
                int arrLen = sysArr.Length;

                sysSet.CopyTo(sysArr);

                if (setLen < arrLen)
                {
                    // null terminator
                    sysArr[setLen] = null;
                }
            }
        }

        /// <summary>
        /// The current network update stage being executed.
        /// </summary>
        public static NetworkUpdateStage UpdateStage;

        internal static void RunNetworkUpdateStage(NetworkUpdateStage updateStage)
        {
            UpdateStage = updateStage;

            var sysArr = s_UpdateSystem_Arrays[updateStage];
            int arrLen = sysArr.Length;
            for (int curIdx = 0; curIdx < arrLen; curIdx++)
            {
                var curSys = sysArr[curIdx];
                if (curSys == null)
                {
                    // null terminator
                    break;
                }

                curSys.NetworkUpdate(updateStage);
            }
        }

        internal struct NetworkInitialization
        {
            public static PlayerLoopSystem CreateLoopSystem()
            {
                return new PlayerLoopSystem
                {
                    type = typeof(NetworkInitialization),
                    updateDelegate = () => RunNetworkUpdateStage(NetworkUpdateStage.Initialization)
                };
            }
        }

        internal struct NetworkEarlyUpdate
        {
            public static PlayerLoopSystem CreateLoopSystem()
            {
                return new PlayerLoopSystem
                {
                    type = typeof(NetworkEarlyUpdate),
                    updateDelegate = () => RunNetworkUpdateStage(NetworkUpdateStage.EarlyUpdate)
                };
            }
        }

        internal struct NetworkFixedUpdate
        {
            public static PlayerLoopSystem CreateLoopSystem()
            {
                return new PlayerLoopSystem
                {
                    type = typeof(NetworkFixedUpdate),
                    updateDelegate = () => RunNetworkUpdateStage(NetworkUpdateStage.FixedUpdate)
                };
            }
        }

        internal struct NetworkPreUpdate
        {
            public static PlayerLoopSystem CreateLoopSystem()
            {
                return new PlayerLoopSystem
                {
                    type = typeof(NetworkPreUpdate),
                    updateDelegate = () => RunNetworkUpdateStage(NetworkUpdateStage.PreUpdate)
                };
            }
        }

        internal struct NetworkUpdate
        {
            public static PlayerLoopSystem CreateLoopSystem()
            {
                return new PlayerLoopSystem
                {
                    type = typeof(NetworkUpdate),
                    updateDelegate = () => RunNetworkUpdateStage(NetworkUpdateStage.Update)
                };
            }
        }

        internal struct NetworkPreLateUpdate
        {
            public static PlayerLoopSystem CreateLoopSystem()
            {
                return new PlayerLoopSystem
                {
                    type = typeof(NetworkPreLateUpdate),
                    updateDelegate = () => RunNetworkUpdateStage(NetworkUpdateStage.PreLateUpdate)
                };
            }
        }

        internal struct NetworkPostScriptLateUpdate
        {
            public static PlayerLoopSystem CreateLoopSystem()
            {
                return new PlayerLoopSystem
                {
                    type = typeof(NetworkPostScriptLateUpdate),
                    updateDelegate = () => RunNetworkUpdateStage(NetworkUpdateStage.PostScriptLateUpdate)
                };
            }
        }

        internal struct NetworkPostLateUpdate
        {
            public static PlayerLoopSystem CreateLoopSystem()
            {
                return new PlayerLoopSystem
                {
                    type = typeof(NetworkPostLateUpdate),
                    updateDelegate = () => RunNetworkUpdateStage(NetworkUpdateStage.PostLateUpdate)
                };
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void Initialize()
        {
            UnregisterLoopSystems();
            RegisterLoopSystems();
        }

        private enum LoopSystemPosition
        {
            After,
            Before
        }

        private static bool TryAddLoopSystem(ref PlayerLoopSystem parentLoopSystem, PlayerLoopSystem childLoopSystem, Type anchorSystemType, LoopSystemPosition loopSystemPosition)
        {
            int systemPosition = -1;
            if (anchorSystemType != null)
            {
                for (int i = 0; i < parentLoopSystem.subSystemList.Length; i++)
                {
                    var subsystem = parentLoopSystem.subSystemList[i];
                    if (subsystem.type == anchorSystemType)
                    {
                        systemPosition = loopSystemPosition == LoopSystemPosition.After ? i + 1 : i;
                        break;
                    }
                }
            }
            else
            {
                systemPosition = loopSystemPosition == LoopSystemPosition.After ? parentLoopSystem.subSystemList.Length : 0;
            }

            if (systemPosition == -1)
            {
                return false;
            }

            var newSubsystemList = new PlayerLoopSystem[parentLoopSystem.subSystemList.Length + 1];

            // begin = systemsBefore + systemsAfter
            // + systemsBefore
            if (systemPosition > 0)
            {
                Array.Copy(parentLoopSystem.subSystemList, newSubsystemList, systemPosition);
            }
            // + childSystem
            newSubsystemList[systemPosition] = childLoopSystem;
            // + systemsAfter
            if (systemPosition < parentLoopSystem.subSystemList.Length)
            {
                Array.Copy(parentLoopSystem.subSystemList, systemPosition, newSubsystemList, systemPosition + 1, parentLoopSystem.subSystemList.Length - systemPosition);
            }
            // end = systemsBefore + childSystem + systemsAfter

            parentLoopSystem.subSystemList = newSubsystemList;

            return true;
        }

        private static bool TryRemoveLoopSystem(ref PlayerLoopSystem parentLoopSystem, Type childSystemType)
        {
            int systemPosition = -1;
            for (int i = 0; i < parentLoopSystem.subSystemList.Length; i++)
            {
                var subsystem = parentLoopSystem.subSystemList[i];
                if (subsystem.type == childSystemType)
                {
                    systemPosition = i;
                    break;
                }
            }

            if (systemPosition == -1)
            {
                return false;
            }

            var newSubsystemList = new PlayerLoopSystem[parentLoopSystem.subSystemList.Length - 1];

            // begin = systemsBefore + childSystem + systemsAfter
            // + systemsBefore
            if (systemPosition > 0)
            {
                Array.Copy(parentLoopSystem.subSystemList, newSubsystemList, systemPosition);
            }
            // + systemsAfter
            if (systemPosition < parentLoopSystem.subSystemList.Length - 1)
            {
                Array.Copy(parentLoopSystem.subSystemList, systemPosition + 1, newSubsystemList, systemPosition, parentLoopSystem.subSystemList.Length - systemPosition - 1);
            }
            // end = systemsBefore + systemsAfter

            parentLoopSystem.subSystemList = newSubsystemList;

            return true;
        }

        internal static void RegisterLoopSystems()
        {
            var rootPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();

            for (int i = 0; i < rootPlayerLoop.subSystemList.Length; i++)
            {
                ref var currentSystem = ref rootPlayerLoop.subSystemList[i];

                if (currentSystem.type == typeof(Initialization))
                {
                    TryAddLoopSystem(ref currentSystem, NetworkInitialization.CreateLoopSystem(), null, LoopSystemPosition.After);
                }
                else if (currentSystem.type == typeof(EarlyUpdate))
                {
                    TryAddLoopSystem(ref currentSystem, NetworkEarlyUpdate.CreateLoopSystem(), typeof(EarlyUpdate.ScriptRunDelayedStartupFrame), LoopSystemPosition.Before);
                }
                else if (currentSystem.type == typeof(FixedUpdate))
                {
                    TryAddLoopSystem(ref currentSystem, NetworkFixedUpdate.CreateLoopSystem(), typeof(FixedUpdate.ScriptRunBehaviourFixedUpdate), LoopSystemPosition.Before);
                }
                else if (currentSystem.type == typeof(PreUpdate))
                {
                    TryAddLoopSystem(ref currentSystem, NetworkPreUpdate.CreateLoopSystem(), typeof(PreUpdate.PhysicsUpdate), LoopSystemPosition.Before);
                }
                else if (currentSystem.type == typeof(Update))
                {
                    TryAddLoopSystem(ref currentSystem, NetworkUpdate.CreateLoopSystem(), typeof(Update.ScriptRunBehaviourUpdate), LoopSystemPosition.Before);
                }
                else if (currentSystem.type == typeof(PreLateUpdate))
                {
                    TryAddLoopSystem(ref currentSystem, NetworkPreLateUpdate.CreateLoopSystem(), typeof(PreLateUpdate.ScriptRunBehaviourLateUpdate), LoopSystemPosition.Before);
                    TryAddLoopSystem(ref currentSystem, NetworkPostScriptLateUpdate.CreateLoopSystem(), typeof(PreLateUpdate.ScriptRunBehaviourLateUpdate), LoopSystemPosition.After);
                }
                else if (currentSystem.type == typeof(PostLateUpdate))
                {
                    TryAddLoopSystem(ref currentSystem, NetworkPostLateUpdate.CreateLoopSystem(), typeof(PostLateUpdate.PlayerSendFrameComplete), LoopSystemPosition.After);
                }
            }

            PlayerLoop.SetPlayerLoop(rootPlayerLoop);
        }

        internal static void UnregisterLoopSystems()
        {
            var rootPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();

            for (int i = 0; i < rootPlayerLoop.subSystemList.Length; i++)
            {
                ref var currentSystem = ref rootPlayerLoop.subSystemList[i];

                if (currentSystem.type == typeof(Initialization))
                {
                    TryRemoveLoopSystem(ref currentSystem, typeof(NetworkInitialization));
                }
                else if (currentSystem.type == typeof(EarlyUpdate))
                {
                    TryRemoveLoopSystem(ref currentSystem, typeof(NetworkEarlyUpdate));
                }
                else if (currentSystem.type == typeof(FixedUpdate))
                {
                    TryRemoveLoopSystem(ref currentSystem, typeof(NetworkFixedUpdate));
                }
                else if (currentSystem.type == typeof(PreUpdate))
                {
                    TryRemoveLoopSystem(ref currentSystem, typeof(NetworkPreUpdate));
                }
                else if (currentSystem.type == typeof(Update))
                {
                    TryRemoveLoopSystem(ref currentSystem, typeof(NetworkUpdate));
                }
                else if (currentSystem.type == typeof(PreLateUpdate))
                {
                    TryRemoveLoopSystem(ref currentSystem, typeof(NetworkPreLateUpdate));
                    TryRemoveLoopSystem(ref currentSystem, typeof(NetworkPostScriptLateUpdate));
                }
                else if (currentSystem.type == typeof(PostLateUpdate))
                {
                    TryRemoveLoopSystem(ref currentSystem, typeof(NetworkPostLateUpdate));
                }
            }

            PlayerLoop.SetPlayerLoop(rootPlayerLoop);
        }
    }
}
