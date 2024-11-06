namespace Unity.Netcode
{
    internal struct ClientConnectedMessage : INetworkMessage, INetworkSerializeByMemcpy
    {
        public int Version => 0;

        public ulong ClientId;

        public bool ShouldSynchronize;


        public void Serialize(FastBufferWriter writer, int targetVersion)
        {
            BytePacker.WriteValueBitPacked(writer, ClientId);
            writer.WriteValueSafe(ShouldSynchronize);
        }

        public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            if (!networkManager.IsClient)
            {
                return false;
            }
            ByteUnpacker.ReadValueBitPacked(reader, out ClientId);
            reader.ReadValueSafe(out ShouldSynchronize);

            return true;
        }

        public void Handle(ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            if (ShouldSynchronize && networkManager.NetworkConfig.EnableSceneManagement && networkManager.DistributedAuthorityMode && networkManager.LocalClient.IsSessionOwner)
            {
                networkManager.SceneManager.SynchronizeNetworkObjects(ClientId);
            }
            else
            {
                // All modes support adding NetworkClients
                networkManager.ConnectionManager.AddClient(ClientId);
            }
            if (!networkManager.ConnectionManager.ConnectedClientIds.Contains(ClientId))
            {
                networkManager.ConnectionManager.ConnectedClientIds.Add(ClientId);
            }
            if (networkManager.IsConnectedClient)
            {
                networkManager.ConnectionManager.InvokeOnPeerConnectedCallback(ClientId);
            }

            // DANGO-TODO: Remove the session owner object distribution check once the service handles object distribution
            if (networkManager.DistributedAuthorityMode && networkManager.CMBServiceConnection && !networkManager.NetworkConfig.EnableSceneManagement)
            {
                // Don't redistribute for the local instance
                if (ClientId != networkManager.LocalClientId)
                {
                    // Show any NetworkObjects that are:
                    // - Hidden from the session owner
                    // - Owned by this client
                    // - Has NetworkObject.SpawnWithObservers set to true (the default)
                    if (!networkManager.LocalClient.IsSessionOwner)
                    {
                        networkManager.SpawnManager.ShowHiddenObjectsToNewlyJoinedClient(ClientId);
                    }

                    // We defer redistribution to the end of the NetworkUpdateStage.PostLateUpdate
                    networkManager.RedistributeToClient = true;
                    networkManager.ClientToRedistribute = ClientId;
                    networkManager.TickToRedistribute = networkManager.ServerTime.Tick + 20;
                }
            }
        }
    }
}
