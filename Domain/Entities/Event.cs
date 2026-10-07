using KasiTix.Domain.Exceptions;

namespace KasiTix.Domain.Entities;

public enum EventStatus
{
    Draft,
    Published,
    Cancelled
}

public class Event
{
    private readonly List<TicketType> _ticketTypes = new();

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public string Venue { get; private set; } = null!;

    public DateTime StartsAt { get; private set; }

    public EventStatus Status { get; private set; }

    public IReadOnlyCollection<TicketType> TicketTypes => _ticketTypes.AsReadOnly();

    // TODO (Week 5): EF Core has to load events whose StartsAt is already in
    // the past. If EF Core calls the constructor below to do that, it throws.
    // What does this class need so EF Core never calls it?
    private Event()
    {
    }

    public Event(
        string name,
        string venue,
        DateTime startsAt)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 120)
        {
            throw new ArgumentException(
                "Name is required and must be 120 characters or fewer.",
                nameof(name));
        }

        if (string.IsNullOrWhiteSpace(venue))
        {
            throw new ArgumentException(
                "Venue is required.",
                nameof(venue));
        }

        if (startsAt <= DateTime.UtcNow)
        {
            throw new ArgumentException(
                "An event must start in the future.",
                nameof(startsAt));
        }

        Id = Guid.NewGuid();
        Name = name;
        Venue = venue;
        StartsAt = startsAt;
        Status = EventStatus.Draft;
    }

    public TicketType AddTicketType(
        string name,
        decimal price,
        int capacity)
    {
        if (Status != EventStatus.Draft)
        {
            throw new UnprocessableEntityException(
                "Ticket types can only be added while the event is in Draft.");
        }

        if (_ticketTypes.Any(ticketType => ticketType.Name == name))
        {
            throw new ConflictException(
                $"This event already has a ticket type called '{name}'.");
        }

        var ticketType = new TicketType(Id, name, price, capacity);
        _ticketTypes.Add(ticketType);
        return ticketType;
    }

    // TODO (R2, R3): Draft -> Published, only if there's at least one ticket type.
    // Publishing an event that's already Published changes nothing (the endpoint still
    // returns 204).
    // Publishing a Cancelled event -> UnprocessableEntityException.
    public void Publish()
    {
        if (Status == EventStatus.Published)
        {
            return;
        }
        if (Status == EventStatus.Cancelled || _ticketTypes.Count == 0)
        {
            throw new UnprocessableEntityException(
                "Only a draft event with ticket types can be published.");
        }

        Status = EventStatus.Published;
    }

    // TODO (R2): Draft or Published -> Cancelled. Cancelling twice changes nothing.
    // Cancelling the orders and releasing their tickets is NOT this method's job. Why not?
    public void Cancel()
    {
        Status = EventStatus.Cancelled;
    }
}
