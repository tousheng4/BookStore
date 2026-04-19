using System.Security.Claims;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Account;

[Authorize]
public class CouponsModel(StoreService storeService) : PageModel
{
    public IReadOnlyList<CouponClaimItem> Coupons { get; private set; } = [];
    public string? Message { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadCouponsAsync();
    }

    public async Task<IActionResult> OnPostClaimAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var ok = await storeService.ClaimCouponAsync(userId, id);
        Message = ok ? "优惠券已领取。" : "领取失败，请确认优惠券是否仍可用。";

        await LoadCouponsAsync();
        return Page();
    }

    private async Task LoadCouponsAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Coupons = await storeService.GetCouponClaimItemsAsync(userId);
    }
}
