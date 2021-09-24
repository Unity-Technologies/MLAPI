using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// This is an example of using more than one Network Prefab override when using a custom handler
/// USAGE NOTE: When using more than one network prefab, it is important to understand that each
/// client determines what prefab they will be using and will not be synchronized across other clients.
/// This feature is primarily to be used for things like platform specific Network Prefabs where
/// things like collision models or graphics related assets might need to vary between platforms.
/// The usage of different visual assets used is strictly for example purposes only.
/// </summary>
public class NetworkPrefabHandlerObjectPoolOverride : NetworkBehaviour, INetworkPrefabInstanceHandler
{
    private GameObject m_ObjectToPool;

    [SerializeField]
    private GameObject m_ObjectToOverride;

    [SerializeField]
    private List<GameObject> m_ObjectOverrides;

    [SerializeField]
    private int m_ObjectPoolSize = 15;

    [SerializeField]
    [Range(1, 5)]
    private int m_SpawnsPerSecond = 2;

    private Dictionary<int, List<GameObject>> m_ObjectsPool;
    private List<string> m_NameValidation;

    private bool m_IsSpawningObjects;

    public override void OnNetworkSpawn()
    {
        // Register your object to be overridden (m_ObjectToOverride) with this INetworkPrefabInstanceHandler implementation
        if (NetworkManager && NetworkManager.PrefabHandler != null)
        {
            NetworkManager.PrefabHandler.AddHandler(m_ObjectToOverride, this);
        }

        // Start with the base object to be overridden (i.e. Server mode will always use this)
        m_ObjectToPool = m_ObjectToOverride;

        // Host and Client need to do an extra step
        if (IsClient)
        {
            // Makes sure we have the right prefab to create a pool for (i.e. Clients and Hosts)
            m_ObjectToPool = NetworkManager.GetNetworkPrefabOverride(m_ObjectToPool);

            // Host Only:
            // Since the host will be spawning overrides, we need to manually create the link between the
            // m_ObjectToOverride and the objects that could override it (i.e. m_ObjectOverrides)
            if (IsHost)
            {
                // While this seems redundant, we could theoretically have several objects that we could potentially be spawning
                NetworkManager.PrefabHandler.RegisterHostGlobalObjectIdHashValues(m_ObjectToOverride, m_ObjectOverrides);
            }
        }

        m_ObjectsPool = new Dictionary<int, List<GameObject>>();
        m_NameValidation = new List<string>();
        for (int x = 0; x < m_ObjectOverrides.Count; x++)
        {
            // If we are a server, then we just create a big pool of the same base override object
            // otherwise for Host and Client we use the list of object overrides
            var objectIndex = (IsServer && !IsHost) ? 0 : x;
            var objectToPool = (IsServer && !IsHost) ? m_ObjectToOverride : m_ObjectOverrides[objectIndex];

            if (!m_ObjectsPool.ContainsKey(objectIndex))
            {
                m_ObjectsPool.Add(objectIndex, new List<GameObject>());
            }

            for (int y = 0; y < m_ObjectPoolSize; y++)
            {
                var newObject = Instantiate(objectToPool);

                // One way to verify this object exists
                // You could also make this a dictionary that linked to the actual GameObject instance
                newObject.name += m_ObjectsPool[objectIndex].Count.ToString();
                m_NameValidation.Add(newObject.name);

                // Make sure we start this object as inactive
                newObject.SetActive(false);
                m_ObjectsPool[objectIndex].Add(newObject);
            }
        }

        // Host and Server spawn the objects
        if (IsServer)
        {
            StartCoroutine(SpawnObjects());
        }
    }

    private GameObject GetNextSpawnObject(int synchronizedIndex = -1)
    {
        // If we are just a server use index 0, otherwise we are a host or client so get a random override object to spawn
        var indexType = IsServer && !IsHost ? 0 : Random.Range(0, m_ObjectOverrides.Count - 1);

        if (m_ObjectsPool.ContainsKey(indexType))
        {
            foreach (var gameObject in m_ObjectsPool[indexType])
            {
                if (!gameObject.activeInHierarchy)
                {
                    return gameObject;
                }
            }
            // We are out of objects, get the type of object we need to instantiate and add to the pool
            var objectToPool = (IsServer && !IsHost) ? m_ObjectToOverride : m_ObjectOverrides[indexType];

            // Expand our pool by 1 more NetworkObject
            var newObject = Instantiate(objectToPool);
            var genericObjectPooledBehaviour = NetworkObject.GetComponent<GenericPooledObjectBehaviour>();
            genericObjectPooledBehaviour.SyncrhonizedObjectTypeIndex = (IsServer && !IsHost) ? Random.Range(0, m_ObjectOverrides.Count - 1) : indexType;
            m_ObjectsPool[indexType].Add(newObject);
            return newObject;
        }
        // If requesting a bad index return null
        return null;
    }

    public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
    {
        var gameObject = GetNextSpawnObject();
        gameObject.transform.position = position;
        gameObject.transform.rotation = rotation;
        gameObject.SetActive(true);
        return gameObject.GetComponent<NetworkObject>();
    }

    public void Destroy(NetworkObject networkObject)
    {
        if (m_NameValidation.Contains(networkObject.gameObject.name))
        {
            networkObject.gameObject.SetActive(false);
        }
    }

    /// <summary>
    ///  Spawns the objects.
    /// </summary>
    /// <returns>IEnumerator</returns>
    private IEnumerator SpawnObjects()
    {
        //Exit if we are a client or we happen to not have a NetworkManager
        if (NetworkManager == null || (NetworkManager.IsClient && !NetworkManager.IsHost && !NetworkManager.IsServer))
        {
            yield return null;
        }

        m_IsSpawningObjects = true;

        var entitySpawnUpdateRate = 1.0f;

        while (m_IsSpawningObjects)
        {
            entitySpawnUpdateRate = 1.0f / (float)m_SpawnsPerSecond;

            GameObject go = GetNextSpawnObject();
            if (go != null)
            {
                go.transform.position = transform.position;
                go.SetActive(true);

                float ang = Random.Range(0.0f, 2 * Mathf.PI);
                go.GetComponent<GenericPooledObjectBehaviour>().SetDirectionAndVelocity(new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)), 4);

                var no = go.GetComponent<NetworkObject>();
                if (!no.IsSpawned)
                {
                    no.Spawn(true);
                }
            }
            yield return new WaitForSeconds(entitySpawnUpdateRate);
        }
    }
}

