using KasiTix.Domain.Entities;
using KasiTix.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace KasiTix.Tests;

public class DatabaseModelTests
{
    private static KasiTixDbContext CreateContext()
    {
        var options =
            new DbContextOptionsBuilder<KasiTixDbContext>()
                .UseNpgsql("Host=localhost;Database=unused;Username=unused")
                .Options;

        return new KasiTixDbContext(options);
    }

    [Fact]
    public void MigrationContainsRequiredConstraintsAndMatchesModel()
    {
        // Arrange
        using var dbContext = CreateContext();

        // Act
        var script = dbContext.GetService<IMigrator>().GenerateScript();

        // Assert
        Assert.Contains("CK_TicketTypes_Sold", script);
        Assert.Contains("CK_TicketTypes_Price", script);
        Assert.Contains("CK_OrderLines_Quantity", script);
        Assert.Contains("WHERE \"Status\" = 0", script);
        Assert.False(dbContext.Database.HasPendingModelChanges());
        Assert.True(dbContext.Model.FindEntityType(typeof(TicketType))!.FindProperty(nameof(TicketType.Version))!.IsConcurrencyToken);
    }
}
