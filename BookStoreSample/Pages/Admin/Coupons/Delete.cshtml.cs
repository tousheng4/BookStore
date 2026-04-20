using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Admin.Coupons;

[Authorize(Roles = UserRoles.Admin)]
public class DeleteModel(StoreService storeService) : PageModel
{
    public Coupon? Coupon { get; private set; }
    public string? Message { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Coupon = await storeService.GetCouponAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var ok = await storeService.DeleteCouponAsync(id);
        if (!ok)
        {
            Coupon = await storeService.GetCouponAsync(id);
            Message = "删除失败，优惠券可能已不存在。";
            return Page();
        }

        return RedirectToPage("/Admin/Coupons/Index");
    }
}
