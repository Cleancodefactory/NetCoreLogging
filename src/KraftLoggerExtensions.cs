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
using Microsoft.AspNetCore.Hosting.Internal;
using NLog.Config;
using System.Xml;

namespace Ccf.Ck.Libs.Logging
{
    public static class KraftLoggerExtensions
    {
        private static IApplicationLifetime _ApplicationLifetime;
        private static FileSystemWatcher _FileSystemWatcher;
        public static void UseBindKraftLogger(this IApplicationBuilder builder, IHostingEnvironment env, ILoggerFactory loggerFactory, string errorUrlSegment)
        {
            RouteHandler kraftLoggerRoutehandler = new RouteHandler(KraftLoggerMiddleware.ExecuteDelegate(builder, errorUrlSegment));
            builder.UseRouter(KraftLoggerMiddleware.MakeRouter(builder, kraftLoggerRoutehandler, errorUrlSegment));
            loggerFactory.AddProvider(new KraftLoggerProvider(LogManager.GetCurrentClassLogger(), builder.ApplicationServices.GetService<IHttpContextAccessor>()));
            Utilities.ShouldSerializeWithAllDetails = int.Parse(LogManager.Configuration.Variables["ShouldSerializeWithAllDetails"].Text);
            _ApplicationLifetime = builder.ApplicationServices.GetRequiredService<IApplicationLifetime>();
            Utilities.CreateSqliteTarget();
        }

        public static void UseBindKraftLogger(this IServiceCollection services)
        {
            services.AddRouting();
            FileInfo nlogConfig = new FileInfo(Path.Combine(Path.GetDirectoryName(Assembly.GetEntryAssembly().Location), "nlog.config"));
            if (nlogConfig.Exists)
            {
                _FileSystemWatcher = new FileSystemWatcher(nlogConfig.Directory.FullName, nlogConfig.Name);
                _FileSystemWatcher.NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.FileName;
                _FileSystemWatcher.EnableRaisingEvents = true;
                _FileSystemWatcher.IncludeSubdirectories = false;
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
            services.AddSingleton<IApplicationLifetime, ApplicationLifetime>();
        }

        public static void RestartApplication(IApplicationLifetime applicationLifetime, FileSystemEventArgs e)
        {
            try {
                if (applicationLifetime != null)
                {
                    applicationLifetime.StopApplication();
                    KraftLogger.LogDebug($"Method: RestartApplication(KraftLoggerExtensions): Stopping application caused by {e.ChangeType} file {e.FullPath}");
                    if (!applicationLifetime.ApplicationStopping.IsCancellationRequested)
                    {
                        Task.Delay(10 * 1000, applicationLifetime.ApplicationStopping);
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
#if !DEBUG
            RestartApplication(_ApplicationLifetime, e);
#endif
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

        public static string HtmlView()
        {
            return Utilities.GetViewTemplate();
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
