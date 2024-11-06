using System.Collections.Generic;
using UnityEngine;
using Random = System.Random;

namespace Unity.Netcode.EditorTests
{
    /// <summary>
    /// Helper functions for timing related tests. Allows to get a set of time steps and simulate time advancing without the need of a full playmode test.
    /// </summary>
    internal static class TimingTestHelper
    {
        public static List<float> GetRandomTimeSteps(float totalDuration, float min, float max, int seed)
        {
            var random = new Random(seed);
            var steps = new List<float>();

            while (totalDuration > 0f)
            {
                var next = Mathf.Lerp(min, max, (float)random.NextDouble());
                steps.Add(next);
                totalDuration -= next;
            }

            // correct overshoot at the end
            steps[steps.Count - 1] -= totalDuration;

            return steps;
        }

        public delegate void StepCheckDelegate(int step);

        public delegate void StepCheckResetDelegate(int step, bool reset);

        public static void ApplySteps(NetworkTimeSystem timeSystem, NetworkTickSystem tickSystem, List<float> steps, StepCheckDelegate stepCheck = null)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                timeSystem.Advance(step);
                tickSystem.UpdateTick(timeSystem.LocalTime, timeSystem.ServerTime);
                if (stepCheck != null)
                {
                    stepCheck(i);
                }
            }
        }

        public static void ApplySteps(NetworkTimeSystem timeSystem, NetworkTickSystem tickSystem, List<float> steps, StepCheckResetDelegate stepCheck = null)
        {
            for (var i = 0; i < steps.Count; i++)
            {
                var step = steps[i];
                var reset = timeSystem.Advance(step);
                tickSystem.UpdateTick(timeSystem.LocalTime, timeSystem.ServerTime);
                if (stepCheck != null)
                {
                    stepCheck(i, reset);
                }
            }
        }
    }
}

