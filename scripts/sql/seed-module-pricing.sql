-- seed-module-pricing.sql
-- Precios por módulo y por nivel de país (cotizador). IDEMPOTENTE: no pisa lo que ya editaste en /ops/precios.
-- Orden: 1) migración AddModulePricing (crea las tablas y columnas)  2) este script  3) API  4) web.
-- Para probar sin guardar, cambia el COMMIT del final por ROLLBACK.

BEGIN;

-- 1) Pesos de los módulos de pago (suma Growth = 11, suma Professional = 10)
INSERT INTO "ModulePricings" ("Token", "Weight") VALUES
  ('orders', 1.5), ('pos', 1.5), ('inventory', 1), ('shipping', 1), ('board', 1),
  ('reservations', 1), ('appointments', 1), ('kitchen', 1),
  ('station', 0.5), ('queue', 0.5), ('staff', 0.5), ('modifiers', 0.5),
  ('contracts', 2), ('routes', 2),
  ('invoices', 1), ('pricelists', 1), ('expenses', 1), ('quality', 1), ('proposals', 1), ('workforce', 1)
ON CONFLICT ("Token") DO NOTHING;

-- 2) Niveles de precio (siempre USD; el nivel baja el precio, no cambia la moneda)
INSERT INTO "PriceTiers" ("Tier", "Name", "Factor", "RoundTo") VALUES
  (1, 'Nivel 1 · Lista',               1.000, 0.01),
  (2, 'Nivel 2 · RD y Latinoamérica',  0.550, 5.00),
  (3, 'Nivel 3 · Reservado',           0.400, 5.00)
ON CONFLICT ("Tier") DO NOTHING;

-- 3) Países en Nivel 2 (solo RD por ahora; el resto de Latinoamérica lo agregas en /ops/precios)
INSERT INTO "CountryTiers" ("Country", "Tier") VALUES ('DO', 2)
ON CONFLICT ("Country") DO NOTHING;

-- 4) Ajustes generales (fila única)
INSERT INTO "PricingSettings"
  ("Id", "GrowthUsd", "ProfessionalUsd", "SoloMarkup", "CorporateMinUsd", "SetupBaseUsd",
   "SetupGrowthPerWeightUsd", "SetupProPerWeightUsd", "IncludedUsers", "IncludedClients", "ExtraLocationFactor")
VALUES (1, 38, 95, 1.3, 350, 150, 40, 120, 3, 100, 0.5)
ON CONFLICT ("Id") DO NOTHING;

-- 5) Cargos por unidad y soporte corporativo (se editan en /ops/precios como los demás extras;
--    no salen en /servicios porque su key empieza con unit-, corp- o data-import)
INSERT INTO "PlatformAddOns" ("Id", "Key", "NameEs", "NameEn", "DescEs", "DescEn", "Price", "VolumePrice", "VolumeFromQty", "Unit", "SortOrder", "Active", "CreatedAt", "ApplyTierDiscount")
VALUES
  ('a1d0c0de-0000-4000-8000-000000000101', 'unit-vehicle',     'Vehículo de reparto',               'Delivery vehicle',            'Cada vehículo activo con rutas y hoja del conductor.',               'Each active vehicle with routes and driver sheet.',          10,  NULL, NULL, 1, 101, TRUE, now(), TRUE),
  ('a1d0c0de-0000-4000-8000-000000000102', 'unit-user',        'Usuario del equipo adicional',      'Extra team user',             'Cada usuario del equipo sobre los incluidos.',                        'Each team user above the included ones.',                    5,   NULL, NULL, 1, 102, TRUE, now(), TRUE),
  ('a1d0c0de-0000-4000-8000-000000000103', 'unit-clients-100', 'Bloque de 100 clientes con contrato', 'Block of 100 contract clients', 'Cada 100 clientes con contrato sobre los incluidos.',                'Each 100 contract clients above the included ones.',         10,  NULL, NULL, 1, 103, TRUE, now(), TRUE),
  ('a1d0c0de-0000-4000-8000-000000000104', 'corp-support',     'Soporte corporativo',               'Corporate support',           'Soporte prioritario y acompañamiento para clientes corporativos.',   'Priority support and guidance for corporate clients.',       150, NULL, NULL, 1, 104, TRUE, now(), TRUE),
  ('a1d0c0de-0000-4000-8000-000000000105', 'data-import-100',  'Importación de 100 clientes',       'Import of 100 customers',     'Carga inicial de clientes desde Excel, por bloques de 100.',          'Initial customer load from Excel, per block of 100.',        25,  NULL, NULL, 0, 105, TRUE, now(), TRUE)
ON CONFLICT ("Key") DO NOTHING;

-- El Kit de pantalla es hardware: el nivel de país no lo rebaja
UPDATE "PlatformAddOns" SET "ApplyTierDiscount" = FALSE WHERE "Key" = 'screen-kit';

-- 6) Nueva Era: cliente corporativo con 1 local
UPDATE "Affiliates"
   SET "IsCorporate" = TRUE, "Locations" = 1
 WHERE "Slug" = 'f-brica-de-agua-y-hielo-nueva-era';

-- Solo si no tiene país, se pone RD (el nivel de precio sale de ahí)
UPDATE "Affiliates"
   SET "Country" = 'DO'
 WHERE "Slug" = 'f-brica-de-agua-y-hielo-nueva-era'
   AND ("Country" IS NULL OR "Country" = '');

-- Verificación
SELECT 'Pesos Growth (debe dar 11)' AS "Chequeo", SUM("Weight") AS "Valor"
  FROM "ModulePricings" WHERE "Token" IN ('orders','pos','inventory','shipping','board','reservations','appointments','kitchen','station','queue','staff','modifiers')
UNION ALL
SELECT 'Pesos Professional (debe dar 10)', SUM("Weight")
  FROM "ModulePricings" WHERE "Token" IN ('contracts','routes','invoices','pricelists','expenses','quality','proposals','workforce')
UNION ALL SELECT 'Niveles', COUNT(*) FROM "PriceTiers"
UNION ALL SELECT 'Países en Nivel 2', COUNT(*) FROM "CountryTiers" WHERE "Tier" = 2
UNION ALL SELECT 'Extras de unidades', COUNT(*) FROM "PlatformAddOns" WHERE "Key" LIKE 'unit-%' OR "Key" LIKE 'corp-%' OR "Key" LIKE 'data-import%';

SELECT "Slug", "Country", "Currency", "IsCorporate", "Locations" FROM "Affiliates" WHERE "Slug" = 'f-brica-de-agua-y-hielo-nueva-era';

COMMIT;
