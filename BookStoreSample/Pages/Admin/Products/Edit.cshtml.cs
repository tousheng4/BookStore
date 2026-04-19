using System.Security.Claims;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Admin.Products;

[Authorize(Roles = UserRoles.Admin)]
public class EditModel(StoreService storeService) : PageModel
{
    [BindProperty]
    public BookProduct Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(int id)
    {
        var product = await storeService.GetProductAsync(id);
        if (product is null)
        {
            return RedirectToPage("/Admin/Products/Index");
        }

        Input = product;
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "admin";
        await storeService.UpdateProductAsync(Input, userId);
        return RedirectToPage("/Admin/Products/Index");
    }
}
