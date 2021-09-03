using System.Collections.Generic;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;


namespace Unity.Netcode
{
    /// <summary>
    /// Used by <see cref="NetworkSceneManager"/> for <see cref="MessageQueueContainer.MessageType.SceneEvent"/> messages
    /// Note: This is only when <see cref="NetworkConfig.EnableSceneManagement"/> is enabled
    /// </summary>
    public class SceneEventData : IDisposable
    {
        /// <summary>
        /// The different types of scene events communicated between a server and client.
        /// Scene event types can be:
        /// A Server To Client Event (S2C)
        /// A Client to Server Event (C2S)
        /// </summary>
        public enum SceneEventTypes
        {
            /// <summary>
            /// Load a scene
            /// Invocation: Server Side
            /// Message Flow: Server to client
            /// Event Notification: Both server and client are notified a load scene event started
            /// </summary>
            S2C_Load,
            /// <summary>
            /// Unload a scene
            /// Invocation: Server Side
            /// Message Flow: Server to client
            /// Event Notification: Both server and client are notified an unload scene event started
            /// </summary>
            S2C_Unload,
            /// <summary>
            /// Synchronize current game session state for approved clients
            /// Invocation: Server Side
            /// Message Flow: Server to client
            /// Event Notification: Server and Client receives a local notification (server receives the ClientId being synchronized)
            /// </summary>
            S2C_Sync,
            /// <summary>
            /// Game session re-synchronization of NetworkOjects that were destroyed during a <see cref="S2C_Sync"/> event
            /// Invocation: Server Side
            /// Message Flow: Server to client
            /// Event Notification: Both server and client receive a local notification
            /// </summary>
            S2C_ReSync,
            /// <summary>
            /// All clients have finished loading a scene
            /// Invocation: Server Side
            /// Message Flow: Server to Client
            /// Event Notification: Both server and client receive a local notification containing the clients that finished
            /// as well as the clients that timed out (if any).
            /// </summary>
            S2C_LoadComplete,
            /// <summary>
            /// All clients have unloaded a scene
            /// Invocation: Server Side
            /// Message Flow: Server to Client
            /// Event Notification: Both server and client receive a local notification containing the clients that finished
            /// as well as the clients that timed out (if any).
            /// </summary>
            S2C_UnLoadComplete,
            /// <summary>
            /// A client has finished loading a scene
            /// Invocation: Client Side
            /// Message Flow: Client to Server
            /// Event Notification: Both server and client receive a local notification
            /// </summary>
            C2S_LoadComplete,
            /// <summary>
            /// A client has finished unloading a scene
            /// Invocation: Client Side
            /// Message Flow: Client to Server
            /// Event Notification: Both server and client receive a local notification
            /// </summary>
            C2S_UnloadComplete,
            /// <summary>
            /// A client has finished synchronizing from a <see cref="S2C_Sync"/> event
            /// Invocation: Client Side
            /// Message Flow: Client to Server
            /// Event Notification: Both server and client receive a local notification
            /// </summary>
            C2S_SyncComplete,
        }

        internal SceneEventTypes SceneEventType;
        internal LoadSceneMode LoadSceneMode;
        internal Guid SceneEventGuid;

        internal uint SceneIndex;
        internal int SceneHandle;

        /// Only used for S2C_Synch scene events, this assures permissions when writing
        /// NetworkVariable information.  If that process changes, then we need to update
        /// this
        internal ulong TargetClientId;

        private Dictionary<uint, List<NetworkObject>> m_SceneNetworkObjects;
        private Dictionary<uint, long> m_SceneNetworkObjectDataOffsets;

        /// <summary>
        /// Client or Server Side:
        /// Client side: Generates a list of all NetworkObjects by their NetworkObjectId that was spawned during th synchronization process
        /// Server side: Compares list from client to make sure client didn't drop a message about a NetworkObject being despawned while it
        /// was synchronizing (if so server will send another message back to the client informing the client of NetworkObjects to remove)
        /// spawned during an initial synchronization.
        /// </summary>
        private List<NetworkObject> m_NetworkObjectsSync = new List<NetworkObject>();

