
namespace Unity.Netcode
{
    internal struct ChangeOwnershipMessage : INetworkMessage, INetworkSerializeByMemcpy
    {
        public int Version => 0;

        private const string k_Name = "ChangeOwnershipMessage";

        public ulong NetworkObjectId;
        public ulong OwnerClientId;
        // SERVICE NOTES:
        // When forwarding the message to clients on the CMB Service side,
        // you can set the ClientIdCount to 0 and skip writing the ClientIds.
        // See the NetworkObjet.OwnershipRequest for more potential service side additions

        /// <summary>
        /// When requesting, RequestClientId is the requestor.
        /// When approving, RequestClientId is the owner that approved.
        /// When responding (only for denied), RequestClientId is the requestor 
        /// </summary>
        internal ulong RequestClientId;
        internal int ClientIdCount;
        internal ulong[] ClientIds;
        internal bool DistributedAuthorityMode;
        internal ushort OwnershipFlags;
        internal byte OwnershipRequestResponseStatus;
        private byte m_OwnershipMessageTypeFlags;

        private const byte k_OwnershipChanging = 0x01;
        private const byte k_OwnershipFlagsUpdate = 0x02;
        private const byte k_RequestOwnership = 0x04;
        private const byte k_RequestApproved = 0x08;
        private const byte k_RequestDenied = 0x10;

        // If no flags are set, then ownership is changing
        internal bool OwnershipIsChanging
        {
            get
            {
                return GetFlag(k_OwnershipChanging);
            }

            set
            {
                SetFlag(value, k_OwnershipChanging);
            }
        }

        internal bool OwnershipFlagsUpdate
        {
            get
            {
                return GetFlag(k_OwnershipFlagsUpdate);
            }

            set
            {
                SetFlag(value, k_OwnershipFlagsUpdate);
            }
        }

        internal bool RequestOwnership
        {
            get
            {
                return GetFlag(k_RequestOwnership);
            }

            set
            {
                SetFlag(value, k_RequestOwnership);
            }
        }

        internal bool RequestApproved
        {
            get
            {
                return GetFlag(k_RequestApproved);
            }

            set
            {
                SetFlag(value, k_RequestApproved);
            }
        }

        internal bool RequestDenied
        {
            get
            {
                return GetFlag(k_RequestDenied);
            }

            set
            {
                SetFlag(value, k_RequestDenied);
            }
        }

        private bool GetFlag(int flag)
        {
            return (m_OwnershipMessageTypeFlags & flag) != 0;
        }

        private void SetFlag(bool set, byte flag)
        {
            if (set) { m_OwnershipMessageTypeFlags = (byte)(m_OwnershipMessageTypeFlags | flag); }
            else { m_OwnershipMessageTypeFlags = (byte)(m_OwnershipMessageTypeFlags & ~flag); }
        }

        public void Serialize(FastBufferWriter writer, int targetVersion)
        {
            BytePacker.WriteValueBitPacked(writer, NetworkObjectId);
            BytePacker.WriteValueBitPacked(writer, OwnerClientId);
            if (DistributedAuthorityMode)
            {
                BytePacker.WriteValueBitPacked(writer, ClientIdCount);
                if (ClientIdCount > 0)
                {
                    if (ClientIdCount != ClientIds.Length)
                    {
                        throw new System.Exception($"[{nameof(ChangeOwnershipMessage)}] ClientIdCount is {ClientIdCount} but the ClientIds length is {ClientIds.Length}!");
                    }
                    foreach (var clientId in ClientIds)
                    {
                        BytePacker.WriteValueBitPacked(writer, clientId);
                    }
                }

                writer.WriteValueSafe(m_OwnershipMessageTypeFlags);
                if (OwnershipFlagsUpdate || OwnershipIsChanging)
                {
                    writer.WriteValueSafe(OwnershipFlags);
                }

                // When requesting, it is the requestor
                // When approving, it is the owner that approved
                // When denied, it is the requestor
                if (RequestOwnership || RequestApproved || RequestDenied)
                {
                    writer.WriteValueSafe(RequestClientId);

                    if (RequestDenied)
                    {
                        writer.WriteValueSafe(OwnershipRequestResponseStatus);
                    }
                }
            }
        }

        public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            if (!networkManager.IsClient)
            {
                return false;
            }
            ByteUnpacker.ReadValueBitPacked(reader, out NetworkObjectId);
            ByteUnpacker.ReadValueBitPacked(reader, out OwnerClientId);

