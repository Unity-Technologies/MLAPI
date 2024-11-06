namespace Unity.Netcode.TestHelpers.Runtime
{
    public class MockTimeProvider : IRealTimeProvider
    {
        public float RealTimeSinceStartup => (float)s_DoubleRealTime;
        public float UnscaledTime => (float)s_DoubleRealTime;
        public float UnscaledDeltaTime => (float)s_DoubleDelta;
        public float DeltaTime => (float)s_DoubleDelta;

        // DANGO-EXP TODO: Figure out how we want to handle time travel with fixed delta time.
        public float FixedDeltaTime => (float)s_DoubleDelta;

        public static float StaticRealTimeSinceStartup => (float)s_DoubleRealTime;
        public static float StaticUnscaledTime => (float)s_DoubleRealTime;
        public static float StaticUnscaledDeltaTime => (float)s_DoubleDelta;
        public static float StaticDeltaTime => (float)s_DoubleDelta;

        private static double s_DoubleRealTime = 0;
        private static double s_DoubleDelta = 0;

        public static void TimeTravel(double amountOfTimeTraveled)
        {
            s_DoubleDelta = amountOfTimeTraveled;
            s_DoubleRealTime += amountOfTimeTraveled;
        }

        public static void Reset()
        {
            s_DoubleDelta = 0;
            s_DoubleRealTime = 0;
        }
    }
}