        /// <summary>
        /// Server Side Re-Synchronization:
        /// If there happens to be NetworkObjects in the final Event_Sync_Complete message that are no longer spawned,
        /// the server will compile a list and send back an Event_ReSync message to the client.
        /// </summary>
        private List<ulong> m_NetworkObjectsToBeRemoved = new List<ulong>();

        internal PooledNetworkBuffer InternalBuffer;

        private NetworkManager m_NetworkManager;

        /// <summary>
        /// Client side and only applies to the following scene event types:
        /// <see cref="C2S_LoadComplete"/>
        /// <see cref="C2S_UnLoadComplete"/>
        /// </summary>
        internal SceneEvent SceneEvent;

        internal List<ulong> ClientsCompleted;
        internal List<ulong> ClientsTimedOut;

        internal Queue<uint> ScenesToSynchronize;
        internal Queue<uint> SceneHandlesToSynchronize;


        /// <summary>
        /// Server Side:
        /// Add a scene and its handle to the list of scenes the client should load before synchronizing
        /// Since scene handles are not the same per instance, the client builds a server scene handle to
        /// client scene handle lookup table.
        /// Why include the scene handle? In order to support loading of the same additive scene more than once
        /// we must distinguish which scene we are talking about when the server tells the client to unload a scene.
        /// The server will always communicate its local relative scene's handle and the client will determine its
        /// local relative handle from the table being built.
        /// Look for <see cref="NetworkSceneManager.m_ServerSceneHandleToClientSceneHandle"/> usage to see where
        /// entries are being added to or removed from the table
        /// </summary>
        /// <param name="sceneIndex"></param>
        /// <param name="sceneHandle"></param>
        internal void AddSceneToSynchronize(uint sceneIndex, int sceneHandle)
        {
            ScenesToSynchronize.Enqueue(sceneIndex);
            SceneHandlesToSynchronize.Enqueue((uint)sceneHandle);
        }

        /// <summary>
        /// Client Side:
        /// Gets the next scene index to be loaded for approval and/or late joining
        /// </summary>
        /// <returns></returns>
        internal uint GetNextSceneSynchronizationIndex()
        {
            return ScenesToSynchronize.Dequeue();
        }

        /// <summary>
        /// Client Side:
        /// Gets the next scene handle to be loaded for approval and/or late joining
        /// </summary>
        /// <returns></returns>
        internal int GetNextSceneSynchronizationHandle()
        {
            return (int)SceneHandlesToSynchronize.Dequeue();
        }

        /// <summary>
        /// Client Side:
        /// Determines if all scenes have been processed during the synchronization process
        /// </summary>
        /// <returns>true/false</returns>
        internal bool IsDoneWithSynchronization()
        {
            if (ScenesToSynchronize.Count == 0 && SceneHandlesToSynchronize.Count == 0)
            {
                return true;
            }
            else if (ScenesToSynchronize.Count != SceneHandlesToSynchronize.Count)
            {
                // This should never happen, but in the event it does...
                throw new Exception($"[{nameof(SceneEventData)}-Internal Mismatch Error] {nameof(ScenesToSynchronize)} count != {nameof(SceneHandlesToSynchronize)} count!");
            }
            return false;
        }

        /// <summary>
        /// Server Side:
        /// Called just before the synchronization process
        /// </summary>
        internal void InitializeForSynch()
        {
            if (m_SceneNetworkObjects == null)
            {
                m_SceneNetworkObjects = new Dictionary<uint, List<NetworkObject>>();
            }
            else
            {
                m_SceneNetworkObjects.Clear();
            }

            if (ScenesToSynchronize == null)
            {
                ScenesToSynchronize = new Queue<uint>();
            }
            else
            {
                ScenesToSynchronize.Clear();
            }

            if (SceneHandlesToSynchronize == null)
            {
                SceneHandlesToSynchronize = new Queue<uint>();
            }
            else
            {
                SceneHandlesToSynchronize.Clear();
            }
        }

