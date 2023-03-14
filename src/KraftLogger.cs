using System;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using NLog;
using NLog.Extensions.Logging;
using Microsoft.AspNetCore.Http;
using NLog.Targets;
using System.Collections.ObjectModel;
using Microsoft.AspNetCore.Hosting;

namespace Ccf.Ck.Libs.Logging
{
    public class KraftLogger : Microsoft.Extensions.Logging.ILogger
    {
        static readonly object _Locker = new();
        private static Logger _Logger;
        private readonly NLogProviderOptions _Options;
        private static string _ContentRootPath = string.Empty;
        private static readonly object EmptyEventId = default(EventId);    // Cache boxing of empty EventId-struct
        private static readonly object ZeroEventId = default(EventId).Id;  // Cache boxing of zero EventId-Value
        private Tuple<string, string, string> _EventIdPropertyNames;
        private readonly IHttpContextAccessor _Accessor;

        public KraftLogger(Logger logger, IHttpContextAccessor accessor, NLogProviderOptions options, IWebHostEnvironment env)
        {
            _Logger = logger;
            _Accessor = accessor;
            _Options = options ?? GetDefaultOptions();
            if (env != null)
            {
                _ContentRootPath = env.ContentRootPath ?? string.Empty;
            }
        }

        private static NLogProviderOptions GetDefaultOptions()
        {
            NLogProviderOptions options = new()
            {
                CaptureMessageProperties = true,
                CaptureMessageTemplates = true,
                EventIdSeparator = ".",
                IgnoreEmptyEventId = true
            };
            return options;
        }

        public static ReadOnlyCollection<Target> GetAllTargets()
        {
            if (_Logger != null)
            {
                lock (_Locker)
                {
                    return LogManager.Configuration.AllTargets;
                }
            }
            return null;
        }

        //todo  callsite showing the framework logging classes/methods
        public void Log<TState>(Microsoft.Extensions.Logging.LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
        {
            if (_Logger != null)
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
                    ScopeContext.PushProperty("KraftError", Utilities.Serialize(logLevel, exception));
                    if (_Accessor.HttpContext != null) // you should check HttpContext 
                    {
                        ScopeContext.PushProperty("Args", Utilities.Serialize(logLevel, new object[] { _Accessor.HttpContext }));
                    }
                }
                _Logger.Log(eventInfo);
            }
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
        private static bool IsEnabled(NLog.LogLevel logLevel)
        {
            if (_Logger != null)
            {
                lock (_Logger)
                {
                    return _Logger.IsEnabled(logLevel);
                }
            }
            return false;
        }

        /// <summary>
        /// Convert loglevel to NLog variant.
        /// </summary>
        /// <param name="logLevel">level to be converted.</param>
        /// <returns></returns>
        private static NLog.LogLevel ConvertLogLevel(Microsoft.Extensions.Logging.LogLevel logLevel)
        {
            return logLevel switch
            {
                Microsoft.Extensions.Logging.LogLevel.Trace => NLog.LogLevel.Trace,
                Microsoft.Extensions.Logging.LogLevel.Debug => NLog.LogLevel.Debug,
                Microsoft.Extensions.Logging.LogLevel.Information => NLog.LogLevel.Info,
                Microsoft.Extensions.Logging.LogLevel.Warning => NLog.LogLevel.Warn,
                Microsoft.Extensions.Logging.LogLevel.Error => NLog.LogLevel.Error,
                Microsoft.Extensions.Logging.LogLevel.Critical => NLog.LogLevel.Fatal,
                Microsoft.Extensions.Logging.LogLevel.None => NLog.LogLevel.Off,
                _ => NLog.LogLevel.Debug,
            };
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

            return ScopeContext.PushNestedState(state);
        }

        #region Static Methods

        public static void EnableDisableLogging(bool enable)
        {
            if (_Logger != null)
            {
                lock (_Locker)
                {
                    if (enable)
                    {
                        LogManager.ResumeLogging();
                    }
                    else
                    {
                        LogManager.SuspendLogging();
                    }
                }
            }
        }

