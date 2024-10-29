namespace Unity.Netcode
{
    internal class ClientsAndHostRpcTarget : BaseRpcTarget
    {
        private BaseRpcTarget m_UnderlyingTarget;

        public override void Dispose()
        {
            m_UnderlyingTarget = null;
        }

        internal override void Send(NetworkBehaviour behaviour, ref RpcMessage message, NetworkDelivery delivery, RpcParams rpcParams)
        {
            if (m_UnderlyingTarget == null)
            {
                // NotServer treats a host as being a server and will not send to it
                // ClientsAndHost sends to everyone who runs any client logic
                // So if the server is a host, this target includes it (as hosts run client logic)
                // If the server is not a host, this target leaves it out, ergo the selection of NotServer.
                // If we are in distributed authority mode and connected to a service, then send to all clients.
                if (behaviour.NetworkManager.ServerIsHost || (m_NetworkManager.DistributedAuthorityMode && m_NetworkManager.CMBServiceConnection))
                {
                    m_UnderlyingTarget = behaviour.RpcTarget.Everyone;
                }
                else
                {
                    m_UnderlyingTarget = behaviour.RpcTarget.NotServer;
                }
            }

            m_UnderlyingTarget.Send(behaviour, ref message, delivery, rpcParams);
        }

        internal ClientsAndHostRpcTarget(NetworkManager manager) : base(manager)
        {
        }
    }
}
