#if COM_UNITY_MODULES_ANIMATION
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor.Animations;
#endif

namespace Unity.Netcode.Components
{
    internal class NetworkAnimatorStateChangeHandler : INetworkUpdateSystem
    {
        private NetworkAnimator m_NetworkAnimator;
        private bool m_IsServer;

        /// <summary>
        /// This removes sending RPCs from within RPCs when the
        /// server is forwarding updates from clients to clients
        /// As well this handles newly connected client synchronization
        /// of the existing Animator's state.
        /// </summary>
        private void FlushMessages()
        {
            foreach (var animationUpdate in m_SendAnimationUpdates)
            {

                if (m_NetworkAnimator.NetworkManager.DistributedAuthorityMode)
                {
                    m_NetworkAnimator.SendAnimStateRpc(animationUpdate.AnimationMessage);
                }
                else
                {
                    m_NetworkAnimator.SendAnimStateClientRpc(animationUpdate.AnimationMessage, animationUpdate.ClientRpcParams);
                }
            }

            m_SendAnimationUpdates.Clear();

            foreach (var sendEntry in m_SendParameterUpdates)
            {
                if (m_NetworkAnimator.NetworkManager.DistributedAuthorityMode)
                {
                    m_NetworkAnimator.SendParametersUpdateRpc(sendEntry.ParametersUpdateMessage);
                }
                else
                {
                    m_NetworkAnimator.SendParametersUpdateClientRpc(sendEntry.ParametersUpdateMessage, sendEntry.ClientRpcParams);
                }
            }
            m_SendParameterUpdates.Clear();

            foreach (var sendEntry in m_SendTriggerUpdates)
            {
                if (m_NetworkAnimator.NetworkManager.DistributedAuthorityMode)
                {
                    m_NetworkAnimator.SendAnimTriggerRpc(sendEntry.AnimationTriggerMessage);
                }
                else
                {
                    if (!sendEntry.SendToServer)
                    {
                        m_NetworkAnimator.SendAnimTriggerClientRpc(sendEntry.AnimationTriggerMessage, sendEntry.ClientRpcParams);
                    }
                    else
                    {
                        m_NetworkAnimator.SendAnimTriggerServerRpc(sendEntry.AnimationTriggerMessage);
                    }
                }
            }
            m_SendTriggerUpdates.Clear();
        }

        /// <inheritdoc />
        public void NetworkUpdate(NetworkUpdateStage updateStage)
        {
            switch (updateStage)
            {
                case NetworkUpdateStage.PreUpdate:
                    {
                        // Only the owner or the server send messages
                        if (m_NetworkAnimator.IsOwner || m_IsServer)
                        {
                            // Flush any pending messages
                            FlushMessages();
                        }

                        // Everyone applies any parameters updated
                        for (int i = 0; i < m_ProcessParameterUpdates.Count; i++)
                        {
                            var parameterUpdate = m_ProcessParameterUpdates[i];
                            m_NetworkAnimator.UpdateParameters(ref parameterUpdate);
                        }
                        m_ProcessParameterUpdates.Clear();
                        var isServerAuthority = m_NetworkAnimator.IsServerAuthoritative();

                        // owners when owner authoritative or the server when server authoritative are the only instances that
                        // checks for Animator changes
                        if ((!isServerAuthority && m_NetworkAnimator.IsOwner) || (isServerAuthority && m_NetworkAnimator.IsServer))
                        {
                            m_NetworkAnimator.CheckForAnimatorChanges();
                        }
                        break;
                    }
            }
        }

        /// <summary>
        /// A pending outgoing Animation update for (n) clients
        /// </summary>
        private struct AnimationUpdate
        {
            public ClientRpcParams ClientRpcParams;
            public NetworkAnimator.AnimationMessage AnimationMessage;
        }

        private List<AnimationUpdate> m_SendAnimationUpdates = new List<AnimationUpdate>();

        /// <summary>
        /// Invoked when a server needs to forwarding an update to the animation state
        /// </summary>
        internal void SendAnimationUpdate(NetworkAnimator.AnimationMessage animationMessage, ClientRpcParams clientRpcParams = default)
        {
            m_SendAnimationUpdates.Add(new AnimationUpdate() { ClientRpcParams = clientRpcParams, AnimationMessage = animationMessage });
        }

        private struct ParameterUpdate
        {
            public ClientRpcParams ClientRpcParams;
            public NetworkAnimator.ParametersUpdateMessage ParametersUpdateMessage;
        }

        private List<ParameterUpdate> m_SendParameterUpdates = new List<ParameterUpdate>();

        /// <summary>
        /// Invoked when a server needs to forwarding an update to the parameter state
        /// </summary>
        internal void SendParameterUpdate(NetworkAnimator.ParametersUpdateMessage parametersUpdateMessage, ClientRpcParams clientRpcParams = default)
        {
            m_SendParameterUpdates.Add(new ParameterUpdate() { ClientRpcParams = clientRpcParams, ParametersUpdateMessage = parametersUpdateMessage });
        }

        private List<NetworkAnimator.ParametersUpdateMessage> m_ProcessParameterUpdates = new List<NetworkAnimator.ParametersUpdateMessage>();
        internal void ProcessParameterUpdate(NetworkAnimator.ParametersUpdateMessage parametersUpdateMessage)
        {
            m_ProcessParameterUpdates.Add(parametersUpdateMessage);
        }

        private struct TriggerUpdate
        {
            public bool SendToServer;
            public ClientRpcParams ClientRpcParams;
            public NetworkAnimator.AnimationTriggerMessage AnimationTriggerMessage;
        }

        private List<TriggerUpdate> m_SendTriggerUpdates = new List<TriggerUpdate>();

        /// <summary>
        /// Invoked when a server needs to forward an update to a Trigger state
        /// </summary>
        internal void QueueTriggerUpdateToClient(NetworkAnimator.AnimationTriggerMessage animationTriggerMessage, ClientRpcParams clientRpcParams = default)
        {
            m_SendTriggerUpdates.Add(new TriggerUpdate() { ClientRpcParams = clientRpcParams, AnimationTriggerMessage = animationTriggerMessage });
        }

        internal void QueueTriggerUpdateToServer(NetworkAnimator.AnimationTriggerMessage animationTriggerMessage)
        {
            m_SendTriggerUpdates.Add(new TriggerUpdate() { AnimationTriggerMessage = animationTriggerMessage, SendToServer = true });
        }

        internal void DeregisterUpdate()
        {
            NetworkUpdateLoop.UnregisterNetworkUpdate(this, NetworkUpdateStage.PreUpdate);
        }

        internal NetworkAnimatorStateChangeHandler(NetworkAnimator networkAnimator)
        {
            m_NetworkAnimator = networkAnimator;
            m_IsServer = networkAnimator.NetworkManager.IsServer;
            NetworkUpdateLoop.RegisterNetworkUpdate(this, NetworkUpdateStage.PreUpdate);
        }
    }

