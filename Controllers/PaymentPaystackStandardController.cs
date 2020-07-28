using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.PaystackStandard.Models;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.PaystackStandard.Controllers
{
    public class PaymentPaystackStandardController : BasePaymentController
    {
        #region Fields

        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IOrderService _orderService;
        private readonly IPaymentPluginManager _paymentPluginManager;
        private readonly IPermissionService _permissionService;
        private readonly ILocalizationService _localizationService;
        private readonly ILogger _logger;
        private readonly INotificationService _notificationService;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly ShoppingCartSettings _shoppingCartSettings;

        #endregion

        #region Ctor

        public PaymentPaystackStandardController(IGenericAttributeService genericAttributeService,
            IOrderProcessingService orderProcessingService,
            IOrderService orderService,
            IPaymentPluginManager paymentPluginManager,
            IPermissionService permissionService,
            ILocalizationService localizationService,
            ILogger logger,
            INotificationService notificationService,
            ISettingService settingService,
            IStoreContext storeContext,
            IWebHelper webHelper,
            IWorkContext workContext,
            ShoppingCartSettings shoppingCartSettings)
        {
            _genericAttributeService = genericAttributeService;
            _orderProcessingService = orderProcessingService;
            _orderService = orderService;
            _paymentPluginManager = paymentPluginManager;
            _permissionService = permissionService;
            _localizationService = localizationService;
            _logger = logger;
            _notificationService = notificationService;
            _settingService = settingService;
            _storeContext = storeContext;
            _webHelper = webHelper;
            _workContext = workContext;
            _shoppingCartSettings = shoppingCartSettings;
        }

        #endregion


        #region Methods

        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var paystackStandardPaymentSettings = _settingService.LoadSetting<PaystackStandardPaymentSettings>(storeScope);

            var model = new ConfigurationModel
            {
                UseSandbox = paystackStandardPaymentSettings.UseSandbox,
                LiveSecretKey = paystackStandardPaymentSettings.LiveSecretToken,
                TestSecretKey = paystackStandardPaymentSettings.TestSecretToken,
                PassProductNamesAndTotals = paystackStandardPaymentSettings.PassProductNamesAndTotals,
                AdditionalFee = paystackStandardPaymentSettings.AdditionalFee,
                AdditionalFeePercentage = paystackStandardPaymentSettings.AdditionalFeePercentage,
                ActiveStoreScopeConfiguration = storeScope
            };

            if (storeScope <= 0)
                return View("~/Plugins/Payments.PaystackStandard/Views/Configure.cshtml", model);

            model.UseSandbox_OverrideForStore = _settingService.SettingExists(paystackStandardPaymentSettings, x => x.UseSandbox, storeScope);
            model.LiveSecretKey_OverrideForStore = _settingService.SettingExists(paystackStandardPaymentSettings, x => x.LiveSecretToken, storeScope);
            model.TestSecretKey_OverrideForStore = _settingService.SettingExists(paystackStandardPaymentSettings, x => x.TestSecretToken, storeScope);
            model.PassProductNamesAndTotals_OverrideForStore = _settingService.SettingExists(paystackStandardPaymentSettings, x => x.PassProductNamesAndTotals, storeScope);
            model.AdditionalFee_OverrideForStore = _settingService.SettingExists(paystackStandardPaymentSettings, x => x.AdditionalFee, storeScope);
            model.AdditionalFeePercentage_OverrideForStore = _settingService.SettingExists(paystackStandardPaymentSettings, x => x.AdditionalFeePercentage, storeScope);

            return View("~/Plugins/Payments.PaystackStandard/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [AdminAntiForgery]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var paystackStandardPaymentSettings = _settingService.LoadSetting<PaystackStandardPaymentSettings>(storeScope);

            //save settings
            paystackStandardPaymentSettings.UseSandbox = model.UseSandbox;
            paystackStandardPaymentSettings.LiveSecretToken = model.LiveSecretKey;
            paystackStandardPaymentSettings.TestSecretToken = model.TestSecretKey;
            paystackStandardPaymentSettings.PassProductNamesAndTotals = model.PassProductNamesAndTotals;
            paystackStandardPaymentSettings.AdditionalFee = model.AdditionalFee;
            paystackStandardPaymentSettings.AdditionalFeePercentage = model.AdditionalFeePercentage;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(paystackStandardPaymentSettings, x => x.UseSandbox, model.UseSandbox_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(paystackStandardPaymentSettings, x => x.LiveSecretToken, model.LiveSecretKey_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(paystackStandardPaymentSettings, x => x.TestSecretToken, model.TestSecretKey_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(paystackStandardPaymentSettings, x => x.PassProductNamesAndTotals, model.PassProductNamesAndTotals_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(paystackStandardPaymentSettings, x => x.AdditionalFee, model.AdditionalFee_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(paystackStandardPaymentSettings, x => x.AdditionalFeePercentage, model.AdditionalFeePercentage_OverrideForStore, storeScope, false);

            //now clear settings cache
            _settingService.ClearCache();

            _notificationService.SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        //action displaying notification (warning) to a store owner about inaccurate Paystack rounding
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult RoundingWarning(bool passProductNamesAndTotals)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //prices and total aren't rounded, so display warning
            if (passProductNamesAndTotals && !_shoppingCartSettings.RoundPricesDuringCalculation)
                return Json(new { Result = _localizationService.GetResource("Plugins.Payments.PaystackStandard.RoundingWarning") });

            return Json(new { Result = string.Empty });
        }

        public IActionResult CallbackHandler()
        {
            var reference = _webHelper.QueryString<string>("trxref");

            if (!(_paymentPluginManager.LoadPluginBySystemName("Payments.PaystackStandard") is PaystackStandardPaymentProcessor processor) || !_paymentPluginManager.IsPluginActive(processor))
                throw new NopException("Paystack Standard module cannot be loaded");

            var verificationResponse = processor.VerifyTransaction(reference);
            var order = _orderService.GetOrderByGuid(new Guid(reference));

            if (order == null)
            {
                return RedirectToAction("Index", "Home", new { area = string.Empty });
            }
            //else if (order.OrderTotal != verificationResponse.Amount)
            //{
            //    var errorStr = $"Paystack returned an order total { verificationResponse.Amount} that doesn't equal order total {order.OrderTotal}. Order# {order.Id}.";
            //    //log
            //    _logger.Error(errorStr);
            //    //order note
            //    order.OrderNotes.Add(new OrderNote
            //    {
            //        Note = errorStr,
            //        DisplayToCustomer = false,
            //        CreatedOnUtc = DateTime.UtcNow
            //    });
            //    _orderService.UpdateOrder(order);

            //    return RedirectToAction("Index", "Home", new { area = string.Empty });
            //}

            order.OrderNotes.Add(new OrderNote
            {
                Note = verificationResponse.Message,
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });

            order.AuthorizationTransactionId = reference;
            order.AuthorizationTransactionCode = verificationResponse.AuthCode;
            _orderService.UpdateOrder(order);

            if (verificationResponse.IsVerified)
                _orderProcessingService.MarkOrderAsPaid(order);
            else
                _logger.Error($"Paystack payment order {reference} failed.", new NopException(verificationResponse.Message));


            return RedirectToRoute("CheckoutCompleted", new { orderId = order.Id });
        }

        public IActionResult NotifyHandler()
        {
            var reference = _webHelper.QueryString<string>("trxref");


            if (!(_paymentPluginManager.LoadPluginBySystemName("Payments.PaystackStandard") is PaystackStandardPaymentProcessor processor) || !_paymentPluginManager.IsPluginActive(processor))
                throw new NopException("Paystack Standard module cannot be loaded");

            var verificationResponse = processor.VerifyTransaction(reference);
            var order = _orderService.GetOrderByGuid(new Guid(reference));

            if (order == null)
                return Content(string.Empty);

            //else if (order.OrderTotal != verificationResponse.Amount)
            //{
            //    var errorStr = $"Paystack returned an order total { verificationResponse.Amount} that doesn't equal order total {order.OrderTotal}. Order# {order.Id}.";
                
            //    _logger.Error(errorStr);
                
            //    order.OrderNotes.Add(new OrderNote
            //    {
            //        Note = errorStr,
            //        DisplayToCustomer = false,
            //        CreatedOnUtc = DateTime.UtcNow
            //    });
            //    _orderService.UpdateOrder(order);

            //    return Content(string.Empty);
            //}

            order.OrderNotes.Add(new OrderNote
            {
                Note = verificationResponse.Message,
                DisplayToCustomer = false,
                CreatedOnUtc = DateTime.UtcNow
            });

            order.AuthorizationTransactionId = reference;
            order.AuthorizationTransactionCode = verificationResponse.AuthCode;
            _orderService.UpdateOrder(order);

            if (verificationResponse.IsVerified)
                _orderProcessingService.MarkOrderAsPaid(order);
            else
                _logger.Error($"Paystack payment order {reference} failed.", new NopException(verificationResponse.Message));

            return Content(string.Empty);
        }

        public IActionResult CancelOrder()
        {
            var order = _orderService.SearchOrders(_storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1).FirstOrDefault();

            if (order != null)
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });

            return RedirectToRoute("Homepage");
        }

        #endregion
    }
}