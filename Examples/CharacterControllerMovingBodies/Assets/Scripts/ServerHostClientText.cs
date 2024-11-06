using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public class ServerHostClientText : NetworkBehaviour
{
    private Text m_DisplayText;

    private Color m_Color;
    private Color m_ColorAlpha;
    private Vector3 m_LocalPosition;

    public void SetColor(Color color)
    {
        m_Color = color;
        m_ColorAlpha = color;
        m_ColorAlpha.a = 0.35f;
    }

    private void Awake()
    {
        m_LocalPosition = transform.localPosition;
        m_DisplayText = GetComponent<Text>();
    }

    private void Start()
    {
        if (m_DisplayText != null)
        {
            m_DisplayText.text = string.Empty;
            SetColor(m_DisplayText.color);
        }
    }

    public override void OnNetworkSpawn()
    {
        if (m_DisplayText != null)
        {
            if (NetworkManager.IsServer)
            {
                m_DisplayText.text = NetworkManager.IsHost ? "Host" : "Server";
            }
            else if (NetworkManager.IsClient)
            {
                m_DisplayText.text = $"Client-{NetworkManager.LocalClientId}";
            }
        }
        transform.localPosition = m_LocalPosition;
    }

    public override void OnNetworkDespawn()
    {
        if (m_DisplayText != null)
        {
            m_DisplayText.text = string.Empty;
        }
        base.OnNetworkDespawn();
    }

    private bool m_LastFocusedValue;
    private void OnGUI()
    {
        if (!IsSpawned || m_LastFocusedValue == Application.isFocused)
        {
            return;
        }

        m_LastFocusedValue = Application.isFocused;

        if (m_LastFocusedValue)
        {
            m_DisplayText.color = m_Color;
        }
        else
        {
            m_DisplayText.color = m_ColorAlpha;
        }
    }
}