            if (networkManager.DistributedAuthorityMode)
            {
                ByteUnpacker.ReadValueBitPacked(reader, out ClientIdCount);
                if (ClientIdCount > 0)
                {
                    ClientIds = new ulong[ClientIdCount];
                    var clientId = (ulong)0;
                    for (int i = 0; i < ClientIdCount; i++)
                    {
                        ByteUnpacker.ReadValueBitPacked(reader, out clientId);
                        ClientIds[i] = clientId;
                    }
                }

                reader.ReadValueSafe(out m_OwnershipMessageTypeFlags);
                if (OwnershipFlagsUpdate || OwnershipIsChanging)
                {
                    reader.ReadValueSafe(out OwnershipFlags);
                }

                // When requesting, it is the requestor
                // When approving, it is the owner that approved
                // When denied, it is the requestor
                if (RequestOwnership || RequestApproved || RequestDenied)
                {
                    reader.ReadValueSafe(out RequestClientId);

                    if (RequestDenied)
                    {
                        reader.ReadValueSafe(out OwnershipRequestResponseStatus);
                    }
                }
            }

            // If we are not a DAHost instance and the NetworkObject does not exist then defer it as it very likely is not spawned yet.
            // Otherwise if we are the DAHost and it does not exist then we want to forward this message because when the NetworkObject
            // is made visible again, the ownership flags and owner information will be synchronized with the DAHost by the current
            // authority of the NetworkObject in question.
            if (!networkManager.DAHost && !networkManager.SpawnManager.SpawnedObjects.ContainsKey(NetworkObjectId))
            {
                networkManager.DeferredMessageManager.DeferMessage(IDeferredNetworkMessageManager.TriggerType.OnSpawn, NetworkObjectId, reader, ref context, k_Name);
                return false;
            }
            return true;
        }

