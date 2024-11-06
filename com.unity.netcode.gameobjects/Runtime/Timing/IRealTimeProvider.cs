namespace Unity.Netcode
{
    internal interface IRealTimeProvider
    {
        float RealTimeSinceStartup { get; }
        float UnscaledTime { get; }
        float UnscaledDeltaTime { get; }
        float DeltaTime { get; }
        float FixedDeltaTime { get; }
    }
}
