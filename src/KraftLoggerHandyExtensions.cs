using System;
using System.Collections.Generic;
using System.Text;
using NLog;

namespace Ccf.Ck.Libs.Logging
{
    public static class KraftLoggerHandyExtensions
    {
        public class Arguments
        {
            public object[] arguments { get => arguments; private set => arguments = value; }
            public long Id { get => Id; private set => Id = value; }
            public LogLevel Level { get => Level; private set => Level = value; }
            public Arguments(long id, object[] args, LogLevel level)
            {
                arguments = args;
                Id = id;
                Level = level;
            }
        }
        private static Dictionary<NLog.LogLevel, Action<string, object[]>> loggers = new Dictionary<NLog.LogLevel, Action<string, object[]>>()
        {
            { LogLevel.Debug,  KraftLogger.LogDebug },
            { LogLevel.Error,  KraftLogger.LogError },
            { LogLevel.Fatal,  KraftLogger.LogCritical},
            { LogLevel.Info,  KraftLogger.LogInformation},
            { LogLevel.Trace,  KraftLogger.LogTrace},
            { LogLevel.Warn,  KraftLogger.LogWarning}
        };
       
        public static void Log(this Arguments args, string message,params object[] embeds)
        {
            string text = String.Format("<id:{0}>", args.Id) + String.Format(message, embeds);
            loggers[args.Level](text, args.arguments);

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="level">NLog.LogLevel for logging</param>
        /// <param name="id">If you have some id to identify a sequence place it here, if not place 0 for instance.</param>
        /// <param name="args">Additional arguments to include in the log (not ot embed in the message!)</param>
        /// <returns></returns>
        public static Arguments WithArgs(this LogLevel level, long id, params object[] args)
        {
            return new Arguments(id, args, level);
        }
        public static Arguments WithContext(this LogLevel level, long id)
        {
            return new Arguments(id, null, level);
        }

    }
}
