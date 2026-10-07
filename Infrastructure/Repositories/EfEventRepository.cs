using Microsoft.EntityFrameworkCore;
using KasiTix.Domain.Entities;
using KasiTix.Domain.Repositories;
using KasiTix.Infrastructure.Data;

namespace KasiTix.Infrastructure.Repositories;

public class EfEventRepository : IEventRepository
{
    private readonly KasiTixDbContext _dbContext;

    public EfEventRepository(KasiTixDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Event?> GetByIdAsync(Guid id, bool tracked)
    {
        IQueryable<Event> query =
            _dbContext.Events.Include(eventEntity => eventEntity.TicketTypes);

        if (!tracked)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(
            eventEntity => eventEntity.Id == id);
    }

    public async Task AddAsync(Event eventEntity)
    {
        await _dbContext.Events.AddAsync(eventEntity);
    }

    public async Task SaveChangesAsync()
    {
        await _dbContext.SaveChangesAsync();
    }
}
