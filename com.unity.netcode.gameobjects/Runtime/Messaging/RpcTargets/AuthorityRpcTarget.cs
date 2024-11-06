namespace Unity.Netcode
{
    internal class AuthorityRpcTarget : ServerRpcTarget
    {
        private ProxyRpcTarget m_AuthorityTarget;
        private DirectSendRpcTarget m_DirectSendTarget;

        public override void Dispose()
        {
            if (m_AuthorityTarget != null)
            {
                m_AuthorityTarget.Dispose();
                m_AuthorityTarget = null;
            }

            if (m_DirectSendTarget != null)
            {
                m_DirectSendTarget.Dispose();
                m_DirectSendTarget = null;
            }

            base.Dispose();
        }

        internal override void Send(NetworkBehaviour behaviour, ref RpcMessage message, NetworkDelivery delivery, RpcParams rpcParams)
        {
            if (behaviour.NetworkManager.DistributedAuthorityMode)
            {
                // If invoked locally, then send locally
                if (behaviour.HasAuthority)
                {
                    if (m_UnderlyingTarget == null)
                    {
                        m_UnderlyingTarget = new LocalSendRpcTarget(m_NetworkManager);
                    }
                    m_UnderlyingTarget.Send(behaviour, ref message, delivery, rpcParams);
                }
                else if (behaviour.NetworkManager.DAHost)
                {
                    if (m_DirectSendTarget == null)
                    {
                        m_DirectSendTarget = new DirectSendRpcTarget(behaviour.OwnerClientId, m_NetworkManager);
                    }
                    else
                    {
                        m_DirectSendTarget.ClientId = behaviour.OwnerClientId;
                    }
                    m_DirectSendTarget.Send(behaviour, ref message, delivery, rpcParams);
                }
                else // Otherwise (for now), we always proxy the RPC messages to the owner
                {
                    if (m_AuthorityTarget == null)
                    {
                        m_AuthorityTarget = new ProxyRpcTarget(behaviour.OwnerClientId, m_NetworkManager);
                    }
                    else
                    {
                        // Since the owner can change, for now we will just clear and set the owner each time
                        m_AuthorityTarget.SetClientId(behaviour.OwnerClientId);
                    }
                    m_AuthorityTarget.Send(behaviour, ref message, delivery, rpcParams);
                }
            }
            else
            {
                // If we are not in distributed authority mode, then we invoke the normal ServerRpc code.
                base.Send(behaviour, ref message, delivery, rpcParams);
            }
        }

        internal AuthorityRpcTarget(NetworkManager manager) : base(manager)
        {
        }
    }
}
