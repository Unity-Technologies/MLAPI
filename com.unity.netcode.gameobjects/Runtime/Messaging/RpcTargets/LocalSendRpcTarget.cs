using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Netcode
{
    internal class LocalSendRpcTarget : BaseRpcTarget
    {
        public override void Dispose()
        {

        }

        internal override void Send(NetworkBehaviour behaviour, ref RpcMessage message, NetworkDelivery delivery, RpcParams rpcParams)
        {
            var networkManager = behaviour.NetworkManager;
            var context = new NetworkContext
            {
                SenderId = m_NetworkManager.LocalClientId,
                Timestamp = networkManager.RealTimeProvider.RealTimeSinceStartup,
                SystemOwner = networkManager,
                // header information isn't valid since it's not a real message.
                // RpcMessage doesn't access this stuff so it's just left empty.
                Header = new NetworkMessageHeader(),
                SerializedHeaderSize = 0,
                MessageSize = 0
            };
            int length;
            if (rpcParams.Send.LocalDeferMode == LocalDeferMode.Defer)
            {
                using var serializedWriter = new FastBufferWriter(message.WriteBuffer.Length + UnsafeUtility.SizeOf<RpcMetadata>(), Allocator.Temp, int.MaxValue);
                message.Serialize(serializedWriter, message.Version);
                using var reader = new FastBufferReader(serializedWriter, Allocator.None);
                context.Header = new NetworkMessageHeader
                {
                    MessageSize = (uint)reader.Length,
                    MessageType = m_NetworkManager.MessageManager.GetMessageType(typeof(RpcMessage))
                };

                networkManager.DeferredMessageManager.DeferMessage(IDeferredNetworkMessageManager.TriggerType.OnNextFrame, 0, reader, ref context);
                length = reader.Length;
            }
            else
            {
                using var tempBuffer = new FastBufferReader(message.WriteBuffer, Allocator.None);
                message.ReadBuffer = tempBuffer;
                message.Handle(ref context);
                length = tempBuffer.Length;
            }
#if DEVELOPMENT_BUILD || UNITY_EDITOR || UNITY_MP_TOOLS_NET_STATS_MONITOR_ENABLED_IN_RELEASE
            if (NetworkBehaviour.__rpc_name_table[behaviour.GetType()].TryGetValue(message.Metadata.NetworkRpcMethodId, out var rpcMethodName))
            {
                networkManager.NetworkMetrics.TrackRpcSent(
                    networkManager.LocalClientId,
                    behaviour.NetworkObject,
                    rpcMethodName,
                    behaviour.__getTypeName(),
                    length);
            }
#endif
        }

        internal LocalSendRpcTarget(NetworkManager manager) : base(manager)
        {

        }
    }
}
