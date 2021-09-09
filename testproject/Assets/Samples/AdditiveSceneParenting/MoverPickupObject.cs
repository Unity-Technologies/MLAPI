using System.Collections;
using UnityEngine;
using Unity.Netcode;

public class MoverPickupObject : GenericMover
{
    public override void OnNetworkSpawn()
    {
        name += "-" + NetworkObjectId.ToString();
        base.OnNetworkSpawn();
    }

    public void SetHunter(ulong hunterId)
    {
        if (IsServer)
        {
            SetBeingHunted(hunterId);
        }
    }

    public ulong GetHunter()
    {
        return GetHunterObjectId();
    }

    public bool IsHunterHoldingObject(ulong clientId)
    {
        return GetHunterObjectId() == clientId && IsHunterHoldingObject();
    }

    /// <summary>
    /// Server Side:
    /// Drops the object
    /// </summary>
    public void DropObject()
    {
        if (IsServer)
        {
            var newPosition = transform.parent.transform.position + Vector3.right + Vector3.forward;
            ResetAsDropped();
            StartCoroutine(NextPickupDelay(0.5f));
            transform.parent = null;
            transform.position = newPosition;
            m_LocalCollider.enabled = true;
            m_RigidBody.isKinematic = false;
            MovementEnabled.Value = true;
        }
    }

    public override void OnNetworkObjectParentChanged(NetworkObject parentNetworkObject)
    {
        base.OnNetworkObjectParentChanged(parentNetworkObject);

        if (IsServer)
        {
            if (parentNetworkObject == null)
            {
                if (!TryMoveBackToOriginalScene())
                {
                    Debug.LogError($"Could not move {nameof(SceneAwareNetworkObject)} back to its original scene!");
                }
            }
        }
    }

    private GameObject m_SetParent;
    // This is a work around for missed FixedUpdate Messages?
    private void Update()
    {
        if (NetworkManager != null && NetworkManager.IsListening && NetworkManager.IsServer)
        {
            if (m_SetParent != null)
            {
                if (NetworkObject.TrySetParent(m_SetParent.gameObject))
                {
                    SetPickedUp();
                    MovementEnabled.Value = false;
                    m_LocalCollider.enabled = false;
                    m_RigidBody.isKinematic = true;
                    transform.position = m_SetParent.transform.position + Vector3.up;
                }
                else
                {
                    Debug.LogError($"Client {m_SetParent.name} failed to pickup {name}!");
                }
                m_SetParent = null;
            }
        }
    }

    /// <summary>
    /// This detects if we can be picked up and if so we will start the parenting process
    /// This also includes important notes towards the end of this method regarding
    /// SceneAwareNetworkObject and keeping clients synchronized properly
    /// </summary>
    /// <param name="collider"></param>
    protected override void HandleCollision(Collider collider)
    {
        base.HandleCollision(collider);

        if (NetworkManager != null && NetworkManager.IsListening && IsServer)
        {
            if (collider.CompareTag("Floor") || collider.CompareTag("Boundary"))
            {
                return;
            }

            var seekerHunter = collider.gameObject.GetComponent<PickUpSeekerMovement>();
            if (seekerHunter == null || seekerHunter != null && seekerHunter.IsHoldingObject())
            {
                return;
            }

            if (seekerHunter.NetworkObject != null && seekerHunter.NetworkObject.IsPlayerObject)
            {
                // First see if we can be picked up
                if (!CanBePickedUp(seekerHunter.NetworkObject.OwnerClientId))
                {
                    // If not then bail early
                    return;
                }
                else // Double check to make sure we don't have a parent
                if (transform.parent != null)
                {
                    Debug.LogWarning($"{name} thinks it can be picked up but still has a parent!");
                }

                // If there is already someone hunting it, then don't pick it up
                if (GetHunterObjectId() == seekerHunter.NetworkObject.OwnerClientId)
                {
                    // In the event the MoverPickupObject is in a different scene, we need to
                    // **first** move the SceneAwareNetworkObject into the target scene so clients
                    // will synchronize to the current scene ***before we parent***.  Parenting first
                    // will move the SceneAwareNetworkObject into the seekerHunter's scene automatically
                    // and we cannot move something into a scene that has a parent (this is a Unity behavior).
                    MoveToScene(seekerHunter.NetworkObject.gameObject.scene);

                    // Delay parenting until this GameObject's next update by setting the target parent GameObject
                    m_SetParent = seekerHunter.NetworkObject.gameObject;
                }
            }
        }
    }

    /// <summary>
    /// All Parenting State Related Game Logic is From Here Down
    /// </summary>
    private NetworkVariable<bool> m_DelayUntilNextPickup = new NetworkVariable<bool>();
    private NetworkVariable<bool> m_HunterHoldingObject = new NetworkVariable<bool>();

    private NetworkVariable<bool> m_IsBeingHunted = new NetworkVariable<bool>();
    private NetworkVariable<short> m_HunterObjectId = new NetworkVariable<short>();


    public void SetPickedUp()
    {
        m_HunterHoldingObject.Value = true;
    }

    public bool IsHunterHoldingObject()
    {
        return m_HunterHoldingObject.Value;
    }


    public ulong GetHunterObjectId()
    {
        return (ulong)m_HunterObjectId.Value;
    }

    public void SetBeingHunted(ulong hunterObjectId)
    {
        m_HunterObjectId.Value = (short)hunterObjectId;
        m_IsBeingHunted.Value = true;
    }

    public bool CanBePickedUp(ulong hunterObjectId)
    {
        if (m_IsBeingHunted.Value)
        {
            return (hunterObjectId == (ulong)m_HunterObjectId.Value) && !m_DelayUntilNextPickup.Value;
        }

        return !m_DelayUntilNextPickup.Value;
    }

    protected void ResetAsDropped()
    {
        m_HunterObjectId.Value = -1;
        m_IsBeingHunted.Value = false;
        m_HunterHoldingObject.Value = false;
    }

    /// <summary>
    /// Resets the pickup object and delays for a bit until it can be picked up again to avoid
    /// immediate "pickup" when dropped.
    /// </summary>
    /// <param name="pickupDelay">how long to wait until it can be picked up</param>
    /// <returns></returns>
    public IEnumerator NextPickupDelay(float pickupDelay)
    {
        m_DelayUntilNextPickup.Value = true;
        yield return new WaitForSeconds(pickupDelay);
        m_DelayUntilNextPickup.Value = false;
        yield return null;
    }
}
