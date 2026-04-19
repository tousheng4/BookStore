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
    SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty]
    public ProfileInput Input { get; set; } = new();

    public string? Message { get; private set; }
    public string MessageType { get; private set; } = "success";

    public async Task<IActionResult> OnGetAsync()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToPage("/Account/Login");
        }

        Input = new ProfileInput
        {
            DisplayName = user.DisplayName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber ?? string.Empty
        };

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
            Message = "昵称不能为空。";
            MessageType = "error";
            return Page();
        }

        user.DisplayName = Input.DisplayName.Trim();
        user.Email = string.IsNullOrWhiteSpace(Input.Email) ? null : Input.Email.Trim();
        user.PhoneNumber = string.IsNullOrWhiteSpace(Input.PhoneNumber) ? null : Input.PhoneNumber.Trim();

        var result = await userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            Message = result.Errors.FirstOrDefault()?.Description ?? "资料保存失败，请稍后再试。";
            MessageType = "error";
            return Page();
        }

        await signInManager.RefreshSignInAsync(user);
        Message = "个人资料已保存。";
        MessageType = "success";
        return Page();
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
    }
}
