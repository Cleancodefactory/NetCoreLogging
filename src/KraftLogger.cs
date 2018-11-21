using System;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using NLog;
using NLog.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using NLog.Targets;
using System.Collections.ObjectModel;

namespace Ccf.Ck.Libs.Logging
{
    public class KraftLogger : Microsoft.Extensions.Logging.ILogger
    { 
        private static Logger _Logger;
        private readonly NLogProviderOptions _Options;
        private static readonly object EmptyEventId = default(EventId);    // Cache boxing of empty EventId-struct
        private static readonly object ZeroEventId = default(EventId).Id;  // Cache boxing of zero EventId-Value
        private Tuple<string, string, string> _EventIdPropertyNames;
        private readonly IHttpContextAccessor _Accessor;

        public KraftLogger(Logger logger, IHttpContextAccessor accessor, NLogProviderOptions options)
        {
            _Logger = logger;
            _Accessor = accessor;
            _Options = options ?? GetDefaultOptions();
        }

        private NLogProviderOptions GetDefaultOptions()
        {
            NLogProviderOptions options = new NLogProviderOptions();
            options.CaptureMessageProperties = true;
            options.CaptureMessageTemplates = true;
            options.EventIdSeparator = ".";
            options.IgnoreEmptyEventId = true;
            return options;
        }

        public static ReadOnlyCollection<Target> GetAllTargets()
        {
            return LogManager.Configuration.AllTargets;
        }

        //todo  callsite showing the framework logging classes/methods
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            var nLogLogLevel = ConvertLogLevel(logLevel);
            if (!IsEnabled(nLogLogLevel))
            {
                return;
            }
            if (formatter == null)
            {
                throw new ArgumentNullException(nameof(formatter));
            }
            var message = formatter(state, exception);

            //message arguments are not needed as it is already checked that the loglevel is enabled.
            var eventInfo = LogEventInfo.Create(nLogLogLevel, _Logger.Name, message);
            eventInfo.Exception = exception;
            if (!_Options.IgnoreEmptyEventId || eventId.Id != 0 || !string.IsNullOrEmpty(eventId.Name))
            {
                // Attempt to reuse the same string-allocations based on the current <see cref="NLogProviderOptions.EventIdSeparator"/>
                var eventIdPropertyNames = _EventIdPropertyNames ?? new Tuple<string, string, string>(null, null, null);
                var eventIdSeparator = _Options.EventIdSeparator ?? string.Empty;
                if (!ReferenceEquals(eventIdPropertyNames.Item1, eventIdSeparator))
                {
                    // Perform atomic cache update of the string-allocations matching the current separator
                    eventIdPropertyNames = new Tuple<string, string, string>(
                        eventIdSeparator,
                        string.Concat("EventId", eventIdSeparator, "Id"),
                        string.Concat("EventId", eventIdSeparator, "Name"));
                    _EventIdPropertyNames = eventIdPropertyNames;
                }

                var idIsZero = eventId.Id == 0;
                eventInfo.Properties[eventIdPropertyNames.Item2] = idIsZero ? ZeroEventId : eventId.Id;
                eventInfo.Properties[eventIdPropertyNames.Item3] = eventId.Name;
                eventInfo.Properties["EventId"] = idIsZero && eventId.Name == null ? EmptyEventId : eventId;
            }
            if (exception != null)
            {
                MappedDiagnosticsContext.Set("KraftError", Utilities.Serialize(logLevel, exception));
                if (_Accessor.HttpContext != null) // you should check HttpContext 
                {
                    MappedDiagnosticsContext.Set("Args", Utilities.Serialize(logLevel, new object[] { _Accessor.HttpContext }));
                }
            }
            _Logger.Log(eventInfo);
        }

