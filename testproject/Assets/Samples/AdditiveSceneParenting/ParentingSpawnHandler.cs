using System.Collections.Generic;
using UnityEngine;

using Unity.Netcode;

public class ParentingSpawnHandler : NetworkBehaviour
{
    public GameObject PrefabToSpawnAndParent;

    [SerializeField]
    private float m_MoverVelocity;

    [Tooltip("When true, the MoverPickupObject will be spawned and moved into the scene this ParentingSpawnHandler is located within.")]
    [SerializeField]
    private bool m_SpawnLocalScene;

    public override void OnNetworkSpawn()
    {
        if (IsServer && PrefabToSpawnAndParent != null)
        {
            NetworkManager.OnClientConnectedCallback += NetworkManager_OnClientConnectedCallback;
            NetworkManager.OnClientDisconnectCallback += NetworkManager_OnClientDisconnectCallback;
            // Always spawn a thing to be picked up for the host
            if (IsHost)
            {
                SpawnThingToPickup(0);
            }
        }
        base.OnNetworkSpawn();
    }

    private void NetworkManager_OnClientDisconnectCallback(ulong clientId)
    {
        var pickupLogic = (MoverPickupObject)null;
        foreach (var spawnedThing in m_ThingsSpawned)
        {
            pickupLogic = spawnedThing.GetComponent<MoverPickupObject>();

            if (pickupLogic.CanBePickedUp(clientId) || (pickupLogic.GetHunter() == clientId && pickupLogic.IsHunterHoldingObject()))
            {
                break;
            }
            pickupLogic = null;
        }

        if (pickupLogic != null)
        {
            pickupLogic.DropObject();
            pickupLogic.GetComponent<NetworkObject>().Despawn(true);
            m_ThingsSpawned.Remove(pickupLogic.gameObject);
            Destroy(pickupLogic.gameObject);
        }
    }

    private void NetworkManager_OnClientConnectedCallback(ulong clientId)
    {
        if (clientId != NetworkManager.ServerClientId)
        {
            SpawnThingToPickup(clientId);
        }
    }

    private List<GameObject> m_ThingsSpawned = new List<GameObject>();

    private void SpawnThingToPickup(ulong clientId)
    {
        if (IsServer)
        {
            var numbertoSpawn = NetworkManager.ConnectedClientsIds.Length - m_ThingsSpawned.Count;
            for (int i = 0; i < numbertoSpawn; i++)
            {
                var objectToParent = Instantiate(PrefabToSpawnAndParent);
                objectToParent.transform.position = transform.position;
                objectToParent.transform.rotation = transform.rotation;
                objectToParent.transform.localScale = transform.localScale;
                var pickupObject = objectToParent.GetComponent<MoverPickupObject>();
                pickupObject.SpawnInScene(gameObject.scene);
                float ang = Random.Range(0.0f, 2 * Mathf.PI);
                pickupObject.SetDirectionAndVelocity(new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)), m_MoverVelocity);
                m_ThingsSpawned.Add(objectToParent);
            }
        }
    }
}



