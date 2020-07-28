using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Nop.Core;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Shipping;
using Nop.Plugin.Payments.PaystackStandard.Models;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Tax;
using PayStack.Net;

namespace Nop.Plugin.Payments.PaystackStandard
{
    /// <summary>
    /// PaystackStandard payment processor
    /// </summary>
    public class PaystackStandardPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICurrencyService _currencyService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly PaystackStandardPaymentSettings _paystackStandardPaymentSettings;

        #endregion

        #region Ctor

        public PaystackStandardPaymentProcessor(CurrencySettings currencySettings,
            ICheckoutAttributeParser checkoutAttributeParser,
            ICurrencyService currencyService,
            IGenericAttributeService genericAttributeService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IPaymentService paymentService,
            ISettingService settingService,
            ITaxService taxService,
            IWebHelper webHelper,
            PaystackStandardPaymentSettings paystackStandardPaymentSettings)
        {
            _currencySettings = currencySettings;
            _checkoutAttributeParser = checkoutAttributeParser;
            _currencyService = currencyService;
            _genericAttributeService = genericAttributeService;
            _httpContextAccessor = httpContextAccessor;
            _localizationService = localizationService;
            _paymentService = paymentService;
            _settingService = settingService;
            _taxService = taxService;
            _webHelper = webHelper;
            _paystackStandardPaymentSettings = paystackStandardPaymentSettings;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Verifies IPN
        /// </summary>
        /// <param name="formString">Form string</param>
        /// <param name="values">Values</param>
        /// <returns>Result</returns>
        public TransactionVerificationResponse VerifyTransaction(string referenceCode)
        {
            var secretKey = _paystackStandardPaymentSettings.UseSandbox ?
                _paystackStandardPaymentSettings.TestSecretToken : _paystackStandardPaymentSettings.LiveSecretToken;

            var payStackApi = new PayStackApi(secretKey);
            var response = payStackApi.Transactions.Verify(referenceCode);

            return new TransactionVerificationResponse
            {
                IsVerified = response.Status,
                Amount = (response.Data.Amount / 100),
                Message = response.Message,
                AuthCode = response.Data.Authorization.AuthorizationCode
            };
        }
        /// <summary>
        /// Create common query parameters for the request
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Created query parameters</returns>
        private void AddOrderInfoMetaData(TransactionInitializeRequest request, PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //get store location
            var storeLocation = _webHelper.GetStoreLocation();

            //choosing correct order address
            var orderAddress = postProcessPaymentRequest.Order.PickupInStore
                    ? postProcessPaymentRequest.Order.PickupAddress
                    : postProcessPaymentRequest.Order.ShippingAddress;

            //create request metadata
            request.MetadataObject["currency_code"] = _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId)?.CurrencyCode;

            //order identifier
            request.MetadataObject["invoice"] = postProcessPaymentRequest.Order.CustomOrderNumber;
            request.MetadataObject["custom"] = postProcessPaymentRequest.Order.OrderGuid.ToString();

            //PDT, IPN and cancel URL
            request.MetadataObject["return"] = $"{storeLocation}Plugins/PaymentPaystackStandard/CallbackHandler";
            request.MetadataObject["notify_url"] = $"{storeLocation}Plugins/PaymentPaystackStandard/NotifyHandler";
            request.MetadataObject["cancel_return"] = $"{storeLocation}Plugins/PaymentPaystackStandard/CancelOrder";

            //shipping address, if exists
            request.MetadataObject["no_shipping"] = postProcessPaymentRequest.Order.ShippingStatus == ShippingStatus.ShippingNotRequired ? "1" : "2";
            request.MetadataObject["address_override"] = postProcessPaymentRequest.Order.ShippingStatus == ShippingStatus.ShippingNotRequired ? "0" : "1";
            request.MetadataObject["first_name"] = orderAddress?.FirstName;
            request.MetadataObject["last_name"] = orderAddress?.LastName;
            request.MetadataObject["address1"] = orderAddress?.Address1;
            request.MetadataObject["address2"] = orderAddress?.Address2;
            request.MetadataObject["city"] = orderAddress?.City;
            request.MetadataObject["state"] = orderAddress?.StateProvince?.Abbreviation;
            request.MetadataObject["country"] = orderAddress?.Country?.TwoLetterIsoCode;
            request.MetadataObject["zip"] = orderAddress?.ZipPostalCode;
            request.MetadataObject["email"] = orderAddress?.Email;
            request.MetadataObject["phonenumber"] = orderAddress?.PhoneNumber;

        }

        /// <summary>
        /// Add order items to the request request metadata
        /// </summary>
        /// <param name="parameters">Query parameters</param>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        private void AddOrderItemsMetaData(TransactionInitializeRequest request, PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //upload order items
            var cartTotal = decimal.Zero;
            var roundedCartTotal = decimal.Zero;
            var itemCount = 1;

