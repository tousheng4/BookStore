using System.Security.Claims;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Account;

[Authorize]
public class MembershipModel(StoreService storeService) : PageModel
{
    public MemberCenterResult Member { get; private set; } = new(
        "普通会员",
        0,
        0,
        null,
        300,
        0,
        "银卡会员",
        [],
        []);

    public async Task OnGetAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Member = await storeService.GetMemberCenterAsync(userId);
    }
}
