using System;
using MediaBrowser.Model.Logging;

namespace Emby.Plugin.Danmu.Core.Extensions
{
    public static class ILoggerExtension
    {
        private static readonly string DefaultName = "com.fengymi.danmu";
        public static ILogger getDefaultLogger(this ILogManager logManager, params string?[] args)
        {
            return logManager.GetLogger(DefaultName);
        }

        public static void LogError(this ILogger logger, Exception? ex, string? message, params object?[] args)
        {
            logger.ErrorException(message, ex, args);
        }

        public static void LogInformation(this ILogger logger, string? message, params object?[] args)
        {
            logger.Info(message, args);
        }

        public static void LogDebug(this ILogger logger, string? message, params object?[] args)
        {
            logger.Debug(message, args);
        }
    }
}