using Ccf.Ck.Libs.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TestLogger
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.UseBindKraftLogger();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime)
        {
            ILoggerFactory loggerFactory = app.ApplicationServices.GetService<ILoggerFactory>();
            app.UseBindKraftLogger(env, loggerFactory, "/error");

            try
            {
                //KraftLogger.LogError("Error....");
                for (int i = 0; i < 20; i++)
                {
                    KraftLogger.LogCritical($"Method: public IActionResult Error for path: {i}", new Exception($"Exception {i}"));
                }
                

                //KraftLogger.LogError("Error....");
                //KraftLogger.LogError("Error....");
                //KraftLogger.LogError("Error....");
                //KraftLogger.LogError("Error....");
                //KraftLogger.LogError("Error....");
                //KraftLogger.LogError("Error....");
                //KraftLogger.LogError("Error....");
                //KraftLogger.LogError("Error....");
                //KraftLogger.LogError("Error....");
                //KraftLogger.LogError("Error....");
                //KraftLogger.LogError("Error....");
                //KraftLogger.LogError("Error....");
                //KraftLogger.LogError("Error....");
                //KraftLogger.LogError("Error....");

                //try
                //{
                //    throw new Exception("Inner");
                //}
                //catch (Exception inner)
                //{
                //    throw new Exception("Outer", inner);
                //}
            }
            catch (Exception ex)
            {
                KraftLogger.LogError(ex);
                Console.WriteLine(ex.Message);
                Console.ReadLine();
            }
        }
    }
}
