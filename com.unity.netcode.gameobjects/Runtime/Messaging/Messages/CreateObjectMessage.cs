using System.Linq;
using System.Runtime.CompilerServices;

namespace Unity.Netcode
{
    internal struct CreateObjectMessage : INetworkMessage
    {
        public int Version => 0;

        private const string k_Name = "CreateObjectMessage";

        public NetworkObject.SceneObject ObjectInfo;
        private FastBufferReader m_ReceivedNetworkVariableData;

        // DA - NGO CMB SERVICE NOTES:
        // The ObserverIds and ExistingObserverIds will only be populated if k_UpdateObservers is set
        // ObserverIds is the full list of observers (see below)
        internal ulong[] ObserverIds;

        // While this does consume a bit more bandwidth, this is only sent by the authority/owner
        // and can be used to determine which clients should receive the ObjectInfo serialized data.
        // All other already existing observers just need to receive the NewObserverIds and the
        // NetworkObjectId
        internal ulong[] NewObserverIds;

        // If !IncludesSerializedObject then the NetworkObjectId will be serialized.
        // This happens when we are just sending an update to the observers list
        // to clients that already have the NetworkObject spawned
        internal ulong NetworkObjectId;

        private const byte k_IncludesSerializedObject = 0x01;
        private const byte k_UpdateObservers = 0x02;
        private const byte k_UpdateNewObservers = 0x04;


        private byte m_CreateObjectMessageTypeFlags;

        internal bool IncludesSerializedObject
        {
            get
            {
                return GetFlag(k_IncludesSerializedObject);
            }

            set
            {
                SetFlag(value, k_IncludesSerializedObject);
            }
        }

        internal bool UpdateObservers
        {
            get
            {
                return GetFlag(k_UpdateObservers);
            }

            set
            {
                SetFlag(value, k_UpdateObservers);
            }
        }

        internal bool UpdateNewObservers
        {
            get
            {
                return GetFlag(k_UpdateNewObservers);
            }

            set
            {
                SetFlag(value, k_UpdateNewObservers);
            }
        }

        private bool GetFlag(int flag)
        {
            return (m_CreateObjectMessageTypeFlags & flag) != 0;
        }

        private void SetFlag(bool set, byte flag)
        {
            if (set) { m_CreateObjectMessageTypeFlags = (byte)(m_CreateObjectMessageTypeFlags | flag); }
            else { m_CreateObjectMessageTypeFlags = (byte)(m_CreateObjectMessageTypeFlags & ~flag); }
        }

        public void Serialize(FastBufferWriter writer, int targetVersion)
        {
            writer.WriteValueSafe(m_CreateObjectMessageTypeFlags);

            if (UpdateObservers)
            {
                BytePacker.WriteValuePacked(writer, ObserverIds.Length);
                foreach (var clientId in ObserverIds)
                {
                    BytePacker.WriteValuePacked(writer, clientId);
                }
            }

            if (UpdateNewObservers)
            {
                BytePacker.WriteValuePacked(writer, NewObserverIds.Length);
                foreach (var clientId in NewObserverIds)
                {
                    BytePacker.WriteValuePacked(writer, clientId);
                }
            }

            if (IncludesSerializedObject)
            {
                ObjectInfo.Serialize(writer);
            }
            else
            {
                BytePacker.WriteValuePacked(writer, NetworkObjectId);
            }
        }

        public bool Deserialize(FastBufferReader reader, ref NetworkContext context, int receivedMessageVersion)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            if (!networkManager.IsClient)
            {
                return false;
            }

            reader.ReadValueSafe(out m_CreateObjectMessageTypeFlags);
            if (UpdateObservers)
            {
                var length = 0;
                ByteUnpacker.ReadValuePacked(reader, out length);
                ObserverIds = new ulong[length];
                var clientId = (ulong)0;
                for (int i = 0; i < length; i++)
                {
                    ByteUnpacker.ReadValuePacked(reader, out clientId);
                    ObserverIds[i] = clientId;
                }
            }

            if (UpdateNewObservers)
            {
                var length = 0;
                ByteUnpacker.ReadValuePacked(reader, out length);
                NewObserverIds = new ulong[length];
                var clientId = (ulong)0;
                for (int i = 0; i < length; i++)
                {
                    ByteUnpacker.ReadValuePacked(reader, out clientId);
                    NewObserverIds[i] = clientId;
                }
            }

            if (IncludesSerializedObject)
            {
                ObjectInfo.Deserialize(reader);
            }
            else
            {
                ByteUnpacker.ReadValuePacked(reader, out NetworkObjectId);
            }

            if (!networkManager.NetworkConfig.ForceSamePrefabs && !networkManager.SpawnManager.HasPrefab(ObjectInfo))
            {
                networkManager.DeferredMessageManager.DeferMessage(IDeferredNetworkMessageManager.TriggerType.OnAddPrefab, ObjectInfo.Hash, reader, ref context, k_Name);
                return false;
            }
            m_ReceivedNetworkVariableData = reader;

            return true;
        }

