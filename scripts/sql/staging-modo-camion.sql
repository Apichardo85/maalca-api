-- Modo camión (migración 20261108180702_AddRouteTracking). Idempotente: si ya se aplicó, no hace nada.
-- Requiere que 20261108170736_AddWaterQuality ya esté aplicada.
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108180702_AddRouteTracking') THEN
    ALTER TABLE "RouteStops" ADD "ArrivalLat" numeric(9,6);
    ALTER TABLE "RouteStops" ADD "ArrivalLng" numeric(9,6);
    ALTER TABLE "RouteStops" ADD "ArrivedAt" timestamp with time zone;
    ALTER TABLE "RouteStops" ADD "DepartedAt" timestamp with time zone;

    CREATE TABLE "VehiclePositions" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "RouteId" uuid NOT NULL,
        "Lat" numeric(9,6) NOT NULL,
        "Lng" numeric(9,6) NOT NULL,
        "AccuracyMeters" double precision,
        "SpeedKmh" double precision,
        "RecordedAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_VehiclePositions" PRIMARY KEY ("Id")
    );

    CREATE INDEX "IX_VehiclePositions_AffiliateId" ON "VehiclePositions" ("AffiliateId");
    CREATE UNIQUE INDEX "IX_VehiclePositions_RouteId" ON "VehiclePositions" ("RouteId");

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108180702_AddRouteTracking', '8.0.0');
    END IF;
END $EF$;

COMMIT;
