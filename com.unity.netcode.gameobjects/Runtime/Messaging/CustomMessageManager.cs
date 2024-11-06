using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// The manager class to manage custom messages, note that this is different from the NetworkManager custom messages.
    /// These are named and are much easier to use.
    /// </summary>
    public class CustomMessagingManager
    {
        private readonly NetworkManager m_NetworkManager;

        internal CustomMessagingManager(NetworkManager networkManager)
        {
            m_NetworkManager = networkManager;
        }

        /// <summary>
        /// Delegate used for incoming unnamed messages
        /// </summary>
        /// <param name="clientId">The clientId that sent the message</param>
        /// <param name="reader">The stream containing the message data</param>
        public delegate void UnnamedMessageDelegate(ulong clientId, FastBufferReader reader);

        /// <summary>
        /// Event invoked when unnamed messages arrive
        /// </summary>
        public event UnnamedMessageDelegate OnUnnamedMessage;

        internal void InvokeUnnamedMessage(ulong clientId, FastBufferReader reader, int serializedHeaderSize)
        {
            if (OnUnnamedMessage != null)
            {
                var pos = reader.Position;
                var delegates = OnUnnamedMessage.GetInvocationList();
                foreach (var handler in delegates)
                {
                    reader.Seek(pos);
                    ((UnnamedMessageDelegate)handler).Invoke(clientId, reader);
                }
            }
            m_NetworkManager.NetworkMetrics.TrackUnnamedMessageReceived(clientId, reader.Length + serializedHeaderSize);
        }

        /// <summary>
        /// Sends unnamed message to all clients
        /// </summary>
        /// <param name="messageBuffer">The message stream containing the data</param>
        /// <param name="networkDelivery">The delivery type (QoS) to send data with</param>
        public void SendUnnamedMessageToAll(FastBufferWriter messageBuffer, NetworkDelivery networkDelivery = NetworkDelivery.ReliableSequenced)
        {
            SendUnnamedMessage(m_NetworkManager.ConnectedClientsIds, messageBuffer, networkDelivery);
        }

        /// <summary>
        /// Sends unnamed message to a list of clients
        /// </summary>
        /// <param name="clientIds">The clients to send to, sends to everyone if null</param>
        /// <param name="messageBuffer">The message stream containing the data</param>
        /// <param name="networkDelivery">The delivery type (QoS) to send data with</param>
        public void SendUnnamedMessage(IReadOnlyList<ulong> clientIds, FastBufferWriter messageBuffer, NetworkDelivery networkDelivery = NetworkDelivery.ReliableSequenced)
        {
            if (clientIds == null)
            {
                throw new ArgumentNullException(nameof(clientIds), "You must pass in a valid clientId List!");
            }

            if (!m_NetworkManager.DistributedAuthorityMode && !m_NetworkManager.IsServer)
            {
                if (clientIds.Count > 1 || (clientIds.Count == 1 && clientIds[0] != NetworkManager.ServerClientId))
                {
                    Debug.LogError("Clients cannot send unnamed messages to other clients!");
                    return;
                }
                else if (clientIds.Count == 1)
                {
                    SendUnnamedMessage(clientIds[0], messageBuffer, networkDelivery);
                }
            }
            else if (m_NetworkManager.DistributedAuthorityMode && !m_NetworkManager.DAHost)
            {
                if (clientIds.Count > 1)
                {
                    Debug.LogError("Sending an unnamed message to multiple clients is not yet supported in distributed authority.");
                    return;
                }
            }

            if (clientIds.Count == 0)
            {
                Debug.LogError($"{nameof(clientIds)} is empty! No clients to send to.");
                return;
            }

            ValidateMessageSize(messageBuffer, networkDelivery, isNamed: false);

            if (m_NetworkManager.IsHost)
            {
                for (var i = 0; i < clientIds.Count; ++i)
                {
                    if (clientIds[i] == m_NetworkManager.LocalClientId)
                    {
                        InvokeUnnamedMessage(
                            m_NetworkManager.LocalClientId,
                            new FastBufferReader(messageBuffer, Allocator.None),
                            0
                        );
                    }
                }
            }
            var message = new UnnamedMessage
            {
                SendData = messageBuffer
            };
            var size = m_NetworkManager.ConnectionManager.SendMessage(ref message, networkDelivery, clientIds);

            // Size is zero if we were only sending the message to ourself in which case it isn't sent.
            if (size != 0)
            {
                m_NetworkManager.NetworkMetrics.TrackUnnamedMessageSent(clientIds, size);
            }
        }

        /// <summary>
        /// Sends a unnamed message to a specific client
        /// </summary>
        /// <param name="clientId">The client to send the message to</param>
        /// <param name="messageBuffer">The message stream containing the data</param>
        /// <param name="networkDelivery">The delivery type (QoS) to send data with</param>
        public void SendUnnamedMessage(ulong clientId, FastBufferWriter messageBuffer, NetworkDelivery networkDelivery = NetworkDelivery.ReliableSequenced)
        {
            ValidateMessageSize(messageBuffer, networkDelivery, isNamed: false);

            if (m_NetworkManager.IsHost)
            {
                if (clientId == m_NetworkManager.LocalClientId)
                {
                    InvokeUnnamedMessage(
                        m_NetworkManager.LocalClientId,
                        new FastBufferReader(messageBuffer, Allocator.None),
                        0
                    );
                    return;
                }
            }
            var message = new UnnamedMessage
            {
                SendData = messageBuffer
            };
            var size = m_NetworkManager.ConnectionManager.SendMessage(ref message, networkDelivery, clientId);
            // Size is zero if we were only sending the message to ourself in which case it isn't sent.
            if (size != 0)
            {
                m_NetworkManager.NetworkMetrics.TrackUnnamedMessageSent(clientId, size);
            }
        }

        /// <summary>
        /// Delegate used to handle named messages
        /// </summary>
        public delegate void HandleNamedMessageDelegate(ulong senderClientId, FastBufferReader messagePayload);

        private Dictionary<ulong, HandleNamedMessageDelegate> m_NamedMessageHandlers32 = new Dictionary<ulong, HandleNamedMessageDelegate>();
        private Dictionary<ulong, HandleNamedMessageDelegate> m_NamedMessageHandlers64 = new Dictionary<ulong, HandleNamedMessageDelegate>();

        private Dictionary<ulong, string> m_MessageHandlerNameLookup32 = new Dictionary<ulong, string>();
        private Dictionary<ulong, string> m_MessageHandlerNameLookup64 = new Dictionary<ulong, string>();

        internal void InvokeNamedMessage(ulong hash, ulong sender, FastBufferReader reader, int serializedHeaderSize)
        {
            var bytesCount = reader.Length + serializedHeaderSize;

            if (m_NetworkManager == null)
            {
                // We dont know what size to use. Try every (more collision prone)
                if (m_NamedMessageHandlers32.TryGetValue(hash, out HandleNamedMessageDelegate messageHandler32))
                {
                    messageHandler32(sender, reader);
                    m_NetworkManager.NetworkMetrics.TrackNamedMessageReceived(sender, m_MessageHandlerNameLookup32[hash], bytesCount);
                }

                if (m_NamedMessageHandlers64.TryGetValue(hash, out HandleNamedMessageDelegate messageHandler64))
                {
                    messageHandler64(sender, reader);
                    m_NetworkManager.NetworkMetrics.TrackNamedMessageReceived(sender, m_MessageHandlerNameLookup64[hash], bytesCount);
                }
            }
            else
            {
                // Only check the right size.
                switch (m_NetworkManager.NetworkConfig.RpcHashSize)
                {
                    case HashSize.VarIntFourBytes:
                        if (m_NamedMessageHandlers32.TryGetValue(hash, out HandleNamedMessageDelegate messageHandler32))
                        {
                            messageHandler32(sender, reader);
                            m_NetworkManager.NetworkMetrics.TrackNamedMessageReceived(sender, m_MessageHandlerNameLookup32[hash], bytesCount);
                        }
                        break;
                    case HashSize.VarIntEightBytes:
                        if (m_NamedMessageHandlers64.TryGetValue(hash, out HandleNamedMessageDelegate messageHandler64))
                        {
                            messageHandler64(sender, reader);
                            m_NetworkManager.NetworkMetrics.TrackNamedMessageReceived(sender, m_MessageHandlerNameLookup64[hash], bytesCount);
                        }
                        break;
                }
            }
        }

        /// <summary>
        /// Registers a named message handler delegate.
        /// </summary>
        /// <param name="name">Name of the message.</param>
        /// <param name="callback">The callback to run when a named message is received.</param>
        public void RegisterNamedMessageHandler(string name, HandleNamedMessageDelegate callback)
        {
            if (string.IsNullOrEmpty(name))
            {
                if (m_NetworkManager.LogLevel <= LogLevel.Error)
                {
                    Debug.LogError($"[{nameof(RegisterNamedMessageHandler)}] Cannot register a named message of type null or empty!");
                }
                return;
            }
            var hash32 = XXHash.Hash32(name);
            var hash64 = XXHash.Hash64(name);

            if (m_NetworkManager.LogLevel <= LogLevel.Developer)
            {
                if (m_MessageHandlerNameLookup32.ContainsKey(hash32) || m_MessageHandlerNameLookup64.ContainsKey(hash64))
                {
                    Debug.LogWarning($"Registering {name} named message over existing registration! Your previous registration's callback is being overwritten!");
                }
            }

            m_NamedMessageHandlers32[hash32] = callback;
            m_NamedMessageHandlers64[hash64] = callback;

            m_MessageHandlerNameLookup32[hash32] = name;
            m_MessageHandlerNameLookup64[hash64] = name;
        }

        /// <summary>
        /// Unregisters a named message handler.
        /// </summary>
        /// <param name="name">The name of the message.</param>
        public void UnregisterNamedMessageHandler(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                if (m_NetworkManager.LogLevel <= LogLevel.Error)
                {
                    Debug.LogError($"[{nameof(UnregisterNamedMessageHandler)}] Cannot unregister a named message of type null or empty!");
                }
                return;
            }

            var hash32 = XXHash.Hash32(name);
            var hash64 = XXHash.Hash64(name);

            m_NamedMessageHandlers32.Remove(hash32);
            m_NamedMessageHandlers64.Remove(hash64);

            m_MessageHandlerNameLookup32.Remove(hash32);
            m_MessageHandlerNameLookup64.Remove(hash64);
        }

        /// <summary>
        /// Sends a named message to all clients
        /// </summary>
        /// <param name="messageName">The message name to send</param>
        /// <param name="messageStream">The message stream containing the data</param>
        /// <param name="networkDelivery">The delivery type (QoS) to send data with</param>
        public void SendNamedMessageToAll(string messageName, FastBufferWriter messageStream, NetworkDelivery networkDelivery = NetworkDelivery.ReliableSequenced)
        {
            SendNamedMessage(messageName, m_NetworkManager.ConnectedClientsIds, messageStream, networkDelivery);
        }

        /// <summary>
        /// Sends a named message
        /// </summary>
        /// <param name="messageName">The message name to send</param>
        /// <param name="clientId">The client to send the message to</param>
        /// <param name="messageStream">The message stream containing the data</param>
        /// <param name="networkDelivery">The delivery type (QoS) to send data with</param>
        public void SendNamedMessage(string messageName, ulong clientId, FastBufferWriter messageStream, NetworkDelivery networkDelivery = NetworkDelivery.ReliableSequenced)
        {
            ValidateMessageSize(messageStream, networkDelivery, isNamed: true);

            ulong hash = 0;
            switch (m_NetworkManager.NetworkConfig.RpcHashSize)
            {
                case HashSize.VarIntFourBytes:
                    hash = XXHash.Hash32(messageName);
                    break;
                case HashSize.VarIntEightBytes:
                    hash = XXHash.Hash64(messageName);
                    break;
            }
            if (m_NetworkManager.IsHost)
            {
                if (clientId == m_NetworkManager.LocalClientId)
                {
                    InvokeNamedMessage(
                        hash,
                        m_NetworkManager.LocalClientId,
                        new FastBufferReader(messageStream, Allocator.None),
                        0
                    );

                    return;
                }
            }

            var message = new NamedMessage
            {
                Hash = hash,
                SendData = messageStream,
            };
            var size = m_NetworkManager.ConnectionManager.SendMessage(ref message, networkDelivery, clientId);

            // Size is zero if we were only sending the message to ourself in which case it isn't sent.
            if (size != 0)
            {
                m_NetworkManager.NetworkMetrics.TrackNamedMessageSent(clientId, messageName, size);
            }
        }

        /// <summary>
        /// Sends the named message
        /// </summary>
        /// <param name="messageName">The message name to send</param>
        /// <param name="clientIds">The clients to send to</param>
        /// <param name="messageStream">The message stream containing the data</param>
        /// <param name="networkDelivery">The delivery type (QoS) to send data with</param>
        public void SendNamedMessage(string messageName, IReadOnlyList<ulong> clientIds, FastBufferWriter messageStream, NetworkDelivery networkDelivery = NetworkDelivery.ReliableSequenced)
        {
            if (clientIds == null)
            {
                throw new ArgumentNullException(nameof(clientIds), "Client list is null! You must pass in a valid clientId list to send a named message.");
            }

            if (!m_NetworkManager.DistributedAuthorityMode && !m_NetworkManager.IsServer)
            {
                if (clientIds.Count > 1 || (clientIds.Count == 1 && clientIds[0] != NetworkManager.ServerClientId))
                {
                    Debug.LogError("Clients cannot send named messages to other clients!");
                    return;
                }
                else if (clientIds.Count == 1)
                {
                    SendNamedMessage(messageName, clientIds[0], messageStream, networkDelivery);
                    return;
                }
            }
            else if (m_NetworkManager.DistributedAuthorityMode && !m_NetworkManager.DAHost)
            {
                if (clientIds.Count > 1)
                {
                    Debug.LogError("Sending a named message to multiple clients is not yet supported in distributed authority.");
                    return;
                }
            }

            if (clientIds.Count == 0)
            {
                Debug.LogError($"{nameof(clientIds)} is empty! No clients to send the named message {messageName} to!");
                return;
            }

            ValidateMessageSize(messageStream, networkDelivery, isNamed: true);

            ulong hash = 0;
            switch (m_NetworkManager.NetworkConfig.RpcHashSize)
            {
                case HashSize.VarIntFourBytes:
                    hash = XXHash.Hash32(messageName);
                    break;
                case HashSize.VarIntEightBytes:
                    hash = XXHash.Hash64(messageName);
                    break;
            }
            if (m_NetworkManager.IsHost)
            {
                for (var i = 0; i < clientIds.Count; ++i)
                {
                    if (clientIds[i] == m_NetworkManager.LocalClientId)
                    {
                        InvokeNamedMessage(
                            hash,
                            m_NetworkManager.LocalClientId,
                            new FastBufferReader(messageStream, Allocator.None),
                            0
                        );
                    }
                }
            }
            var message = new NamedMessage
            {
                Hash = hash,
                SendData = messageStream
            };
            var size = m_NetworkManager.ConnectionManager.SendMessage(ref message, networkDelivery, clientIds);

            // Size is zero if we were only sending the message to ourself in which case it isn't sent.
            if (size != 0)
            {
                m_NetworkManager.NetworkMetrics.TrackNamedMessageSent(clientIds, messageName, size);
            }
        }

        /// <summary>
        /// Validate the size of the message. If it's a non-fragmented delivery type the message must fit within the
        /// max allowed size with headers also subtracted. Named messages also include the hash
        /// of the name string. Only validates in editor and development builds.
        /// </summary>
        /// <param name="messageStream">The named message payload</param>
        /// <param name="networkDelivery">Delivery method</param>
        /// <param name="isNamed">Is the message named (or unnamed)</param>
        /// <exception cref="OverflowException">Exception thrown in case validation fails</exception>
        private unsafe void ValidateMessageSize(FastBufferWriter messageStream, NetworkDelivery networkDelivery, bool isNamed)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            var maxNonFragmentedSize = m_NetworkManager.MessageManager.NonFragmentedMessageMaxSize - FastBufferWriter.GetWriteSize<NetworkMessageHeader>() - sizeof(NetworkBatchHeader);
            if (isNamed)
            {
                maxNonFragmentedSize -= sizeof(ulong); // MessageName hash
            }
            if (networkDelivery != NetworkDelivery.ReliableFragmentedSequenced
                && messageStream.Length > maxNonFragmentedSize)
            {
                throw new OverflowException($"Given message size ({messageStream.Length} bytes) is greater than " +
                    $"the maximum allowed for the selected delivery method ({maxNonFragmentedSize} bytes). Try using " +
                    $"ReliableFragmentedSequenced delivery method instead.");
            }
#endif
        }
    }
}
