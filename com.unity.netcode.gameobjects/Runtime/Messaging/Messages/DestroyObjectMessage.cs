using System.Linq;

namespace Unity.Netcode
{
    internal struct DestroyObjectMessage : INetworkMessage, INetworkSerializeByMemcpy
    {
        public int Version => 0;

        private const string k_Name = "DestroyObjectMessage";

        public ulong NetworkObjectId;
        public bool DestroyGameObject;
        private byte m_DestroyFlags;

        internal int DeferredDespawnTick;
        // Temporary until we make this a list
        internal ulong TargetClientId;

        internal bool IsDistributedAuthority;

        internal const byte ClientTargetedDestroy = 0x01;

        internal bool IsTargetedDestroy
        {
            get
            {
                return GetFlag(ClientTargetedDestroy);
            }

            set
            {
                SetFlag(value, ClientTargetedDestroy);
            }
        }

        private bool GetFlag(int flag)
        {
            return (m_DestroyFlags & flag) != 0;
        }

        private void SetFlag(bool set, byte flag)
        {
            if (set) { m_DestroyFlags = (byte)(m_DestroyFlags | flag); }
            else { m_DestroyFlags = (byte)(m_DestroyFlags & ~flag); }
        }

        public void Serialize(FastBufferWriter writer, int targetVersion)
        {
            BytePacker.WriteValueBitPacked(writer, NetworkObjectId);
            if (IsDistributedAuthority)
            {
                writer.WriteByteSafe(m_DestroyFlags);

                if (IsTargetedDestroy)
                {
                    BytePacker.WriteValueBitPacked(writer, TargetClientId);
                }
                BytePacker.WriteValueBitPacked(writer, DeferredDespawnTick);
            }
            writer.WriteValueSafe(DestroyGameObject);
        }

        public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            if (!networkManager.IsClient)
            {
                return false;
            }

            ByteUnpacker.ReadValueBitPacked(reader, out NetworkObjectId);
            if (networkManager.DistributedAuthorityMode)
            {
                reader.ReadByteSafe(out m_DestroyFlags);
                if (IsTargetedDestroy)
                {
                    ByteUnpacker.ReadValueBitPacked(reader, out TargetClientId);
                }
                ByteUnpacker.ReadValueBitPacked(reader, out DeferredDespawnTick);
            }

            reader.ReadValueSafe(out DestroyGameObject);

            if (!networkManager.SpawnManager.SpawnedObjects.TryGetValue(NetworkObjectId, out var networkObject))
            {
                // Client-Server mode we always defer where in distributed authority mode we only defer if it is not a targeted destroy
                if (!networkManager.DistributedAuthorityMode || (networkManager.DistributedAuthorityMode && !IsTargetedDestroy))
                {
                    networkManager.DeferredMessageManager.DeferMessage(IDeferredNetworkMessageManager.TriggerType.OnSpawn, NetworkObjectId, reader, ref context, k_Name);
                }
            }
            return true;
        }

        public void Handle(ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;

            var networkObject = (NetworkObject)null;
            if (!networkManager.DistributedAuthorityMode)
            {
                // If this NetworkObject does not exist on this instance then exit early
                if (!networkManager.SpawnManager.SpawnedObjects.TryGetValue(NetworkObjectId, out networkObject))
                {
                    return;
                }
            }
            else
            {
                networkManager.SpawnManager.SpawnedObjects.TryGetValue(NetworkObjectId, out networkObject);
                if (!networkManager.DAHost && networkObject == null)
                {
                    // If this NetworkObject does not exist on this instance then exit early
                    return;
                }
            }
            // DANGO-TODO: This is just a quick way to foward despawn messages to the remaining clients
            if (networkManager.DistributedAuthorityMode && networkManager.DAHost)
            {
                var message = new DestroyObjectMessage
                {
                    NetworkObjectId = NetworkObjectId,
                    DestroyGameObject = DestroyGameObject,
                    IsDistributedAuthority = true,
                    IsTargetedDestroy = IsTargetedDestroy,
                    TargetClientId = TargetClientId, // Just always populate this value whether we write it or not
                    DeferredDespawnTick = DeferredDespawnTick,
                };
                var ownerClientId = networkObject == null ? context.SenderId : networkObject.OwnerClientId;
                var clientIds = networkObject == null ? networkManager.ConnectedClientsIds.ToList() : networkObject.Observers.ToList();

                foreach (var clientId in clientIds)
                {
                    if (clientId == networkManager.LocalClientId || clientId == ownerClientId)
                    {
                        continue;
                    }
                    networkManager.ConnectionManager.SendMessage(ref message, NetworkDelivery.ReliableSequenced, clientId);
                }
            }

            // If we are deferring the despawn, then add it to the deferred despawn queue
            if (networkManager.DistributedAuthorityMode)
            {
                if (DeferredDespawnTick > 0)
                {
                    // Clients always add it to the queue while DAHost will only add it to the queue if it is not a targeted destroy or it is and the target is the
                    // DAHost client.
                    if (!networkManager.DAHost || (networkManager.DAHost && (!IsTargetedDestroy || (IsTargetedDestroy && TargetClientId == 0))))
                    {
                        networkObject.DeferredDespawnTick = DeferredDespawnTick;
                        var hasCallback = networkObject.OnDeferredDespawnComplete != null;
                        networkManager.SpawnManager.DeferDespawnNetworkObject(NetworkObjectId, DeferredDespawnTick, hasCallback);
                        return;
                    }
                }

                // If this is targeted and we are not the target, then just update our local observers for this object
                if (IsTargetedDestroy && TargetClientId != networkManager.LocalClientId && networkObject != null)
                {
                    networkObject.Observers.Remove(TargetClientId);
                    return;
                }
            }

            if (networkObject != null)
            {
                // Otherwise just despawn the NetworkObject right now
                networkManager.SpawnManager.OnDespawnObject(networkObject, DestroyGameObject);
                networkManager.NetworkMetrics.TrackObjectDestroyReceived(context.SenderId, networkObject, context.MessageSize);
            }
        }
    }
}