    /// <summary>
    /// NetworkAnimator enables remote synchronization of <see cref="UnityEngine.Animator"/> state for on network objects.
    /// </summary>
    [AddComponentMenu("Netcode/Network Animator")]
    public class NetworkAnimator : NetworkBehaviour, ISerializationCallbackReceiver
    {
        [Serializable]
        internal class TransitionStateinfo
        {
            public bool IsCrossFadeExit;
            public int Layer;
            public int OriginatingState;
            public int DestinationState;
            public float TransitionDuration;
            public int TriggerNameHash;
            public int TransitionIndex;
        }

        /// <summary>
        /// Used to build the destination state to transition info table
        /// </summary>
        [HideInInspector]
        [SerializeField]
        internal List<TransitionStateinfo> TransitionStateInfoList;

        // Used to get the associated transition information required to synchronize late joining clients with transitions
        // [Layer][DestinationState][TransitionStateInfo]
        private Dictionary<int, Dictionary<int, TransitionStateinfo>> m_DestinationStateToTransitioninfo = new Dictionary<int, Dictionary<int, TransitionStateinfo>>();

        /// <summary>
        /// Builds the m_DestinationStateToTransitioninfo lookup table
        /// </summary>
        private void BuildDestinationToTransitionInfoTable()
        {
            foreach (var entry in TransitionStateInfoList)
            {
                if (!m_DestinationStateToTransitioninfo.ContainsKey(entry.Layer))
                {
                    m_DestinationStateToTransitioninfo.Add(entry.Layer, new Dictionary<int, TransitionStateinfo>());
                }
                var destinationStateTransitionInfo = m_DestinationStateToTransitioninfo[entry.Layer];
                if (!destinationStateTransitionInfo.ContainsKey(entry.DestinationState))
                {
                    destinationStateTransitionInfo.Add(entry.DestinationState, entry);
                }
            }
        }

#if UNITY_EDITOR
        private void ParseStateMachineStates(int layerIndex, ref AnimatorController animatorController, ref AnimatorStateMachine stateMachine)
        {
            for (int y = 0; y < stateMachine.states.Length; y++)
            {
                var animatorState = stateMachine.states[y].state;
                for (int z = 0; z < animatorState.transitions.Length; z++)
                {
                    var transition = animatorState.transitions[z];
                    if (transition.conditions.Length == 0 && transition.isExit)
                    {
                        // We don't need to worry about exit transitions with no conditions
                        continue;
                    }

                    foreach (var condition in transition.conditions)
                    {
                        var parameterName = condition.parameter;

                        var parameters = animatorController.parameters;
                        // Find the associated parameter for the condition
                        foreach (var parameter in parameters)
                        {
                            // Only process the associated parameter(s)
                            if (parameter.name != parameterName)
                            {
                                continue;
                            }

                            switch (parameter.type)
                            {
                                case AnimatorControllerParameterType.Trigger:
                                    {

                                        if (transition.destinationStateMachine != null)
                                        {
                                            var destinationStateMachine = transition.destinationStateMachine;
                                            ParseStateMachineStates(layerIndex, ref animatorController, ref destinationStateMachine);
                                        }
                                        else if (transition.destinationState != null)
                                        {
                                            var transitionInfo = new TransitionStateinfo()
                                            {
                                                Layer = layerIndex,
                                                OriginatingState = animatorState.nameHash,
                                                DestinationState = transition.destinationState.nameHash,
                                                TransitionDuration = transition.duration,
                                                TriggerNameHash = parameter.nameHash,
                                                TransitionIndex = z
                                            };
                                            TransitionStateInfoList.Add(transitionInfo);
                                        }
                                        else
                                        {
                                            Debug.LogError($"[{name}][Conditional Transition for {animatorState.name}] Conditional triggered transition has neither a DestinationState nor a DestinationStateMachine! This transition is not likely to synchronize properly. " +
                                                $"Please file a GitHub issue about this error with details about your Animator's setup.");
                                        }
                                        break;
                                    }
                                default:
                                    break;
                            }
                        }
                    }
                }
            }
        }
#endif

        /// <summary>
        /// Creates the TransitionStateInfoList table
        /// </summary>
        private void BuildTransitionStateInfoList()
        {
#if UNITY_EDITOR
            if (UnityEditor.EditorApplication.isUpdating || UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
            {
                return;
            }
            if (m_Animator == null)
            {
                return;
            }

            TransitionStateInfoList = new List<TransitionStateinfo>();
            var animatorController = m_Animator.runtimeAnimatorController as AnimatorController;
            if (animatorController == null)
            {
                return;
            }

            for (int x = 0; x < animatorController.layers.Length; x++)
            {
                var stateMachine = animatorController.layers[x].stateMachine;
                ParseStateMachineStates(x, ref animatorController, ref stateMachine);
            }
#endif
        }

        public void OnAfterDeserialize()
        {
            BuildDestinationToTransitionInfoTable();
        }

        public void OnBeforeSerialize()
        {
            BuildTransitionStateInfoList();
        }

        internal struct AnimationState : INetworkSerializable
        {
            // Not to be serialized, used for processing the animation state
            internal bool HasBeenProcessed;
            internal int StateHash;
            internal float NormalizedTime;
            internal int Layer;
            internal float Weight;
            internal float Duration;

            // For synchronizing transitions
            internal bool Transition;
            internal bool CrossFade;

            // Flags for bool states
            private const byte k_IsTransition = 0x01;
            private const byte k_IsCrossFade = 0x02;

            // Used to serialize the bool states
            private byte m_StateFlags;

            // The StateHash is where the transition starts
            // and the DestinationStateHash is the destination state
            internal int DestinationStateHash;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                if (serializer.IsWriter)
                {
                    var writer = serializer.GetFastBufferWriter();
                    m_StateFlags = 0x00;
                    if (Transition)
                    {
                        m_StateFlags |= k_IsTransition;
                    }
                    if (CrossFade)
                    {
                        m_StateFlags |= k_IsCrossFade;
                    }
                    serializer.SerializeValue(ref m_StateFlags);

                    BytePacker.WriteValuePacked(writer, StateHash);
                    BytePacker.WriteValuePacked(writer, Layer);
                    if (Transition)
                    {
                        BytePacker.WriteValuePacked(writer, DestinationStateHash);
                    }
                }
                else
                {
                    var reader = serializer.GetFastBufferReader();
                    serializer.SerializeValue(ref m_StateFlags);
                    Transition = (m_StateFlags & k_IsTransition) == k_IsTransition;
                    CrossFade = (m_StateFlags & k_IsCrossFade) == k_IsCrossFade;

                    ByteUnpacker.ReadValuePacked(reader, out StateHash);
                    ByteUnpacker.ReadValuePacked(reader, out Layer);
                    if (Transition)
                    {
                        ByteUnpacker.ReadValuePacked(reader, out DestinationStateHash);
                    }
                }

                serializer.SerializeValue(ref NormalizedTime);
                serializer.SerializeValue(ref Weight);

                // Cross fading includes the duration of the cross fade.
                if (CrossFade)
                {
                    serializer.SerializeValue(ref Duration);
                }
            }
        }

        internal struct AnimationMessage : INetworkSerializable
        {
            // Not to be serialized, used for processing the animation message
            internal bool HasBeenProcessed;

