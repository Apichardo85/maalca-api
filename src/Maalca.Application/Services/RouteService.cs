using Maalca.Application.Common;
using Maalca.Application.Common.DTOs;
using Maalca.Application.Common.Interfaces;
using Maalca.Domain.Entities;
using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Maalca.Application.Services;

public class RouteService : IRouteService
{
    private static readonly string[] VehicleStatuses = { "disponible", "preparacion", "en_ruta", "entrega", "mantenimiento" };

    private readonly AppDbContext _db;
    private readonly IContainerService _containers;
    private readonly IAuditLogService _audit;

    public RouteService(AppDbContext db, IContainerService containers, IAuditLogService audit)
    {
        _db = db;
        _containers = containers;
        _audit = audit;
    }

    // ── Vehículos ────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<VehicleDto>> GetVehiclesAsync(Guid affiliateId, bool includeInactive)
    {
        var q = _db.Vehicles.AsNoTracking().Where(v => v.AffiliateId == affiliateId);
        if (!includeInactive) q = q.Where(v => v.IsActive);
        var list = await q.OrderBy(v => v.Name).ThenBy(v => v.Plate).ToListAsync();
        var names = await DriverNamesAsync(affiliateId, list.Select(v => v.DriverId));
        return list.Select(v => ToDto(v, names)).ToList();
    }

    public async Task<VehicleDto> CreateVehicleAsync(Guid affiliateId, UpsertVehicleRequest request, string? actorId)
    {
        await ValidateVehicleAsync(affiliateId, request);
        var vehicle = new Vehicle { AffiliateId = affiliateId, CreatedBy = actorId };
        ApplyVehicle(vehicle, request);
        _db.Vehicles.Add(vehicle);
        await _db.SaveChangesAsync();
        return ToDto(vehicle, await DriverNamesAsync(affiliateId, new[] { vehicle.DriverId }));
    }

