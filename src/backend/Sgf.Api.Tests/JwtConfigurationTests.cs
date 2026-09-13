using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Sgf.Api.Tests;

public sealed class JwtConfigurationTests
{
    [Theory]
    [InlineData("Testing", "")]
    [InlineData("Testing", "short")]
    [InlineData("Development", "")]
    [InlineData("Development", "short")]
    [InlineData("Production", "")]
    [InlineData("Production", "short")]
    public void Startup_RequiresExplicitValidKey(string environment, string key)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.UseSetting("Jwt:SigningKey", key);
        });
        var exception = Assert.Throws<InvalidOperationException>(() => factory.CreateClient());
        Assert.Contains("JWT signing key must be configured", exception.Message);
    }
}
