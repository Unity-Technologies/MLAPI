using System;
using System.Collections.Generic;
using Unity.Collections;

namespace Unity.Netcode
{
    internal class ProxyRpcTargetGroup : BaseRpcTarget, IDisposable, IGroupRpcTarget
    {
        public BaseRpcTarget Target => this;

        private ServerRpcTarget m_ServerRpcTarget;
        private LocalSendRpcTarget m_LocalSendRpcTarget;

        private bool m_Disposed;
        public NativeList<ulong> TargetClientIds;
        internal HashSet<ulong> Ids = new HashSet<ulong>();

        internal override void Send(NetworkBehaviour behaviour, ref RpcMessage message, NetworkDelivery delivery, RpcParams rpcParams)
        {
            // If there are no targets then don't attempt to send anything.
            if (TargetClientIds.Length == 0 && Ids.Count == 0)
            {
                return;
            }
            var proxyMessage = new ProxyMessage { Delivery = delivery, TargetClientIds = TargetClientIds.AsArray(), WrappedMessage = message };
#if DEVELOPMENT_BUILD || UNITY_EDITOR || UNITY_MP_TOOLS_NET_STATS_MONITOR_ENABLED_IN_RELEASE
            var size =
#endif
                behaviour.NetworkManager.MessageManager.SendMessage(ref proxyMessage, delivery, NetworkManager.ServerClientId);

#if DEVELOPMENT_BUILD || UNITY_EDITOR || UNITY_MP_TOOLS_NET_STATS_MONITOR_ENABLED_IN_RELEASE
            if (NetworkBehaviour.__rpc_name_table[behaviour.GetType()].TryGetValue(message.Metadata.NetworkRpcMethodId, out var rpcMethodName))
            {
                foreach (var clientId in TargetClientIds)
                {
                    behaviour.NetworkManager.NetworkMetrics.TrackRpcSent(
                        clientId,
                        behaviour.NetworkObject,
                        rpcMethodName,
                        behaviour.__getTypeName(),
                        size);
                }
            }
#endif
            if (Ids.Contains(NetworkManager.ServerClientId))
            {
                m_ServerRpcTarget.Send(behaviour, ref message, delivery, rpcParams);
            }
            if (Ids.Contains(m_NetworkManager.LocalClientId))
            {
                m_LocalSendRpcTarget.Send(behaviour, ref message, delivery, rpcParams);
            }
        }

        internal ProxyRpcTargetGroup(NetworkManager manager) : base(manager)
        {
            TargetClientIds = new NativeList<ulong>(Allocator.Persistent);
            m_ServerRpcTarget = new ServerRpcTarget(manager);
            m_LocalSendRpcTarget = new LocalSendRpcTarget(manager);
        }

        public override void Dispose()
        {
            CheckLockBeforeDispose();
            if (!m_Disposed)
            {
                TargetClientIds.Dispose();
                m_Disposed = true;
                m_ServerRpcTarget.Dispose();
                m_LocalSendRpcTarget.Dispose();
            }
        }

        public void Add(ulong clientId)
        {
            if (!Ids.Contains(clientId))
            {
                Ids.Add(clientId);
                if (clientId != NetworkManager.ServerClientId && clientId != m_NetworkManager.LocalClientId)
                {
                    TargetClientIds.Add(clientId);
                }
            }
        }

        public void Remove(ulong clientId)
        {
            Ids.Remove(clientId);
            for (var i = 0; i < TargetClientIds.Length; ++i)
            {
                if (TargetClientIds[i] == clientId)
                {
                    TargetClientIds.RemoveAt(i);
                    break;
                }
            }
        }

        public void Clear()
        {
            Ids.Clear();
            TargetClientIds.Clear();
        }
    }
}
