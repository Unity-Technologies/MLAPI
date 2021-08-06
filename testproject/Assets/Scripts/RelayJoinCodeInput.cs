using UnityEngine;
using MLAPI.Transports;
using UnityEngine.UI;

public class RelayJoinCodeInput : MonoBehaviour
{
    public ConnectionModeScript ConnectionScript;
    private InputField m_TextInput;

    private void Start()
    {
        m_TextInput = GetComponent<InputField>();
    }

    private void Update()
    {
        if (m_TextInput.IsInteractable()) {
            if (!string.IsNullOrEmpty(ConnectionScript.GetRelayJoinCode())) {
                m_TextInput.text = ConnectionScript.GetRelayJoinCode();
                m_TextInput.readOnly = true;
            }
        }
    }

    public void SetJoinCode()
    {
        ConnectionScript.SetRelayJoinCode(m_TextInput.text);
    }
}
