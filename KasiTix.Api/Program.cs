using FluentValidation;
using KasiTix.Api.Common;
using KasiTix.Api.Services;
using KasiTix.Domain.Repositories;
using KasiTix.Infrastructure.Data;
using KasiTix.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

// Keeps "Async" in action names, so CreatedAtAction(nameof(GetByIdAsync), ...) finds its route.
builder.Services.AddControllers(options =>
{
    options.SuppressAsyncSuffixInActionNames = false;
});

builder.Services.AddValidatorsFromAssemblyContaining<Program>();

builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddProblemDetails();

// TODO (Week 5):
//  - read the connection string from user-secrets, and fail loudly at startup if it's missing
//  - AddDbContext with UseNpgsql + EnableRetryOnFailure
//  - register every repository and service, each with a lifetime you can defend
builder.Configuration.AddUserSecrets<Program>(optional: true);

var connectionString =
    builder.Configuration.GetConnectionString("KasiTix");

if (string.IsNullOrWhiteSpace(connectionString))
{
    throw new InvalidOperationException(
        "The 'ConnectionStrings:KasiTix' connection string is required. Configure it in user-secrets.");
}

builder.Services.AddDbContext<KasiTixDbContext>(options =>
{
    options.UseNpgsql(
        connectionString,
        npgsqlOptions =>
        {
            npgsqlOptions.EnableRetryOnFailure();
        });
});

builder.Services.AddScoped<
    IEventRepository,
    EfEventRepository>();

builder.Services.AddScoped<
    IOrderRepository,
    EfOrderRepository>();

builder.Services.AddScoped<
    IEventService,
    EventService>();

builder.Services.AddScoped<
    IOrderService,
    OrderService>();

var app = builder.Build();

// TODO (Week 5): apply pending migrations on startup.
await using (var scope = app.Services.CreateAsyncScope())
{
    var dbContext =
        scope.ServiceProvider.GetRequiredService<KasiTixDbContext>();

    await dbContext.Database.MigrateAsync();
}

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.MapControllers();

app.Run();

// Lets KasiTix.Tests boot this app with WebApplicationFactory<Program>.
public partial class Program { }
