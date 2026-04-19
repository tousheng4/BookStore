using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Admin.Inventory;

[Authorize(Roles = UserRoles.Admin)]
public class IndexModel(StoreService storeService) : PageModel
{
    public IReadOnlyList<InventoryChangeLog> Logs { get; private set; } = [];

    public async Task OnGetAsync()
    {
        Logs = await storeService.GetInventoryChangeLogsAsync();
    }
}
