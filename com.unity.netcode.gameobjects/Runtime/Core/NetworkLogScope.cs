using System;

namespace TrollKing.Core
{
    public enum NetworkLoggingLevel
    {
        Debug,
        Info,
        Warn,
        Error,
        Exception,
        None
    }

    public class NetworkLogScope
    {
        private readonly string m_LoggerName;
        private readonly NetworkLoggingLevel m_Level = NetworkLoggingLevel.Info;

        public NetworkLogScope(string logName, NetworkLoggingLevel logLevel = NetworkLoggingLevel.Info)
        {
            m_LoggerName = logName;
            m_Level = logLevel;
        }

        public NetworkLoggingLevel GetLevel()
        {
            return m_Level;
        }

        public void Log(Func<string> stringProvider, NetworkLoggingLevel logLevel = NetworkLoggingLevel.Info)
        {
            if (logLevel >= m_Level)
            {
                string logString = stringProvider.Invoke();
                DateTime time = DateTime.Now;
                var shortTime = time.ToString("T");

                switch (logLevel)
                {
                    case NetworkLoggingLevel.Debug:
                        UnityEngine.Debug.Log($"[{shortTime}][DEBUG][{m_LoggerName}] {logString}");
                        break;
                    case NetworkLoggingLevel.Info:
                        UnityEngine.Debug.Log($"[{shortTime}][INFO][{m_LoggerName}] {logString}");
                        break;
                    case NetworkLoggingLevel.Warn:
                        UnityEngine.Debug.LogWarning($"[{shortTime}][WARN][{m_LoggerName}] {logString}");
                        break;
                    case NetworkLoggingLevel.Error:
                        UnityEngine.Debug.LogError($"[{shortTime}][ERROR][{m_LoggerName}] {logString}");
                        break;
                    case NetworkLoggingLevel.Exception:
                        UnityEngine.Debug.LogError($"[{shortTime}][EXCEPTION][{m_LoggerName}] {logString}");
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(logLevel), logLevel, null);
                }
            }
        }

        public void Debug(Func<string> logString)
        {
            Log(logString, NetworkLoggingLevel.Debug);
        }

        public void Info(Func<string> logString)
        {
            Log(logString, NetworkLoggingLevel.Info);
        }

        public void Warning(Func<string> logString)
        {
            Log(logString, NetworkLoggingLevel.Warn);
        }

        public void LogWarning(Func<string> logString)
        {
            Log(logString, NetworkLoggingLevel.Warn);
        }

        public void Error(Func<string> logString)
        {
            Log(logString, NetworkLoggingLevel.Error);
        }

        public void LogError(Func<string> logString)
        {
            Log(logString, NetworkLoggingLevel.Error);
        }

        public void LogException(Exception e)
        {
            UnityEngine.Debug.LogException(e);
        }

        public void LogError(Exception e)
        {
            UnityEngine.Debug.LogError($"[{m_LoggerName}] {e}");
            UnityEngine.Debug.LogException(e);
        }
    }
}
