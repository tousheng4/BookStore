using System.ComponentModel.DataAnnotations;
using BookStoreSample.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Account;

public class LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager) : PageModel
{
    [BindProperty]
    public LoginInput Input { get; set; } = new();

    public string? Message { get; private set; }

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
        var user = await userManager.FindByNameAsync(Input.UserName.Trim());
        if (user is null)
        {
            Message = "用户名或密码错误。";
            return Page();
        }

        var result = await signInManager.PasswordSignInAsync(user, Input.Password, false, false);
        if (!result.Succeeded)
        {
            Message = "用户名或密码错误。";
            return Page();
        }

        return RedirectToPage("/Index");
    }

    public class LoginInput
    {
        [Display(Name = "用户名")]
        public string UserName { get; set; } = string.Empty;

        [Display(Name = "密码")]
        public string Password { get; set; } = string.Empty;
    }
}
