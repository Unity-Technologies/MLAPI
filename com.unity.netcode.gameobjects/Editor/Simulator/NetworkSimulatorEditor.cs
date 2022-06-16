﻿using Unity.Netcode.Transports.UTP;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Netcode.Editor
{
    [CustomEditor(typeof(NetworkSimulator))]
    public class NetworkSimulatorEditor : UnityEditor.Editor
    {
        NetworkSimulator m_NetworkSimulator;

        VisualElement m_Inspector;

        public override VisualElement CreateInspectorGUI()
        {
            m_NetworkSimulator = target as NetworkSimulator;

            m_Inspector = new VisualElement();

            m_Inspector.Add(new NetworkEventsView(
                new NetworkEventsApi(m_NetworkSimulator, m_NetworkSimulator.GetComponent<UnityTransport>())));

            m_Inspector.Add(new NetworkTypeView(m_NetworkSimulator));

            return m_Inspector;
        }
    }
}
