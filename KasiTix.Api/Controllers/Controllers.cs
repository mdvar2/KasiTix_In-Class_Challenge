using KasiTix.Api.Models;
using KasiTix.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace KasiTix.Api.Controllers;

[ApiController]
[Route("api/events")]
public class EventsController : ControllerBase
{
    private readonly IEventService _eventService;

    public EventsController(IEventService eventService)
    {
        _eventService = eventService;
    }

    /// <summary>
    /// Creates an event in Draft.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(
        typeof(EventResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<EventResponse>> CreateAsync(CreateEventRequest request)
    {
        var created =
            await _eventService.CreateAsync(request);
        return CreatedAtAction(nameof(GetByIdAsync), new { id = created.Id }, created);
    }

    /// <summary>
    /// Gets one event with its ticket types and what's left of each.
    /// </summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(
        typeof(EventResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EventResponse>> GetByIdAsync(Guid id)
    {
        var response =
            await _eventService.GetByIdAsync(id);

        return Ok(response);
    }

    /// <summary>
    /// Adds a ticket type to a draft event.
    /// </summary>
    // TODO: POST /api/events/{id}/ticket-types   (endpoint 3: 201, 400, 404, 409, 422)
    [HttpPost("{id:guid}/ticket-types")]
    [ProducesResponseType(
        typeof(TicketTypeResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status409Conflict)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<TicketTypeResponse>> AddTicketTypeAsync(
        Guid id, CreateTicketTypeRequest request)
    {
        var created =
            await _eventService.AddTicketTypeAsync(id, request);
        return CreatedAtAction(nameof(GetByIdAsync), new { id }, created);
    }

    /// <summary>
    /// Publishes a draft event.
    /// </summary>
    // TODO: POST /api/events/{id}/publish        (endpoint 4: 204, 404, 422)
    [HttpPost("{id:guid}/publish")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> PublishAsync(Guid id)
    {
        await _eventService.PublishAsync(id);

        return NoContent();
    }
    // Every status code goes on the action as [ProducesResponseType].
}

[ApiController]
[Route("api/events/{eventId:guid}/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    /// <summary>
    /// Places an order. Safe to retry with the same Idempotency-Key.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(
        typeof(OrderResponse),
        StatusCodes.Status201Created)]
    [ProducesResponseType(
        typeof(OrderResponse),
        StatusCodes.Status200OK)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status400BadRequest)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status404NotFound)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status409Conflict)]
    [ProducesResponseType(
        typeof(ProblemDetails),
        StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<OrderResponse>> PlaceAsync(
        Guid eventId,
        [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey,
        PlaceOrderRequest request)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            throw new ArgumentException("The Idempotency-Key header is required.", nameof(idempotencyKey));
        }
        var (order, isReplay) =
            await _orderService.PlaceAsync(eventId, idempotencyKey, request);

        // TODO: a replay returns 200 with the original order; a new order returns 201.
        return isReplay
            ? Ok(order)
            : StatusCode(StatusCodes.Status201Created, order);
    }
}
