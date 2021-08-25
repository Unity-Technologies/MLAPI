using UnityEngine;
using Unity.Netcode;

namespace TestProject.ManualTests
{
    /// <summary>
    /// A prototype component for syncing transforms
    /// </summary>
    [AddComponentMenu("Netcode/" + nameof(BandwidthTest))]
    public class BandwidthTest : NetworkBehaviour
    {
        private int m_IdCount = 2000;
        private NetworkList<int> m_Ids;
        private bool m_WriteDone = false;

        private void Start()
        {
            Debug.Log("Start");
        }

        private void Awake()
        {
            m_Ids = new NetworkList<int>();
            m_Ids.Settings.WritePermission = NetworkVariableWritePermission.ServerOnly;
            m_Ids.OnListChanged += ListChanged;

            Debug.Log("Awake");
        }

        private void ListChanged(NetworkListEvent<int> listEvent)
        {
            if (!IsServer)
            {
                Debug.Log(m_Ids.Count);
                if (m_Ids.Count == m_IdCount)
                {
                    Debug.Log("Passed");
                }
            }
        }

        private void Update()
        {
            // server test start
            if (!m_WriteDone && NetworkManager.Singleton.ConnectedClientsList.Count > 1)
            {
                for (int x = 0; x < m_IdCount; x++)
                {
                    m_Ids.Add(x);
                }

                m_WriteDone = true;
                Debug.Log("Writing done");
            }
        }
    }
}
