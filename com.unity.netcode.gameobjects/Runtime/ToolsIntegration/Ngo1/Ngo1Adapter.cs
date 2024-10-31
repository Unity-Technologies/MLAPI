using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Unity.Multiplayer.Tools.Common;
using Unity.Multiplayer.Tools.DataCollection;
using Unity.Multiplayer.Tools.Events;
using Unity.Multiplayer.Tools.NetStats;
using Unity.Netcode;
using UnityEngine;


namespace Unity.Multiplayer.Tools.Adapters.Ngo1
{
    internal class Ngo1Adapter

        : INetworkAdapter

        // Events
        // --------------------------------------------------------------------
        , IGetConnectedClients
        , IMetricCollectionEvent
        , IGetConnectionStatus

        // Queries
        // --------------------------------------------------------------------
        , IGetBandwidth
        , IGetClientId
        , IGetGameObject
        , IGetObjectIds
        , IGetOwnership
        , IGetRpcCount
    {
        [NotNull]
        private NetworkManager m_NetworkManager;

        private DefaultMetricCollection m_Collection;

        [MaybeNull]
        private NetworkSpawnManager SpawnManager => m_NetworkManager.SpawnManager;

        [MaybeNull]
        private Dictionary<ulong, NetworkObject> SpawnedObjects => SpawnManager?.SpawnedObjects;

        public Ngo1Adapter([NotNull] NetworkManager networkManager)
        {
            Debug.Assert(networkManager != null, $"The parameter {nameof(networkManager)} can't be null.");
            Init(networkManager);
        }

        private void Init(NetworkManager networkManager)
        {
            m_NetworkManager = networkManager;

            NetworkMetrics nwMetrics = GetOrAddNetworkMetrics();
            m_Collection = nwMetrics.Collector;
            nwMetrics.OnMetricsDispatched += NotifyObservers;

            m_NetworkManager.OnConnectionEvent += OnConnectionEvent;
            m_NetworkManager.NetworkTickSystem.Tick += OnTick;

            m_NetworkManager.OnServerStarted += OnServerOrClientStarted;
            m_NetworkManager.OnClientStarted += OnServerOrClientStarted;
            m_NetworkManager.OnServerStopped += OnServerOrClientStopped;
            m_NetworkManager.OnClientStopped += OnServerOrClientStopped;

            if (m_NetworkManager.IsConnectedClient || m_NetworkManager.IsServer)
            {
                OnServerOrClientStarted();
            }
        }

        internal void ReplaceNetworkManager(NetworkManager networkManager)
        {
            Debug.Assert(networkManager != null, $"The parameter {nameof(networkManager)} can't be null.");
            Deinitialize();
            Init(networkManager);
        }

        internal void Deinitialize()
        {
            if (m_NetworkManager == null)
            {
                return;
            }

            m_NetworkManager.OnConnectionEvent -= OnConnectionEvent;

            if (m_NetworkManager.NetworkTickSystem != null)
            {
                m_NetworkManager.NetworkTickSystem.Tick -= OnTick;
            }

            m_NetworkManager.OnServerStarted -= OnServerOrClientStarted;
            m_NetworkManager.OnClientStarted -= OnServerOrClientStarted;
            m_NetworkManager.OnServerStopped -= OnServerOrClientStopped;
            m_NetworkManager.OnClientStopped -= OnServerOrClientStopped;

            if (m_NetworkManager.NetworkMetrics is NetworkMetrics nm)
            {
                nm.OnMetricsDispatched -= NotifyObservers;
            }

        }

        private readonly List<ClientId> m_ClientIds = new();
        private readonly List<ObjectId> m_ObjectIds = new();

        private void OnTick()
        {
            RefreshObjectIds();
            RefreshClientIds();
        }

        private void RefreshObjectIds()
        {
            m_ObjectIds.Clear();
            var spawnedObjects = m_NetworkManager.SpawnManager?.SpawnedObjectsList;
            if (spawnedObjects == null)
            {
                return;
            }
            foreach (var spawnedObject in spawnedObjects)
            {
                m_ObjectIds.Add((ObjectId)spawnedObject.NetworkObjectId);
            }
        }

        private void RefreshClientIds()
        {
            if (m_NetworkManager.IsServer)
            {
                m_ClientIds.Clear();
                for (var index = 0; index < m_NetworkManager.ConnectedClientsIds.Count; index++)
                {
                    var clientId = m_NetworkManager.ConnectedClientsIds[index];
                    m_ClientIds.Add((ClientId) clientId);
                }
            }
            else if (m_NetworkManager.SpawnManager != null)
            {
                // NetworkManager.ConnectedClientsIds is only available on the server
                foreach (var (clientId, clientNetworkObjects) in m_NetworkManager.SpawnManager.OwnershipToObjectsTable)
                {
                    // Avoid polluting the client list because of DA.
                    // Only auto add already existing clients through SpawnManager that have at least one object in the scene.
                    if (!m_ClientIds.Contains((ClientId)clientId) && clientNetworkObjects.Count > 0)
                    {
                        m_ClientIds.Add((ClientId)clientId);
                        OnClientConnected(clientId);
                    }
                }
            }
        }

        public AdapterMetadata Metadata { get; } = new AdapterMetadata
        {
            PackageInfo = new PackageInfo
            {
                PackageName = "com.unity.netcode.gameobjects",
                Version = new PackageVersion
                {
                    Major = 1,
                    Minor = 0,
                    Patch = 0,
                    PreRelease = ""
                }
            }
        };

        public T GetComponent<T>() where T : class, IAdapterComponent
        {
            return this as T;
        }

        // Events
        // --------------------------------------------------------------------
        public IReadOnlyList<ClientId> ConnectedClients => m_ClientIds;
        public event Action<ClientId> ClientConnectionEvent;

        private void OnClientConnected(ulong clientId)
        {
            var typedClientId = (ClientId)clientId;
            if (!m_ClientIds.Contains(typedClientId))
            {
                m_ClientIds.Add(typedClientId);
            }
            ClientConnectionEvent?.Invoke(typedClientId);
        }

        public event Action<ClientId> ClientDisconnectionEvent;

        private void OnClientDisconnected(ulong clientId)
        {
            var typedClientId = (ClientId)clientId;
            m_ClientIds.RemoveAll(id => id == typedClientId);
            ClientDisconnectionEvent?.Invoke(typedClientId);
        }

        private void OnConnectionEvent(NetworkManager networkManager, ConnectionEventData clientConnectionData)
        {
            switch (clientConnectionData.EventType)
            {
                case ConnectionEvent.ClientConnected:
                case ConnectionEvent.PeerConnected:
                    OnClientConnected(clientConnectionData.ClientId);

                    // Adding clients already existing before we joined
                    foreach (var peerClientId in clientConnectionData.PeerClientIds)
                    {
                        OnClientConnected(peerClientId);
                    }
                    break;
                case ConnectionEvent.ClientDisconnected:
                case ConnectionEvent.PeerDisconnected:
                    OnClientDisconnected(clientConnectionData.ClientId);
                    break;
                default:
                    Debug.LogWarning("Unknown ConnectionEvent: " + clientConnectionData.EventType);
                    break;
            }
        }

        public event Action ServerOrClientStarted;
        public event Action ServerOrClientStopped;

        private void OnServerOrClientStarted()
        {
            // NetworkTickSystem is recreated every time the server or client is (re)started
            m_NetworkManager.NetworkTickSystem.Tick -= OnTick;
            m_NetworkManager.NetworkTickSystem.Tick += OnTick;
            ServerOrClientStarted?.Invoke();
        }

        private void OnServerOrClientStopped(bool isHost)
        {
            m_NetworkManager.NetworkTickSystem.Tick -= OnTick;
            ServerOrClientStopped?.Invoke();
        }

        public event Action<IMetricCollection> MetricCollectionEvent;

        private void NotifyObservers(IMetricCollection collection)
        {
            OnBandwidthUpdated?.Invoke();
            OnRpcCountUpdated?.Invoke();
            MetricCollectionEvent?.Invoke(collection);
        }

        // Simple Queries
        // --------------------------------------------------------------------
        public ClientId LocalClientId => (ClientId)m_NetworkManager.LocalClientId;
        public ClientId ServerClientId => (ClientId)NetworkManager.ServerClientId;

        public IReadOnlyList<ObjectId> ObjectIds => m_ObjectIds;

        public GameObject GetGameObject(ObjectId objectId)
        {
            var spawnedObjects = SpawnedObjects;
            if (spawnedObjects == null)
            {
                return null;
            }

            return spawnedObjects.TryGetValue((ulong)objectId, out var networkObject) ? networkObject.gameObject : null;
        }

        public ClientId GetOwner(ObjectId objectId)
        {
            var spawnedObjects = SpawnedObjects;
            if (spawnedObjects != null && spawnedObjects.TryGetValue((ulong)objectId, out var networkObject))
            {
                return (ClientId)networkObject.OwnerClientId;
            }
            return 0;
        }

        // IGetBandwidth
        // --------------------------------------------------------------------
        public bool IsCacheEmpty => m_Collection == null || m_Collection.HasNoBandwidthData;

        public BandwidthTypes SupportedBandwidthTypes =>
            BandwidthTypes.Other | BandwidthTypes.Rpc | BandwidthTypes.NetVar;

        public event Action OnBandwidthUpdated;

        public float GetBandwidthBytes(
            ObjectId objectId,
            BandwidthTypes bandwidthTypes = BandwidthTypes.All,
            NetworkDirection networkDirection = NetworkDirection.SentAndReceived)
            => m_Collection.GetBandwidth(objectId, bandwidthTypes, networkDirection);

        // IGetRpcCount

        // --------------------------------------------------------------------

        public event Action OnRpcCountUpdated;

        public int GetRpcCount(ObjectId objectId) => m_Collection.GetRpcCount(objectId);

        private NetworkMetrics GetOrAddNetworkMetrics()
        {
            if (m_NetworkManager.NetworkMetrics is NetworkMetrics nm)
            {
                nm.OnMetricsDispatched -= NotifyObservers;
                return nm;
            }

            var nwMetrics = new NetworkMetrics();
            m_NetworkManager.NetworkMetrics = nwMetrics;
            return nwMetrics;
        }
    }
}
