using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Admin.Products;

[Authorize(Roles = UserRoles.Admin)]
public class CreateModel(StoreService storeService) : PageModel
{
    [BindProperty]
    public BookProduct Input { get; set; } = new();

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await storeService.AddProductAsync(Input);
        return RedirectToPage("/Admin/Products/Index");
    }
}
