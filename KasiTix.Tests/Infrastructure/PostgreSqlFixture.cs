using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Xunit;

namespace KasiTix.Tests.Infrastructure;

[CollectionDefinition("PostgreSQL")]
public sealed class PostgreSqlCollection : ICollectionFixture<PostgreSqlFixture> { }
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly string _databaseName = $"kasitix_tests_{Guid.NewGuid():N}";
    private string? _adminConnectionString;
    private bool _databaseCreated;
    public WebApplicationFactory<Program> App { get; private set; } = null!;
    public HttpClient Client { get; private set; } = null!;
    public async Task InitializeAsync()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .AddUserSecrets<PostgreSqlFixture>(optional: true)
            .AddEnvironmentVariables()
            .Build();
        var configuredConnection = config.GetConnectionString("PostgresAdmin")
            ?? config.GetConnectionString("KasiTix");
        if (string.IsNullOrWhiteSpace(configuredConnection))
            throw new InvalidOperationException(
                "Configure ConnectionStrings:PostgresAdmin in KasiTix.Tests user-secrets, or configure the API's ConnectionStrings:KasiTix. The PostgreSQL user must have CREATEDB permission.");

        // Use a maintenance database only; never migrate or clear the development database.
        var connection = new NpgsqlConnectionStringBuilder(configuredConnection)
        {
            Database = "postgres",
            Pooling = false
        };
        _adminConnectionString = connection.ConnectionString;
        await using (var admin = new NpgsqlConnection(_adminConnectionString))
        {
            await admin.OpenAsync();
            await using var command = new NpgsqlCommand($"CREATE DATABASE \"{_databaseName}\"", admin);
            await command.ExecuteNonQueryAsync();
            _databaseCreated = true;
        }
        connection.Database = _databaseName;
        var testConnectionString = connection.ConnectionString;
        try
        {
            App = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, settings) => settings.AddInMemoryCollection(
                    new Dictionary<string, string?> { ["ConnectionStrings:KasiTix"] = testConnectionString }));
            });
            Client = App.CreateClient();
        }
        catch
        {
            await DisposeAsync();
            throw;
        }
    }
    public async Task DisposeAsync()
    {
        Client?.Dispose();
        if (App is not null)
        {
            await App.DisposeAsync();
            App = null!;
        }
        if (!_databaseCreated) return;
        await using var admin = new NpgsqlConnection(_adminConnectionString);
        await admin.OpenAsync();
        // Only the GUID-named database created by this fixture is eligible for cleanup.
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_databaseName}\" WITH (FORCE)", admin);
        await command.ExecuteNonQueryAsync();
        _databaseCreated = false;
    }
}