        internal void AddSpawnedNetworkObjects()
        {
            m_NetworkObjectsSync = m_NetworkManager.SpawnManager.SpawnedObjectsList.ToList();
            m_NetworkObjectsSync.Sort(SortNetworkObjects);
        }

        /// <summary>
        /// Server Side:
        /// Used during the synchronization process to associate NetworkObjects with scenes
        /// </summary>
        /// <param name="sceneIndex"></param>
        /// <param name="networkObject"></param>
        internal void AddNetworkObjectForSynch(uint sceneIndex, NetworkObject networkObject)
        {
            if (!m_SceneNetworkObjects.ContainsKey(sceneIndex))
            {
                m_SceneNetworkObjects.Add(sceneIndex, new List<NetworkObject>());
            }

            m_SceneNetworkObjects[sceneIndex].Add(networkObject);
        }

        /// <summary>
        /// Client and Server:
        /// Determines if the scene event type was intended for the client ( or server )
        /// </summary>
        /// <returns>true (client should handle this message) false (server should handle this message)</returns>
        internal bool IsSceneEventClientSide()
        {
            switch (SceneEventType)
            {
                case SceneEventTypes.S2C_Load:
                case SceneEventTypes.S2C_Unload:
                case SceneEventTypes.S2C_Sync:
                case SceneEventTypes.S2C_ReSync:
                case SceneEventTypes.S2C_LoadComplete:
                case SceneEventTypes.S2C_UnLoadComplete:
                    {
                        return true;
                    }
            }
            return false;
        }

        /// <summary>
        /// Server Side:
        /// Sorts the NetworkObjects to assure proper instantiation order of operations for
        /// registered INetworkPrefabInstanceHandler implementations
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        private int SortNetworkObjects(NetworkObject first, NetworkObject second)
        {
            var doesFirstHaveHandler = m_NetworkManager.PrefabHandler.ContainsHandler(first);
            var doesSecondHaveHandler = m_NetworkManager.PrefabHandler.ContainsHandler(second);
            if (doesFirstHaveHandler != doesSecondHaveHandler)
            {
                if (doesFirstHaveHandler)
                {
                    return 1;
                }
                else
                {
                    return -1;
                }
            }
            return 0;
        }

        /// <summary>
        /// Client and Server Side:
        /// Serializes data based on the SceneEvent type (<see cref="SceneEventTypes"/>)
        /// </summary>
        /// <param name="writer"><see cref="NetworkWriter"/> to write the scene event data</param>
        internal void OnWrite(NetworkWriter writer)
        {
            // Write the scene event type
            writer.WriteByte((byte)SceneEventType);

            // Write the scene loading mode
            writer.WriteByte((byte)LoadSceneMode);

            // Write the scene event progress Guid
            if (SceneEventType != SceneEventTypes.S2C_Sync)
            {
                writer.WriteByteArray(SceneEventGuid.ToByteArray());
            }

            // Write the scene index and handle
            writer.WriteUInt32Packed(SceneIndex);
            writer.WriteInt32Packed(SceneHandle);

            switch (SceneEventType)
            {
                case SceneEventTypes.S2C_Sync:
                    {
                        WriteSceneSynchronizationData(writer);
                        break;
                    }
                case SceneEventTypes.S2C_Load:
                    {
                        SerializeScenePlacedObjects(writer);
                        break;
                    }
                case SceneEventTypes.C2S_SyncComplete:
                    {
                        WriteClientSynchronizationResults(writer);
                        break;
                    }
                case SceneEventTypes.S2C_ReSync:
                    {
                        WriteClientReSynchronizationData(writer);
                        break;
                    }
                case SceneEventTypes.S2C_LoadComplete:
                case SceneEventTypes.S2C_UnLoadComplete:
                    {
                        WriteSceneEventProgressDone(writer);
                        break;
                    }
            }
        }