            //add shopping cart items
            foreach (var item in postProcessPaymentRequest.Order.OrderItems)
            {
                var roundedItemPrice = Math.Round(item.UnitPriceExclTax, 2);

                //add query parameters
                request.MetadataObject[$"item_name_{itemCount}"] = item.Product.Name;
                request.MetadataObject[$"amount_{itemCount}"] = roundedItemPrice.ToString("0.00", CultureInfo.InvariantCulture);
                request.MetadataObject[$"quantity_{itemCount}"] = item.Quantity.ToString();

                cartTotal += item.PriceExclTax;
                roundedCartTotal += roundedItemPrice * item.Quantity;
                itemCount++;
            }

            //add checkout attributes as order items
            var checkoutAttributeValues = _checkoutAttributeParser.ParseCheckoutAttributeValues(postProcessPaymentRequest.Order.CheckoutAttributesXml);
            foreach (var attributeValue in checkoutAttributeValues)
            {
                var attributePrice = _taxService.GetCheckoutAttributePrice(attributeValue, false, postProcessPaymentRequest.Order.Customer);
                var roundedAttributePrice = Math.Round(attributePrice, 2);

                //add query parameters
                if (attributeValue.CheckoutAttribute == null)
                    continue;

                request.MetadataObject[$"item_name_{itemCount}"] = attributeValue.CheckoutAttribute.Name;
                request.MetadataObject[$"amount_{itemCount}"] = roundedAttributePrice.ToString("0.00", CultureInfo.InvariantCulture);
                request.MetadataObject[$"quantity_{itemCount}"] = "1";

                cartTotal += attributePrice;
                roundedCartTotal += roundedAttributePrice;
                itemCount++;
            }

            //add shipping fee as a separate order item, if it has price
            var roundedShippingPrice = Math.Round(postProcessPaymentRequest.Order.OrderShippingExclTax, 2);
            if (roundedShippingPrice > decimal.Zero)
            {
                request.MetadataObject[$"item_name_{itemCount}"] = "Shipping fee";
                request.MetadataObject[$"amount_{itemCount}"] = roundedShippingPrice.ToString("0.00", CultureInfo.InvariantCulture);
                request.MetadataObject[$"quantity_{itemCount}"] = "1";

                cartTotal += postProcessPaymentRequest.Order.OrderShippingExclTax;
                roundedCartTotal += roundedShippingPrice;
                itemCount++;
            }

            //add payment method additional fee as a separate order item, if it has price
            var roundedPaymentMethodPrice = Math.Round(postProcessPaymentRequest.Order.PaymentMethodAdditionalFeeExclTax, 2);
            if (roundedPaymentMethodPrice > decimal.Zero)
            {
                request.MetadataObject[$"item_name_{itemCount}"] = "Payment method fee";
                request.MetadataObject[$"amount_{itemCount}"] = roundedPaymentMethodPrice.ToString("0.00", CultureInfo.InvariantCulture);
                request.MetadataObject[$"quantity_{itemCount}"] = "1";

                cartTotal += postProcessPaymentRequest.Order.PaymentMethodAdditionalFeeExclTax;
                roundedCartTotal += roundedPaymentMethodPrice;
                itemCount++;
            }

            //add tax as a separate order item, if it has positive amount
            var roundedTaxAmount = Math.Round(postProcessPaymentRequest.Order.OrderTax, 2);
            if (roundedTaxAmount > decimal.Zero)
            {
                request.MetadataObject[$"item_name_{itemCount}"] = "Tax amount";
                request.MetadataObject[$"amount_{itemCount}"] = roundedTaxAmount.ToString("0.00", CultureInfo.InvariantCulture);
                request.MetadataObject[$"quantity_{itemCount}"] = "1";

                cartTotal += postProcessPaymentRequest.Order.OrderTax;
                roundedCartTotal += roundedTaxAmount;
            }

            if (cartTotal > postProcessPaymentRequest.Order.OrderTotal)
            {
                //get the difference between what the order total is and what it should be and use that as the "discount"
                var discountTotal = Math.Round(cartTotal - postProcessPaymentRequest.Order.OrderTotal, 2);
                roundedCartTotal -= discountTotal;

                //gift card or rewarded point amount applied to cart in nopCommerce - shows in Paystack as "discount"
                request.MetadataObject["discount_amount_cart"] = discountTotal.ToString("0.00", CultureInfo.InvariantCulture);
            }

