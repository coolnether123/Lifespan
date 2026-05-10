using ModAPI.Core;
using System;

namespace Lifespan
{
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
