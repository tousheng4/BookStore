using System.Security.Claims;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Account;

[Authorize]
public class WishlistModel(StoreService storeService) : PageModel
{
    public IReadOnlyList<WishlistItem> Items { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Items = await storeService.GetWishlistAsync(userId);
    }

    public async Task<IActionResult> OnPostAddToCartAsync(int productId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await storeService.AddToCartAsync(userId, productId, 1);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostRemoveAsync(int productId)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await storeService.RemoveFromWishlistAsync(userId, productId);
        return RedirectToPage();
    }
}