            // This is preallocated/populated in OnNetworkSpawn for all instances in the event ownership or
            // authority changes.  When serializing, IsDirtyCount determines how many AnimationState entries
            // should be serialized from the list.  When deserializing the list is created and populated with
            // only the number of AnimationStates received which is dictated by the deserialized IsDirtyCount.
            internal List<AnimationState> AnimationStates;

            // Used to determine how many AnimationState entries we are sending or receiving
            internal int IsDirtyCount;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                var animationState = new AnimationState();
                if (serializer.IsReader)
                {
                    AnimationStates = new List<AnimationState>();

                    serializer.SerializeValue(ref IsDirtyCount);
                    // Since we create a new AnimationMessage when deserializing
                    // we need to create new animation states for each incoming
                    // AnimationState being updated
                    for (int i = 0; i < IsDirtyCount; i++)
                    {
                        animationState = new AnimationState();
                        serializer.SerializeValue(ref animationState);
                        AnimationStates.Add(animationState);
                    }
                }
                else
                {
                    // When writing, only send the counted dirty animation states
                    serializer.SerializeValue(ref IsDirtyCount);
                    for (int i = 0; i < IsDirtyCount; i++)
                    {
                        animationState = AnimationStates[i];
                        serializer.SerializeNetworkSerializable(ref animationState);
                    }
                }
            }
        }

