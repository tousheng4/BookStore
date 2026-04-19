using System.Security.Claims;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Account;

[Authorize]
public class NotificationsModel(StoreService storeService) : PageModel
{
    public IReadOnlyList<UserNotification> Notifications { get; private set; } = [];
    public int UnreadCount { get; private set; }

    public async Task OnGetAsync()
    {
        await LoadAsync();
    }

    public async Task<IActionResult> OnPostReadAsync(int id)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await storeService.MarkNotificationReadAsync(userId, id);
        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostReadAllAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        await storeService.MarkAllNotificationsReadAsync(userId);
        return RedirectToPage();
    }

    private async Task LoadAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        Notifications = await storeService.GetNotificationsAsync(userId);
        UnreadCount = await storeService.GetUnreadNotificationCountAsync(userId);
    }
}
