using Microsoft.AspNetCore.Identity;
using Sgf.Domain.Companies;

namespace Sgf.Infrastructure.Identity;

public sealed class ApplicationUser : IdentityUser
{
    public string Name { get; set; } = string.Empty;

    public ICollection<Membership> Memberships { get; } = new List<Membership>();
}