        /// <summary>
        /// Server Side:
        /// Called at the end of an S2C_Load event once the scene is loaded and scene placed NetworkObjects
        /// have been locally spawned
        /// </summary>
        internal void WriteSceneSynchronizationData(NetworkWriter writer)
        {
            // Write the scenes we want to load, in the order we want to load them
            writer.WriteUIntArrayPacked(ScenesToSynchronize.ToArray());
            writer.WriteUIntArrayPacked(SceneHandlesToSynchronize.ToArray());

            // Store our current position in the stream to come back and say how much data we have written
            var positionStart = writer.GetStream().Position;

            // Size Place Holder -- Start
            // !!NOTE!!: Since this is a placeholder to be set after we know how much we have written,
            // for stream offset purposes this MUST not be a packed value!
            writer.WriteUInt32(0);
            var totalBytes = 0;

            // Write the number of NetworkObjects we are serializing
            writer.WriteInt32Packed(m_NetworkObjectsSync.Count());

            foreach (var networkObject in m_NetworkObjectsSync)
            {
                var noStart = writer.GetStream().Position;
                writer.WriteInt32Packed(networkObject.gameObject.scene.handle);
                networkObject.SerializeSceneObject(writer, TargetClientId);
                var noStop = writer.GetStream().Position;
                totalBytes += (int)(noStop - noStart);
            }

            // Size Place Holder -- End
            var positionEnd = writer.GetStream().Position;
            var bytesWritten = (uint)(positionEnd - (positionStart + sizeof(uint)));
            writer.GetStream().Position = positionStart;
            // Write the total size written to the stream by NetworkObjects being serialized
            writer.WriteUInt32(bytesWritten);
            writer.GetStream().Position = positionEnd;
        }

        /// <summary>
        /// Server Side:
        /// Called at the end of an S2C_Load event once the scene is loaded and scene placed NetworkObjects
        /// have been locally spawned
        /// Maximum number of objects that could theoretically be synchronized is 65536
        /// </summary>
        internal void SerializeScenePlacedObjects(NetworkWriter writer)
        {
            var numberOfObjects = (ushort)0;
            var stream = writer.GetStream();
            var headPosition = stream.Position;

            // Write our count place holder (must not be packed!)
            writer.WriteUInt16(0);

            foreach (var keyValuePairByGlobalObjectIdHash in m_NetworkManager.SceneManager.ScenePlacedObjects)
            {
                foreach (var keyValuePairBySceneHandle in keyValuePairByGlobalObjectIdHash.Value)
                {
                    // Write our server relative scene handle for the NetworkObject being serialized
                    writer.WriteInt32Packed(keyValuePairBySceneHandle.Key);
                    // Serialize the NetworkObject
                    keyValuePairBySceneHandle.Value.SerializeSceneObject(writer, TargetClientId);
                    numberOfObjects++;
                }
            }

            var tailPosition = stream.Position;
            // Reposition to our count position to the head before we wrote our object count
            stream.Position = headPosition;
            // Write number of NetworkObjects serialized (must not be packed!)
            writer.WriteUInt16(numberOfObjects);
            // Set our position back to the tail
            stream.Position = tailPosition;
        }

