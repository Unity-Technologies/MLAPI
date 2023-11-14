using System.Collections.Generic;
using Unity.Collections;

namespace Unity.Netcode
{
    /// <summary>
    /// Configuration for the default method by which an RPC is communicated across the network
    /// </summary>
    public enum SendTo
    {
        /// <summary>
        /// Send to the NetworkObject's current owner
        /// Will execute locally if the local process is the owner
        /// </summary>
        Owner,
        /// <summary>
        /// Send to everyone but the current owner, filtered to the current observer list
        /// Will execute locally if the local process is not the owner
        /// </summary>
        NotOwner,
        /// <summary>
        /// Send to the server, regardless of ownership
        /// Will execute locally if invoked on the server
        /// </summary>
        Server,
        /// <summary>
        /// Send to everyone but the server, filtered to the current observer list
        /// Will execute locally if invoked on a client
        /// Will NOT execute locally if invoked on a server running in host mode
        /// </summary>
        NotServer,
        /// <summary>
        /// Execute this RPC locally
        ///
        /// Normally this is no different from a standard function call.
        ///
        /// Using the DeferLocal parameter of the attribute or the LocalDeferMode override in RpcSendParams,
        /// this can allow an RPC to be processed on localhost with a one-frame delay as if it were sent over
        /// the network.
        /// </summary>
        Me,
        /// <summary>
        /// Send this RPC to everyone but the local machine, filtered to the current observer list
        /// </summary>
        NotMe,
        /// <summary>
        /// Send this RPC to everone, filtered to the current observer list
        /// Will execute locally
        /// </summary>
        Everyone,
        /// <summary>
        /// Send this RPC to all clients, including the host.
        /// If the server is running in host mode, this is the same as SendTo.Everyone
        /// If the server is running in dedicated server mode, this is the same as SendTo.NotServer
        /// </summary>
        ClientsAndHost,
        /// <summary>
        /// This RPC cannot be sent without passing in a target in RpcSendParams
        /// </summary>
        SpecifiedInParams
    }

    /// <summary>
    /// Implementations of the various <see cref="SendTo"/> options, as well as additional runtime-only options
    /// <see cref="Single"/>,
    /// <see cref="Group(NativeArray{ulong})"/>,
    /// <see cref="Group(NativeList{ulong})"/>,
    /// <see cref="Group(ulong[])"/>, and
    /// <see cref="Group{T}(T)"/>
    /// </summary>
    public class RpcTarget
    {
        private NetworkManager m_NetworkManager;
        internal RpcTarget(NetworkManager manager)
        {
            m_NetworkManager = manager;

            Everyone = new EveryoneRpcTarget(manager);
            Owner = new OwnerRpcTarget(manager);
            NotOwner = new NotOwnerRpcTarget(manager);
            Server = new ServerRpcTarget(manager);
            NotServer = new NotServerRpcTarget(manager);
            NotMe = new NotMeRpcTarget(manager);
            Me = new LocalSendRpcTarget(manager);
            ClientsAndHost = new ClientsAndHostRpcTarget(manager);

            m_CachedProxyRpcTargetGroup = new ProxyRpcTargetGroup(manager);
            m_CachedTargetGroup = new RpcTargetGroup(manager);
            m_CachedDirectSendTarget = new DirectSendRpcTarget(manager);
            m_CachedProxyRpcTarget = new ProxyRpcTarget(0, manager);
        }

        public void Dispose()
        {
            Everyone.Dispose();
            Owner.Dispose();
            NotOwner.Dispose();
            Server.Dispose();
            NotServer.Dispose();
            NotMe.Dispose();
            Me.Dispose();
            ClientsAndHost.Dispose();

            m_CachedProxyRpcTargetGroup.Dispose();
            m_CachedTargetGroup.Dispose();
            m_CachedDirectSendTarget.Dispose();
            m_CachedProxyRpcTarget.Dispose();
        }


        /// <summary>
        /// Send to the NetworkObject's current owner
        /// Will execute locally if the local process is the owner
        /// </summary>
        public BaseRpcTarget Owner;

        /// <summary>
        /// Send to everyone but the current owner, filtered to the current observer list
        /// Will execute locally if the local process is not the owner
        /// </summary>
        public BaseRpcTarget NotOwner;

        /// <summary>
        /// Send to the server, regardless of ownership
        /// Will execute locally if invoked on the server
        /// </summary>
        public BaseRpcTarget Server;

        /// <summary>
        /// Send to everyone but the server, filtered to the current observer list
        /// Will execute locally if invoked on a client
        /// Will NOT execute locally if invoked on a server running in host mode
        /// </summary>
        public BaseRpcTarget NotServer;

        /// <summary>
        /// Execute this RPC locally
        ///
        /// Normally this is no different from a standard function call.
        ///
        /// Using the DeferLocal parameter of the attribute or the LocalDeferMode override in RpcSendParams,
        /// this can allow an RPC to be processed on localhost with a one-frame delay as if it were sent over
        /// the network.
        /// </summary>
        public BaseRpcTarget Me;

