using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Account.Addresses;

[Authorize]
public class IndexModel(StoreService storeService) : PageModel
{
    public IReadOnlyList<ShippingAddress> Addresses { get; private set; } = [];

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Addresses = await storeService.GetAddressesAsync(userId);
    }

    public async Task<IActionResult> OnPostDeleteAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await storeService.DeleteAddressAsync(id, userId);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostSetDefaultAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await storeService.SetDefaultAddressAsync(id, userId);
        return RedirectToPage();
    }
}
