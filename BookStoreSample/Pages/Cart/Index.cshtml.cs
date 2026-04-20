using System.Security.Claims;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Cart;

[Authorize]
public class IndexModel(StoreService storeService) : PageModel
{
    public IReadOnlyList<CartItem> Items { get; private set; } = [];
    public OrderBoosterResult Booster { get; private set; } = new(null, 0m, false, []);
    public decimal TotalAmount => Items.Sum(item => item.LineTotal);

    [TempData]
    public string? Message { get; set; }

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Items = await storeService.GetCartAsync(userId);
        Booster = await storeService.GetOrderBoosterAsync(userId, TotalAmount, Items.Select(item => item.ProductId));
    }

    public async Task<IActionResult> OnPostUpdateAsync(int productId, int quantity)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await storeService.UpdateCartItemAsync(userId, productId, quantity);
        Message = "购物车已更新。";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync(int productId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await storeService.RemoveCartItemAsync(userId, productId);
        Message = "商品已从购物车移除。";
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostAddBoostAsync(int productId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var success = await storeService.AddToCartAsync(userId, productId, 1);
        Message = success ? "已加入凑单图书。" : "加入失败，可能库存不足或图书已下架。";
        return RedirectToPage();
    }
}
