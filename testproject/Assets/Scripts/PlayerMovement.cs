using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class PlayerMovement : NetworkBehaviour
{
    [SerializeField]
    private float m_Speed = 20.0f;
    [SerializeField]
    private float m_RotSpeed = 5.0f;
    private Rigidbody m_Rigidbody;

    public static Dictionary<ulong, PlayerMovement> Players = new Dictionary<ulong, PlayerMovement>();

    private void Start()
    {
        m_Rigidbody = GetComponent<Rigidbody>();
        if (IsLocalPlayer)
        {
            var temp = transform.position;
            temp.y = 0.5f;
            transform.position = temp;
        }

        if (m_Rigidbody)
        {
            // Only the owner should ever move an object
            // If we don't set the non-local-player object as kinematic,
            // the local physics would apply and result in unwanted position
            // updates being sent up
            m_Rigidbody.isKinematic = !IsServer;
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        Players[OwnerClientId] = this; // todo should really have a NetworkStop for unregistering this...
    }

    [ServerRpc(RequireOwnership = true)]
    private void MovePlayerServerRpc(Vector3 displacement, float rotation)
    {
        transform.position += displacement;
        transform.rotation = Quaternion.Euler(0, rotation, 0) * transform.rotation;
    }

    private void FixedUpdate()
    {
        if (IsLocalPlayer)
        {
            // we want to move by a given amount in the direction the client currently sees the player facing
            var translation = Input.GetAxis("Vertical") * m_Speed * Time.fixedDeltaTime * transform.forward;
            // we want to rotate by a given amount
            var rotation = Input.GetAxis("Horizontal") * 90 * m_RotSpeed * Time.fixedDeltaTime;

            // we instruct the server to do so
            MovePlayerServerRpc(translation, rotation);
        }
    }
}
