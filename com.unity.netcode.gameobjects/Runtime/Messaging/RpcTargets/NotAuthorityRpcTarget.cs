namespace Unity.Netcode
{
    internal class NotAuthorityRpcTarget : NotServerRpcTarget
    {
        internal override void Send(NetworkBehaviour behaviour, ref RpcMessage message, NetworkDelivery delivery, RpcParams rpcParams)
        {
            var networkObject = behaviour.NetworkObject;
            if (m_NetworkManager.DistributedAuthorityMode)
            {
                if (m_GroupSendTarget == null)
                {
                    // When mocking the CMB service, we are running a server so create a non-proxy target group
                    if (m_NetworkManager.DAHost)
                    {
                        m_GroupSendTarget = new RpcTargetGroup(m_NetworkManager);
                    }
                    else // Otherwise (for now), we always proxy the RPC messages
                    {
                        m_GroupSendTarget = new ProxyRpcTargetGroup(m_NetworkManager);
                    }
                }
                m_GroupSendTarget.Clear();

                if (behaviour.HasAuthority)
                {
                    foreach (var clientId in networkObject.Observers)
                    {
                        if (clientId == behaviour.OwnerClientId)
                        {
                            continue;
                        }

                        // The CMB-Service holds ID 0 and should not be added to the targets
                        if (clientId == NetworkManager.ServerClientId && m_NetworkManager.CMBServiceConnection)
                        {
                            continue;
                        }
                        m_GroupSendTarget.Add(clientId);
                    }
                }
                else
                {
                    foreach (var clientId in m_NetworkManager.ConnectedClientsIds)
                    {
                        if (clientId == behaviour.OwnerClientId || !networkObject.Observers.Contains(clientId))
                        {
                            continue;
                        }

                        // The CMB-Service holds ID 0 and should not be added to the targets
                        if (clientId == NetworkManager.ServerClientId && m_NetworkManager.CMBServiceConnection)
                        {
                            continue;
                        }

                        if (clientId == m_NetworkManager.LocalClientId)
                        {
                            m_LocalSendRpcTarget.Send(behaviour, ref message, delivery, rpcParams);
                            continue;
                        }
                        m_GroupSendTarget.Add(clientId);
                    }
                }

                m_GroupSendTarget.Target.Send(behaviour, ref message, delivery, rpcParams);
            }
            else
            {
                base.Send(behaviour, ref message, delivery, rpcParams);
            }
        }

        internal NotAuthorityRpcTarget(NetworkManager manager) : base(manager)
        {
        }
    }
}
