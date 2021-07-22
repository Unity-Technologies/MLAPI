using System;
using UnityEngine;

namespace MLAPI
{
    internal class ConnectionRtt
    {
        internal const int RttSize = 5; // number of RTT to keep an average of (plus one)
        internal const int RingSize = 64; // number of slots to use for RTT computations (max number of in-flight packets)

        private double[] m_RttSendTimes; // times at which packet were sent for RTT computations
        private int[] m_SendSequence; // tick, or other key, at which packets were sent (to allow matching)
        private double[] m_MeasuredLatencies; // measured latencies (ring buffer)
        private int m_LatenciesBegin = 0; // ring buffer begin
        private int m_LatenciesEnd = 0; // ring buffer end

        /// <summary>
        /// Round-trip-time data
        /// </summary>
        public struct Rtt
        {
            public double BestSec; // best RTT
            public double AverageSec; // average RTT
            public double WorstSec; // worst RTT
            public double LastSec; // latest ack'ed RTT
            public int SampleCount; // number of contributing samples
        }
        public ConnectionRtt()
        {
            m_RttSendTimes = new double[RingSize];
            m_SendSequence = new int[RingSize];
            m_MeasuredLatencies = new double[RingSize];
        }

        /// <summary>
        /// Returns the Round-trip-time computation for this client
        /// </summary>
        public Rtt GetRtt()
        {
            var ret = new Rtt(); // is this a memory alloc ? How do I get a stack alloc ?
            var index = m_LatenciesBegin;
            double total = 0.0;
            ret.BestSec = m_MeasuredLatencies[m_LatenciesBegin];
            ret.WorstSec = m_MeasuredLatencies[m_LatenciesBegin];

            while (index != m_LatenciesEnd)
            {
                total += m_MeasuredLatencies[index];
                ret.SampleCount++;
                ret.BestSec = Math.Min(ret.BestSec, m_MeasuredLatencies[index]);
                ret.WorstSec = Math.Max(ret.WorstSec, m_MeasuredLatencies[index]);
                index = (index + 1) % RttSize;
            }

            if (ret.SampleCount != 0)
            {
                ret.AverageSec = total / ret.SampleCount;
                // the latest RTT is one before m_LatenciesEnd
                ret.LastSec = m_MeasuredLatencies[(m_LatenciesEnd + (RingSize - 1)) % RingSize];
            }
            else
            {
                ret.AverageSec = 0;
                ret.BestSec = 0;
                ret.WorstSec = 0;
                ret.SampleCount = 0;
                ret.LastSec = 0;
            }

            return ret;
        }

        internal void NotifySend(int sequence, double timeSec)
        {
            m_RttSendTimes[sequence % RingSize] = timeSec;
            m_SendSequence[sequence % RingSize] = sequence;
        }

        internal void NotifyAck(int sequence, double timeSec)
        {
            // if the same slot was not used by a later send
            if (m_SendSequence[sequence % RingSize] == sequence)
            {
                double latency = timeSec - m_RttSendTimes[sequence % RingSize];

                m_MeasuredLatencies[m_LatenciesEnd] = latency;
                m_LatenciesEnd = (m_LatenciesEnd + 1) % RttSize;

                if (m_LatenciesEnd == m_LatenciesBegin)
                {
                    m_LatenciesBegin = (m_LatenciesBegin + 1) % RttSize;
                }
            }
        }
    }
}
