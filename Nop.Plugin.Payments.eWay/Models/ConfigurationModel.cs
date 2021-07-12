using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.eWay.Models
{
    public record ConfigurationModel : BaseNopModel
    {
        [NopResourceDisplayName("Plugins.Payments.eWay.UseSandbox")]
        public bool UseSandbox { get; set; }

        [NopResourceDisplayName("Plugins.Payments.eWay.CustomerId")]
        public string CustomerId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.eWay.AdditionalFee")]
        public decimal AdditionalFee { get; set; }
    }
}