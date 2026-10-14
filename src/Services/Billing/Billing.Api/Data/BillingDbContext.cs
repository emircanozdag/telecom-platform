using Billing.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace Billing.Api.Data;

public sealed class BillingDbContext(DbContextOptions<BillingDbContext> options) : DbContext(options)
{
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<ProcessedEvent> ProcessedEvents => Set<ProcessedEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Invoice>(invoice =>
        {
            invoice.HasKey(i => new { i.SubscriberId, i.Period });
            invoice.Property(i => i.Msisdn).HasMaxLength(15);
            invoice.Property(i => i.OverageAmount).HasPrecision(14, 2);
        });

        modelBuilder.Entity<ProcessedEvent>().HasKey(e => e.EventId);
    }
}
