using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Maalca.Domain.Common;
using Maalca.Domain.Common.Interfaces;
using Maalca.Domain.Entities;

namespace Maalca.Infrastructure.Data;

public class AppDbContext : DbContext
{
    private readonly ICurrentUserService? _currentUserService;

    public AppDbContext(DbContextOptions<AppDbContext> options, ICurrentUserService? currentUserService = null)
        : base(options)
    {
        _currentUserService = currentUserService;
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<Affiliate> Affiliates => Set<Affiliate>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<GiftCard> GiftCards => Set<GiftCard>();
    public DbSet<Campaign> Campaigns => Set<Campaign>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var userId = _currentUserService?.UserId?.ToString();
        var auditEntries = new List<AuditLog>();

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            // Skip AuditLog entities to prevent infinite recursion
            if (entry.Entity is AuditLog) continue;

            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    if (entry.Entity is AuditableEntity addedAuditable)
                        addedAuditable.CreatedBy = userId;

                    auditEntries.Add(new AuditLog
                    {
                        EntityType = entry.Entity.GetType().Name,
                        EntityId = entry.Entity.Id.ToString(),
                        Action = "Create",
                        NewValues = SerializeProperties(entry, EntityState.Added),
                        UserId = userId,
                        Timestamp = DateTime.UtcNow
                    });
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    if (entry.Entity is AuditableEntity modifiedAuditable)
                        modifiedAuditable.UpdatedBy = userId;

                    var changedProps = entry.Properties.Where(p => p.IsModified).ToList();
                    if (changedProps.Count > 0)
                    {
                        auditEntries.Add(new AuditLog
                        {
                            EntityType = entry.Entity.GetType().Name,
                            EntityId = entry.Entity.Id.ToString(),
                            Action = entry.Entity.IsDeleted ? "Delete" : "Update",
                            OldValues = JsonSerializer.Serialize(
                                changedProps.ToDictionary(p => p.Metadata.Name, p => p.OriginalValue?.ToString())),
                            NewValues = JsonSerializer.Serialize(
                                changedProps.ToDictionary(p => p.Metadata.Name, p => p.CurrentValue?.ToString())),
                            AffectedColumns = JsonSerializer.Serialize(
                                changedProps.Select(p => p.Metadata.Name)),
                            UserId = userId,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    break;

                case EntityState.Deleted:
                    // Convert hard delete to soft delete
                    entry.State = EntityState.Modified;
                    entry.Entity.IsDeleted = true;
                    entry.Entity.DeletedAt = DateTime.UtcNow;
                    if (entry.Entity is AuditableEntity deletedAuditable)
                        deletedAuditable.DeletedBy = userId;

                    auditEntries.Add(new AuditLog
                    {
                        EntityType = entry.Entity.GetType().Name,
                        EntityId = entry.Entity.Id.ToString(),
                        Action = "Delete",
                        OldValues = SerializeProperties(entry, EntityState.Deleted),
                        UserId = userId,
                        Timestamp = DateTime.UtcNow
                    });
                    break;
            }
        }

        if (auditEntries.Count > 0)
            AuditLogs.AddRange(auditEntries);

        return await base.SaveChangesAsync(cancellationToken);
    }

    private static string? SerializeProperties(Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, EntityState state)
    {
        var dict = entry.Properties
            .Where(p => p.Metadata.Name != "RowVersion")
            .ToDictionary(
                p => p.Metadata.Name,
                p => (state == EntityState.Added ? p.CurrentValue : p.OriginalValue)?.ToString());
        return JsonSerializer.Serialize(dict);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Global query filters for soft delete (exclude AuditLog)
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                var parameter = Expression.Parameter(entityType.ClrType, "e");
                var isDeletedProperty = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
                var filter = Expression.Lambda(Expression.Not(isDeletedProperty), parameter);
                entityType.SetQueryFilter(filter);
            }
        }

        // Concurrency token using PostgreSQL xmin
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                modelBuilder.Entity(entityType.ClrType).UseXminAsConcurrencyToken();
            }
        }

        // AuditLog
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.EntityType);
            entity.HasIndex(e => e.EntityId);
            entity.HasIndex(e => e.Timestamp);
            entity.HasIndex(e => e.UserId);
        });

        // User
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).IsRequired();
            entity.Property(e => e.PasswordHash).IsRequired();
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.Users)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Affiliate
        modelBuilder.Entity<Affiliate>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
        });

        // Customer
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.Customers)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Service
        modelBuilder.Entity<Service>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.Services)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Appointment
        modelBuilder.Entity<Appointment>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.Appointments)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Customer)
                  .WithMany()
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.Service)
                  .WithMany(s => s.Appointments)
                  .HasForeignKey(e => e.ServiceId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(e => e.AssignedTo)
                  .WithMany()
                  .HasForeignKey(e => e.AssignedToId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // TeamMember
        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.TeamMembers)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // InventoryItem
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.InventoryItems)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // InventoryMovement
        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.InventoryItem)
                  .WithMany(i => i.Movements)
                  .HasForeignKey(e => e.InventoryItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // QueueEntry
        modelBuilder.Entity<QueueEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DisplayName).IsRequired();
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.QueueEntries)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Service)
                  .WithMany()
                  .HasForeignKey(e => e.ServiceId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.AssignedTo)
                  .WithMany()
                  .HasForeignKey(e => e.AssignedToId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Product
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.Products)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Invoice
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.InvoiceNumber).IsRequired();
            entity.HasIndex(e => e.InvoiceNumber).IsUnique();
            entity.Property(e => e.Total).HasPrecision(18, 2);
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.Invoices)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Customer)
                  .WithMany()
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        // InvoiceItem
        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Total).HasPrecision(18, 2);
            entity.HasOne(e => e.Invoice)
                  .WithMany(i => i.Items)
                  .HasForeignKey(e => e.InvoiceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // GiftCard
        modelBuilder.Entity<GiftCard>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Code).IsUnique();
            entity.Property(e => e.Code).IsRequired();
            entity.Property(e => e.InitialAmount).HasPrecision(18, 2);
            entity.Property(e => e.Balance).HasPrecision(18, 2);
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.GiftCards)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Campaign
        modelBuilder.Entity<Campaign>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.Campaigns)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Lead
        modelBuilder.Entity<Lead>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Email).IsRequired();
        });
    }
}