    public async Task<VehicleDto?> UpdateVehicleAsync(Guid affiliateId, Guid vehicleId, UpsertVehicleRequest request, string? actorId)
    {
        var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == vehicleId && v.AffiliateId == affiliateId);
        if (vehicle is null) return null;
        await ValidateVehicleAsync(affiliateId, request);
        ApplyVehicle(vehicle, request);
        vehicle.UpdatedAt = DateTime.UtcNow;
        vehicle.UpdatedBy = actorId;
        await _db.SaveChangesAsync();
        return ToDto(vehicle, await DriverNamesAsync(affiliateId, new[] { vehicle.DriverId }));
    }

    // ── Rutas ────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<RouteDto>> GetRoutesAsync(Guid affiliateId, DateOnly? from, DateOnly? to, string? status, Guid? restrictDriverId = null)
    {
        var q = _db.DeliveryRoutes.AsNoTracking().Include(r => r.Vehicle).Include(r => r.Stops)
            .Where(r => r.AffiliateId == affiliateId);
        if (from.HasValue) q = q.Where(r => r.RouteDate >= from.Value);
        if (to.HasValue) q = q.Where(r => r.RouteDate <= to.Value);
        if (!string.IsNullOrWhiteSpace(status)) q = q.Where(r => r.Status == status);
        if (restrictDriverId.HasValue) q = q.Where(r => r.DriverId == restrictDriverId.Value);
        var list = await q.OrderByDescending(r => r.RouteDate).ThenBy(r => r.Name).Take(300).ToListAsync();
        var names = await DriverNamesAsync(affiliateId, list.Select(r => r.DriverId));
        // Listado: solo conteos de paradas, sin el detalle (el detalle se pide por ruta).
        return list.Select(r => ToDto(r, names, includeStops: false, customers: null)).ToList();
    }

    public async Task<RouteDto?> GetRouteAsync(Guid affiliateId, Guid routeId, Guid? restrictDriverId = null)
    {
        var route = await LoadRouteAsync(affiliateId, routeId, track: false, restrictDriverId);
        return route is null ? null : await ToFullDtoAsync(affiliateId, route);
    }

    public async Task<RouteDto> CreateRouteAsync(Guid affiliateId, UpsertRouteRequest request, string? actorId)
    {
        var (vehicle, driverId) = await ValidateRouteAsync(affiliateId, request);
        var route = new DeliveryRoute { AffiliateId = affiliateId, CreatedBy = actorId };
        ApplyRoute(route, request, vehicle, driverId);
        await ReplaceStopsAsync(affiliateId, route, request.CustomerIds);
        _db.DeliveryRoutes.Add(route);
        await _db.SaveChangesAsync();
        return (await GetRouteAsync(affiliateId, route.Id))!;
    }

    public async Task<RouteDto?> UpdateRouteAsync(Guid affiliateId, Guid routeId, UpsertRouteRequest request, string? actorId)
    {
        var route = await LoadRouteAsync(affiliateId, routeId, track: true);
        if (route is null) return null;
        if (route.Status is not ("planned" or "loading"))
            throw new InvalidOperationException("La ruta ya salió o está cerrada; no se puede editar.");

        var (vehicle, driverId) = await ValidateRouteAsync(affiliateId, request);
        ApplyRoute(route, request, vehicle, driverId);
        if (request.CustomerIds is not null) await ReplaceStopsAsync(affiliateId, route, request.CustomerIds);
        route.UpdatedAt = DateTime.UtcNow;
        route.UpdatedBy = actorId;
        await _db.SaveChangesAsync();
        return await GetRouteAsync(affiliateId, routeId);
    }

    public async Task<RouteDto?> StartRouteAsync(Guid affiliateId, Guid routeId, StartRouteRequest request, string? actorId, Guid? restrictDriverId = null)
    {
        var route = await LoadRouteAsync(affiliateId, routeId, track: true, restrictDriverId);
        if (route is null) return null;
        if (route.Status is not ("planned" or "loading"))
            throw new InvalidOperationException("Solo una ruta planificada puede salir.");
        if (route.Stops.Count == 0)
            throw new InvalidOperationException("La ruta no tiene paradas.");
        if (request.LoadedFull is < 0 or > 100_000)
            throw new ArgumentException("La cantidad cargada es inválida.");

        if (route.VehicleId.HasValue)
        {
            var busy = await _db.DeliveryRoutes.AnyAsync(r => r.AffiliateId == affiliateId && r.Id != route.Id
                && r.VehicleId == route.VehicleId && r.Status == "in_progress");
            if (busy) throw new InvalidOperationException("Ese vehículo ya está en otra ruta en curso.");
            var vehicle = await _db.Vehicles.FirstAsync(v => v.Id == route.VehicleId.Value);
            vehicle.Status = "en_ruta";
        }

        route.Status = "in_progress";
        route.DepartedAt = DateTime.UtcNow;
        route.LoadedFull = request.LoadedFull ?? route.LoadedFull;
        route.UpdatedAt = DateTime.UtcNow;
        route.UpdatedBy = actorId;
        await _db.SaveChangesAsync();
        return await GetRouteAsync(affiliateId, routeId);
    }

    public async Task<RouteDto?> CancelRouteAsync(Guid affiliateId, Guid routeId, string? actorId)
    {
        var route = await LoadRouteAsync(affiliateId, routeId, track: true);
        if (route is null) return null;
        if (route.Status is not ("planned" or "loading"))
            throw new InvalidOperationException("Solo se puede cancelar una ruta que todavía no salió.");
        route.Status = "cancelled";
        route.ClosedAt = DateTime.UtcNow;
        route.UpdatedAt = DateTime.UtcNow;
        route.UpdatedBy = actorId;
        await _db.SaveChangesAsync();
        return await GetRouteAsync(affiliateId, routeId);
    }

    public async Task<RouteReconciliationDto?> CloseRouteAsync(Guid affiliateId, Guid routeId, CloseRouteRequest request, string? actorId, Guid? restrictDriverId = null)
    {
        var route = await LoadRouteAsync(affiliateId, routeId, track: true, restrictDriverId);
        if (route is null) return null;
        if (route.Status != "in_progress")
            throw new InvalidOperationException("Solo se puede cerrar una ruta en curso.");

        var pending = route.Stops.Where(s => s.Status == "pending").ToList();
        if (pending.Count > 0 && !request.MarkPendingAsSkipped)
            throw new InvalidOperationException($"Quedan {pending.Count} paradas sin visitar; márcalas como saltadas para cerrar.");
        foreach (var s in pending) s.Status = "skipped";

        route.Status = "completed";
        route.ClosedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(request.Notes))
            route.Notes = string.IsNullOrWhiteSpace(route.Notes) ? request.Notes.Trim() : $"{route.Notes}\n{request.Notes.Trim()}";
        route.UpdatedAt = DateTime.UtcNow;
        route.UpdatedBy = actorId;
        if (route.VehicleId.HasValue)
        {
            var vehicle = await _db.Vehicles.FirstOrDefaultAsync(v => v.Id == route.VehicleId.Value);
            if (vehicle is not null) vehicle.Status = "disponible";
        }
        await _db.SaveChangesAsync();
        return await GetReconciliationAsync(affiliateId, routeId);
    }

    public async Task<RouteReconciliationDto?> GetReconciliationAsync(Guid affiliateId, Guid routeId, Guid? restrictDriverId = null)
    {
        var route = await LoadRouteAsync(affiliateId, routeId, track: false, restrictDriverId);
        if (route is null) return null;

        var stopIds = route.Stops.Select(s => s.Id).ToList();
        var deliveries = await _db.Deliveries.AsNoTracking().Include(d => d.Items)
            .Where(d => d.AffiliateId == affiliateId && stopIds.Contains(d.RouteStopId)).ToListAsync();
        var deliveryIds = deliveries.Select(d => d.Id).ToList();

        var moves = await _db.ContainerMovements.AsNoTracking()
            .Where(m => m.AffiliateId == affiliateId && m.DeliveryId != null && deliveryIds.Contains(m.DeliveryId.Value))
            .GroupBy(m => m.Type)
            .Select(g => new { Type = g.Key, Qty = g.Sum(m => m.Quantity) })
            .ToListAsync();
        int Sum(string type) => moves.Where(m => m.Type == type).Sum(m => m.Qty);

        var paymentIds = deliveries.Where(d => d.PaymentId.HasValue).Select(d => d.PaymentId!.Value).ToList();
        var payments = await _db.Payments.AsNoTracking().Where(p => paymentIds.Contains(p.Id)).ToListAsync();

        var delivered = Sum("delivery");
        return new RouteReconciliationDto(
            route.Id, route.Status, route.LoadedFull,
            delivered, Sum("return"), Sum("damaged"),
            route.LoadedFull.HasValue ? route.LoadedFull.Value - delivered : null,
            route.Stops.Count,
            route.Stops.Count(s => s.Status == "visited"),
            route.Stops.Count(s => s.Status == "skipped"),
            route.Stops.Count(s => s.Status == "failed"),
            route.Stops.Count(s => s.Status == "pending"),
            deliveries.Sum(d => d.Items.Sum(i => Math.Round(i.Quantity * i.UnitPrice, 2))),
            payments.Where(p => p.Method == "cash").Sum(p => p.Amount),
            payments.Where(p => p.Method == "transfer").Sum(p => p.Amount));
    }

    // ── Entregas ─────────────────────────────────────────────────────────────

    public async Task<RegisterDeliveryResultDto?> RegisterDeliveryAsync(
        Guid affiliateId, Guid routeId, Guid stopId, RegisterDeliveryRequest r, string? actorId, string? actorName, Guid? restrictDriverId = null)
    {
        var route = await LoadRouteAsync(affiliateId, routeId, track: true, restrictDriverId);
        var stop = route?.Stops.FirstOrDefault(s => s.Id == stopId);
        if (route is null || stop is null) return null;

        var key = string.IsNullOrWhiteSpace(r.IdempotencyKey) ? null : r.IdempotencyKey.Trim();
        if (key is { Length: > 90 }) throw new ArgumentException("La clave de idempotencia es demasiado larga (máx. 90).");
        if (key is not null)
        {
            var existing = await FindDeliveryByKeyAsync(affiliateId, key);
            if (existing is not null) return await DuplicateResultAsync(affiliateId, existing);
        }

        if (route.Status != "in_progress")
            throw new InvalidOperationException("La ruta no está en curso; no se pueden registrar entregas.");
        if (stop.Status != "pending")
            throw new InvalidOperationException("Esa parada ya fue atendida.");

        var items = (r.Items ?? Array.Empty<DeliveryItemInput>()).ToList();
        var lines = (r.Containers ?? Array.Empty<DeliveryContainerLine>()).ToList();
        foreach (var i in items)
        {
            if (string.IsNullOrWhiteSpace(i.Description) || i.Description.Trim().Length > 200)
                throw new ArgumentException("Cada producto necesita descripción (máx. 200 caracteres).");
            if (i.Quantity <= 0 || i.Quantity > 100_000) throw new ArgumentException("La cantidad de un producto es inválida.");
            if (i.UnitPrice < 0) throw new ArgumentException("El precio no puede ser negativo.");
        }
        foreach (var l in lines)
        {
            if (l.Delivered < 0 || l.Returned < 0 || l.Damaged < 0
                || l.Delivered > 10_000 || l.Returned > 10_000 || l.Damaged > 10_000)
                throw new ArgumentException("Las cantidades de envases son inválidas.");
        }
        lines = lines.Where(l => l.Delivered + l.Returned + l.Damaged > 0).ToList();
        if (lines.GroupBy(l => l.ContainerTypeId).Any(g => g.Count() > 1))
            throw new ArgumentException("Un tipo de envase solo puede aparecer una vez por entrega.");

        decimal? payAmount = r.PaymentAmount.HasValue ? Math.Round(r.PaymentAmount.Value, 2, MidpointRounding.AwayFromZero) : null;
        if (payAmount is <= 0) payAmount = null;
        var payMethod = (r.PaymentMethod ?? "cash").Trim().ToLowerInvariant();
        if (payMethod is not ("cash" or "transfer"))
            throw new ArgumentException("Método de pago inválido (cash o transfer).");

        if (items.Count == 0 && lines.Count == 0 && payAmount is null)
            throw new ArgumentException("Registra al menos un producto, un envase o un cobro.");

        if (lines.Count > 0)
        {
            var typeIds = lines.Select(l => l.ContainerTypeId).ToList();
            var valid = await _db.ContainerTypes.CountAsync(t => t.AffiliateId == affiliateId && t.IsActive && typeIds.Contains(t.Id));
            if (valid != typeIds.Count) throw new ArgumentException("Algún tipo de envase no existe o está desactivado.");
        }
        if (r.AnaquelId.HasValue)
        {
            var anaquel = await _db.Anaqueles.AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == r.AnaquelId.Value && a.AffiliateId == affiliateId)
                ?? throw new ArgumentException("El anaquel no existe en este negocio.");
            if (anaquel.CustomerId != stop.CustomerId)
                throw new ArgumentException("El anaquel no pertenece al cliente de esta parada.");
        }

        var now = DateTime.UtcNow;
        var at = r.DeliveredAt.HasValue ? NormalizeUtc(r.DeliveredAt.Value) : now;
        if (at > now.AddMinutes(5)) throw new ArgumentException("La fecha de la entrega no puede ser futura.");
        var who = actorName ?? actorId;

        var delivery = new Delivery
        {
            AffiliateId = affiliateId,
            RouteStopId = stop.Id,
            CustomerId = stop.CustomerId,
            DeliveredAt = at,
            ReceivedBy = string.IsNullOrWhiteSpace(r.ReceivedBy) ? null : r.ReceivedBy.Trim(),
            Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim(),
            RegisteredBy = who,
            IdempotencyKey = key,
        };
        foreach (var i in items)
            delivery.Items.Add(new DeliveryItem
            {
                ItemId = i.ItemId,
                Description = i.Description.Trim(),
                Quantity = i.Quantity,
                UnitPrice = i.UnitPrice,
            });
        _db.Deliveries.Add(delivery);

        foreach (var l in lines)
        {
            void Add(string type, int qty)
            {
                if (qty <= 0) return;
                _db.ContainerMovements.Add(new ContainerMovement
                {
                    AffiliateId = affiliateId,
                    CustomerId = stop.CustomerId,
                    ContainerTypeId = l.ContainerTypeId,
                    AnaquelId = r.AnaquelId,
                    DeliveryId = delivery.Id,
                    Type = type,
                    Quantity = qty,
                    OccurredAt = at,
                    RegisteredBy = who,
                });
            }
            Add("delivery", l.Delivered);
            Add("return", l.Returned);
            Add("damaged", l.Damaged);
        }

        Payment? payment = null;
        if (payAmount.HasValue)
        {
            // Cobro en ruta = abono a cuenta (sin factura): el saldo y la antigüedad lo aplican en orden FIFO.
            payment = new Payment
            {
                AffiliateId = affiliateId,
                CustomerId = stop.CustomerId,
                Amount = payAmount.Value,
                Method = payMethod,
                PaidAt = at,
                Notes = $"Cobro en ruta: {route.Name}",
                RegisteredBy = who,
                IdempotencyKey = key is null ? null : $"{key}:pay",
            };
            _db.Payments.Add(payment);
            delivery.PaymentId = payment.Id;
        }

        // Cliente de contado (0 días de crédito explícitos en su contrato): la entrega se factura en el momento y el cobro
        // se liga a esa factura. Con crédito (o sin dato) las entregas se acumulan y se facturan por ciclo.
        Invoice? invoice = null;
        if (delivery.Items.Count > 0)
        {
            var creditDays = await _db.Customers.AsNoTracking()
                .Where(c => c.Id == stop.CustomerId).Select(c => c.CreditDays).FirstOrDefaultAsync();
            if (creditDays == 0)
            {
                var invoiceLines = DeliveryInvoicing.BuildLines(delivery.Items);
                var linesTotal = invoiceLines.Sum(l => l.Total);
                var taxSettings = await ReceivablesSettingsService.LoadAsync(_db, affiliateId);
                var (subtotal, taxAmount, invoiceTotal) = DeliveryInvoicing.ApplyTax(linesTotal, taxSettings.TaxMode, taxSettings.TaxRate);
                if (linesTotal > 0)
                {
                    invoice = new Invoice
                    {
                        Id = Guid.NewGuid(),
                        AffiliateId = affiliateId,
                        CustomerId = stop.CustomerId,
                        InvoiceNumber = DeliveryInvoicing.NewInvoiceNumber(),
                        Subtotal = subtotal,
                        Tax = taxAmount,
                        Total = invoiceTotal,
                        Status = "Pending",
                        IssueDate = at,
                        DueDate = at,
                        Notes = $"Entrega en ruta: {route.Name}",
                        CreatedAt = now,
                        CreatedBy = actorId,
                        Items = invoiceLines,
                    };
                    _db.Invoices.Add(invoice);
                    delivery.InvoiceId = invoice.Id;
                    if (payment is not null)
                    {
                        payment.InvoiceId = invoice.Id;
                        if (payment.Amount >= invoice.Total)
                        {
                            invoice.Status = "Paid";
                            invoice.PaidDate = at;
                        }
                    }
                }
            }
        }

        stop.Status = "visited";
        stop.VisitedAt = at;

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException) when (key is not null)
        {
            // Dos reintentos simultáneos con la misma clave: el índice único dejó pasar solo uno → devuelve el ganador.
            _db.ChangeTracker.Clear();
            var winner = await FindDeliveryByKeyAsync(affiliateId, key);
            if (winner is null) throw;
            return await DuplicateResultAsync(affiliateId, winner);
        }

        if (invoice is not null)
            await _audit.LogAsync(affiliateId, "invoice.created", "Invoice", invoice.Id,
                $"Factura {invoice.InvoiceNumber} por {invoice.Total:N2}: entrega de contado en ruta {route.Name}", actorId, actorName);

        var balance = await _containers.GetBalanceAsync(affiliateId, stop.CustomerId);
        return new RegisterDeliveryResultDto(ToDto(delivery, payment?.Amount, invoice?.InvoiceNumber), false, balance);
    }

    public async Task<RouteDto?> SkipStopAsync(Guid affiliateId, Guid routeId, Guid stopId, SkipStopRequest request, string? actorId, Guid? restrictDriverId = null)
    {
        if (request.Status is not ("skipped" or "failed"))
            throw new ArgumentException("Estado inválido (skipped o failed).");
        var route = await LoadRouteAsync(affiliateId, routeId, track: true, restrictDriverId);
        var stop = route?.Stops.FirstOrDefault(s => s.Id == stopId);
        if (route is null || stop is null) return null;
        if (route.Status != "in_progress")
            throw new InvalidOperationException("La ruta no está en curso.");
        if (stop.Status != "pending")
            throw new InvalidOperationException("Esa parada ya fue atendida.");

        stop.Status = request.Status;
        stop.VisitedAt = DateTime.UtcNow;
        stop.Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim();
        route.UpdatedAt = DateTime.UtcNow;
        route.UpdatedBy = actorId;
        await _db.SaveChangesAsync();
        return await GetRouteAsync(affiliateId, routeId);
    }

    // ── Conductor (PWA) ──────────────────────────────────────────────────────

    public async Task<Guid?> FindDriverIdByEmailAsync(Guid affiliateId, string? email)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        var e = email.Trim().ToLowerInvariant();
        var id = await _db.TeamMembers.AsNoTracking()
            .Where(t => t.AffiliateId == affiliateId && t.IsActive && t.Email != null && t.Email.ToLower() == e)
            .Select(t => (Guid?)t.Id).FirstOrDefaultAsync();
        return id;
    }

    public async Task<IReadOnlyList<RouteDto>> GetMyRoutesAsync(Guid affiliateId, Guid driverId, DateOnly? date)
    {
        var day = date ?? DeliveryInvoicing.Today();
        var list = await _db.DeliveryRoutes.AsNoTracking().Include(r => r.Vehicle).Include(r => r.Stops)
            .Where(r => r.AffiliateId == affiliateId && r.DriverId == driverId && r.RouteDate == day
                && (r.Status == "planned" || r.Status == "loading" || r.Status == "in_progress"))
            .OrderBy(r => r.Name).ToListAsync();
        var names = await DriverNamesAsync(affiliateId, list.Select(r => r.DriverId));
        return list.Select(r => ToDto(r, names, includeStops: false, customers: null)).ToList();
    }

    public async Task<DriverSheetDto?> GetDriverSheetAsync(Guid affiliateId, Guid routeId, Guid? restrictDriverId = null)
    {
        var route = await LoadRouteAsync(affiliateId, routeId, track: false, restrictDriverId);
        if (route is null) return null;

        var customerIds = route.Stops.Select(s => s.CustomerId).Distinct().ToList();
        var customers = await _db.Customers.AsNoTracking()
            .Where(c => c.AffiliateId == affiliateId && customerIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);

        // Envases en poder de cada cliente.
        var typeNames = await _db.ContainerTypes.AsNoTracking().Where(t => t.AffiliateId == affiliateId)
            .ToDictionaryAsync(t => t.Id, t => t.Name);
        var moves = await _db.ContainerMovements.AsNoTracking()
            .Where(m => m.AffiliateId == affiliateId && customerIds.Contains(m.CustomerId))
            .GroupBy(m => new { m.CustomerId, m.ContainerTypeId })
            .Select(g => new
            {
                g.Key.CustomerId,
                g.Key.ContainerTypeId,
                Delivered = g.Where(m => m.Type == "delivery").Sum(m => m.Quantity),
                Returned = g.Where(m => m.Type == "return").Sum(m => m.Quantity),
                Damaged = g.Where(m => m.Type == "damaged").Sum(m => m.Quantity),
                Adjustments = g.Where(m => m.Type == "adjustment").Sum(m => m.Quantity),
            }).ToListAsync();
        var balanceByCustomer = moves.GroupBy(m => m.CustomerId).ToDictionary(
            g => g.Key,
            g => (IReadOnlyList<ContainerBalanceItemDto>)g.Select(m => new ContainerBalanceItemDto(
                m.ContainerTypeId, typeNames.TryGetValue(m.ContainerTypeId, out var n) ? n : "—",
                m.Delivered, m.Returned, m.Damaged, m.Adjustments,
                m.Delivered - m.Returned - m.Damaged + m.Adjustments)).OrderBy(x => x.ContainerTypeName).ToList());

        var anaquelesByCustomer = (await _db.Anaqueles.AsNoTracking()
                .Where(a => a.AffiliateId == affiliateId && a.CustomerId != null && customerIds.Contains(a.CustomerId.Value)
                    && a.Status != "fuera_servicio")
                .ToListAsync())
            .GroupBy(a => a.CustomerId!.Value)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DriverAnaquelDto>)g.Select(a => new DriverAnaquelDto(a.Id, a.QrCode, a.Label, a.Location)).ToList());

        // Saldo y facturas abiertas: la misma matemática que el estado de cuenta (BillingCalculator).
        var invoices = await _db.Invoices.AsNoTracking()
            .Where(i => i.AffiliateId == affiliateId && customerIds.Contains(i.CustomerId)).ToListAsync();
        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.AffiliateId == affiliateId && customerIds.Contains(p.CustomerId)).ToListAsync();
        var invByCustomer = invoices.GroupBy(i => i.CustomerId).ToDictionary(g => g.Key, g => g.ToList());
        var payByCustomer = payments.GroupBy(p => p.CustomerId).ToDictionary(g => g.Key, g => g.ToList());
        var asOf = DateTime.UtcNow;

        var contractByCustomer = (await _db.Contracts.AsNoTracking()
                .Where(c => c.AffiliateId == affiliateId && c.IsActive && customerIds.Contains(c.CustomerId))
                .ToListAsync())
            .GroupBy(c => c.CustomerId).ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.StartDate).First());

        // Lo ya registrado en cada parada (entregas, envases y cobros), para la hoja impresa.
        var stopIds = route.Stops.Select(s => s.Id).ToList();
        var deliveries = await _db.Deliveries.AsNoTracking().Include(d => d.Items)
            .Where(d => d.AffiliateId == affiliateId && stopIds.Contains(d.RouteStopId)).ToListAsync();
        var deliveryIds = deliveries.Select(d => d.Id).ToList();
        var paymentIds = deliveries.Where(d => d.PaymentId != null).Select(d => d.PaymentId!.Value).ToList();
        var deliveryMoves = new List<(Guid DeliveryId, string Type, int Quantity)>();
        if (deliveryIds.Count > 0)
        {
            var moveRows = await _db.ContainerMovements.AsNoTracking()
                .Where(m => m.AffiliateId == affiliateId && m.DeliveryId != null && deliveryIds.Contains(m.DeliveryId.Value))
                .Select(m => new { DeliveryId = m.DeliveryId!.Value, m.Type, m.Quantity }).ToListAsync();
            deliveryMoves = moveRows.Select(m => (m.DeliveryId, m.Type, m.Quantity)).ToList();
        }
        var paymentsById = paymentIds.Count == 0
            ? new Dictionary<Guid, Payment>()
            : await _db.Payments.AsNoTracking().Where(p => p.AffiliateId == affiliateId && paymentIds.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var resultByStop = deliveries.GroupBy(d => d.RouteStopId).ToDictionary(g => g.Key, g =>
        {
            var dIds = g.Select(d => d.Id).ToHashSet();
            var dItems = g.SelectMany(d => d.Items)
                .Select(i => new DeliveryItemDto(i.ItemId, i.Description, i.Quantity, i.UnitPrice, Math.Round(i.Quantity * i.UnitPrice, 2))).ToList();
            var stopMoves = deliveryMoves.Where(m => dIds.Contains(m.DeliveryId)).ToList();
            var stopPays = g.Where(d => d.PaymentId != null && paymentsById.ContainsKey(d.PaymentId.Value))
                .Select(d => paymentsById[d.PaymentId!.Value]).ToList();
            return new StopResultDto(
                dItems, dItems.Sum(i => i.LineTotal),
                stopMoves.Where(m => m.Type == "delivery").Sum(m => m.Quantity),
                stopMoves.Where(m => m.Type == "return").Sum(m => m.Quantity),
                stopMoves.Where(m => m.Type == "damaged").Sum(m => m.Quantity),
                stopPays.Sum(p => p.Amount),
                stopPays.Select(p => p.Method).Distinct().Count() == 1 ? stopPays[0].Method : (stopPays.Count > 1 ? "mixed" : null));
        });

        var stops = new List<DriverStopDto>();
        foreach (var s in route.Stops.OrderBy(s => s.SortOrder))
        {
            customers.TryGetValue(s.CustomerId, out var c);
            invByCustomer.TryGetValue(s.CustomerId, out var invs);
            payByCustomer.TryGetValue(s.CustomerId, out var pays);
            var billing = BillingCalculator.Compute(
                (invs ?? new()).Select(i => new BillingInvoice(i.Id, i.InvoiceNumber, i.Total, i.Status, i.IssueDate, i.DueDate, i.PaidDate, i.CreatedAt)),
                (pays ?? new()).Select(p => new BillingPayment(p.Id, p.InvoiceId, p.Amount, p.PaidAt, p.Method, p.Notes)),
                asOf);
            contractByCustomer.TryGetValue(s.CustomerId, out var contract);

            stops.Add(new DriverStopDto(
                new RouteStopDto(s.Id, s.CustomerId, c?.Name ?? "—", c?.Phone, c?.Address, s.SortOrder, s.Status, s.VisitedAt, s.Notes, s.ArrivedAt, s.DepartedAt, c?.GpsLat, c?.GpsLng),
                balanceByCustomer.TryGetValue(s.CustomerId, out var bal) ? bal : Array.Empty<ContainerBalanceItemDto>(),
                anaquelesByCustomer.TryGetValue(s.CustomerId, out var an) ? an : Array.Empty<DriverAnaquelDto>(),
                billing.Balance, billing.CreditOnAccount, billing.OpenInvoices.Any(o => o.IsOverdue), billing.OpenInvoices,
                contract?.ContractNumber, contract?.RacksLoaned ?? 0, contract?.BottlesLoaned ?? 0,
                contract?.CreditDays ?? c?.CreditDays,
                string.IsNullOrWhiteSpace(contract?.AgreedPrices) ? null : System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(contract!.AgreedPrices!),
                resultByStop.TryGetValue(s.Id, out var result) ? result : null));
        }

        var types = await GetContainerTypesForSheetAsync(affiliateId);
        return new DriverSheetDto(route.Id, route.Name, route.RouteDate, route.Status, route.Vehicle?.Plate, route.LoadedFull,
            stops, types, DateTime.UtcNow);
    }

    private async Task<IReadOnlyList<ContainerTypeDto>> GetContainerTypesForSheetAsync(Guid affiliateId) =>
        await _containers.GetTypesAsync(affiliateId, includeInactive: false);

    // ── helpers ──────────────────────────────────────────────────────────────

    private Task<DeliveryRoute?> LoadRouteAsync(Guid affiliateId, Guid routeId, bool track, Guid? restrictDriverId = null)
    {
        var q = _db.DeliveryRoutes.Include(r => r.Vehicle).Include(r => r.Stops).AsQueryable();
        if (restrictDriverId.HasValue) q = q.Where(r => r.DriverId == restrictDriverId.Value);
        if (!track) q = q.AsNoTracking();
        return q.FirstOrDefaultAsync(r => r.Id == routeId && r.AffiliateId == affiliateId);
    }

    private Task<Delivery?> FindDeliveryByKeyAsync(Guid affiliateId, string key) =>
        _db.Deliveries.AsNoTracking().Include(d => d.Items)
            .FirstOrDefaultAsync(d => d.AffiliateId == affiliateId && d.IdempotencyKey == key);

    private async Task<RegisterDeliveryResultDto> DuplicateResultAsync(Guid affiliateId, Delivery existing)
    {
        decimal? paid = null;
        if (existing.PaymentId.HasValue)
            paid = await _db.Payments.AsNoTracking().Where(p => p.Id == existing.PaymentId.Value).Select(p => (decimal?)p.Amount).FirstOrDefaultAsync();
        string? invoiceNumber = null;
        if (existing.InvoiceId.HasValue)
            invoiceNumber = await _db.Invoices.AsNoTracking().Where(i => i.Id == existing.InvoiceId.Value).Select(i => i.InvoiceNumber).FirstOrDefaultAsync();
        var balance = await _containers.GetBalanceAsync(affiliateId, existing.CustomerId);
        return new RegisterDeliveryResultDto(ToDto(existing, paid, invoiceNumber), true, balance);
    }

    private async Task<Dictionary<Guid, string>> DriverNamesAsync(Guid affiliateId, IEnumerable<Guid?> ids)
    {
        var wanted = ids.Where(i => i.HasValue).Select(i => i!.Value).Distinct().ToList();
        if (wanted.Count == 0) return new();
        return await _db.TeamMembers.AsNoTracking()
            .Where(t => t.AffiliateId == affiliateId && wanted.Contains(t.Id))
            .ToDictionaryAsync(t => t.Id, t => t.Name);
    }

    private async Task EnsureDriverAsync(Guid affiliateId, Guid? driverId)
    {
        if (!driverId.HasValue) return;
        var ok = await _db.TeamMembers.AnyAsync(t => t.Id == driverId.Value && t.AffiliateId == affiliateId && t.IsActive);
        if (!ok) throw new ArgumentException("El conductor no existe en este negocio o está inactivo.");
    }

    private async Task ValidateVehicleAsync(Guid affiliateId, UpsertVehicleRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Plate) || r.Plate.Trim().Length > 20)
            throw new ArgumentException("La placa es obligatoria (máx. 20 caracteres).");
        if (r.Name is { Length: > 100 }) throw new ArgumentException("El nombre no puede pasar de 100 caracteres.");
        if (r.CapacityContainers is < 0 or > 100_000) throw new ArgumentException("Capacidad fuera de rango.");
        if (r.Status is not null && !VehicleStatuses.Contains(r.Status)) throw new ArgumentException("Estado de vehículo inválido.");
        await EnsureDriverAsync(affiliateId, r.DriverId);
    }

    private static void ApplyVehicle(Vehicle v, UpsertVehicleRequest r)
    {
        v.Plate = r.Plate.Trim().ToUpperInvariant();
        v.Name = string.IsNullOrWhiteSpace(r.Name) ? null : r.Name.Trim();
        v.CapacityContainers = r.CapacityContainers;
        v.DriverId = r.DriverId;
        v.Status = r.Status ?? v.Status;
        v.IsActive = r.IsActive;
        v.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
    }

    /// <summary>Valida el request y devuelve el vehículo y el conductor efectivo (el del request o, si falta, el del vehículo).</summary>
    private async Task<(Vehicle? vehicle, Guid? driverId)> ValidateRouteAsync(Guid affiliateId, UpsertRouteRequest r)
    {
        if (string.IsNullOrWhiteSpace(r.Name) || r.Name.Trim().Length > 100)
            throw new ArgumentException("El nombre de la ruta es obligatorio (máx. 100 caracteres).");
        Vehicle? vehicle = null;
        if (r.VehicleId.HasValue)
        {
            vehicle = await _db.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == r.VehicleId.Value && v.AffiliateId == affiliateId)
                ?? throw new ArgumentException("El vehículo no existe en este negocio.");
            if (!vehicle.IsActive || vehicle.Status == "mantenimiento")
                throw new ArgumentException("El vehículo está inactivo o en mantenimiento.");
        }
        var driverId = r.DriverId ?? vehicle?.DriverId;
        await EnsureDriverAsync(affiliateId, driverId);
        return (vehicle, driverId);
    }

    private static void ApplyRoute(DeliveryRoute route, UpsertRouteRequest r, Vehicle? vehicle, Guid? driverId)
    {
        route.Name = r.Name.Trim();
        route.RouteDate = r.RouteDate;
        route.VehicleId = vehicle?.Id;
        route.DriverId = driverId;
        route.Notes = string.IsNullOrWhiteSpace(r.Notes) ? null : r.Notes.Trim();
    }

    /// <summary>Deja las paradas igual a la lista (y su orden): quita las que sobran, agrega las nuevas, reordena.</summary>
    private async Task ReplaceStopsAsync(Guid affiliateId, DeliveryRoute route, IReadOnlyList<Guid>? customerIds)
    {
        var wanted = (customerIds ?? Array.Empty<Guid>()).ToList();
        if (wanted.Distinct().Count() != wanted.Count)
            throw new ArgumentException("Un cliente no puede estar dos veces en la misma ruta.");
        if (wanted.Count > 500) throw new ArgumentException("Una ruta no puede pasar de 500 paradas.");

        var found = await _db.Customers.AsNoTracking()
            .Where(c => c.AffiliateId == affiliateId && wanted.Contains(c.Id)).Select(c => c.Id).ToListAsync();
        if (found.Count != wanted.Count) throw new ArgumentException("Algún cliente de la lista no existe en este negocio.");

        foreach (var gone in route.Stops.Where(s => !wanted.Contains(s.CustomerId)).ToList())
        {
            route.Stops.Remove(gone);
            if (_db.Entry(gone).State != EntityState.Detached && _db.Entry(gone).State != EntityState.Added)
                _db.RouteStops.Remove(gone);
        }
        for (var i = 0; i < wanted.Count; i++)
        {
            var stop = route.Stops.FirstOrDefault(s => s.CustomerId == wanted[i]);
            if (stop is null)
            {
                stop = new RouteStop { AffiliateId = affiliateId, CustomerId = wanted[i] };
                route.Stops.Add(stop);
            }
            stop.SortOrder = i + 1;
        }
    }

    private static DateTime NormalizeUtc(DateTime d) =>
        d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime();

    private async Task<RouteDto> ToFullDtoAsync(Guid affiliateId, DeliveryRoute route)
    {
        var names = await DriverNamesAsync(affiliateId, new[] { route.DriverId });
        var ids = route.Stops.Select(s => s.CustomerId).ToList();
        var customers = await _db.Customers.AsNoTracking().Where(c => c.AffiliateId == affiliateId && ids.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id);
        return ToDto(route, names, includeStops: true, customers);
    }

    private static RouteDto ToDto(DeliveryRoute r, Dictionary<Guid, string> driverNames, bool includeStops, Dictionary<Guid, Customer>? customers)
    {
        var stops = includeStops
            ? r.Stops.OrderBy(s => s.SortOrder).Select(s =>
            {
                customers!.TryGetValue(s.CustomerId, out var c);
                return new RouteStopDto(s.Id, s.CustomerId, c?.Name ?? "—", c?.Phone, c?.Address, s.SortOrder, s.Status, s.VisitedAt, s.Notes, s.ArrivedAt, s.DepartedAt, c?.GpsLat, c?.GpsLng);
            }).ToList()
            : new List<RouteStopDto>();
        return new RouteDto(
            r.Id, r.Name, r.RouteDate, r.VehicleId, r.Vehicle?.Plate,
            r.DriverId, r.DriverId.HasValue && driverNames.TryGetValue(r.DriverId.Value, out var dn) ? dn : null,
            r.Status, r.LoadedFull, r.DepartedAt, r.ClosedAt, r.Notes,
            r.Stops.Count, r.Stops.Count(s => s.Status != "pending"), stops);
    }

    private static VehicleDto ToDto(Vehicle v, Dictionary<Guid, string> driverNames) =>
        new(v.Id, v.Plate, v.Name, v.CapacityContainers, v.DriverId,
            v.DriverId.HasValue && driverNames.TryGetValue(v.DriverId.Value, out var dn) ? dn : null,
            v.Status, v.IsActive, v.Notes);

    private static DeliveryDto ToDto(Delivery d, decimal? paymentAmount, string? invoiceNumber = null)
    {
        var items = d.Items.Select(i => new DeliveryItemDto(i.ItemId, i.Description, i.Quantity, i.UnitPrice, Math.Round(i.Quantity * i.UnitPrice, 2))).ToList();
        return new DeliveryDto(d.Id, d.RouteStopId, d.CustomerId, d.DeliveredAt, items, items.Sum(i => i.LineTotal),
            d.PaymentId, paymentAmount, d.ReceivedBy, d.Notes, d.InvoiceId, invoiceNumber);
    }
}
