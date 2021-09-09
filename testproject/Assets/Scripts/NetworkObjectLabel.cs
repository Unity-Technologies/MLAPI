using UnityEngine;
using Unity.Netcode;

public class NetworkObjectLabel : NetworkBehaviour
{
    private TextMesh m_ObjectLabel;
    private MeshRenderer m_Renderer;

    private void OnGUI()
    {
        if(NetworkManager != null && NetworkManager.IsListening)
        {
            if (m_ObjectLabel == null)
            {
                m_ObjectLabel = GetComponent<TextMesh>();
            }
            m_ObjectLabel.text = NetworkObject.NetworkObjectId.ToString();
        }
    }
}
