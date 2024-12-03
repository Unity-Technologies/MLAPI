using Unity.Collections;

namespace Unity.Netcode
{
    /// <summary>
    /// Only used when connecting to the distributed authority service
    /// </summary>
    internal struct ClientConfig : INetworkSerializable
    {
        public SessionConfig SessionConfig;
        public int SessionVersion => (int)SessionConfig.SessionVersion;
        public uint TickRate;
        public bool EnableSceneManagement;

        // Only gets deserialized but should never be used unless testing
        public int RemoteClientSessionVersion;

        public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
        {
            // Clients always write
            if (serializer.IsWriter)
            {
                var writer = serializer.GetFastBufferWriter();
                BytePacker.WriteValueBitPacked(writer, SessionVersion);
                BytePacker.WriteValueBitPacked(writer, TickRate);
                writer.WriteValueSafe(EnableSceneManagement);
            }
            else
            {
                var reader = serializer.GetFastBufferReader();
                ByteUnpacker.ReadValueBitPacked(reader, out RemoteClientSessionVersion);
                ByteUnpacker.ReadValueBitPacked(reader, out TickRate);
                reader.ReadValueSafe(out EnableSceneManagement);
            }
        }
    }

    internal struct ConnectionRequestMessage : INetworkMessage
    {
        internal const string InvalidSessionVersionMessage = "The client version is not compatible with the session version.";

        // This version update is unidirectional (client to service) and version
        // handling occurs on the service side. This serialized data is never sent
        // to a host or server.
        private const int k_SendClientConfigToService = 1;
        public int Version => k_SendClientConfigToService;

        public ulong ConfigHash;
        public bool DistributedAuthority;
        public ClientConfig ClientConfig;

        public byte[] ConnectionData;

        public bool ShouldSendConnectionData;

        public NativeArray<MessageVersionData> MessageVersions;

        public void Serialize(FastBufferWriter writer, int targetVersion)
        {
            // ============================================================
            // BEGIN FORBIDDEN SEGMENT
            // DO NOT CHANGE THIS HEADER. Everything added to this message
            // must go AFTER the message version header.
            // ============================================================
            BytePacker.WriteValueBitPacked(writer, MessageVersions.Length);
            foreach (var messageVersion in MessageVersions)
            {
                messageVersion.Serialize(writer);
            }
            // ============================================================
            // END FORBIDDEN SEGMENT
            // ============================================================

            if (DistributedAuthority)
            {
                writer.WriteNetworkSerializable(ClientConfig);
            }

            if (ShouldSendConnectionData)
            {
                writer.WriteValueSafe(ConfigHash);
                writer.WriteValueSafe(ConnectionData);
            }
            else
            {
                writer.WriteValueSafe(ConfigHash);
            }
        }

        public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            if (!networkManager.IsServer)
            {
                return false;
            }

            // ============================================================
            // BEGIN FORBIDDEN SEGMENT
            // DO NOT CHANGE THIS HEADER. Everything added to this message
            // must go AFTER the message version header.
            // ============================================================
            ByteUnpacker.ReadValueBitPacked(reader, out int length);
            for (var i = 0; i < length; ++i)
            {
                var messageVersion = new MessageVersionData();
                messageVersion.Deserialize(reader);
                networkManager.ConnectionManager.MessageManager.SetVersion(context.SenderId, messageVersion.Hash, messageVersion.Version);

                // Update the received version since this message will always be passed version 0, due to the map not
                // being initialized until just now.
                var messageType = networkManager.ConnectionManager.MessageManager.GetMessageForHash(messageVersion.Hash);
                if (messageType == typeof(ConnectionRequestMessage))
                {
                    receivedMessageVersion = messageVersion.Version;
                }
            }
            // ============================================================
            // END FORBIDDEN SEGMENT
            // ============================================================

            if (networkManager.DAHost)
            {
                reader.ReadNetworkSerializable(out ClientConfig);
            }

            if (networkManager.NetworkConfig.ConnectionApproval)
            {
                if (!reader.TryBeginRead(FastBufferWriter.GetWriteSize(ConfigHash) + FastBufferWriter.GetWriteSize<int>()))
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning($"Incomplete connection request message given config - possible {nameof(NetworkConfig)} mismatch.");
                    }

                    networkManager.DisconnectClient(context.SenderId);
                    return false;
                }

                reader.ReadValue(out ConfigHash);

                if (!networkManager.NetworkConfig.CompareConfig(ConfigHash))
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning($"{nameof(NetworkConfig)} mismatch. The configuration between the server and client does not match");
                    }

                    networkManager.DisconnectClient(context.SenderId);
                    return false;
                }

                reader.ReadValueSafe(out ConnectionData);
            }
            else
            {
                if (!reader.TryBeginRead(FastBufferWriter.GetWriteSize(ConfigHash)))
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning($"Incomplete connection request message.");
                    }

                    networkManager.DisconnectClient(context.SenderId);
                    return false;
                }
                reader.ReadValue(out ConfigHash);

                if (!networkManager.NetworkConfig.CompareConfig(ConfigHash))
                {
                    if (NetworkLog.CurrentLogLevel <= LogLevel.Normal)
                    {
                        NetworkLog.LogWarning($"{nameof(NetworkConfig)} mismatch. The configuration between the server and client does not match");
                    }

                    networkManager.DisconnectClient(context.SenderId);
                    return false;
                }
            }

            return true;
        }

        public void Handle(ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            var senderId = context.SenderId;

            // DAHost mocking the service logic to disconnect clients trying to connect with a lower session version
            if (networkManager.DAHost)
            {
                if (ClientConfig.RemoteClientSessionVersion < networkManager.SessionConfig.SessionVersion)
                {
                    //Disconnect with reason
                    networkManager.ConnectionManager.DisconnectClient(senderId, InvalidSessionVersionMessage);
                    return;
                }
            }

            if (networkManager.ConnectionManager.PendingClients.TryGetValue(senderId, out PendingClient client))
            {
                // Set to pending approval to prevent future connection requests from being approved
                client.ConnectionState = PendingClient.State.PendingApproval;
            }

            if (networkManager.NetworkConfig.ConnectionApproval)
            {
                var messageRequest = this;
                networkManager.ConnectionManager.ApproveConnection(ref messageRequest, ref context);
            }
            else
            {
                var response = new NetworkManager.ConnectionApprovalResponse
                {
                    Approved = true,
                    CreatePlayerObject = networkManager.DistributedAuthorityMode && networkManager.AutoSpawnPlayerPrefabClientSide ? false : networkManager.NetworkConfig.PlayerPrefab != null
                };
                networkManager.ConnectionManager.HandleConnectionApproval(senderId, response);
            }
        }
    }
}