        internal struct ParametersUpdateMessage : INetworkSerializable
        {
            internal byte[] Parameters;
            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Parameters);
            }
        }

        internal struct AnimationTriggerMessage : INetworkSerializable
        {
            internal int Hash;
            internal bool IsTriggerSet;

            public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
            {
                serializer.SerializeValue(ref Hash);
                serializer.SerializeValue(ref IsTriggerSet);
            }
        }

        [SerializeField] private Animator m_Animator;

        public Animator Animator
        {
            get { return m_Animator; }
            set
            {
                m_Animator = value;
            }
        }

        internal bool IsServerAuthoritative()
        {
            return OnIsServerAuthoritative();
        }

        /// <summary>
        /// Override this method and return false to switch to owner authoritative mode
        /// </summary>
        /// <remarks>
        /// When using a distributed authority network topology, this will default to
        /// owner authoritative.
        /// </remarks>
        protected virtual bool OnIsServerAuthoritative()
        {
            return NetworkManager ? !NetworkManager.DistributedAuthorityMode : true;
        }

        // Animators only support up to 32 parameters
        // TODO: Look into making this a range limited property
        private const int k_MaxAnimationParams = 32;

        private int[] m_TransitionHash;
        private int[] m_AnimationHash;
        private float[] m_LayerWeights;
        private static byte[] s_EmptyArray = new byte[] { };
        private List<int> m_ParametersToUpdate;
        private List<ulong> m_ClientSendList;
        private ClientRpcParams m_ClientRpcParams;
        private AnimationMessage m_AnimationMessage;
        private NetworkAnimatorStateChangeHandler m_NetworkAnimatorStateChangeHandler;

        /// <summary>
        /// Used for integration test purposes
        /// </summary>
        internal List<AnimatorStateInfo> SynchronizationStateInfo;

        private unsafe struct AnimatorParamCache
        {
            internal int Hash;
            internal int Type;
            internal fixed byte Value[4]; // this is a max size of 4 bytes
        }

        // 128 bytes per Animator
        private FastBufferWriter m_ParameterWriter = new FastBufferWriter(k_MaxAnimationParams * sizeof(float), Allocator.Persistent);

        private NativeArray<AnimatorParamCache> m_CachedAnimatorParameters;

        // We cache these values because UnsafeUtility.EnumToInt uses direct IL that allows a non-boxing conversion
        private struct AnimationParamEnumWrapper
        {
            internal static readonly int AnimatorControllerParameterInt;
            internal static readonly int AnimatorControllerParameterFloat;
            internal static readonly int AnimatorControllerParameterBool;
            internal static readonly int AnimatorControllerParameterTriggerBool;

            static AnimationParamEnumWrapper()
            {
                AnimatorControllerParameterInt = UnsafeUtility.EnumToInt(AnimatorControllerParameterType.Int);
                AnimatorControllerParameterFloat = UnsafeUtility.EnumToInt(AnimatorControllerParameterType.Float);
                AnimatorControllerParameterBool = UnsafeUtility.EnumToInt(AnimatorControllerParameterType.Bool);
                AnimatorControllerParameterTriggerBool = UnsafeUtility.EnumToInt(AnimatorControllerParameterType.Trigger);
            }
        }

        /// <summary>
        /// Only things instantiated/created within OnNetworkSpawn should be
        /// cleaned up here.
        /// </summary>
        private void SpawnCleanup()
        {
            if (m_NetworkAnimatorStateChangeHandler != null)
            {
                m_NetworkAnimatorStateChangeHandler.DeregisterUpdate();
                m_NetworkAnimatorStateChangeHandler = null;
            }
        }

        public override void OnDestroy()
        {
            SpawnCleanup();

            if (m_CachedAnimatorParameters != null && m_CachedAnimatorParameters.IsCreated)
            {
                m_CachedAnimatorParameters.Dispose();
            }

            if (m_ParameterWriter.IsInitialized)
            {
                m_ParameterWriter.Dispose();
            }
            base.OnDestroy();
        }

        protected virtual void Awake()
        {
            int layers = m_Animator.layerCount;
            // Initializing the below arrays for everyone handles an issue
            // when running in owner authoritative mode and the owner changes.
            m_TransitionHash = new int[layers];
            m_AnimationHash = new int[layers];
            m_LayerWeights = new float[layers];

            // We initialize the m_AnimationMessage for all instances in the event that
            // ownership or authority changes during runtime.
            m_AnimationMessage = new AnimationMessage
            {
                AnimationStates = new List<AnimationState>()
            };

            // Store off our current layer weights and create our animation
            // state entries per layer.
            for (int layer = 0; layer < m_Animator.layerCount; layer++)
            {
                // We create an AnimationState per layer to preallocate the maximum
                // number of possible AnimationState changes we could send in one
                // AnimationMessage.
                m_AnimationMessage.AnimationStates.Add(new AnimationState());
                float layerWeightNow = m_Animator.GetLayerWeight(layer);
                if (layerWeightNow != m_LayerWeights[layer])
                {
                    m_LayerWeights[layer] = layerWeightNow;
                }
            }

            // Build our reference parameter values to detect when they change
            var parameters = m_Animator.parameters;
            m_CachedAnimatorParameters = new NativeArray<AnimatorParamCache>(parameters.Length, Allocator.Persistent);
            m_ParametersToUpdate = new List<int>(parameters.Length);

            // Include all parameters including any controlled by an AnimationCurve as this could change during runtime.
            // We ignore changes to any parameter controlled by an AnimationCurve when we are checking for changes in
            // the Animator's parameters.
            for (var i = 0; i < parameters.Length; i++)
            {
                var parameter = parameters[i];

                var cacheParam = new AnimatorParamCache
                {
                    Type = UnsafeUtility.EnumToInt(parameter.type),
                    Hash = parameter.nameHash
                };

                unsafe
                {
                    switch (parameter.type)
                    {
                        case AnimatorControllerParameterType.Float:
                            var value = m_Animator.GetFloat(cacheParam.Hash);
                            UnsafeUtility.WriteArrayElement(cacheParam.Value, 0, value);
                            break;
                        case AnimatorControllerParameterType.Int:
                            var valueInt = m_Animator.GetInteger(cacheParam.Hash);
                            UnsafeUtility.WriteArrayElement(cacheParam.Value, 0, valueInt);
                            break;
                        case AnimatorControllerParameterType.Bool:
                            var valueBool = m_Animator.GetBool(cacheParam.Hash);
                            UnsafeUtility.WriteArrayElement(cacheParam.Value, 0, valueBool);
                            break;
                        default:
                            break;
                    }
                }

                m_CachedAnimatorParameters[i] = cacheParam;
            }
        }

        /// <summary>
        /// Used for integration test to validate that the
        /// AnimationMessage.AnimationStates remains the same
        /// size as the layer count.
        /// </summary>
        internal AnimationMessage GetAnimationMessage()
        {
            return m_AnimationMessage;
        }

        /// <inheritdoc/>
        public override void OnNetworkSpawn()
        {
            // If there is no assigned Animator then generate a server network warning (logged locally and if applicable on the server-host side as well).
            if (m_Animator == null)
            {
                NetworkLog.LogWarningServer($"[{gameObject.name}][{nameof(NetworkAnimator)}] {nameof(Animator)} is not assigned! Animation synchronization will not work for this instance!");
            }

            m_ClientSendList = new List<ulong>(128);
            m_ClientRpcParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = m_ClientSendList
                }
            };

            // Create a handler for state changes
            m_NetworkAnimatorStateChangeHandler = new NetworkAnimatorStateChangeHandler(this);
        }

        /// <inheritdoc/>
        public override void OnNetworkDespawn()
        {
            SpawnCleanup();
        }

        /// <summary>
        /// Wries all parameter and state information needed to initially synchronize a client
        /// </summary>
        private void WriteSynchronizationData<T>(ref BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // Parameter synchronization
            {
                // We include all parameters for the initial synchronization
                m_ParametersToUpdate.Clear();
                for (int i = 0; i < m_CachedAnimatorParameters.Length; i++)
                {
                    m_ParametersToUpdate.Add(i);
                }
                // Write, apply, and serialize
                WriteParameters(ref m_ParameterWriter);
                var parametersMessage = new ParametersUpdateMessage
                {
                    Parameters = m_ParameterWriter.ToArray()
                };
                serializer.SerializeValue(ref parametersMessage);
            }

            // Animation state synchronization
            {
                // Reset the dirty count before synchronizing the newly connected client with all layers
                m_AnimationMessage.IsDirtyCount = 0;

                for (int layer = 0; layer < m_Animator.layerCount; layer++)
                {
                    var synchronizationStateInfo = m_Animator.GetCurrentAnimatorStateInfo(layer);
                    SynchronizationStateInfo?.Add(synchronizationStateInfo);
                    var stateHash = synchronizationStateInfo.fullPathHash;
                    var normalizedTime = synchronizationStateInfo.normalizedTime;
                    var isInTransition = m_Animator.IsInTransition(layer);

                    // Grab one of the available AnimationState entries so we can fill it with the current
                    // layer's animation state.
                    var animationState = m_AnimationMessage.AnimationStates[layer];

                    // Synchronizing transitions with trigger conditions for late joining clients is now
                    // handled by cross fading between the late joining client's current layer's AnimationState
                    // and the transition's destination AnimationState.
                    if (isInTransition)
                    {
                        var tt = m_Animator.GetAnimatorTransitionInfo(layer);
                        var nextState = m_Animator.GetNextAnimatorStateInfo(layer);

                        if (nextState.length > 0)
                        {
                            var nextStateTotalSpeed = nextState.speed * nextState.speedMultiplier;
                            var nextStateAdjustedLength = nextState.length * nextStateTotalSpeed;
                            // TODO: We need to get the transition curve for the target state as well as some
                            // reasonable RTT estimate in order to get a more precise normalized synchronization time
                            var transitionTime = Mathf.Min(tt.duration, tt.duration * tt.normalizedTime) * 0.5f;
                            normalizedTime = Mathf.Min(1.0f, transitionTime > 0.0f ? transitionTime / nextStateAdjustedLength : 0.0f);
                        }
                        else
                        {
                            normalizedTime = 0.0f;
                        }
                        stateHash = nextState.fullPathHash;

                        // Use the destination state to transition info lookup table to see if this is a transition we can
                        // synchronize using cross fading
                        if (m_DestinationStateToTransitioninfo.ContainsKey(layer))
                        {
                            if (m_DestinationStateToTransitioninfo[layer].ContainsKey(nextState.shortNameHash))
                            {
                                var destinationInfo = m_DestinationStateToTransitioninfo[layer][nextState.shortNameHash];
                                stateHash = destinationInfo.OriginatingState;
                                // Set the destination state to cross fade to from the originating state
                                animationState.DestinationStateHash = destinationInfo.DestinationState;
                            }
                        }
                    }

                    animationState.Transition = isInTransition;        // The only time this could be set to true
                    animationState.StateHash = stateHash;              // When a transition, this is the originating/starting state
                    animationState.NormalizedTime = normalizedTime;
                    animationState.Layer = layer;
                    animationState.Weight = m_LayerWeights[layer];

                    // Apply the changes
                    m_AnimationMessage.AnimationStates[layer] = animationState;
                }
                // Send all animation states
                m_AnimationMessage.IsDirtyCount = m_Animator.layerCount;
                m_AnimationMessage.NetworkSerialize(serializer);
            }
        }

        /// <summary>
        /// Used to synchronize newly joined clients
        /// </summary>
        protected override void OnSynchronize<T>(ref BufferSerializer<T> serializer)
        {
            if (serializer.IsWriter)
            {
                WriteSynchronizationData(ref serializer);
            }
            else
            {
                var parameters = new ParametersUpdateMessage();
                var animationMessage = new AnimationMessage();
                serializer.SerializeValue(ref parameters);
                UpdateParameters(ref parameters);
                serializer.SerializeValue(ref animationMessage);
                foreach (var animationState in animationMessage.AnimationStates)
                {
                    UpdateAnimationState(animationState);
                }
            }
        }

        /// <summary>
        /// Checks for animation state changes in:
        /// -Layer weights
        /// -Cross fades
        /// -Transitions
        /// -Layer AnimationStates
        /// </summary>
        private void CheckForStateChange(int layer)
        {
            var stateChangeDetected = false;
            var animState = m_AnimationMessage.AnimationStates[m_AnimationMessage.IsDirtyCount];
            float layerWeightNow = m_Animator.GetLayerWeight(layer);
            animState.CrossFade = false;
            animState.Transition = false;
            animState.NormalizedTime = 0.0f;
            animState.Layer = layer;
            animState.Duration = 0.0f;
            animState.Weight = m_LayerWeights[layer];
            animState.DestinationStateHash = 0;

            if (layerWeightNow != m_LayerWeights[layer])
            {
                m_LayerWeights[layer] = layerWeightNow;
                stateChangeDetected = true;
                animState.Weight = layerWeightNow;
            }

            AnimatorStateInfo st = m_Animator.GetCurrentAnimatorStateInfo(layer);

            if (m_Animator.IsInTransition(layer))
            {
                AnimatorTransitionInfo tt = m_Animator.GetAnimatorTransitionInfo(layer);
                AnimatorStateInfo nt = m_Animator.GetNextAnimatorStateInfo(layer);
                if (tt.anyState && tt.fullPathHash == 0 && m_TransitionHash[layer] != nt.fullPathHash)
                {
                    m_TransitionHash[layer] = nt.fullPathHash;
                    m_AnimationHash[layer] = 0;
                    animState.DestinationStateHash = nt.fullPathHash; // Next state is the destination state for cross fade
                    animState.CrossFade = true;
                    animState.Transition = true;
                    animState.Duration = tt.duration;
                    animState.NormalizedTime = tt.normalizedTime;
                    stateChangeDetected = true;
                    //Debug.Log($"[Cross-Fade] To-Hash: {nt.fullPathHash} | TI-Duration: ({tt.duration}) | TI-Norm: ({tt.normalizedTime}) | From-Hash: ({m_AnimationHash[layer]}) | SI-FPHash: ({st.fullPathHash}) | SI-Norm: ({st.normalizedTime})");
                }
                // If we are not transitioned into the "any state" and the animator transition isn't a full path hash (layer to layer) and our pre-built destination state to transition does not contain the
                // current layer (i.e. transitioning into a state from another layer) =or= we do contain the layer and the layer contains state to transition to is contained within our pre-built destination
                // state then we can handle this transition as a non-cross fade state transition between layers.
                // Otherwise, if we don't enter into this then this is a "trigger transition to some state that is now being transitioned back to the Idle state via trigger" or "Dual Triggers" IDLE<-->State.
                else if (!tt.anyState && tt.fullPathHash != m_TransitionHash[layer] && (!m_DestinationStateToTransitioninfo.ContainsKey(layer) ||
                    (m_DestinationStateToTransitioninfo.ContainsKey(layer) && m_DestinationStateToTransitioninfo[layer].ContainsKey(nt.fullPathHash))))
                {
                    // first time in this transition for this layer
                    m_TransitionHash[layer] = tt.fullPathHash;
                    m_AnimationHash[layer] = 0;
                    animState.StateHash = tt.fullPathHash; // Transitioning from state
                    animState.CrossFade = false;
                    animState.Transition = true;
                    animState.NormalizedTime = tt.normalizedTime;
                    if (m_DestinationStateToTransitioninfo.ContainsKey(layer) && m_DestinationStateToTransitioninfo[layer].ContainsKey(nt.fullPathHash))
                    {
                        animState.DestinationStateHash = nt.fullPathHash;
                    }
                    stateChangeDetected = true;
                    //Debug.Log($"[Transition] TI-Duration: ({tt.duration}) | TI-Norm: ({tt.normalizedTime}) | From-Hash: ({m_AnimationHash[layer]}) |SI-FPHash: ({st.fullPathHash}) | SI-Norm: ({st.normalizedTime})");
                }
            }
            else
            {
                if (st.fullPathHash != m_AnimationHash[layer])
                {
                    m_TransitionHash[layer] = 0;
                    m_AnimationHash[layer] = st.fullPathHash;
                    // first time in this animation state
                    if (m_AnimationHash[layer] != 0)
                    {
                        // came from another animation directly - from Play()
                        animState.StateHash = st.fullPathHash;
                        animState.NormalizedTime = st.normalizedTime;
                    }
                    stateChangeDetected = true;
                    //Debug.Log($"[State] From-Hash: ({m_AnimationHash[layer]}) |SI-FPHash: ({st.fullPathHash}) | SI-Norm: ({st.normalizedTime})");
                }
            }
            if (stateChangeDetected)
            {
                m_AnimationMessage.AnimationStates[m_AnimationMessage.IsDirtyCount] = animState;
                m_AnimationMessage.IsDirtyCount++;
            }
        }

        /// <summary>
        /// Checks for changes in both Animator parameters and state.
        /// </summary>
        /// <remarks>
        /// This is only invoked by clients that are the owner when not in server authoritative mode
        /// or by the server itself when in server authoritative mode.
        /// </remarks>
        internal void CheckForAnimatorChanges()
        {
            if (CheckParametersChanged())
            {
                SendParametersUpdate();
            }

            if (m_Animator.runtimeAnimatorController == null)
            {
                if (NetworkManager.LogLevel == LogLevel.Developer)
                {
                    Debug.LogError($"[{GetType().Name}] Could not find an assigned {nameof(RuntimeAnimatorController)}! Cannot check {nameof(Animator)} for changes in state!");
                }
                return;
            }

            // Reset the dirty count before checking for AnimationState updates
            m_AnimationMessage.IsDirtyCount = 0;

            // This sends updates only if a layer's state has changed
            for (int layer = 0; layer < m_Animator.layerCount; layer++)
            {
                AnimatorStateInfo st = m_Animator.GetCurrentAnimatorStateInfo(layer);
                var totalSpeed = st.speed * st.speedMultiplier;
                var adjustedNormalizedMaxTime = totalSpeed > 0.0f ? 1.0f / totalSpeed : 0.0f;
                CheckForStateChange(layer);
            }

            // Send an AnimationMessage only if there are dirty AnimationStates to send
            if (m_AnimationMessage.IsDirtyCount > 0)
            {
                if (NetworkManager.DistributedAuthorityMode)
                {
                    SendAnimStateRpc(m_AnimationMessage);
                }
                else
                if (!IsServer && IsOwner)
                {
                    SendAnimStateServerRpc(m_AnimationMessage);
                }
                else
                {
                    // Just notify all remote clients and not the local server
                    m_ClientSendList.Clear();
                    foreach (var clientId in NetworkManager.ConnectedClientsIds)
                    {
                        if (clientId == NetworkManager.LocalClientId || !NetworkObject.Observers.Contains(clientId))
                        {
                            continue;
                        }
                        m_ClientSendList.Add(clientId);
                    }
                    m_ClientRpcParams.Send.TargetClientIds = m_ClientSendList;
                    SendAnimStateClientRpc(m_AnimationMessage, m_ClientRpcParams);
                }
            }
        }

        private void SendParametersUpdate(ClientRpcParams clientRpcParams = default, bool sendDirect = false)
        {
            WriteParameters(ref m_ParameterWriter);

            var parametersMessage = new ParametersUpdateMessage
            {
                Parameters = m_ParameterWriter.ToArray()
            };
            if (NetworkManager.DistributedAuthorityMode)
            {
                if (IsOwner)
                {
                    SendParametersUpdateRpc(parametersMessage);
                }
                else
                {
                    Debug.LogError($"[{name}][Client-{NetworkManager.LocalClientId}] Attempting to send parameter updates but not the owner!");
                }
            }
            else
            {
                if (!IsServer)
                {
                    SendParametersUpdateServerRpc(parametersMessage);
                }
                else
                {
                    if (sendDirect)
                    {
                        SendParametersUpdateClientRpc(parametersMessage, clientRpcParams);
                    }
                    else
                    {
                        m_NetworkAnimatorStateChangeHandler.SendParameterUpdate(parametersMessage, clientRpcParams);
                    }
                }
            }
        }

        /// <summary>
        /// Helper function to get the cached value
        /// </summary>
        private unsafe T GetValue<T>(ref AnimatorParamCache animatorParamCache)
        {
            T currentValue;
            fixed (void* value = animatorParamCache.Value)
            {
                currentValue = UnsafeUtility.ReadArrayElement<T>(value, 0);
            }
            return currentValue;
        }

        /// <summary>
        /// Checks if any of the Animator's parameters have changed
        /// If so, it fills out m_ParametersToUpdate with the indices of the parameters
        /// that have changed.  Returns true if any parameters changed.
        /// </summary>
        private unsafe bool CheckParametersChanged()
        {
            m_ParametersToUpdate.Clear();
            for (int i = 0; i < m_CachedAnimatorParameters.Length; i++)
            {
                ref var cacheValue = ref UnsafeUtility.ArrayElementAsRef<AnimatorParamCache>(m_CachedAnimatorParameters.GetUnsafePtr(), i);

                // If a parameter gets controlled by a curve during runtime after initialization of NetworkAnimator
                // then ignore changes to this parameter. We are not removing the parameter in the event that
                // it no longer is controlled by a curve.
                if (m_Animator.IsParameterControlledByCurve(cacheValue.Hash))
                {
                    continue;
                }
                var hash = cacheValue.Hash;
                if (cacheValue.Type == AnimationParamEnumWrapper.AnimatorControllerParameterInt)
                {
                    var valueInt = m_Animator.GetInteger(hash);
                    var currentValue = GetValue<int>(ref cacheValue);
                    if (currentValue != valueInt)
                    {
                        m_ParametersToUpdate.Add(i);
                        continue;
                    }
                }
                else if (cacheValue.Type == AnimationParamEnumWrapper.AnimatorControllerParameterBool)
                {
                    var valueBool = m_Animator.GetBool(hash);
                    var currentValue = GetValue<bool>(ref cacheValue);
                    if (currentValue != valueBool)
                    {
                        m_ParametersToUpdate.Add(i);
                        continue;
                    }
                }
                else if (cacheValue.Type == AnimationParamEnumWrapper.AnimatorControllerParameterFloat)
                {
                    var valueFloat = m_Animator.GetFloat(hash);
                    var currentValue = GetValue<float>(ref cacheValue);
                    if (currentValue != valueFloat)
                    {
                        m_ParametersToUpdate.Add(i);
                        continue;
                    }
                }
            }
            return m_ParametersToUpdate.Count > 0;
        }

        /// <summary>
        /// Writes all of the Animator's parameters
        /// This uses the m_ParametersToUpdate list to write out only
        /// the parameters that have changed
        /// </summary>
        private unsafe void WriteParameters(ref FastBufferWriter writer)
        {
            writer.Seek(0);
            writer.Truncate();
            // Write how many parameter entries we are going to write
            BytePacker.WriteValuePacked(writer, (uint)m_ParametersToUpdate.Count);
            foreach (var parameterIndex in m_ParametersToUpdate)
            {
                ref var cacheValue = ref UnsafeUtility.ArrayElementAsRef<AnimatorParamCache>(m_CachedAnimatorParameters.GetUnsafePtr(), parameterIndex);
                var hash = cacheValue.Hash;
                BytePacker.WriteValuePacked(writer, (uint)parameterIndex);
                if (cacheValue.Type == AnimationParamEnumWrapper.AnimatorControllerParameterInt)
                {
                    var valueInt = m_Animator.GetInteger(hash);
                    fixed (void* value = cacheValue.Value)
                    {
                        UnsafeUtility.WriteArrayElement(value, 0, valueInt);
                        BytePacker.WriteValuePacked(writer, (uint)valueInt);
                    }
                }
                else // Note: Triggers are treated like boolean values
                if (cacheValue.Type == AnimationParamEnumWrapper.AnimatorControllerParameterBool)
                {
                    var valueBool = m_Animator.GetBool(hash);
                    fixed (void* value = cacheValue.Value)
                    {
                        UnsafeUtility.WriteArrayElement(value, 0, valueBool);
                        BytePacker.WriteValuePacked(writer, valueBool);
                    }
                }
                else if (cacheValue.Type == AnimationParamEnumWrapper.AnimatorControllerParameterFloat)
                {
                    var valueFloat = m_Animator.GetFloat(hash);
                    fixed (void* value = cacheValue.Value)
                    {
                        UnsafeUtility.WriteArrayElement(value, 0, valueFloat);
                        BytePacker.WriteValuePacked(writer, valueFloat);
                    }
                }
            }
        }

        /// <summary>
        /// Reads all parameters that were updated and applies the values
        /// </summary>
        private unsafe void ReadParameters(FastBufferReader reader)
        {
            ByteUnpacker.ReadValuePacked(reader, out uint totalParametersToRead);
            var totalParametersRead = 0;

            while (totalParametersRead < totalParametersToRead)
            {
                ByteUnpacker.ReadValuePacked(reader, out uint parameterIndex);
                ref var cacheValue = ref UnsafeUtility.ArrayElementAsRef<AnimatorParamCache>(m_CachedAnimatorParameters.GetUnsafePtr(), (int)parameterIndex);
                var hash = cacheValue.Hash;
                if (cacheValue.Type == AnimationParamEnumWrapper.AnimatorControllerParameterInt)
                {
                    ByteUnpacker.ReadValuePacked(reader, out uint newValue);
                    m_Animator.SetInteger(hash, (int)newValue);
                    fixed (void* value = cacheValue.Value)
                    {
                        UnsafeUtility.WriteArrayElement(value, 0, newValue);
                    }
                }
                else if (cacheValue.Type == AnimationParamEnumWrapper.AnimatorControllerParameterBool)
                {
                    ByteUnpacker.ReadValuePacked(reader, out bool newBoolValue);
                    m_Animator.SetBool(hash, newBoolValue);
                    fixed (void* value = cacheValue.Value)
                    {
                        UnsafeUtility.WriteArrayElement(value, 0, newBoolValue);
                    }
                }
                else if (cacheValue.Type == AnimationParamEnumWrapper.AnimatorControllerParameterFloat)
                {
                    ByteUnpacker.ReadValuePacked(reader, out float newFloatValue);
                    m_Animator.SetFloat(hash, newFloatValue);
                    fixed (void* value = cacheValue.Value)
                    {
                        UnsafeUtility.WriteArrayElement(value, 0, newFloatValue);
                    }
                }
                totalParametersRead++;
            }
        }

        /// <summary>
        /// Applies the ParametersUpdateMessage state to the Animator
        /// </summary>
        internal unsafe void UpdateParameters(ref ParametersUpdateMessage parametersUpdate)
        {
            if (parametersUpdate.Parameters != null && parametersUpdate.Parameters.Length != 0)
            {
                // We use a fixed value here to avoid the copy of data from the byte buffer since we own the data
                fixed (byte* parameters = parametersUpdate.Parameters)
                {
                    var reader = new FastBufferReader(parameters, Allocator.None, parametersUpdate.Parameters.Length);
                    ReadParameters(reader);
                }
            }
        }

        /// <summary>
        /// Applies the AnimationState state to the Animator
        /// </summary>
        internal void UpdateAnimationState(AnimationState animationState)
        {
            // Handle updating layer weights first.
            if (animationState.Layer < m_LayerWeights.Length)
            {
                if (m_LayerWeights[animationState.Layer] != animationState.Weight)
                {
                    m_Animator.SetLayerWeight(animationState.Layer, animationState.Weight);
                    m_LayerWeights[animationState.Layer] = animationState.Weight;
                }
            }

            // If there is no state transition then return
            if (animationState.StateHash == 0 && !animationState.Transition)
            {
                return;
            }

            var currentState = m_Animator.GetCurrentAnimatorStateInfo(animationState.Layer);
            // If it is a transition, then we are synchronizing transitions in progress when a client late joins
            if (animationState.Transition && !animationState.CrossFade)
            {
                // We should have all valid entries for any animation state transition update
                // Verify the AnimationState's assigned Layer exists
                if (m_DestinationStateToTransitioninfo.ContainsKey(animationState.Layer))
                {
                    // Verify the inner-table has the destination AnimationState name hash
                    if (m_DestinationStateToTransitioninfo[animationState.Layer].ContainsKey(animationState.DestinationStateHash))
                    {
                        // Make sure we are on the originating/starting state we are going to cross fade into
                        if (currentState.shortNameHash == animationState.StateHash)
                        {
                            // Get the transition state information
                            var transitionStateInfo = m_DestinationStateToTransitioninfo[animationState.Layer][animationState.DestinationStateHash];

                            // Cross fade from the current to the destination state for the transitions duration while starting at the server's current normalized time of the transition
                            m_Animator.CrossFade(transitionStateInfo.DestinationState, transitionStateInfo.TransitionDuration, transitionStateInfo.Layer, 0.0f, animationState.NormalizedTime);
                        }
                        else if (NetworkManager.LogLevel == LogLevel.Developer)
                        {
                            NetworkLog.LogWarning($"Current State Hash ({currentState.fullPathHash}) != AnimationState.StateHash ({animationState.StateHash})");
                        }
                    }
                    else if (NetworkManager.LogLevel == LogLevel.Developer)
                    {
                        NetworkLog.LogError($"[DestinationState To Transition Info] Layer ({animationState.Layer}) sub-table does not contain destination state ({animationState.DestinationStateHash})!");
                    }
                }
                else if (NetworkManager.LogLevel == LogLevel.Developer)
                {
                    NetworkLog.LogError($"[DestinationState To Transition Info] Layer ({animationState.Layer}) does not exist!");
                }
            }
            else if (animationState.Transition && animationState.CrossFade)
            {
                m_Animator.CrossFade(animationState.DestinationStateHash, animationState.Duration, animationState.Layer, animationState.NormalizedTime);
            }
            else
            {
                // Make sure we are not just updating the weight of a layer.
                if (currentState.fullPathHash != animationState.StateHash && m_Animator.HasState(animationState.Layer, animationState.StateHash))
                {
                    m_Animator.Play(animationState.StateHash, animationState.Layer, animationState.NormalizedTime);
                }
            }
        }

        /// <summary>
        /// Server-side animator parameter update request
        /// The server sets its local parameters and then forwards the message to the remaining clients
        /// </summary>
        [ServerRpc]
        private unsafe void SendParametersUpdateServerRpc(ParametersUpdateMessage parametersUpdate, ServerRpcParams serverRpcParams = default)
        {
            if (IsServerAuthoritative())
            {
                m_NetworkAnimatorStateChangeHandler.SendParameterUpdate(parametersUpdate);
            }
            else
            {
                if (serverRpcParams.Receive.SenderClientId != OwnerClientId)
                {
                    return;
                }
                UpdateParameters(ref parametersUpdate);
                if (NetworkManager.ConnectedClientsIds.Count > (IsHost ? 2 : 1))
                {
                    m_ClientSendList.Clear();
                    foreach (var clientId in NetworkManager.ConnectedClientsIds)
                    {
                        if (clientId == serverRpcParams.Receive.SenderClientId || clientId == NetworkManager.ServerClientId || !NetworkObject.Observers.Contains(clientId))
                        {
                            continue;
                        }
                        m_ClientSendList.Add(clientId);
                    }

                    m_ClientRpcParams.Send.TargetClientIds = m_ClientSendList;
                    m_NetworkAnimatorStateChangeHandler.SendParameterUpdate(parametersUpdate, m_ClientRpcParams);
                }
            }
        }

        /// <summary>
        /// Distributed Authority: Updates the client's animator's parameters
        /// </summary>
        [Rpc(SendTo.NotAuthority)]
        internal void SendParametersUpdateRpc(ParametersUpdateMessage parametersUpdate)
        {
            m_NetworkAnimatorStateChangeHandler.ProcessParameterUpdate(parametersUpdate);
        }

        /// <summary>
        /// Client-Server: Updates the client's animator's parameters
        /// </summary>
        [ClientRpc]
        internal void SendParametersUpdateClientRpc(ParametersUpdateMessage parametersUpdate, ClientRpcParams clientRpcParams = default)
        {
            var isServerAuthoritative = IsServerAuthoritative();
            if (!isServerAuthoritative && !IsOwner || isServerAuthoritative)
            {
                m_NetworkAnimatorStateChangeHandler.ProcessParameterUpdate(parametersUpdate);
            }
        }

        /// <summary>
        /// Server-side animation state update request
        /// The server sets its local state and then forwards the message to the remaining clients
        /// </summary>
        [ServerRpc]
        private void SendAnimStateServerRpc(AnimationMessage animationMessage, ServerRpcParams serverRpcParams = default)
        {
            if (IsServerAuthoritative())
            {
                m_NetworkAnimatorStateChangeHandler.SendAnimationUpdate(animationMessage);
            }
            else
            {
                if (serverRpcParams.Receive.SenderClientId != OwnerClientId)
                {
                    return;
                }

                foreach (var animationState in animationMessage.AnimationStates)
                {
                    UpdateAnimationState(animationState);
                }

                if (NetworkManager.ConnectedClientsIds.Count > (IsHost ? 2 : 1))
                {
                    m_ClientSendList.Clear();
                    foreach (var clientId in NetworkManager.ConnectedClientsIds)
                    {
                        if (clientId == serverRpcParams.Receive.SenderClientId || clientId == NetworkManager.ServerClientId || !NetworkObject.Observers.Contains(clientId))
                        {
                            continue;
                        }
                        m_ClientSendList.Add(clientId);
                    }
                    m_ClientRpcParams.Send.TargetClientIds = m_ClientSendList;
                    m_NetworkAnimatorStateChangeHandler.SendAnimationUpdate(animationMessage, m_ClientRpcParams);
                }
            }
        }

        /// <summary>
        /// Client-Server: Internally-called RPC client-side receiving function to update animation states
        /// </summary>
        [ClientRpc]
        internal void SendAnimStateClientRpc(AnimationMessage animationMessage, ClientRpcParams clientRpcParams = default)
        {
            ProcessAnimStates(animationMessage);
        }

        /// <summary>
        /// Distributed Authority: Internally-called RPC non-authority receiving function to update animation states
        /// </summary>
        [Rpc(SendTo.NotAuthority)]
        internal void SendAnimStateRpc(AnimationMessage animationMessage)
        {
            ProcessAnimStates(animationMessage);
        }

        private void ProcessAnimStates(AnimationMessage animationMessage)
        {
            if (HasAuthority)
            {
                if (NetworkManager.LogLevel == LogLevel.Developer)
                {
                    var hostOrOwner = NetworkManager.DistributedAuthorityMode ? "Owner" : "Host";
                    var clientServerOrDAMode = NetworkManager.DistributedAuthorityMode ? "distributed authority" : "client-server";
                    NetworkLog.LogWarning($"Detected the {hostOrOwner} is sending itself animation updates in {clientServerOrDAMode} mode! Please report this issue.");
                }
                return;
            }

            foreach (var animationState in animationMessage.AnimationStates)
            {
                UpdateAnimationState(animationState);
            }
        }



        /// <summary>
        /// Server-side trigger state update request
        /// The server sets its local state and then forwards the message to the remaining clients
        /// </summary>
        [ServerRpc]
        internal void SendAnimTriggerServerRpc(AnimationTriggerMessage animationTriggerMessage, ServerRpcParams serverRpcParams = default)
        {
            // Ignore if a non-owner sent this.
            if (serverRpcParams.Receive.SenderClientId != OwnerClientId)
            {
                if (NetworkManager.LogLevel == LogLevel.Developer)
                {
                    NetworkLog.LogWarning($"[Owner Authoritative] Detected the a non-authoritative client is sending the server animation trigger updates. If you recently changed ownership of the {name} object, then this could be the reason.");
                }
                return;
            }

            // set the trigger locally on the server
            InternalSetTrigger(animationTriggerMessage.Hash, animationTriggerMessage.IsTriggerSet);

            m_ClientSendList.Clear();
            foreach (var clientId in NetworkManager.ConnectedClientsIds)
            {
                if (clientId == NetworkManager.ServerClientId || !NetworkObject.Observers.Contains(clientId))
                {
                    continue;
                }
                m_ClientSendList.Add(clientId);
            }
            if (IsServerAuthoritative())
            {
                m_NetworkAnimatorStateChangeHandler.QueueTriggerUpdateToClient(animationTriggerMessage, m_ClientRpcParams);
            }
            else if (NetworkManager.ConnectedClientsIds.Count > (IsHost ? 2 : 1))
            {
                m_ClientSendList.Remove(serverRpcParams.Receive.SenderClientId);
                m_NetworkAnimatorStateChangeHandler.QueueTriggerUpdateToClient(animationTriggerMessage, m_ClientRpcParams);
            }
        }

        /// <summary>
        /// See above <see cref="m_LastTriggerHash"/>
        /// </summary>
        private void InternalSetTrigger(int hash, bool isSet = true)
        {
            m_Animator.SetBool(hash, isSet);
        }

        /// <summary>
        /// Distributed Authority: Internally-called RPC client receiving function to update a trigger when the server wants to forward
        ///   a trigger for a client to play / reset
        /// </summary>
        /// <param name="animationTriggerMessage">the payload containing the trigger data to apply</param>
        [Rpc(SendTo.NotAuthority)]
        internal void SendAnimTriggerRpc(AnimationTriggerMessage animationTriggerMessage)
        {
            InternalSetTrigger(animationTriggerMessage.Hash, animationTriggerMessage.IsTriggerSet);
        }

        /// <summary>
        /// Client Server: Internally-called RPC client receiving function to update a trigger when the server wants to forward
        ///   a trigger for a client to play / reset
        /// </summary>
        /// <param name="animationTriggerMessage">the payload containing the trigger data to apply</param>
        /// <param name="clientRpcParams">unused</param>
        [ClientRpc]
        internal void SendAnimTriggerClientRpc(AnimationTriggerMessage animationTriggerMessage, ClientRpcParams clientRpcParams = default)
        {
            InternalSetTrigger(animationTriggerMessage.Hash, animationTriggerMessage.IsTriggerSet);
        }

        /// <summary>
        /// Sets the trigger for the associated animation
        /// </summary>
        /// <param name="triggerName">The string name of the trigger to activate</param>
        public void SetTrigger(string triggerName)
        {
            SetTrigger(Animator.StringToHash(triggerName));
        }

        /// <inheritdoc cref="SetTrigger(string)" />
        /// <param name="hash">The hash for the trigger to activate</param>
        /// <param name="setTrigger">sets (true) or resets (false) the trigger. The default is to set it (true).</param>
        public void SetTrigger(int hash, bool setTrigger = true)
        {
            if (!IsSpawned)
            {
                NetworkLog.LogError($"[{gameObject.name}] Cannot set a synchronized trigger when the {nameof(NetworkObject)} is not spawned!");
                return;
            }

            // MTT-3564:
            // After fixing the issue with trigger controlled Transitions being synchronized twice,
            // it exposed additional issues with this logic.  Now, either the owner or the server can
            // update triggers. Since server-side RPCs are immediately invoked, for a host a trigger
            // will happen when SendAnimTriggerClientRpc is called.  For a client owner, we call the
            // SendAnimTriggerServerRpc and then trigger locally when running in owner authority mode.
            var animTriggerMessage = new AnimationTriggerMessage() { Hash = hash, IsTriggerSet = setTrigger };
            if (NetworkManager.DistributedAuthorityMode && HasAuthority)
            {
                m_NetworkAnimatorStateChangeHandler.QueueTriggerUpdateToClient(animTriggerMessage);
                InternalSetTrigger(hash, setTrigger);
            }
            else if (!NetworkManager.DistributedAuthorityMode && (IsOwner || IsServer))
            {
                if (IsServer)
                {
                    /// <see cref="UpdatePendingTriggerStates"/> as to why we queue
                    m_NetworkAnimatorStateChangeHandler.QueueTriggerUpdateToClient(animTriggerMessage);
                    if (!IsHost)
                    {
                        InternalSetTrigger(hash, setTrigger);
                    }
                }
                else
                {
                    /// <see cref="UpdatePendingTriggerStates"/> as to why we queue
                    m_NetworkAnimatorStateChangeHandler.QueueTriggerUpdateToServer(animTriggerMessage);
                    if (!IsServerAuthoritative())
                    {
                        InternalSetTrigger(hash, setTrigger);
                    }
                }
            }
        }

        /// <summary>
        /// Resets the trigger for the associated animation.  See <see cref="SetTrigger(string)">SetTrigger</see> for more on how triggers are special
        /// </summary>
        /// <param name="triggerName">The string name of the trigger to reset</param>
        public void ResetTrigger(string triggerName)
        {
            ResetTrigger(Animator.StringToHash(triggerName));
        }

        /// <inheritdoc cref="ResetTrigger(string)" path="summary" />
        /// <param name="hash">The hash for the trigger to activate</param>
        public void ResetTrigger(int hash)
        {
            SetTrigger(hash, false);
        }
    }
}
#endif // COM_UNITY_MODULES_ANIMATION
