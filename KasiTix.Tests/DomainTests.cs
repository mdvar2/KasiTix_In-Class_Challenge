using KasiTix.Api.Models;
using KasiTix.Api.Validation;
using KasiTix.Domain.Entities;
using KasiTix.Domain.Exceptions;
using Xunit;

namespace KasiTix.Tests;

public class DomainTests
{
    [Fact]
    public void T0_Reserve_WhenQuantityExceedsRemaining_ThrowsConflictAndLeavesSoldUnchanged()
    {
        // Arrange
        var ticketType =
            new TicketType(Guid.NewGuid(), "General", 80m, 2);

        ticketType.Reserve(1);

        // Act
        Assert.Throws<ConflictException>(
            () => ticketType.Reserve(2));

        // Assert
        Assert.Equal(1, ticketType.Sold);
    }

    [Fact]
    public void Publish_WhenEventHasNoTicketsOrIsCancelled_ThrowsUnprocessableEntityException()
    {
        // Arrange
        var eventEntity =
            new Event("Concert", "Hall", DateTime.UtcNow.AddDays(1));

        // Act and Assert
        Assert.Throws<UnprocessableEntityException>(eventEntity.Publish);

        eventEntity.AddTicketType("General", 80m, 2);
        eventEntity.Publish();
        eventEntity.Publish();
        eventEntity.Cancel();
        eventEntity.Cancel();

        Assert.Throws<UnprocessableEntityException>(eventEntity.Publish);
        Assert.Throws<UnprocessableEntityException>(
            () => eventEntity.AddTicketType("VIP", 100m, 2));
    }

    [Fact]
    public void AddLine_WhenTicketIsValid_RecordsPriceAndRejectsDuplicateOrOtherEvent()
    {
        // Arrange
        var eventId = Guid.NewGuid();

        var ticketType =
            new TicketType(eventId, "General", 80m, 10);

        var order =
            new Order(eventId, "buyer@example.com", "key");

        // Act
        order.AddLine(ticketType, 2);

        // Assert
        Assert.Equal(160m, order.Total);
        Assert.Equal(80m, order.Lines.Single().UnitPrice);
        Assert.Equal(0, ticketType.Sold);
        Assert.Throws<ArgumentException>(
            () => order.AddLine(ticketType, 1));
        Assert.Throws<ArgumentException>(
            () => order.AddLine(
                new TicketType(Guid.NewGuid(), "Other", 1m, 1),
                1));
    }

    [Fact]
    public void Release_WhenQuantityExceedsSold_ThrowsConflictAndLeavesSoldUnchanged()
    {
        // Arrange
        var ticketType =
            new TicketType(Guid.NewGuid(), "General", 1m, 3);

        ticketType.Reserve(2);

        // Act and Assert
        Assert.Throws<ConflictException>(
            () => ticketType.Release(3));
        Assert.Equal(2, ticketType.Sold);

        ticketType.Release(2);

        Assert.Equal(0, ticketType.Sold);
    }

    [Fact]
    public void Validate_WhenOrderHasDuplicatesInvalidQuantitiesOrNullLines_ReturnsInvalid()
    {
        // Arrange
        var validator = new PlaceOrderRequestValidator();
        var ticketTypeId = Guid.NewGuid();

        var duplicateRequest =
            new PlaceOrderRequest(
                "buyer@example.com",
                [new(ticketTypeId, 1), new(ticketTypeId, 1)]);

        var invalidQuantityRequest =
            new PlaceOrderRequest(
                "buyer@example.com",
                [new(ticketTypeId, 11)]);

        var missingLinesRequest =
            new PlaceOrderRequest("buyer@example.com", null!);

        var nullLineRequest =
            new PlaceOrderRequest("buyer@example.com", [null!]);

        // Act
        var duplicateResult = validator.Validate(duplicateRequest);
        var quantityResult = validator.Validate(invalidQuantityRequest);
        var missingLinesResult = validator.Validate(missingLinesRequest);
        var nullLineResult = validator.Validate(nullLineRequest);

        // Assert
        Assert.False(duplicateResult.IsValid);
        Assert.False(quantityResult.IsValid);
        Assert.False(missingLinesResult.IsValid);
        Assert.False(nullLineResult.IsValid);
    }
}
