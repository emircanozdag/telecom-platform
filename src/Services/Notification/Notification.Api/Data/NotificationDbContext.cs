using Microsoft.EntityFrameworkCore;

namespace Notification.Api.Data;

public sealed class NotificationDbContext(DbContextOptions<NotificationDbContext> options) : DbContext(options)
{
    public DbSet<Domain.Notification> Notifications => Set<Domain.Notification>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Domain.Notification>(n =>
        {
            n.HasKey(x => x.Id);
            n.HasIndex(x => new { x.EventId, x.Channel }).IsUnique();
            n.HasIndex(x => x.SubscriberId);
            n.Property(x => x.Channel).HasConversion<string>().HasMaxLength(16);
            n.Property(x => x.Recipient).HasMaxLength(254);
            n.Property(x => x.Message).HasMaxLength(2000);
        });
    }
}
