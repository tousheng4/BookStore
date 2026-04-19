using System.Security.Claims;
using BookStoreSample.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace BookStoreSample.Security;

public class AppUserClaimsPrincipalFactory(
    UserManager<ApplicationUser> userManager,
    RoleManager<IdentityRole> roleManager,
    IOptions<IdentityOptions> optionsAccessor)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(userManager, roleManager, optionsAccessor)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim("DisplayName", user.DisplayName));
        return identity;
    }
}
