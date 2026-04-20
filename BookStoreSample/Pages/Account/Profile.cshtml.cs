using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BookStoreSample.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Account;

[Authorize]
public class ProfileModel(
    UserManager<ApplicationUser> userManager,
    SignInManager<ApplicationUser> signInManager,
    IWebHostEnvironment environment) : PageModel
{
    private const long MaxAvatarSize = 2 * 1024 * 1024;

    [BindProperty]
    public ProfileInput Input { get; set; } = new();

    public string CurrentAvatarUrl { get; private set; } = string.Empty;
    public string? Message { get; private set; }
    public string MessageType { get; private set; } = "success";

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToPage("/Account/Login");
        }

        LoadInput(user);
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToPage("/Account/Login");
        }

        if (string.IsNullOrWhiteSpace(Input.DisplayName))
        {
            LoadInput(user);
            Message = "昵称不能为空。";
            MessageType = "error";
            return Page();
        }

        var avatarUrl = await SaveAvatarAsync(Input.Avatar);
        if (avatarUrl is null)
        {
            LoadInput(user);
            Message = "头像上传失败，请选择 2MB 以内的 JPG、PNG、WEBP 或 GIF 图片。";
            MessageType = "error";
            return Page();
        }

        user.DisplayName = Input.DisplayName.Trim();
        user.Email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(Input.PhoneNumber) ? null : Input.PhoneNumber.Trim();
        if (!string.IsNullOrWhiteSpace(avatarUrl))
        {
            user.AvatarUrl = avatarUrl;
        }

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            LoadInput(user);
            Message = result.Errors.FirstOrDefault()?.Description ?? "资料保存失败，请稍后再试。";
            MessageType = "error";
            return Page();
        }

        await signInManager.RefreshSignInAsync(user);
        LoadInput(user);
        Message = "个人资料已保存。";
        MessageType = "success";
        return Page();
    }

    private void LoadInput(ApplicationUser user)
    {
        CurrentAvatarUrl = user.AvatarUrl;
        Input = new ProfileInput
        {
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty
        };
    }

    private async Task<string?> SaveAvatarAsync(IFormFile? avatar)
    {
        if (avatar is null || avatar.Length == 0)
        {
            return string.Empty;
        }

        if (avatar.Length > MaxAvatarSize ||
            !avatar.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var extension = Path.GetExtension(avatar.FileName).ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".webp" or ".gif"))
        {
            return null;
        }

        var relativeDirectory = Path.Combine("uploads", "avatars");
        var absoluteDirectory = Path.Combine(environment.WebRootPath, relativeDirectory);
        Directory.CreateDirectory(absoluteDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var absolutePath = Path.Combine(absoluteDirectory, fileName);
        await using var stream = System.IO.File.Create(absolutePath);
        await avatar.CopyToAsync(stream);

        return "/" + relativeDirectory.Replace('\\', '/') + "/" + fileName;
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return string.IsNullOrWhiteSpace(userId)
            ? null
            : await userManager.FindByIdAsync(userId);
    }

    public class ProfileInput
    {
        [Display(Name = "昵称")]
        [StringLength(40)]
        public string DisplayName { get; set; } = string.Empty;

        [Display(Name = "邮箱")]
        [EmailAddress]
        [StringLength(120)]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "手机号")]
        [Phone]
        [StringLength(30)]
        public string PhoneNumber { get; set; } = string.Empty;

        [Display(Name = "头像")]
        public IFormFile? Avatar { get; set; }
    }
}
