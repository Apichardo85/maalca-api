START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110003142_AddModulePricing') THEN
    ALTER TABLE "PlatformAddOns" ADD "ApplyTierDiscount" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110003142_AddModulePricing') THEN
    ALTER TABLE "Affiliates" ADD "IsCorporate" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110003142_AddModulePricing') THEN
    ALTER TABLE "Affiliates" ADD "Locations" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110003142_AddModulePricing') THEN
    CREATE TABLE "CountryTiers" (
        "Country" character varying(2) NOT NULL,
        "Tier" integer NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" character varying(255),
        CONSTRAINT "PK_CountryTiers" PRIMARY KEY ("Country")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110003142_AddModulePricing') THEN
    CREATE TABLE "ModulePricings" (
        "Token" character varying(40) NOT NULL,
        "Weight" numeric(6,2) NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" character varying(255),
        CONSTRAINT "PK_ModulePricings" PRIMARY KEY ("Token")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110003142_AddModulePricing') THEN
    CREATE TABLE "PriceTiers" (
        "Tier" integer NOT NULL,
        "Name" character varying(60) NOT NULL,
        "Factor" numeric(5,3) NOT NULL,
        "RoundTo" numeric(6,2) NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" character varying(255),
        CONSTRAINT "PK_PriceTiers" PRIMARY KEY ("Tier")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110003142_AddModulePricing') THEN
    CREATE TABLE "PricingSettings" (
        "Id" integer NOT NULL,
        "GrowthUsd" numeric(10,2) NOT NULL,
        "ProfessionalUsd" numeric(10,2) NOT NULL,
        "SoloMarkup" numeric(5,2) NOT NULL,
        "CorporateMinUsd" numeric(10,2) NOT NULL,
        "SetupBaseUsd" numeric(10,2) NOT NULL,
        "SetupGrowthPerWeightUsd" numeric(10,2) NOT NULL,
        "SetupProPerWeightUsd" numeric(10,2) NOT NULL,
        "IncludedUsers" integer NOT NULL,
        "IncludedClients" integer NOT NULL,
        "ExtraLocationFactor" numeric(5,2) NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" character varying(255),
        CONSTRAINT "PK_PricingSettings" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261110003142_AddModulePricing') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261110003142_AddModulePricing', '8.0.0');
    END IF;
END $EF$;
COMMIT;

