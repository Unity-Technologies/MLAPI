namespace Unity.Netcode
{
    internal class EveryoneRpcTarget : BaseRpcTarget
    {
        private NotServerRpcTarget m_NotServerRpcTarget;
        private ServerRpcTarget m_ServerRpcTarget;
        private NotAuthorityRpcTarget m_NotAuthorityRpcTarget;
        private AuthorityRpcTarget m_AuthorityRpcTarget;

        public override void Dispose()
        {
            m_NotServerRpcTarget.Dispose();
            m_ServerRpcTarget.Dispose();
            m_NotAuthorityRpcTarget.Dispose();
            m_AuthorityRpcTarget.Dispose();
        }

        internal override void Send(NetworkBehaviour behaviour, ref RpcMessage message, NetworkDelivery delivery, RpcParams rpcParams)
        {
            if (NetworkManager.IsDistributedAuthority)
            {
                m_NotAuthorityRpcTarget.Send(behaviour, ref message, delivery, rpcParams);
                m_AuthorityRpcTarget.Send(behaviour, ref message, delivery, rpcParams);
            }
            else
            {
                m_NotServerRpcTarget.Send(behaviour, ref message, delivery, rpcParams);
                m_ServerRpcTarget.Send(behaviour, ref message, delivery, rpcParams);
            }
        }

        internal EveryoneRpcTarget(NetworkManager manager) : base(manager)
        {
            m_NotServerRpcTarget = new NotServerRpcTarget(manager);
            m_ServerRpcTarget = new ServerRpcTarget(manager);
            m_NotAuthorityRpcTarget = new NotAuthorityRpcTarget(manager);
            m_AuthorityRpcTarget = new AuthorityRpcTarget(manager);
        }
    }
}
