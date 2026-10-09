using Microsoft.AspNetCore.Identity;

namespace LogAnalyzer.Infrastructure.Accounts;

/// <summary>An account. The user name is the email address; ids are UUIDs so they reveal nothing about how many accounts exist.</summary>
public sealed class AppUser : IdentityUser<Guid>
{
}
