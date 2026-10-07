using System.Net;
using System.Net.Http.Json;
using KasiTix.Api.Models;
using KasiTix.Domain.Entities;
using KasiTix.Infrastructure.Data;
using KasiTix.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace KasiTix.Tests;

[Collection("PostgreSQL")]
public class ApiTests
{
    private readonly PostgreSqlFixture _fixture;

    public ApiTests(PostgreSqlFixture fixture)
    {
        _fixture = fixture;
    }


    [Fact]
    public async Task T1_OrderWorksEndToEnd()
    {
        // Arrange
        var (entity, ticket) = await CreatePublishedAsync(10);

        // Act
        var response = await PlaceAsync(entity.Id, ticket.Id, 2);

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var order = (await response.Content.ReadFromJsonAsync<OrderResponse>())!;
        Assert.Equal(160m, order.Total);
        var fetched = (await _fixture.Client.GetFromJsonAsync<EventResponse>($"/api/events/{entity.Id}"))!;
        Assert.Equal(8, fetched.TicketTypes.Single().Remaining);
    }

    [Fact]
    public async Task T2_OverCapacityIsProblemDetailsAndDoesNotSellTickets()
    {
        // Arrange
        var (entity, ticket) = await CreatePublishedAsync(1);

        // Act
        var response = await PlaceAsync(entity.Id, ticket.Id, 2);

        // Assert
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        var fetched = (await _fixture.Client.GetFromJsonAsync<EventResponse>($"/api/events/{entity.Id}"))!;
        Assert.Equal(1, fetched.TicketTypes.Single().Remaining);
    }

    [Fact]
    public async Task T3_ReplayingKeyReturnsOriginalOrderAndSellsOnce()
    {
        // Arrange
        var (entity, ticket) = await CreatePublishedAsync(10);
        var key = Guid.NewGuid().ToString();
        var email = $"buyer-{Guid.NewGuid():N}@example.com";

        // Act
        var first = await PlaceAsync(entity.Id, ticket.Id, 2, key, email);
        var second = await PlaceAsync(entity.Id, ticket.Id, 2, key, email);

        // Assert
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);

        // Assert
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal((await first.Content.ReadFromJsonAsync<OrderResponse>())!.Id,
            (await second.Content.ReadFromJsonAsync<OrderResponse>())!.Id);
        var fetched = (await _fixture.Client.GetFromJsonAsync<EventResponse>($"/api/events/{entity.Id}"))!;
        Assert.Equal(8, fetched.TicketTypes.Single().Remaining);
    }

    [Fact]
    public async Task T4_XminDeterministicallyCatchesConcurrentSale()
    {
        // Arrange
        var (entity, ticket) = await CreatePublishedAsync(1);
        using var firstScope = _fixture.App.Services.CreateScope();
        using var secondScope = _fixture.App.Services.CreateScope();
        var firstDb = firstScope.ServiceProvider.GetRequiredService<KasiTixDbContext>();
        var secondDb = secondScope.ServiceProvider.GetRequiredService<KasiTixDbContext>();
        var first = await firstDb.TicketTypes.SingleAsync(t => t.Id == ticket.Id);
        var second = await secondDb.TicketTypes.SingleAsync(t => t.Id == ticket.Id);
        first.Reserve(1);
        second.Reserve(1);
        await firstDb.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => secondDb.SaveChangesAsync());
        var fetched = (await _fixture.Client.GetFromJsonAsync<EventResponse>($"/api/events/{entity.Id}"))!;
        Assert.Equal(0, fetched.TicketTypes.Single().Remaining);
    }

    [Fact]
    public async Task T5_DatabaseRejectsDuplicateTicketNamesBypassingServices()
    {
        // Arrange
        var (entity, ticket) = await CreatePublishedAsync(10);
        using var scope = _fixture.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KasiTixDbContext>();
        db.TicketTypes.Add(new TicketType(entity.Id, "Direct", 1m, 1));
        await db.SaveChangesAsync();
        db.TicketTypes.Add(new TicketType(entity.Id, "Direct", 2m, 1));
        var error = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
    }

    private async Task<(EventResponse Event, TicketTypeResponse Ticket)> CreatePublishedAsync(int capacity)
    {
        var response = await _fixture.Client.PostAsJsonAsync("/api/events", new CreateEventRequest(
            $"Concert-{Guid.NewGuid():N}", "Hall", DateTime.UtcNow.AddDays(1)));

        // Assert
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        var entity = (await response.Content.ReadFromJsonAsync<EventResponse>())!;
        var ticketResponse = await _fixture.Client.PostAsJsonAsync($"/api/events/{entity.Id}/ticket-types", new CreateTicketTypeRequest("General", 80m, capacity));

        // Assert
        Assert.Equal(HttpStatusCode.Created, ticketResponse.StatusCode);
        var ticket = (await ticketResponse.Content.ReadFromJsonAsync<TicketTypeResponse>())!;

        // Assert
        Assert.Equal(HttpStatusCode.NoContent, (await _fixture.Client.PostAsync($"/api/events/{entity.Id}/publish", null)).StatusCode);
        return (entity, ticket);
    }

    private Task<HttpResponseMessage> PlaceAsync(Guid eventId, Guid ticketId, int quantity, string? key = null, string? email = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/events/{eventId}/orders")
        {
            Content = JsonContent.Create(new PlaceOrderRequest(email ?? $"buyer-{Guid.NewGuid():N}@example.com", [new(ticketId, quantity)]))
        };
        request.Headers.Add("Idempotency-Key", key ?? Guid.NewGuid().ToString());
        return _fixture.Client.SendAsync(request);
    }
}
