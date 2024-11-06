using Unity.Netcode;
using UnityEngine;

namespace TestProject.ManualTests
{
    /// <summary>
    /// Used to simulate a player moving around
    /// </summary>
    public class PlayerMovementManager : NetworkBehaviour
    {
        public int MoveSpeed = 10;

        private RandomMovement m_RandomMovement;

        // Start is called before the first frame update
        private void Start()
        {
            m_RandomMovement = GetComponent<RandomMovement>();
        }

        private void Update()
        {
            if (!IsSpawned)
            {
                return;
            }

            if (m_RandomMovement.IsAuthority())
            {
                if (Input.GetKeyDown(KeyCode.Space) && IsOwner)
                {
                    m_RandomMovement.enabled = !m_RandomMovement.enabled;
                }
                if (m_RandomMovement.enabled)
                {
                    m_RandomMovement.Move(MoveSpeed);
                }
            }
            else if (IsOwner)
            {
                if (Input.GetKeyDown(KeyCode.Space))
                {
                    ToggleEnableDisableServerRpc();
                }
            }
        }

        [ServerRpc]
        private void ToggleEnableDisableServerRpc()
        {
            m_RandomMovement.enabled = !m_RandomMovement.enabled;
        }
    }
}
