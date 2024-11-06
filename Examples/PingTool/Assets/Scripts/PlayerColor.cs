using System;
using Unity.Netcode;
using UnityEngine;


public class PlayerColor : NetworkBehaviour
{
    private static Color[] s_Colors = { Color.yellow, Color.green, Color.blue, Color.cyan, Color.magenta};
    public bool ApplyColorToChildren;

    public override void OnNetworkSpawn()
    {
        MeshRenderer meshRenderer = GetComponent<MeshRenderer>();
        ulong myId = GetComponent<NetworkObject>().OwnerClientId;
        var color = myId == NetworkManager.ServerClientId ? Color.red : s_Colors[myId % Convert.ToUInt64(s_Colors.Length)];
        meshRenderer.material.color = color;
        if (ApplyColorToChildren)
        {
            var meshRenderers = GetComponentsInChildren<MeshRenderer>();
            foreach (var childMeshRenderer in meshRenderers)
            {
                childMeshRenderer.material.color = color;
            }
        }

        if (IsLocalPlayer)
        {
            var gameObject = GameObject.Find("ServerHostClientDisplay");
            if (gameObject != null)
            {
                var serverHost = gameObject.GetComponent<ServerHostClientText>();
                serverHost?.SetColor(color);
            }
        }
        base.OnNetworkSpawn();
    }
}