        public static void LogCritical(string message, params object[] args)
        {
            if (_Logger != null)
            {
                if (_Logger.IsEnabled(NLog.LogLevel.Fatal))
                {
                    lock (_Locker)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            ScopeContext.PushProperty("ExecutionFolder", _ContentRootPath);
                            ScopeContext.PushProperty("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Critical, args));
                            _Logger.Log(NLog.LogLevel.Fatal, message);

                        }).Wait();
                    }
                }
            }
            else
            {
                EmergencyFileLogger.Log(message, null, Microsoft.Extensions.Logging.LogLevel.Critical);
            }
        }

        public static void LogCritical(Exception exception, string message = null, params object[] args)
        {
            if (_Logger != null)
            {
                if (_Logger.IsEnabled(NLog.LogLevel.Fatal))
                {
                    lock (_Locker)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            ScopeContext.PushProperty("ExecutionFolder", _ContentRootPath);
                            ScopeContext.PushProperty("KraftError", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Critical, exception));
                            ScopeContext.PushProperty("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Critical, args));
                            _Logger.Log(NLog.LogLevel.Fatal, exception, message);
                        }).Wait();
                    }
                }
            }
            else
            {
                EmergencyFileLogger.Log(message, exception, Microsoft.Extensions.Logging.LogLevel.Critical);
            }
        }

        public static void LogDebug(string message, params object[] args)
        {
            if (_Logger != null)
            {
                if (_Logger.IsEnabled(NLog.LogLevel.Debug))
                {
                    lock (_Locker)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            ScopeContext.PushProperty("ExecutionFolder", _ContentRootPath);
                            ScopeContext.PushProperty("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Debug, args));
                            _Logger?.Log(NLog.LogLevel.Debug, message);
                        }).Wait();
                    }
                }
            }
        }

        public static void LogDebug(Exception exception, string message = null, params object[] args)
        {
            if (_Logger != null)
            {
                if (_Logger.IsEnabled(NLog.LogLevel.Debug))
                {
                    lock (_Locker)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            ScopeContext.PushProperty("ExecutionFolder", _ContentRootPath);
                            ScopeContext.PushProperty("KraftError", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Debug, exception));
                            ScopeContext.PushProperty("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Debug, args));
                            _Logger.Log(NLog.LogLevel.Debug, exception, message);
                        }).Wait();
                    }
                }
            }
        }

        public static void LogError(string message, params object[] args)
        {
            if (_Logger != null)
            {
                if (_Logger.IsEnabled(NLog.LogLevel.Error))
                {
                    lock (_Locker)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            ScopeContext.PushProperty("ExecutionFolder", _ContentRootPath);
                            ScopeContext.PushProperty("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Error, args));
                            _Logger.Log(NLog.LogLevel.Error, message);
                        }).Wait();
                    }
                }
            }
            else
            {
                EmergencyFileLogger.Log(message, null, Microsoft.Extensions.Logging.LogLevel.Error);
            }
        }

        public static void LogError(Exception exception, string message = null, params object[] args)
        {
            if (_Logger != null)
            {
                if (_Logger.IsEnabled(NLog.LogLevel.Error))
                {
                    lock (_Locker)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            ScopeContext.PushProperty("ExecutionFolder", _ContentRootPath);
                            ScopeContext.PushProperty("KraftError", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Error, exception));
                            ScopeContext.PushProperty("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Error, args));
                            _Logger.Log(NLog.LogLevel.Error, exception, message);
                        }).Wait();
                    }
                }
            }
            else
            {
                EmergencyFileLogger.Log(message, exception, Microsoft.Extensions.Logging.LogLevel.Error);
            }
        }

        public static void LogInformation(string message, params object[] args)
        {
            if (_Logger != null)
            {
                if (_Logger.IsEnabled(NLog.LogLevel.Info))
                {
                    lock (_Locker)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            ScopeContext.PushProperty("ExecutionFolder", _ContentRootPath);
                            ScopeContext.PushProperty("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Information, args));
                            _Logger.Log(NLog.LogLevel.Info, message);
                        }).Wait();
                    }
                }
            }
        }

        public static void LogInformation(Exception exception, string message = null, params object[] args)
        {
            if (_Logger != null)
            {
                if (_Logger.IsEnabled(NLog.LogLevel.Info))
                {
                    lock (_Locker)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            ScopeContext.PushProperty("ExecutionFolder", _ContentRootPath);
                            ScopeContext.PushProperty("KraftError", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Information, exception));
                            ScopeContext.PushProperty("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Information, args));
                            _Logger.Log(NLog.LogLevel.Info, exception, message);
                        }).Wait();
                    }
                }
            }
        }

        public static void LogTrace(string message, params object[] args)
        {
            if (_Logger != null)
            {
                if (_Logger.IsEnabled(NLog.LogLevel.Trace))
                {
                    lock (_Locker)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            ScopeContext.PushProperty("ExecutionFolder", _ContentRootPath);
                            ScopeContext.PushProperty("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Trace, args));
                            _Logger.Log(NLog.LogLevel.Trace, message);
                        }).Wait();
                    }
                }
            }
        }

        public static void LogTrace(Exception exception, string message = null, params object[] args)
        {
            if (_Logger != null)
            {
                if (_Logger.IsEnabled(NLog.LogLevel.Trace))
                {
                    lock (_Locker)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            ScopeContext.PushProperty("ExecutionFolder", _ContentRootPath);
                            ScopeContext.PushProperty("KraftError", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Trace, exception));
                            ScopeContext.PushProperty("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Trace, args));
                            _Logger.Log(NLog.LogLevel.Trace, exception, message);
                        }).Wait();
                    }
                }
            }
        }

        public static void LogWarning(string message, params object[] args)
        {
            if (_Logger != null)
            {
                if (_Logger.IsEnabled(NLog.LogLevel.Warn))
                {
                    lock (_Locker)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            ScopeContext.PushProperty("ExecutionFolder", _ContentRootPath);
                            ScopeContext.PushProperty("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Warning, args));
                            _Logger.Log(NLog.LogLevel.Warn, message);
                        }).Wait();
                    }
                }
            }
        }

        public static void LogWarning(Exception exception, string message = null, params object[] args)
        {
            if (_Logger != null)
            {
                if (_Logger.IsEnabled(NLog.LogLevel.Warn))
                {
                    lock (_Locker)
                    {
                        Task.Factory.StartNew(() =>
                        {
                            ScopeContext.PushProperty("ExecutionFolder", _ContentRootPath);
                            ScopeContext.PushProperty("KraftError", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Warning, exception));
                            ScopeContext.PushProperty("Args", Utilities.Serialize(Microsoft.Extensions.Logging.LogLevel.Warning, args));
                            _Logger.Log(NLog.LogLevel.Warn, exception, message);
                        }).Wait();
                    }
                }
            }
        }
        #endregion Static Methods
    }
}