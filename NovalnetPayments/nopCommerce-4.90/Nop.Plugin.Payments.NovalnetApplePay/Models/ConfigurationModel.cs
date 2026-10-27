using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.Payments.NovalnetApplePay.Models;

public record ConfigurationModel : BaseNopModel, ILocalizedModel<ConfigurationModel.ConfigurationLocalizedModel>
{
    public ConfigurationModel()
    {
        Locales = new List<ConfigurationLocalizedModel>();
    }

    [NopResourceDisplayName("Plugins.Payments.NovalnetApplePay.Fields.DescriptionText")]
    public string DescriptionText { get; set; }
    public bool DescriptionText_OverrideForStore { get; set; }

    public int ActiveStoreScopeConfiguration { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetApplePay.Fields.AdditionalFeePercentage")]
    public bool AdditionalFeePercentage { get; set; }
    public bool AdditionalFeePercentage_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetApplePay.Fields.AdditionalFee")]
    public decimal AdditionalFee { get; set; }
    public bool AdditionalFee_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetApplePay.Fields.TestMode")]
    public bool TestMode { get; set; }
    public bool TestMode_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetApplePay.Fields.OnholdMinAmount")]
    public int? OnholdMinAmount { get; set; }
    public bool OnholdMinAmount_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetApplePay.Fields.BusinessName")]
    public string BusinessName { get; set; }
    public bool BusinessName_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetApplePay.Fields.PaymentAction")]
    public string PaymentActionString { get; set; }
    public IList<SelectListItem> AvailablePaymentActions { get; set; } = new List<SelectListItem>();

    public bool PaymentAction_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetApplePay.Fields.ButtonType")]
    public string ButtonType { get; set; }
    public IList<SelectListItem> AvailableButtonTypes { get; set; } = new List<SelectListItem>();
    public bool ButtonType_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetApplePay.Fields.ButtonTheme")]
    public string ButtonTheme { get; set; }
    public IList<SelectListItem> AvailableButtonThemes { get; set; } = new List<SelectListItem>();
    public bool ButtonTheme_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetApplePay.Fields.ButtonHeight")]
    [Range(30, 64)]
    public int ButtonHeight { get; set; }
    public bool ButtonHeight_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetApplePay.Fields.ButtonCornerRadius")]
    [Range(0, 10)]
    public int ButtonCornerRadius { get; set; }
    public bool ButtonCornerRadius_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetApplePay.Fields.DisplayPages")]
    public IList<string> SelectedDisplayPages { get; set; } = new List<string>();
    public IList<SelectListItem> AvailableDisplayPages { get; set; } = new List<SelectListItem>();
    public bool DisplayPages_OverrideForStore { get; set; }

    public IList<ConfigurationLocalizedModel> Locales { get; set; }

    #region Nested class

    public class ConfigurationLocalizedModel : ILocalizedLocaleModel
    {
        public int LanguageId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.NovalnetApplePay.Fields.DescriptionText")]
        public string DescriptionText { get; set; }
    }
    #endregion
}
