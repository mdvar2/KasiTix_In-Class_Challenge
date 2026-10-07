using KasiTix.Domain.Entities;
namespace KasiTix.Domain.Repositories;

public interface IEventRepository
{
    Task<Event?> GetByIdAsync(Guid id, bool tracked);
    Task AddAsync(Event entity);
    Task SaveChangesAsync();
}

public interface IOrderRepository
{
    Task<Order?> GetByIdempotencyKeyAsync(string key);
    Task<bool> HasConfirmedOrderAsync(Guid eventId, string buyerEmail);
    Task AddAsync(Order order);
    Task SaveChangesAsync();
}
