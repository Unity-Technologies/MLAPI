using UnityEngine;
using Unity.Netcode;

/// <summary>
/// A general object used for pooling purposes
/// </summary>
public class GenericMover : SceneAwareNetworkObject
{
    private float m_Velocity;

    [HideInInspector]
    public bool HasHandler;

    [HideInInspector]
    public int SyncrhonizedObjectTypeIndex;

    protected Rigidbody m_RigidBody;
    protected Collider m_LocalCollider;
    private Vector3 m_Direction;

    [HideInInspector]
    public NetworkVariable<bool> MovementEnabled = new NetworkVariable<bool>();

    public override void OnNetworkSpawn()
    {
        m_RigidBody = GetComponent<Rigidbody>();
        m_LocalCollider = GetComponent<Collider>();

        if (!IsServer)
        {
            m_RigidBody.isKinematic = true;
            m_LocalCollider.enabled = false;
        }
        else
        {
            MovementEnabled.Value = true;
        }
        base.OnNetworkSpawn();
    }

    /// <summary>
    /// Sets the object's direction and velocity
    /// </summary>
    /// <param name="direction">vector3 direction</param>
    /// <param name="velocity">float velocity</param>
    public void SetDirectionAndVelocity(Vector3 direction, float velocity)
    {
        m_Direction = direction;
        m_Direction.Normalize();
        m_Direction.y = 0;
        m_Velocity = velocity;
    }

    /// <summary>
    /// Handles moving the object based on its current direction and velocity
    /// </summary>
    private void FixedUpdate()
    {
        if (NetworkManager != null && NetworkManager.IsListening && MovementEnabled.Value)
        {
            if (IsOwner)
            {
                m_RigidBody.MovePosition(transform.position + m_Direction * (m_Velocity * Time.fixedDeltaTime));
            }
        }
    }

    protected virtual void HandleCollision(Collider collider)
    {
        if (IsServer)
        {
            if (collider.gameObject.CompareTag("Floor") || collider.gameObject.CompareTag("GenericObject"))
            {
                return;
            }
            Vector3 collisionPoint = collider.ClosestPoint(transform.position);
            var moveRight = (collisionPoint.x < transform.position.x) ? -1 : 1;
            var moveDown = collisionPoint.z > transform.position.z ? -1 : 1;

            float ang = Random.Range(0, 2 * Mathf.PI);
            m_Direction.x = Mathf.Cos(ang) * moveRight;
            m_Direction.y = 0.0f;
            ang = Random.Range(0, 2 * Mathf.PI);
            m_Direction.z = Mathf.Sin(ang) * moveDown;
            m_Direction.Normalize();
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        HandleCollision(other);
    }

    private void OnCollisionStay(Collision collision)
    {
        HandleCollision(collision.collider);
    }
}