        /// <summary>
        /// Is logging enabled for this logger at this <paramref name="logLevel"/>?
        /// </summary>
        /// <param name="logLevel"></param>
        /// <returns></returns>
        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            var convertLogLevel = ConvertLogLevel(logLevel);
            return IsEnabled(convertLogLevel);
        }

        /// <summary>
        /// Is logging enabled for this logger at this <paramref name="logLevel"/>?
        /// </summary>
        private bool IsEnabled(NLog.LogLevel logLevel)
        {
            return _Logger.IsEnabled(logLevel);
        }

        /// <summary>
        /// Convert loglevel to NLog variant.
        /// </summary>
        /// <param name="logLevel">level to be converted.</param>
        /// <returns></returns>
        private static NLog.LogLevel ConvertLogLevel(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            switch (logLevel)
            {
                case Microsoft.Extensions.Logging.LogLevel.Trace:
                    return NLog.LogLevel.Trace;
                case Microsoft.Extensions.Logging.LogLevel.Debug:
                    return NLog.LogLevel.Debug;
                case Microsoft.Extensions.Logging.LogLevel.Information:
                    return NLog.LogLevel.Info;
                case Microsoft.Extensions.Logging.LogLevel.Warning:
                    return NLog.LogLevel.Warn;
                case Microsoft.Extensions.Logging.LogLevel.Error:
                    return NLog.LogLevel.Error;
                case Microsoft.Extensions.Logging.LogLevel.Critical:
                    return NLog.LogLevel.Fatal;
                case Microsoft.Extensions.Logging.LogLevel.None:
                    return NLog.LogLevel.Off;
                default:
                    return NLog.LogLevel.Debug;
            }
        }

        /// <summary>
        /// Begin a scope. Use in config with ${ndlc} 
        /// </summary>
        /// <param name="state">The state (message)</param>
        /// <returns></returns>
        public IDisposable BeginScope<TState>(TState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return NestedDiagnosticsLogicalContext.Push(state);
        }

        #region Static Methods

        public static void EnableDisableLogging(bool enable)
        {
            if (enable)
            {
                LogManager.EnableLogging();
            }
            else
            {
                LogManager.DisableLogging();
            }          
        }

        public static void LogCritical(string message, params object[] args)
        {
            Task.Factory.StartNew(() =>
            {
                MappedDiagnosticsContext.Set("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Critical, args));
                _Logger.Log(NLog.LogLevel.Fatal, message);
            });
        }

        public static void LogCritical(Exception exception, string message = null, params object[] args)
        {
            Task.Factory.StartNew(() =>
            {
                MappedDiagnosticsContext.Set("KraftError", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Critical, exception));
                MappedDiagnosticsContext.Set("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Critical, args));
                _Logger.Log(NLog.LogLevel.Fatal, exception, message);
            });
        }

        public static void LogDebug(string message, params object[] args)
        {
            Task.Factory.StartNew(() =>
            {
                MappedDiagnosticsContext.Set("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Debug, args));
                _Logger?.Log(NLog.LogLevel.Debug, message);
            });
        }

        public static void LogDebug(Exception exception, string message = null, params object[] args)
        {
            Task.Factory.StartNew(() =>
            {
                MappedDiagnosticsContext.Set("KraftError", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Debug, exception));
                MappedDiagnosticsContext.Set("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Debug, args));
                _Logger.Log(NLog.LogLevel.Debug, exception, message);
            });
        }

        public static void LogError(string message, params object[] args)
        {
            Task.Factory.StartNew(() =>
            {
                MappedDiagnosticsContext.Set("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Error, args));
                _Logger.Log(NLog.LogLevel.Error, message);
            });
        }

        public static void LogError(Exception exception, string message = null, params object[] args)
        {
            Task.Factory.StartNew(() =>
            {
                MappedDiagnosticsContext.Set("KraftError", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Error, exception));
                MappedDiagnosticsContext.Set("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Error, args));
                _Logger.Log(NLog.LogLevel.Error, exception, message);
            });
        }

        public static void LogInformation(string message, params object[] args)
        {
            Task.Factory.StartNew(() =>
            {
                MappedDiagnosticsContext.Set("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Information, args));
                _Logger.Log(NLog.LogLevel.Info, message);
            });
        }

        public static void LogInformation(Exception exception, string message = null, params object[] args)
        {
            Task.Factory.StartNew(() =>
            {
                MappedDiagnosticsContext.Set("KraftError", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Information, exception));
                MappedDiagnosticsContext.Set("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Information, args));
                _Logger.Log(NLog.LogLevel.Info, exception, message);
            });
        }

        public static void LogTrace(string message, params object[] args)
        {
            Task.Factory.StartNew(() =>
            {
                MappedDiagnosticsContext.Set("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Trace, args));
                _Logger.Log(NLog.LogLevel.Trace, message);
            });
        }

        public static void LogTrace(Exception exception, string message = null, params object[] args)
        {
            Task.Factory.StartNew(() =>
            {
                MappedDiagnosticsContext.Set("KraftError", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Trace, exception));
                MappedDiagnosticsContext.Set("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Trace, args));
                _Logger.Log(NLog.LogLevel.Trace, exception, message);
            });
        }

        public static void LogWarning(string message, params object[] args)
        {
            Task.Factory.StartNew(() =>
            {
                MappedDiagnosticsContext.Set("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Warning, args));
                _Logger.Log(NLog.LogLevel.Warn, message);
            });
        }

        public static void LogWarning(Exception exception, string message = null, params object[] args)
        {
            Task.Factory.StartNew(() =>
            {
                MappedDiagnosticsContext.Set("KraftError", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Warning, exception));
                MappedDiagnosticsContext.Set("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Warning, args));
                _Logger.Log(NLog.LogLevel.Warn, exception, message);
            });
        }
        #endregion Static Methods
    }
}