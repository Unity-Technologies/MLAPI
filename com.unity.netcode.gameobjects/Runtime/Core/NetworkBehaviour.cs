using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Reflection;
using Unity.Collections;

namespace Unity.Netcode
{
    /// <summary>
    /// The base class to override to write network code. Inherits MonoBehaviour
    /// </summary>
    public abstract class NetworkBehaviour : MonoBehaviour
    {
#pragma warning disable IDE1006 // disable naming rule violation check
        // RuntimeAccessModifiersILPP will make this `protected`
        internal enum __RpcExecStage
#pragma warning restore IDE1006 // restore naming rule violation check
        {
            None = 0,
            Server = 1,
            Client = 2
        }

#pragma warning disable IDE1006 // disable naming rule violation check
        // NetworkBehaviourILPP will override this in derived classes to return the name of the concrete type
        internal virtual string __getTypeName() => nameof(NetworkBehaviour);
#pragma warning restore IDE1006 // restore naming rule violation check

#pragma warning disable 414 // disable assigned but its value is never used
#pragma warning disable IDE1006 // disable naming rule violation check
        [NonSerialized]
        // RuntimeAccessModifiersILPP will make this `protected`
        internal __RpcExecStage __rpc_exec_stage = __RpcExecStage.None;
#pragma warning restore 414 // restore assigned but its value is never used
#pragma warning restore IDE1006 // restore naming rule violation check

#pragma warning disable 414 // disable assigned but its value is never used
#pragma warning disable IDE1006 // disable naming rule violation check
        // RuntimeAccessModifiersILPP will make this `protected`
        internal void __sendServerRpc(FastBufferWriter writer, uint rpcMethodId, ServerRpcParams rpcParams, RpcDelivery delivery)
#pragma warning restore 414 // restore assigned but its value is never used
#pragma warning restore IDE1006 // restore naming rule violation check
        {
            NetworkDelivery networkDelivery = NetworkDelivery.Reliable;
            switch (delivery)
            {
                case RpcDelivery.Reliable:
                    networkDelivery = NetworkDelivery.ReliableFragmentedSequenced;
                    break;
                case RpcDelivery.Unreliable:
                    if (writer.Length > MessagingSystem.NON_FRAGMENTED_MESSAGE_MAX_SIZE - sizeof(RpcMessage.RpcType) - sizeof(ulong) - sizeof(uint) - sizeof(ushort))
                    {
                        throw new OverflowException("RPC parameters are too large for unreliable delivery.");
                    }
                    networkDelivery = NetworkDelivery.Unreliable;
                    break;
            }

            var message = new RpcMessage
            {
                Header = new RpcMessage.HeaderData
                {
                    Type = RpcMessage.RpcType.Server,
                    NetworkObjectId = NetworkObjectId,
                    NetworkBehaviourId = NetworkBehaviourId,
                    NetworkMethodId = rpcMethodId
                },
                RpcData = writer
            };

            // If we are a server/host then we just no op and send to ourself
            if (IsHost || IsServer)
            {
                var tempBuffer = new FastBufferReader(writer, Allocator.Temp);
                message.Handle(tempBuffer, NetworkManager, NetworkBehaviourId);
                tempBuffer.Dispose();

                return;
            }

            var rpcMessageSize = NetworkManager.SendMessage(message, networkDelivery, NetworkManager.ServerClientId, true);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (NetworkManager.__rpc_name_table.TryGetValue(rpcMethodId, out var rpcMethodName))
            {
                NetworkManager.NetworkMetrics.TrackRpcSent(
                    NetworkManager.ServerClientId,
                    NetworkObjectId,
                    rpcMethodName,
                    __getTypeName(),
                    rpcMessageSize);
            }
#endif
        }

#pragma warning disable 414 // disable assigned but its value is never used
#pragma warning disable IDE1006 // disable naming rule violation check
        // RuntimeAccessModifiersILPP will make this `protected`
        internal unsafe void __sendClientRpc(FastBufferWriter writer, uint rpcMethodId, ClientRpcParams rpcParams, RpcDelivery delivery)
#pragma warning disable 414 // disable assigned but its value is never used
#pragma warning disable IDE1006 // disable naming rule violation check
        {
            NetworkDelivery networkDelivery = NetworkDelivery.Reliable;
            switch (delivery)
            {
                case RpcDelivery.Reliable:
                    networkDelivery = NetworkDelivery.ReliableFragmentedSequenced;
                    break;
                case RpcDelivery.Unreliable:
                    if (writer.Length > MessagingSystem.NON_FRAGMENTED_MESSAGE_MAX_SIZE - sizeof(RpcMessage.RpcType) - sizeof(ulong) - sizeof(uint) - sizeof(ushort))
                    {
                        throw new OverflowException("RPC parameters are too large for unreliable delivery.");
                    }
                    networkDelivery = NetworkDelivery.Unreliable;
                    break;
            }

            var message = new RpcMessage
            {
                Header = new RpcMessage.HeaderData
                {
                    Type = RpcMessage.RpcType.Client,
                    NetworkObjectId = NetworkObjectId,
                    NetworkBehaviourId = NetworkBehaviourId,
                    NetworkMethodId = rpcMethodId
                },
                RpcData = writer
            };
            int messageSize;

            if (rpcParams.Send.TargetClientIds != null)
            {
                messageSize = NetworkManager.SendMessage(message, networkDelivery, in rpcParams.Send.TargetClientIds, true);
            }
            else if (rpcParams.Send.TargetClientIdsNativeArray != null)
            {
                messageSize = NetworkManager.SendMessage(message, networkDelivery, rpcParams.Send.TargetClientIdsNativeArray.Value);
            }
            else
            {
                messageSize = NetworkManager.SendMessage(message, networkDelivery, NetworkManager.ConnectedClientsIds, true);
            }

#if DEVELOPMENT_BUILD || UNITY_EDITOR
            if (NetworkManager.__rpc_name_table.TryGetValue(rpcMethodId, out var rpcMethodName))
            {
                foreach (var client in NetworkManager.ConnectedClients)
                {
                    var bytesReported = NetworkManager.LocalClientId == client.Key
                        ? 0
                        : messageSize;
                    NetworkManager.NetworkMetrics.TrackRpcSent(
                        client.Key,
                        NetworkObjectId,
                        rpcMethodName,
                        __getTypeName(),
                        bytesReported);
                }
            }
#endif
        }

