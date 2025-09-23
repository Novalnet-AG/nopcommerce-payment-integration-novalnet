using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;
using Nop.Plugin.Payments.NovalnetAPIConfiguration.Validators;  


namespace Nop.Plugin.Payments.NovalnetSepa.Models;

public record ConfigurationModel : BaseNopModel, ILocalizedModel<ConfigurationModel.ConfigurationLocalizedModel>
{
    public ConfigurationModel()
    {
        Locales = new List<ConfigurationLocalizedModel>();
    }

    [NopResourceDisplayName("Plugins.Payments.NovalnetSepa.Fields.DescriptionText")]
    public string DescriptionText { get; set; }
    public bool DescriptionText_OverrideForStore { get; set; }

    public int ActiveStoreScopeConfiguration { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetSepa.Fields.AdditionalFeePercentage")]
    public bool AdditionalFeePercentage { get; set; }
    public bool AdditionalFeePercentage_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetSepa.Fields.AdditionalFee")]
    public decimal AdditionalFee { get; set; }
    public bool AdditionalFee_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetSepa.Fields.TestMode")]
    public bool TestMode { get; set; }
    public bool TestMode_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetSepa.Fields.OnholdMinAmount")]
    public int? OnholdMinAmount { get; set; }
    public bool OnholdMinAmount_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetSepa.Fields.DueDate")]
    [OptionalRange(3, 14)]
    public int? DueDate { get; set; }
    public bool DueDate_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetSepa.Fields.PaymentAction")]
    public string PaymentActionString { get; set; }
    public IList<SelectListItem> AvailablePaymentActions { get; set; } = new List<SelectListItem>();

    public bool PaymentAction_OverrideForStore { get; set; }


    public IList<ConfigurationLocalizedModel> Locales { get; set; }

    #region Nested class

    public class ConfigurationLocalizedModel : ILocalizedLocaleModel
    {
        public int LanguageId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.NovalnetSepa.Fields.DescriptionText")]
        public string DescriptionText { get; set; }
    }
    #endregion

}
