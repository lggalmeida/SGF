using Microsoft.AspNetCore.Identity;
using Sgf.Domain.Companies;
using Sgf.Infrastructure.Identity.Authentication;

namespace Sgf.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string Name { get; set; } = string.Empty;

    public ICollection<Membership> Memberships { get; } = new List<Membership>();

    public ICollection<RefreshToken> RefreshTokens { get; } = new List<RefreshToken>();
}

