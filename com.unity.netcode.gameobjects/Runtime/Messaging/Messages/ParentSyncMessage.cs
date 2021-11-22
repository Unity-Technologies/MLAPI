namespace Unity.Netcode
{
    internal struct ParentSyncMessage : INetworkMessage
    {
        public ulong NetworkObjectId;

        public bool IsReparented;

        //If(Metadata.IsReparented)
        public bool IsLatestParentSet;

        //If(IsLatestParentSet)
        public ulong? LatestParent;

        public void Serialize(FastBufferWriter writer)
        {
            writer.WriteValueSafe(NetworkObjectId);
            writer.WriteValueSafe(IsReparented);
            if (IsReparented)
            {
                writer.WriteValueSafe(IsLatestParentSet);
                if (IsLatestParentSet)
                {
                    writer.WriteValueSafe((ulong)LatestParent);
                }
            }
        }

        public bool Deserialize(FastBufferReader reader, in NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            if (!networkManager.IsClient)
            {
                return false;
            }

            reader.ReadValueSafe(out NetworkObjectId);
            reader.ReadValueSafe(out IsReparented);
            if (IsReparented)
            {
                reader.ReadValueSafe(out IsLatestParentSet);
                if (IsLatestParentSet)
                {
                    reader.ReadValueSafe(out ulong latestParent);
                    LatestParent = latestParent;
                }
            }

            if (!networkManager.SpawnManager.SpawnedObjects.ContainsKey(NetworkObjectId))
            {
                networkManager.SpawnManager.TriggerOnSpawn(NetworkObjectId, reader, context);
                return false;
            }

            return true;
        }

        public void Handle(in NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            var networkObject = networkManager.SpawnManager.SpawnedObjects[NetworkObjectId];
            networkObject.SetNetworkParenting(IsReparented, LatestParent);
            networkObject.ApplyNetworkParenting();
        }
    }
}
