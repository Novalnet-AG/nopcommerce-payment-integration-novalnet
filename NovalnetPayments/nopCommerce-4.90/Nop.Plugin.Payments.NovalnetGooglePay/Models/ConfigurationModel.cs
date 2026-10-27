using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Plugin.Payments.NovalnetGooglePay.Domain;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.Payments.NovalnetGooglePay.Models;

public record ConfigurationModel : BaseNopModel, ILocalizedModel<ConfigurationModel.ConfigurationLocalizedModel>
{
    public ConfigurationModel()
    {
        Locales = new List<ConfigurationLocalizedModel>();
    }

    [NopResourceDisplayName("Plugins.Payments.NovalnetGooglePay.Fields.DescriptionText")]
    public string DescriptionText { get; set; }
    public bool DescriptionText_OverrideForStore { get; set; }

    public int ActiveStoreScopeConfiguration { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetGooglePay.Fields.AdditionalFeePercentage")]
    public bool AdditionalFeePercentage { get; set; }
    public bool AdditionalFeePercentage_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetGooglePay.Fields.AdditionalFee")]
    public decimal AdditionalFee { get; set; }
    public bool AdditionalFee_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetGooglePay.Fields.TestMode")]
    public bool TestMode { get; set; }
    public bool TestMode_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetGooglePay.Fields.OnholdMinAmount")]
    public int? OnholdMinAmount { get; set; }
    public bool OnholdMinAmount_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetGooglePay.Fields.BusinessName")]
    public string BusinessName { get; set; }
    public bool BusinessName_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetGooglePay.Fields.GoogleMerchantId")]
    public string GoogleMerchantId { get; set; }
    public bool GoogleMerchantId_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetGooglePay.Fields.PaymentAction")]
    public string PaymentActionString { get; set; }
    public IList<SelectListItem> AvailablePaymentActions { get; set; } = new List<SelectListItem>();

    public bool PaymentAction_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetGooglePay.Fields.ExpressCheckoutPages")]
    public IList<string> SelectedExpressCheckoutPages { get; set; } = new List<string>();
    public IList<SelectListItem> AvailableExpressCheckoutPages { get; set; } = new List<SelectListItem>();
    public bool ExpressCheckoutPages_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetGooglePay.Fields.ButtonHeight")]
    [Range(GooglePayButtonDefaults.MinHeight, GooglePayButtonDefaults.MaxHeight)]
    public int ButtonHeight { get; set; }
    public bool ButtonHeight_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetGooglePay.Fields.ButtonType")]
    public string ButtonType { get; set; }
    public IList<SelectListItem> AvailableButtonTypes { get; set; } = new List<SelectListItem>();
    public bool ButtonType_OverrideForStore { get; set; }

    public IList<ConfigurationLocalizedModel> Locales { get; set; }

    #region Nested class

    public class ConfigurationLocalizedModel : ILocalizedLocaleModel
    {
        public int LanguageId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.NovalnetGooglePay.Fields.DescriptionText")]
        public string DescriptionText { get; set; }
    }
    #endregion
}
