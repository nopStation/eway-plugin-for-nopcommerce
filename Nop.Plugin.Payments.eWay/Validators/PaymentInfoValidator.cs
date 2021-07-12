﻿using FluentValidation;
using Nop.Plugin.Payments.eWay.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Payments.eWay.Validators
{
    public class PaymentInfoValidator : AbstractValidator<PaymentInfoModel>
    {
        public PaymentInfoValidator(ILocalizationService localizationService)
        {
            //useful links:
            //http://fluentvalidation.codeplex.com/wikipage?title=Custom&referringTitle=Documentation&ANCHOR#CustomValidator
            //http://benjii.me/2010/11/credit-card-validator-attribute-for-asp-net-mvc-3/

            RuleFor(x => x.CardholderName).NotEmpty().WithMessage(localizationService.GetResourceAsync("Payment.CardholderName.Required").Result);
            RuleFor(x => x.CardNumber).IsCreditCard().WithMessage(localizationService.GetResourceAsync("Payment.CardNumber.Wrong").Result);
            RuleFor(x => x.CardCode).Matches(@"^[0-9]{3,4}$").WithMessage(localizationService.GetResourceAsync("Payment.CardCode.Wrong").Result);
        }
    }
}