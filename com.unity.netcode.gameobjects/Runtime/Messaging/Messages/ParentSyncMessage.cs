using UnityEngine;

namespace Unity.Netcode
{
    internal struct ParentSyncMessage : INetworkMessage
    {
        public ulong NetworkObjectId;

        private byte m_BitField;

        public bool WorldPositionStays
        {
            get => (m_BitField & 1) != 0;
            set => m_BitField = (byte)((m_BitField & ~BytePacker.ToByte(value)) | (BytePacker.ToByte(value)));
        }

        //If(Metadata.IsReparented)
        public bool IsLatestParentSet
        {
            get => (m_BitField & 1 << 1) != 0;
            set => m_BitField = (byte)((m_BitField & ~BytePacker.ToByte(value)) | (BytePacker.ToByte(value) << 1));
        }

        //If(IsLatestParentSet)
        public ulong? LatestParent;

        // Is set when the parent should be removed (similar to IsReparented functionality but only for removing the parent)
        public bool RemoveParent
        {
            get => (m_BitField & 1 << 2) != 0;
            set => m_BitField = (byte)((m_BitField & ~BytePacker.ToByte(value)) | (BytePacker.ToByte(value) << 2));
        }

        // These additional properties are used to synchronize clients with the current position,
        // rotation, and scale after parenting/de-parenting (world/local space relative). This
        // allows users to control the final child's transform values without having to have a
        // NetworkTransform component on the child. (i.e. picking something up)
        public Vector3 Position;
        public Quaternion Rotation;
        public Vector3 Scale;

        public void Serialize(FastBufferWriter writer)
        {
            BytePacker.WriteValueBitPacked(writer, NetworkObjectId);
            writer.WriteValueSafe(m_BitField);
            if (!RemoveParent)
            {
                if (IsLatestParentSet)
                {
                    BytePacker.WriteValueBitPacked(writer, LatestParent.Value);
                }
            }

            // Whether parenting or removing a parent, we always update the position, rotation, and scale
            writer.WriteValueSafe(Position);
            writer.WriteValueSafe(Rotation);
            writer.WriteValueSafe(Scale);
        }

        public bool Deserialize(FastBufferReader reader, ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            if (!networkManager.IsClient)
            {
                return false;
            }

            ByteUnpacker.ReadValueBitPacked(reader, out NetworkObjectId);
            reader.ReadValueSafe(out m_BitField);
            if (!RemoveParent)
            {
                if (IsLatestParentSet)
                {
                    ByteUnpacker.ReadValueBitPacked(reader, out ulong latestParent);
                    LatestParent = latestParent;
                }
            }

            // Whether parenting or removing a parent, we always update the position, rotation, and scale
            reader.ReadValueSafe(out Position);
            reader.ReadValueSafe(out Rotation);
            reader.ReadValueSafe(out Scale);

            if (!networkManager.SpawnManager.SpawnedObjects.ContainsKey(NetworkObjectId))
            {
                networkManager.DeferredMessageManager.DeferMessage(IDeferredMessageManager.TriggerType.OnSpawn, NetworkObjectId, reader, ref context);
                return false;
            }
            return true;
        }

        public void Handle(ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            var networkObject = networkManager.SpawnManager.SpawnedObjects[NetworkObjectId];
            networkObject.SetNetworkParenting(LatestParent, WorldPositionStays);
            networkObject.ApplyNetworkParenting(RemoveParent);

            // We set all of the transform values after parenting as they are
            // the values of the server-side post-parenting transform values
            if (!WorldPositionStays)
            {
                networkObject.transform.localPosition = Position;
                networkObject.transform.localRotation = Rotation;
            }
            else
            {
                networkObject.transform.position = Position;
                networkObject.transform.rotation = Rotation;
            }
            networkObject.transform.localScale = Scale;
        }
    }
}
