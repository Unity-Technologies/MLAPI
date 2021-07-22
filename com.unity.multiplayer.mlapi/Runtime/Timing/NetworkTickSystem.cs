using System;
using Unity.Profiling;
using UnityEngine;

namespace MLAPI.Timing
{
    public class NetworkTickSystem
    {
#if DEVELOPMENT_BUILD || UNITY_EDITOR
        private static ProfilerMarker s_Tick = new ProfilerMarker($"{nameof(NetworkTimeSystem)}.Tick");
#endif

        /// <summary>
        /// Special value to indicate "No tick information"
        /// </summary>
        public const int NoTick = int.MinValue;

        /// <summary>
        /// The TickRate of the tick system. This is used to decide how often a fixed network tick is run.
        /// </summary>
        public int TickRate { get; }

        /// <summary>
        /// The current local time. This is the time at which predicted or client authoritative objects move. This value is accurate when called in Update or during the <see cref="Tick"/> event but does not work correctly for FixedUpdate.
        /// </summary>
        public NetworkTime LocalTime { get; internal set; }

        /// <summary>
        /// The current server time. This value is mostly used for internal purposes and to interpolate state received from the server. This value is accurate when called in Update or during the <see cref="Tick"/> event but does not work correctly for FixedUpdate.
        /// </summary>
        public NetworkTime ServerTime { get; internal set; }

        /// <summary>
        /// Gets invoked before every network tick.
        /// </summary>
        public event Action Tick;

        /// <summary>
        /// Creates a new instance of the <see cref="NetworkTickSystem"/> class.
        /// </summary>
        /// <param name="tickRate">The tick rate</param>
        /// <param name="localTimeSec">The initial local time to start at.</param>
        /// <param name="serverTimeSec">The initial server time to start at.</param>
        public NetworkTickSystem(int tickRate, double localTimeSec, double serverTimeSec)
        {
            TickRate = tickRate;
            Tick = null;
            LocalTime = new NetworkTime(tickRate, localTimeSec);
            ServerTime = new NetworkTime(tickRate, serverTimeSec);
        }

        /// <summary>
        /// Called after advancing the time system to run ticks based on the difference in time.
        /// </summary>
        public void UpdateTick(double localTimeSec, double serverTimeSec)
        {
            // store old local tick to know how many fixed ticks passed
            var previousLocalTick = LocalTime.Tick;

            LocalTime = new NetworkTime(TickRate, localTimeSec);
            ServerTime = new NetworkTime(TickRate, serverTimeSec);

            // cache times here so that we can adjust them to temporary values while simulating ticks.
            var cacheLocalTime = LocalTime;
            var cacheServerTime = ServerTime;

            var currentLocalTick = LocalTime.Tick;
            var localToServerDifference = currentLocalTick - ServerTime.Tick;

            for (int i = previousLocalTick + 1; i <= currentLocalTick; i++)
            {
                // set exposed time values to correct fixed values
                LocalTime = new NetworkTime(TickRate, i);
                ServerTime = new NetworkTime(TickRate, i - localToServerDifference);

#if DEVELOPMENT_BUILD || UNITY_EDITOR
                s_Tick.Begin();
#endif
#if UNITY_EDITOR && !UNITY_2020_2_OR_NEWER
            NetworkProfiler.StartTick(TickType.Event);
#endif
                Tick?.Invoke();

#if UNITY_EDITOR && !UNITY_2020_2_OR_NEWER
            NetworkProfiler.EndTick();
#endif
#if DEVELOPMENT_BUILD || UNITY_EDITOR
                s_Tick.End();
#endif
            }

            // Set exposed time to values from tick system
            LocalTime = cacheLocalTime;
            ServerTime = cacheServerTime;
        }
    }
}
