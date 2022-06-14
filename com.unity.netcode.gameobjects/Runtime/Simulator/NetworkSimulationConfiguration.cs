﻿using UnityEngine;

namespace Unity.Netcode
{
    [CreateAssetMenu(
        fileName = nameof(NetworkSimulationConfiguration),
        menuName = "Multiplayer/" + nameof(NetworkSimulationConfiguration))]
    public class NetworkSimulationConfiguration : ScriptableObject
    {
        /// <summary>
        /// Network simulation configuration name.
        /// </summary>
        [field: SerializeField]
        public string Name { get; set; }

        /// <summary>
        /// Optional description of the configuration.
        /// </summary>
        [field: SerializeField]
        public string Description { get; set; }

        /// <summary>
        /// Value for the delay between packet in milliseconds.
        /// </summary>
        [field: SerializeField]
        public int PacketDelayMs { get; set; }

        /// <summary>
        /// Value for the network jitter (variance) in milliseconds.
        /// </summary>
        [field: SerializeField]
        public int PacketJitterMs { get; set; }

        /// <summary>
        /// Value for at which interval packet are dropped
        /// This value is a drop every X packet, not in time.
        /// </summary>
        [field: SerializeField]
        public int PacketLossInterval { get; set; }

        /// <summary>
        /// Value for the average percentage of packet are dropped.
        /// </summary>
        [field: SerializeField]
        public int PacketLossPercent { get; set; }

        /// <summary>
        /// Value for the percentage of packet that should be duplicate.
        /// </summary>
        [field: SerializeField]
        public int PacketDuplicationPercent { get; set; }

        // TODO: Clear up what this do
        [field: SerializeField]
        public int PacketFuzzFactor { get; set; }

        // TODO: Clear up what this do
        [field: SerializeField]
        public int PacketFuzzOffset { get; set; }

        // TODO: Is the description required here?
        /// <summary>
        /// Utility function to create a configuration at runtime.
        /// </summary>
        /// <param name="name">Name of the configuration.</param>
        /// <param name="description">Description of the configuration.</param>
        /// <param name="packetDelayMs">Value for the packet delay in milliseconds.</param>
        /// <param name="packetJitterMs">Value for the network jitter in milliseconds.</param>
        /// <param name="packetLossInterval">Value for the packet loss interval.</param>
        /// <param name="packetLossPercent">Value for the packet loss percentage.</param>
        /// <param name="packetDuplicationPercent">Value for the packet duplication percentage.</param>
        /// <param name="packetFuzzFactor"></param>
        /// <param name="packetFuzzOffset"></param>
        /// <returns>A valid simulation configuration.</returns>
        public static NetworkSimulationConfiguration Create(
            string name,
            string description,
            int packetDelayMs,
            int packetJitterMs,
            int packetLossInterval,
            int packetLossPercent,
            int packetDuplicationPercent,
            int packetFuzzFactor,
            int packetFuzzOffset)
        {
            var configuration = CreateInstance<NetworkSimulationConfiguration>();

            configuration.Name = name;
            configuration.Description = description;
            configuration.PacketDelayMs = packetDelayMs;
            configuration.PacketJitterMs = packetJitterMs;
            configuration.PacketLossInterval = packetLossInterval;
            configuration.PacketLossPercent = packetLossPercent;
            configuration.PacketDuplicationPercent = packetDuplicationPercent;
            configuration.PacketFuzzFactor = packetFuzzFactor;
            configuration.PacketFuzzOffset = packetFuzzOffset;

            return configuration;
        }
    }
}
