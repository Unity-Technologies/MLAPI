using UnityEngine;

namespace Unity.Netcode
{
    internal class RealTimeProvider : IRealTimeProvider
    {
        public float RealTimeSinceStartup => Time.realtimeSinceStartup;
        public float UnscaledTime => Time.unscaledTime;
        public float UnscaledDeltaTime => Time.unscaledDeltaTime;
        public float DeltaTime => Time.deltaTime;
        public float FixedDeltaTime => Time.fixedDeltaTime;
    }
}