        /// <summary>
        /// Gets the NetworkManager that owns this NetworkBehaviour instance
        /// </summary>
        public NetworkManager NetworkManager => NetworkObject.NetworkManager;

        /// <summary>
        /// Gets if the object is the the personal clients player object
        /// </summary>
        public bool IsLocalPlayer => NetworkObject.IsLocalPlayer;

        /// <summary>
        /// Gets if the object is owned by the local player or if the object is the local player object
        /// </summary>
        public bool IsOwner => NetworkObject.IsOwner;

        /// <summary>
        /// Gets if we are executing as server
        /// </summary>
        protected bool IsServer => IsRunning && NetworkManager.IsServer;

        /// <summary>
        /// Gets if we are executing as client
        /// </summary>
        protected bool IsClient => IsRunning && NetworkManager.IsClient;

        /// <summary>
        /// Gets if we are executing as Host, I.E Server and Client
        /// </summary>
        protected bool IsHost => IsRunning && NetworkManager.IsHost;

        private bool IsRunning => NetworkManager != null && NetworkManager.IsListening;

        /// <summary>
        /// Gets Whether or not the object has a owner
        /// </summary>
        public bool IsOwnedByServer => NetworkObject.IsOwnedByServer;

        /// <summary>
        /// Used to determine if it is safe to access NetworkObject and NetworkManager from within a NetworkBehaviour component
        /// Primarily useful when checking NetworkObject/NetworkManager properties within FixedUpate
        /// </summary>
        public bool IsSpawned => HasNetworkObject ? NetworkObject.IsSpawned : false;