        /// <summary>
        /// Send this RPC to everyone but the local machine, filtered to the current observer list
        /// </summary>
        public BaseRpcTarget NotMe;

        /// <summary>
        /// Send this RPC to everone, filtered to the current observer list
        /// Will execute locally
        /// </summary>
        public BaseRpcTarget Everyone;

        /// <summary>
        /// Send this RPC to all clients, including the host.
        /// If the server is running in host mode, this is the same as SendTo.Everyone
        /// If the server is running in dedicated server mode, this is the same as SendTo.NotServer
        /// </summary>
        public BaseRpcTarget ClientsAndHost;

        /// <summary>
        /// Send to a specific single client ID.
        ///
        /// Do not cache or reuse the result of this method.
        /// For performance reasons, the same object is used each time to avoid garbage-collected allocations,
        /// and its contents are simply changed.
        /// </summary>
        /// <param name="clientId"></param>
        /// <returns></returns>
        public BaseRpcTarget Single(ulong clientId)
        {
            if (clientId == m_NetworkManager.LocalClientId)
            {
                return Me;
            }

            if (m_NetworkManager.IsServer || clientId == NetworkManager.ServerClientId)
            {
                m_CachedDirectSendTarget.SetClientId(clientId);
                return m_CachedDirectSendTarget;
            }

            m_CachedProxyRpcTarget.SetClientId(clientId);
            return m_CachedProxyRpcTarget;
        }

        /// <summary>
        /// Sends to a group of client IDs.
        /// NativeArrays can be trivially constructed using Allocator.Temp, making this an efficient
        /// Group method if the group list is dynamically constructed.
        ///
        /// Do not cache or reuse the result of this method.
        /// For performance reasons, the same object is used each time to avoid garbage-collected allocations,
        /// and its contents are simply changed.
        /// </summary>
        /// <param name="clientIds"></param>
        /// <returns></returns>
        public BaseRpcTarget Group(NativeArray<ulong> clientIds)
        {
            IGroupRpcTarget target;
            if (m_NetworkManager.IsServer)
            {
                target = m_CachedTargetGroup;
            }
            else
            {
                target = m_CachedProxyRpcTargetGroup;
            }
            target.Clear();
            foreach (var clientId in clientIds)
            {
                target.Add(clientId);
            }

            return target.Target;
        }

        /// <summary>
        /// Sends to a group of client IDs.
        /// NativeList can be trivially constructed using Allocator.Temp, making this an efficient
        /// Group method if the group list is dynamically constructed.
        ///
        /// Do not cache or reuse the result of this method.
        /// For performance reasons, the same object is used each time to avoid garbage-collected allocations,
        /// and its contents are simply changed.
        /// </summary>
        /// <param name="clientIds"></param>
        /// <returns></returns>
        public BaseRpcTarget Group(NativeList<ulong> clientIds)
        {
            return Group(clientIds.AsArray());
        }

        /// <summary>
        /// Sends to a group of client IDs.
        /// Constructing arrays requires garbage collected allocations. This override is only recommended
        /// if you either have no strict performance requirements, or have the group of client IDs cached so
        /// it is not created each time.
        ///
        /// Do not cache or reuse the result of this method.
        /// For performance reasons, the same object is used each time to avoid garbage-collected allocations,
        /// and its contents are simply changed.
        /// </summary>
        /// <param name="clientIds"></param>
        /// <returns></returns>
        public BaseRpcTarget Group(ulong[] clientIds)
        {
            return Group(new NativeArray<ulong>(clientIds, Allocator.Temp));
        }


        /// <summary>
        /// Sends to a group of client IDs.
        /// This accepts any IEnumerable type, such as List&lt;ulong&gt;, but cannot be called without
        /// a garbage collected allocation (even if the type itself is a struct type, due to boxing).
        /// This override is only recommended if you either have no strict performance requirements,
        /// or have the group of client IDs cached so it is not created each time.
        ///
        /// Do not cache or reuse the result of this method.
        /// For performance reasons, the same object is used each time to avoid garbage-collected allocations,
        /// and its contents are simply changed.
        /// </summary>
        /// <param name="clientIds"></param>
        /// <returns></returns>
        public BaseRpcTarget Group<T>(T clientIds) where T : IEnumerable<ulong>
        {
            IGroupRpcTarget target;
            if (m_NetworkManager.IsServer)
            {
                target = m_CachedTargetGroup;
            }
            else
            {
                target = m_CachedProxyRpcTargetGroup;
            }
            target.Clear();
            foreach (var clientId in clientIds)
            {
                target.Add(clientId);
            }

            return target.Target;
        }

        private ProxyRpcTargetGroup m_CachedProxyRpcTargetGroup;
        private RpcTargetGroup m_CachedTargetGroup;
        private DirectSendRpcTarget m_CachedDirectSendTarget;
        private ProxyRpcTarget m_CachedProxyRpcTarget;
    }
}