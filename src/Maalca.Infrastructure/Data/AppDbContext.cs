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
    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<StaffTask> StaffTasks => Set<StaffTask>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<ProductIngredient> ProductIngredients => Set<ProductIngredient>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<Combo> Combos => Set<Combo>();
    public DbSet<ComboRecipe> ComboRecipes => Set<ComboRecipe>();
    public DbSet<ComboServing> ComboServings => Set<ComboServing>();
    public DbSet<ModifierGroup> ModifierGroups => Set<ModifierGroup>();
    public DbSet<ModifierOption> ModifierOptions => Set<ModifierOption>();
    public DbSet<ProductModifierGroup> ProductModifierGroups => Set<ProductModifierGroup>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<TableReservation> TableReservations => Set<TableReservation>();
    public DbSet<Lead> Leads => Set<Lead>();
    public DbSet<AgentExecution> AgentExecutions => Set<AgentExecution>();
    public DbSet<UserAffiliateMap> UserAffiliateMaps => Set<UserAffiliateMap>();
    public DbSet<AffiliateMilestone> AffiliateMilestones => Set<AffiliateMilestone>();
    public DbSet<PlatformAdmin> PlatformAdmins => Set<PlatformAdmin>();
    public DbSet<AdminImpersonationLog> AdminImpersonationLogs => Set<AdminImpersonationLog>();
    public DbSet<AffiliateNote> AffiliateNotes => Set<AffiliateNote>();
    public DbSet<Canal> Canales => Set<Canal>();
    public DbSet<EventoInteraccion> EventosInteraccion => Set<EventoInteraccion>();
    public DbSet<StripeProcessedEvent> StripeProcessedEvents => Set<StripeProcessedEvent>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<ScreenAd> ScreenAds => Set<ScreenAd>();
    public DbSet<Screen> Screens => Set<Screen>();
    public DbSet<TimeBlock> TimeBlocks => Set<TimeBlock>();
    public DbSet<Proposal> Proposals => Set<Proposal>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<Causa> Causas => Set<Causa>();
    public DbSet<Donation> Donations => Set<Donation>();

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
            entity.Property(e => e.Description).HasMaxLength(1000);
            entity.Property(e => e.PrimaryColor).HasMaxLength(7);
            entity.Property(e => e.Slug).HasMaxLength(100);
            entity.HasIndex(e => e.Slug).IsUnique().HasFilter("\"Slug\" IS NOT NULL");
            entity.Property(e => e.ModulosActivos).HasMaxLength(200);
            entity.Property(e => e.StripeCustomerId).HasMaxLength(255);
            entity.Property(e => e.StripeSubscriptionId).HasMaxLength(255);
            entity.Property(e => e.StripeConnectAccountId).HasMaxLength(255);
            entity.Property(e => e.Country).HasMaxLength(2);
            entity.Property(e => e.Language).HasMaxLength(2);
            entity.HasIndex(e => e.StripeCustomerId);
            entity.HasIndex(e => e.StripeConnectAccountId);
        });

        // StripeProcessedEvent
        modelBuilder.Entity<StripeProcessedEvent>(entity =>
        {
            entity.HasKey(e => e.EventId);
            entity.Property(e => e.EventId).HasMaxLength(255);
        });

        // Order
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ItemsJson).IsRequired();
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Subtotal).HasPrecision(18, 2);
            entity.Property(e => e.Tax).HasPrecision(18, 2);
            entity.Property(e => e.Tip).HasPrecision(18, 2);
            entity.Property(e => e.Total).HasPrecision(18, 2);
            entity.Property(e => e.StripeCheckoutSessionId).HasMaxLength(255);
            entity.Property(e => e.StripePaymentIntentId).HasMaxLength(255);
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.AffiliateId, e.CreatedAt });
        });

        // ScreenAd
        modelBuilder.Entity<ScreenAd>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.MediaUrl).HasMaxLength(500).IsRequired();
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.AffiliateId, e.SortOrder });
        });

        // Screen — Fase 9 Etapa B
        modelBuilder.Entity<Screen>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(120).IsRequired();
            entity.Property(e => e.Language).HasMaxLength(2);
            entity.Property(e => e.CategoryFilter).HasMaxLength(500);
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.AffiliateId, e.SortOrder });
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
            entity.Property(e => e.DescriptionEn).HasMaxLength(1000);
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.Services)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Activity -- modulo Eventos/Actividades (backlog 2026-09-25), ver comentario en
        // Activity.cs. Indice compuesto AffiliateId+StartsAt porque la consulta mas comun es
        // "proximos eventos de este afiliado" (dashboard y pagina publica), ordenada por fecha.
        modelBuilder.Entity<Activity>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Location).HasMaxLength(300);
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.Activities)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.AffiliateId, e.StartsAt });
        });

        // Causa -- movido de columna JSON en Affiliate a tabla propia (backlog 2026-09-25,
        // migracion MoveCausasToTable), ver comentario en Causa.cs.
        modelBuilder.Entity<Causa>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Type).IsRequired().HasMaxLength(20);
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.GoalAmount).HasPrecision(18, 2);
            entity.Property(e => e.CurrentAmount).HasPrecision(18, 2);
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.Causas)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.AffiliateId, e.SortOrder });
        });

        // Donation -- donaciones reales de Community via Stripe Connect (backlog 2026-09-26),
        // reemplaza Affiliate.CommunityImpact.FundraisingCurrentAmount reportado a mano cuando
        // el afiliado tiene Connect activo, ver comentario en Donation.cs y DonationService.
        modelBuilder.Entity<Donation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DonorName).HasMaxLength(200);
            entity.Property(e => e.DonorEmail).HasMaxLength(320);
            entity.Property(e => e.Message).HasMaxLength(500);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.StripeCheckoutSessionId).HasMaxLength(255);
            entity.Property(e => e.StripePaymentIntentId).HasMaxLength(255);
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.AffiliateId, e.Status, e.CreatedAt });
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
            entity.HasIndex(e => e.Token).IsUnique();
        });

        // TableReservation — CustomerName/Phone/Email siguen siendo la fuente de verdad del
        // request público (ver comentario en la entidad); CustomerId (tarea #244) es un vínculo
        // opcional agregado después para acumular historial, no reemplaza esos campos.
        modelBuilder.Entity<TableReservation>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.TableReservations)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Customer)
                  .WithMany()
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(e => new { e.AffiliateId, e.Date });
        });

        // Proposal
        modelBuilder.Entity<Proposal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CustomerName).IsRequired();
            entity.Property(e => e.Title).IsRequired();
            entity.Property(e => e.Amount).HasPrecision(18, 2);
            entity.Property(e => e.Currency).HasMaxLength(3).IsRequired();
            entity.HasIndex(e => e.Token).IsUnique();
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Customer)
                  .WithMany()
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // ProductIngredient (receta: Product -> InventoryItem + cantidad)
        modelBuilder.Entity<ProductIngredient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.HasOne(e => e.Product)
                  .WithMany()
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
            // Restrict (no Cascade): borrar un InventoryItem usado en receta debe fallar explícito
            // (ver InventoryService.DeleteInventoryItemAsync), no desaparecer el vínculo en
            // silencio dejando la receta corta sin avisar a nadie.
            entity.HasOne(e => e.InventoryItem)
                  .WithMany()
                  .HasForeignKey(e => e.InventoryItemId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.ProductId, e.InventoryItemId }).IsUnique();
        });

        // ModifierGroup (grupo de modificadores reutilizable, ej. "Guarnición")
        modelBuilder.Entity<ModifierGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ModifierOption (opción dentro de un ModifierGroup, con su propio delta de precio)
        modelBuilder.Entity<ModifierOption>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.PriceDelta).HasPrecision(18, 2);
            entity.HasOne(e => e.ModifierGroup)
                  .WithMany(g => g.Options)
                  .HasForeignKey(e => e.ModifierGroupId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ProductModifierGroup (junction Product <-> ModifierGroup, reutilizable entre productos)
        modelBuilder.Entity<ProductModifierGroup>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Product)
                  .WithMany()
                  .HasForeignKey(e => e.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
            // Restrict (no Cascade): borrar un ModifierGroup usado por productos debe fallar
            // explícito (ver ModifierService.DeleteGroupAsync), no desaparecer el vínculo en
            // silencio dejando el producto sin avisar a nadie.
            entity.HasOne(e => e.ModifierGroup)
                  .WithMany()
                  .HasForeignKey(e => e.ModifierGroupId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.ProductId, e.ModifierGroupId }).IsUnique();
        });

        // TimeBlock
        modelBuilder.Entity<TimeBlock>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.StartTime).HasMaxLength(5).IsRequired();
            entity.Property(e => e.EndTime).HasMaxLength(5).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(200);
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Staff)
                  .WithMany()
                  .HasForeignKey(e => e.StaffId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(e => new { e.AffiliateId, e.Date });
        });

        // TeamMember
        modelBuilder.Entity<TeamMember>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.HourlyRate).HasPrecision(18, 2);
            entity.Property(e => e.PinCode).HasMaxLength(6);
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.TeamMembers)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // TimeEntry (ponche de entrada/salida)
        modelBuilder.Entity<TimeEntry>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.TeamMember)
                  .WithMany()
                  .HasForeignKey(e => e.TeamMemberId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // StaffTask (tareas asignadas)
        modelBuilder.Entity<StaffTask>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired();
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            // SetNull (no Cascade): borrar a un empleado no debe borrar el historial de tareas
            // que se le asignaron, solo desvincularlas.
            entity.HasOne(e => e.TeamMember)
                  .WithMany()
                  .HasForeignKey(e => e.TeamMemberId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // InventoryItem
        modelBuilder.Entity<InventoryItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.Property(e => e.MinStock).HasPrecision(18, 3);
            entity.Property(e => e.UnitCost).HasPrecision(18, 4);
            entity.Property(e => e.DescriptionEn).HasMaxLength(1000);
            entity.Property(e => e.Unit).HasMaxLength(20);
            // Filtro ?expiringBefore= de /inventory-items (alertas de vencimiento en Comunidad).
            entity.HasIndex(e => new { e.AffiliateId, e.ExpirationDate });
            entity.HasOne(e => e.Affiliate)
                  .WithMany(a => a.InventoryItems)
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // InventoryMovement
        modelBuilder.Entity<InventoryMovement>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Quantity).HasPrecision(18, 3);
            entity.HasOne(e => e.InventoryItem)
                  .WithMany(i => i.Movements)
                  .HasForeignKey(e => e.InventoryItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ── MaalCa Comunidad (Fase 1): Recipe / RecipeIngredient / Combo / ComboServing ──
        modelBuilder.Entity<Recipe>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CostPerServing).HasPrecision(18, 4);
            entity.HasIndex(e => e.AffiliateId);
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<RecipeIngredient>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.QuantityRequired).HasPrecision(18, 3);
            entity.HasOne(e => e.Recipe)
                  .WithMany(r => r.Ingredients)
                  .HasForeignKey(e => e.RecipeId)
                  .OnDelete(DeleteBehavior.Cascade);
            // Restrict, igual que ProductIngredient: borrar un insumo en uso debe fallar explícito
            // (ver InventoryService/CommunityService delete), no dejar la receta corta en silencio.
            entity.HasOne(e => e.InventoryItem)
                  .WithMany()
                  .HasForeignKey(e => e.InventoryItemId)
                  .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(e => new { e.RecipeId, e.InventoryItemId }).IsUnique();
        });

        modelBuilder.Entity<Combo>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CostPerPlate).HasPrecision(18, 4);
            entity.HasIndex(e => e.AffiliateId);
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ComboRecipe>(entity =>
        {
            entity.HasKey(e => new { e.ComboId, e.RecipeId });
            entity.HasOne(e => e.Combo)
                  .WithMany(c => c.Recipes)
                  .HasForeignKey(e => e.ComboId)
                  .OnDelete(DeleteBehavior.Cascade);
            // Restrict: una Recipe en uso por un combo no se borra en silencio (ver CommunityService).
            entity.HasOne(e => e.Recipe)
                  .WithMany()
                  .HasForeignKey(e => e.RecipeId)
                  .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ComboServing>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ComboName).IsRequired().HasMaxLength(200);
            entity.Property(e => e.CostPerPlate).HasPrecision(18, 4);
            // community-metrics suma por afiliado + rango de fecha.
            entity.HasIndex(e => new { e.AffiliateId, e.ServedAt });
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Combo)
                  .WithMany()
                  .HasForeignKey(e => e.ComboId)
                  .OnDelete(DeleteBehavior.SetNull);
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
            entity.HasOne(e => e.Customer)
                  .WithMany()
                  .HasForeignKey(e => e.CustomerId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // Product
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Price).HasPrecision(18, 2);
            entity.Property(e => e.DescriptionEn).HasMaxLength(1000);
            entity.Property(e => e.Periods).HasMaxLength(100);
            entity.Property(e => e.WeekDays).HasMaxLength(100);
            entity.Property(e => e.Flags).HasMaxLength(200);
            entity.Property(e => e.VideoUrl).HasMaxLength(500);
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

        // Lead
        modelBuilder.Entity<Lead>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).IsRequired();
            entity.Property(e => e.Email).IsRequired();
        });

        // UserAffiliateMap
        modelBuilder.Entity<UserAffiliateMap>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SupabaseUserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.SupabaseUserId);
            entity.HasIndex(e => new { e.SupabaseUserId, e.AffiliateId }).IsUnique();
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            // Vínculo opcional Personal ↔ Equipo — SetNull para que borrar un TeamMember de
            // Personal no arrastre el acceso al dashboard, solo lo desvincule.
            entity.HasOne(e => e.TeamMember)
                  .WithMany()
                  .HasForeignKey(e => e.TeamMemberId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        // PlatformAdmin
        modelBuilder.Entity<PlatformAdmin>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.SupabaseUserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.Email).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.SupabaseUserId);
            entity.HasIndex(e => e.Email).IsUnique();
        });

        // AdminImpersonationLog
        modelBuilder.Entity<AdminImpersonationLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AdminSupabaseUserId).IsRequired().HasMaxLength(64);
            entity.Property(e => e.AdminEmail).IsRequired().HasMaxLength(256);
            entity.HasIndex(e => e.AdminSupabaseUserId);
            entity.HasIndex(e => e.AffiliateId);
        });

        // AffiliateNote
        modelBuilder.Entity<AffiliateNote>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.AuthorEmail).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Text).IsRequired();
            entity.HasIndex(e => e.AffiliateId);
        });

        // AffiliateMilestone
        modelBuilder.Entity<AffiliateMilestone>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Key).IsRequired().HasMaxLength(100);
            entity.HasIndex(e => new { e.AffiliateId, e.Key }).IsUnique();
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Canal
        modelBuilder.Entity<Canal>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ValorCrudo).IsRequired();
            entity.Property(e => e.EnlaceGenerado).IsRequired();
            entity.HasIndex(e => e.AffiliateId);
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // EventoInteraccion
        modelBuilder.Entity<EventoInteraccion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => new { e.AffiliateId, e.Tipo, e.CreatedAt });
            entity.HasOne(e => e.Affiliate)
                  .WithMany()
                  .HasForeignKey(e => e.AffiliateId)
                  .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.Canal)
                  .WithMany()
                  .HasForeignKey(e => e.CanalId)
                  .OnDelete(DeleteBehavior.SetNull);
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
