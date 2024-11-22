namespace Unity.Netcode
{
    internal class SessionConfig
    {
        /// <summary>
        /// The running list of session versions
        /// </summary>
        public const uint NoFeatureCompatibility = 0;
        public const uint BypassFeatureCompatible = 1;
        public const uint ServerDistributionCompatible = 2;

        // The most current session version (!!!!set this when you increment!!!!!)
        public static uint PackageSessionVersion => ServerDistributionCompatible;

        internal uint SessionVersion;

        public bool ServiceSideDistribution;


        /// <summary>
        /// Service to client
        /// Set when the client receives a <see cref="ConnectionApprovedMessage"/>
        /// </summary>
        /// <param name="serviceConfig">the session's settings</param>
        public SessionConfig(ServiceConfig serviceConfig)
        {
            SessionVersion = serviceConfig.SessionVersion;
            ServiceSideDistribution = serviceConfig.ServerRedistribution;
        }

        /// <summary>
        /// Can be used to directly set the version.
        /// </summary>
        /// <remarks>
        /// If a client connects that does not support session configuration then
        /// this will be invoked. The default values set in the constructor should
        /// assume that no features are available.
        /// Can also be used for mock/integration testing version handling.
        /// </remarks>
        /// <param name="version">version to set</param>
        public SessionConfig(uint version)
        {
            SessionVersion = version;
            ServiceSideDistribution = false;
        }

        /// <summary>
        /// Client to Service
        /// Default package constructor set when <see cref="NetworkManager.Initialize(bool)"/> is invoked.
        /// </summary>
        public SessionConfig()
        {
            // The current
            SessionVersion = PackageSessionVersion;
            ServiceSideDistribution = false;
        }
    }
}
