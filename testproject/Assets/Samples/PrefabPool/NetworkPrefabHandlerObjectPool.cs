using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
public class NetworkPrefabHandlerObjectPool : NetworkBehaviour, INetworkPrefabInstanceHandler
{
    [SerializeField]
    private GameObject m_ObjectToPool;

    [SerializeField]
    private int m_ObjectPoolSize = 15;

    [SerializeField]
    [Range(1, 5)]
    private int m_SpawnsPerSecond = 2;

    private List<GameObject> m_ObjectsPool;

    private bool m_IsSpawningObjects;

    public override void OnNetworkSpawn()
    {
        if (NetworkManager && NetworkManager.PrefabHandler != null)
        {
            NetworkManager.PrefabHandler.AddHandler(m_ObjectToPool, this);
        }

        // This assures we have the right prefab
        if (IsClient)
        {
            m_ObjectToPool = NetworkManager.GetNetworkPrefabOverride(m_ObjectToPool);
        }

        if (m_ObjectToPool != null)
        {
            m_ObjectsPool = new List<GameObject>();
            for (int i = 0; i < m_ObjectPoolSize; i++)
            {
                InstantiatePoolObject().SetActive(false);
            }
        }

        // Host and Server spawn the objects
        if (IsServer)
        {
            StartCoroutine(SpawnObjects());
        }
    }

    private GameObject InstantiatePoolObject()
    {
        m_ObjectsPool.Add(Instantiate(m_ObjectToPool));
        return m_ObjectsPool[m_ObjectsPool.Count - 1];
    }

    private GameObject GetNextSpawnObject()
    {
        foreach (var gameObject in m_ObjectsPool)
        {
            if (!gameObject.activeInHierarchy)
            {
                return gameObject;
            }
        }
        //We are out of objects, expand our pool by 1 more NetworkObject
        return InstantiatePoolObject();
    }

    public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
    {
        var gameObject = GetNextSpawnObject();
        gameObject.transform.position = position;
        gameObject.transform.rotation = rotation;
        gameObject.SetActive(true);
        return gameObject.GetComponent<NetworkObject>();
    }

    private void OnDisable()
    {
        if (NetworkManager && NetworkManager.PrefabHandler != null)
        {
            NetworkManager.PrefabHandler.RemoveHandler(m_ObjectToPool);
        }
    }

    public void Destroy(NetworkObject networkObject)
    {
        if (m_ObjectsPool.Contains(networkObject.gameObject))
        {
            networkObject.gameObject.SetActive(false);
        }
    }

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

            var go = GetNextSpawnObject();
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
