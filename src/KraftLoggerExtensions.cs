using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NLog.Web;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using NLog;
using Microsoft.AspNetCore.Routing;
using System;
using System.Threading.Tasks;
using NLog.Config;
using System.Xml;
using NLog.Layouts;
using Microsoft.Extensions.Hosting;

namespace Ccf.Ck.Libs.Logging
{
    public static class KraftLoggerExtensions
    {
        private static IHostApplicationLifetime _HostApplicationLifetime;
        private static FileSystemWatcher _FileSystemWatcher;
        public static void UseBindKraftLogger(this IApplicationBuilder builder, IWebHostEnvironment env, ILoggerFactory loggerFactory, string errorUrlSegment)
        {
            if (!string.IsNullOrEmpty(errorUrlSegment))
            {
                RouteHandler kraftLoggerRoutehandler = new RouteHandler(KraftLoggerMiddleware.ExecuteDelegate(builder, errorUrlSegment));
                builder.UseRouter(KraftLoggerMiddleware.MakeRouter(builder, kraftLoggerRoutehandler, errorUrlSegment));
            }
            loggerFactory.AddProvider(new KraftLoggerProvider(LogManager.GetCurrentClassLogger(), builder.ApplicationServices.GetService<IHttpContextAccessor>(), env));
            Utilities.ShouldSerializeWithAllDetails = 0;
            if (LogManager.Configuration.Variables.Keys.Contains("ShouldSerializeWithAllDetails"))
            {
                SimpleLayout shouldSerializeWithAllDetails = (SimpleLayout)LogManager.Configuration.Variables["ShouldSerializeWithAllDetails"];
                if (shouldSerializeWithAllDetails != null)
                {
                    if (int.TryParse(shouldSerializeWithAllDetails.Text, out int serialize))
                    {
                        Utilities.ShouldSerializeWithAllDetails = serialize;
                    }
                }
            }

            _HostApplicationLifetime = builder.ApplicationServices.GetRequiredService<IHostApplicationLifetime>();
            Utilities.CreateSqliteTarget();
        }

        public static void UseBindKraftLogger(this IServiceCollection services)
        {
            services.AddRouting();
            IWebHostEnvironment env = services.BuildServiceProvider().GetService<IWebHostEnvironment>();
            FileInfo nlogConfig = null;
            if (env != null)
            {
                nlogConfig = new FileInfo(Path.Combine(Path.GetFullPath(env.ContentRootPath), "nlog.config"));
            }
            else
            {
                nlogConfig = new FileInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "nlog.config"));
            }
            if (nlogConfig.Exists)
            {
                _FileSystemWatcher = new FileSystemWatcher(nlogConfig.Directory.FullName, nlogConfig.Name)
                {
                    NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName,
                    EnableRaisingEvents = true,
                    IncludeSubdirectories = false
                };
                _FileSystemWatcher.Changed += FileWatcher_Changed;
                NLogBuilder.ConfigureNLog(nlogConfig.FullName);
            }
            else
            {
                Assembly assembly = typeof(KraftLoggerExtensions).GetTypeInfo().Assembly;
                Stream resource = assembly.GetManifestResourceStream("Ccf.Ck.Libs.Logging.Resources.nlog.config");
                StringReader sr;
                using (var reader = new StreamReader(resource))
                {
                    sr = new StringReader(reader.ReadToEnd());
                }
                XmlReader xr = XmlReader.Create(sr);
                XmlLoggingConfiguration config = new XmlLoggingConfiguration(xr, null);
                NLogBuilder.ConfigureNLog(config);
            }

            services.AddLogging(logging =>
            {
                logging.ClearProviders();
                logging.SetMinimumLevel(Utilities.ConfiguredLogLevel);
            });
            //call this in case you need aspnet-user-authtype/aspnet-user-identity
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        }

        public static void RestartApplication(IHostApplicationLifetime hostApplicationLifetime, FileSystemEventArgs e)
        {
            try
            {
                if (hostApplicationLifetime != null)
                {
                    hostApplicationLifetime.StopApplication();
                    KraftLogger.LogDebug($"Method: RestartApplication(KraftLoggerExtensions): Stopping application caused by {e.ChangeType} file {e.FullPath}");
                    if (!hostApplicationLifetime.ApplicationStopping.IsCancellationRequested)
                    {
                        Task.Delay(10 * 1000, hostApplicationLifetime.ApplicationStopping);
                    }
                }
                else
                {
                    KraftLogger.LogDebug("Method: RestartApplication(nlog): applicationLifetime is null.");
                }
            }
            catch (Exception exception)
            {
                KraftLogger.LogError(exception, "Method: RestartApplication(nlog)(IApplicationLifetime applicationLifetime)");
            }
        }

        private static void FileWatcher_Changed(object sender, FileSystemEventArgs e)
        {
            //#if !DEBUG
            RestartApplication(_HostApplicationLifetime, e);
            //#endif
        }

        public static object GetRowCount(IQueryCollection parameters = null)
        {
            string connectionString = Utilities.GetConnectionString();
            Dictionary<string, object> args = Utilities.ExtractParametersFromRequest(parameters);
            if (!args.ContainsKey("filter"))
            {
                args.Add("filter", null);
            }
            else
            {
                string filterValue = args["filter"].ToString().ToLower();
                if (filterValue == "all")
                {
                    args["filter"] = null;
                }
            }
            return Utilities.GetCount(connectionString, args);
        }

        public static object GetDataFromDB(IQueryCollection parameters = null, int rowCount = 0, int limit = 0)
        {
            string connectionString = Utilities.GetConnectionString();

            Dictionary<string, object> args = Utilities.ExtractParametersFromRequest(parameters);
            if (!args.ContainsKey("filter"))
            {
                args.Add("filter", null);
            }
            else
            {
                string filterValue = args["filter"].ToString().ToLower();
                if (filterValue == "all")
                {
                    args["filter"] = null;
                }
            }
            args.Add("rowcount", rowCount);
            args.Add("limit", limit);

            return Utilities.GetData(connectionString, args);
        }

        public static bool TruncateLogData()
        {
            string connectionString = Utilities.GetConnectionString();
            return Utilities.TruncateLogTable(connectionString);
        }

        public static string GetDbFilePath()
        {
            string path = Utilities.GetDbFilePath();
            return File.Exists(path) ? path : string.Empty;
        }
    }
}
