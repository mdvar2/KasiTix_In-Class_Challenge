using KasiTix.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace KasiTix.Infrastructure.Data;

public class KasiTixDbContext : DbContext
{
    public KasiTixDbContext(DbContextOptions<KasiTixDbContext> options)
        : base(options)
    {
    }

    public DbSet<Event> Events => Set<Event>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<TicketType> TicketTypes => Set<TicketType>();
    public DbSet<OrderLine> OrderLines => Set<OrderLine>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        var events = modelBuilder.Entity<Event>();
        events.HasKey(eventEntity => eventEntity.Id);
        events.Property(eventEntity => eventEntity.Id)
            .ValueGeneratedNever();
        events.Property(eventEntity => eventEntity.Name)
            .HasMaxLength(120);
        events.HasMany(eventEntity => eventEntity.TicketTypes)
            .WithOne()
            .HasForeignKey(ticketType => ticketType.EventId);
        events.Navigation(eventEntity => eventEntity.TicketTypes)
            .HasField("_ticketTypes")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        var tickets = modelBuilder.Entity<TicketType>();
        tickets.HasKey(ticketType => ticketType.Id);
        tickets.Property(ticketType => ticketType.Id)
            .ValueGeneratedNever();
        tickets.ToTable("TicketTypes", table =>
        {
            table.HasCheckConstraint("CK_TicketTypes_Sold", "\"Sold\" >= 0 AND \"Sold\" <= \"Capacity\"");
            table.HasCheckConstraint("CK_TicketTypes_Price", "\"Price\" >= 0");
            table.HasCheckConstraint("CK_TicketTypes_Capacity", "\"Capacity\" BETWEEN 1 AND 10000");
        });
        tickets.Property(ticketType => ticketType.Version)
            .IsRowVersion();
        tickets.Ignore(ticketType => ticketType.Remaining);
        tickets.HasIndex(ticketType => new { ticketType.EventId, ticketType.Name })
            .IsUnique();

        var orders = modelBuilder.Entity<Order>();
        orders.HasKey(order => order.Id);
        orders.Property(order => order.Id)
            .ValueGeneratedNever();
        orders.Ignore(order => order.Total);
        orders.HasIndex(order => order.IdempotencyKey)
            .IsUnique();
        orders.HasIndex(order => new { order.EventId, order.BuyerEmail })
            .IsUnique()
            .HasFilter("\"Status\" = 0");
        orders.HasOne<Event>()
            .WithMany()
            .HasForeignKey(order => order.EventId);
        orders.HasMany(order => order.Lines)
            .WithOne()
            .HasForeignKey(line => line.OrderId);
        orders.Navigation(order => order.Lines)
            .HasField("_lines")
            .UsePropertyAccessMode(PropertyAccessMode.Field);

        var lines = modelBuilder.Entity<OrderLine>();
        lines.HasKey(line => line.Id);
        lines.Property(line => line.Id)
            .ValueGeneratedNever();
        lines.ToTable("OrderLines", table => table.HasCheckConstraint("CK_OrderLines_Quantity", "\"Quantity\" BETWEEN 1 AND 10"));
        lines.HasIndex(line => new { line.OrderId, line.TicketTypeId })
            .IsUnique();
        lines.HasOne<TicketType>()
            .WithMany()
            .HasForeignKey(line => line.TicketTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
