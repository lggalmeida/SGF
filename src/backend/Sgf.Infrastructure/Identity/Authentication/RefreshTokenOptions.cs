namespace Sgf.Infrastructure.Identity.Authentication;

public sealed class RefreshTokenOptions
{
    public const string SectionName = "RefreshToken";

    public int Days { get; set; } = 7;

    public string CookieName { get; set; } = "sgf_refresh_token";
}
