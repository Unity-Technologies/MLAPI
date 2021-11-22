using System;
using System.Collections.Generic;

namespace Unity.Netcode
{
    internal struct ConnectionApprovedMessage : INetworkMessage
    {
        public ulong OwnerClientId;
        public int NetworkTick;
        public int SceneObjectCount;

        // Not serialized, held as references to serialize NetworkVariable data
        public HashSet<NetworkObject> SpawnedObjectsList;

        private FastBufferReader m_ReceivedSceneObjectData;

        public void Serialize(FastBufferWriter writer)
        {
            if (!writer.TryBeginWrite(sizeof(ulong) + sizeof(int) + sizeof(int)))
            {
                throw new OverflowException(
                    $"Not enough space in the write buffer to serialize {nameof(ConnectionApprovedMessage)}");
            }
            writer.WriteValue(OwnerClientId);
            writer.WriteValue(NetworkTick);
            writer.WriteValue(SceneObjectCount);

            if (SceneObjectCount != 0)
            {
                // Serialize NetworkVariable data
                foreach (var sobj in SpawnedObjectsList)
                {
                    if (sobj.CheckObjectVisibility == null || sobj.CheckObjectVisibility(OwnerClientId))
                    {
                        sobj.Observers.Add(OwnerClientId);
                        var sceneObject = sobj.GetMessageSceneObject(OwnerClientId);
                        sceneObject.Serialize(writer);
                    }
                }
            }
        }

        public bool Deserialize(FastBufferReader reader, in NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            if (!networkManager.IsClient)
            {
                return false;
            }

            if (!reader.TryBeginRead(sizeof(ulong) + sizeof(int) + sizeof(int)))
            {
                throw new OverflowException(
                    $"Not enough space in the buffer to read {nameof(ConnectionApprovedMessage)}");
            }

            reader.ReadValue(out OwnerClientId);
            reader.ReadValue(out NetworkTick);
            reader.ReadValue(out SceneObjectCount);
            m_ReceivedSceneObjectData = reader;
            return true;
        }

        public void Handle(in NetworkContext context)
        {
            var networkManager = (NetworkManager)context.SystemOwner;
            networkManager.LocalClientId = OwnerClientId;
            networkManager.NetworkMetrics.SetConnectionId(networkManager.LocalClientId);

            var time = new NetworkTime(networkManager.NetworkTickSystem.TickRate, NetworkTick);
            networkManager.NetworkTimeSystem.Reset(time.Time, 0.15f); // Start with a constant RTT of 150 until we receive values from the transport.
            networkManager.NetworkTickSystem.Reset(networkManager.NetworkTimeSystem.LocalTime, networkManager.NetworkTimeSystem.ServerTime);

            networkManager.LocalClient = new NetworkClient() { ClientId = networkManager.LocalClientId };

            // Only if scene management is disabled do we handle NetworkObject synchronization at this point
            if (!networkManager.NetworkConfig.EnableSceneManagement)
            {
                networkManager.SpawnManager.DestroySceneObjects();

                // Deserializing NetworkVariable data is deferred from Receive() to Handle to avoid needing
                // to create a list to hold the data. This is a breach of convention for performance reasons.
                for (ushort i = 0; i < SceneObjectCount; i++)
                {
                    var sceneObject = new NetworkObject.SceneObject();
                    sceneObject.Deserialize(m_ReceivedSceneObjectData);
                    NetworkObject.AddSceneObject(sceneObject, m_ReceivedSceneObjectData, networkManager);
                }

                // Mark the client being connected
                networkManager.IsConnectedClient = true;
                // When scene management is disabled we notify after everything is synchronized
                networkManager.InvokeOnClientConnectedCallback(context.SenderId);
            }
        }
    }
}
