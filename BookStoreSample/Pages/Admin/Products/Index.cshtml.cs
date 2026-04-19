using System.Security.Claims;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Admin.Products;

[Authorize(Roles = UserRoles.Admin)]
public class IndexModel(StoreService storeService, IWebHostEnvironment env) : PageModel
{
    public IReadOnlyList<BookProduct> Products { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Products = await storeService.GetProductsAsync();
    }

    public async Task<IActionResult> OnPostUploadCoverAsync()
    {
        var file = Request.Form.Files.FirstOrDefault();
        if (file is null || file.Length == 0)
        {
            return new JsonResult(new { url = "" });
        }

        var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(ext))
        {
            return new JsonResult(new { url = "", error = "不支持的图片格式" });
        }

        if (file.Length > 5 * 1024 * 1024)
        {
            return new JsonResult(new { url = "", error = "图片大小不能超过 5MB" });
        }

        var uploadDir = Path.Combine(env.WebRootPath, "images", "books");
        Directory.CreateDirectory(uploadDir);

        var uniqueName = $"{Guid.NewGuid()}{ext}";
        var filePath = Path.Combine(uploadDir, uniqueName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        var url = $"/images/books/{uniqueName}";
        return new JsonResult(new { url });
    }

    public async Task<IActionResult> OnPostToggleActiveAsync(int id)
    {
        var product = await storeService.GetProductAsync(id);
        if (product is null)
        {
            return RedirectToPage();
        }

        await storeService.SetProductActiveAsync(id, !product.IsActive);
        return RedirectToPage();
    }
}
