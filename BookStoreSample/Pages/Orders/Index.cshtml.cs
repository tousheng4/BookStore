using System.Security.Claims;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Orders;

[Authorize]
public class IndexModel(StoreService storeService) : PageModel
{
    public IReadOnlyList<Order> Orders { get; private set; } = [];
    public string? Message { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadOrdersAsync();
    }

    public async Task<IActionResult> OnPostCancelAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var isAdmin = User.IsInRole(UserRoles.Admin);

        var ok = await storeService.CancelOrderAsync(id, userId, isAdmin, userId);
        Message = ok ? "订单已取消，库存已回滚。" : "当前订单不能取消。";

        await LoadOrdersAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostPayAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (User.IsInRole(UserRoles.Admin))
        {
            return Forbid();
        }

        var ok = await storeService.PayOrderAsync(id, userId);
        Message = ok ? "支付成功，订单已进入待发货流程。" : "当前订单不能支付。";

        await LoadOrdersAsync();
        return Page();
    }

    public async Task<IActionResult> OnPostConfirmReceiptAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (User.IsInRole(UserRoles.Admin))
        {
            return Forbid();
        }

        var ok = await storeService.ConfirmReceiptAsync(id, userId);
        Message = ok ? "已确认收货，感谢你的购买。" : "当前订单不能确认收货。";

        await LoadOrdersAsync();
        return Page();
    }

    private async Task LoadOrdersAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Orders = await storeService.GetOrdersAsync(userId, User.IsInRole(UserRoles.Admin));
    }
}
