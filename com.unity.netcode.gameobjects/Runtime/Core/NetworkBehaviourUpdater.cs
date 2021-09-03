using System.Collections.Generic;
using Unity.Profiling;

namespace Unity.Netcode
{
    public class NetworkBehaviourUpdater
    {
        private HashSet<NetworkObject> m_Touched = new HashSet<NetworkObject>();

#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private ProfilerMarker m_NetworkBehaviourUpdate = new ProfilerMarker($"{nameof(NetworkBehaviour)}.{nameof(NetworkBehaviourUpdate)}");
#endif

        internal void NetworkBehaviourUpdate(NetworkManager networkManager)
        {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
            m_NetworkBehaviourUpdate.Begin();
#endif
            try
            {
                if (networkManager.IsServer)
                {
                    m_Touched.Clear();
                    for (int i = 0; i < networkManager.ConnectedClientsList.Count; i++)
                    {
                        var client = networkManager.ConnectedClientsList[i];
                        var spawnedObjs = networkManager.SpawnManager.SpawnedObjectsList;
                        m_Touched.UnionWith(spawnedObjs);
                        foreach (var sobj in spawnedObjs)
                        {
                            // Sync just the variables for just the objects this client sees
                            for (int k = 0; k < sobj.ChildNetworkBehaviours.Count; k++)
                            {
                                sobj.ChildNetworkBehaviours[k].VariableUpdate(client.ClientId);
                            }
                        }
                    }

                    // Now, reset all the no-longer-dirty variables
                    foreach (var sobj in m_Touched)
                    {
                        for (int k = 0; k < sobj.ChildNetworkBehaviours.Count; k++)
                        {
                            sobj.ChildNetworkBehaviours[k].PostNetworkVariableWrite();
                        }
                    }
                }
                else
                {
                    // when client updates the server, it tells it about all its objects
                    foreach (var sobj in networkManager.SpawnManager.SpawnedObjectsList)
                    {
                        if (sobj.IsOwner)
                        {
                            for (int k = 0; k < sobj.ChildNetworkBehaviours.Count; k++)
                            {
                                sobj.ChildNetworkBehaviours[k].VariableUpdate(networkManager.ServerClientId);
                            }
                        }
                    }

                    // Now, reset all the no-longer-dirty variables
                    foreach (var sobj in networkManager.SpawnManager.SpawnedObjectsList)
                    {
                        for (int k = 0; k < sobj.ChildNetworkBehaviours.Count; k++)
                        {
                            sobj.ChildNetworkBehaviours[k].PostNetworkVariableWrite();
                        }
                    }
                }
            }
            finally
            {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                m_NetworkBehaviourUpdate.End();
#endif
            }
        }

    }
}
