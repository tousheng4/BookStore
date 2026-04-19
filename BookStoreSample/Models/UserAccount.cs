namespace BookStoreSample.Models;

public class UserAccount
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Role { get; set; } = UserRoles.Customer;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
}