        /// <summary>
        /// Gets the NetworkObject that owns this NetworkBehaviour instance
        /// </summary>
        public NetworkObject NetworkObject
        {
            get
            {
                if (m_NetworkObject == null)
                {
                    m_NetworkObject = GetComponentInParent<NetworkObject>();
                }

                if (m_NetworkObject == null && NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                {
                    NetworkLog.LogWarning($"Could not get {nameof(NetworkObject)} for the {nameof(NetworkBehaviour)}. Are you missing a {nameof(NetworkObject)} component?");
                }

                return m_NetworkObject;
            }
        }

        /// <summary>
        /// Gets whether or not this NetworkBehaviour instance has a NetworkObject owner.
        /// </summary>
        public bool HasNetworkObject => NetworkObject != null;

        private NetworkObject m_NetworkObject = null;

        /// <summary>
        /// Gets the NetworkId of the NetworkObject that owns this NetworkBehaviour
        /// </summary>
        public ulong NetworkObjectId => NetworkObject.NetworkObjectId;

        /// <summary>
        /// Gets NetworkId for this NetworkBehaviour from the owner NetworkObject
        /// </summary>
        public ushort NetworkBehaviourId => NetworkObject.GetNetworkBehaviourOrderIndex(this);

        /// <summary>
        /// Internally caches the Id of this behaviour in a NetworkObject. Makes look-up faster
        /// </summary>
        internal ushort NetworkBehaviourIdCache = 0;

        /// <summary>
        /// Returns a the NetworkBehaviour with a given BehaviourId for the current NetworkObject
        /// </summary>
        /// <param name="behaviourId">The behaviourId to return</param>
        /// <returns>Returns NetworkBehaviour with given behaviourId</returns>
        protected NetworkBehaviour GetNetworkBehaviour(ushort behaviourId)
        {
            return NetworkObject.GetNetworkBehaviourAtOrderIndex(behaviourId);
        }

        /// <summary>
        /// Gets the ClientId that owns the NetworkObject
        /// </summary>
        public ulong OwnerClientId => NetworkObject.OwnerClientId;

        /// <summary>
        /// Gets called when the <see cref="NetworkObject"/> gets spawned, message handlers are ready to be registered and the network is setup.
        /// </summary>
        public virtual void OnNetworkSpawn() { }

        /// <summary>
        /// Gets called when the <see cref="NetworkObject"/> gets despawned. Is called both on the server and clients.
        /// </summary>
        public virtual void OnNetworkDespawn() { }

        internal void InternalOnNetworkSpawn()
        {
            InitializeVariables();
        }

        internal void InternalOnNetworkDespawn()
        {

        }

        /// <summary>
        /// Gets called when the local client gains ownership of this object
        /// </summary>
        public virtual void OnGainedOwnership() { }

        /// <summary>
        /// Gets called when we loose ownership of this object
        /// </summary>
        public virtual void OnLostOwnership() { }

        /// <summary>
        /// Gets called when the parent NetworkObject of this NetworkBehaviour's NetworkObject has changed
        /// </summary>
        public virtual void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject) { }

        private bool m_VarInit = false;

        private readonly List<HashSet<int>> m_DeliveryMappedNetworkVariableIndices = new List<HashSet<int>>();
        private readonly List<NetworkDelivery> m_DeliveryTypesForNetworkVariableGroups = new List<NetworkDelivery>();
        internal readonly List<NetworkVariableBase> NetworkVariableFields = new List<NetworkVariableBase>();

        private static Dictionary<Type, FieldInfo[]> s_FieldTypes = new Dictionary<Type, FieldInfo[]>();

        private static FieldInfo[] GetFieldInfoForType(Type type)
        {
            if (!s_FieldTypes.ContainsKey(type))
            {
                s_FieldTypes.Add(type, GetFieldInfoForTypeRecursive(type));
            }

            return s_FieldTypes[type];
        }

        private static FieldInfo[] GetFieldInfoForTypeRecursive(Type type, List<FieldInfo> list = null)
        {
            if (list == null)
            {
                list = new List<FieldInfo>();
                list.AddRange(type.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance));
            }
            else
            {
                list.AddRange(type.GetFields(BindingFlags.NonPublic | BindingFlags.Instance));
            }

            if (type.BaseType != null && type.BaseType != typeof(NetworkBehaviour))
            {
                return GetFieldInfoForTypeRecursive(type.BaseType, list);
            }

            return list.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
        }

