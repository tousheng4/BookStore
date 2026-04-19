using System.ComponentModel.DataAnnotations;
using BookStoreSample.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Account;

public class RegisterModel(UserManager<ApplicationUser> userManager, SignInManager<ApplicationUser> signInManager) : PageModel
{
    [BindProperty]
    public RegisterInput Input { get; set; } = new();

    public string? Message { get; private set; }
    public string MessageType { get; private set; } = "error";

    public IActionResult OnGet()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToPage("/Index");
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.Password != Input.ConfirmPassword)
        {
            Message = "两次输入的密码不一致。";
            return Page();
        }

        var user = new ApplicationUser
        {
            UserName = Input.UserName.Trim(),
            DisplayName = Input.DisplayName.Trim()
        };

        var result = await userManager.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            Message = result.Errors.FirstOrDefault()?.Description ?? "注册失败，请稍后再试。";
            return Page();
        }

        await userManager.AddToRoleAsync(user, UserRoles.Customer);
        await signInManager.SignInAsync(user, isPersistent: false);
        return RedirectToPage("/Index");
    }

    public class RegisterInput
    {
        [Display(Name = "用户名")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "显示名称")]
        public string DisplayName { get; set; } = string.Empty;

        [Display(Name = "登录密码")]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "确认密码")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
