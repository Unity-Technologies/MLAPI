using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Unity.Collections;

namespace Unity.Netcode
{
    /// <summary>
    /// This particular struct is a little weird because it doesn't actually contain the data
    /// it's serializing. Instead, it contains references to the data it needs to do the
    /// serialization. This is due to the generally amorphous nature of network variable
    /// deltas, since they're all driven by custom virtual method overloads.
    /// </summary>
    /// <remarks>
    /// Version 1:
    /// This version -does not- use the "KeepDirty" approach. Instead, the server will forward any state updates
    /// to the connected clients that are not the sender or the server itself. Each NetworkVariable state update
    /// included, on a per client basis, is first validated that the client can read the NetworkVariable before
    /// being added to the m_ForwardUpdates table.
    /// Version 0:
    /// The original version uses the "KeepDirty" approach in a client-server network topology where the server
    /// proxies state updates by "keeping the NetworkVariable(s) dirty" so it will send state updates
    /// at the end of the frame (but could delay until the next tick).
    /// </remarks>
    internal struct NetworkVariableDeltaMessage : INetworkMessage
    {
        private const int k_ServerDeltaForwardingAndNetworkDelivery = 1;
        public int Version => k_ServerDeltaForwardingAndNetworkDelivery;


        public ulong NetworkObjectId;
        public ushort NetworkBehaviourIndex;

        public HashSet<int> DeliveryMappedNetworkVariableIndex;
        public ulong TargetClientId;
        public NetworkBehaviour NetworkBehaviour;

        public NetworkDelivery NetworkDelivery;

        private FastBufferReader m_ReceivedNetworkVariableData;

        private bool m_ForwardingMessage;

        private int m_ReceivedMessageVersion;

        private const string k_Name = "NetworkVariableDeltaMessage";

        private Dictionary<ulong, List<int>> m_ForwardUpdates;

        private List<int> m_UpdatedNetworkVariables;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void WriteNetworkVariable(ref FastBufferWriter writer, ref NetworkVariableBase networkVariable, bool distributedAuthorityMode, bool ensureNetworkVariableLengthSafety, int nonfragmentedSize, int fragmentedSize)
        {
            if (ensureNetworkVariableLengthSafety)
            {
                var tempWriter = new FastBufferWriter(nonfragmentedSize, Allocator.Temp, fragmentedSize);
                networkVariable.WriteDelta(tempWriter);
                BytePacker.WriteValueBitPacked(writer, tempWriter.Length);

                if (!writer.TryBeginWrite(tempWriter.Length))
                {
                    throw new OverflowException($"Not enough space in the buffer to write {nameof(NetworkVariableDeltaMessage)}");
                }

                tempWriter.CopyTo(writer);
            }
            else
            {
                // TODO: Determine if we need to remove this with the 6.1 service updates
                if (distributedAuthorityMode)
                {
                    var size_marker = writer.Position;
                    writer.WriteValueSafe<ushort>(0);
                    var start_marker = writer.Position;
                    networkVariable.WriteDelta(writer);
                    var end_marker = writer.Position;
                    writer.Seek(size_marker);
                    var size = end_marker - start_marker;
                    if (size == 0)
                    {
                        UnityEngine.Debug.LogError($"Invalid write size of zero!");
                    }
                    writer.WriteValueSafe((ushort)size);
                    writer.Seek(end_marker);
                }
                else
                {
                    networkVariable.WriteDelta(writer);
                }
            }
        }

