using Microsoft.EntityFrameworkCore;
using KasiTix.Domain.Entities;
using KasiTix.Domain.Repositories;
using KasiTix.Infrastructure.Data;

namespace KasiTix.Infrastructure.Repositories;

public class EfOrderRepository : IOrderRepository
{
    private readonly KasiTixDbContext _dbContext;

    public EfOrderRepository(KasiTixDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Order?> GetByIdempotencyKeyAsync(string key)
    {
        return await _dbContext.Orders
            .AsNoTracking()
            .Include(order => order.Lines)
            .SingleOrDefaultAsync(order => order.IdempotencyKey == key);
    }

    public async Task<bool> HasConfirmedOrderAsync(
        Guid eventId,
        string buyerEmail)
    {
        return await _dbContext.Orders.AnyAsync(
            order =>
                order.EventId == eventId &&
                order.BuyerEmail == buyerEmail &&
                order.Status == OrderStatus.Confirmed);
    }

    public async Task AddAsync(Order order)
    {
        await _dbContext.Orders.AddAsync(order);
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}
