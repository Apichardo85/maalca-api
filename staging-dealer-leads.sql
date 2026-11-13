START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110184047_AddDealerVehicleLeads') THEN
    CREATE TABLE "VehicleLeads" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "VehicleListingId" uuid,
        "VehicleSummary" character varying(200),
        "Kind" character varying(20) NOT NULL,
        "Name" character varying(100) NOT NULL,
        "Phone" character varying(30),
        "Email" character varying(200),
        "Message" character varying(1000),
        "Currency" character varying(3),
        "DownPayment" numeric(18,2),
        "TermMonths" integer,
        "MonthlyEstimate" numeric(18,2),
        "TradeMake" character varying(60),
        "TradeModel" character varying(80),
        "TradeYear" integer,
        "TradeMileage" integer,
        "TradeMileageUnit" character varying(2),
        "TradeCondition" character varying(20),
        "TradeImages" text,
        "PreferredAt" timestamp with time zone,
        "Status" character varying(20) NOT NULL,
        "InternalNotes" character varying(2000),
        "Source" character varying(20),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_VehicleLeads" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_VehicleLeads_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_VehicleLeads_VehicleListings_VehicleListingId" FOREIGN KEY ("VehicleListingId") REFERENCES "VehicleListings" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110184047_AddDealerVehicleLeads') THEN
    CREATE INDEX "IX_VehicleLeads_AffiliateId_Kind_VehicleListingId" ON "VehicleLeads" ("AffiliateId", "Kind", "VehicleListingId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110184047_AddDealerVehicleLeads') THEN
    CREATE INDEX "IX_VehicleLeads_AffiliateId_Status_CreatedAt" ON "VehicleLeads" ("AffiliateId", "Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110184047_AddDealerVehicleLeads') THEN
    CREATE INDEX "IX_VehicleLeads_VehicleListingId" ON "VehicleLeads" ("VehicleListingId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110184047_AddDealerVehicleLeads') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261110184047_AddDealerVehicleLeads', '8.0.0');
    END IF;
END $EF$;
COMMIT;

