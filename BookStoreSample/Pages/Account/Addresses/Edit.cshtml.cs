using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Json;
using BookStoreSample.Models;
using BookStoreSample.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace BookStoreSample.Pages.Account.Addresses;

[Authorize]
public class EditModel(StoreService storeService, IHttpClientFactory httpClientFactory, IConfiguration configuration) : PageModel
{
    public bool IsEditing => Input.Id > 0;

    public string AmapWebApiKey => configuration["Amap:WebApiKey"] ?? string.Empty;

    public string AmapSecurityJsCode => configuration["Amap:SecurityJsCode"] ?? string.Empty;

    [BindProperty]
    public AddressInput Input { get; set; } = new();

    public string? ErrorMessage { get; private set; }

    public async Task<IActionResult> OnGetAsync(int id = 0)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;

        if (id > 0)
        {
            var addr = await storeService.GetAddressAsync(id, userId);
            if (addr is null) return NotFound();
            Input = new AddressInput
            {
                Id = addr.Id,
                ReceiverName = addr.ReceiverName,
                ReceiverPhone = addr.ReceiverPhone,
                Province = addr.Province,
                City = addr.City,
                District = addr.District,
                StreetAddress = addr.StreetAddress,
                IsDefault = addr.IsDefault
            };
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid) return Page();

        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var address = new ShippingAddress
        {
            Id = Input.Id,
            ReceiverName = Input.ReceiverName.Trim(),
            ReceiverPhone = Input.ReceiverPhone.Trim(),
            Province = Input.Province.Trim(),
            City = Input.City.Trim(),
            District = Input.District.Trim(),
            StreetAddress = Input.StreetAddress.Trim(),
            IsDefault = Input.IsDefault
        };

        var ok = await storeService.SaveAddressAsync(userId, address);
        if (!ok)
        {
            ErrorMessage = "地址保存失败，请检查是否超过数量上限（10个）。";
            return Page();
        }

        return RedirectToPage("Index");
    }

    public async Task<IActionResult> OnGetGeocodeAsync(string address, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(address))
            return new JsonResult(new { success = false });

        try
        {
            var client = httpClientFactory.CreateClient();
            var key = "a3b2f2d3c04fce5ae2b5e227c1ed5baf";
            var url = $"https://restapi.amap.com/v3/geocode/geo?address={Uri.EscapeDataString(address)}&key={key}";
            var response = await client.GetStringAsync(url, cancellationToken);
            using var json = JsonDocument.Parse(response);

            if (json.RootElement.TryGetProperty("status", out var status) && status.GetString() != "1")
                return new JsonResult(new { success = false });

            if (json.RootElement.TryGetProperty("geocodes", out var geocodes) && geocodes.GetArrayLength() > 0)
            {
                var first = geocodes[0];
                var location = GetJsonString(first, "location");
                if (string.IsNullOrWhiteSpace(location))
                    return new JsonResult(new { success = false });

                return new JsonResult(new
                {
                    success = true,
                    location,
                    formattedAddress = GetJsonString(first, "formatted_address"),
                    province = GetJsonString(first, "province"),
                    city = GetJsonString(first, "city"),
                    district = GetJsonString(first, "district")
                });
            }

            return new JsonResult(new { success = false });
        }
        catch
        {
            return new JsonResult(new { success = false });
        }
    }

    private static string? GetJsonString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return null;

        return property.ValueKind == JsonValueKind.String ? property.GetString() : property.ToString();
    }

    public class AddressInput
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "请填写收货人姓名")]
        [Display(Name = "收货人")]
        [MaxLength(50)]
        public string ReceiverName { get; set; } = string.Empty;

        [Required(ErrorMessage = "请填写联系电话")]
        [Display(Name = "联系电话")]
        [MaxLength(30)]
        public string ReceiverPhone { get; set; } = string.Empty;

        [Required(ErrorMessage = "请填写省份")]
        [Display(Name = "省份")]
        [MaxLength(30)]
        public string Province { get; set; } = string.Empty;

        [Required(ErrorMessage = "请填写城市")]
        [Display(Name = "城市")]
        [MaxLength(30)]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "请填写区县")]
        [Display(Name = "区县")]
        [MaxLength(30)]
        public string District { get; set; } = string.Empty;

        [Required(ErrorMessage = "请填写详细地址")]
        [Display(Name = "详细地址")]
        [MaxLength(200)]
        public string StreetAddress { get; set; } = string.Empty;

        [Display(Name = "设为默认")]
        public bool IsDefault { get; set; }
    }
}
