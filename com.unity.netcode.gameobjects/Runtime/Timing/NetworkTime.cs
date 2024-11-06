using System;
using UnityEngine;
using UnityEngine.Assertions;

namespace Unity.Netcode
{
    /// <summary>
    /// A struct to represent a point of time in a networked game.
    /// Time is stored as a combination of amount of passed ticks + a duration offset.
    /// This struct is meant to replace the Unity <see cref="Time"/> API for multiplayer gameplay.
    /// </summary>
    public struct NetworkTime
    {
        private double m_TimeSec;

        private uint m_TickRate;
        private double m_TickInterval;

        private int m_CachedTick;
        private double m_CachedTickOffset;

        /// <summary>
        /// Gets the amount of time which has passed since the last network tick.
        /// </summary>
        public double TickOffset => m_CachedTickOffset;

        /// <summary>
        /// Gets the tick, including partial tick value passed since it started.
        /// </summary>
        public double TickWithPartial => Tick + (TickOffset / m_TickInterval);

        /// <summary>
        /// Gets the current time. This is a non fixed time value and similar to <see cref="Time.time"/>.
        /// </summary>
        public double Time => m_TimeSec;

        /// <summary>
        /// Gets the current time as a float.
        /// </summary>
        public float TimeAsFloat => (float)m_TimeSec;

        /// <summary>
        /// Gets he current fixed network time. This is the time value of the last network tick. Similar to <see cref="Time.fixedUnscaledTime"/>.
        /// </summary>
        public double FixedTime => m_CachedTick * m_TickInterval;

        /// <summary>
        /// Gets the fixed delta time. This value is based on the <see cref="TickRate"/> and stays constant.
        /// Similar to <see cref="Time.fixedUnscaledTime"/> There is no equivalent to <see cref="Time.deltaTime"/>.
        /// </summary>
        public float FixedDeltaTime => (float)m_TickInterval;

        /// <summary>
        /// Gets the amount of network ticks which have passed until reaching the current time value.
        /// </summary>
        public int Tick => m_CachedTick;

        /// <summary>
        /// Gets the tickrate of the system of this <see cref="NetworkTime"/>.
        /// Ticks per second.
        /// </summary>
        public uint TickRate => m_TickRate;

        /// <summary>
        /// Creates a new instance of the <see cref="NetworkTime"/> struct.
        /// </summary>
        /// <param name="tickRate">The tickrate.</param>
        public NetworkTime(uint tickRate)
        {
            Assert.IsTrue(tickRate > 0, "Tickrate must be a positive value.");

            m_TickRate = tickRate;
            m_TickInterval = 1f / m_TickRate; // potential floating point precision issue, could result in different interval on different machines
            m_CachedTickOffset = 0;
            m_CachedTick = 0;
            m_TimeSec = 0;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="NetworkTime"/> struct.
        /// </summary>
        /// <param name="tickRate">The tickrate.</param>
        /// <param name="tick">The time will be created with a value where this many tick have already passed.</param>
        /// <param name="tickOffset">Can be used to create a <see cref="NetworkTime"/> with a non fixed time value by adding an offset to the given tick value.</param>
        public NetworkTime(uint tickRate, int tick, double tickOffset = 0d)
            : this(tickRate)
        {
            Assert.IsTrue(tickOffset < 1d / tickRate);

            m_CachedTickOffset = tickOffset;
            m_CachedTick = tick;
            m_TimeSec = tick * m_TickInterval + tickOffset;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="NetworkTime"/> struct.
        /// </summary>
        /// <param name="tickRate">The tickrate.</param>
        /// <param name="timeSec">The time value as a float.</param>
        public NetworkTime(uint tickRate, double timeSec)
            : this(tickRate)
        {
            this += timeSec;
        }


        /// <summary>
        /// Converts the network time into a fixed time value.
        /// </summary>
        /// <returns>A <see cref="NetworkTime"/> where Time is the FixedTime value of this instance.</returns>
        public NetworkTime ToFixedTime()
        {
            return new NetworkTime(m_TickRate, m_CachedTick);
        }

        /// <summary>
        /// Returns the time a number of ticks in the past.
        /// </summary>
        /// <param name="ticks">The number of ticks ago we're querying the time</param>
        /// <returns></returns>
        public NetworkTime TimeTicksAgo(int ticks, float offset = 0.0f)
        {
            return this - new NetworkTime(TickRate, ticks, offset);
        }

        private void UpdateCache()
        {
            double d = m_TimeSec / m_TickInterval;
            m_CachedTick = (int)d;
            // This check is needed due to double division imprecision of large numbers
            if ((d - m_CachedTick) >= 0.999999999999)
            {
                m_CachedTick++;
            }
            m_CachedTickOffset = ((d - Math.Truncate(d)) * m_TickInterval);

            // This handles negative time, decreases tick by 1 and makes offset positive.
            if (m_CachedTick < 0 && m_CachedTickOffset != 0d)
            {
                m_CachedTick--;
                m_CachedTickOffset = m_TickInterval + m_CachedTickOffset;
            }
        }

        /// <summary>
        /// Computes the time difference between two ticks
        /// </summary>
        /// <param name="a">End time</param>
        /// <param name="b">Start time</param>
        /// <returns>The time difference between start and end</returns>
        public static NetworkTime operator -(NetworkTime a, NetworkTime b)
        {
            return new NetworkTime(a.TickRate, a.Time - b.Time);
        }

        /// <summary>
        /// Computes the sum of two times
        /// </summary>
        /// <param name="a">First time</param>
        /// <param name="b">Second time</param>
        /// <returns>The sum of the two times passed in</returns>
        public static NetworkTime operator +(NetworkTime a, NetworkTime b)
        {
            return new NetworkTime(a.TickRate, a.Time + b.Time);
        }

        /// <summary>
        /// Computes the time a number of seconds later
        /// </summary>
        /// <param name="a">The start time</param>
        /// <param name="b">The number of seconds to add</param>
        /// <returns>The resulting time</returns>
        public static NetworkTime operator +(NetworkTime a, double b)
        {
            a.m_TimeSec += b;
            a.UpdateCache();
            return a;
        }

        /// <summary>
        /// Computes the time a number of seconds before
        /// </summary>
        /// <param name="a">The start time</param>
        /// <param name="b">The number of seconds to remove</param>
        /// <returns>The resulting time</returns>
        public static NetworkTime operator -(NetworkTime a, double b)
        {
            return a + -b;
        }
    }
}
