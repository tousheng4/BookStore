using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Admin.Coupons;

[Authorize(Roles = UserRoles.Admin)]
public class IndexModel(StoreService storeService) : PageModel
{
    public IReadOnlyList<CouponManageItem> Coupons { get; private set; } = [];
    public int TotalCount { get; private set; }
    public int CurrentPageNumber { get; private set; } = 1;
    public int PageSize { get; } = 12;
    public int TotalPages { get; private set; } = 1;
    public int StartItem => TotalCount == 0 ? 0 : (CurrentPageNumber - 1) * PageSize + 1;
    public int EndItem => Math.Min(CurrentPageNumber * PageSize, TotalCount);

    [BindProperty(SupportsGet = true)]
    public int CurrentPage { get; set; } = 1;

    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        await LoadCouponsAsync();
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        await storeService.ToggleCouponActiveAsync(id);
        Message = "状态已更新。";
        return RedirectToPage(new { CurrentPage });
    }

    private async Task LoadCouponsAsync()
    {
        var pagedResult = await storeService.GetPagedCouponsAsync(CurrentPage, PageSize);
        Coupons = pagedResult.Items;
        TotalCount = pagedResult.TotalCount;
        CurrentPageNumber = pagedResult.Page;
        TotalPages = pagedResult.TotalPages;
    }
}