        public void Handle(ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;

            // If we are the DAHost then forward this message
            if (networkManager.DAHost)
            {
                var clientList = ClientIdCount > 0 ? ClientIds : networkManager.ConnectedClientsIds;

                var message = new ChangeOwnershipMessage()
                {
                    NetworkObjectId = NetworkObjectId,
                    OwnerClientId = OwnerClientId,
                    DistributedAuthorityMode = true,
                    OwnershipFlags = OwnershipFlags,
                    RequestClientId = RequestClientId,
                    ClientIdCount = 0,
                    m_OwnershipMessageTypeFlags = m_OwnershipMessageTypeFlags,
                };

                if (RequestDenied)
                {
                    // If the local DAHost's client is not the target, then forward to the target
                    if (RequestClientId != networkManager.LocalClientId)
                    {
                        message.OwnershipRequestResponseStatus = OwnershipRequestResponseStatus;
                        networkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.Reliable, RequestClientId);
                        // We don't want the local DAHost's client to process this message, so exit early
                        return;
                    }
                }
                else if (RequestOwnership)
                {
                    // If the DAHost client is not authority, just forward the message to the authority
                    if (OwnerClientId != networkManager.LocalClientId)
                    {
                        networkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.Reliable, OwnerClientId);
                        // We don't want the local DAHost's client to process this message, so exit early
                        return;
                    }
                    // Otherwise, fall through and process the request.
                }
                else
                {
                    foreach (var clientId in clientList)
                    {
                        if (clientId == networkManager.LocalClientId)
                        {
                            continue;
                        }

                        // If ownership is changing and this is not an ownership request approval then ignore the SenderId
                        if (OwnershipIsChanging && !RequestApproved && context.SenderId == clientId)
                        {
                            continue;
                        }

                        // If it is just updating flags then ignore sending to the owner
                        // If it is a request or approving request, then ignore the RequestClientId
                        if ((OwnershipFlagsUpdate && clientId == OwnerClientId) || ((RequestOwnership || RequestApproved) && clientId == RequestClientId))
                        {
                            continue;
                        }
                        networkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.Reliable, clientId);
                    }
                }
                // If the NetworkObject is not visible to the DAHost client, then exit early 
                if (!networkManager.SpawnManager.SpawnedObjects.ContainsKey(NetworkObjectId))
                {
                    return;
                }
            }

            // If ownership is changing, then run through the ownershipd changed sequence
            // Note: There is some extended ownership script at the bottom of HandleOwnershipChange
            // If not in distributed authority mode, then always go straight to HandleOwnershipChange
            if (OwnershipIsChanging || !networkManager.DistributedAuthorityMode)
            {
                HandleOwnershipChange(ref context);
            }
            else if (networkManager.DistributedAuthorityMode)
            {
                // Otherwise, we handle and extended ownership update
                HandleExtendedOwnershipUpdate(ref context);
            }
        }

        /// <summary>
        /// Handle the 
        /// </summary>
        /// <param name="context"></param>
        private void HandleExtendedOwnershipUpdate(ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;

            // Handle the extended ownership message types
            var networkObject = networkManager.SpawnManager.SpawnedObjects[NetworkObjectId];

            if (OwnershipFlagsUpdate)
            {
                // Just update the ownership flags
                networkObject.Ownership = (NetworkObject.OwnershipStatus)OwnershipFlags;
            }
            else if (RequestOwnership)
            {
                // Requesting ownership, if allowed it will automatically send the ownership change message
                networkObject.OwnershipRequest(RequestClientId);
            }
            else if (RequestDenied)
            {
                networkObject.OwnershipRequestResponse((NetworkObject.OwnershipRequestResponseStatus)OwnershipRequestResponseStatus);
            }
        }

        /// <summary>
        /// Handle the traditional change in ownership message type logic
        /// </summary>
        /// <param name="context"></param>
        private void HandleOwnershipChange(ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            var networkObject = networkManager.SpawnManager.SpawnedObjects[NetworkObjectId];

            // Sanity check that we are not sending duplicated change ownership messages
            if (networkObject.OwnerClientId == OwnerClientId)
            {
                UnityEngine.Debug.LogError($"Unnecessary ownership changed message for {NetworkObjectId}.");
                // Ignore the message
                return;
            }

            var originalOwner = networkObject.OwnerClientId;
            networkObject.OwnerClientId = OwnerClientId;

            if (networkManager.DistributedAuthorityMode)
            {
                networkObject.Ownership = (NetworkObject.OwnershipStatus)OwnershipFlags;
            }

            // We are current owner (client-server) or running in distributed authority mode
            if (originalOwner == networkManager.LocalClientId || networkManager.DistributedAuthorityMode)
            {
                networkObject.InvokeBehaviourOnLostOwnership();
            }

            // If in distributed authority mode 
            if (networkManager.DistributedAuthorityMode)
            {
                // Always update the network properties in distributed authority mode 
                for (int i = 0; i < networkObject.ChildNetworkBehaviours.Count; i++)
                {
                    networkObject.ChildNetworkBehaviours[i].UpdateNetworkProperties();
                }
            }
            else // Otherwise update properties like we would in client-server
            {
                // For all other clients that are neither the former or current owner, update the behaviours' properties
                if (OwnerClientId != networkManager.LocalClientId && originalOwner != networkManager.LocalClientId)
                {
                    for (int i = 0; i < networkObject.ChildNetworkBehaviours.Count; i++)
                    {
                        networkObject.ChildNetworkBehaviours[i].UpdateNetworkProperties();
                    }
                }
            }

            // We are new owner or (client-server) or running in distributed authority mode
            if (OwnerClientId == networkManager.LocalClientId || networkManager.DistributedAuthorityMode)
            {
                networkObject.InvokeBehaviourOnGainedOwnership();
            }


            if (originalOwner == networkManager.LocalClientId && !networkManager.DistributedAuthorityMode)
            {
                // Mark any owner read variables as dirty
                networkObject.MarkOwnerReadVariablesDirty();
                // Immediately queue any pending deltas and order the message before the
                // change in ownership message.
                networkManager.BehaviourUpdater.NetworkBehaviourUpdate(true);
            }

            // Always invoke ownership change notifications
            networkObject.InvokeOwnershipChanged(originalOwner, OwnerClientId);

            // If this change was requested, then notify that the request was approved (doing this last so all ownership
            // changes have already been applied if the callback is invoked)
            if (networkManager.DistributedAuthorityMode && networkManager.LocalClientId == OwnerClientId)
            {
                if (RequestApproved)
                {
                    networkObject.OwnershipRequestResponse(NetworkObject.OwnershipRequestResponseStatus.Approved);
                }

                // If the NetworkObject changed ownership and the Requested flag was set (i.e. it was an ownership request),
                // then the new owner granted ownership removes the Requested flag and sends out an ownership status update.
                if (networkObject.HasExtendedOwnershipStatus(NetworkObject.OwnershipStatusExtended.Requested))
                {
                    networkObject.RemoveOwnershipExtended(NetworkObject.OwnershipStatusExtended.Requested);
                    networkObject.SendOwnershipStatusUpdate();
                }
            }

            networkManager.NetworkMetrics.TrackOwnershipChangeReceived(context.SenderId, networkObject, context.MessageSize);
        }
    }
}
