using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Nop.Plugin.Payments.eWay.Models;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Messages;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;

namespace Nop.Plugin.Payments.eWay.Controllers
{
    [AuthorizeAdmin]
    [Area(AreaNames.Admin)]
    public class PaymenteWayController : BasePaymentController
    {
        private readonly ISettingService _settingService;
        private readonly eWayPaymentSettings _eWayPaymentSettings;
        private readonly IPermissionService _permissionService;
        private readonly INotificationService _notificationService;
        private readonly ILocalizationService _localizationService;

        public PaymenteWayController(ISettingService settingService, 
            eWayPaymentSettings eWayPaymentSettings,
            IPermissionService permissionService,
            INotificationService notificationService,
            ILocalizationService localizationService)
        {
            _settingService = settingService;
            _eWayPaymentSettings = eWayPaymentSettings;
            _permissionService = permissionService;
            _notificationService = notificationService;
            _localizationService = localizationService;
        }

        public async Task<IActionResult> Configure()
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            var model = new ConfigurationModel
            {
                UseSandbox = _eWayPaymentSettings.UseSandbox,
                CustomerId = _eWayPaymentSettings.CustomerId,
                AdditionalFee = _eWayPaymentSettings.AdditionalFee
            };

            return View("~/Plugins/Payments.eWay/Views/Configure.cshtml", model);
        }

        [HttpPost]
        public async Task<IActionResult> Configure(ConfigurationModel model)
        {
            if (!await _permissionService.AuthorizeAsync(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //save settings
            _eWayPaymentSettings.UseSandbox = model.UseSandbox;
            _eWayPaymentSettings.CustomerId = model.CustomerId;
            _eWayPaymentSettings.AdditionalFee = model.AdditionalFee;

            await _settingService.SaveSettingAsync(_eWayPaymentSettings);

            _notificationService.SuccessNotification(await _localizationService.GetResourceAsync("Admin.Plugins.Saved"));

            return RedirectToAction("Configure");
        }
    }
}