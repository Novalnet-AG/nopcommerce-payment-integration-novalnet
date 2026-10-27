using Newtonsoft.Json;

namespace Nop.Plugin.Payments.NovalnetApplePay.Models;

/// <summary>
/// Contact fields the Novalnet SDK normalizes from the Apple Pay sheet response
/// (order.billing.contact / order.shipping.contact) - firstName/lastName come from
/// splitting the wallet's "name" field, addressLines from joining address1-3
/// </summary>
public class ExpressContactModel
{
    [JsonProperty("firstName")]
    public string FirstName { get; set; }

    [JsonProperty("lastName")]
    public string LastName { get; set; }

    [JsonProperty("email")]
    public string Email { get; set; }

    [JsonProperty("phoneNumber")]
    public string PhoneNumber { get; set; }

    [JsonProperty("addressLines")]
    public string AddressLines { get; set; }

    [JsonProperty("locality")]
    public string Locality { get; set; }

    [JsonProperty("administrativeArea")]
    public string AdministrativeArea { get; set; }

    [JsonProperty("postalCode")]
    public string PostalCode { get; set; }

    [JsonProperty("countryCode")]
    public string CountryCode { get; set; }
}

public class ExpressPartyModel
{
    [JsonProperty("contact")]
    public ExpressContactModel Contact { get; set; }

    [JsonProperty("email")]
    public string Email { get; set; }
}

public class ExpressOrderModel
{
    [JsonProperty("billing")]
    public ExpressPartyModel Billing { get; set; }

    [JsonProperty("shipping")]
    public ExpressPartyModel Shipping { get; set; }
}

/// <summary>
/// Represents the data the Apple Pay JS SDK hands back in onProcessCompletion on the product/cart page,
/// including the billing/shipping contact info the sheet collects - needed here because, unlike the
/// checkout Payment Info step, there is no address already on file
/// </summary>
public class ExpressApplePayResponseModel
{
    [JsonProperty("token")]
    public string Token { get; set; }

    [JsonProperty("order")]
    public ExpressOrderModel Order { get; set; }
}
