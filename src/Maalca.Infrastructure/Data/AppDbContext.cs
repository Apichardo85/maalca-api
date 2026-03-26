using Microsoft.EntityFrameworkCore;
using Maalca.Domain.Entities;

namespace Maalca.Infrastructure.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

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
    public DbSet<AgentExecution> AgentExecutions => Set<AgentExecution>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

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

        // AgentExecution
        modelBuilder.Entity<AgentExecution>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.IssueTitle).IsRequired();
            entity.Property(e => e.Repo).IsRequired();
            entity.Property(e => e.AgentRole).IsRequired();
            entity.Property(e => e.ModelUsed).IsRequired();
            entity.Property(e => e.CostUsd).HasPrecision(18, 8);
            entity.HasIndex(e => e.IssueNumber);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
