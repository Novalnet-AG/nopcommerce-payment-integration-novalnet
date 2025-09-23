using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;
using System.ComponentModel.DataAnnotations;

namespace Nop.Plugin.Payments.NovalnetCreditcard.Models;

public record ConfigurationModel : BaseNopModel, ILocalizedModel<ConfigurationModel.ConfigurationLocalizedModel>
{
    public ConfigurationModel()
    {
        Locales = new List<ConfigurationLocalizedModel>();
    }

    [NopResourceDisplayName("Plugins.Payments.NovalnetCreditcard.Fields.DescriptionText")]
    public string DescriptionText { get; set; }
    public bool DescriptionText_OverrideForStore { get; set; }

    public int ActiveStoreScopeConfiguration { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetCreditcard.Fields.AdditionalFeePercentage")]
    public bool AdditionalFeePercentage { get; set; }
    public bool AdditionalFeePercentage_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetCreditcard.Fields.AdditionalFee")]
    public decimal AdditionalFee { get; set; }
    public bool AdditionalFee_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetCreditcard.Fields.TestMode")]
    public bool TestMode { get; set; }
    public bool TestMode_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetCreditcard.Fields.OnholdMinAmount")]
    public int? OnholdMinAmount { get; set; }
    public bool OnholdMinAmount_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetCreditcard.Fields.Enforce3d")]
    public bool Enforce3d { get; set; }
    public bool Enforce3d_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetCreditcard.Fields.InlineForm")]
    public bool InlineForm { get; set; }
    public bool InlineForm_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetCreditcard.Fields.LabelStyle")]
    public string LabelStyle { get; set; }
    public bool LabelStyle_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetCreditcard.Fields.InputStyle")]
    public string InputStyle { get; set; }
    public bool InputStyle_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetCreditcard.Fields.TextStyle")]
    public string TextStyle { get; set; }
    public bool TextStyle_OverrideForStore { get; set; }

    [NopResourceDisplayName("Plugins.Payments.NovalnetCreditcard.Fields.PaymentAction")]
    public string PaymentActionString { get; set; }
    public IList<SelectListItem> AvailablePaymentActions { get; set; } = new List<SelectListItem>();

    public bool PaymentAction_OverrideForStore { get; set; }


    public IList<ConfigurationLocalizedModel> Locales { get; set; }

    #region Nested class

    public class ConfigurationLocalizedModel : ILocalizedLocaleModel
    {
        public int LanguageId { get; set; }

        [NopResourceDisplayName("Plugins.Payments.NovalnetCreditcard.Fields.DescriptionText")]
        public string DescriptionText { get; set; }
    }
    #endregion
}