        public void Serialize(FastBufferWriter writer, int targetVersion)
        {
            if (!writer.TryBeginWrite(FastBufferWriter.GetWriteSize(NetworkObjectId) + FastBufferWriter.GetWriteSize(NetworkBehaviourIndex)))
            {
                throw new OverflowException($"Not enough space in the buffer to write {nameof(NetworkVariableDeltaMessage)}");
            }

            var obj = NetworkBehaviour.NetworkObject;
            var networkManager = obj.NetworkManagerOwner;
            var typeName = NetworkBehaviour.__getTypeName();
            var nonFragmentedMessageMaxSize = networkManager.MessageManager.NonFragmentedMessageMaxSize;
            var fragmentedMessageMaxSize = networkManager.MessageManager.FragmentedMessageMaxSize;
            var ensureNetworkVariableLengthSafety = networkManager.NetworkConfig.EnsureNetworkVariableLengthSafety;
            var distributedAuthorityMode = networkManager.DistributedAuthorityMode;

            BytePacker.WriteValueBitPacked(writer, NetworkObjectId);
            BytePacker.WriteValueBitPacked(writer, NetworkBehaviourIndex);

            // If using k_IncludeNetworkDelivery version, then we want to write the network delivery used and if we
            // are forwarding state updates then serialize any NetworkVariable states specific to this client.
            if (targetVersion >= k_ServerDeltaForwardingAndNetworkDelivery)
            {
                writer.WriteValueSafe(NetworkDelivery);
                // If we are forwarding the message, then proceed to forward state updates specific to the targeted client
                if (m_ForwardingMessage)
                {
                    // DANGO TODO: Remove distributedAuthorityMode portion when we remove the service specific NetworkVariable stuff
                    if (distributedAuthorityMode)
                    {
                        writer.WriteValueSafe((ushort)NetworkBehaviour.NetworkVariableFields.Count);
                    }

                    for (int i = 0; i < NetworkBehaviour.NetworkVariableFields.Count; i++)
                    {
                        var startingSize = writer.Length;
                        var networkVariable = NetworkBehaviour.NetworkVariableFields[i];
                        var shouldWrite = m_ForwardUpdates[TargetClientId].Contains(i);

                        // This var does not belong to the currently iterating delivery group.
                        if (distributedAuthorityMode)
                        {
                            if (!shouldWrite)
                            {
                                writer.WriteValueSafe<ushort>(0);
                            }
                        }
                        else if (ensureNetworkVariableLengthSafety)
                        {
                            if (!shouldWrite)
                            {
                                BytePacker.WriteValueBitPacked(writer, (ushort)0);
                            }
                        }
                        else
                        {
                            writer.WriteValueSafe(shouldWrite);
                        }

                        if (shouldWrite)
                        {
                            WriteNetworkVariable(ref writer, ref networkVariable, distributedAuthorityMode, ensureNetworkVariableLengthSafety, nonFragmentedMessageMaxSize, fragmentedMessageMaxSize);
                            networkManager.NetworkMetrics.TrackNetworkVariableDeltaSent(TargetClientId, obj, networkVariable.Name, typeName, writer.Length - startingSize);
                        }
                    }
                    return;
                }
            }

            // DANGO TODO: Remove this when we remove the service specific NetworkVariable stuff
            if (distributedAuthorityMode)
            {
                writer.WriteValueSafe((ushort)NetworkBehaviour.NetworkVariableFields.Count);
            }

            for (int i = 0; i < NetworkBehaviour.NetworkVariableFields.Count; i++)
            {
                if (!DeliveryMappedNetworkVariableIndex.Contains(i))
                {
                    // DANGO TODO: Remove distributedAuthorityMode portion when we remove the service specific NetworkVariable stuff
                    if (distributedAuthorityMode)
                    {
                        writer.WriteValueSafe<ushort>(0);
                    }
                    else if (ensureNetworkVariableLengthSafety)
                    {
                        BytePacker.WriteValueBitPacked(writer, (ushort)0);
                    }
                    else
                    {
                        writer.WriteValueSafe(false);
                    }

                    continue;
                }

                var startingSize = writer.Length;
                var networkVariable = NetworkBehaviour.NetworkVariableFields[i];

                var shouldWrite = networkVariable.IsDirty() &&
                    networkVariable.CanClientRead(TargetClientId) &&
                    (networkManager.IsServer || networkVariable.CanClientWrite(networkManager.LocalClientId)) &&
                    networkVariable.CanSend();

                // Prevent the server from writing to the client that owns a given NetworkVariable
                // Allowing the write would send an old value to the client and cause jitter
                if (networkVariable.WritePerm == NetworkVariableWritePermission.Owner &&
                    networkVariable.OwnerClientId() == TargetClientId)
                {
                    shouldWrite = false;
                }

                // The object containing the behaviour we're about to process is about to be shown to this client
                // As a result, the client will get the fully serialized NetworkVariable and would be confused by
                // an extraneous delta
                if (networkManager.SpawnManager.ObjectsToShowToClient.ContainsKey(TargetClientId) &&
                    networkManager.SpawnManager.ObjectsToShowToClient[TargetClientId]
                    .Contains(obj))
                {
                    shouldWrite = false;
                }

                // DANGO TODO: Remove distributedAuthorityMode portion when we remove the service specific NetworkVariable stuff
                if (distributedAuthorityMode)
                {
                    if (!shouldWrite)
                    {
                        writer.WriteValueSafe<ushort>(0);
                    }
                }
                else if (ensureNetworkVariableLengthSafety)
                {
                    if (!shouldWrite)
                    {
                        BytePacker.WriteValueBitPacked(writer, (ushort)0);
                    }
                }
                else
                {
                    writer.WriteValueSafe(shouldWrite);
                }

                if (shouldWrite)
                {
                    WriteNetworkVariable(ref writer, ref networkVariable, distributedAuthorityMode, ensureNetworkVariableLengthSafety, nonFragmentedMessageMaxSize, fragmentedMessageMaxSize);
                    networkManager.NetworkMetrics.TrackNetworkVariableDeltaSent(TargetClientId, obj, networkVariable.Name, typeName, writer.Length - startingSize);
                }
            }
        }

