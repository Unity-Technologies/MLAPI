using System.Collections.Generic;
using Unity.Profiling;

namespace Unity.Netcode
{
    /// <summary>
    /// An helper class that helps NetworkManager update NetworkBehaviours and replicate them down to connected clients.
    /// </summary>
    public class NetworkBehaviourUpdater
    {
        private NetworkManager m_NetworkManager;
        private NetworkConnectionManager m_ConnectionManager;
        private HashSet<NetworkObject> m_DirtyNetworkObjects = new HashSet<NetworkObject>();
        private HashSet<NetworkObject> m_PendingDirtyNetworkObjects = new HashSet<NetworkObject>();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private ProfilerMarker m_NetworkBehaviourUpdate = new ProfilerMarker($"{nameof(NetworkBehaviour)}.{nameof(NetworkBehaviourUpdate)}");
#endif

        internal void AddForUpdate(NetworkObject networkObject)
        {
            // Since this is a HashSet, we don't need to worry about duplicate entries
            m_PendingDirtyNetworkObjects.Add(networkObject);
        }

        /// <summary>
        /// Sends NetworkVariable deltas
        /// </summary>
        /// <param name="forceSend">internal only, when changing ownership we want to send this before the change in ownership message</param>
        internal void NetworkBehaviourUpdate(bool forceSend = false)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            m_NetworkBehaviourUpdate.Begin();
#endif
            try
            {
                m_DirtyNetworkObjects.UnionWith(m_PendingDirtyNetworkObjects);
                m_PendingDirtyNetworkObjects.Clear();

                // NetworkObject references can become null, when hidden or despawned. Once NUll, there is no point
                // trying to process them, even if they were previously marked as dirty.
                m_DirtyNetworkObjects.RemoveWhere((sobj) => sobj == null);

                if (m_ConnectionManager.LocalClient.IsServer)
                {
                    foreach (var dirtyObj in m_DirtyNetworkObjects)
                    {
                        for (int k = 0; k < dirtyObj.ChildNetworkBehaviours.Count; k++)
                        {
                            dirtyObj.ChildNetworkBehaviours[k].PreVariableUpdate();
                        }

                        for (int i = 0; i < m_ConnectionManager.ConnectedClientsList.Count; i++)
                        {
                            var client = m_ConnectionManager.ConnectedClientsList[i];
                            if (m_NetworkManager.DistributedAuthorityMode || dirtyObj.IsNetworkVisibleTo(client.ClientId))
                            {
                                // Sync just the variables for just the objects this client sees
                                for (int k = 0; k < dirtyObj.ChildNetworkBehaviours.Count; k++)
                                {
                                    dirtyObj.ChildNetworkBehaviours[k].NetworkVariableUpdate(client.ClientId, forceSend);
                                }
                            }
                        }
                    }
                }
                else
                {
                    // when client updates the server, it tells it about all its objects
                    foreach (var sobj in m_DirtyNetworkObjects)
                    {
                        if (sobj.IsOwner)
                        {
                            for (int k = 0; k < sobj.ChildNetworkBehaviours.Count; k++)
                            {
                                sobj.ChildNetworkBehaviours[k].PreVariableUpdate();
                            }
                            for (int k = 0; k < sobj.ChildNetworkBehaviours.Count; k++)
                            {
                                sobj.ChildNetworkBehaviours[k].NetworkVariableUpdate(NetworkManager.ServerClientId, forceSend);
                            }
                        }
                    }
                }

                foreach (var dirtyObj in m_DirtyNetworkObjects)
                {
                    for (int k = 0; k < dirtyObj.ChildNetworkBehaviours.Count; k++)
                    {
                        var behaviour = dirtyObj.ChildNetworkBehaviours[k];
                        for (int i = 0; i < behaviour.NetworkVariableFields.Count; i++)
                        {
                            // Set to true for NetworkVariable to ignore duplication of the
                            // "internal original value" for collections support.
                            behaviour.NetworkVariableFields[i].NetworkUpdaterCheck = true;
                            if (behaviour.NetworkVariableFields[i].IsDirty() &&
                                !behaviour.NetworkVariableIndexesToResetSet.Contains(i))
                            {
                                behaviour.NetworkVariableIndexesToResetSet.Add(i);
                                behaviour.NetworkVariableIndexesToReset.Add(i);
                            }
                            // Reset back to false when done
                            behaviour.NetworkVariableFields[i].NetworkUpdaterCheck = false;
                        }
                    }
                }
                // Now, reset all the no-longer-dirty variables
                foreach (var dirtyobj in m_DirtyNetworkObjects)
                {
                    dirtyobj.PostNetworkVariableWrite(forceSend);
                    // Once done processing, we set the previous owner id to the current owner id
                    dirtyobj.PreviousOwnerId = dirtyobj.OwnerClientId;
                }
                m_DirtyNetworkObjects.Clear();
            }
            finally
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                m_NetworkBehaviourUpdate.End();
#endif
            }
        }

        internal void Initialize(NetworkManager networkManager)
        {
            m_NetworkManager = networkManager;
            m_ConnectionManager = networkManager.ConnectionManager;
            m_NetworkManager.NetworkTickSystem.Tick += NetworkBehaviourUpdater_Tick;
        }

        internal void Shutdown()
        {
            m_NetworkManager.NetworkTickSystem.Tick -= NetworkBehaviourUpdater_Tick;
        }

        // Order of operations requires NetworkVariable updates first then showing NetworkObjects
        private void NetworkBehaviourUpdater_Tick()
        {
            // First update NetworkVariables
            NetworkBehaviourUpdate();

            // Then show any NetworkObjects queued to be made visible/shown
            m_NetworkManager.SpawnManager.HandleNetworkObjectShow();
        }
    }
}
