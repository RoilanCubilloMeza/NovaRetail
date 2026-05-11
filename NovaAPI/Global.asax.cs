using System;
using System.IO;
using System.Web.Http;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using NovaAPI.Services;

namespace NovaAPI
{
    public class WebApiApplication : System.Web.HttpApplication
    {
        private static readonly string ErrorLogPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\nova_error.log");

        protected void Application_Start()
        {
            AreaRegistration.RegisterAllAreas();
            GlobalConfiguration.Configure(WebApiConfig.Register);
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);
            RouteConfig.RegisterRoutes(RouteTable.Routes);
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }

        protected void Application_Error(object sender, EventArgs e)
        {
            var ex = Server.GetLastError();
            if (ex != null)
            {
                try { NovaFileLogger.AppendLine(ErrorLogPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] APPLICATION_ERROR:\r\n{ex}\r\n"); } catch { }
            }
        }

        protected void Application_End()
        {
            NovaFileLogger.Shutdown();
        }
    }
}