        public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
        {
            m_ReceivedMessageVersion = receivedMessageVersion;
            ByteUnpacker.ReadValueBitPacked(reader, out NetworkObjectId);
            ByteUnpacker.ReadValueBitPacked(reader, out NetworkBehaviourIndex);
            // If we are using the k_IncludeNetworkDelivery message version, then read the NetworkDelivery used
            if (receivedMessageVersion >= k_ServerDeltaForwardingAndNetworkDelivery)
            {
                reader.ReadValueSafe(out NetworkDelivery);
            }
            m_ReceivedNetworkVariableData = reader;

            return true;
        }

        public void Handle(ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;

            if (networkManager.SpawnManager.SpawnedObjects.TryGetValue(NetworkObjectId, out NetworkObject networkObject))
            {
                var distributedAuthorityMode = networkManager.DistributedAuthorityMode;
                var ensureNetworkVariableLengthSafety = networkManager.NetworkConfig.EnsureNetworkVariableLengthSafety;
                var networkBehaviour = networkObject.GetNetworkBehaviourAtOrderIndex(NetworkBehaviourIndex);
                var isServerAndDeltaForwarding = m_ReceivedMessageVersion >= k_ServerDeltaForwardingAndNetworkDelivery && networkManager.IsServer;
                var markNetworkVariableDirty = m_ReceivedMessageVersion >= k_ServerDeltaForwardingAndNetworkDelivery ? false : networkManager.IsServer;
                m_UpdatedNetworkVariables = new List<int>();

                if (networkBehaviour == null)
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning($"Network variable delta message received for a non-existent behaviour. {nameof(NetworkObjectId)}: {NetworkObjectId}, {nameof(NetworkBehaviourIndex)}: {NetworkBehaviourIndex}");
                    }
                }
                else
                {
                    // DANGO TODO: Remove distributedAuthorityMode portion when we remove the service specific NetworkVariable stuff
                    if (distributedAuthorityMode)
                    {
                        m_ReceivedNetworkVariableData.ReadValueSafe(out ushort variableCount);
                        if (variableCount != networkBehaviour.NetworkVariableFields.Count)
                        {
                            UnityEngine.Debug.LogError("Variable count mismatch");
                        }
                    }

                    // (For client-server) As opposed to worrying about adding additional processing on the server to send NetworkVariable
                    // updates at the end of the frame, we now track all NetworkVariable state updates, per client, that need to be forwarded
                    // to the client. This creates a list of all remaining connected clients that could have updates applied.
                    if (isServerAndDeltaForwarding)
                    {
                        m_ForwardUpdates = new Dictionary<ulong, List<int>>();
                        foreach (var clientId in networkManager.ConnectedClientsIds)
                        {
                            if (clientId == context.SenderId || clientId == networkManager.LocalClientId || !networkObject.Observers.Contains(clientId))
                            {
                                continue;
                            }
                            m_ForwardUpdates.Add(clientId, new List<int>());
                        }
                    }

                    // Update NetworkVariable Fields
                    for (int i = 0; i < networkBehaviour.NetworkVariableFields.Count; i++)
                    {
                        int varSize = 0;
                        var networkVariable = networkBehaviour.NetworkVariableFields[i];

                        // DANGO TODO: Remove distributedAuthorityMode portion when we remove the service specific NetworkVariable stuff
                        if (distributedAuthorityMode)
                        {
                            m_ReceivedNetworkVariableData.ReadValueSafe(out ushort variableSize);
                            varSize = variableSize;

                            if (varSize == 0)
                            {
                                continue;
                            }
                        }
                        else if (ensureNetworkVariableLengthSafety)
                        {
                            ByteUnpacker.ReadValueBitPacked(m_ReceivedNetworkVariableData, out varSize);
                            if (varSize == 0)
                            {
                                continue;
                            }
                        }
                        else
                        {
                            m_ReceivedNetworkVariableData.ReadValueSafe(out bool deltaExists);
                            if (!deltaExists)
                            {
                                continue;
                            }
                        }

                        if (networkManager.IsServer && !networkVariable.CanClientWrite(context.SenderId))
                        {
                            // we are choosing not to fire an exception here, because otherwise a malicious client could use this to crash the server
                            if (networkManager.NetworkConfig.EnsureNetworkVariableLengthSafety)
                            {
                                if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                                {
                                    NetworkLog.LogWarning($"Client wrote to {typeof(NetworkVariable<>).Name} without permission. => {nameof(NetworkObjectId)}: {NetworkObjectId} - {nameof(NetworkObject.GetNetworkBehaviourOrderIndex)}(): {networkObject.GetNetworkBehaviourOrderIndex(networkBehaviour)} - VariableIndex: {i}");
                                    NetworkLog.LogError($"[{networkVariable.GetType().Name}]");
                                }

                                m_ReceivedNetworkVariableData.Seek(m_ReceivedNetworkVariableData.Position + varSize);
                                continue;
                            }

                            //This client wrote somewhere they are not allowed. This is critical
                            //We can't just skip this field. Because we don't actually know how to dummy read
                            //That is, we don't know how many bytes to skip. Because the interface doesn't have a
                            //Read that gives us the value. Only a Read that applies the value straight away
                            //A dummy read COULD be added to the interface for this situation, but it's just being too nice.
                            //This is after all a developer fault. A critical error should be fine.
                            // - TwoTen
                            if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                            {
                                NetworkLog.LogError($"Client wrote to {typeof(NetworkVariable<>).Name} without permission. No more variables can be read. This is critical. => {nameof(NetworkObjectId)}: {NetworkObjectId} - {nameof(NetworkObject.GetNetworkBehaviourOrderIndex)}(): {networkObject.GetNetworkBehaviourOrderIndex(networkBehaviour)} - VariableIndex: {i}");
                                NetworkLog.LogError($"[{networkVariable.GetType().Name}]");
                            }
                            return;
                        }
                        int readStartPos = m_ReceivedNetworkVariableData.Position;

                        // DANGO TODO: Remove distributedAuthorityMode portion when we remove the service specific NetworkVariable stuff
                        if (distributedAuthorityMode || ensureNetworkVariableLengthSafety)
                        {
                            var remainingBufferSize = m_ReceivedNetworkVariableData.Length - m_ReceivedNetworkVariableData.Position;
                            if (varSize > (remainingBufferSize))
                            {
                                UnityEngine.Debug.LogError($"[{networkBehaviour.name}][Delta State Read Error] Expecting to read {varSize} but only {remainingBufferSize} remains!");
                                return;
                            }
                        }

                        // Added a try catch here to assure any failure will only fail on this one message and not disrupt the stack
                        try
                        {
                            // Read the delta
                            networkVariable.ReadDelta(m_ReceivedNetworkVariableData, markNetworkVariableDirty);

                            // Add the NetworkVariable field index so we can invoke the PostDeltaRead
                            m_UpdatedNetworkVariables.Add(i);
                        }
                        catch (Exception ex)
                        {
                            UnityEngine.Debug.LogException(ex);
                            return;
                        }

                        // (For client-server) As opposed to worrying about adding additional processing on the server to send NetworkVariable
                        // updates at the end of the frame, we now track all NetworkVariable state updates, per client, that need to be forwarded
                        // to the client. This happens once the server is finished processing all state updates for this message.
                        if (isServerAndDeltaForwarding)
                        {
                            foreach (var forwardEntry in m_ForwardUpdates)
                            {
                                // Only track things that the client can read
                                if (networkVariable.CanClientRead(forwardEntry.Key))
                                {
                                    // If the object is about to be shown to the client then don't send an update as it will
                                    // send a full update when shown.
                                    if (networkManager.SpawnManager.ObjectsToShowToClient.ContainsKey(forwardEntry.Key) &&
                                        networkManager.SpawnManager.ObjectsToShowToClient[forwardEntry.Key]
                                        .Contains(networkObject))
                                    {
                                        continue;
                                    }
                                    forwardEntry.Value.Add(i);
                                }
                            }
                        }

                        networkManager.NetworkMetrics.TrackNetworkVariableDeltaReceived(
                            context.SenderId,
                            networkObject,
                            networkVariable.Name,
                            networkBehaviour.__getTypeName(),
                            context.MessageSize);

                        // DANGO TODO: Remove distributedAuthorityMode portion when we remove the service specific NetworkVariable stuff
                        if (distributedAuthorityMode || ensureNetworkVariableLengthSafety)
                        {
                            if (m_ReceivedNetworkVariableData.Position > (readStartPos + varSize))
                            {
                                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                                {
                                    NetworkLog.LogWarning($"Var delta read too far. {m_ReceivedNetworkVariableData.Position - (readStartPos + varSize)} bytes. => {nameof(NetworkObjectId)}: {NetworkObjectId} - {nameof(NetworkObject.GetNetworkBehaviourOrderIndex)}(): {networkObject.GetNetworkBehaviourOrderIndex(networkBehaviour)} - VariableIndex: {i}");
                                }

                                m_ReceivedNetworkVariableData.Seek(readStartPos + varSize);
                            }
                            else if (m_ReceivedNetworkVariableData.Position < (readStartPos + varSize))
                            {
                                if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                                {
                                    NetworkLog.LogWarning($"Var delta read too little. {readStartPos + varSize - m_ReceivedNetworkVariableData.Position} bytes. => {nameof(NetworkObjectId)}: {NetworkObjectId} - {nameof(NetworkObject.GetNetworkBehaviourOrderIndex)}(): {networkObject.GetNetworkBehaviourOrderIndex(networkBehaviour)} - VariableIndex: {i}");
                                }

                                m_ReceivedNetworkVariableData.Seek(readStartPos + varSize);
                            }
                        }
                    }

                    // If we are using the version of this message that includes network delivery, then
                    // forward this update to all connected clients (other than the sender and the server).
                    if (isServerAndDeltaForwarding)
                    {
                        var message = new NetworkVariableDeltaMessage()
                        {
                            NetworkBehaviour = networkBehaviour,
                            NetworkBehaviourIndex = NetworkBehaviourIndex,
                            NetworkObjectId = NetworkObjectId,
                            m_ForwardingMessage = true,
                            m_ForwardUpdates = m_ForwardUpdates,
                        };

                        foreach (var forwardEntry in m_ForwardUpdates)
                        {
                            // Only forward updates to any client that has visibility to the state updates included in this message
                            if (forwardEntry.Value.Count > 0)
                            {
                                message.TargetClientId = forwardEntry.Key;
                                networkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery, forwardEntry.Key);
                            }
                        }
                    }

                    // This should be always invoked (client & server) to assure the previous values are set
                    // !! IMPORTANT ORDER OF OPERATIONS !! (Has to happen after forwarding deltas)
                    // When a server forwards delta updates to connected clients, it needs to preserve the previous value
                    // until it is done serializing all valid NetworkVariable field deltas (relative to each client). This
                    // is invoked after it is done forwarding the deltas.
                    foreach (var fieldIndex in m_UpdatedNetworkVariables)
                    {
                        networkBehaviour.NetworkVariableFields[fieldIndex].PostDeltaRead();
                    }
                }
            }
            else
            {
                // DANGO-TODO: Fix me!
                // When a client-spawned NetworkObject is despawned by the owner client, the owner client will still get messages for deltas and cause this to
                // log a warning. The issue is primarily how NetworkVariables handle updating and will require some additional re-factoring.
                networkManager.DeferredMessageManager.DeferMessage(IDeferredNetworkMessageManager.TriggerType.OnSpawn, NetworkObjectId, m_ReceivedNetworkVariableData, ref context, k_Name);
            }
        }
    }
}
