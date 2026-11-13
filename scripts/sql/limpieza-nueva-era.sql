-- Limpieza de datos de prueba operativos de Nueva Era (slug f-brica-de-agua-y-hielo-nueva-era).
-- BORRA: entregas, movimientos de envases, rutas y paradas, posiciones GPS, anaqueles, pagos,
--        contratos, facturas, pedidos, vehiculos y clientes de ESE negocio. NO se puede deshacer tras COMMIT.
-- NO TOCA: el negocio, usuarios/invitaciones, equipo (TeamMembers), productos, tipos de envase,
--          plantilla de contrato, ajustes de cobro, StoreCustomers (cuentas del portal).
-- Se ejecuta como ENSAYO: termina en ROLLBACK y muestra cuantas filas borraria. Para aplicar de verdad,
-- cambia la ultima linea ROLLBACK por COMMIT y ejecuta de nuevo (todo el script junto, Alt+Shift+Enter).
START TRANSACTION;

CREATE TEMP TABLE _a ON COMMIT DROP AS
  SELECT "Id" FROM "Affiliates" WHERE "Slug" = 'f-brica-de-agua-y-hielo-nueva-era';
DO $$ BEGIN
  IF (SELECT count(*) FROM _a) <> 1 THEN RAISE EXCEPTION 'No se encontro exactamente 1 negocio con ese slug'; END IF;
END $$;

CREATE TEMP TABLE _log (tabla text, filas int) ON COMMIT DROP;

WITH d AS (DELETE FROM "DeliveryItems" WHERE "DeliveryId" IN (SELECT "Id" FROM "Deliveries" WHERE "AffiliateId" IN (SELECT "Id" FROM _a)) RETURNING 1)
  INSERT INTO _log SELECT 'DeliveryItems', count(*) FROM d;
WITH d AS (DELETE FROM "ContainerMovements" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'ContainerMovements', count(*) FROM d;
WITH d AS (DELETE FROM "Deliveries" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'Deliveries', count(*) FROM d;
WITH d AS (DELETE FROM "VehiclePositions" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'VehiclePositions', count(*) FROM d;
UPDATE "Customers" SET "RouteId" = NULL WHERE "AffiliateId" IN (SELECT "Id" FROM _a) AND "RouteId" IS NOT NULL;
WITH d AS (DELETE FROM "RouteStops" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'RouteStops', count(*) FROM d;
WITH d AS (DELETE FROM "DeliveryRoutes" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'DeliveryRoutes', count(*) FROM d;
WITH d AS (DELETE FROM "Anaqueles" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'Anaqueles', count(*) FROM d;
WITH d AS (DELETE FROM "Payments" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'Payments', count(*) FROM d;
WITH d AS (DELETE FROM "Contracts" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'Contracts', count(*) FROM d;
WITH d AS (DELETE FROM "Orders" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'Orders', count(*) FROM d;
WITH d AS (DELETE FROM "InvoiceItems" WHERE "InvoiceId" IN (SELECT "Id" FROM "Invoices" WHERE "AffiliateId" IN (SELECT "Id" FROM _a)) RETURNING 1)
  INSERT INTO _log SELECT 'InvoiceItems', count(*) FROM d;
WITH d AS (DELETE FROM "Invoices" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'Invoices', count(*) FROM d;
WITH d AS (DELETE FROM "Vehicles" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'Vehicles', count(*) FROM d;
WITH d AS (DELETE FROM "Customers" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'Customers', count(*) FROM d;

SELECT * FROM _log ORDER BY tabla;

ROLLBACK;
