using Microsoft.AspNetCore.Mvc.Rendering;
using Nop.Web.Framework.Models;
using Nop.Web.Framework.Mvc.ModelBinding;

namespace Nop.Plugin.Payments.NovalnetApplePay.Models;

public record PaymentInfoModel : BaseNopModel
{
   public string DescriptionText { get; set; }

   public bool TestMode { get; set; }

   public string PaymentJsonData { get; set; }

   public int? IsVirtualProduct { get; set; }

}
