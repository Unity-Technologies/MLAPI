using System;
using Unity.Collections;
using UnityEngine;

namespace Unity.Netcode
{
    internal static class RpcMessageHelpers
    {
        public static unsafe void Serialize(ref FastBufferWriter writer, ref RpcMetadata metadata, ref FastBufferWriter payload)
        {
            BytePacker.WriteValueBitPacked(writer, metadata.NetworkObjectId);
            BytePacker.WriteValueBitPacked(writer, metadata.NetworkBehaviourId);
            BytePacker.WriteValueBitPacked(writer, metadata.NetworkRpcMethodId);
            writer.WriteBytesSafe(payload.GetUnsafePtr(), payload.Length);
        }

        public static unsafe bool Deserialize(ref FastBufferReader reader, ref NetworkContext context, ref RpcMetadata metadata, ref FastBufferReader payload)
        {
            ByteUnpacker.ReadValueBitPacked(reader, out metadata.NetworkObjectId);
            ByteUnpacker.ReadValueBitPacked(reader, out metadata.NetworkBehaviourId);
            ByteUnpacker.ReadValueBitPacked(reader, out metadata.NetworkRpcMethodId);

            var networkManager = (NetworkManager)context.SystemOwner;
            if (!networkManager.SpawnManager.SpawnedObjects.ContainsKey(metadata.NetworkObjectId))
            {
                networkManager.DeferredMessageManager.DeferMessage(IDeferredNetworkMessageManager.TriggerType.OnSpawn, metadata.NetworkObjectId, reader, ref context);
                return false;
            }

            var networkObject = networkManager.SpawnManager.SpawnedObjects[metadata.NetworkObjectId];
            var networkBehaviour = networkManager.SpawnManager.SpawnedObjects[metadata.NetworkObjectId].GetNetworkBehaviourAtOrderIndex(metadata.NetworkBehaviourId);
            if (networkBehaviour == null)
            {
                return false;
            }

            if (!NetworkBehaviour.__rpc_func_table[networkBehaviour.GetType()].ContainsKey(metadata.NetworkRpcMethodId))
            {
                return false;
            }

            payload = new FastBufferReader(reader.GetUnsafePtrAtCurrentPosition(), Allocator.None, reader.Length - reader.Position);

#if DEVELOPMENT_BUILD || UNITY_EDITOR || UNITY_MP_TOOLS_NET_STATS_MONITOR_ENABLED_IN_RELEASE
            if (NetworkBehaviour.__rpc_name_table[networkBehaviour.GetType()].TryGetValue(metadata.NetworkRpcMethodId, out var rpcMethodName))
            {
                networkManager.NetworkMetrics.TrackRpcReceived(
                    context.SenderId,
                    networkObject,
                    rpcMethodName,
                    networkBehaviour.__getTypeName(),
                    reader.Length);
            }
#endif

            return true;
        }

        public static void Handle(ref NetworkContext context, ref RpcMetadata metadata, ref FastBufferReader payload, ref __RpcParams rpcParams)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            if (!networkManager.SpawnManager.SpawnedObjects.TryGetValue(metadata.NetworkObjectId, out var networkObject))
            {
                // If the NetworkObject no longer exists then just log a warning when developer mode logging is enabled and exit.
                // This can happen if NetworkObject is despawned and a client sends an RPC before receiving the despawn message.
                if (networkManager.LogLevel == LogLevel.Developer)
                {
                    NetworkLog.LogWarning($"[{metadata.NetworkObjectId}, {metadata.NetworkBehaviourId}, {metadata.NetworkRpcMethodId}] An RPC called on a {nameof(NetworkObject)} that is not in the spawned objects list. Please make sure the {nameof(NetworkObject)} is spawned before calling RPCs.");
                }
                return;
            }
            var networkBehaviour = networkObject.GetNetworkBehaviourAtOrderIndex(metadata.NetworkBehaviourId);

