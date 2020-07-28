using Nop.Core.Domain.Payments;

namespace Nop.Plugin.Payments.PaystackStandard
{
    /// <summary>
    /// Represents Paystack helper
    /// </summary>
    public class PaystackHelper
    {
        #region Properties

        /// <summary>
        /// Get the generic attribute name that is used to store an order total that actually sent to Paystack (used to PDT order total validation)
        /// </summary>
        public static string OrderTotalSentToPaystack => "OrderTotalSentToPaystack";

        #endregion
    }
}