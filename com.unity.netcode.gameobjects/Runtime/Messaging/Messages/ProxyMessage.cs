using Unity.Collections;

namespace Unity.Netcode
{
    internal struct ProxyMessage : INetworkMessage
    {
        public NativeArray<ulong> TargetClientIds;
        public NetworkDelivery Delivery;
        public RpcMessage WrappedMessage;

        // Version of ProxyMessage and RpcMessage must always match.
        // If ProxyMessage needs to change, increment RpcMessage's version
        public int Version => new RpcMessage().Version;

        public void Serialize(FastBufferWriter writer, int targetVersion)
        {
            writer.WriteValueSafe(TargetClientIds);
            BytePacker.WriteValuePacked(writer, Delivery);
            WrappedMessage.Serialize(writer, targetVersion);
        }

        public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
        {
            reader.ReadValueSafe(out TargetClientIds, Allocator.Temp);
            ByteUnpacker.ReadValuePacked(reader, out Delivery);
            WrappedMessage = new RpcMessage();
            WrappedMessage.Deserialize(reader, ref context, receivedMessageVersion);
            return true;
        }

        public unsafe void Handle(ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            if (!networkManager.SpawnManager.SpawnedObjects.TryGetValue(WrappedMessage.Metadata.NetworkObjectId, out var networkObject))
            {
                // If the NetworkObject no longer exists then just log a warning when developer mode logging is enabled and exit.
                // This can happen if NetworkObject is despawned and a client sends an RPC before receiving the despawn message.
                if (networkManager.LogLevel == LogLevel.Developer)
                {
                    NetworkLog.LogWarning($"[{WrappedMessage.Metadata.NetworkObjectId}, {WrappedMessage.Metadata.NetworkBehaviourId}, {WrappedMessage.Metadata.NetworkRpcMethodId}] An RPC called on a {nameof(NetworkObject)} that is not in the spawned objects list. Please make sure the {nameof(NetworkObject)} is spawned before calling RPCs.");
                }
                return;
            }

            var observers = networkObject.Observers;

            var nonServerIds = new NativeList<ulong>(Allocator.Temp);
            for (var i = 0; i < TargetClientIds.Length; ++i)
            {
                if (!observers.Contains(TargetClientIds[i]))
                {
                    continue;
                }

                if (TargetClientIds[i] == NetworkManager.ServerClientId)
                {
                    WrappedMessage.Handle(ref context);
                }
                else
                {
                    nonServerIds.Add(TargetClientIds[i]);
                }
            }

            WrappedMessage.WriteBuffer = new FastBufferWriter(WrappedMessage.ReadBuffer.Length, Allocator.Temp);

            using (WrappedMessage.WriteBuffer)
            {
                WrappedMessage.WriteBuffer.WriteBytesSafe(WrappedMessage.ReadBuffer.GetUnsafePtr(), WrappedMessage.ReadBuffer.Length);
                networkManager.MessageManager.SendMessage(ref WrappedMessage, Delivery, nonServerIds);
            }
        }
    }
}
