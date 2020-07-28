using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.PaystackStandard
{
    /// <summary>
    /// Represents settings of the Paystack Standard payment plugin
    /// </summary>
    public class PaystackStandardPaymentSettings : ISettings
    {
        /// <summary>
        /// Gets or sets a value indicating whether to use sandbox (testing environment)
        /// </summary>
        public bool UseSandbox { get; set; }

        /// <summary>
        /// Gets or sets Live Secret Key
        /// </summary>
        public string LiveSecretToken { get; set; }

        /// <summary>
        /// Gets or sets Test Secret Key
        /// </summary>
        public string TestSecretToken { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to pass info about purchased items to Paystack
        /// </summary>
        public bool PassProductNamesAndTotals { get; set; }

        /// <summary>
        /// Gets or sets an additional fee
        /// </summary>
        public decimal AdditionalFee { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to "additional fee" is specified as percentage. true - percentage, false - fixed value.
        /// </summary>
        public bool AdditionalFeePercentage { get; set; }
    }
}
