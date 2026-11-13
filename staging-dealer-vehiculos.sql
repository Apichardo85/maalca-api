START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110181807_AddDealerVehicleListings') THEN
    CREATE TABLE "VehicleListings" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Make" character varying(60) NOT NULL,
        "Model" character varying(80) NOT NULL,
        "Trim" character varying(80),
        "Year" integer NOT NULL,
        "Condition" character varying(20) NOT NULL,
        "Price" numeric(18,2) NOT NULL,
        "CompareAtPrice" numeric(18,2),
        "Mileage" integer,
        "MileageUnit" character varying(2) NOT NULL,
        "Transmission" character varying(20),
        "FuelType" character varying(20),
        "BodyType" character varying(20),
        "Drivetrain" character varying(10),
        "ExteriorColor" character varying(40),
        "InteriorColor" character varying(40),
        "Engine" character varying(60),
        "Doors" integer,
        "Passengers" integer,
        "Vin" character varying(20),
        "StockNumber" character varying(40),
        "ImportOrigin" character varying(20),
        "Status" character varying(20) NOT NULL,
        "Featured" boolean NOT NULL,
        "IsPubliclyVisible" boolean NOT NULL,
        "SortOrder" integer NOT NULL,
        "IsDemo" boolean NOT NULL,
        "ImageUrl" character varying(500),
        "Images" text,
        "Description" text,
        "DescriptionEn" character varying(2000),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_VehicleListings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_VehicleListings_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110181807_AddDealerVehicleListings') THEN
    CREATE INDEX "IX_VehicleListings_AffiliateId_BodyType" ON "VehicleListings" ("AffiliateId", "BodyType");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110181807_AddDealerVehicleListings') THEN
    CREATE INDEX "IX_VehicleListings_AffiliateId_IsPubliclyVisible_Status" ON "VehicleListings" ("AffiliateId", "IsPubliclyVisible", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110181807_AddDealerVehicleListings') THEN
    CREATE INDEX "IX_VehicleListings_AffiliateId_Make_Model" ON "VehicleListings" ("AffiliateId", "Make", "Model");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110181807_AddDealerVehicleListings') THEN
    CREATE INDEX "IX_VehicleListings_AffiliateId_Price" ON "VehicleListings" ("AffiliateId", "Price");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110181807_AddDealerVehicleListings') THEN
    CREATE INDEX "IX_VehicleListings_AffiliateId_Year" ON "VehicleListings" ("AffiliateId", "Year");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110181807_AddDealerVehicleListings') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261110181807_AddDealerVehicleListings', '8.0.0');
    END IF;
END $EF$;
COMMIT;

