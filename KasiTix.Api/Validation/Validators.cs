using FluentValidation;
using KasiTix.Api.Models;

namespace KasiTix.Api.Validation;

public class CreateEventRequestValidator
    : AbstractValidator<CreateEventRequest>
{
    public CreateEventRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(120);

        RuleFor(request => request.Venue)
            .NotEmpty();

        // TODO (R1): StartsAt must be in the future.
        RuleFor(request => request.StartsAt)
            .Must(startsAt => startsAt > DateTime.UtcNow)
            .WithMessage("An event must start in the future.");
    }
}

// TODO (R4): write CreateTicketTypeRequestValidator yourself, from scratch.
public class CreateTicketTypeRequestValidator
    : AbstractValidator<CreateTicketTypeRequest>
{
    public CreateTicketTypeRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty();

        RuleFor(request => request.Price)
            .GreaterThanOrEqualTo(0);

        RuleFor(request => request.Capacity)
            .InclusiveBetween(1, 10_000);
    }
}

public class PlaceOrderRequestValidator : AbstractValidator<PlaceOrderRequest>
{
    public PlaceOrderRequestValidator()
    {
        RuleFor(request => request.BuyerEmail)
            .NotEmpty()
            .EmailAddress();

        RuleFor(request => request.Lines)
            .NotEmpty()
            .Must(lines => lines is null || lines.Count <= 5)
            .WithMessage("An order can have at most 5 lines.")
            // TODO (R6): no TicketTypeId appears twice in Lines.
            .Must(HaveDistinctTicketTypes)
            .WithMessage("Ticket types must not appear twice.");

        // TODO (R6): every line's Quantity is between 1 and 10. (Hint: RuleForEach.)
        RuleForEach(request => request.Lines)
            .NotNull()
            .ChildRules(line =>
            {
                line.RuleFor(request => request.TicketTypeId)
                    .NotEmpty();

                line.RuleFor(request => request.Quantity)
                    .InclusiveBetween(1, 10);
            });
    }

    private static bool HaveDistinctTicketTypes(
        IReadOnlyList<OrderLineRequest>? lines)
    {
        if (lines is null)
        {
            return true;
        }

        var distinctCount = lines
            .Where(line => line is not null)
            .Select(line => line.TicketTypeId)
            .Distinct()
            .Count();

        return distinctCount == lines.Count;
    }
}
