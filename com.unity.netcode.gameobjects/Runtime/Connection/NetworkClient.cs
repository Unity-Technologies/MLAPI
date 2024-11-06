using UnityEngine;

namespace Unity.Netcode
{

    /// <summary>
    /// A NetworkClient
    /// </summary>
    public class NetworkClient
    {
        /// <summary>
        /// Returns true if the session instance is considered a server
        /// </summary>
        internal bool IsServer { get; set; }

        /// <summary>
        /// Returns true if the session instance is considered a client
        /// </summary>
        internal bool IsClient { get; set; }

        /// <summary>
        /// Returns true if the session instance is considered a host
        /// </summary>
        internal bool IsHost => IsClient && IsServer;

        /// <summary>
        /// When true, the client is connected, approved, and synchronized with
        /// the server.
        /// </summary>
        internal bool IsConnected { get; set; }

        /// <summary>
        /// Is true when the client has been approved.
        /// </summary>
        internal bool IsApproved { get; set; }

        public NetworkTopologyTypes NetworkTopologyType { get; internal set; }

        public bool DAHost { get; internal set; }

        /// <summary>
        /// Is true when the client has been assigned session ownership in distributed authority mode
        /// </summary>
        public bool IsSessionOwner { get; internal set; }

        /// <summary>
        /// The ClientId of the NetworkClient
        /// </summary>
        public ulong ClientId;

        /// <summary>
        /// The PlayerObject of the Client
        /// </summary>
        public NetworkObject PlayerObject;

        /// <summary>
        /// The NetworkObject's owned by this client instance
        /// </summary>
        public NetworkObject[] OwnedObjects => IsConnected ? SpawnManager.GetClientOwnedObjects(ClientId) : new NetworkObject[] { };

        internal NetworkSpawnManager SpawnManager { get; private set; }

        internal bool SetRole(bool isServer, bool isClient, NetworkManager networkManager = null)
        {
            ResetClient(isServer, isClient);

            IsServer = isServer;
            IsClient = isClient;

            if (networkManager != null)
            {
                SpawnManager = networkManager.SpawnManager;
                NetworkTopologyType = networkManager.NetworkConfig.NetworkTopology;

                if (NetworkTopologyType == NetworkTopologyTypes.DistributedAuthority)
                {
                    DAHost = IsClient && IsServer;

                    // DANGO-TODO: We might allow a dedicated mock CMB server, but for now do not allow this
                    if (!IsClient && IsServer)
                    {
                        Debug.LogError("You cannot start NetworkManager as a server when operating in distributed authority mode!");
                        return false;
                    }

                    if (DAHost && networkManager.CMBServiceConnection)
                    {
                        Debug.LogError("You cannot start a host when connecting to a distributed authority CMB Service!");
                        return false;
                    }
                }
            }
            return true;
        }

        /// <summary>
        /// Only to be invoked when setting the role.
        /// This resets the current NetworkClient's properties.
        /// </summary>
        private void ResetClient(bool isServer, bool isClient)
        {
            // If we are niether client nor server, then reset properties (i.e. client has no role)
            if (!IsServer && !IsClient)
            {
                PlayerObject = null;
                ClientId = 0;
                IsConnected = false;
                IsApproved = false;
                SpawnManager = null;
                DAHost = false;
            }
        }

        internal void AssignPlayerObject(ref NetworkObject networkObject)
        {
            PlayerObject = networkObject;
        }
    }
}
