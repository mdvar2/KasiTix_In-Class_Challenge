using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
namespace KasiTix.Infrastructure.Data;
// Migration generation only; the API reads its actual connection string from user-secrets.
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<KasiTixDbContext>
{
    public KasiTixDbContext CreateDbContext(string[] args) => new(
        new DbContextOptionsBuilder<KasiTixDbContext>()
            .UseNpgsql("Host=localhost;Database=kasitix_design;Username=postgres")
            .Options);
}