        public void Handle(ref NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            // If a client receives a create object message and it is still synchronizing, then defer the object creation until it has finished synchronizing
            if (networkManager.SceneManager.ShouldDeferCreateObject())
            {
                networkManager.SceneManager.DeferCreateObject(context.SenderId, context.MessageSize, ObjectInfo, m_ReceivedNetworkVariableData, ObserverIds, NewObserverIds);
            }
            else
            {
                if (networkManager.DistributedAuthorityMode && !IncludesSerializedObject && UpdateObservers)
                {
                    ObjectInfo = new NetworkObject.SceneObject()
                    {
                        NetworkObjectId = NetworkObjectId,
                    };
                }
                CreateObject(ref networkManager, context.SenderId, context.MessageSize, ObjectInfo, m_ReceivedNetworkVariableData, ObserverIds, NewObserverIds);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void CreateObject(ref NetworkManager networkManager, ref NetworkSceneManager.DeferredObjectCreation deferredObjectCreation)
        {
            var senderId = deferredObjectCreation.SenderId;
            var observerIds = deferredObjectCreation.ObserverIds;
            var newObserverIds = deferredObjectCreation.NewObserverIds;
            var messageSize = deferredObjectCreation.MessageSize;
            var sceneObject = deferredObjectCreation.SceneObject;
            var networkVariableData = deferredObjectCreation.FastBufferReader;
            CreateObject(ref networkManager, senderId, messageSize, sceneObject, networkVariableData, observerIds, newObserverIds);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal static void CreateObject(ref NetworkManager networkManager, ulong senderId, uint messageSize, NetworkObject.SceneObject sceneObject, FastBufferReader networkVariableData, ulong[] observerIds, ulong[] newObserverIds)
        {
            var networkObject = (NetworkObject)null;
            try
            {
                if (!networkManager.DistributedAuthorityMode)
                {
                    networkObject = NetworkObject.AddSceneObject(sceneObject, networkVariableData, networkManager);
                }
                else
                {
                    var hasObserverIdList = observerIds != null && observerIds.Length > 0;
                    var hasNewObserverIdList = newObserverIds != null && newObserverIds.Length > 0;
                    // Depending upon visibility of the NetworkObject and the client in question, it could be that
                    // this client already has visibility of this NetworkObject
                    if (networkManager.SpawnManager.SpawnedObjects.ContainsKey(sceneObject.NetworkObjectId))
                    {
                        // If so, then just get the local instance
                        networkObject = networkManager.SpawnManager.SpawnedObjects[sceneObject.NetworkObjectId];

                        // This should not happen, logging error just in case
                        if (hasNewObserverIdList && newObserverIds.Contains(networkManager.LocalClientId))
                        {
                            NetworkLog.LogErrorServer($"[{nameof(CreateObjectMessage)}][Duplicate-Broadcast] Detected duplicated object creation for {sceneObject.NetworkObjectId}!");
                        }
                        else // Trap to make sure the owner is not receiving any messages it sent
                        if (networkManager.CMBServiceConnection && networkManager.LocalClientId == networkObject.OwnerClientId)
                        {
                            NetworkLog.LogWarning($"[{nameof(CreateObjectMessage)}][Client-{networkManager.LocalClientId}][Duplicate-CreateObjectMessage][Client Is Owner] Detected duplicated object creation for {networkObject.name}-{sceneObject.NetworkObjectId}!");
                        }
                    }
                    else
                    {
                        networkObject = NetworkObject.AddSceneObject(sceneObject, networkVariableData, networkManager, true);
                    }

                    // DA - NGO CMB SERVICE NOTES:
                    // It is possible for two clients to connect at the exact same time which, due to client-side spawning, can cause each client
                    // to miss their spawns. For now, all player NetworkObject spawns will always be visible to all known connected clients
                    var clientList = hasObserverIdList && !networkObject.IsPlayerObject ? observerIds : networkManager.ConnectedClientsIds;

                    // Update the observers for this instance
                    foreach (var clientId in clientList)
                    {
                        networkObject.Observers.Add(clientId);
                    }

                    // Mock CMB Service and forward to all clients
                    if (networkManager.DAHost)
                    {
                        // DA - NGO CMB SERVICE NOTES:
                        // (*** See above notes fist ***)
                        // If it is a player object freshly spawning and one or more clients all connect at the exact same time (i.e. received on effectively 
                        // the same frame), then we need to check the observers list to make sure all players are visible upon first spawning. At a later date,
                        // for area of interest we will need to have some form of follow up "observer update" message to cull out players not within each
                        // player's AOI.
                        if (networkObject.IsPlayerObject && hasNewObserverIdList && clientList.Count != observerIds.Length)
                        {
                            // For same-frame newly spawned players that might not be aware of all other players, update the player's observer
                            // list.
                            observerIds = clientList.ToArray();
                        }

                        var createObjectMessage = new CreateObjectMessage()
                        {
                            ObjectInfo = sceneObject,
                            m_ReceivedNetworkVariableData = networkVariableData,
                            ObserverIds = hasObserverIdList ? observerIds : null,
                            NetworkObjectId = networkObject.NetworkObjectId,
                            IncludesSerializedObject = true,
                        };
                        foreach (var clientId in clientList)
                        {
                            // DA - NGO CMB SERVICE NOTES:
                            // If the authority did not specify the list of clients and the client is not an observer or we are the owner/originator
                            // or we are the DAHost, then we skip sending the message.
                            if ((!hasObserverIdList && (!networkObject.Observers.Contains(clientId)) ||
                                clientId == networkObject.OwnerClientId || clientId == NetworkManager.ServerClientId))
                            {
                                continue;
                            }

                            // DA - NGO CMB SERVICE NOTES:
                            // If this included a list of new observers and the targeted clientId is one of the observers, then send the serialized data.
                            // Otherwise, the targeted clientId has already has visibility (i.e. it is already spawned) and so just send the updated
                            // observers list to that client's instance.
                            createObjectMessage.IncludesSerializedObject = hasNewObserverIdList && newObserverIds.Contains(clientId);

                            networkManager.SpawnManager.SendSpawnCallForObject(clientId, networkObject);
                        }
                    }
                }
                if (networkObject != null)
                {
                    networkManager.NetworkMetrics.TrackObjectSpawnReceived(senderId, networkObject, messageSize);
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
            }
        }
    }
}
