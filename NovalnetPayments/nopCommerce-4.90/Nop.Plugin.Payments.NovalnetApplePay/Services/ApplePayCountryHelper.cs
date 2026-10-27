using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Services.Common;
using Nop.Services.Customers;
using Nop.Services.Directory;

namespace Nop.Plugin.Payments.NovalnetApplePay.Services;

/// <summary>
/// Resolves the country code the Apple Pay sheet is initialized with, before the buyer picks an address in it
/// </summary>
public static class ApplePayCountryHelper
{
    /// <summary>
    /// Gets the two-letter ISO code of the customer's shipping (or billing) address country,
    /// falling back to the store's default country; null if neither is available
    /// </summary>
    public static async Task<string> GetDefaultCountryCodeAsync(Customer customer,
        ICustomerService customerService,
        ICountryService countryService,
        AddressSettings addressSettings)
    {
        var address = await customerService.GetCustomerShippingAddressAsync(customer)
            ?? await customerService.GetCustomerBillingAddressAsync(customer);

        var countryId = address?.CountryId ?? addressSettings.DefaultCountryId;
        if (countryId is null or 0)
            return null;

        var country = await countryService.GetCountryByIdAsync(countryId.Value);

        return country?.TwoLetterIsoCode;
    }
}
