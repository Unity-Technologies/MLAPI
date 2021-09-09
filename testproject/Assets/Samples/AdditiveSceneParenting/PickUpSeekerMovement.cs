using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using TestProject.ManualTests;


/// <summary>
/// Used with GenericObjects to randomly move them around
/// </summary>
public class PickUpSeekerMovement : RandomMovement
{
    [SerializeField]
    private float m_DelayUntilSeekNewTarget = 5.0f;

    private Text m_PressDText;

    private MoverPickupObject m_PickThisUpWhenTriggered;

    private float m_LastSearchForPickup;

    public void Start()
    {
        var textObjects = FindObjectsOfType<Text>();
        foreach (var textObject in textObjects)
        {
            if (textObject.name == "KeyPressDNotification")
            {
                m_PressDText = textObject;
                m_PressDText.enabled = false;
            }
        }
    }

    public override void OnNetworkSpawn()
    {
        name += "-" + NetworkObjectId.ToString();
        base.OnNetworkSpawn();
    }

    private ulong[] m_ClientToSendTo = new ulong[1] { 0 };

    [ServerRpc(RequireOwnership = false)]
    private void SetBeingHuntedServerRpc(ulong networkObjectIdHunted, ulong clientIdHunter)
    {
        SetBeingHunted(networkObjectIdHunted, clientIdHunter);
    }

    /// <summary>
    /// Tags a MoverPickupObject as being hunted
    /// </summary>
    /// <param name="networkObjectIdHunted">MoverPickupObject's NetworkObjectId</param>
    /// <param name="clientIdHunter">client id hunting the MoverPickupObject</param>
    private void SetBeingHunted(ulong networkObjectIdHunted, ulong clientIdHunter)
    {
        if (IsServer)
        {
            if (NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(networkObjectIdHunted))
            {
                var pickupObject = NetworkManager.SpawnManager.SpawnedObjects[networkObjectIdHunted].GetComponent<MoverPickupObject>();
                pickupObject.SetHunter(clientIdHunter);
                if (clientIdHunter != NetworkManager.ServerClientId)
                {
                    m_ClientToSendTo[0] = clientIdHunter;
                    ConfirmPickupObjectHuntedClientRpc(networkObjectIdHunted, new ClientRpcParams() { Send = new ClientRpcSendParams() { TargetClientIds = m_ClientToSendTo } });
                }
                m_PickThisUpWhenTriggered = pickupObject;
            }
        }
        else
        {
            SetBeingHuntedServerRpc(networkObjectIdHunted, clientIdHunter);
        }
    }


    [ClientRpc]
    private void ConfirmPickupObjectHuntedClientRpc(ulong networkObjectIdHunted, ClientRpcParams clientParams)
    {
        if (NetworkManager.SpawnManager.SpawnedObjects.ContainsKey(networkObjectIdHunted))
        {
            m_PickThisUpWhenTriggered = NetworkManager.SpawnManager.SpawnedObjects[networkObjectIdHunted].GetComponent<MoverPickupObject>();
        }
    }


    public bool IsHoldingObject()
    {
        if (m_PickThisUpWhenTriggered != null)
        {
            return m_PickThisUpWhenTriggered.IsHunterHoldingObject();
        }
        return false;
    }


    private void Update()
    {
        if (IsOwner)
        {
            if (m_PickThisUpWhenTriggered != null && m_PickThisUpWhenTriggered.IsHunterHoldingObject(NetworkManager.LocalClientId))
            {
                if (m_PressDText != null && !m_PressDText.enabled)
                {
                    m_PressDText.enabled = true;
                }

                if (Input.GetKeyDown(KeyCode.D))
                {
                    DropObject();
                }
                return;
            }
            else
            {
                if (m_PressDText != null)
                {
                    m_PressDText.enabled = false;
                }
            }

            // Troll for objects and if we have an object follow it until it is picked up
            if (m_LastSearchForPickup < Time.realtimeSinceStartup)
            {
                if (m_PickThisUpWhenTriggered == null)
                {
                    var foundTargets = FindObjectsOfType<MoverPickupObject>();
                    if (foundTargets != null && foundTargets.Length > 0)
                    {
                        foreach (var target in foundTargets)
                        {
                            if (target.CanBePickedUp(NetworkManager.LocalClientId))
                            {
                                SetBeingHunted(target.NetworkObjectId, NetworkManager.LocalClientId);
                                break;
                            }
                        }
                    }
                    m_LastSearchForPickup = Time.realtimeSinceStartup + 1.0f;
                }
                else
                {
                    // Server can set the value before client notifies, so we have to watch for that scenario
                    if (m_PickThisUpWhenTriggered.CanBePickedUp(NetworkManager.LocalClientId))
                    {
                        m_Direction = (m_PickThisUpWhenTriggered.gameObject.transform.position - transform.position).normalized;
                        m_Direction.y = 0.0f;
                    }
                    else // Client grabbed this, let's find something else.
                    {
                        m_PickThisUpWhenTriggered = null;
                    }
                    m_LastSearchForPickup = Time.realtimeSinceStartup + 0.15f;
                }
            }
        }
    }

    /// <summary>
    /// Drops the object being carried
    /// </summary>
    private void DropObject()
    {
        if (IsServer)
        {
            m_PickThisUpWhenTriggered.DropObject();
        }
        else
        {
            DropObjectServerRpc();
        }
        ChangeDirection(true, false);
        m_Direction.y = 0;
        m_PickThisUpWhenTriggered = null;
        m_LastSearchForPickup = Time.realtimeSinceStartup + m_DelayUntilSeekNewTarget;
    }

    /// <summary>
    /// Clients have to send an RPC to drop the object
    /// </summary>
    [ServerRpc(RequireOwnership = false)]
    private void DropObjectServerRpc()
    {
        DropObject();
    }
}
