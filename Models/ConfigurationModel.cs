using Nop.Web.Framework.Mvc.ModelBinding;
using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.PaystackStandard.Models
{
    public class ConfigurationModel : BaseNopModel
    {
        public int ActiveStoreScopeConfiguration { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PaystackStandard.Fields.UseSandbox")]
        public bool UseSandbox { get; set; }
        public bool UseSandbox_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PaystackStandard.Fields.LiveSecretKey")]
        public string LiveSecretKey { get; set; }
        public bool LiveSecretKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PaystackStandard.Fields.TestSecretKey")]
        public string TestSecretKey { get; set; }
        public bool TestSecretKey_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PaystackStandard.Fields.PassProductNamesAndTotals")]
        public bool PassProductNamesAndTotals { get; set; }
        public bool PassProductNamesAndTotals_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PaystackStandard.Fields.AdditionalFee")]
        public decimal AdditionalFee { get; set; }
        public bool AdditionalFee_OverrideForStore { get; set; }

        [NopResourceDisplayName("Plugins.Payments.PaystackStandard.Fields.AdditionalFeePercentage")]
        public bool AdditionalFeePercentage { get; set; }
        public bool AdditionalFeePercentage_OverrideForStore { get; set; }
    }
}