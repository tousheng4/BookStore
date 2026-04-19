using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Admin.Products;

[Authorize(Roles = UserRoles.Admin)]
public class DeleteModel(StoreService storeService) : PageModel
{
    public BookProduct? Product { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id)
    {
        Product = await storeService.GetProductAsync(id);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync(int id)
    {
        await storeService.DeleteProductAsync(id);
        return RedirectToPage("/Admin/Products/Index");
    }
}