        /// <summary>
        /// Client and Server Side:
        /// Deserialize data based on the SceneEvent type.
        /// </summary>
        /// <param name="reader"></param>
        internal void OnRead(NetworkReader reader)
        {
            var sceneEventTypeValue = reader.ReadByte();

            if (Enum.IsDefined(typeof(SceneEventTypes), sceneEventTypeValue))
            {
                SceneEventType = (SceneEventTypes)sceneEventTypeValue;
            }
            else
            {
                Debug.LogError($"Serialization Read Error: {nameof(SceneEventType)} vale {sceneEventTypeValue} is not within the range of the defined {nameof(SceneEventTypes)} enumerator!");
            }

            var loadSceneModeValue = reader.ReadByte();

            if (Enum.IsDefined(typeof(LoadSceneMode), loadSceneModeValue))
            {
                LoadSceneMode = (LoadSceneMode)loadSceneModeValue;
            }
            else
            {
                Debug.LogError($"Serialization Read Error: {nameof(LoadSceneMode)} vale {loadSceneModeValue} is not within the range of the defined {nameof(LoadSceneMode)} enumerator!");
            }

            if (SceneEventType != SceneEventTypes.S2C_Sync)
            {
                SceneEventGuid = new Guid(reader.ReadByteArray());
            }

            SceneIndex = reader.ReadUInt32Packed();
            SceneHandle = reader.ReadInt32Packed();

            switch (SceneEventType)
            {
                case SceneEventTypes.S2C_Sync:
                    {
                        CopySceneSyncrhonizationData(reader);
                        break;
                    }
                case SceneEventTypes.C2S_SyncComplete:
                    {
                        CheckClientSynchronizationResults(reader);
                        break;
                    }
                case SceneEventTypes.S2C_Load:
                    {
                        SetInternalBuffer();
                        // We store off the trailing in-scene placed serialized NetworkObject data to
                        // be processed once we are done loading.
                        InternalBuffer.Position = 0;
                        InternalBuffer.CopyUnreadFrom(reader.GetStream());
                        InternalBuffer.Position = 0;
                        break;
                    }
                case SceneEventTypes.S2C_ReSync:
                    {
                        ReadClientReSynchronizationData(reader);
                        break;
                    }
                case SceneEventTypes.S2C_LoadComplete:
                case SceneEventTypes.S2C_UnLoadComplete:
                    {
                        ReadSceneEventProgressDone(reader);
                        break;
                    }
            }
        }

        /// <summary>
        /// Client Side:
        /// Prepares for a scene synchronization event and copies the scene synchronization data
        /// into the internal buffer to be used throughout the synchronization process.
        /// </summary>
        /// <param name="reader"></param>
        internal void CopySceneSyncrhonizationData(NetworkReader reader)
        {
            SetInternalBuffer();
            m_NetworkObjectsSync.Clear();
            ScenesToSynchronize = new Queue<uint>(reader.ReadUIntArrayPacked());
            SceneHandlesToSynchronize = new Queue<uint>(reader.ReadUIntArrayPacked());
            InternalBuffer.Position = 0;

            // is not packed!
            var sizeToCopy = reader.ReadUInt32();

            using var writer = PooledNetworkWriter.Get(InternalBuffer);
            writer.ReadAndWrite(reader, (long)sizeToCopy);

            InternalBuffer.Position = 0;
        }

        /// <summary>
        /// Client Side:
        /// This needs to occur at the end of a S2C_Load event when the scene has finished loading
        /// Maximum number of objects that could theoretically be synchronized is 65536
        /// </summary>
        internal void DeserializeScenePlacedObjects()
        {
            using var reader = PooledNetworkReader.Get(InternalBuffer);
            // is not packed!
            var newObjectsCount = reader.ReadUInt16();

            for (ushort i = 0; i < newObjectsCount; i++)
            {
                // Set our relative scene to the NetworkObject
                m_NetworkManager.SceneManager.SetTheSceneBeingSynchronized(reader.ReadInt32Packed());

                // Deserialize the NetworkObject
                NetworkObject.DeserializeSceneObject(InternalBuffer as NetworkBuffer, reader, m_NetworkManager);
            }
            ReleaseInternalBuffer();
        }

