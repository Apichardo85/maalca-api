-- Calidad del agua (migración 20261108170736_AddWaterQuality). Idempotente: si ya se aplicó, no hace nada.
-- Requiere que 20261108020311_AddReceivablesSettings ya esté aplicada.
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108170736_AddWaterQuality') THEN
    CREATE TABLE "WaterMaintenanceLogs" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "LogDate" date NOT NULL,
        "Kind" character varying(20) NOT NULL,
        "Description" character varying(300) NOT NULL,
        "PerformedBy" character varying(120),
        "IsPublic" boolean NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_WaterMaintenanceLogs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_WaterMaintenanceLogs_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );

    CREATE TABLE "WaterQualityTests" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "TestDate" date NOT NULL,
        "LabName" character varying(150) NOT NULL,
        "TestType" character varying(20) NOT NULL,
        "Sample" character varying(200),
        "Result" character varying(20) NOT NULL,
        "ParametersJson" text,
        "CertificateUrl" character varying(500),
        "Notes" text,
        "IsPublic" boolean NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_WaterQualityTests" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_WaterQualityTests_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );

    CREATE INDEX "IX_WaterMaintenanceLogs_AffiliateId_LogDate" ON "WaterMaintenanceLogs" ("AffiliateId", "LogDate");
    CREATE INDEX "IX_WaterQualityTests_AffiliateId_TestDate" ON "WaterQualityTests" ("AffiliateId", "TestDate");

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108170736_AddWaterQuality', '8.0.0');
    END IF;
END $EF$;

COMMIT;
