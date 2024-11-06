using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// Solves for incoming values that are jittered.
    /// Partially solves for message loss. Unclamped lerping helps hide this, but not completely
    /// </summary>
    /// <typeparam name="T">The type of interpolated value</typeparam>
    public abstract class BufferedLinearInterpolator<T> where T : struct
    {
        internal float MaxInterpolationBound = 3.0f;
        protected internal struct BufferedItem
        {
            public T Item;
            public double TimeSent;

            public BufferedItem(T item, double timeSent)
            {
                Item = item;
                TimeSent = timeSent;
            }
        }

        /// <summary>
        /// There's two factors affecting interpolation: buffering (set in NetworkManager's NetworkTimeSystem) and interpolation time, which is the amount of time it'll take to reach the target. This is to affect the second one.
        /// </summary>
        public float MaximumInterpolationTime = 0.1f;

        private const double k_SmallValue = 9.999999439624929E-11; // copied from Vector3's equal operator

        protected internal T m_InterpStartValue;
        protected internal T m_CurrentInterpValue;
        protected internal T m_InterpEndValue;

        private double m_EndTimeConsumed;
        private double m_StartTimeConsumed;

        protected internal readonly List<BufferedItem> m_Buffer = new List<BufferedItem>(k_BufferCountLimit);



        // Buffer consumption scenarios
        // Perfect case consumption
        // | 1 | 2 | 3 |
        // | 2 | 3 | 4 | consume 1
        // | 3 | 4 | 5 | consume 2
        // | 4 | 5 | 6 | consume 3
        // | 5 | 6 | 7 | consume 4
        // jittered case
        // | 1 | 2 | 3 |
        // | 2 | 3 |   | consume 1
        // | 3 |   |   | consume 2
        // | 4 | 5 | 6 | consume 3
        // | 5 | 6 | 7 | consume 4
        // bursted case (assuming max count is 5)
        // | 1 | 2 | 3 |
        // | 2 | 3 |   | consume 1
        // | 3 |   |   | consume 2
        // |   |   |   | consume 3
        // |   |   |   |
        // | 4 | 5 | 6 | 7 | 8 | --> consume all and teleport to last value <8> --> this is the nuclear option, ideally this example would consume 4 and 5
        // instead of jumping to 8, but since in OnValueChange we don't yet have an updated server time (updated in pre-update) to know which value
        // we should keep and which we should drop, we don't have enough information to do this. Another thing would be to not have the burst in the first place.

        // Constant absolute value for max buffer count instead of dynamic time based value. This is in case we have very low tick rates, so
        // that we don't have a very small buffer because of this.
        private const int k_BufferCountLimit = 100;
        private BufferedItem m_LastBufferedItemReceived;
        private int m_NbItemsReceivedThisFrame;

        private int m_LifetimeConsumedCount;

        private bool InvalidState => m_Buffer.Count == 0 && m_LifetimeConsumedCount == 0;

        internal bool EndOfBuffer => m_Buffer.Count == 0;

        internal bool InLocalSpace;

        protected internal virtual void OnConvertTransformSpace(Transform transform, bool inLocalSpace)
        {

        }

        internal void ConvertTransformSpace(Transform transform, bool inLocalSpace)
        {
            OnConvertTransformSpace(transform, inLocalSpace);
            InLocalSpace = inLocalSpace;
        }

        /// <summary>
        /// Resets interpolator to initial state
        /// </summary>
        public void Clear()
        {
            m_Buffer.Clear();
            m_EndTimeConsumed = 0.0d;
            m_StartTimeConsumed = 0.0d;
        }

        /// <summary>
        /// Teleports current interpolation value to targetValue.
        /// </summary>
        /// <param name="targetValue">The target value to teleport instantly</param>
        /// <param name="serverTime">The current server time</param>
        public void ResetTo(T targetValue, double serverTime)
        {
            m_LifetimeConsumedCount = 1;
            m_InterpStartValue = targetValue;
            m_InterpEndValue = targetValue;
            m_CurrentInterpValue = targetValue;
            m_Buffer.Clear();
            m_EndTimeConsumed = 0.0d;
            m_StartTimeConsumed = 0.0d;

            Update(0, serverTime, serverTime);
        }

        // todo if I have value 1, 2, 3 and I'm treating 1 to 3, I shouldn't interpolate between 1 and 3, I should interpolate from 1 to 2, then from 2 to 3 to get the best path
        private void TryConsumeFromBuffer(double renderTime, double serverTime)
        {
            int consumedCount = 0;
            // only consume if we're ready

            //  this operation was measured as one of our most expensive, and we should put some thought into this.
            //   NetworkTransform has (currently) 7 buffered linear interpolators (3 position, 3 scale, 1 rot), and
            //   each has its own independent buffer and 'm_endTimeConsume'.  That means every frame I have to do 7x
            //   these checks vs. if we tracked these values in a unified way
            if (renderTime >= m_EndTimeConsumed)
            {
                BufferedItem? itemToInterpolateTo = null;
                // assumes we're using sequenced messages for netvar syncing
                // buffer contains oldest values first, iterating from end to start to remove elements from list while iterating

                // calling m_Buffer.Count shows up hot in the profiler.
                for (int i = m_Buffer.Count - 1; i >= 0; i--) // todo stretch: consume ahead if we see we're missing values due to packet loss
                {
                    var bufferedValue = m_Buffer[i];
                    // Consume when ready and interpolate to last value we can consume. This can consume multiple values from the buffer
                    if (bufferedValue.TimeSent <= serverTime)
                    {
                        if (!itemToInterpolateTo.HasValue || bufferedValue.TimeSent > itemToInterpolateTo.Value.TimeSent)
                        {
                            if (m_LifetimeConsumedCount == 0)
                            {
                                // if interpolator not initialized, teleport to first value when available
                                m_StartTimeConsumed = bufferedValue.TimeSent;
                                m_InterpStartValue = bufferedValue.Item;
                            }
                            else if (consumedCount == 0)
                            {
                                // Interpolating to new value, end becomes start. We then look in our buffer for a new end.
                                m_StartTimeConsumed = m_EndTimeConsumed;
                                m_InterpStartValue = m_InterpEndValue;
                            }

                            if (bufferedValue.TimeSent > m_EndTimeConsumed)
                            {
                                itemToInterpolateTo = bufferedValue;
                                m_EndTimeConsumed = bufferedValue.TimeSent;
                                m_InterpEndValue = bufferedValue.Item;
                            }
                        }

                        m_Buffer.RemoveAt(i);
                        consumedCount++;
                        m_LifetimeConsumedCount++;
                    }
                }
            }
        }

        /// <summary>
        /// Convenience version of 'Update' mainly for testing
        ///  the reason we don't want to always call this version is so that on the calling side we can compute
        ///  the renderTime once for the many things being interpolated (and the many interpolators per object)
        /// </summary>
        /// <param name="deltaTime">time since call</param>
        /// <param name="serverTime">current server time</param>
        /// <returns>The newly interpolated value of type 'T'</returns>
        public T Update(float deltaTime, NetworkTime serverTime)
        {
            return Update(deltaTime, serverTime.TimeTicksAgo(1).Time, serverTime.Time);
        }

        /// <summary>
        /// Call to update the state of the interpolators before reading out
        /// </summary>
        /// <param name="deltaTime">time since last call</param>
        /// <param name="renderTime">our current time</param>
        /// <param name="serverTime">current server time</param>
        /// <returns>The newly interpolated value of type 'T'</returns>
        public T Update(float deltaTime, double renderTime, double serverTime)
        {
            TryConsumeFromBuffer(renderTime, serverTime);

            if (InvalidState)
            {
                throw new InvalidOperationException("trying to update interpolator when no data has been added to it yet");
            }

            // Interpolation example to understand the math below
            // 4   4.5      6   6.5
            // |   |        |   |
            // A   render   B   Server

            if (m_LifetimeConsumedCount >= 1) // shouldn't interpolate between default values, let's wait to receive data first, should only interpolate between real measurements
            {
                float t = 1.0f;
                double range = m_EndTimeConsumed - m_StartTimeConsumed;
                if (range > k_SmallValue)
                {
                    var rangeFactor = 1.0f / (float)range;

                    t = ((float)renderTime - (float)m_StartTimeConsumed) * rangeFactor;

                    if (t < 0.0f)
                    {
                        // There is no mechanism to guarantee renderTime to not be before m_StartTimeConsumed
                        // This clamps t to a minimum of 0 and fixes issues with longer frames and pauses

                        if (NetworkLog.CurrentLogLevel <= LogLevel.Developer)
                        {
                            NetworkLog.LogError($"renderTime was before m_StartTimeConsumed. This should never happen. {nameof(renderTime)} is {renderTime}, {nameof(m_StartTimeConsumed)} is {m_StartTimeConsumed}");
                        }
                        t = 0.0f;
                    }

                    if (t > MaxInterpolationBound) // max extrapolation
                    {
                        // TODO this causes issues with teleport, investigate
                        t = 1.0f;
                    }
                }

                var target = InterpolateUnclamped(m_InterpStartValue, m_InterpEndValue, t);
                m_CurrentInterpValue = Interpolate(m_CurrentInterpValue, target, deltaTime / MaximumInterpolationTime); // second interpolate to smooth out extrapolation jumps
            }

            m_NbItemsReceivedThisFrame = 0;
            return m_CurrentInterpValue;
        }

        /// <summary>
        /// Add measurements to be used during interpolation. These will be buffered before being made available to be displayed as "latest value".
        /// </summary>
        /// <param name="newMeasurement">The new measurement value to use</param>
        /// <param name="sentTime">The time to record for measurement</param>
        public void AddMeasurement(T newMeasurement, double sentTime)
        {
            m_NbItemsReceivedThisFrame++;

            // This situation can happen after a game is paused. When starting to receive again, the server will have sent a bunch of messages in the meantime
            // instead of going through thousands of value updates just to get a big teleport, we're giving up on interpolation and teleporting to the latest value
            if (m_NbItemsReceivedThisFrame > k_BufferCountLimit)
            {
                if (m_LastBufferedItemReceived.TimeSent < sentTime)
                {
                    m_LastBufferedItemReceived = new BufferedItem(newMeasurement, sentTime);
                    ResetTo(newMeasurement, sentTime);
                    // Next line keeps renderTime above m_StartTimeConsumed. Fixes pause/unpause issues
                    m_Buffer.Add(m_LastBufferedItemReceived);
                }

                return;
            }

            // Part the of reason for disabling extrapolation is how we add and use measurements over time.
            // TODO: Add detailed description of this area in Jira ticket
            if (sentTime > m_EndTimeConsumed || m_LifetimeConsumedCount == 0) // treat only if value is newer than the one being interpolated to right now
            {
                m_LastBufferedItemReceived = new BufferedItem(newMeasurement, sentTime);
                m_Buffer.Add(m_LastBufferedItemReceived);
            }
        }

        /// <summary>
        /// Gets latest value from the interpolator. This is updated every update as time goes by.
        /// </summary>
        /// <returns>The current interpolated value of type 'T'</returns>
        public T GetInterpolatedValue()
        {
            return m_CurrentInterpValue;
        }

        /// <summary>
        /// Method to override and adapted to the generic type. This assumes interpolation for that value will be clamped.
        /// </summary>
        /// <param name="start">The start value (min)</param>
        /// <param name="end">The end value (max)</param>
        /// <param name="time">The time value used to interpolate between start and end values (pos)</param>
        /// <returns>The interpolated value</returns>
        protected abstract T Interpolate(T start, T end, float time);

        /// <summary>
        /// Method to override and adapted to the generic type. This assumes interpolation for that value will not be clamped.
        /// </summary>
        /// <param name="start">The start value (min)</param>
        /// <param name="end">The end value (max)</param>
        /// <param name="time">The time value used to interpolate between start and end values (pos)</param>
        /// <returns>The interpolated value</returns>
        protected abstract T InterpolateUnclamped(T start, T end, float time);
    }

    /// <inheritdoc />
    /// <remarks>
    /// This is a buffered linear interpolator for a <see cref="float"/> type value
    /// </remarks>
    public class BufferedLinearInterpolatorFloat : BufferedLinearInterpolator<float>
    {
        /// <inheritdoc />
        protected override float InterpolateUnclamped(float start, float end, float time)
        {
            // Disabling Extrapolation:
            // TODO: Add Jira Ticket
            return Mathf.Lerp(start, end, time);
        }

        /// <inheritdoc />
        protected override float Interpolate(float start, float end, float time)
        {
            return Mathf.Lerp(start, end, time);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This is a buffered linear interpolator for a <see cref="Quaternion"/> type value
    /// </remarks>
    public class BufferedLinearInterpolatorQuaternion : BufferedLinearInterpolator<Quaternion>
    {
        /// <summary>
        /// Use <see cref="Quaternion.Slerp"/> when <see cref="true"/>.
        /// Use <see cref="Quaternion.Lerp"/> when <see cref="false"/>
        /// </summary>
        /// <remarks>
        /// When using half precision (due to the imprecision) using <see cref="Quaternion.Lerp"/> is
        /// less processor intensive (i.e. precision is already "imprecise").
        /// When using full precision (to maintain precision) using <see cref="Quaternion.Slerp"/> is
        /// more processor intensive yet yields more precise results.
        /// </remarks>
        public bool IsSlerp;

        /// <inheritdoc />
        protected override Quaternion InterpolateUnclamped(Quaternion start, Quaternion end, float time)
        {
            if (IsSlerp)
            {
                return Quaternion.Slerp(start, end, time);
            }
            else
            {
                return Quaternion.Lerp(start, end, time);
            }
        }

        /// <inheritdoc />
        protected override Quaternion Interpolate(Quaternion start, Quaternion end, float time)
        {
            if (IsSlerp)
            {
                return Quaternion.Slerp(start, end, time);
            }
            else
            {
                return Quaternion.Lerp(start, end, time);
            }
        }

        private Quaternion ConvertToNewTransformSpace(Transform transform, Quaternion rotation, bool inLocalSpace)
        {
            if (inLocalSpace)
            {
                return Quaternion.Inverse(transform.rotation) * rotation;

            }
            else
            {
                return transform.rotation * rotation;
            }
        }

        protected internal override void OnConvertTransformSpace(Transform transform, bool inLocalSpace)
        {
            for (int i = 0; i < m_Buffer.Count; i++)
            {
                var entry = m_Buffer[i];
                entry.Item = ConvertToNewTransformSpace(transform, entry.Item, inLocalSpace);
                m_Buffer[i] = entry;
            }

            m_InterpStartValue = ConvertToNewTransformSpace(transform, m_InterpStartValue, inLocalSpace);
            m_CurrentInterpValue = ConvertToNewTransformSpace(transform, m_CurrentInterpValue, inLocalSpace);
            m_InterpEndValue = ConvertToNewTransformSpace(transform, m_InterpEndValue, inLocalSpace);

            base.OnConvertTransformSpace(transform, inLocalSpace);
        }
    }

    /// <summary>
    /// A <see cref="BufferedLinearInterpolator<T>"/> <see cref="Vector3"/> implementation.
    /// </summary>
    public class BufferedLinearInterpolatorVector3 : BufferedLinearInterpolator<Vector3>
    {
        /// <summary>
        /// Use <see cref="Vector3.Slerp"/> when <see cref="true"/>.
        /// Use <see cref="Vector3.Lerp"/> when <see cref="false"/>
        /// </summary>
        public bool IsSlerp;
        /// <inheritdoc />
        protected override Vector3 InterpolateUnclamped(Vector3 start, Vector3 end, float time)
        {
            if (IsSlerp)
            {
                return Vector3.Slerp(start, end, time);
            }
            else
            {
                return Vector3.Lerp(start, end, time);
            }
        }

        /// <inheritdoc />
        protected override Vector3 Interpolate(Vector3 start, Vector3 end, float time)
        {
            if (IsSlerp)
            {
                return Vector3.Slerp(start, end, time);
            }
            else
            {
                return Vector3.Lerp(start, end, time);
            }
        }

        private Vector3 ConvertToNewTransformSpace(Transform transform, Vector3 position, bool inLocalSpace)
        {
            if (inLocalSpace)
            {
                return transform.InverseTransformPoint(position);

            }
            else
            {
                return transform.TransformPoint(position);
            }
        }

        protected internal override void OnConvertTransformSpace(Transform transform, bool inLocalSpace)
        {
            for (int i = 0; i < m_Buffer.Count; i++)
            {
                var entry = m_Buffer[i];
                entry.Item = ConvertToNewTransformSpace(transform, entry.Item, inLocalSpace);
                m_Buffer[i] = entry;
            }

            m_InterpStartValue = ConvertToNewTransformSpace(transform, m_InterpStartValue, inLocalSpace);
            m_CurrentInterpValue = ConvertToNewTransformSpace(transform, m_CurrentInterpValue, inLocalSpace);
            m_InterpEndValue = ConvertToNewTransformSpace(transform, m_InterpEndValue, inLocalSpace);

            base.OnConvertTransformSpace(transform, inLocalSpace);
        }
    }
}
