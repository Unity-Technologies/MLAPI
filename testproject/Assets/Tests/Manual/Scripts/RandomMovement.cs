using UnityEngine;
using Unity.Netcode;

namespace TestProject.ManualTests
{
    /// <summary>
    /// Used with GenericObjects to randomly move them around
    /// </summary>
    public class RandomMovement : NetworkBehaviour, IPlayerMovement
    {
        private Vector3 m_Direction;
        private Rigidbody m_Rigidbody;


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

        private Vector3 m_MoveTowardsPosition;

        public void Move(int speed)
        {
            if (IsOwner)
            {
                m_MoveTowardsPosition = (m_Direction * speed);
            }
            else
            {
                m_MoveTowardsPosition = Vector3.Lerp(m_MoveTowardsPosition, Vector3.zero, 0.01f);
            }
        }

        // We just apply our current direction with magnitude to our current position during fixed update
        private void FixedUpdate()
        {
            if (IsOwner)
            {
                m_Rigidbody.MovePosition(transform.position + (m_MoveTowardsPosition * Time.fixedDeltaTime));
            }
        }

        private void OnCollisionStay(Collision collision)
        {
            if (collision.gameObject.CompareTag("Floor") || collision.gameObject.CompareTag("GenericObject"))
            {
                return;
            }
            Vector3 collisionPoint = collision.collider.ClosestPoint(transform.position);
            bool moveRight = collisionPoint.x < transform.position.x;
            bool moveDown = collisionPoint.z > transform.position.z;

            if (IsOwner)
            {
                ChangeDirection(moveRight, moveDown);
            }
        }

        private void ChangeDirection(bool moveRight, bool moveDown)
        {
            float ang = Random.Range(0, 2 * Mathf.PI);

            m_Direction.x = Mathf.Cos(ang);
            m_Direction.y = 0.0f;
            ang = Random.Range(0, 2 * Mathf.PI);
            m_Direction.z = Mathf.Sin(ang);
            m_Direction.Normalize();
        }
    }
}