        internal void InitializeVariables()
        {
            if (m_VarInit)
            {
                return;
            }

            m_VarInit = true;

            FieldInfo[] sortedFields = GetFieldInfoForType(GetType());

            for (int i = 0; i < sortedFields.Length; i++)
            {
                Type fieldType = sortedFields[i].FieldType;

                if (fieldType.IsSubclassOf(typeof(NetworkVariableBase)))
                {
                    var instance = (NetworkVariableBase)sortedFields[i].GetValue(this);

                    if (instance == null)
                    {
                        instance = (NetworkVariableBase)Activator.CreateInstance(fieldType, true);
                        sortedFields[i].SetValue(this, instance);
                    }

                    instance.Initialize(this);

                    var instanceNameProperty = fieldType.GetProperty(nameof(NetworkVariableBase.Name));
                    var sanitizedVariableName = sortedFields[i].Name.Replace("<", string.Empty).Replace(">k__BackingField", string.Empty);
                    instanceNameProperty?.SetValue(instance, sanitizedVariableName);

                    NetworkVariableFields.Add(instance);
                }
            }

            {
                // Create index map for delivery types
                var firstLevelIndex = new Dictionary<NetworkDelivery, int>();
                int secondLevelCounter = 0;

                for (int i = 0; i < NetworkVariableFields.Count; i++)
                {
                    var networkDelivery = NetworkVariableBase.Delivery;
                    if (!firstLevelIndex.ContainsKey(networkDelivery))
                    {
                        firstLevelIndex.Add(networkDelivery, secondLevelCounter);
                        m_DeliveryTypesForNetworkVariableGroups.Add(networkDelivery);
                        secondLevelCounter++;
                    }

                    if (firstLevelIndex[networkDelivery] >= m_DeliveryMappedNetworkVariableIndices.Count)
                    {
                        m_DeliveryMappedNetworkVariableIndices.Add(new HashSet<int>());
                    }

                    m_DeliveryMappedNetworkVariableIndices[firstLevelIndex[networkDelivery]].Add(i);
                }
            }
        }

        internal void PreNetworkVariableWrite()
        {
            // reset our "which variables got written" data
            NetworkVariableIndexesToReset.Clear();
            NetworkVariableIndexesToResetSet.Clear();
        }

        internal void PostNetworkVariableWrite()
        {
            // mark any variables we wrote as no longer dirty
            for (int i = 0; i < NetworkVariableIndexesToReset.Count; i++)
            {
                NetworkVariableFields[NetworkVariableIndexesToReset[i]].ResetDirty();
            }
        }

        internal void VariableUpdate(ulong clientId)
        {
            if (!m_VarInit)
            {
                InitializeVariables();
            }

            PreNetworkVariableWrite();
            NetworkVariableUpdate(clientId, NetworkBehaviourId);
        }

        internal readonly List<int> NetworkVariableIndexesToReset = new List<int>();
        internal readonly HashSet<int> NetworkVariableIndexesToResetSet = new HashSet<int>();

        private void NetworkVariableUpdate(ulong clientId, int behaviourIndex)
        {
            if (!CouldHaveDirtyNetworkVariables())
            {
                return;
            }

            if (NetworkManager.NetworkConfig.UseSnapshotDelta)
            {
                for (int k = 0; k < NetworkVariableFields.Count; k++)
                {
                    NetworkManager.SnapshotSystem.Store(NetworkObjectId, behaviourIndex, k, NetworkVariableFields[k]);
                }
            }

            if (!NetworkManager.NetworkConfig.UseSnapshotDelta)
            {
                for (int j = 0; j < m_DeliveryMappedNetworkVariableIndices.Count; j++)
                {
                    var shouldSend = false;
                    for (int k = 0; k < NetworkVariableFields.Count; k++)
                    {
                        if (NetworkVariableFields[k].ShouldWrite(clientId, IsServer))
                        {
                            shouldSend = true;
                        }
                    }

                    if (shouldSend)
                    {
                        var message = new NetworkVariableDeltaMessage
                        {
                            NetworkObjectId = NetworkObjectId,
                            NetworkBehaviourIndex = NetworkObject.GetNetworkBehaviourOrderIndex(this),
                            NetworkBehaviour = this,
                            ClientId = clientId,
                            DeliveryMappedNetworkVariableIndex = m_DeliveryMappedNetworkVariableIndices[j]
                        };
                        // TODO: Serialization is where the IsDirty flag gets changed.
                        // Messages don't get sent from the server to itself, so if we're host and sending to ourselves,
                        // we still have to actually serialize the message even though we're not sending it, otherwise
                        // the dirty flag doesn't change properly. These two pieces should be decoupled at some point
                        // so we don't have to do this serialization work if we're not going to use the result.
                        if (IsServer && clientId == NetworkManager.ServerClientId)
                        {
                            var tmpWriter = new FastBufferWriter(MessagingSystem.NON_FRAGMENTED_MESSAGE_MAX_SIZE, Allocator.Temp);
                            using (tmpWriter)
                            {
                                message.Serialize(tmpWriter);
                            }
                        }
                        else
                        {
                            NetworkManager.SendMessage(message, m_DeliveryTypesForNetworkVariableGroups[j], clientId);
                        }
                    }
                }
            }
        }

