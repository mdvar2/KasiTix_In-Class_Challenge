using KasiTix.Domain.Entities;

namespace KasiTix.Api.Models;

// -- Requests --
public record CreateEventRequest(
    string Name,
    string Venue,
    DateTime StartsAt);

public record CreateTicketTypeRequest(
    string Name,
    decimal Price,
    int Capacity);

public record PlaceOrderRequest(
    string BuyerEmail,
    IReadOnlyList<OrderLineRequest> Lines);

public record OrderLineRequest(
    Guid TicketTypeId,
    int Quantity);

// -- Responses --
public record TicketTypeResponse(
    Guid Id,
    string Name,
    decimal Price,
    int Capacity,
    int Remaining)
{
    public static TicketTypeResponse FromEntity(TicketType ticketType)
    {
        return new TicketTypeResponse(
            ticketType.Id,
            ticketType.Name,
            ticketType.Price,
            ticketType.Capacity,
            ticketType.Remaining);
    }
}

public record EventResponse(
    Guid Id,
    string Name,
    string Venue,
    DateTime StartsAt,
    string Status,
    IReadOnlyList<TicketTypeResponse> TicketTypes)
{
    public static EventResponse FromEntity(Event eventEntity)
    {
        var ticketTypes = eventEntity.TicketTypes
            .Select(TicketTypeResponse.FromEntity)
            .ToList();

        return new EventResponse(
            eventEntity.Id,
            eventEntity.Name,
            eventEntity.Venue,
            eventEntity.StartsAt,
            eventEntity.Status.ToString(),
            ticketTypes);
    }
}

public record OrderLineResponse(
    Guid TicketTypeId,
    string TicketTypeName,
    int Quantity,
    decimal UnitPrice);

// TODO: add a FromEntity for OrderResponse. OrderLine only holds a
// TicketTypeId, so where does TicketTypeName come from?
public record OrderResponse(
    Guid Id,
    Guid EventId,
    string BuyerEmail,
    string Status,
    DateTime CreatedAt,
    IReadOnlyList<OrderLineResponse> Lines,
    decimal Total)
{
    public static OrderResponse FromEntity(Order order, Event eventEntity)
    {
        if (order.EventId != eventEntity.Id)
        {
            throw new ArgumentException(
                "The event must belong to the order.",
                nameof(eventEntity));
        }

        var ticketTypeNames = eventEntity.TicketTypes
            .ToDictionary(ticketType => ticketType.Id, ticketType => ticketType.Name);

        var lines = order.Lines
            .Select(line => new OrderLineResponse(
                line.TicketTypeId,
                ticketTypeNames[line.TicketTypeId],
                line.Quantity,
                line.UnitPrice))
            .ToList();

        return new OrderResponse(
            order.Id,
            order.EventId,
            order.BuyerEmail,
            order.Status.ToString(),
            order.CreatedAt,
            lines,
            order.Total);
    }
}

// AIP-158: an empty NextPageToken is the only end-of-list signal (Tier 2).
public record PagedResponse<T>(
    IReadOnlyList<T> Items,
    string NextPageToken);
