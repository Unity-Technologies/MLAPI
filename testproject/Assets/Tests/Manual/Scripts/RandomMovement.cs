using UnityEngine;
using Unity.Netcode;

namespace TestProject.ManualTests
{
    /// <summary>
    /// Used with GenericObjects to randomly move them around
    /// </summary>
    public class RandomMovement : NetworkBehaviour, IPlayerMovement
    {
        protected Vector3 m_Direction;
        protected Rigidbody m_Rigidbody;


        public override void OnNetworkSpawn()
        {
            m_Rigidbody = GetComponent<Rigidbody>();
            if (NetworkObject != null && m_Rigidbody != null)
            {
                if (NetworkObject.IsOwner)
                {
                    ChangeDirection(true, true);
                }
            }
        }

        protected virtual void OnServerMovePlayer(Vector3 moveTowards)
        {
            m_MoveTowardsPosition = moveTowards;
        }


        /// <summary>
        /// Notify the server of any client side change in direction or speed
        /// </summary>
        /// <param name="moveTowards"></param>
        [ServerRpc(RequireOwnership = false)]
        private void MovePlayerServerRpc(Vector3 moveTowards)
        {
            OnServerMovePlayer(moveTowards);
        }

        private Vector3 m_MoveTowardsPosition;

        protected virtual void OnMoveObject(int speed)
        {
            // Server sets this locally
            if (IsServer && IsOwner)
            {
                m_MoveTowardsPosition = (m_Direction * speed);
            }
            else if (!IsServer && IsOwner)
            {
                // Client must sent Rpc
                MovePlayerServerRpc(m_Direction * speed * 1.05f);
            }
            else if (IsServer && !IsOwner)
            {
                m_MoveTowardsPosition = Vector3.Lerp(m_MoveTowardsPosition, Vector3.zero, 0.01f);
            }
        }


        public void Move(int speed)
        {
            OnMoveObject(speed);
        }

        protected virtual void OnFixedUpdateMovement()
        {
            if (IsServer && NetworkObject && NetworkObject.NetworkManager && NetworkObject.NetworkManager.IsListening)
            {
                if (m_Rigidbody == null)
                {
                    m_Rigidbody = GetComponent<Rigidbody>();
                }
                if (m_Rigidbody != null)
                {
                    m_Rigidbody.MovePosition(transform.position + (m_MoveTowardsPosition * Time.fixedDeltaTime));
                }
            }
        }


        // We just apply our current direction with magnitude to our current position during fixed update
        private void FixedUpdate()
        {
            OnFixedUpdateMovement();
        }


        protected void OnClientChangeDirection(Vector3 direction)
        {
            m_Direction = direction;
        }


        /// <summary>
        /// Handles server notification to client that we need to change direction
        /// </summary>
        /// <param name="direction"></param>
        [ClientRpc]
        protected void ChangeDirectionClientRpc(Vector3 direction)
        {
            OnClientChangeDirection(direction);
        }

        protected virtual void HandleCollision(Collision collision)
        {
            if (IsServer)
            {
                if (collision.gameObject.CompareTag("Floor") || collision.gameObject.CompareTag("GenericObject"))
                {
                    return;
                }
                Vector3 collisionPoint = collision.collider.ClosestPoint(transform.position);
                bool moveRight = collisionPoint.x < transform.position.x;
                bool moveDown = collisionPoint.z > transform.position.z;

                ChangeDirection(moveRight, moveDown);

                // If we are not the owner then we need to notify the client that their direction
                // must change
                if (!IsOwner)
                {
                    m_MoveTowardsPosition = m_Direction * m_MoveTowardsPosition.magnitude;
                    ChangeDirectionClientRpc(m_Direction);
                }
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            HandleCollision(collision);
        }

        protected virtual void ChangeDirection(bool moveRight, bool moveDown)
        {
            float ang = Random.Range(0, 2 * Mathf.PI);
            m_Direction.x = Mathf.Cos(ang) * (moveRight ? -1 : 1);
            m_Direction.y = 0.0f;
            ang = Random.Range(0, 2 * Mathf.PI);
            m_Direction.z = Mathf.Sin(ang) * (moveDown ? -1 : 1);
            m_Direction.Normalize();
        }
    }
}
