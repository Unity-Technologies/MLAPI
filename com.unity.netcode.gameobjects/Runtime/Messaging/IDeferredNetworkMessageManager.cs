
namespace Unity.Netcode
{
    internal interface IDeferredNetworkMessageManager
    {
        internal enum TriggerType
        {
            OnSpawn,
            OnAddPrefab,
            OnNextFrame,
        }

        /// <summary>
        /// Defers processing of a message until the moment a specific networkObjectId is spawned.
        /// This is to handle situations where an RPC or other object-specific message arrives before the spawn does,
        /// either due to it being requested in OnNetworkSpawn before the spawn call has been executed
        ///
        /// There is a one second maximum lifetime of triggers to avoid memory leaks. After one second has passed
        /// without the requested object ID being spawned, the triggers for it are automatically deleted.
        /// </summary>
        void DeferMessage(TriggerType trigger, ulong key, FastBufferReader reader, ref NetworkContext context, string messageType = null);

        /// <summary>
        /// Cleans up any trigger that's existed for more than a second.
        /// These triggers were probably for situations where a request was received after a despawn rather than before a spawn.
        /// </summary>
        void CleanupStaleTriggers();

        void ProcessTriggers(TriggerType trigger, ulong key);

        /// <summary>
        /// Cleans up any trigger that's existed for more than a second.
        /// These triggers were probably for situations where a request was received after a despawn rather than before a spawn.
        /// </summary>
        void CleanupAllTriggers();
    }
}
