using System;

namespace Nop.Plugin.Payments.NovalnetAPIConfiguration
{
    public class MerchantInfo
    {
        public string signature { get; set; }
        public bool ShouldSerializesignature() =>
        !string.IsNullOrWhiteSpace(signature);
        public string tariff { get; set; }
        public bool ShouldSerializetariff() =>
        !string.IsNullOrWhiteSpace(tariff);
    }

    public class BillingInfo
    {
        public string company { get; set; }
        public bool ShouldSerializecompany() =>
        !string.IsNullOrWhiteSpace(company);
        public string street { get; set; }
        public bool ShouldSerializestreet() =>
        !string.IsNullOrWhiteSpace(street);
        public string city { get; set; }
        public bool ShouldSerializecity() =>
        !string.IsNullOrWhiteSpace(city);
        public string zip { get; set; }
        public bool ShouldSerializezip() =>
        !string.IsNullOrWhiteSpace(zip);
        public string country_code { get; set; }
        public bool ShouldSerializecountry_code() =>
        !string.IsNullOrWhiteSpace(country_code);
        public string state { get; set; }
        public bool ShouldSerializestate() =>
        !string.IsNullOrWhiteSpace(state);
    }

    public class ShippingInfo
    {
        public int? same_as_billing { get; set; }
        public string company { get; set; }
        public bool ShouldSerializecompany() =>
        !string.IsNullOrWhiteSpace(company);
        public string street { get; set; }
        public bool ShouldSerializestreet() =>
        !string.IsNullOrWhiteSpace(street);
        public string city { get; set; }
        public bool ShouldSerializecity() =>
        !string.IsNullOrWhiteSpace(city);
        public string zip { get; set; }
        public bool ShouldSerializezip() =>
        !string.IsNullOrWhiteSpace(zip);
        public string country_code { get; set; }
        public bool ShouldSerializecountry_code() =>
        !string.IsNullOrWhiteSpace(country_code);
        public string state { get; set; }
        public bool ShouldSerializestate() =>
        !string.IsNullOrWhiteSpace(state);
    }

    public class CustomerInfo
    {
        public string first_name { get; set; }
        public bool ShouldSerializefirst_name() =>
        !string.IsNullOrWhiteSpace(first_name);
        public string last_name { get; set; }
        public bool ShouldSerializelast_name() =>
        !string.IsNullOrWhiteSpace(last_name);
        public string email { get; set; }
        public bool ShouldSerializeemail() =>
        !string.IsNullOrWhiteSpace(email);
        public string customer_no { get; set; }
        public bool ShouldSerializecustomer_no() =>
        !string.IsNullOrWhiteSpace(customer_no);

        public string birth_date { get; set; }
        public bool ShouldSerializebirth_date() =>
        !string.IsNullOrWhiteSpace(birth_date);

        public string customer_ip { get; set; }
        public bool ShouldSerializecustomer_ip() =>
        !string.IsNullOrWhiteSpace(customer_ip);
        public string tel { get; set; }
        public bool ShouldSerializetel() =>
        !string.IsNullOrWhiteSpace(tel);
        public BillingInfo billing { get; set; }
        public ShippingInfo shipping { get; set; }
    }
    public class PaymentInfo
    {
        public string pan_hash { get; set; }
        public bool ShouldSerializepan_hash() =>
        !string.IsNullOrWhiteSpace(pan_hash);

        public string unique_id { get; set; }
        public bool ShouldSerializeunique_id() =>
        !string.IsNullOrWhiteSpace(unique_id);

        public string iban { get; set; }
        public bool ShouldSerializeiban() =>
        !string.IsNullOrWhiteSpace(iban);

        public string bic { get; set; }
        public bool ShouldSerializebic() =>
        !string.IsNullOrWhiteSpace(bic);

        public string account_number { get; set; }
        public bool ShouldSerializeaccount_number() =>
        !string.IsNullOrWhiteSpace(account_number);

        public string routing_number { get; set; }
        public bool ShouldSerializerouting_number() =>
        !string.IsNullOrWhiteSpace(routing_number);

        public string account_holder { get; set; }
        public bool ShouldSerializeaccount_holder() =>
        !string.IsNullOrWhiteSpace(account_holder);
    }

    public class TransactionInfo
    {
        public int amount { get; set; }
        public string currency { get; set; }
        public bool ShouldSerializecurrency() =>
        !string.IsNullOrWhiteSpace(currency);
        public int test_mode { get; set; }
        public string payment_type { get; set; }
        public bool ShouldSerializepayment_type() =>
        !string.IsNullOrWhiteSpace(payment_type);
        public string return_url { get; set; }
        public bool ShouldSerializereturn_url() =>
        !string.IsNullOrWhiteSpace(return_url);
        public string error_return_url { get; set; }
        public bool ShouldSerializeerror_return_url() =>
        !string.IsNullOrWhiteSpace(error_return_url);
        public string order_no { get; set; }
        public bool ShouldSerializeorder_no() =>
        !string.IsNullOrWhiteSpace(order_no);
        public string due_date { get; set; }
        public bool ShouldSerializedue_date() =>
        !string.IsNullOrWhiteSpace(due_date);

        public int? enforce_3d { get; set; }

        public string system_ip { get; set; }
        public bool ShouldSerializesystem_ip() =>
        !string.IsNullOrWhiteSpace(system_ip);

        public string system_name { get; set; }
        public bool ShouldSerializesystem_name() =>
        !string.IsNullOrWhiteSpace(system_name);

        public string system_url { get; set; }
        public bool ShouldSerializesystem_url() =>
        !string.IsNullOrWhiteSpace(system_url);

        public string system_version { get; set; }
        public bool ShouldSerializesystem_version() =>
        !string.IsNullOrWhiteSpace(system_version);

        public PaymentInfo payment_data { get; set; }
    }

    public class CustomInfo
    {
        public string lang { get; set; }
        public bool ShouldSerializelang() =>
        !string.IsNullOrWhiteSpace(lang);

        public string input1 { get; set; }
        public bool ShouldSerializeinput1() =>
        !string.IsNullOrWhiteSpace(input1);

        public string inputval1 { get; set; }
        public bool ShouldSerializeinputval1() =>
        !string.IsNullOrWhiteSpace(inputval1);
    }

    public class NovalnetPayload
    {
        public MerchantInfo merchant { get; set; }
        public CustomerInfo customer { get; set; }
        public TransactionInfo transaction { get; set; }
        public CustomInfo custom { get; set; }
    }
}
