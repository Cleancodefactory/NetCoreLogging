using Ccf.Ck.Libs.Logging;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace TestLogger
{
    class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
               Host.CreateDefaultBuilder(args)
                   .ConfigureServices((hostContext, services) =>
                   {
                       services.UseBindKraftLogger();
                       ServiceProvider serviceProvider = services.BuildServiceProvider();
                       ApplicationBuilder builder = new ApplicationBuilder(serviceProvider);
                       var loggerFactory = builder.ApplicationServices.GetService<ILoggerFactory>();
                       builder.UseBindKraftLogger(null, loggerFactory, null);

                       try
                       {
                           try
                           {
                               throw new Exception("Inner");
                           }
                           catch (Exception inner)
                           {
                               throw new Exception("Outer", inner);
                           }                           
                       }
                       catch (Exception ex)
                       {
                           KraftLogger.LogError(ex);
                           Console.WriteLine(ex.Message);
                           Console.ReadLine();
                       }
                   });
    }
}
