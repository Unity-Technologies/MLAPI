﻿using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.Netcode
{
    [RequireComponent(typeof(UnityTransport))]
    public class NetworkSimulator : MonoBehaviour
    {
        private NetworkSimulatorConfiguration m_SimulatorConfiguration;

        public NetworkSimulatorConfiguration SimulatorConfiguration
        {
            get => m_SimulatorConfiguration;
            set
            {
                m_SimulatorConfiguration = value;
                UpdateLiveParameters();
            }
        }

        public void UpdateLiveParameters()
        {
            var transport = NetworkManager.Singleton.NetworkConfig?.NetworkTransport as UnityTransport;
            if (transport != null)
            {
                transport.UpdateSimulationPipelineParameters(SimulatorConfiguration);
            }
        }
    }
}
