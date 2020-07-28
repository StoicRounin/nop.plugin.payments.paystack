using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.PaystackStandard.Infrastructure
{
    public partial class RouteProvider : IRouteProvider
    {
        /// <summary>
        /// Register routes
        /// </summary>
        /// <param name="routeBuilder">Route builder</param>
        public void RegisterRoutes(IRouteBuilder routeBuilder)
        {
            //Callback
            routeBuilder.MapRoute("Plugin.Payments.PaystackStandard.CallbackHandler", "Plugins/PaymentPaystackStandard/CallbackHandler",
                 new { controller = "PaymentPaystackStandard", action = "CallbackHandler" });

            //Webhook
            routeBuilder.MapRoute("Plugin.Payments.PaystackStandard.NotifyHandler", "Plugins/PaymentPaystackStandard/NotifyHandler",
                 new { controller = "PaymentPaystackStandard", action = "NotifyHandler" });

            //Cancel
            routeBuilder.MapRoute("Plugin.Payments.PaystackStandard.CancelOrder", "Plugins/PaymentPaystackStandard/CancelOrder",
                 new { controller = "PaymentPaystackStandard", action = "CancelOrder" });
        }

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority => -1;
    }
}