        /// <summary>
        /// Client Side:
        /// If there happens to be NetworkObjects in the final Event_Sync_Complete message that are no longer spawned,
        /// the server will compile a list and send back an Event_ReSync message to the client.  This is where the
        /// client handles any returned values by the server.
        /// </summary>
        /// <param name="reader"></param>
        internal void ReadClientReSynchronizationData(NetworkReader reader)
        {
            var networkObjectsToRemove = reader.ReadULongArrayPacked();

            if (networkObjectsToRemove.Length > 0)
            {
                var networkObjects = UnityEngine.Object.FindObjectsOfType<NetworkObject>();
                var networkObjectIdToNetworkObject = new Dictionary<ulong, NetworkObject>();
                foreach (var networkObject in networkObjects)
                {
                    if (!networkObjectIdToNetworkObject.ContainsKey(networkObject.NetworkObjectId))
                    {
                        networkObjectIdToNetworkObject.Add(networkObject.NetworkObjectId, networkObject);
                    }
                }

                foreach (var networkObjectId in networkObjectsToRemove)
                {
                    if (networkObjectIdToNetworkObject.ContainsKey(networkObjectId))
                    {
                        var networkObject = networkObjectIdToNetworkObject[networkObjectId];
                        networkObjectIdToNetworkObject.Remove(networkObjectId);

                        networkObject.IsSpawned = false;
                        if (m_NetworkManager.PrefabHandler.ContainsHandler(networkObject))
                        {
                            // Since this is the client side and we have missed the delete message, until the Snapshot system is in place for spawn and despawn handling
                            // we have to remove this from the list of spawned objects manually or when a NetworkObjectId is recycled the client will throw an error
                            // about the id already being assigned.
                            if (m_NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(networkObjectId))
                            {
                                m_NetworkManager.SpawnManager.SpawnedObjects.Remove(networkObjectId);
                            }
                            if (m_NetworkManager.SpawnManager.SpawnedObjectsList.Contains(networkObject))
                            {
                                m_NetworkManager.SpawnManager.SpawnedObjectsList.Remove(networkObject);
                            }
                            NetworkManager.Singleton.PrefabHandler.HandleNetworkPrefabDestroy(networkObject);
                        }
                        else
                        {
                            UnityEngine.Object.DestroyImmediate(networkObject.gameObject);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Server Side:
        /// If there happens to be NetworkObjects in the final Event_Sync_Complete message that are no longer spawned,
        /// the server will compile a list and send back an Event_ReSync message to the client.
        /// </summary>
        /// <param name="writer"></param>
        internal void WriteClientReSynchronizationData(NetworkWriter writer)
        {
            //Write how many objects need to be removed
            writer.WriteULongArrayPacked(m_NetworkObjectsToBeRemoved.ToArray());
        }

        /// <summary>
        /// Server Side:
        /// Determines if the client needs to be slightly re-synchronized if during the deserialization
        /// process the server finds NetworkObjects that the client still thinks are spawned.
        /// </summary>
        /// <returns></returns>
        internal bool ClientNeedsReSynchronization()
        {
            return (m_NetworkObjectsToBeRemoved.Count > 0);
        }

        /// <summary>
        /// Server Side:
        /// Determines if the client needs to be re-synchronized if during the deserialization
        /// process the server finds NetworkObjects that the client still thinks are spawned but
        /// have since been despawned.
        /// </summary>
        /// <param name="reader"></param>
        internal void CheckClientSynchronizationResults(NetworkReader reader)
        {
            m_NetworkObjectsToBeRemoved.Clear();
            var networkObjectIdCount = reader.ReadUInt32Packed();
            for (int i = 0; i < networkObjectIdCount; i++)
            {
                var networkObjectId = (ulong)reader.ReadUInt32Packed();
                if (!m_NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(networkObjectId))
                {
                    m_NetworkObjectsToBeRemoved.Add(networkObjectId);
                }
            }
        }

        /// <summary>
        /// Client Side:
        /// During the deserialization process of the servers Event_Sync, the client builds a list of
        /// all NetworkObjectIds that were spawned.  Upon responding to the server with the Event_Sync_Complete
        /// this list is included for the server to review over and determine if the client needs a minor resynchronization
        /// of NetworkObjects that might have been despawned while the client was processing the Event_Sync.
        /// </summary>
        /// <param name="writer"></param>
        internal void WriteClientSynchronizationResults(NetworkWriter writer)
        {
            //Write how many objects were spawned
            writer.WriteUInt32Packed((uint)m_NetworkObjectsSync.Count);
            foreach (var networkObject in m_NetworkObjectsSync)
            {
                writer.WriteUInt32Packed((uint)networkObject.NetworkObjectId);
            }
        }

        /// <summary>
        /// Client Side:
        /// During the processing of a server sent Event_Sync, this method will be called for each scene once
        /// it is finished loading.  The client will also build a list of NetworkObjects that it spawned during
        /// this process which will be used as part of the Event_Sync_Complete response.
        /// </summary>
        /// <param name="sceneId"></param>
        /// <param name="networkManager"></param>
        internal void SynchronizeSceneNetworkObjects(NetworkManager networkManager)
        {
            using var reader = PooledNetworkReader.Get(InternalBuffer);
            // Process all NetworkObjects for this scene
            var newObjectsCount = reader.ReadInt32Packed();

            for (int i = 0; i < newObjectsCount; i++)
            {
                /// We want to make sure for each NetworkObject we have the appropriate scene selected as the scene that is
                /// currently being synchronized.  This assures in-scene placed NetworkObjects will use the right NetworkObject
                /// from the list of populated <see cref="NetworkSceneManager.ScenePlacedObjects"/>
                m_NetworkManager.SceneManager.SetTheSceneBeingSynchronized(reader.ReadInt32Packed());

                var spawnedNetworkObject = NetworkObject.DeserializeSceneObject(InternalBuffer, reader, networkManager);
                if (!m_NetworkObjectsSync.Contains(spawnedNetworkObject))
                {
                    m_NetworkObjectsSync.Add(spawnedNetworkObject);
                }
            }
            ReleaseInternalBuffer();
        }

        /// <summary>
        /// Writes the all clients loaded or unloaded completed and timed out lists
        /// </summary>
        /// <param name="writer"></param>
        internal void WriteSceneEventProgressDone(NetworkWriter writer)
        {
            writer.WriteUInt16Packed((ushort)ClientsCompleted.Count);
            foreach (var clientId in ClientsCompleted)
            {
                writer.WriteUInt64Packed(clientId);
            }

            writer.WriteUInt16Packed((ushort)ClientsTimedOut.Count);
            foreach (var clientId in ClientsTimedOut)
            {
                writer.WriteUInt64Packed(clientId);
            }
        }

        /// <summary>
        /// Reads the all clients loaded or unloaded completed and timed out lists
        /// </summary>
        /// <param name="reader"></param>
        internal void ReadSceneEventProgressDone(NetworkReader reader)
        {
            var completedCount = reader.ReadUInt16Packed();
            ClientsCompleted = new List<ulong>();
            for (int i = 0; i < completedCount; i++)
            {
                ClientsCompleted.Add(reader.ReadUInt64Packed());
            }

            var timedOutCount = reader.ReadUInt16Packed();
            ClientsTimedOut = new List<ulong>();
            for (int i = 0; i < timedOutCount; i++)
            {
                ClientsTimedOut.Add(reader.ReadUInt64Packed());
            }
        }

        /// <summary>
        /// Gets a PooledNetworkBuffer if needed
        /// </summary>
        private void SetInternalBuffer()
        {
            if (InternalBuffer == null)
            {
                InternalBuffer = NetworkBufferPool.GetBuffer();
            }
        }

        /// <summary>
        /// Releases the PooledNetworkBuffer when no longer needed
        /// </summary>
        private void ReleaseInternalBuffer()
        {
            if (InternalBuffer != null)
            {
                NetworkBufferPool.PutBackInPool(InternalBuffer);
                InternalBuffer = null;
            }
        }

        /// <summary>
        /// Used to release the pooled network buffer
        /// </summary>
        public void Dispose()
        {
            ReleaseInternalBuffer();
        }

        /// <summary>
        /// Constructor for SceneEventData
        /// </summary>
        internal SceneEventData(NetworkManager networkManager)
        {
            m_NetworkManager = networkManager;
        }
    }
}