            //save order total that actually sent to Paystack (used for PDT order total validation)
            _genericAttributeService.SaveAttribute(postProcessPaymentRequest.Order, PaystackHelper.OrderTotalSentToPaystack, roundedCartTotal);
        }


        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult();
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            var order = postProcessPaymentRequest.Order;
            var orderUniqueId = postProcessPaymentRequest.Order.OrderGuid.ToString();

            var roundedOrderTotal = Math.Round(order.OrderTotal, 2);
            var roundedOrderTotalPesewas = Convert.ToInt32(roundedOrderTotal * 100);

            _genericAttributeService.SaveAttribute(postProcessPaymentRequest.Order, PaystackHelper.OrderTotalSentToPaystack, roundedOrderTotal);

            var secretKey = _paystackStandardPaymentSettings.UseSandbox ?
                _paystackStandardPaymentSettings.TestSecretToken : _paystackStandardPaymentSettings.LiveSecretToken;

            var payStackApi = new PayStackApi(secretKey);

            var request = new TransactionInitializeRequest
            {
                Email = order.Customer.Email,
                Reference = orderUniqueId,
                AmountInKobo = roundedOrderTotalPesewas,
                Currency = "GHS",
            };

            AddOrderInfoMetaData(request, postProcessPaymentRequest);

            if (_paystackStandardPaymentSettings.PassProductNamesAndTotals)
            {
                AddOrderItemsMetaData(request, postProcessPaymentRequest);
            }

            var response = payStackApi.Transactions.Initialize(request);

            if (response.Status)
                _httpContextAccessor.HttpContext.Response.Redirect(response.Data.AuthorizationUrl);
            else
            {
                var thisPage = _webHelper.GetThisPageUrl(true);
                _httpContextAccessor.HttpContext.Response.Redirect(thisPage);
            }
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return _paymentService.CalculateAdditionalFee(cart,
                _paystackStandardPaymentSettings.AdditionalFee, _paystackStandardPaymentSettings.AdditionalFeePercentage);
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            return new RefundPaymentResult { Errors = new[] { "Refund method not supported" } };
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            return new VoidPaymentResult { Errors = new[] { "Void method not supported" } };
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentPaystackStandard/Configure";
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return "PaymentPaystackStandard";
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            _settingService.SaveSetting(new PaystackStandardPaymentSettings
            {
                UseSandbox = true
            });

            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.AdditionalFee", "Additional fee");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.AdditionalFeePercentage", "Additional fee. Use percentage");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.AdditionalFeePercentage.Hint", "Determines whether to apply a percentage additional fee to the order total. If not enabled, a fixed value is used.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.PassProductNamesAndTotals", "Pass product names and order totals to Paystack");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.PassProductNamesAndTotals.Hint", "Check if product names and order totals should be passed to Paystack.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.LiveSecretKey", "Live Secret Key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.LiveSecretKey.Hint", "Specify Live Secret Key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.TestSecretKey", "Test Secret Key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.TestSecretKey.Hint", "Specify Test Secret Key");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.RedirectionTip", "You will be redirected to Paystack site to complete the order.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.UseSandbox", "Use Sandbox");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.UseSandbox.Hint", "Check to enable Sandbox (testing environment).");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.Instructions", @"
            <p>
	            <b>If you're using this gateway ensure that your primary store currency is supported by Paystack.</b>
	            <br />
	            <br />To use Paystack, you must setup a Paystack account.  Follow these steps to configure your account for Paystack:<br />
	            <br />1. Log in to your Paystack account (click <a href=""https://dashboard.paystack.com/"" target=""_blank"">here</a> to create your account).
	            <br />2. Click the Settings link on the sidebar.
	            <br />3. Click the API Keys & Webhooks tab.
	            <br />4. Copy Secret Keys, regenerate new ones if you must.
                <br />5. Fill the form below.
	            <br />6. Click Save.
	            <br />
            </p>");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.PaymentMethodDescription", "You will be redirected to Paystack site to complete the payment");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PaystackStandard.RoundingWarning", "It looks like you have \"ShoppingCartSettings.RoundPricesDuringCalculation\" setting disabled. Keep in mind that this can lead to a discrepancy of the order total amount, as Paystack only rounds to two decimals.");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PaystackStandardPaymentSettings>();

            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.AdditionalFee");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.AdditionalFee.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.AdditionalFeePercentage");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.AdditionalFeePercentage.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.PassProductNamesAndTotals");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.PassProductNamesAndTotals.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.LiveSecretKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.LiveSecretKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.TestSecretKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.TestSecretKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.RedirectionTip");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.UseSandbox");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.Fields.UseSandbox.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.Instructions");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.PaymentMethodDescription");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PaystackStandard.RoundingWarning");

            base.Uninstall();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture => false;

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund => false;

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund => false;

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid => false;

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType => RecurringPaymentType.NotSupported;

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType => PaymentMethodType.Redirection;

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo => false;

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription => _localizationService.GetResource("Plugins.Payments.PaystackStandard.PaymentMethodDescription");

        #endregion
    }
}