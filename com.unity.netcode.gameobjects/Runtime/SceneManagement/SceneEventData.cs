using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Unity.Netcode
{
    /// <summary>
    /// The different types of scene events communicated between a server and client. <br/>
    /// Used by <see cref="NetworkSceneManager"/> for <see cref="SceneEventMessage"/> messages.<br/>
    /// <em>Note: This is only when <see cref="NetworkConfig.EnableSceneManagement"/> is enabled.</em><br/>
    /// See also: <br/>
    /// <seealso cref="SceneEvent"/>
    /// </summary>
    public enum SceneEventType : byte
    {
        /// <summary>
        /// Load a scene<br/>
        /// <b>Invocation:</b> Server Side<br/>
        /// <b>Message Flow:</b> Server to client<br/>
        /// <b>Event Notification:</b> Both server and client are notified a load scene event started
        /// </summary>
        Load,
        /// <summary>
        /// Unload a scene<br/>
        /// <b>Invocation:</b> Server Side<br/>
        /// <b>Message Flow:</b> Server to client<br/>
        /// <b>Event Notification:</b> Both server and client are notified an unload scene event started.
        /// </summary>
        Unload,
        /// <summary>
        /// Synchronizes current game session state for newly approved clients<br/>
        /// <b>Invocation:</b> Server Side<br/>
        /// <b>Message Flow:</b> Server to client<br/>
        /// <b>Event Notification:</b> Server and Client receives a local notification (<em>server receives the ClientId being synchronized</em>).
        /// </summary>
        Synchronize,
        /// <summary>
        /// Game session re-synchronization of NetworkObjects that were destroyed during a <see cref="Synchronize"/> event<br/>
        /// <b>Invocation:</b> Server Side<br/>
        /// <b>Message Flow:</b> Server to client<br/>
        /// <b>Event Notification:</b> Both server and client receive a local notification<br/>
        /// </summary>
        ReSynchronize,
        /// <summary>
        /// All clients have finished loading a scene<br/>
        /// <b>Invocation:</b> Server Side<br/>
        /// <b>Message Flow:</b> Server to Client<br/>
        /// <b>Event Notification:</b> Both server and client receive a local notification containing the clients that finished
        /// as well as the clients that timed out(<em>if any</em>).
        /// </summary>
        LoadEventCompleted,
        /// <summary>
        /// All clients have unloaded a scene<br/>
        /// <b>Invocation:</b> Server Side<br/>
        /// <b>Message Flow:</b> Server to Client<br/>
        /// <b>Event Notification:</b> Both server and client receive a local notification containing the clients that finished
        /// as well as the clients that timed out(<em>if any</em>).
        /// </summary>
        UnloadEventCompleted,
        /// <summary>
        /// A client has finished loading a scene<br/>
        /// <b>Invocation:</b> Client Side<br/>
        /// <b>Message Flow:</b> Client to Server<br/>
        /// <b>Event Notification:</b> Both server and client receive a local notification.
        /// </summary>
        LoadComplete,
        /// <summary>
        /// A client has finished unloading a scene<br/>
        /// <b>Invocation:</b> Client Side<br/>
        /// <b>Message Flow:</b> Client to Server<br/>
        /// <b>Event Notification:</b> Both server and client receive a local notification.
        /// </summary>
        UnloadComplete,
        /// <summary>
        /// A client has finished synchronizing from a <see cref="Synchronize"/> event<br/>
        /// <b>Invocation:</b> Client Side<br/>
        /// <b>Message Flow:</b> Client to Server<br/>
        /// <b>Event Notification:</b> Both server and client receive a local notification.
        /// </summary>
        SynchronizeComplete,
        /// <summary>
        /// Synchronizes clients when the active scene has changed
        /// See: <see cref="NetworkObject.ActiveSceneSynchronization"/>
        /// </summary>
        ActiveSceneChanged,
        /// <summary>
        /// Synchronizes clients when one or more NetworkObjects are migrated into a new scene
        /// See: <see cref="NetworkObject.SceneMigrationSynchronization"/>
        /// </summary>
        ObjectSceneChanged,
    }

    /// <summary>
    /// Used by <see cref="NetworkSceneManager"/> for <see cref="SceneEventMessage"/> messages
    /// <em>Note: This is only when <see cref="NetworkConfig.EnableSceneManagement"/> is enabled.</em><br/>
    /// See also: <seealso cref="SceneEvent"/>
    /// </summary>
    internal class SceneEventData : IDisposable
    {
        internal SceneEventType SceneEventType;
        internal LoadSceneMode LoadSceneMode;
        internal ForceNetworkSerializeByMemcpy<Guid> SceneEventProgressId;
        internal uint SceneEventId;

        internal string ActiveSceneAsset;
        internal string SceneAsset;
        internal int SceneHandle;

        // Used by the client during synchronization
        internal string ClientSceneName;
        internal int NetworkSceneHandle;

        /// Only used for <see cref="SceneEventType.Synchronize"/> scene events, this assures permissions when writing
        /// NetworkVariable information.  If that process changes, then we need to update this
        /// In distributed authority mode this is used to route messages to the appropriate destination client
        internal ulong TargetClientId;
        /// Only used with a DAHost
        internal ulong SenderClientId;

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

        private List<NetworkObject> m_DespawnedInSceneObjectsSync = new List<NetworkObject>();
        private Dictionary<int, List<uint>> m_DespawnedInSceneObjects = new Dictionary<int, List<uint>>();

        /// <summary>
        /// Server Side Re-Synchronization:
        /// If there happens to be NetworkObjects in the final Event_Sync_Complete message that are no longer spawned,
        /// the server will compile a list and send back an Event_ReSync message to the client.
        /// </summary>
        private List<ulong> m_NetworkObjectsToBeRemoved = new List<ulong>();

        private bool m_HasInternalBuffer;
        internal FastBufferReader InternalBuffer;

        private NetworkManager m_NetworkManager;

        internal List<ulong> ClientsCompleted;
        internal List<ulong> ClientsTimedOut;

        internal Queue<string> ScenesToSynchronize;
        internal Queue<uint> SceneHandlesToSynchronize;

        internal LoadSceneMode ClientSynchronizationMode;


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
        internal void AddSceneToSynchronize(string sceneHash, int sceneHandle)
        {
            ScenesToSynchronize.Enqueue(sceneHash);
            SceneHandlesToSynchronize.Enqueue((uint)sceneHandle);
        }

        /// <summary>
        /// Client Side:
        /// Gets the next scene hash to be loaded for approval and/or late joining
        /// </summary>
        /// <returns></returns>
        internal string GetNextSceneSynchronizationHash()
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
                ScenesToSynchronize = new Queue<string>();
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
            ForwardSynchronization = false;
        }

        /// <summary>
        /// Used with SortParentedNetworkObjects to sort the children of the root parent NetworkObject
        /// </summary>
        /// <param name="first">object to be sorted</param>
        /// <param name="second">object to be compared to for sorting the first object</param>
        /// <returns></returns>
        private int SortChildrenNetworkObjects(NetworkObject first, NetworkObject second)
        {
            var firstParent = first.GetCachedParent()?.GetComponent<NetworkObject>();
            // If the second is the first's parent then move the first down
            if (firstParent != null && firstParent == second)
            {
                return 1;
            }

            var secondParent = second.GetCachedParent()?.GetComponent<NetworkObject>();
            // If the first is the second's parent then move the first up
            if (secondParent != null && secondParent == first)
            {
                return -1;
            }

            // Otherwise, don't move the first at all
            return 0;
        }

        /// <summary>
        /// Sorts the synchronization order of the NetworkObjects to be serialized
        /// by parents before children order
        /// </summary>
        private void SortParentedNetworkObjects()
        {
            var networkObjectList = m_NetworkObjectsSync.ToList();
            foreach (var networkObject in networkObjectList)
            {
                // Find only the root parent NetworkObjects
                if (networkObject.transform.childCount > 0 && networkObject.transform.parent == null)
                {
                    // Get all child NetworkObjects of the root
                    var childNetworkObjects = networkObject.GetComponentsInChildren<NetworkObject>().ToList();

                    childNetworkObjects.Sort(SortChildrenNetworkObjects);

                    // Remove the root from the children list
                    childNetworkObjects.Remove(networkObject);

                    // Remove the root's children from the primary list
                    foreach (var childObject in childNetworkObjects)
                    {
                        m_NetworkObjectsSync.Remove(childObject);
                    }
                    // Insert or Add the sorted children list
                    var nextIndex = m_NetworkObjectsSync.IndexOf(networkObject) + 1;
                    if (nextIndex == m_NetworkObjectsSync.Count)
                    {
                        m_NetworkObjectsSync.AddRange(childNetworkObjects);
                    }
                    else
                    {
                        m_NetworkObjectsSync.InsertRange(nextIndex, childNetworkObjects);
                    }
                }
            }
        }

        internal static bool LogSerializationOrder = false;

        internal void AddSpawnedNetworkObjects()
        {
            m_NetworkObjectsSync.Clear();
            // If distributed authority mode and sending to the service, then ignore observers
            var distributedAuthoritySendingToService = m_NetworkManager.DistributedAuthorityMode && TargetClientId == NetworkManager.ServerClientId;
            foreach (var sobj in m_NetworkManager.SpawnManager.SpawnedObjectsList)
            {
                if (sobj.Observers.Contains(TargetClientId) || distributedAuthoritySendingToService)
                {
                    m_NetworkObjectsSync.Add(sobj);
                }
            }

            // Sort by INetworkPrefabInstanceHandler implementation before the
            // NetworkObjects spawned by the implementation
            m_NetworkObjectsSync.Sort(SortNetworkObjects);

            // The last thing we sort is parents before children
            SortParentedNetworkObjects();

            // This is useful to know what NetworkObjects a client is going to be synchronized with
            // as well as the order in which they will be deserialized
            if (LogSerializationOrder && m_NetworkManager.LogLevel == LogLevel.Developer)
            {
                var messageBuilder = new StringBuilder(0xFFFF);
                messageBuilder.AppendLine("[Server-Side Client-Synchronization] NetworkObject serialization order:");
                foreach (var networkObject in m_NetworkObjectsSync)
                {
                    messageBuilder.AppendLine($"{networkObject.name}");
                }
                NetworkLog.LogInfo(messageBuilder.ToString());
            }
        }

        internal void AddDespawnedInSceneNetworkObjects()
        {
            m_DespawnedInSceneObjectsSync.Clear();
            // Find all active and non-active in-scene placed NetworkObjects
#if UNITY_2023_1_OR_NEWER
            var inSceneNetworkObjects = UnityEngine.Object.FindObjectsByType<NetworkObject>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.InstanceID).Where((c) => c.NetworkManager == m_NetworkManager);
#else
            var inSceneNetworkObjects = UnityEngine.Object.FindObjectsOfType<NetworkObject>(includeInactive: true).Where((c) => c.NetworkManager == m_NetworkManager);

#endif
            foreach (var sobj in inSceneNetworkObjects)
            {
                if (sobj.IsSceneObject.HasValue && sobj.IsSceneObject.Value && !sobj.IsSpawned)
                {
                    m_DespawnedInSceneObjectsSync.Add(sobj);
                }
            }
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
                case SceneEventType.Load:
                case SceneEventType.Unload:
                case SceneEventType.Synchronize:
                case SceneEventType.ReSynchronize:
                case SceneEventType.LoadEventCompleted:
                case SceneEventType.UnloadEventCompleted:
                case SceneEventType.ActiveSceneChanged:
                case SceneEventType.ObjectSceneChanged:
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

        internal bool EnableSerializationLogs = false;

        private void LogArray(byte[] data, int start = 0, int stop = 0, StringBuilder builder = null)
        {
            var usingExternalBuilder = builder != null;
            if (!usingExternalBuilder)
            {
                builder = new StringBuilder();
            }

            if (stop == 0)
            {
                stop = data.Length;
            }

            builder.AppendLine($"[Start Data Dump][Start = {start}][Stop = {stop}] Size ({stop - start})");
            for (int i = start; i < stop; i++)
            {
                builder.Append($"{data[i]:X2} ");
            }
            builder.Append("\n");

            if (!usingExternalBuilder)
            {
                UnityEngine.Debug.Log(builder.ToString());
            }
        }

        internal bool ForwardSynchronization;

        /// <summary>
        /// Client and Server Side:
        /// Serializes data based on the SceneEvent type (<see cref="SceneEventType"/>)
        /// </summary>
        /// <param name="writer"><see cref="FastBufferWriter"/> to write the scene event data</param>
        internal void Serialize(FastBufferWriter writer)
        {
            // Write the scene event type
            writer.WriteValueSafe(SceneEventType);

            if (m_NetworkManager.DistributedAuthorityMode)
            {
                BytePacker.WriteValueBitPacked(writer, TargetClientId);
                BytePacker.WriteValueBitPacked(writer, SenderClientId);
            }

            if (SceneEventType == SceneEventType.ActiveSceneChanged)
            {
                writer.WriteValueSafe(ActiveSceneAsset);
                return;
            }

            if (SceneEventType == SceneEventType.ObjectSceneChanged)
            {
                SerializeObjectsMovedIntoNewScene(writer);
                return;
            }

            // Write the scene loading mode
            writer.WriteValueSafe((byte)LoadSceneMode);

            // Write the scene event progress Guid
            if (SceneEventType != SceneEventType.Synchronize)
            {
                writer.WriteValueSafe(SceneEventProgressId);
            }
            else
            {
                writer.WriteValueSafe(ClientSynchronizationMode);
            }

            // Write the scene index and handle
            var sceneName = SceneAsset ?? "";
            writer.WriteValueSafe(sceneName);
            writer.WriteValueSafe(SceneHandle);

            switch (SceneEventType)
            {
                case SceneEventType.Synchronize:
                    {
                        if (ActiveSceneAsset == null)
                        {
                            Debug.LogError($"Synchronizing - ActiveSceneAsset but it hasn't been initialized yet");
                            ActiveSceneAsset = "";
                        }

                        writer.WriteValueSafe(ActiveSceneAsset);
                        WriteSceneSynchronizationData(writer);

                        if (EnableSerializationLogs)
                        {
                            LogArray(writer.ToArray(), 0, writer.Length);
                        }
                        break;
                    }
                case SceneEventType.Load:
                    {
                        if (m_NetworkManager.DistributedAuthorityMode && IsForwarding && m_NetworkManager.DAHost)
                        {
                            CopyInternalBuffer(ref writer);
                        }
                        else
                        {
                            SerializeScenePlacedObjects(writer);
                        }
                        break;
                    }
                case SceneEventType.SynchronizeComplete:
                    {
                        WriteClientSynchronizationResults(writer);
                        break;
                    }
                case SceneEventType.ReSynchronize:
                    {
                        WriteClientReSynchronizationData(writer);
                        break;
                    }
                case SceneEventType.LoadEventCompleted:
                case SceneEventType.UnloadEventCompleted:
                    {
                        WriteSceneEventProgressDone(writer);
                        break;
                    }
            }
        }

        private unsafe void CopyInternalBuffer(ref FastBufferWriter writer)
        {
            writer.WriteBytesSafe(InternalBuffer.GetUnsafePtrAtCurrentPosition(), InternalBuffer.Length);
        }

        /// <summary>
        /// Server Side:
        /// Called at the end of a <see cref="SceneEventType.Load"/> event once the scene is loaded and scene placed NetworkObjects
        /// have been locally spawned
        /// </summary>
        internal void WriteSceneSynchronizationData(FastBufferWriter writer)
        {
            var builder = (StringBuilder)null;
            if (EnableSerializationLogs)
            {
                builder = new StringBuilder();
                builder.AppendLine($"[Write][Synchronize-Start][WPos: {writer.Position}] Begin:");
            }
            // Write the scenes we want to load, in the order we want to load them
            var valArray = ScenesToSynchronize.ToArray();
            writer.WriteValueSafe(valArray.Length);
            foreach (var v in valArray)
            {
                writer.WriteValueSafe(v);
            }

            writer.WriteValueSafe(SceneHandlesToSynchronize.ToArray());
            // Store our current position in the stream to come back and say how much data we have written
            var positionStart = writer.Position;

            if (m_NetworkManager.DistributedAuthorityMode && ForwardSynchronization && m_NetworkManager.DAHost)
            {
                writer.WriteValueSafe(m_InternalBufferSize);
                CopyInternalBuffer(ref writer);
                if (EnableSerializationLogs)
                {
                    LogArray(writer.ToArray(), positionStart);
                }
                return;
            }

            // Size Place Holder -- Start
            // !!NOTE!!: Since this is a placeholder to be set after we know how much we have written,
            // for stream offset purposes this MUST not be a packed value!
            writer.WriteValueSafe(0);
            int totalBytes = 0;

            // Write the number of NetworkObjects we are serializing
            writer.WriteValueSafe(m_NetworkObjectsSync.Count);
            if (EnableSerializationLogs)
            {
                builder.AppendLine($"[Synchronize Objects][positionStart: {positionStart}][WPos: {writer.Position}][NO-Count: {m_NetworkObjectsSync.Count}] Begin:");
            }
            var distributedAuthority = m_NetworkManager.DistributedAuthorityMode;

            // Serialize all NetworkObjects that are spawned
            for (var i = 0; i < m_NetworkObjectsSync.Count; ++i)
            {
                var networkObject = m_NetworkObjectsSync[i];
                var noStart = writer.Position;
                // In distributed authority mode, we send the currently known observers of each NetworkObject to the client being synchronized.
                var sceneObject = m_NetworkObjectsSync[i].GetMessageSceneObject(TargetClientId, distributedAuthority);

                sceneObject.Serialize(writer);
                var noStop = writer.Position;
                totalBytes += noStop - noStart;
                if (EnableSerializationLogs)
                {
                    var offStart = noStart - (positionStart + sizeof(int));
                    var offStop = noStop - (positionStart + sizeof(int));
                    builder.AppendLine($"[Head: {offStart}][Tail: {offStop}][Size: {offStop - offStart}][{networkObject.name}][NID-{networkObject.NetworkObjectId}][Children: {networkObject.ChildNetworkBehaviours.Count}]");
                    LogArray(writer.ToArray(), noStart, noStop, builder);
                }
            }
            if (EnableSerializationLogs)
            {
                UnityEngine.Debug.Log(builder.ToString());
            }

            // Write the number of despawned in-scene placed NetworkObjects
            writer.WriteValueSafe(m_DespawnedInSceneObjectsSync.Count);
            // Write the scene handle and GlobalObjectIdHash value
            for (var i = 0; i < m_DespawnedInSceneObjectsSync.Count; ++i)
            {
                var noStart = writer.Position;
                writer.WriteValueSafe(m_DespawnedInSceneObjectsSync[i].GetSceneOriginHandle());
                writer.WriteValueSafe(m_DespawnedInSceneObjectsSync[i].GlobalObjectIdHash);
                var noStop = writer.Position;
                totalBytes += noStop - noStart;
            }

            // Size Place Holder -- End
            var positionEnd = writer.Position;
            var bytesWritten = (uint)(positionEnd - (positionStart + sizeof(uint)));
            writer.Seek(positionStart);
            // Write the total size written to the stream by NetworkObjects being serialized
            writer.WriteValueSafe(bytesWritten);
            writer.Seek(positionEnd);
            if (EnableSerializationLogs)
            {
                LogArray(writer.ToArray(), positionStart);
            }
        }

        /// <summary>
        /// Server Side:
        /// Called at the end of a <see cref="SceneEventType.Load"/> event once the scene is loaded and scene placed NetworkObjects
        /// have been locally spawned
        /// Maximum number of objects that could theoretically be synchronized is 65536
        /// </summary>
        internal void SerializeScenePlacedObjects(FastBufferWriter writer)
        {
            var numberOfObjects = (ushort)0;
            var headPosition = writer.Position;

            // Write our count place holder (must not be packed!)
            writer.WriteValueSafe((ushort)0);
            var distributedAuthority = m_NetworkManager.DistributedAuthorityMode;
            // If distributed authority mode and sending to the service, then ignore observers
            var distributedAuthoritySendingToService = distributedAuthority && TargetClientId == NetworkManager.ServerClientId;

            foreach (var keyValuePairByGlobalObjectIdHash in m_NetworkManager.SceneManager.ScenePlacedObjects)
            {
                foreach (var keyValuePairBySceneHandle in keyValuePairByGlobalObjectIdHash.Value)
                {
                    if (keyValuePairBySceneHandle.Value.Observers.Contains(TargetClientId) || distributedAuthoritySendingToService)
                    {
                        // Serialize the NetworkObject
                        var sceneObject = keyValuePairBySceneHandle.Value.GetMessageSceneObject(TargetClientId, distributedAuthority);
                        sceneObject.Serialize(writer);
                        numberOfObjects++;
                    }
                }
            }

            // Write the number of despawned in-scene placed NetworkObjects
            writer.WriteValueSafe(m_DespawnedInSceneObjectsSync.Count);
            // Write the scene handle and GlobalObjectIdHash value
            for (var i = 0; i < m_DespawnedInSceneObjectsSync.Count; ++i)
            {
                writer.WriteValueSafe(m_DespawnedInSceneObjectsSync[i].GetSceneOriginHandle());
                writer.WriteValueSafe(m_DespawnedInSceneObjectsSync[i].GlobalObjectIdHash);
            }

            var tailPosition = writer.Position;
            // Reposition to our count position to the head before we wrote our object count
            writer.Seek(headPosition);
            // Write number of NetworkObjects serialized (must not be packed!)
            writer.WriteValueSafe(numberOfObjects);
            // Set our position back to the tail
            writer.Seek(tailPosition);
        }

        /// <summary>
        /// Client and Server Side:
        /// Deserialize data based on the SceneEvent type.
        /// </summary>
        /// <param name="reader"></param>
        internal void Deserialize(FastBufferReader reader)
        {
            reader.ReadValueSafe(out SceneEventType);
            if (m_NetworkManager.DistributedAuthorityMode)
            {
                ByteUnpacker.ReadValueBitPacked(reader, out TargetClientId);
                ByteUnpacker.ReadValueBitPacked(reader, out SenderClientId);
            }

            if (SceneEventType == SceneEventType.ActiveSceneChanged)
            {
                reader.ReadValueSafe(out ActiveSceneAsset);
                return;
            }

            if (SceneEventType == SceneEventType.ObjectSceneChanged)
            {
                // Defer these scene event types if a client hasn't finished synchronizing
                if (!m_NetworkManager.IsConnectedClient)
                {
                    DeferObjectsMovedIntoNewScene(reader);
                }
                else
                {
                    DeserializeObjectsMovedIntoNewScene(reader);
                }
                return;
            }

            reader.ReadValueSafe(out byte loadSceneMode);
            LoadSceneMode = (LoadSceneMode)loadSceneMode;

            if (SceneEventType != SceneEventType.Synchronize)
            {
                reader.ReadValueSafe(out SceneEventProgressId);
            }
            else
            {
                reader.ReadValueSafe(out ClientSynchronizationMode);
            }

            reader.ReadValueSafe(out SceneAsset);
            reader.ReadValueSafe(out SceneHandle);

            switch (SceneEventType)
            {
                case SceneEventType.Synchronize:
                    {
                        reader.ReadValueSafe(out ActiveSceneAsset);
                        if (EnableSerializationLogs)
                        {
                            LogArray(reader.ToArray(), 0, reader.Length);
                        }
                        CopySceneSynchronizationData(reader);
                        break;
                    }
                case SceneEventType.SynchronizeComplete:
                    {
                        CheckClientSynchronizationResults(reader);
                        break;
                    }
                case SceneEventType.Load:
                    {
                        unsafe
                        {
                            // We store off the trailing in-scene placed serialized NetworkObject data to
                            // be processed once we are done loading.
                            m_HasInternalBuffer = true;
                            // We use Allocator.Persistent since scene loading could take longer than 4 frames
                            InternalBuffer = new FastBufferReader(reader.GetUnsafePtrAtCurrentPosition(), Allocator.Persistent, reader.Length - reader.Position);
                        }
                        break;
                    }
                case SceneEventType.ReSynchronize:
                    {
                        ReadClientReSynchronizationData(reader);
                        break;
                    }
                case SceneEventType.LoadEventCompleted:
                case SceneEventType.UnloadEventCompleted:
                    {
                        ReadSceneEventProgressDone(reader);
                        break;
                    }
            }
        }

        private int m_InternalBufferSize;

        /// <summary>
        /// Client Side:
        /// Prepares for a scene synchronization event and copies the scene synchronization data
        /// into the internal buffer to be used throughout the synchronization process.
        /// </summary>
        /// <param name="reader"></param>
        internal void CopySceneSynchronizationData(FastBufferReader reader)
        {
            m_NetworkObjectsSync.Clear();
            reader.ReadValueSafe(out int sceneCount);
            ScenesToSynchronize = new Queue<string>();
            for (int i = 0; i < sceneCount; i++)
            {
                reader.ReadValueSafe(out string s);
                ScenesToSynchronize.Enqueue(s);
            }

            reader.ReadValueSafe(out uint[] sceneHandlesToSynchronize);
            SceneHandlesToSynchronize = new Queue<uint>(sceneHandlesToSynchronize);

            // is not packed!
            reader.ReadValueSafe(out int sizeToCopy);
            m_InternalBufferSize = sizeToCopy;

            unsafe
            {
                if (!reader.TryBeginRead(sizeToCopy))
                {
                    throw new OverflowException("Not enough space in the buffer to read recorded synchronization data size.");
                }

                m_HasInternalBuffer = true;
                // We use Allocator.Persistent since scene synchronization will most likely take longer than 4 frames
                InternalBuffer = new FastBufferReader(reader.GetUnsafePtrAtCurrentPosition(), Allocator.Persistent, sizeToCopy);
                if (EnableSerializationLogs)
                {
                    LogArray(InternalBuffer.ToArray());
                }
            }
        }

        /// <summary>
        /// Client Side:
        /// This needs to occur at the end of a <see cref="SceneEventType.Load"/> event when the scene has finished loading
        /// Maximum number of objects that could theoretically be synchronized is 65536
        /// </summary>
        internal void DeserializeScenePlacedObjects()
        {
            try
            {
                // is not packed!
                InternalBuffer.ReadValueSafe(out ushort newObjectsCount);
                var sceneObjects = new List<NetworkObject>();
                for (ushort i = 0; i < newObjectsCount; i++)
                {
                    var sceneObject = new NetworkObject.SceneObject();
                    sceneObject.Deserialize(InternalBuffer);

                    if (sceneObject.IsSceneObject)
                    {
                        // Set our relative scene to the NetworkObject
                        m_NetworkManager.SceneManager.SetTheSceneBeingSynchronized(sceneObject.NetworkSceneHandle);
                    }

                    var networkObject = NetworkObject.AddSceneObject(sceneObject, InternalBuffer, m_NetworkManager);

                    if (sceneObject.IsSceneObject)
                    {
                        sceneObjects.Add(networkObject);
                    }
                }
                // Now deserialize the despawned in-scene placed NetworkObjects list (if any)
                DeserializeDespawnedInScenePlacedNetworkObjects();

                // Notify all newly spawned in-scene placed NetworkObjects that all in-scene placed
                // NetworkObjects have been spawned.
                foreach (var networkObject in sceneObjects)
                {
                    networkObject.InternalInSceneNetworkObjectsSpawned();
                }
            }
            finally
            {
                InternalBuffer.Dispose();
                m_HasInternalBuffer = false;
            }
        }

        /// <summary>
        /// Client Side:
        /// If there happens to be NetworkObjects in the final Event_Sync_Complete message that are no longer spawned,
        /// the server will compile a list and send back an Event_ReSync message to the client.  This is where the
        /// client handles any returned values by the server.
        /// </summary>
        /// <param name="reader"></param>
        internal void ReadClientReSynchronizationData(FastBufferReader reader)
        {
            reader.ReadValueSafe(out uint[] networkObjectsToRemove);

            if (networkObjectsToRemove.Length > 0)
            {
#if UNITY_2023_1_OR_NEWER
                var networkObjects = UnityEngine.Object.FindObjectsByType<NetworkObject>(UnityEngine.FindObjectsSortMode.InstanceID);
#else
                var networkObjects = UnityEngine.Object.FindObjectsOfType<NetworkObject>();
#endif
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
        internal void WriteClientReSynchronizationData(FastBufferWriter writer)
        {
            //Write how many objects need to be removed
            writer.WriteValueSafe(m_NetworkObjectsToBeRemoved.ToArray());
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
        internal void CheckClientSynchronizationResults(FastBufferReader reader)
        {
            m_NetworkObjectsToBeRemoved.Clear();
            reader.ReadValueSafe(out uint networkObjectIdCount);
            for (int i = 0; i < networkObjectIdCount; i++)
            {
                reader.ReadValueSafe(out uint networkObjectId);
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
        internal void WriteClientSynchronizationResults(FastBufferWriter writer)
        {
            //Write how many objects were spawned
            writer.WriteValueSafe((uint)m_NetworkObjectsSync.Count);
            foreach (var networkObject in m_NetworkObjectsSync)
            {
                writer.WriteValueSafe((uint)networkObject.NetworkObjectId);
            }
        }

        /// <summary>
        /// For synchronizing any despawned in-scene placed NetworkObjects that were
        /// despawned by the server during synchronization or scene loading
        /// </summary>
        private void DeserializeDespawnedInScenePlacedNetworkObjects()
        {
            // Process all de-spawned in-scene NetworkObjects for this network session
            m_DespawnedInSceneObjects.Clear();
            InternalBuffer.ReadValueSafe(out int despawnedObjectsCount);
            var sceneCache = new Dictionary<int, Dictionary<uint, NetworkObject>>();

            for (int i = 0; i < despawnedObjectsCount; i++)
            {
                // We just need to get the scene
                InternalBuffer.ReadValueSafe(out int networkSceneHandle);
                InternalBuffer.ReadValueSafe(out uint globalObjectIdHash);
                var sceneRelativeNetworkObjects = new Dictionary<uint, NetworkObject>();
                if (!sceneCache.ContainsKey(networkSceneHandle))
                {
                    if (m_NetworkManager.SceneManager.ServerSceneHandleToClientSceneHandle.ContainsKey(networkSceneHandle))
                    {
                        var localSceneHandle = m_NetworkManager.SceneManager.ServerSceneHandleToClientSceneHandle[networkSceneHandle];
                        if (m_NetworkManager.SceneManager.ScenesLoaded.ContainsKey(localSceneHandle))
                        {
                            var objectRelativeScene = m_NetworkManager.SceneManager.ScenesLoaded[localSceneHandle];

                            // Find all active and non-active in-scene placed NetworkObjects
#if UNITY_2023_1_OR_NEWER
                            var inSceneNetworkObjects = UnityEngine.Object.FindObjectsByType<NetworkObject>(UnityEngine.FindObjectsInactive.Include, UnityEngine.FindObjectsSortMode.InstanceID).Where((c) =>
                            c.GetSceneOriginHandle() == localSceneHandle && (c.IsSceneObject != false)).ToList();
#else
                            var inSceneNetworkObjects = UnityEngine.Object.FindObjectsOfType<NetworkObject>(includeInactive: true).Where((c) =>
                            c.GetSceneOriginHandle() == localSceneHandle && (c.IsSceneObject != false)).ToList();
#endif


                            foreach (var inSceneObject in inSceneNetworkObjects)
                            {
                                if (!sceneRelativeNetworkObjects.ContainsKey(inSceneObject.GlobalObjectIdHash))
                                {
                                    sceneRelativeNetworkObjects.Add(inSceneObject.GlobalObjectIdHash, inSceneObject);
                                }
                            }
                            // Add this to a cache so we don't have to run this potentially multiple times (nothing will spawn or despawn during this time
                            sceneCache.Add(networkSceneHandle, sceneRelativeNetworkObjects);
                        }
                        else
                        {
                            UnityEngine.Debug.LogError($"In-Scene NetworkObject GlobalObjectIdHash ({globalObjectIdHash}) cannot find its relative local scene handle {localSceneHandle}!");
                        }
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"In-Scene NetworkObject GlobalObjectIdHash ({globalObjectIdHash}) cannot find its relative NetworkSceneHandle {networkSceneHandle}!");
                    }
                }
                else // Use the cached NetworkObjects if they exist
                {
                    sceneRelativeNetworkObjects = sceneCache[networkSceneHandle];
                }

                // Now find the in-scene NetworkObject with the current GlobalObjectIdHash we are looking for
                if (sceneRelativeNetworkObjects.ContainsKey(globalObjectIdHash))
                {
                    // Since this is a NetworkObject that was never spawned, we just need to send a notification
                    // out that it was despawned so users can make adjustments
                    sceneRelativeNetworkObjects[globalObjectIdHash].InvokeBehaviourNetworkDespawn();
                    if (!m_NetworkManager.SceneManager.ScenePlacedObjects.ContainsKey(globalObjectIdHash))
                    {
                        m_NetworkManager.SceneManager.ScenePlacedObjects.Add(globalObjectIdHash, new Dictionary<int, NetworkObject>());
                    }

                    if (!m_NetworkManager.SceneManager.ScenePlacedObjects[globalObjectIdHash].ContainsKey(sceneRelativeNetworkObjects[globalObjectIdHash].GetSceneOriginHandle()))
                    {
                        m_NetworkManager.SceneManager.ScenePlacedObjects[globalObjectIdHash].Add(sceneRelativeNetworkObjects[globalObjectIdHash].GetSceneOriginHandle(), sceneRelativeNetworkObjects[globalObjectIdHash]);
                    }
                }
                else
                {
                    UnityEngine.Debug.LogError($"In-Scene NetworkObject GlobalObjectIdHash ({globalObjectIdHash}) could not be found!");
                }
            }
        }

        /// <summary>
        /// Client Side:
        /// During the processing of a server sent Event_Sync, this method will be called for each scene once
        /// it is finished loading.  The client will also build a list of NetworkObjects that it spawned during
        /// this process which will be used as part of the Event_Sync_Complete response.
        /// </summary>
        /// <param name="networkManager"></param>
        internal void SynchronizeSceneNetworkObjects(NetworkManager networkManager)
        {
            var builder = (StringBuilder)null;
            if (EnableSerializationLogs)
            {
                builder = new StringBuilder();
            }

            try
            {
                // Process all spawned NetworkObjects for this network session
                InternalBuffer.ReadValueSafe(out int newObjectsCount);
                if (EnableSerializationLogs)
                {
                    builder.AppendLine($"[Read][Synchronize Objects][WPos: {InternalBuffer.Position}][NO-Count: {newObjectsCount}] Begin:");
                }

                for (int i = 0; i < newObjectsCount; i++)
                {
                    var noStart = InternalBuffer.Position;
                    var sceneObject = new NetworkObject.SceneObject();
                    sceneObject.Deserialize(InternalBuffer);

                    // If the sceneObject is in-scene placed, then set the scene being synchronized
                    if (sceneObject.IsSceneObject)
                    {
                        m_NetworkManager.SceneManager.SetTheSceneBeingSynchronized(sceneObject.NetworkSceneHandle);
                    }
                    var spawnedNetworkObject = NetworkObject.AddSceneObject(sceneObject, InternalBuffer, networkManager);

                    var noStop = InternalBuffer.Position;
                    if (EnableSerializationLogs)
                    {
                        builder.AppendLine($"[Head: {noStart}][Tail: {noStop}][Size: {noStop - noStart}][{spawnedNetworkObject.name}][NID-{spawnedNetworkObject.NetworkObjectId}][Children: {spawnedNetworkObject.ChildNetworkBehaviours.Count}]");
                        LogArray(InternalBuffer.ToArray(), noStart, noStop, builder);
                    }
                    // If we failed to deserialize the NetowrkObject then don't add null to the list
                    if (spawnedNetworkObject != null)
                    {
                        if (!m_NetworkObjectsSync.Contains(spawnedNetworkObject))
                        {
                            m_NetworkObjectsSync.Add(spawnedNetworkObject);
                        }
                    }
                }
                if (EnableSerializationLogs)
                {
                    UnityEngine.Debug.Log(builder.ToString());
                }

                // Notify that all in-scene placed NetworkObjects have been spawned
                foreach (var networkObject in m_NetworkObjectsSync)
                {
                    if (networkObject.IsSceneObject.HasValue && networkObject.IsSceneObject.Value)
                    {
                        networkObject.InternalInSceneNetworkObjectsSpawned();
                    }
                }

                // Now deserialize the despawned in-scene placed NetworkObjects list (if any)
                DeserializeDespawnedInScenePlacedNetworkObjects();

            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogException(ex);
                UnityEngine.Debug.Log(builder.ToString());
            }
            finally
            {
                InternalBuffer.Dispose();
                m_HasInternalBuffer = false;
            }
        }

        /// <summary>
        /// Writes the all clients loaded or unloaded completed and timed out lists
        /// </summary>
        /// <param name="writer"></param>
        internal void WriteSceneEventProgressDone(FastBufferWriter writer)
        {
            writer.WriteValueSafe((ushort)ClientsCompleted.Count);
            foreach (var clientId in ClientsCompleted)
            {
                writer.WriteValueSafe(clientId);
            }

            writer.WriteValueSafe((ushort)ClientsTimedOut.Count);
            foreach (var clientId in ClientsTimedOut)
            {
                writer.WriteValueSafe(clientId);
            }
        }

        /// <summary>
        /// Reads the all clients loaded or unloaded completed and timed out lists
        /// </summary>
        /// <param name="reader"></param>
        internal void ReadSceneEventProgressDone(FastBufferReader reader)
        {
            reader.ReadValueSafe(out ushort completedCount);
            ClientsCompleted = new List<ulong>();
            for (int i = 0; i < completedCount; i++)
            {
                reader.ReadValueSafe(out ulong clientId);
                ClientsCompleted.Add(clientId);
            }

            reader.ReadValueSafe(out ushort timedOutCount);
            ClientsTimedOut = new List<ulong>();
            for (int i = 0; i < timedOutCount; i++)
            {
                reader.ReadValueSafe(out ulong clientId);
                ClientsTimedOut.Add(clientId);
            }
        }

        /// <summary>
        /// Serialize scene handles and associated NetworkObjects that were migrated
        /// into a new scene.
        /// </summary>
        internal bool IsForwarding;
        private ulong m_OwnerId;

        private void SerializeObjectsMovedIntoNewScene(FastBufferWriter writer)
        {
            var sceneManager = m_NetworkManager.SceneManager;
            var ownerId = m_NetworkManager.LocalClientId;
            if (IsForwarding)
            {
                ownerId = m_OwnerId;
            }

            // Write the owner identifier
            writer.WriteValueSafe(ownerId);

            // Write the number of scene handles
            writer.WriteValueSafe(sceneManager.ObjectsMigratedIntoNewScene.Count);
            foreach (var sceneHandleObjects in sceneManager.ObjectsMigratedIntoNewScene)
            {
                if (!sceneManager.ObjectsMigratedIntoNewScene[sceneHandleObjects.Key].ContainsKey(ownerId))
                {
                    throw new Exception($"Trying to send object scene migration for Client-{ownerId} but the client has no entries to send!");
                }
                // Write the scene handle
                writer.WriteValueSafe(sceneHandleObjects.Key);
                // Write the number of NetworkObjectIds to expect
                writer.WriteValueSafe(sceneHandleObjects.Value[ownerId].Count);
                foreach (var networkObject in sceneHandleObjects.Value[ownerId])
                {
                    writer.WriteValueSafe(networkObject.NetworkObjectId);
                }
            }
        }

        /// <summary>
        /// Deserialize scene handles and associated NetworkObjects that need to
        /// be migrated into a new scene.
        /// </summary>
        private void DeserializeObjectsMovedIntoNewScene(FastBufferReader reader)
        {
            var sceneManager = m_NetworkManager.SceneManager;
            var spawnManager = m_NetworkManager.SpawnManager;

            var numberOfScenes = 0;
            var sceneHandle = 0;
            var objectCount = 0;
            var networkObjectId = (ulong)0;

            var ownerID = (ulong)0;
            reader.ReadValueSafe(out ownerID);
            m_OwnerId = ownerID;
            reader.ReadValueSafe(out numberOfScenes);

            for (int i = 0; i < numberOfScenes; i++)
            {
                reader.ReadValueSafe(out sceneHandle);
                if (!sceneManager.ObjectsMigratedIntoNewScene.ContainsKey(sceneHandle))
                {
                    sceneManager.ObjectsMigratedIntoNewScene.Add(sceneHandle, new Dictionary<ulong, List<NetworkObject>>());
                }

                if (!sceneManager.ObjectsMigratedIntoNewScene[sceneHandle].ContainsKey(ownerID))
                {
                    sceneManager.ObjectsMigratedIntoNewScene[sceneHandle].Add(ownerID, new List<NetworkObject>());
                }

                reader.ReadValueSafe(out objectCount);
                for (int j = 0; j < objectCount; j++)
                {
                    reader.ReadValueSafe(out networkObjectId);
                    if (!spawnManager.SpawnedObjects.ContainsKey(networkObjectId))
                    {
                        NetworkLog.LogError($"[Object Scene Migration] Trying to synchronize NetworkObjectId ({networkObjectId}) but it was not spawned or no longer exists!!");
                        continue;
                    }
                    var networkObject = spawnManager.SpawnedObjects[networkObjectId];
                    // Add NetworkObject scene migration to ObjectsMigratedIntoNewScene dictionary that is processed
                    sceneManager.ObjectsMigratedIntoNewScene[sceneHandle][ownerID].Add(networkObject);
                }
            }
        }


        /// <summary>
        /// While a client is synchronizing ObjectSceneChanged messages could be received.
        /// This defers any ObjectSceneChanged message processing to occur after the client
        /// has completed synchronization to assure the associated NetworkObjects being
        /// migrated to a new scene are instantiated and spawned.
        /// </summary>
        private void DeferObjectsMovedIntoNewScene(FastBufferReader reader)
        {
            var sceneManager = m_NetworkManager.SceneManager;
            var spawnManager = m_NetworkManager.SpawnManager;
            var ownerId = (ulong)0;
            var numberOfScenes = 0;
            var sceneHandle = 0;
            var objectCount = 0;
            var networkObjectId = (ulong)0;

            reader.ReadValueSafe(out ownerId);


            var deferredObjectsMovedEvent = new NetworkSceneManager.DeferredObjectsMovedEvent()
            {
                OwnerId = ownerId,
                ObjectsMigratedTable = new Dictionary<int, List<ulong>>(),
            };


            reader.ReadValueSafe(out numberOfScenes);
            for (int i = 0; i < numberOfScenes; i++)
            {
                reader.ReadValueSafe(out sceneHandle);
                deferredObjectsMovedEvent.ObjectsMigratedTable.Add(sceneHandle, new List<ulong>());
                reader.ReadValueSafe(out objectCount);
                for (int j = 0; j < objectCount; j++)
                {
                    reader.ReadValueSafe(out networkObjectId);
                    deferredObjectsMovedEvent.ObjectsMigratedTable[sceneHandle].Add(networkObjectId);
                }
            }
            sceneManager.DeferredObjectsMovedEvents.Add(deferredObjectsMovedEvent);
        }

        internal void ProcessDeferredObjectSceneChangedEvents()
        {
            var sceneManager = m_NetworkManager.SceneManager;
            var spawnManager = m_NetworkManager.SpawnManager;
            if (sceneManager.DeferredObjectsMovedEvents.Count == 0)
            {
                return;
            }
            foreach (var objectsMovedEvent in sceneManager.DeferredObjectsMovedEvents)
            {
                foreach (var keyEntry in objectsMovedEvent.ObjectsMigratedTable)
                {
                    if (!sceneManager.ObjectsMigratedIntoNewScene.ContainsKey(keyEntry.Key))
                    {
                        sceneManager.ObjectsMigratedIntoNewScene.Add(keyEntry.Key, new Dictionary<ulong, List<NetworkObject>>());
                    }
                    if (!sceneManager.ObjectsMigratedIntoNewScene[keyEntry.Key].ContainsKey(objectsMovedEvent.OwnerId))
                    {
                        sceneManager.ObjectsMigratedIntoNewScene[keyEntry.Key].Add(objectsMovedEvent.OwnerId, new List<NetworkObject>());
                    }

                    foreach (var objectId in keyEntry.Value)
                    {
                        if (!spawnManager.SpawnedObjects.ContainsKey(objectId))
                        {
                            NetworkLog.LogWarning($"[Deferred][Object Scene Migration] Trying to synchronize NetworkObjectId ({objectId}) but it was not spawned or no longer exists!");
                            continue;
                        }
                        var networkObject = spawnManager.SpawnedObjects[objectId];
                        if (!sceneManager.ObjectsMigratedIntoNewScene[keyEntry.Key][objectsMovedEvent.OwnerId].Contains(networkObject))
                        {
                            sceneManager.ObjectsMigratedIntoNewScene[keyEntry.Key][objectsMovedEvent.OwnerId].Add(networkObject);
                        }
                    }
                }
                objectsMovedEvent.ObjectsMigratedTable.Clear();
            }
            sceneManager.DeferredObjectsMovedEvents.Clear();
        }

        /// <summary>
        /// Used to release the pooled network buffer
        /// </summary>
        public void Dispose()
        {
            if (m_HasInternalBuffer)
            {
                InternalBuffer.Dispose();
                m_HasInternalBuffer = false;
            }
        }

        /// <summary>
        /// Constructor for SceneEventData
        /// </summary>
        internal SceneEventData(NetworkManager networkManager)
        {
            m_NetworkManager = networkManager;
            SceneEventId = XXHash.Hash32(Guid.NewGuid().ToString());
        }
    }
}
