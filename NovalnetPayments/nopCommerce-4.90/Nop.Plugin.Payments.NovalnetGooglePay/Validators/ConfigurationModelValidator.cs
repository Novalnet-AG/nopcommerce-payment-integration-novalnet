using FluentValidation;
using Nop.Plugin.Payments.NovalnetGooglePay.Models;
using Nop.Services.Localization;
using Nop.Web.Framework.Validators;

namespace Nop.Plugin.Payments.NovalnetGooglePay.Validators;

/// <summary>
/// Represents configuration model validator
/// </summary>
public class ConfigurationModelValidator : BaseNopValidator<ConfigurationModel>
{
    #region Ctor

    public ConfigurationModelValidator(ILocalizationService localizationService)
    {
        RuleFor(model => model.GoogleMerchantId)
            .NotEmpty()
            .WithMessageAwait(localizationService.GetResourceAsync("Plugins.Payments.NovalnetGooglePay.Fields.GoogleMerchantId.Required"));
    }

    #endregion
}
