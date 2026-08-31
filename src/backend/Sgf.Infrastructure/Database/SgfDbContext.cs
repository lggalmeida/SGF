using Microsoft.EntityFrameworkCore;

namespace Sgf.Infrastructure.Database;

public sealed class SgfDbContext(DbContextOptions<SgfDbContext> options) : DbContext(options)
{
}
