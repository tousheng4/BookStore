using System.ComponentModel.DataAnnotations;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Admin.Coupons;

[Authorize(Roles = UserRoles.Admin)]
public class EditModel(StoreService storeService) : PageModel
{
    public Coupon? Coupon { get; private set; }

    [BindProperty]
    public CouponInput Input { get; set; } = new();

    public string? Message { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Coupon = await storeService.GetCouponAsync(id);
        if (Coupon is null)
        {
            Message = "未找到该优惠券。";
            return Page();
        }

        Input = new CouponInput
        {
            Name = Coupon.Name,
            Code = Coupon.Code,
            MinimumAmount = Coupon.MinimumAmount,
            DiscountAmount = Coupon.DiscountAmount,
            StartsAt = Coupon.StartsAt.ToLocalTime(),
            EndsAt = Coupon.EndsAt.ToLocalTime(),
            IsActive = Coupon.IsActive
        };
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        if (string.IsNullOrWhiteSpace(Input.Name))
        {
            Message = "请填写优惠券名称。";
            return await LoadCoupon(id);
        }
        if (string.IsNullOrWhiteSpace(Input.Code))
        {
            Message = "请填写优惠码。";
            return await LoadCoupon(id);
        }
        if (Input.MinimumAmount < 0)
        {
            Message = "满减门槛不能为负数。";
            return await LoadCoupon(id);
        }
        if (Input.DiscountAmount <= 0)
        {
            Message = "优惠金额必须大于 0。";
            return await LoadCoupon(id);
        }
        if (Input.EndsAt <= Input.StartsAt)
        {
            Message = "结束时间必须晚于开始时间。";
            return await LoadCoupon(id);
        }

        Input.Code = Input.Code.Trim().ToUpperInvariant();

        var coupon = new Coupon
        {
            Id = id,
            Name = Input.Name.Trim(),
            Code = Input.Code,
            MinimumAmount = Input.MinimumAmount,
            DiscountAmount = Input.DiscountAmount,
            IsActive = Input.IsActive,
            StartsAt = Input.StartsAt.ToUniversalTime(),
            EndsAt = Input.EndsAt.ToUniversalTime()
        };

        try
        {
            var ok = await storeService.UpdateCouponAsync(coupon);
            if (!ok)
            {
                Message = "更新失败，优惠券可能已不存在。";
                return await LoadCoupon(id);
            }
        }
        catch (Exception)
        {
            Message = "保存失败，可能因为优惠码已存在。";
            return await LoadCoupon(id);
        }

        return RedirectToPage("/Admin/Coupons/Index");
    }

    private async Task<IActionResult> LoadCoupon(int id)
    {
        Coupon = await storeService.GetCouponAsync(id);
        return Page();
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

        [Display(Name = "生效中")]
        public bool IsActive { get; set; }
    }
}
