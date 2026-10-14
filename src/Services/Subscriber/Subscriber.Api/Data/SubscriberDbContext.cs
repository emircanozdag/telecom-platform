using Microsoft.EntityFrameworkCore;
using Subscriber.Api.Domain;
using Subscriber.Api.Outbox;

namespace Subscriber.Api.Data;

public sealed class SubscriberDbContext(DbContextOptions<SubscriberDbContext> options) : DbContext(options)
{
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Domain.Subscriber> Subscribers => Set<Domain.Subscriber>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Plan>(plan =>
        {
            plan.HasKey(p => p.Code);
            plan.Property(p => p.Code).HasMaxLength(32);
            plan.Property(p => p.Name).HasMaxLength(100);
            plan.Property(p => p.OveragePricePerMegabyte).HasPrecision(10, 4);
            plan.HasData(
                new Plan { Code = "MINI-5GB", Name = "Mini 5 GB", MonthlyQuotaMegabytes = 5_120, OveragePricePerMegabyte = 0.05m },
                new Plan { Code = "STANDARD-20GB", Name = "Standard 20 GB", MonthlyQuotaMegabytes = 20_480, OveragePricePerMegabyte = 0.03m },
                new Plan { Code = "UNLIMITED", Name = "Unlimited (fair use 100 GB)", MonthlyQuotaMegabytes = 102_400, OveragePricePerMegabyte = 0m });
        });

        modelBuilder.Entity<Domain.Subscriber>(subscriber =>
        {
            subscriber.HasKey(s => s.Id);
            subscriber.Property(s => s.Msisdn).HasMaxLength(15);
            subscriber.HasIndex(s => s.Msisdn).IsUnique();
            subscriber.Property(s => s.Email).HasMaxLength(254);
            subscriber.Property(s => s.FullName).HasMaxLength(200);
            subscriber.HasOne<Plan>().WithMany().HasForeignKey(s => s.PlanCode);
            // Optimistic concurrency on Postgres' system column: concurrent usage updates can't overwrite each other.
            subscriber.Property<uint>("xmin").IsRowVersion();
        });

        modelBuilder.Entity<OutboxMessage>(outbox =>
        {
            outbox.HasKey(o => o.Id);
            outbox.Property(o => o.Topic).HasMaxLength(200);
            outbox.Property(o => o.Key).HasMaxLength(100);
            outbox.Property(o => o.EventType).HasMaxLength(200);
            outbox.Property(o => o.Payload).HasColumnType("jsonb");
            outbox.HasIndex(o => o.OccurredAtUtc).HasFilter("\"ProcessedAtUtc\" IS NULL");
        });
    }
}
