namespace Unity.Netcode
{
    internal struct ChangeOwnershipMessage : INetworkMessage, INetworkSerializeByMemcpy
    {
        public int Version => 0;

        public ulong NetworkObjectId;
        public ulong OwnerClientId;

        public void Serialize(FastBufferWriter writer, int targetVersion)
        {
            BytePacker.WriteValueBitPacked(writer, NetworkObjectId);
            BytePacker.WriteValueBitPacked(writer, OwnerClientId);
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
            if (!networkManager.SpawnManager.SpawnedObjects.ContainsKey(NetworkObjectId))
            {
                networkManager.DeferredMessageManager.DeferMessage(IDeferredNetworkMessageManager.TriggerType.OnSpawn, NetworkObjectId, reader, ref context);
                return false;
            }

            return true;
        }

        public void Handle(ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            var networkObject = networkManager.SpawnManager.SpawnedObjects[NetworkObjectId];
            var originalOwner = networkObject.OwnerClientId;

            networkObject.OwnerClientId = OwnerClientId;

            // We are current owner.
            if (originalOwner == networkManager.LocalClientId)
            {
                networkObject.InvokeBehaviourOnLostOwnership();
            }

            // For all other clients that are neither the former or current owner, update the behaviours' properties
            if (OwnerClientId != networkManager.LocalClientId && originalOwner != networkManager.LocalClientId)
            {
                for (int i = 0; i < networkObject.ChildNetworkBehaviours.Count; i++)
                {
                    networkObject.ChildNetworkBehaviours[i].UpdateNetworkProperties();
                }
            }

            // We are new owner.
            if (OwnerClientId == networkManager.LocalClientId)
            {
                networkObject.InvokeBehaviourOnGainedOwnership();
            }

            if (originalOwner == networkManager.LocalClientId)
            {
                // Mark any owner read variables as dirty
                networkObject.MarkOwnerReadVariablesDirty();
                // Immediately queue any pending deltas and order the message before the
                // change in ownership message.
                networkManager.BehaviourUpdater.NetworkBehaviourUpdate(true);
            }

            networkObject.InvokeOwnershipChanged(originalOwner, OwnerClientId);

            networkManager.NetworkMetrics.TrackOwnershipChangeReceived(context.SenderId, networkObject, context.MessageSize);
        }
    }
}