        private bool CouldHaveDirtyNetworkVariables()
        {
            // TODO: There should be a better way by reading one dirty variable vs. 'n'
            for (int i = 0; i < NetworkVariableFields.Count; i++)
            {
                if (NetworkVariableFields[i].IsDirty())
                {
                    return true;
                }
            }

            return false;
        }

        internal void MarkVariablesDirty()
        {
            for (int j = 0; j < NetworkVariableFields.Count; j++)
            {
                NetworkVariableFields[j].SetDirty(true);
            }
        }

        internal void WriteNetworkVariableData(FastBufferWriter writer, ulong clientId)
        {
            if (NetworkVariableFields.Count == 0)
            {
                return;
            }

            for (int j = 0; j < NetworkVariableFields.Count; j++)
            {
                bool canClientRead = NetworkVariableFields[j].CanClientRead(clientId);

                if (canClientRead)
                {
                    var writePos = writer.Position;
                    writer.WriteValueSafe((ushort)0);
                    var startPos = writer.Position;
                    NetworkVariableFields[j].WriteField(writer);
                    var size = writer.Position - startPos;
                    writer.Seek(writePos);
                    writer.WriteValueSafe((ushort)size);
                    writer.Seek(startPos + size);
                }
                else
                {
                    writer.WriteValueSafe((ushort)0);
                }
            }
        }

        internal void SetNetworkVariableData(FastBufferReader reader)
        {
            if (NetworkVariableFields.Count == 0)
            {
                return;
            }

            for (int j = 0; j < NetworkVariableFields.Count; j++)
            {
                reader.ReadValueSafe(out ushort varSize);
                if (varSize == 0)
                {
                    continue;
                }

                var readStartPos = reader.Position;
                NetworkVariableFields[j].ReadField(reader);

                if (NetworkManager.NetworkConfig.EnsureNetworkVariableLengthSafety)
                {
                    if (reader.Position > (readStartPos + varSize))
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                        {
                            NetworkLog.LogWarning($"Var data read too far. {reader.Position - (readStartPos + varSize)} bytes.");
                        }

                        reader.Seek(readStartPos + varSize);
                    }
                    else if (reader.Position < (readStartPos + varSize))
                    {
                        if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                        {
                            NetworkLog.LogWarning($"Var data read too little. {(readStartPos + varSize) - reader.Position} bytes.");
                        }

                        reader.Seek(readStartPos + varSize);
                    }
                }
            }
        }

        /// <summary>
        /// Gets the local instance of a object with a given NetworkId
        /// </summary>
        /// <param name="networkId"></param>
        /// <returns></returns>
        protected NetworkObject GetNetworkObject(ulong networkId)
        {
            return NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkId, out NetworkObject networkObject) ? networkObject : null;
        }

        public void OnDestroy()
        {
            // this seems odd to do here, but in fact especially in tests we can find ourselves
            //  here without having called InitializedVariables, which causes problems if any
            //  of those variables use native containers (e.g. NetworkList) as they won't be
            //  registered here and therefore won't be cleaned up.
            //
            // we should study to understand the initialization patterns
            if (!m_VarInit)
            {
                InitializeVariables();
            }

            for (int i = 0; i < NetworkVariableFields.Count; i++)
            {
                NetworkVariableFields[i].Dispose();
            }
        }
    }
}
