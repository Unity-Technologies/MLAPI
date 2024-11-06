using System;
using System.Runtime.CompilerServices;
using UnityEngine;
using Random = UnityEngine.Random;


namespace Unity.Netcode.TestHelpers.Runtime
{
    public abstract class IntegrationTestWithApproximation : NetcodeIntegrationTest
    {
        private const float k_AproximateDeltaVariance = 0.016f;

        protected string GetVector3Values(ref Vector3 vector3)
        {
            return $"({vector3.x:F6},{vector3.y:F6},{vector3.z:F6})";
        }

        protected string GetVector3Values(Vector3 vector3)
        {
            return GetVector3Values(ref vector3);
        }

        protected virtual float GetDeltaVarianceThreshold()
        {
            return k_AproximateDeltaVariance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected float EulerDelta(float a, float b)
        {
            return Mathf.DeltaAngle(a, b);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Vector3 EulerDelta(Vector3 a, Vector3 b)
        {
            return new Vector3(Mathf.DeltaAngle(a.x, b.x), Mathf.DeltaAngle(a.y, b.y), Mathf.DeltaAngle(a.z, b.z));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool ApproximatelyEuler(float a, float b)
        {
            return Mathf.Abs(EulerDelta(a, b)) <= GetDeltaVarianceThreshold();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool Approximately(float a, float b)
        {
            return Mathf.Abs(a - b) <= GetDeltaVarianceThreshold();
        }

        protected bool Approximately(Vector2 a, Vector2 b)
        {
            var deltaVariance = GetDeltaVarianceThreshold();
            return Math.Round(Mathf.Abs(a.x - b.x), 2) <= deltaVariance &&
                Math.Round(Mathf.Abs(a.y - b.y), 2) <= deltaVariance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool Approximately(Vector3 a, Vector3 b)
        {
            var deltaVariance = GetDeltaVarianceThreshold();
            return Math.Round(Mathf.Abs(a.x - b.x), 2) <= deltaVariance &&
                Math.Round(Mathf.Abs(a.y - b.y), 2) <= deltaVariance &&
                Math.Round(Mathf.Abs(a.z - b.z), 2) <= deltaVariance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool Approximately(Quaternion a, Quaternion b)
        {
            var deltaVariance = GetDeltaVarianceThreshold();
            return Mathf.Abs(a.x - b.x) <= deltaVariance &&
                Mathf.Abs(a.y - b.y) <= deltaVariance &&
                Mathf.Abs(a.z - b.z) <= deltaVariance &&
                Mathf.Abs(a.w - b.w) <= deltaVariance;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected bool ApproximatelyEuler(Vector3 a, Vector3 b)
        {
            return ApproximatelyEuler(a.x, b.x) && ApproximatelyEuler(a.y, b.y) && ApproximatelyEuler(a.z, b.z);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Vector3 GetRandomVector3(float min, float max)
        {
            return new Vector3(Random.Range(min, max), Random.Range(min, max), Random.Range(min, max));
        }

        public IntegrationTestWithApproximation(NetworkTopologyTypes networkTopologyType) : base(networkTopologyType) { }

        public IntegrationTestWithApproximation(HostOrServer hostOrServer) : base(hostOrServer) { }

        public IntegrationTestWithApproximation() : base() { }
    }
}
