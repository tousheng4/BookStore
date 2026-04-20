using System.ComponentModel.DataAnnotations;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Admin.Coupons;

[Authorize(Roles = UserRoles.Admin)]
public class CreateModel(StoreService storeService) : PageModel
{
    [BindProperty]
    public CouponInput Input { get; set; } = new();

    public string? Message { get; private set; }

    public void OnGet()
    {
        Input.StartsAt = DateTime.UtcNow;
        Input.EndsAt = DateTime.UtcNow.AddMonths(1);
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            Message = "请填写优惠券名称。";
            return Page();
        }
        if (string.IsNullOrWhiteSpace(Input.Code))
        {
            Message = "请填写优惠码。";
            return Page();
        }
        if (Input.MinimumAmount < 0)
        {
            Message = "满减门槛不能为负数。";
            return Page();
        }
        if (Input.DiscountAmount <= 0)
        {
            Message = "优惠金额必须大于 0。";
            return Page();
        }
        if (Input.EndsAt <= Input.StartsAt)
        {
            Message = "结束时间必须晚于开始时间。";
            return Page();
        }

        Input.Code = Input.Code.Trim().ToUpperInvariant();

        var coupon = new Coupon
        {
            Name = Input.Name.Trim(),
            Code = Input.Code,
            MinimumAmount = Input.MinimumAmount,
            DiscountAmount = Input.DiscountAmount,
            IsActive = Input.IsActive,
            StartsAt = Input.StartsAt.ToUniversalTime(),
            EndsAt = Input.EndsAt.ToUniversalTime(),
            CreatedAt = DateTime.UtcNow
        };

        try
        {
            await storeService.AddCouponAsync(coupon);
        }
        catch (Exception)
        {
            Message = "保存失败，可能因为优惠码已存在。";
            return Page();
        }

        return RedirectToPage("/Admin/Coupons/Index");
    }

    public class CouponInput
    {
        [Display(Name = "优惠券名称")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "优惠码")]
        public string Code { get; set; } = string.Empty;

        [Display(Name = "满减门槛")]
        public decimal MinimumAmount { get; set; }

        [Display(Name = "优惠金额")]
        public decimal DiscountAmount { get; set; }

        [Display(Name = "开始时间")]
        public DateTime StartsAt { get; set; }

        [Display(Name = "结束时间")]
        public DateTime EndsAt { get; set; }

        [Display(Name = "立即生效")]
        public bool IsActive { get; set; } = true;
    }
}
