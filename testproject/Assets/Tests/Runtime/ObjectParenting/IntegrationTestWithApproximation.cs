using UnityEngine;
using Unity.Netcode.TestHelpers.Runtime;

namespace TestProject.RuntimeTests
{
    public abstract class IntegrationTestWithApproximation : NetcodeIntegrationTest
    {
        private const float k_AproximateDeltaVariance = 0.01f;

        protected virtual float GetDeltaVarianceThreshold()
        {
            return k_AproximateDeltaVariance;
        }

        protected bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= GetDeltaVarianceThreshold();
        }

        protected bool Approximately(Vector2 a, Vector2 b)
        {
            var deltaVariance = GetDeltaVarianceThreshold();
            return Mathf.Abs(a.x - b.x) <= deltaVariance &&
                Mathf.Abs(a.y - b.y) <= deltaVariance;
        }

        protected bool Approximately(Vector3 a, Vector3 b)
        {
            var deltaVariance = GetDeltaVarianceThreshold();
            return Mathf.Abs(a.x - b.x) <= deltaVariance &&
                Mathf.Abs(a.y - b.y) <= deltaVariance &&
                Mathf.Abs(a.z - b.z) <= deltaVariance;
        }

        protected bool Approximately(Quaternion a, Quaternion b)
        {
            var deltaVariance = GetDeltaVarianceThreshold();
            return Mathf.Abs(a.x - b.x) <= deltaVariance &&
                Mathf.Abs(a.y - b.y) <= deltaVariance &&
                Mathf.Abs(a.z - b.z) <= deltaVariance &&
                Mathf.Abs(a.w - b.w) <= deltaVariance;
        }
    }
}