            try
            {
                NetworkBehaviour.__rpc_func_table[networkBehaviour.GetType()][metadata.NetworkRpcMethodId](networkBehaviour, payload, rpcParams);
            }
            catch (Exception ex)
            {
                Debug.LogException(new Exception("Unhandled RPC exception!", ex));
                if (networkManager.LogLevel == LogLevel.Developer)
                {
                    Debug.Log($"RPC Table Contents");
                    foreach (var entry in NetworkBehaviour.__rpc_func_table[networkBehaviour.GetType()])
                    {
                        Debug.Log($"{entry.Key} | {entry.Value.Method.Name}");
                    }
                }
            }
        }
    }

    internal struct RpcMetadata : INetworkSerializeByMemcpy
    {
        public ulong NetworkObjectId;
        public ushort NetworkBehaviourId;
        public uint NetworkRpcMethodId;
    }

    internal struct ServerRpcMessage : INetworkMessage
    {
        public int Version => 0;

        public RpcMetadata Metadata;

        public FastBufferWriter WriteBuffer;
        public FastBufferReader ReadBuffer;

        public unsafe void Serialize(FastBufferWriter writer, int targetVersion)
        {
            RpcMessageHelpers.Serialize(ref writer, ref Metadata, ref WriteBuffer);
        }

        public unsafe bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
        {
            return RpcMessageHelpers.Deserialize(ref reader, ref context, ref Metadata, ref ReadBuffer);
        }

        public void Handle(ref NetworkContext context)
        {
            var rpcParams = new __RpcParams
            {
                Server = new ServerRpcParams
                {
                    Receive = new ServerRpcReceiveParams
                    {
                        SenderClientId = context.SenderId
                    }
                }
            };
            RpcMessageHelpers.Handle(ref context, ref Metadata, ref ReadBuffer, ref rpcParams);
        }
    }

    internal struct ClientRpcMessage : INetworkMessage
    {
        public int Version => 0;

        public RpcMetadata Metadata;

        public FastBufferWriter WriteBuffer;
        public FastBufferReader ReadBuffer;

        public void Serialize(FastBufferWriter writer, int targetVersion)
        {
            RpcMessageHelpers.Serialize(ref writer, ref Metadata, ref WriteBuffer);
        }

        public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
        {
            return RpcMessageHelpers.Deserialize(ref reader, ref context, ref Metadata, ref ReadBuffer);
        }

        public void Handle(ref NetworkContext context)
        {
            var rpcParams = new __RpcParams
            {
                Client = new ClientRpcParams
                {
                    Receive = new ClientRpcReceiveParams
                    {
                    }
                }
            };
            RpcMessageHelpers.Handle(ref context, ref Metadata, ref ReadBuffer, ref rpcParams);
        }
    }

    internal struct RpcMessage : INetworkMessage
    {
        public int Version => 0;

        public RpcMetadata Metadata;
        public ulong SenderClientId;

        public FastBufferWriter WriteBuffer;
        public FastBufferReader ReadBuffer;

        public unsafe void Serialize(FastBufferWriter writer, int targetVersion)
        {
            BytePacker.WriteValuePacked(writer, SenderClientId);
            RpcMessageHelpers.Serialize(ref writer, ref Metadata, ref WriteBuffer);
        }

        public unsafe bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
        {
            ByteUnpacker.ReadValuePacked(reader, out SenderClientId);
            return RpcMessageHelpers.Deserialize(ref reader, ref context, ref Metadata, ref ReadBuffer);
        }

        public void Handle(ref NetworkContext context)
        {
            var rpcParams = new __RpcParams
            {
                Ext = new RpcParams
                {
                    Receive = new RpcReceiveParams
                    {
                        SenderClientId = SenderClientId
                    }
                }
            };
            RpcMessageHelpers.Handle(ref context, ref Metadata, ref ReadBuffer, ref rpcParams);
        }
    }
}
