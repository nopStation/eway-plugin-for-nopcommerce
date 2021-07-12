using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Services.Plugins;
using Nop.Plugin.Payments.eWay.Models;
using Nop.Plugin.Payments.eWay.Validators;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Localization;
using Nop.Services.Payments;
using System.Threading.Tasks;
using Nop.Services.Common;

namespace Nop.Plugin.Payments.eWay
{
    /// <summary>
    /// eWay payment processor
    /// </summary>
    public class eWayPaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private const string APPROVED_RESPONSE = "00";
        private const string HONOUR_RESPONSE = "08";

        private readonly ICustomerService _customerService;
        private readonly eWayPaymentSettings _eWayPaymentSettings;
        private readonly ISettingService _settingService;
        private readonly IStoreContext _storeContext;
        private readonly ILocalizationService _localizationService;
        private readonly IWebHelper _webHelper;
        private readonly IAddressService _addressService;

        #endregion

        #region Ctor

        public eWayPaymentProcessor(ICustomerService customerService, 
            eWayPaymentSettings eWayPaymentSettings,
            ISettingService settingService, 
            IStoreContext storeContext,
            ILocalizationService localizationService, 
            IWebHelper webHelper,
            IAddressService addressService)
        {
            _customerService = customerService;
            _eWayPaymentSettings = eWayPaymentSettings;
            _settingService = settingService;
            _storeContext = storeContext;
            _localizationService = localizationService;
            _webHelper = webHelper;
            _addressService = addressService;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Gets eWay URL
        /// </summary>
        /// <returns></returns>
        private string GeteWayUrl()
        {
            return _eWayPaymentSettings.UseSandbox ? "https://www.eway.com.au/gateway_cvn/xmltest/TestPage.asp" :
                "https://www.eway.com.au/gateway_cvn/xmlpayment.asp";
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public async Task<ProcessPaymentResult> ProcessPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();

            var eWaygateway = new GatewayConnector();

            var eWayRequest = new GatewayRequest
            {
                EwayCustomerID = _eWayPaymentSettings.CustomerId,
                CardNumber = processPaymentRequest.CreditCardNumber,
                CardExpiryMonth = processPaymentRequest.CreditCardExpireMonth.ToString("D2"),
                CardExpiryYear = processPaymentRequest.CreditCardExpireYear.ToString(),
                CardHolderName = processPaymentRequest.CreditCardName,
                InvoiceAmount = Convert.ToInt32(processPaymentRequest.OrderTotal * 100)
            };

            //Integer

            var customer = await _customerService.GetCustomerByIdAsync(processPaymentRequest.CustomerId);
            var billingAddress = await _addressService.GetAddressByIdAsync(customer.BillingAddressId ?? 0);
            if (billingAddress == null)
            {
                result.AddError("Billing address not found.");
                return result;
            }

            eWayRequest.PurchaserFirstName = billingAddress.FirstName;
            eWayRequest.PurchaserLastName = billingAddress.LastName;
            eWayRequest.PurchaserEmailAddress = billingAddress.Email;
            eWayRequest.PurchaserAddress = billingAddress.Address1;
            eWayRequest.PurchaserPostalCode = billingAddress.ZipPostalCode;
            eWayRequest.InvoiceReference = processPaymentRequest.OrderGuid.ToString();
            eWayRequest.InvoiceDescription = _storeContext.GetCurrentStore().Name + ". Order #" + processPaymentRequest.OrderGuid;
            eWayRequest.TransactionNumber = processPaymentRequest.OrderGuid.ToString();
            eWayRequest.CVN = processPaymentRequest.CreditCardCvv2;
            eWayRequest.EwayOption1 = string.Empty;
            eWayRequest.EwayOption2 = string.Empty;
            eWayRequest.EwayOption3 = string.Empty;

            // Do the payment, send XML doc containing information gathered
            eWaygateway.Uri = GeteWayUrl();
            var eWayResponse = await eWaygateway.ProcessRequestAsync(eWayRequest);
            if (eWayResponse != null)
            {
                // Payment succeeded get values returned
                if (eWayResponse.Status && (eWayResponse.Error.StartsWith(APPROVED_RESPONSE) || eWayResponse.Error.StartsWith(HONOUR_RESPONSE)))
                {
                    result.AuthorizationTransactionCode = eWayResponse.AuthorisationCode;
                    result.AuthorizationTransactionResult = eWayResponse.InvoiceReference;
                    result.AuthorizationTransactionId = eWayResponse.TransactionNumber;
                    result.NewPaymentStatus = PaymentStatus.Paid;
                    //processPaymentResult.AuthorizationDate = DateTime.UtcNow;
                }
                else
                {
                    result.AddError("An invalid response was recieved from the payment gateway." + eWayResponse.Error);
                    //full error: eWAYRequest.ToXml().ToString()
                }
            }
            else
            {
                // invalid response recieved from server.
                result.AddError("An invalid response was recieved from the payment gateway.");
                //full error: eWAYRequest.ToXml().ToString()
            }

            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public Task PostProcessPaymentAsync(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            return Task.CompletedTask;
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public Task<bool> HidePaymentMethodAsync(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return Task.FromResult(false);
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public Task<decimal> GetAdditionalHandlingFeeAsync(IList<ShoppingCartItem> cart)
        {
            return Task.FromResult(_eWayPaymentSettings.AdditionalFee);
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public Task<CapturePaymentResult> CaptureAsync(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            result.AddError("Capture method not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public Task<RefundPaymentResult> RefundAsync(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public Task<VoidPaymentResult> VoidAsync(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public Task<ProcessPaymentResult> ProcessRecurringPaymentAsync(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.AddError("Recurring payment not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public Task<CancelRecurringPaymentResult> CancelRecurringPaymentAsync(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            var result = new CancelRecurringPaymentResult();
            result.AddError("Recurring payment not supported");
            return Task.FromResult(result);
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public Task<bool> CanRePostProcessPaymentAsync(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));
            
            //it's not a redirection payment method. So we always return false
            return Task.FromResult(false);
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymenteWay/Configure";
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public Task<IList<string>> ValidatePaymentFormAsync(IFormCollection form)
        {
            var warnings = new List<string>();

            //validate
            var validator = new PaymentInfoValidator(_localizationService);
            var model = new PaymentInfoModel()
            {
                CardholderName = form["CardholderName"],
                CardNumber = form["CardNumber"],
                CardCode = form["CardCode"],
            };
            var validationResult = validator.Validate(model);
            if (validationResult.IsValid) 
                return Task.FromResult<IList<string>>(warnings);

            warnings.AddRange(validationResult.Errors.Select(error => error.ErrorMessage));
            return Task.FromResult<IList<string>>(warnings);
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public Task<ProcessPaymentRequest> GetPaymentInfoAsync(IFormCollection form)
        {
            var paymentInfo = new ProcessPaymentRequest
            {
                CreditCardType = form["CreditCardType"],
                CreditCardName = form["CardholderName"],
                CreditCardNumber = form["CardNumber"],
                CreditCardExpireMonth = int.Parse(form["ExpireMonth"]),
                CreditCardExpireYear = int.Parse(form["ExpireYear"]),
                CreditCardCvv2 = form["CardCode"]
            };

            return Task.FromResult(paymentInfo);
        }

        /// <summary>
        /// Install plugin
        /// </summary>
        public override async Task InstallAsync()
        {
            var settings = new eWayPaymentSettings()
            {
                UseSandbox = true,
                CustomerId = string.Empty,
                AdditionalFee = 0,
            };
            await _settingService.SaveSettingAsync(settings);

            //locales
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.eWay.UseSandbox", "Use sandbox");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.eWay.UseSandbox.Hint", "Use sandbox?");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.eWay.CustomerId", "Customer ID");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.eWay.CustomerId.Hint", "Enter customer ID.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.eWay.AdditionalFee", "Additional fee");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.eWay.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            await _localizationService.AddOrUpdateLocaleResourceAsync("Plugins.Payments.eWay.PaymentMethodDescription", "Pay by credit / debit card");

            await base.InstallAsync();
        }
        
        /// <summary>
        /// Uninstall plugin
        /// </summary>
        public override async Task UninstallAsync()
        {
            //locales
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.eWay.UseSandbox");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.eWay.UseSandbox.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.eWay.CustomerId");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.eWay.CustomerId.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.eWay.AdditionalFee");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.eWay.AdditionalFee.Hint");
            await _localizationService.DeleteLocaleResourceAsync("Plugins.Payments.eWay.PaymentMethodDescription");

            await base.UninstallAsync();
        }

        /// <summary>
        /// Gets a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <param name="viewComponentName">View component name</param>
        public string GetPublicViewComponentName()
        {
            return "PaymenteWay";
        }

        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.NotSupported;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Standard;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public async Task<string> GetPaymentMethodDescriptionAsync()
        {
            return await _localizationService.GetResourceAsync("Plugins.Payments.eWay.PaymentMethodDescription");
        }

        #endregion
    }
}
