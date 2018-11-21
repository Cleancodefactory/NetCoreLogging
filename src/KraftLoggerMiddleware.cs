using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using System.Text;
using System.IO;

namespace Ccf.Ck.Libs.Logging
{
    internal class KraftLoggerMiddleware
    {
        private static int Limit = 20;

        private static int RowCount;
        internal static RequestDelegate ExecuteDelegate(IApplicationBuilder builder, string errorUrlSegment)
        {
            RequestDelegate requestDelegate = async httpContext =>
            {
                if (httpContext.Request.Path.Value == $"/{errorUrlSegment}" || httpContext.Request.Path.Value == $"/{errorUrlSegment}/")
                {
                    StringBuilder outputBuilder = new StringBuilder();
                    object result = null;

                    result = KraftLoggerExtensions.GetRowCount(httpContext.Request.Query);
                    if (result != null)
                    {
                        int.TryParse(result.ToString(), out RowCount);
                    }

                    string htmlContent = KraftLoggerExtensions.HtmlView();

                    if (!string.IsNullOrEmpty(htmlContent))
                    {

                        int pageCount = (RowCount % Limit) == 0 ? (RowCount / Limit) : (RowCount / Limit) + 1;
                        
                        LoggerViewPreprocessor pagesReplace = new LoggerViewPreprocessor(htmlContent, pageCount);
                        pagesReplace.Pages();
                        htmlContent = pagesReplace.View;

                        result = KraftLoggerExtensions.GetDataFromDB(httpContext.Request.Query, RowCount, Limit);
                        LoggerViewPreprocessor tableReplace = new LoggerViewPreprocessor(htmlContent, result);
                        tableReplace.GenerateTable();

                        outputBuilder.Append(tableReplace.View);
                    }

                    byte[] data = Encoding.UTF8.GetBytes(outputBuilder.ToString());
                    httpContext.Response.StatusCode = 200;
                    await httpContext.Response.Body.WriteAsync(data, 0, data.Length);
                }
                else if (httpContext.Request.Path.Value == $"/{errorUrlSegment}/download")
                {
                    string filePath = KraftLoggerExtensions.GetDbFilePath();
                    if (!string.IsNullOrEmpty(filePath))
                    {
                        string filename = Path.GetFileName(filePath);
                        httpContext.Response.Headers.Add("Content-Disposition", $"attachment;filename={filename}");
                        await httpContext.Response.SendFileAsync(filePath);
                    }
                }
                else if(httpContext.Request.Path.Value == $"/{errorUrlSegment}/truncate")
                {
                    KraftLoggerExtensions.TruncateLogData();
                    httpContext.Response.Redirect($"/{errorUrlSegment}");

                    //if (KraftLoggerExtensions.TruncateLogData())
                    //{
                    //    httpContext.Response.Redirect("");
                    //}
                }
            };

            return requestDelegate;
        }

        internal static IRouter MakeRouter(IApplicationBuilder builder, RouteHandler kraftLoggerRoutehandler, string errorUrlSegment)
        {
            RouteBuilder kraftRoutesBuilder = new RouteBuilder(builder, kraftLoggerRoutehandler);

            kraftRoutesBuilder.MapRoute(
                name: "logger",
                template: errorUrlSegment
                );
            kraftRoutesBuilder.MapRoute(
                name: "logger_download",
                template: errorUrlSegment + "/download"
                );
            kraftRoutesBuilder.MapRoute(
                name: "logger_truncate",
                template: errorUrlSegment + "/truncate"
                );

            IRouter kraftRouter = kraftRoutesBuilder.Build();

            return kraftRouter;
        }
    }
}
