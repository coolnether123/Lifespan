using ModAPI.Core;
using System;

namespace Lifespan
{
    internal sealed class LifespanLogger : IModLogger
    {
        private readonly IModLogger _inner;
        private readonly LifespanConfig _config;

        internal LifespanLogger(IModLogger inner, LifespanConfig config)
        {
            _inner = inner;
            _config = config;
        }

        public bool IsDebugEnabled => _config.verboseLogging && _inner.IsDebugEnabled;
        public void Debug(string message) { if (IsDebugEnabled) _inner.Debug(message); }
        public void Info(string message) { _inner.Info(message); }
        public void Warn(string message) { _inner.Warn(message); }
        public void Error(string message) { _inner.Error(message); }
    }

    public static class LifespanLoggerExtensions
    {
        public static bool VerboseEnabled = false;

        public static void Debug(this IModLogger log, string message)
        {
            if (VerboseEnabled)
            {
                log.Info("[DEBUG] " + message);
            }
        }
    }
}
