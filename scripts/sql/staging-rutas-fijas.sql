START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110042032_AddRouteTemplates') THEN
    ALTER TABLE "DeliveryRoutes" ADD "TemplateId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110042032_AddRouteTemplates') THEN
    CREATE TABLE "RouteTemplates" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "VehicleId" uuid,
        "DriverId" uuid,
        "DaysMask" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_RouteTemplates" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RouteTemplates_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_RouteTemplates_Vehicles_VehicleId" FOREIGN KEY ("VehicleId") REFERENCES "Vehicles" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110042032_AddRouteTemplates') THEN
    CREATE TABLE "RouteTemplateStops" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "TemplateId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "SortOrder" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_RouteTemplateStops" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RouteTemplateStops_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RouteTemplateStops_RouteTemplates_TemplateId" FOREIGN KEY ("TemplateId") REFERENCES "RouteTemplates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110042032_AddRouteTemplates') THEN
    CREATE UNIQUE INDEX "IX_DeliveryRoutes_TemplateId_RouteDate" ON "DeliveryRoutes" ("TemplateId", "RouteDate") WHERE "TemplateId" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110042032_AddRouteTemplates') THEN
    CREATE INDEX "IX_RouteTemplates_AffiliateId" ON "RouteTemplates" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110042032_AddRouteTemplates') THEN
    CREATE INDEX "IX_RouteTemplates_VehicleId" ON "RouteTemplates" ("VehicleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110042032_AddRouteTemplates') THEN
    CREATE INDEX "IX_RouteTemplateStops_CustomerId" ON "RouteTemplateStops" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110042032_AddRouteTemplates') THEN
    CREATE INDEX "IX_RouteTemplateStops_TemplateId_SortOrder" ON "RouteTemplateStops" ("TemplateId", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110042032_AddRouteTemplates') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261110042032_AddRouteTemplates', '8.0.0');
    END IF;
END $EF$;
COMMIT;

