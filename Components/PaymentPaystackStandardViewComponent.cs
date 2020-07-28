using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.PaystackStandard.Components
{
    [ViewComponent(Name = "PaymentPaystackStandard")]
    public class PaymentPaystackStandardViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.PaystackStandard/Views/PaymentInfo.cshtml");
        }
    }
}
