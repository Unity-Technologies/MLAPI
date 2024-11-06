using UnityEngine;

namespace Unity.Netcode
{
    /// <summary>
    /// Helper class for logging
    /// </summary>
    public static class NetworkLog
    {
        /// <summary>
        /// Gets the current log level.
        /// </summary>
        /// <value>The current log level.</value>
        public static LogLevel CurrentLogLevel => NetworkManager.Singleton == null ? LogLevel.Normal : NetworkManager.Singleton.LogLevel;

        // internal logging

        /// <summary>
        /// Locally logs a info log with Netcode prefixing.
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void LogInfo(string message) => Debug.Log($"[Netcode] {message}");

        /// <summary>
        /// Locally logs a warning log with Netcode prefixing.
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void LogWarning(string message) => Debug.LogWarning($"[Netcode] {message}");

        /// <summary>
        /// Locally logs a error log with Netcode prefixing.
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void LogError(string message) => Debug.LogError($"[Netcode] {message}");

        /// <summary>
        /// Logs an info log locally and on the server if possible.
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void LogInfoServer(string message) => LogServer(message, LogType.Info);

        /// <summary>
        /// Logs an info log locally and on the session owner if possible.
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void LogInfoSessionOwner(string message) => LogServer(message, LogType.Info);

        /// <summary>
        /// Logs a warning log locally and on the server if possible.
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void LogWarningServer(string message) => LogServer(message, LogType.Warning);

        /// <summary>
        /// Logs an error log locally and on the server if possible.
        /// </summary>
        /// <param name="message">The message to log</param>
        public static void LogErrorServer(string message) => LogServer(message, LogType.Error);

        internal static NetworkManager NetworkManagerOverride;

        private static void LogServer(string message, LogType logType)
        {
            var networkManager = NetworkManagerOverride ??= NetworkManager.Singleton;
            // Get the sender of the local log
            ulong localId = networkManager?.LocalClientId ?? 0;
            bool isServer = networkManager && networkManager.DistributedAuthorityMode ? networkManager.LocalClient.IsSessionOwner :
                networkManager && !networkManager.DistributedAuthorityMode ? networkManager.IsServer : true;
            switch (logType)
            {
                case LogType.Info:
                    if (isServer)
                    {
                        LogInfoServerLocal(message, localId);
                    }
                    else
                    {
                        LogInfo(message);
                    }
                    break;
                case LogType.Warning:
                    if (isServer)
                    {
                        LogWarningServerLocal(message, localId);
                    }
                    else
                    {
                        LogWarning(message);
                    }
                    break;
                case LogType.Error:
                    if (isServer)
                    {
                        LogErrorServerLocal(message, localId);
                    }
                    else
                    {
                        LogError(message);
                    }
                    break;
            }

            if (!isServer && networkManager.NetworkConfig.EnableNetworkLogs)
            {
                var networkMessage = new ServerLogMessage
                {
                    LogType = logType,
                    Message = message,
                    SenderId = localId
                };
                var size = networkManager.ConnectionManager.SendMessage(ref networkMessage, NetworkDelivery.ReliableFragmentedSequenced, NetworkManager.ServerClientId);

                networkManager.NetworkMetrics.TrackServerLogSent(NetworkManager.ServerClientId, (uint)logType, size);
            }
        }

        private static string Header()
        {
            var networkManager = NetworkManagerOverride ??= NetworkManager.Singleton;
            if (networkManager.DistributedAuthorityMode)
            {
                return "Session-Owner";
            }
            return "Netcode-Server";
        }

        internal static void LogInfoServerLocal(string message, ulong sender) => Debug.Log($"[{Header()} Sender={sender}] {message}");
        internal static void LogWarningServerLocal(string message, ulong sender) => Debug.LogWarning($"[{Header()} Sender={sender}] {message}");
        internal static void LogErrorServerLocal(string message, ulong sender) => Debug.LogError($"[{Header()} Sender={sender}] {message}");

        internal enum LogType : byte
        {
            Info,
            Warning,
            Error,
            None
        }
    }
}
