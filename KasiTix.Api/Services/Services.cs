using FluentValidation;
using KasiTix.Api.Models;
using KasiTix.Domain.Entities;
using KasiTix.Domain.Exceptions;
using KasiTix.Domain.Repositories;   // your interfaces, in Domain

namespace KasiTix.Api.Services;

public interface IEventService
{
    Task<EventResponse> CreateAsync(CreateEventRequest request);
    Task<EventResponse> GetByIdAsync(Guid id);   // throws NotFoundException
    // TODO: AddTicketTypeAsync, PublishAsync (Tier 1); ListAsync, CancelAsync (Tier 2)
    Task<TicketTypeResponse> AddTicketTypeAsync(Guid id, CreateTicketTypeRequest request);
    Task PublishAsync(Guid id);

    // TODO (Tier 2): status-filtered event listing and transactional cancellation.
}

public interface IOrderService
{
    Task<(OrderResponse Order, bool IsReplay)> PlaceAsync(
        Guid eventId,
        string idempotencyKey,
        PlaceOrderRequest request);

    // TODO (Tier 2): order listing with keyset pagination.
}

// TODO: write EventService yourself.
public class EventService : IEventService
{
    private readonly IEventRepository _eventRepository;
    private readonly IValidator<CreateEventRequest> _eventValidator;
    private readonly IValidator<CreateTicketTypeRequest> _ticketTypeValidator;

    public EventService(
        IEventRepository eventRepository,
        IValidator<CreateEventRequest> eventValidator,
        IValidator<CreateTicketTypeRequest> ticketTypeValidator)
    {
        _eventRepository = eventRepository;
        _eventValidator = eventValidator;
        _ticketTypeValidator = ticketTypeValidator;
    }

    public async Task<EventResponse> CreateAsync(
        CreateEventRequest request)
    {
        await _eventValidator.ValidateAndThrowAsync(request);

        var eventEntity =
            new Event(
                request.Name,
                request.Venue,
                request.StartsAt);

        await _eventRepository.AddAsync(eventEntity);
        await _eventRepository.SaveChangesAsync();

        return EventResponse.FromEntity(eventEntity);
    }

    public async Task<EventResponse> GetByIdAsync(Guid id)
    {
        var eventEntity =
            await LoadAsync(id, tracked: false);

        return EventResponse.FromEntity(eventEntity);
    }

    public async Task<TicketTypeResponse> AddTicketTypeAsync(
        Guid id,
        CreateTicketTypeRequest request)
    {
        await _ticketTypeValidator.ValidateAndThrowAsync(request);

        var eventEntity =
            await LoadAsync(id, tracked: true);

        var ticketType =
            eventEntity.AddTicketType(
                request.Name,
                request.Price,
                request.Capacity);

        await _eventRepository.SaveChangesAsync();

        return TicketTypeResponse.FromEntity(ticketType);
    }

    public async Task PublishAsync(Guid id)
    {
        var eventEntity =
            await LoadAsync(id, tracked: true);

        eventEntity.Publish();

        await _eventRepository.SaveChangesAsync();
    }

    private async Task<Event> LoadAsync(Guid id, bool tracked)
    {
        var eventEntity =
            await _eventRepository.GetByIdAsync(id, tracked);

        if (eventEntity is null)
        {
            throw new NotFoundException(
                $"Event '{id}' was not found.");
        }

        return eventEntity;
    }
}

public class OrderService : IOrderService
{
    private readonly IEventRepository _eventRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IValidator<PlaceOrderRequest> _validator;

    public OrderService(
        IEventRepository eventRepository,
        IOrderRepository orderRepository,
        IValidator<PlaceOrderRequest> validator)
    {
        _eventRepository = eventRepository;
        _orderRepository = orderRepository;
        _validator = validator;
    }

    public async Task<(OrderResponse Order, bool IsReplay)> PlaceAsync(
        Guid eventId,
        string idempotencyKey,
        PlaceOrderRequest request)
    {
        await _validator.ValidateAndThrowAsync(request);

        // TODO: in this order. Think about each step before you write it.
        //
        // 1. Replay? Look the key up. If an order already has it, return that
        //    order with IsReplay = true. (What happens if two requests with the
        //    same NEW key arrive at the same moment? Which constraint saves you?)
        //
        // 2. Load the event WITH its ticket types, TRACKED: you're about to
        //    change Sold. Missing -> 404. Not Published, or already started -> 422 (R5).
        //
        // 3. R9: does this buyer already hold a Confirmed order for this event?
        //    -> 409. Ask the database. Don't load every order into memory.
        //
        // 4. Create the Order. For each requested line, find the ticket type on
        //    the event (an id that isn't on this event: 404 or 400? Decide),
        //    reserve the tickets, and add the line at today's price.
        //
        // 5. Save ONCE. If xmin catches a concurrent sale, let the exception
        //    surface: your exception handler turns it into a 409.

        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException(
                "An idempotency key is required.",
                nameof(idempotencyKey));
        }

        var existingOrder =
            await _orderRepository.GetByIdempotencyKeyAsync(
                idempotencyKey);

        if (existingOrder is not null)
        {
            var existingEvent =
                await LoadAsync(existingOrder.EventId, tracked: false);

            var existingResponse =
                OrderResponse.FromEntity(existingOrder, existingEvent);

            return (existingResponse, true);
        }

        var eventEntity =
            await LoadAsync(eventId, tracked: true);

        if (eventEntity.Status != EventStatus.Published ||
            eventEntity.StartsAt <= DateTime.UtcNow)
        {
            throw new UnprocessableEntityException(
                "Orders require a published event that has not started.");
        }

        var buyerHasOrder =
            await _orderRepository.HasConfirmedOrderAsync(
                eventId,
                request.BuyerEmail);

        if (buyerHasOrder)
        {
            throw new ConflictException(
                "This buyer already has a confirmed order for this event.");
        }

        var order =
            new Order(
                eventId,
                request.BuyerEmail,
                idempotencyKey);

        var ticketTypes =
            eventEntity.TicketTypes.ToDictionary(ticketType => ticketType.Id);

        foreach (var line in request.Lines)
        {
            if (!ticketTypes.TryGetValue(line.TicketTypeId, out var ticketType))
            {
                throw new NotFoundException(
                    $"Ticket type '{line.TicketTypeId}' was not found on this event.");
            }

            // Reserve once, then record the purchase price on the line.
            ticketType.Reserve(line.Quantity);
            order.AddLine(ticketType, line.Quantity);
        }

        await _orderRepository.AddAsync(order);
        await _orderRepository.SaveChangesAsync();

        var response =
            OrderResponse.FromEntity(order, eventEntity);

        return (response, false);
    }

    private async Task<Event> LoadAsync(Guid id, bool tracked)
    {
        var eventEntity =
            await _eventRepository.GetByIdAsync(id, tracked);

        if (eventEntity is null)
        {
            throw new NotFoundException(
                $"Event '{id}' was not found.");
        }

        return eventEntity;
    }
}
