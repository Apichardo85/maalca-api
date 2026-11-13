CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "Affiliates" (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        "Description" text,
        "Logo" text,
        "PrimaryColor" text,
        "SecondaryColor" text,
        "HeroImage" text,
        "Modules" text NOT NULL,
        "Features" text NOT NULL,
        "Settings" text NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Affiliates" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "AgentExecutions" (
        "Id" uuid NOT NULL,
        "IssueNumber" integer NOT NULL,
        "IssueTitle" text NOT NULL,
        "Repo" text NOT NULL,
        "AgentRole" text NOT NULL,
        "ModelUsed" text NOT NULL,
        "Tier" text NOT NULL,
        "TokensInput" integer NOT NULL,
        "TokensOutput" integer NOT NULL,
        "CostUsd" numeric(18,8) NOT NULL,
        "DurationMs" bigint NOT NULL,
        "Status" text NOT NULL,
        "RetryCount" integer NOT NULL,
        "ErrorMessage" text,
        "PrUrl" text,
        "BranchName" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_AgentExecutions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "Leads" (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        "Email" text NOT NULL,
        "Phone" text,
        "Country" text,
        "Source" text NOT NULL,
        "PropertyId" text,
        "ProjectType" text,
        "Message" text,
        "Status" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Leads" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "Campaigns" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" text NOT NULL,
        "Type" text NOT NULL,
        "TargetAudience" text,
        "Content" text,
        "Schedule" timestamp with time zone,
        "Status" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Campaigns" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Campaigns_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "Customers" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" text NOT NULL,
        "Email" text,
        "Phone" text,
        "Notes" text,
        "Status" text NOT NULL,
        "LastVisit" timestamp with time zone,
        "TotalVisits" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Customers" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Customers_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "GiftCards" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Code" text NOT NULL,
        "InitialAmount" numeric(18,2) NOT NULL,
        "Balance" numeric(18,2) NOT NULL,
        "RecipientEmail" text,
        "Message" text,
        "Status" text NOT NULL,
        "ExpiresAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_GiftCards" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_GiftCards_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "InventoryItems" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" text NOT NULL,
        "Description" text,
        "Category" text NOT NULL,
        "Quantity" integer NOT NULL,
        "MinStock" integer NOT NULL,
        "UnitPrice" numeric(18,2) NOT NULL,
        "Status" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_InventoryItems" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_InventoryItems_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "Products" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" text NOT NULL,
        "Description" text,
        "Category" text NOT NULL,
        "Price" numeric(18,2) NOT NULL,
        "Stock" integer NOT NULL,
        "ImageUrl" text,
        "Status" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Products" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Products_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "Services" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" text NOT NULL,
        "Description" text,
        "Price" numeric(18,2) NOT NULL,
        "DurationMinutes" integer NOT NULL,
        "Category" text NOT NULL,
        "IsActive" boolean NOT NULL,
        "Status" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Services" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Services_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "TeamMembers" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" text NOT NULL,
        "Email" text,
        "Phone" text,
        "Role" text NOT NULL,
        "Department" text NOT NULL,
        "JoinDate" timestamp with time zone NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_TeamMembers" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TeamMembers_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "Users" (
        "Id" uuid NOT NULL,
        "Email" text NOT NULL,
        "PasswordHash" text NOT NULL,
        "AffiliateId" uuid,
        "Role" text NOT NULL,
        "FullName" text,
        "IsActive" boolean NOT NULL,
        "RefreshToken" text,
        "RefreshTokenExpiry" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Users" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Users_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "Invoices" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "InvoiceNumber" text NOT NULL,
        "Subtotal" numeric NOT NULL,
        "Tax" numeric NOT NULL,
        "Total" numeric(18,2) NOT NULL,
        "Status" text NOT NULL,
        "IssueDate" timestamp with time zone NOT NULL,
        "DueDate" timestamp with time zone,
        "PaidDate" timestamp with time zone,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Invoices" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Invoices_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Invoices_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "InventoryMovements" (
        "Id" uuid NOT NULL,
        "InventoryItemId" uuid NOT NULL,
        "Type" text NOT NULL,
        "Quantity" integer NOT NULL,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_InventoryMovements" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_InventoryMovements_InventoryItems_InventoryItemId" FOREIGN KEY ("InventoryItemId") REFERENCES "InventoryItems" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "Appointments" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "ServiceId" uuid NOT NULL,
        "Date" timestamp with time zone NOT NULL,
        "Time" text NOT NULL,
        "Status" text NOT NULL,
        "Notes" text,
        "AssignedToId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Appointments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Appointments_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Appointments_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Appointments_Services_ServiceId" FOREIGN KEY ("ServiceId") REFERENCES "Services" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Appointments_TeamMembers_AssignedToId" FOREIGN KEY ("AssignedToId") REFERENCES "TeamMembers" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "QueueEntries" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "DisplayName" text NOT NULL,
        "Phone" text,
        "ServiceId" uuid,
        "PreferredBarberId" uuid,
        "Notes" text,
        "Channel" text NOT NULL,
        "Position" integer NOT NULL,
        "Status" text NOT NULL,
        "AssignedToId" uuid,
        "CalledAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_QueueEntries" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_QueueEntries_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_QueueEntries_Services_ServiceId" FOREIGN KEY ("ServiceId") REFERENCES "Services" ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_QueueEntries_TeamMembers_AssignedToId" FOREIGN KEY ("AssignedToId") REFERENCES "TeamMembers" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE TABLE "InvoiceItems" (
        "Id" uuid NOT NULL,
        "InvoiceId" uuid NOT NULL,
        "Description" text NOT NULL,
        "Quantity" integer NOT NULL,
        "UnitPrice" numeric NOT NULL,
        "Total" numeric(18,2) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_InvoiceItems" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_InvoiceItems_Invoices_InvoiceId" FOREIGN KEY ("InvoiceId") REFERENCES "Invoices" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_AgentExecutions_CreatedAt" ON "AgentExecutions" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_AgentExecutions_IssueNumber" ON "AgentExecutions" ("IssueNumber");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_AgentExecutions_Status" ON "AgentExecutions" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_Appointments_AffiliateId" ON "Appointments" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_Appointments_AssignedToId" ON "Appointments" ("AssignedToId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_Appointments_CustomerId" ON "Appointments" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_Appointments_ServiceId" ON "Appointments" ("ServiceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_Campaigns_AffiliateId" ON "Campaigns" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_Customers_AffiliateId" ON "Customers" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_GiftCards_AffiliateId" ON "GiftCards" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE UNIQUE INDEX "IX_GiftCards_Code" ON "GiftCards" ("Code");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_InventoryItems_AffiliateId" ON "InventoryItems" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_InventoryMovements_InventoryItemId" ON "InventoryMovements" ("InventoryItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_InvoiceItems_InvoiceId" ON "InvoiceItems" ("InvoiceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_Invoices_AffiliateId" ON "Invoices" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_Invoices_CustomerId" ON "Invoices" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_Products_AffiliateId" ON "Products" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_QueueEntries_AffiliateId" ON "QueueEntries" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_QueueEntries_AssignedToId" ON "QueueEntries" ("AssignedToId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_QueueEntries_ServiceId" ON "QueueEntries" ("ServiceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_Services_AffiliateId" ON "Services" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_TeamMembers_AffiliateId" ON "TeamMembers" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE INDEX "IX_Users_AffiliateId" ON "Users" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260508174935_InitialSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260508174935_InitialSchema', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    ALTER TABLE "Affiliates" ADD "Address" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    ALTER TABLE "Affiliates" ADD "BusinessType" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    ALTER TABLE "Affiliates" ADD "ContactEmail" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    ALTER TABLE "Affiliates" ADD "CoverImageUrl" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    ALTER TABLE "Affiliates" ADD "LogoUrl" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    ALTER TABLE "Affiliates" ADD "Plan" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    ALTER TABLE "Affiliates" ADD "PlanStartedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    ALTER TABLE "Affiliates" ADD "PlanStatus" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    ALTER TABLE "Affiliates" ADD "Published" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    ALTER TABLE "Affiliates" ADD "Slug" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    ALTER TABLE "Affiliates" ADD "StripeCustomerId" character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    ALTER TABLE "Affiliates" ADD "StripeSubscriptionId" character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    ALTER TABLE "Affiliates" ADD "Website" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    ALTER TABLE "Affiliates" ADD "WhatsApp" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    CREATE TABLE "UserAffiliateMaps" (
        "Id" uuid NOT NULL,
        "SupabaseUserId" character varying(64) NOT NULL,
        "Email" character varying(256) NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Role" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_UserAffiliateMaps" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UserAffiliateMaps_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    CREATE UNIQUE INDEX "IX_Affiliates_Slug" ON "Affiliates" ("Slug") WHERE "Slug" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    CREATE INDEX "IX_UserAffiliateMaps_AffiliateId" ON "UserAffiliateMaps" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    CREATE INDEX "IX_UserAffiliateMaps_SupabaseUserId" ON "UserAffiliateMaps" ("SupabaseUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    CREATE UNIQUE INDEX "IX_UserAffiliateMaps_SupabaseUserId_AffiliateId" ON "UserAffiliateMaps" ("SupabaseUserId", "AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515190450_AddUserAffiliateMapAndSaasFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260515190450_AddUserAffiliateMapAndSaasFields', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515191723_FixCategoryNullable') THEN
    ALTER TABLE "Services" ALTER COLUMN "Category" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515191723_FixCategoryNullable') THEN
    ALTER TABLE "Products" ALTER COLUMN "Category" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515191723_FixCategoryNullable') THEN
    ALTER TABLE "InventoryItems" ALTER COLUMN "Category" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515191723_FixCategoryNullable') THEN
    ALTER TABLE "Affiliates" ALTER COLUMN "PrimaryColor" TYPE character varying(7);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515191723_FixCategoryNullable') THEN
    ALTER TABLE "Affiliates" ALTER COLUMN "Description" TYPE character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515191723_FixCategoryNullable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260515191723_FixCategoryNullable', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515192218_AddPublicCatalogFields') THEN
    ALTER TABLE "Services" ADD "ImageUrl" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515192218_AddPublicCatalogFields') THEN
    ALTER TABLE "Services" ADD "IsDemo" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515192218_AddPublicCatalogFields') THEN
    ALTER TABLE "Services" ADD "IsPubliclyVisible" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515192218_AddPublicCatalogFields') THEN
    ALTER TABLE "Services" ADD "SortOrder" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515192218_AddPublicCatalogFields') THEN
    ALTER TABLE "Products" ADD "IsDemo" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515192218_AddPublicCatalogFields') THEN
    ALTER TABLE "Products" ADD "IsPubliclyVisible" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515192218_AddPublicCatalogFields') THEN
    ALTER TABLE "Products" ADD "SortOrder" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515192218_AddPublicCatalogFields') THEN
    ALTER TABLE "InventoryItems" ADD "ImageUrl" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515192218_AddPublicCatalogFields') THEN
    ALTER TABLE "InventoryItems" ADD "IsDemo" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515192218_AddPublicCatalogFields') THEN
    ALTER TABLE "InventoryItems" ADD "IsPubliclyVisible" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515192218_AddPublicCatalogFields') THEN
    ALTER TABLE "InventoryItems" ADD "SortOrder" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260515192218_AddPublicCatalogFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260515192218_AddPublicCatalogFields', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529220540_AddAffiliateMilestones') THEN
    CREATE TABLE "AffiliateMilestones" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Key" character varying(100) NOT NULL,
        "Metadata" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_AffiliateMilestones" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AffiliateMilestones_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529220540_AddAffiliateMilestones') THEN
    CREATE UNIQUE INDEX "IX_AffiliateMilestones_AffiliateId_Key" ON "AffiliateMilestones" ("AffiliateId", "Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260529220540_AddAffiliateMilestones') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260529220540_AddAffiliateMilestones', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260704141635_AddCanales') THEN
    CREATE TABLE "Canales" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Tipo" integer NOT NULL,
        "Metodo" integer NOT NULL,
        "ValorCrudo" text NOT NULL,
        "EnlaceGenerado" text NOT NULL,
        "NombreVisible" text,
        "Verificado" boolean NOT NULL,
        "OauthRef" text,
        "Orden" integer NOT NULL,
        "Activo" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Canales" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Canales_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260704141635_AddCanales') THEN
    CREATE INDEX "IX_Canales_AffiliateId" ON "Canales" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260704141635_AddCanales') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260704141635_AddCanales', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260704142656_AddEventosInteraccion') THEN
    CREATE TABLE "EventosInteraccion" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Tipo" integer NOT NULL,
        "CanalId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_EventosInteraccion" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_EventosInteraccion_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_EventosInteraccion_Canales_CanalId" FOREIGN KEY ("CanalId") REFERENCES "Canales" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260704142656_AddEventosInteraccion') THEN
    CREATE INDEX "IX_EventosInteraccion_AffiliateId_Tipo_CreatedAt" ON "EventosInteraccion" ("AffiliateId", "Tipo", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260704142656_AddEventosInteraccion') THEN
    CREATE INDEX "IX_EventosInteraccion_CanalId" ON "EventosInteraccion" ("CanalId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260704142656_AddEventosInteraccion') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260704142656_AddEventosInteraccion', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260704152818_AddAffiliateModulosActivos') THEN
    ALTER TABLE "Affiliates" ADD "ModulosActivos" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260704152818_AddAffiliateModulosActivos') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260704152818_AddAffiliateModulosActivos', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260705204833_AddProductMenuFields') THEN
    ALTER TABLE "Products" ADD "DescriptionEn" character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260705204833_AddProductMenuFields') THEN
    ALTER TABLE "Products" ADD "Featured" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260705204833_AddProductMenuFields') THEN
    ALTER TABLE "Products" ADD "Flags" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260705204833_AddProductMenuFields') THEN
    ALTER TABLE "Products" ADD "Periods" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260705204833_AddProductMenuFields') THEN
    ALTER TABLE "Products" ADD "Popular" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260705204833_AddProductMenuFields') THEN
    ALTER TABLE "Products" ADD "WeekDays" character varying(100);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260705204833_AddProductMenuFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260705204833_AddProductMenuFields', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706163148_AddAffiliateProcessStepsAndFaq') THEN
    ALTER TABLE "Affiliates" ADD "Faq" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706163148_AddAffiliateProcessStepsAndFaq') THEN
    ALTER TABLE "Affiliates" ADD "ProcessSteps" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260706163148_AddAffiliateProcessStepsAndFaq') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260706163148_AddAffiliateProcessStepsAndFaq', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710155818_AddDescriptionEnToServiceAndInventoryItem') THEN
    ALTER TABLE "Services" ADD "DescriptionEn" character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710155818_AddDescriptionEnToServiceAndInventoryItem') THEN
    ALTER TABLE "InventoryItems" ADD "DescriptionEn" character varying(1000);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710155818_AddDescriptionEnToServiceAndInventoryItem') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260710155818_AddDescriptionEnToServiceAndInventoryItem', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710160416_AddAffiliateHorario') THEN
    ALTER TABLE "Affiliates" ADD "Horario" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260710160416_AddAffiliateHorario') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260710160416_AddAffiliateHorario', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711040759_AddAffiliateTimezone') THEN
    ALTER TABLE "Affiliates" ADD "Timezone" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260711040759_AddAffiliateTimezone') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260711040759_AddAffiliateTimezone', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727154118_AddAffiliateDescriptionEn') THEN
    ALTER TABLE "Affiliates" ADD "DescriptionEn" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260727154118_AddAffiliateDescriptionEn') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260727154118_AddAffiliateDescriptionEn', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260731205103_AddAffiliateIsFeatured') THEN
    ALTER TABLE "Affiliates" ADD "IsFeatured" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260731205103_AddAffiliateIsFeatured') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260731205103_AddAffiliateIsFeatured', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260802153502_AddStripeProcessedEvents') THEN
    CREATE TABLE "StripeProcessedEvents" (
        "EventId" character varying(255) NOT NULL,
        "ProcessedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_StripeProcessedEvents" PRIMARY KEY ("EventId")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260802153502_AddStripeProcessedEvents') THEN
    CREATE INDEX "IX_Affiliates_StripeCustomerId" ON "Affiliates" ("StripeCustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260802153502_AddStripeProcessedEvents') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260802153502_AddStripeProcessedEvents', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809100000_AddAffiliateStripeConnectFields') THEN
    ALTER TABLE "Affiliates" ADD "StripeConnectAccountId" character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809100000_AddAffiliateStripeConnectFields') THEN
    ALTER TABLE "Affiliates" ADD "StripeConnectChargesEnabled" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809100000_AddAffiliateStripeConnectFields') THEN
    ALTER TABLE "Affiliates" ADD "StripeConnectPayoutsEnabled" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809100000_AddAffiliateStripeConnectFields') THEN
    ALTER TABLE "Affiliates" ADD "StripeConnectDetailsSubmitted" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809100000_AddAffiliateStripeConnectFields') THEN
    ALTER TABLE "Affiliates" ADD "StripeConnectUpdatedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809100000_AddAffiliateStripeConnectFields') THEN
    CREATE INDEX "IX_Affiliates_StripeConnectAccountId" ON "Affiliates" ("StripeConnectAccountId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809100000_AddAffiliateStripeConnectFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260809100000_AddAffiliateStripeConnectFields', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809110000_AddAffiliateCountry') THEN
    ALTER TABLE "Affiliates" ADD "Country" character varying(2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809110000_AddAffiliateCountry') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260809110000_AddAffiliateCountry', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809120000_AddOrders') THEN
    CREATE TABLE "Orders" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "CustomerName" text,
        "CustomerPhone" text,
        "CustomerEmail" text,
        "Notes" text,
        "ItemsJson" text NOT NULL,
        "Subtotal" numeric(18,2) NOT NULL,
        "Tax" numeric(18,2) NOT NULL,
        "Total" numeric(18,2) NOT NULL,
        "Currency" character varying(3) NOT NULL,
        "Status" integer NOT NULL,
        "StripeCheckoutSessionId" character varying(255),
        "StripePaymentIntentId" character varying(255),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Orders" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Orders_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809120000_AddOrders') THEN
    CREATE INDEX "IX_Orders_AffiliateId_CreatedAt" ON "Orders" ("AffiliateId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809120000_AddOrders') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260809120000_AddOrders', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809130000_AddScreenAdsAndVideoUrl') THEN
    ALTER TABLE "Products" ADD "VideoUrl" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809130000_AddScreenAdsAndVideoUrl') THEN
    ALTER TABLE "Affiliates" ADD "AdFrequency" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809130000_AddScreenAdsAndVideoUrl') THEN
    CREATE TABLE "ScreenAds" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "MediaUrl" character varying(500) NOT NULL,
        "MediaType" integer NOT NULL,
        "DurationSeconds" integer NOT NULL,
        "SortOrder" integer NOT NULL,
        "Active" boolean NOT NULL,
        "StartsAt" timestamp with time zone,
        "EndsAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ScreenAds" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ScreenAds_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809130000_AddScreenAdsAndVideoUrl') THEN
    CREATE INDEX "IX_ScreenAds_AffiliateId_SortOrder" ON "ScreenAds" ("AffiliateId", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809130000_AddScreenAdsAndVideoUrl') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260809130000_AddScreenAdsAndVideoUrl', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809140000_AddAffiliateLanguageAndBoardTheme') THEN
    ALTER TABLE "Affiliates" ADD "Language" character varying(2) NOT NULL DEFAULT 'es';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809140000_AddAffiliateLanguageAndBoardTheme') THEN
    ALTER TABLE "Affiliates" ADD "BoardTheme" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260809140000_AddAffiliateLanguageAndBoardTheme') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260809140000_AddAffiliateLanguageAndBoardTheme', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810100000_AddScreens') THEN
    CREATE TABLE "Screens" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "SortOrder" integer NOT NULL,
        "Language" character varying(2),
        "BoardTheme" integer,
        "AdFrequency" integer,
        "CategoryFilter" character varying(500),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Screens" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Screens_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810100000_AddScreens') THEN
    CREATE INDEX "IX_Screens_AffiliateId_SortOrder" ON "Screens" ("AffiliateId", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810100000_AddScreens') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260810100000_AddScreens', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810120000_AddScreenAdFitAndTransitionEffect') THEN
    ALTER TABLE "ScreenAds" ADD "Fit" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810120000_AddScreenAdFitAndTransitionEffect') THEN
    ALTER TABLE "Affiliates" ADD "TransitionEffect" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810120000_AddScreenAdFitAndTransitionEffect') THEN
    ALTER TABLE "Screens" ADD "TransitionEffect" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810120000_AddScreenAdFitAndTransitionEffect') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260810120000_AddScreenAdFitAndTransitionEffect', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810140000_AddCatalogItemImages') THEN
    ALTER TABLE "Products" ADD "Images" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810140000_AddCatalogItemImages') THEN
    ALTER TABLE "Services" ADD "Images" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810140000_AddCatalogItemImages') THEN
    ALTER TABLE "InventoryItems" ADD "Images" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810140000_AddCatalogItemImages') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260810140000_AddCatalogItemImages', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810160000_AddScreenContentModeAndAdIds') THEN
    ALTER TABLE "Screens" ADD "ContentMode" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810160000_AddScreenContentModeAndAdIds') THEN
    ALTER TABLE "Screens" ADD "AdIds" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260810160000_AddScreenContentModeAndAdIds') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260810160000_AddScreenContentModeAndAdIds', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813223226_AddPlatformOps') THEN
    ALTER TABLE "UserAffiliateMaps" ADD "ImpersonationExpiresAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813223226_AddPlatformOps') THEN
    ALTER TABLE "UserAffiliateMaps" ADD "IsImpersonation" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813223226_AddPlatformOps') THEN
    CREATE TABLE "AdminImpersonationLogs" (
        "Id" uuid NOT NULL,
        "AdminSupabaseUserId" character varying(64) NOT NULL,
        "AdminEmail" character varying(256) NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "StartedAt" timestamp with time zone NOT NULL,
        "EndedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_AdminImpersonationLogs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813223226_AddPlatformOps') THEN
    CREATE TABLE "PlatformAdmins" (
        "Id" uuid NOT NULL,
        "SupabaseUserId" character varying(64) NOT NULL,
        "Email" character varying(256) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_PlatformAdmins" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813223226_AddPlatformOps') THEN
    CREATE INDEX "IX_AdminImpersonationLogs_AdminSupabaseUserId" ON "AdminImpersonationLogs" ("AdminSupabaseUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813223226_AddPlatformOps') THEN
    CREATE INDEX "IX_AdminImpersonationLogs_AffiliateId" ON "AdminImpersonationLogs" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813223226_AddPlatformOps') THEN
    CREATE UNIQUE INDEX "IX_PlatformAdmins_Email" ON "PlatformAdmins" ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813223226_AddPlatformOps') THEN
    CREATE INDEX "IX_PlatformAdmins_SupabaseUserId" ON "PlatformAdmins" ("SupabaseUserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260813223226_AddPlatformOps') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260813223226_AddPlatformOps', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814135601_AddPlatformRoleAndAffiliateNotes') THEN
    ALTER TABLE "PlatformAdmins" ADD "Role" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814135601_AddPlatformRoleAndAffiliateNotes') THEN
    CREATE TABLE "AffiliateNotes" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "AuthorEmail" character varying(256) NOT NULL,
        "Text" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_AffiliateNotes" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814135601_AddPlatformRoleAndAffiliateNotes') THEN
    CREATE INDEX "IX_AffiliateNotes_AffiliateId" ON "AffiliateNotes" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814135601_AddPlatformRoleAndAffiliateNotes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814135601_AddPlatformRoleAndAffiliateNotes', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814215041_AddTeamMemberLinkToUserAffiliateMap') THEN
    ALTER TABLE "UserAffiliateMaps" ADD "TeamMemberId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814215041_AddTeamMemberLinkToUserAffiliateMap') THEN
    CREATE INDEX "IX_UserAffiliateMaps_TeamMemberId" ON "UserAffiliateMaps" ("TeamMemberId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814215041_AddTeamMemberLinkToUserAffiliateMap') THEN
    ALTER TABLE "UserAffiliateMaps" ADD CONSTRAINT "FK_UserAffiliateMaps_TeamMembers_TeamMemberId" FOREIGN KEY ("TeamMemberId") REFERENCES "TeamMembers" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260814215041_AddTeamMemberLinkToUserAffiliateMap') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260814215041_AddTeamMemberLinkToUserAffiliateMap', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260815192301_MakeServiceDurationMinutesNullable') THEN
    ALTER TABLE "Services" ALTER COLUMN "DurationMinutes" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260815192301_MakeServiceDurationMinutesNullable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260815192301_MakeServiceDurationMinutesNullable', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260815195211_AddAffiliateCurrency') THEN
    ALTER TABLE "Affiliates" ADD "Currency" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260815195211_AddAffiliateCurrency') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260815195211_AddAffiliateCurrency', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260815210356_AddTeamMemberPhotoUrl') THEN
    ALTER TABLE "TeamMembers" ADD "PhotoUrl" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260815210356_AddTeamMemberPhotoUrl') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260815210356_AddTeamMemberPhotoUrl', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816143838_AddOrderChannelAndPaymentMethod') THEN
    ALTER TABLE "Orders" ADD "Channel" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816143838_AddOrderChannelAndPaymentMethod') THEN
    ALTER TABLE "Orders" ADD "PaymentMethod" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816143838_AddOrderChannelAndPaymentMethod') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816143838_AddOrderChannelAndPaymentMethod', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816150000_RemoveGiftCardAndCampaign') THEN
    DROP TABLE "Campaigns";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816150000_RemoveGiftCardAndCampaign') THEN
    DROP TABLE "GiftCards";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816150000_RemoveGiftCardAndCampaign') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816150000_RemoveGiftCardAndCampaign', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816160000_AddOrderTip') THEN
    ALTER TABLE "Orders" ADD "Tip" numeric(18,2) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816160000_AddOrderTip') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816160000_AddOrderTip', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816170000_AddTableReservations') THEN
    CREATE TABLE "TableReservations" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "CustomerName" text NOT NULL,
        "CustomerPhone" text NOT NULL,
        "CustomerEmail" text,
        "Date" timestamp with time zone NOT NULL,
        "Time" text NOT NULL,
        "PartySize" integer NOT NULL,
        "Status" text NOT NULL,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_TableReservations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TableReservations_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816170000_AddTableReservations') THEN
    CREATE INDEX "IX_TableReservations_AffiliateId_Date" ON "TableReservations" ("AffiliateId", "Date");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260816170000_AddTableReservations') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260816170000_AddTableReservations', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817150000_AddTimeBlockAndReminder') THEN
    ALTER TABLE "Appointments" ADD "ReminderSentAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817150000_AddTimeBlockAndReminder') THEN
    CREATE TABLE "TimeBlocks" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "StaffId" uuid,
        "Date" timestamp with time zone NOT NULL,
        "StartTime" character varying(5) NOT NULL,
        "EndTime" character varying(5) NOT NULL,
        "Reason" character varying(200),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_TimeBlocks" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TimeBlocks_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_TimeBlocks_TeamMembers_StaffId" FOREIGN KEY ("StaffId") REFERENCES "TeamMembers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817150000_AddTimeBlockAndReminder') THEN
    CREATE INDEX "IX_TimeBlocks_AffiliateId_Date" ON "TimeBlocks" ("AffiliateId", "Date");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817150000_AddTimeBlockAndReminder') THEN
    CREATE INDEX "IX_TimeBlocks_StaffId" ON "TimeBlocks" ("StaffId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817150000_AddTimeBlockAndReminder') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817150000_AddTimeBlockAndReminder', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817180000_AddProposals') THEN
    CREATE TABLE "Proposals" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "CustomerName" text NOT NULL,
        "CustomerEmail" text,
        "CustomerPhone" text,
        "Title" text NOT NULL,
        "Description" text,
        "Amount" numeric(18,2) NOT NULL,
        "Currency" character varying(3) NOT NULL,
        "Status" text NOT NULL,
        "Token" uuid NOT NULL,
        "SentAt" timestamp with time zone,
        "AcceptedAt" timestamp with time zone,
        "AcceptedByName" text,
        "ExpiresAt" timestamp with time zone,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Proposals" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Proposals_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817180000_AddProposals') THEN
    CREATE UNIQUE INDEX "IX_Proposals_Token" ON "Proposals" ("Token");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817180000_AddProposals') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817180000_AddProposals', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817202808_AddAffiliateSectionVisibility') THEN
    ALTER TABLE "Affiliates" ADD "SectionVisibility" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260817202808_AddAffiliateSectionVisibility') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260817202808_AddAffiliateSectionVisibility', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818160701_AddAffiliateGalleryImages') THEN
    ALTER TABLE "Affiliates" ADD "GalleryImages" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260818160701_AddAffiliateGalleryImages') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260818160701_AddAffiliateGalleryImages', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819170000_AddCrmLinksAndAppointmentToken') THEN
    ALTER TABLE "Appointments" ADD "Token" uuid NOT NULL DEFAULT (gen_random_uuid());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819170000_AddCrmLinksAndAppointmentToken') THEN
    CREATE UNIQUE INDEX "IX_Appointments_Token" ON "Appointments" ("Token");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819170000_AddCrmLinksAndAppointmentToken') THEN
    ALTER TABLE "QueueEntries" ADD "CustomerId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819170000_AddCrmLinksAndAppointmentToken') THEN
    CREATE INDEX "IX_QueueEntries_CustomerId" ON "QueueEntries" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819170000_AddCrmLinksAndAppointmentToken') THEN
    ALTER TABLE "QueueEntries" ADD CONSTRAINT "FK_QueueEntries_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819170000_AddCrmLinksAndAppointmentToken') THEN
    ALTER TABLE "TableReservations" ADD "CustomerId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819170000_AddCrmLinksAndAppointmentToken') THEN
    CREATE INDEX "IX_TableReservations_CustomerId" ON "TableReservations" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819170000_AddCrmLinksAndAppointmentToken') THEN
    ALTER TABLE "TableReservations" ADD CONSTRAINT "FK_TableReservations_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819170000_AddCrmLinksAndAppointmentToken') THEN
    ALTER TABLE "Proposals" ADD "CustomerId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819170000_AddCrmLinksAndAppointmentToken') THEN
    CREATE INDEX "IX_Proposals_CustomerId" ON "Proposals" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819170000_AddCrmLinksAndAppointmentToken') THEN
    ALTER TABLE "Proposals" ADD CONSTRAINT "FK_Proposals_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260819170000_AddCrmLinksAndAppointmentToken') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260819170000_AddCrmLinksAndAppointmentToken', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260820230243_AddInvoiceStripeFields') THEN
    ALTER TABLE "Invoices" ADD "StripeCheckoutSessionId" character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260820230243_AddInvoiceStripeFields') THEN
    ALTER TABLE "Invoices" ADD "StripePaymentIntentId" character varying(255);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260820230243_AddInvoiceStripeFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260820230243_AddInvoiceStripeFields', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260821190000_AddProductIngredients') THEN
    CREATE TABLE "ProductIngredients" (
        "Id" uuid NOT NULL,
        "ProductId" uuid NOT NULL,
        "InventoryItemId" uuid NOT NULL,
        "Quantity" numeric(18,3) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ProductIngredients" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ProductIngredients_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ProductIngredients_InventoryItems_InventoryItemId" FOREIGN KEY ("InventoryItemId") REFERENCES "InventoryItems" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260821190000_AddProductIngredients') THEN
    CREATE UNIQUE INDEX "IX_ProductIngredients_ProductId_InventoryItemId" ON "ProductIngredients" ("ProductId", "InventoryItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260821190000_AddProductIngredients') THEN
    CREATE INDEX "IX_ProductIngredients_InventoryItemId" ON "ProductIngredients" ("InventoryItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260821190000_AddProductIngredients') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260821190000_AddProductIngredients', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260821200000_AddNameEn') THEN
    ALTER TABLE "Products" ADD "NameEn" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260821200000_AddNameEn') THEN
    ALTER TABLE "Services" ADD "NameEn" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260821200000_AddNameEn') THEN
    ALTER TABLE "InventoryItems" ADD "NameEn" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260821200000_AddNameEn') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260821200000_AddNameEn', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822120000_AddInventoryUnitAndRestrictRecipeDelete') THEN
    ALTER TABLE "InventoryItems" ADD "Unit" character varying(20) NOT NULL DEFAULT 'unidad';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822120000_AddInventoryUnitAndRestrictRecipeDelete') THEN
    ALTER TABLE "ProductIngredients" DROP CONSTRAINT "FK_ProductIngredients_InventoryItems_InventoryItemId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822120000_AddInventoryUnitAndRestrictRecipeDelete') THEN
    ALTER TABLE "ProductIngredients" ADD CONSTRAINT "FK_ProductIngredients_InventoryItems_InventoryItemId" FOREIGN KEY ("InventoryItemId") REFERENCES "InventoryItems" ("Id") ON DELETE RESTRICT;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822120000_AddInventoryUnitAndRestrictRecipeDelete') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260822120000_AddInventoryUnitAndRestrictRecipeDelete', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822130000_AddProposalAttachmentAndAuditFields') THEN
    ALTER TABLE "Proposals" ADD "AttachmentUrl" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822130000_AddProposalAttachmentAndAuditFields') THEN
    ALTER TABLE "Proposals" ADD "AttachmentName" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822130000_AddProposalAttachmentAndAuditFields') THEN
    ALTER TABLE "Proposals" ADD "AcceptedIp" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822130000_AddProposalAttachmentAndAuditFields') THEN
    ALTER TABLE "Proposals" ADD "AcceptedUserAgent" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822130000_AddProposalAttachmentAndAuditFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260822130000_AddProposalAttachmentAndAuditFields', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822140000_AddTeamManagement') THEN
    ALTER TABLE "TeamMembers" ADD "HourlyRate" numeric(18,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822140000_AddTeamManagement') THEN
    ALTER TABLE "TeamMembers" ADD "PinCode" character varying(6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822140000_AddTeamManagement') THEN
    CREATE TABLE "TimeEntries" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "TeamMemberId" uuid NOT NULL,
        "ClockIn" timestamp with time zone NOT NULL,
        "ClockOut" timestamp with time zone,
        "Source" text NOT NULL,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_TimeEntries" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TimeEntries_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_TimeEntries_TeamMembers_TeamMemberId" FOREIGN KEY ("TeamMemberId") REFERENCES "TeamMembers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822140000_AddTeamManagement') THEN
    CREATE TABLE "StaffTasks" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "TeamMemberId" uuid,
        "Title" text NOT NULL,
        "Description" text,
        "Status" text NOT NULL,
        "DueDate" timestamp with time zone,
        "CompletedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_StaffTasks" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StaffTasks_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StaffTasks_TeamMembers_TeamMemberId" FOREIGN KEY ("TeamMemberId") REFERENCES "TeamMembers" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822140000_AddTeamManagement') THEN
    CREATE INDEX "IX_TimeEntries_AffiliateId" ON "TimeEntries" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822140000_AddTeamManagement') THEN
    CREATE INDEX "IX_TimeEntries_TeamMemberId" ON "TimeEntries" ("TeamMemberId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822140000_AddTeamManagement') THEN
    CREATE INDEX "IX_StaffTasks_AffiliateId" ON "StaffTasks" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822140000_AddTeamManagement') THEN
    CREATE INDEX "IX_StaffTasks_TeamMemberId" ON "StaffTasks" ("TeamMemberId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822140000_AddTeamManagement') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260822140000_AddTeamManagement', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822150000_AddInvoiceVoidAndCorrectionFields') THEN
    ALTER TABLE "Invoices" ADD "VoidedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822150000_AddInvoiceVoidAndCorrectionFields') THEN
    ALTER TABLE "Invoices" ADD "VoidReason" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822150000_AddInvoiceVoidAndCorrectionFields') THEN
    ALTER TABLE "Invoices" ADD "ReplacesInvoiceId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822150000_AddInvoiceVoidAndCorrectionFields') THEN
    ALTER TABLE "Invoices" ADD "ReplacedByInvoiceId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822150000_AddInvoiceVoidAndCorrectionFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260822150000_AddInvoiceVoidAndCorrectionFields', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822151000_AddAuditLogEntries') THEN
    CREATE TABLE "AuditLogEntries" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid,
        "ActorId" text,
        "ActorName" text,
        "Action" text NOT NULL,
        "EntityType" text NOT NULL,
        "EntityId" uuid,
        "Description" text NOT NULL,
        "MetadataJson" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_AuditLogEntries" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AuditLogEntries_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822151000_AddAuditLogEntries') THEN
    CREATE INDEX "IX_AuditLogEntries_AffiliateId_CreatedAt" ON "AuditLogEntries" ("AffiliateId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822151000_AddAuditLogEntries') THEN
    CREATE INDEX "IX_AuditLogEntries_EntityType_EntityId" ON "AuditLogEntries" ("EntityType", "EntityId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822151000_AddAuditLogEntries') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260822151000_AddAuditLogEntries', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822160000_AddInventoryBarcodeAndInternalCode') THEN
    ALTER TABLE "InventoryItems" ADD "Barcode" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822160000_AddInventoryBarcodeAndInternalCode') THEN
    ALTER TABLE "InventoryItems" ADD "InternalCode" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822160000_AddInventoryBarcodeAndInternalCode') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260822160000_AddInventoryBarcodeAndInternalCode', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822170000_AddVirtualBookingFields') THEN
    ALTER TABLE "Affiliates" ADD "ZoomLink" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822170000_AddVirtualBookingFields') THEN
    ALTER TABLE "Services" ADD "Modality" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822170000_AddVirtualBookingFields') THEN
    ALTER TABLE "Appointments" ADD "IsVirtual" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260822170000_AddVirtualBookingFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260822170000_AddVirtualBookingFields', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "TeamMembers" ADD "HourlyRate" numeric(18,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "TeamMembers" ADD "PinCode" character varying(6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "Services" ADD "NameEn" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "Proposals" ADD "AcceptedIp" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "Proposals" ADD "AcceptedUserAgent" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "Proposals" ADD "AttachmentName" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "Proposals" ADD "AttachmentUrl" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "Products" ADD "NameEn" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "Invoices" ALTER COLUMN "StripePaymentIntentId" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "Invoices" ALTER COLUMN "StripeCheckoutSessionId" TYPE text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "Invoices" ADD "ReplacedByInvoiceId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "Invoices" ADD "ReplacesInvoiceId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "Invoices" ADD "VoidReason" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "Invoices" ADD "VoidedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "InventoryItems" ADD "Barcode" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "InventoryItems" ADD "InternalCode" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "InventoryItems" ADD "NameEn" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    ALTER TABLE "InventoryItems" ADD "Unit" character varying(20) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE TABLE "AuditLogEntries" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid,
        "ActorId" text,
        "ActorName" text,
        "Action" text NOT NULL,
        "EntityType" text NOT NULL,
        "EntityId" uuid,
        "Description" text NOT NULL,
        "MetadataJson" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_AuditLogEntries" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AuditLogEntries_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE TABLE "ModifierGroups" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" text NOT NULL,
        "NameEn" text,
        "MinSelect" integer NOT NULL,
        "MaxSelect" integer NOT NULL,
        "Required" boolean NOT NULL,
        "SortOrder" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_ModifierGroups" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ModifierGroups_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE TABLE "ProductIngredients" (
        "Id" uuid NOT NULL,
        "ProductId" uuid NOT NULL,
        "InventoryItemId" uuid NOT NULL,
        "Quantity" numeric(18,3) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ProductIngredients" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ProductIngredients_InventoryItems_InventoryItemId" FOREIGN KEY ("InventoryItemId") REFERENCES "InventoryItems" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ProductIngredients_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE TABLE "StaffTasks" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "TeamMemberId" uuid,
        "Title" text NOT NULL,
        "Description" text,
        "Status" text NOT NULL,
        "DueDate" timestamp with time zone,
        "CompletedAt" timestamp with time zone,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_StaffTasks" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StaffTasks_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StaffTasks_TeamMembers_TeamMemberId" FOREIGN KEY ("TeamMemberId") REFERENCES "TeamMembers" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE TABLE "TimeEntries" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "TeamMemberId" uuid NOT NULL,
        "ClockIn" timestamp with time zone NOT NULL,
        "ClockOut" timestamp with time zone,
        "Source" text NOT NULL,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_TimeEntries" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TimeEntries_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_TimeEntries_TeamMembers_TeamMemberId" FOREIGN KEY ("TeamMemberId") REFERENCES "TeamMembers" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE TABLE "ModifierOptions" (
        "Id" uuid NOT NULL,
        "ModifierGroupId" uuid NOT NULL,
        "Name" text NOT NULL,
        "NameEn" text,
        "PriceDelta" numeric(18,2) NOT NULL,
        "IsDefault" boolean NOT NULL,
        "SortOrder" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ModifierOptions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ModifierOptions_ModifierGroups_ModifierGroupId" FOREIGN KEY ("ModifierGroupId") REFERENCES "ModifierGroups" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE TABLE "ProductModifierGroups" (
        "Id" uuid NOT NULL,
        "ProductId" uuid NOT NULL,
        "ModifierGroupId" uuid NOT NULL,
        "SortOrder" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ProductModifierGroups" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ProductModifierGroups_ModifierGroups_ModifierGroupId" FOREIGN KEY ("ModifierGroupId") REFERENCES "ModifierGroups" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ProductModifierGroups_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE INDEX "IX_AuditLogEntries_AffiliateId" ON "AuditLogEntries" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE INDEX "IX_ModifierGroups_AffiliateId" ON "ModifierGroups" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE INDEX "IX_ModifierOptions_ModifierGroupId" ON "ModifierOptions" ("ModifierGroupId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE INDEX "IX_ProductIngredients_InventoryItemId" ON "ProductIngredients" ("InventoryItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE UNIQUE INDEX "IX_ProductIngredients_ProductId_InventoryItemId" ON "ProductIngredients" ("ProductId", "InventoryItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE INDEX "IX_ProductModifierGroups_ModifierGroupId" ON "ProductModifierGroups" ("ModifierGroupId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE UNIQUE INDEX "IX_ProductModifierGroups_ProductId_ModifierGroupId" ON "ProductModifierGroups" ("ProductId", "ModifierGroupId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE INDEX "IX_StaffTasks_AffiliateId" ON "StaffTasks" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE INDEX "IX_StaffTasks_TeamMemberId" ON "StaffTasks" ("TeamMemberId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE INDEX "IX_TimeEntries_AffiliateId" ON "TimeEntries" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    CREATE INDEX "IX_TimeEntries_TeamMemberId" ON "TimeEntries" ("TeamMemberId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260920162258_AddModifierGroups') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260920162258_AddModifierGroups', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923120000_AddAffiliateTrialOverride') THEN
    ALTER TABLE "Affiliates" ADD "TrialOverrideEndsAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923120000_AddAffiliateTrialOverride') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260923120000_AddAffiliateTrialOverride', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    DROP INDEX "IX_InventoryItems_AffiliateId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    ALTER TABLE "InventoryMovements" ALTER COLUMN "Quantity" TYPE numeric(18,3);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    ALTER TABLE "InventoryItems" ALTER COLUMN "Quantity" TYPE numeric(18,3);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    ALTER TABLE "InventoryItems" ALTER COLUMN "MinStock" TYPE numeric(18,3);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    ALTER TABLE "InventoryItems" ADD "ExpirationDate" date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    ALTER TABLE "InventoryItems" ADD "Source" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    ALTER TABLE "InventoryItems" ADD "UnitCost" numeric(18,4) NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    ALTER TABLE "Affiliates" ADD "OperatorType" integer NOT NULL DEFAULT 1;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    CREATE TABLE "Combos" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "CostPerPlate" numeric(18,4) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Combos" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Combos_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    CREATE TABLE "Recipes" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" character varying(200) NOT NULL,
        "Servings" integer NOT NULL,
        "CostPerServing" numeric(18,4) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Recipes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Recipes_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    CREATE TABLE "ComboServings" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "ComboId" uuid,
        "ComboName" character varying(200) NOT NULL,
        "Quantity" integer NOT NULL,
        "CostPerPlate" numeric(18,4) NOT NULL,
        "ServedAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ComboServings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ComboServings_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ComboServings_Combos_ComboId" FOREIGN KEY ("ComboId") REFERENCES "Combos" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    CREATE TABLE "ComboRecipes" (
        "ComboId" uuid NOT NULL,
        "RecipeId" uuid NOT NULL,
        CONSTRAINT "PK_ComboRecipes" PRIMARY KEY ("ComboId", "RecipeId"),
        CONSTRAINT "FK_ComboRecipes_Combos_ComboId" FOREIGN KEY ("ComboId") REFERENCES "Combos" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ComboRecipes_Recipes_RecipeId" FOREIGN KEY ("RecipeId") REFERENCES "Recipes" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    CREATE TABLE "RecipeIngredients" (
        "Id" uuid NOT NULL,
        "RecipeId" uuid NOT NULL,
        "InventoryItemId" uuid NOT NULL,
        "QuantityRequired" numeric(18,3) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_RecipeIngredients" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RecipeIngredients_InventoryItems_InventoryItemId" FOREIGN KEY ("InventoryItemId") REFERENCES "InventoryItems" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RecipeIngredients_Recipes_RecipeId" FOREIGN KEY ("RecipeId") REFERENCES "Recipes" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    CREATE INDEX "IX_InventoryItems_AffiliateId_ExpirationDate" ON "InventoryItems" ("AffiliateId", "ExpirationDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    CREATE INDEX "IX_ComboRecipes_RecipeId" ON "ComboRecipes" ("RecipeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    CREATE INDEX "IX_Combos_AffiliateId" ON "Combos" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    CREATE INDEX "IX_ComboServings_AffiliateId_ServedAt" ON "ComboServings" ("AffiliateId", "ServedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    CREATE INDEX "IX_ComboServings_ComboId" ON "ComboServings" ("ComboId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    CREATE INDEX "IX_RecipeIngredients_InventoryItemId" ON "RecipeIngredients" ("InventoryItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    CREATE UNIQUE INDEX "IX_RecipeIngredients_RecipeId_InventoryItemId" ON "RecipeIngredients" ("RecipeId", "InventoryItemId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    CREATE INDEX "IX_Recipes_AffiliateId" ON "Recipes" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260923180017_AddCommunityPhase1') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260923180017_AddCommunityPhase1', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924192249_AddCommunityCausasImpact') THEN
    ALTER TABLE "Affiliates" ADD "Causas" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924192249_AddCommunityCausasImpact') THEN
    ALTER TABLE "Affiliates" ADD "CommunityImpact" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260924192249_AddCommunityCausasImpact') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260924192249_AddCommunityCausasImpact', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925191615_AddActivitiesAndCausasTable') THEN
    CREATE TABLE "Activities" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Title" character varying(200) NOT NULL,
        "TitleEn" text,
        "Description" text,
        "DescriptionEn" text,
        "Location" character varying(300),
        "StartsAt" timestamp with time zone NOT NULL,
        "EndsAt" timestamp with time zone,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Activities" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Activities_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925191615_AddActivitiesAndCausasTable') THEN
    CREATE TABLE "Causas" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Title" character varying(200) NOT NULL,
        "Type" character varying(20) NOT NULL,
        "Description" character varying(500),
        "GoalAmount" numeric(18,2),
        "CurrentAmount" numeric(18,2),
        "IsActive" boolean NOT NULL,
        "SortOrder" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Causas" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Causas_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925191615_AddActivitiesAndCausasTable') THEN

                INSERT INTO "Causas" ("Id", "AffiliateId", "Title", "Type", "Description", "GoalAmount", "CurrentAmount", "IsActive", "SortOrder", "CreatedAt")
                SELECT
                    gen_random_uuid(),
                    a."Id",
                    elem->>'title',
                    COALESCE(elem->>'type', 'money'),
                    NULLIF(elem->>'description', ''),
                    NULLIF(elem->>'goalAmount', '')::numeric,
                    NULLIF(elem->>'currentAmount', '')::numeric,
                    true,
                    (ordinality - 1)::int,
                    now()
                FROM "Affiliates" a
                CROSS JOIN LATERAL jsonb_array_elements(a."Causas"::jsonb) WITH ORDINALITY AS t(elem, ordinality)
                WHERE a."Causas" IS NOT NULL AND trim(a."Causas") NOT IN ('', '[]');
            
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925191615_AddActivitiesAndCausasTable') THEN
    CREATE INDEX "IX_Activities_AffiliateId_StartsAt" ON "Activities" ("AffiliateId", "StartsAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925191615_AddActivitiesAndCausasTable') THEN
    CREATE INDEX "IX_Causas_AffiliateId_SortOrder" ON "Causas" ("AffiliateId", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925191615_AddActivitiesAndCausasTable') THEN
    ALTER TABLE "Affiliates" DROP COLUMN "Causas";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260925191615_AddActivitiesAndCausasTable') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260925191615_AddActivitiesAndCausasTable', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926162412_AddDonations') THEN
    CREATE TABLE "Donations" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "DonorName" character varying(200),
        "DonorEmail" character varying(320),
        "Message" character varying(500),
        "Amount" numeric(18,2) NOT NULL,
        "Currency" character varying(3) NOT NULL,
        "Status" integer NOT NULL,
        "StripeCheckoutSessionId" character varying(255),
        "StripePaymentIntentId" character varying(255),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Donations" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Donations_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926162412_AddDonations') THEN
    CREATE INDEX "IX_Donations_AffiliateId_Status_CreatedAt" ON "Donations" ("AffiliateId", "Status", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926162412_AddDonations') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260926162412_AddDonations', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926171532_AddCommunityPrograms') THEN
    ALTER TABLE "Activities" ADD "ImageUrl" character varying(500);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926171532_AddCommunityPrograms') THEN
    CREATE TABLE "CommunityPrograms" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Title" character varying(200) NOT NULL,
        "TitleEn" text,
        "Description" text,
        "DescriptionEn" text,
        "ImageUrl" character varying(500),
        "GoalAmount" numeric(18,2),
        "Capacity" integer,
        "Schedule" character varying(200),
        "WeekDays" character varying(100),
        "VolunteersNeeded" integer,
        "IsActive" boolean NOT NULL,
        "SortOrder" integer NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_CommunityPrograms" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CommunityPrograms_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926171532_AddCommunityPrograms') THEN
    CREATE INDEX "IX_CommunityPrograms_AffiliateId_SortOrder" ON "CommunityPrograms" ("AffiliateId", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926171532_AddCommunityPrograms') THEN

                    INSERT INTO "CommunityPrograms"
                        ("Id", "AffiliateId", "Title", "TitleEn", "Description", "DescriptionEn",
                         "ImageUrl", "GoalAmount", "IsActive", "SortOrder", "CreatedAt", "UpdatedAt")
                    SELECT
                        s."Id", s."AffiliateId", s."Name", s."NameEn", s."Description", s."DescriptionEn",
                        s."ImageUrl",
                        CASE WHEN s."Price" > 0 THEN s."Price" ELSE NULL END,
                        s."IsActive", s."SortOrder", s."CreatedAt", s."UpdatedAt"
                    FROM "Services" s
                    INNER JOIN "Affiliates" a ON a."Id" = s."AffiliateId"
                    WHERE a."BusinessType" = 7;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260926171532_AddCommunityPrograms') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260926171532_AddCommunityPrograms', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001221532_AddReminderSentAtToProposalsAndInvoices') THEN
    ALTER TABLE "Proposals" ADD "ReminderSentAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001221532_AddReminderSentAtToProposalsAndInvoices') THEN
    ALTER TABLE "Invoices" ADD "ReminderSentAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001221532_AddReminderSentAtToProposalsAndInvoices') THEN
    UPDATE "Invoices" SET "ReminderSentAt" = NOW() WHERE "ReminderSentAt" IS NULL AND "Status" IN ('Pending', 'Overdue');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001221532_AddReminderSentAtToProposalsAndInvoices') THEN
    UPDATE "Proposals" SET "ReminderSentAt" = NOW() WHERE "ReminderSentAt" IS NULL AND "Status" = 'Sent';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261001221532_AddReminderSentAtToProposalsAndInvoices') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261001221532_AddReminderSentAtToProposalsAndInvoices', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002013000_AddAccentColorToAffiliates') THEN
    ALTER TABLE "Affiliates" ADD "AccentColor" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002013000_AddAccentColorToAffiliates') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261002013000_AddAccentColorToAffiliates', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002030000_AddTableNumberToOrders') THEN
    ALTER TABLE "Orders" ADD "TableNumber" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002030000_AddTableNumberToOrders') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261002030000_AddTableNumberToOrders', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002040000_AddCustomerIdToOrders') THEN
    ALTER TABLE "Orders" ADD "CustomerId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002040000_AddCustomerIdToOrders') THEN
    CREATE INDEX "IX_Orders_CustomerId" ON "Orders" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002040000_AddCustomerIdToOrders') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261002040000_AddCustomerIdToOrders', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002100000_AddScheduledForToOrders') THEN
    ALTER TABLE "Orders" ADD "ScheduledFor" date;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002100000_AddScheduledForToOrders') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261002100000_AddScheduledForToOrders', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002120000_AddMealPeriodHoursToAffiliates') THEN
    ALTER TABLE "Affiliates" ADD COLUMN IF NOT EXISTS "MealPeriodHours" text NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002120000_AddMealPeriodHoursToAffiliates') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261002120000_AddMealPeriodHoursToAffiliates', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002140000_AddOwnerNotificationsAndPushSubscriptions') THEN

    CREATE TABLE IF NOT EXISTS "OwnerNotifications" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Type" character varying(30) NOT NULL,
        "Title" character varying(200) NOT NULL,
        "Body" character varying(500) NULL,
        "TitleEn" character varying(200) NOT NULL,
        "BodyEn" character varying(500) NULL,
        "Url" character varying(200) NULL,
        "EntityId" uuid NULL,
        "ReadAt" timestamp with time zone NULL,
        "CreatedBy" text NULL,
        "UpdatedBy" text NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NULL,
        CONSTRAINT "PK_OwnerNotifications" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_OwnerNotifications_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    CREATE INDEX IF NOT EXISTS "IX_OwnerNotifications_AffiliateId_ReadAt_CreatedAt" ON "OwnerNotifications" ("AffiliateId", "ReadAt", "CreatedAt");

    CREATE TABLE IF NOT EXISTS "PushSubscriptions" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Endpoint" character varying(1000) NOT NULL,
        "P256dh" character varying(300) NOT NULL,
        "Auth" character varying(100) NOT NULL,
        "Lang" character varying(5) NOT NULL,
        "CreatedBy" text NULL,
        "UpdatedBy" text NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NULL,
        CONSTRAINT "PK_PushSubscriptions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PushSubscriptions_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    CREATE INDEX IF NOT EXISTS "IX_PushSubscriptions_AffiliateId" ON "PushSubscriptions" ("AffiliateId");
    CREATE UNIQUE INDEX IF NOT EXISTS "IX_PushSubscriptions_Endpoint" ON "PushSubscriptions" ("Endpoint");

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002140000_AddOwnerNotificationsAndPushSubscriptions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261002140000_AddOwnerNotificationsAndPushSubscriptions', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002160000_AddCategoryTranslationsToAffiliates') THEN
    ALTER TABLE "Affiliates" ADD COLUMN IF NOT EXISTS "CategoryTranslations" text NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002160000_AddCategoryTranslationsToAffiliates') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261002160000_AddCategoryTranslationsToAffiliates', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002200000_AddCommunitySignups') THEN

    ALTER TABLE "Activities" ADD COLUMN IF NOT EXISTS "Capacity" integer NULL;

    CREATE TABLE IF NOT EXISTS "CommunitySignups" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Kind" character varying(20) NOT NULL,
        "CausaId" uuid NULL,
        "ActivityId" uuid NULL,
        "TargetTitle" character varying(200) NOT NULL,
        "Name" character varying(100) NOT NULL,
        "Phone" character varying(30) NULL,
        "Email" character varying(200) NULL,
        "PartySize" integer NOT NULL,
        "Notes" character varying(500) NULL,
        "Language" character varying(5) NOT NULL,
        "Status" character varying(20) NOT NULL,
        "CreatedBy" text NULL,
        "UpdatedBy" text NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NULL,
        CONSTRAINT "PK_CommunitySignups" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CommunitySignups_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    CREATE INDEX IF NOT EXISTS "IX_CommunitySignups_AffiliateId_Status_CreatedAt" ON "CommunitySignups" ("AffiliateId", "Status", "CreatedAt");
    CREATE INDEX IF NOT EXISTS "IX_CommunitySignups_ActivityId" ON "CommunitySignups" ("ActivityId");

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261002200000_AddCommunitySignups') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261002200000_AddCommunitySignups', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004150000_AddCollectedToOrders') THEN
    ALTER TABLE "Orders" ADD "CollectedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004150000_AddCollectedToOrders') THEN
    ALTER TABLE "Orders" ADD "CollectedMethod" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004150000_AddCollectedToOrders') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261004150000_AddCollectedToOrders', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004170000_AddTrackingToOrders') THEN
    ALTER TABLE "Orders" ADD "TrackingToken" character varying(64);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004170000_AddTrackingToOrders') THEN
    ALTER TABLE "Orders" ADD "EstimatedReadyAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004170000_AddTrackingToOrders') THEN
    CREATE UNIQUE INDEX "IX_Orders_TrackingToken" ON "Orders" ("TrackingToken");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004170000_AddTrackingToOrders') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261004170000_AddTrackingToOrders', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004180000_AddOrderPushSubscriptions') THEN

    CREATE TABLE IF NOT EXISTS "OrderPushSubscriptions" (
        "Id" uuid NOT NULL,
        "OrderId" uuid NOT NULL,
        "Endpoint" character varying(1000) NOT NULL,
        "P256dh" character varying(300) NOT NULL,
        "Auth" character varying(100) NOT NULL,
        "Lang" character varying(5) NOT NULL,
        "CreatedBy" text NULL,
        "UpdatedBy" text NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NULL,
        CONSTRAINT "PK_OrderPushSubscriptions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_OrderPushSubscriptions_Orders_OrderId" FOREIGN KEY ("OrderId") REFERENCES "Orders" ("Id") ON DELETE CASCADE
    );
    CREATE INDEX IF NOT EXISTS "IX_OrderPushSubscriptions_OrderId" ON "OrderPushSubscriptions" ("OrderId");
    CREATE UNIQUE INDEX IF NOT EXISTS "IX_OrderPushSubscriptions_OrderId_Endpoint" ON "OrderPushSubscriptions" ("OrderId", "Endpoint");

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261004180000_AddOrderPushSubscriptions') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261004180000_AddOrderPushSubscriptions', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261010170242_AddEmailEvents') THEN
    CREATE TABLE "EmailEvents" (
        "Id" uuid NOT NULL,
        "SvixId" character varying(255) NOT NULL,
        "EventType" character varying(64) NOT NULL,
        "ResendEmailId" character varying(255),
        "ToEmail" character varying(320),
        "Subject" character varying(500),
        "ClickedUrl" character varying(2000),
        "OccurredAt" timestamp with time zone NOT NULL,
        "ReceivedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_EmailEvents" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261010170242_AddEmailEvents') THEN
    CREATE INDEX "IX_EmailEvents_OccurredAt" ON "EmailEvents" ("OccurredAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261010170242_AddEmailEvents') THEN
    CREATE INDEX "IX_EmailEvents_ResendEmailId" ON "EmailEvents" ("ResendEmailId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261010170242_AddEmailEvents') THEN
    CREATE UNIQUE INDEX "IX_EmailEvents_SvixId" ON "EmailEvents" ("SvixId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261010170242_AddEmailEvents') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261010170242_AddEmailEvents', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011180000_AddAcknowledgedAtToOrders') THEN
    ALTER TABLE "Orders" ADD "AcknowledgedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011180000_AddAcknowledgedAtToOrders') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261011180000_AddAcknowledgedAtToOrders', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011183000_AddCollectedReferenceToOrders') THEN
    ALTER TABLE "Orders" ADD "CollectedReference" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261011183000_AddCollectedReferenceToOrders') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261011183000_AddCollectedReferenceToOrders', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013150000_AddBoardLayout') THEN
    ALTER TABLE "Affiliates" ADD "BoardLayout" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013150000_AddBoardLayout') THEN
    ALTER TABLE "Screens" ADD "Layout" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261013150000_AddBoardLayout') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261013150000_AddBoardLayout', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016180000_AddCompareAtPriceToInventoryItems') THEN
    ALTER TABLE "InventoryItems" ADD "CompareAtPrice" numeric(18,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016180000_AddCompareAtPriceToInventoryItems') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261016180000_AddCompareAtPriceToInventoryItems', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016190000_AddTrackStockToInventoryItems') THEN
    ALTER TABLE "InventoryItems" ADD "TrackStock" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016190000_AddTrackStockToInventoryItems') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261016190000_AddTrackStockToInventoryItems', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016230000_AddFeaturedPopularToInventoryItems') THEN
    ALTER TABLE "InventoryItems" ADD "Featured" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016230000_AddFeaturedPopularToInventoryItems') THEN
    ALTER TABLE "InventoryItems" ADD "Popular" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261016230000_AddFeaturedPopularToInventoryItems') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261016230000_AddFeaturedPopularToInventoryItems', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017150000_AddCompareAtPriceToProducts') THEN
    ALTER TABLE "Products" ADD "CompareAtPrice" numeric(18,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261017150000_AddCompareAtPriceToProducts') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261017150000_AddCompareAtPriceToProducts', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019230000_AddScreenWalls') THEN
    CREATE TABLE "ScreenWalls" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Mode" integer NOT NULL,
        "Cols" integer NOT NULL,
        "Rows" integer NOT NULL,
        "BezelPx" integer NOT NULL,
        "Calibrate" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ScreenWalls" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ScreenWalls_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019230000_AddScreenWalls') THEN
    CREATE INDEX "IX_ScreenWalls_AffiliateId" ON "ScreenWalls" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019230000_AddScreenWalls') THEN
    ALTER TABLE "Screens" ADD "WallId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019230000_AddScreenWalls') THEN
    ALTER TABLE "Screens" ADD "WallCol" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019230000_AddScreenWalls') THEN
    ALTER TABLE "Screens" ADD "WallRow" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019230000_AddScreenWalls') THEN
    CREATE INDEX "IX_Screens_WallId" ON "Screens" ("WallId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019230000_AddScreenWalls') THEN
    ALTER TABLE "Screens" ADD CONSTRAINT "FK_Screens_ScreenWalls_WallId" FOREIGN KEY ("WallId") REFERENCES "ScreenWalls" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261019230000_AddScreenWalls') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261019230000_AddScreenWalls', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020020000_AddPickupTrustRules') THEN
    ALTER TABLE "Orders" ADD "NoShowAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020020000_AddPickupTrustRules') THEN
    ALTER TABLE "Customers" ADD "NoShows" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020020000_AddPickupTrustRules') THEN
    ALTER TABLE "Affiliates" ADD "PickupNewCustomerLimit" numeric(10,2);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020020000_AddPickupTrustRules') THEN
    ALTER TABLE "Affiliates" ADD "PickupNoShowRequiresCard" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020020000_AddPickupTrustRules') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261020020000_AddPickupTrustRules', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020150000_AddBillingScreens') THEN
    ALTER TABLE "Affiliates" ADD "ExtraScreens" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020150000_AddBillingScreens') THEN
    ALTER TABLE "Affiliates" ADD "BillingInterval" character varying(8);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020150000_AddBillingScreens') THEN
    ALTER TABLE "Affiliates" ADD "SetupFeePaidAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261020150000_AddBillingScreens') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261020150000_AddBillingScreens', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024040000_AddScreenDevices') THEN
    CREATE TABLE "ScreenDevices" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "ScreenId" uuid,
        "DeviceId" character varying(64) NOT NULL,
        "FirstSeenAt" timestamp with time zone NOT NULL,
        "LastSeenAt" timestamp with time zone NOT NULL,
        "Visible" boolean NOT NULL,
        "ShowingKind" character varying(20),
        "ShowingLabel" character varying(200),
        "Viewport" character varying(20),
        "UserAgent" character varying(300),
        "AppVersion" character varying(40),
        "PendingCommand" character varying(20),
        "CommandAt" timestamp with time zone,
        "AlertSentAt" timestamp with time zone,
        "Archived" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ScreenDevices" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ScreenDevices_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024040000_AddScreenDevices') THEN
    CREATE INDEX "IX_ScreenDevices_AffiliateId_DeviceId" ON "ScreenDevices" ("AffiliateId", "DeviceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024040000_AddScreenDevices') THEN
    CREATE INDEX "IX_ScreenDevices_LastSeenAt" ON "ScreenDevices" ("LastSeenAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261024040000_AddScreenDevices') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261024040000_AddScreenDevices', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027170000_AddPlatformAddOnsAndScreensOnly') THEN
    ALTER TABLE "Affiliates" ADD "ScreensOnly" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027170000_AddPlatformAddOnsAndScreensOnly') THEN
    CREATE TABLE "PlatformAddOns" (
        "Id" uuid NOT NULL,
        "Key" character varying(60) NOT NULL,
        "NameEs" character varying(120) NOT NULL,
        "NameEn" character varying(120) NOT NULL,
        "DescEs" character varying(600) NOT NULL,
        "DescEn" character varying(600) NOT NULL,
        "Price" numeric(10,2) NOT NULL,
        "VolumePrice" numeric(10,2),
        "VolumeFromQty" integer,
        "Unit" integer NOT NULL,
        "SortOrder" integer NOT NULL,
        "Active" boolean NOT NULL,
        "UpdatedBy" character varying(255),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_PlatformAddOns" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027170000_AddPlatformAddOnsAndScreensOnly') THEN
    CREATE UNIQUE INDEX "IX_PlatformAddOns_Key" ON "PlatformAddOns" ("Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027170000_AddPlatformAddOnsAndScreensOnly') THEN
    INSERT INTO "PlatformAddOns" ("Id", "Key", "NameEs", "NameEn", "DescEs", "DescEn", "Price", "VolumePrice", "VolumeFromQty", "Unit", "SortOrder", "Active", "CreatedAt")
    VALUES ('a1d0c0de-0000-4000-8000-000000000001', 'screen-kit', 'Kit de pantalla', 'Screen kit', 'Dispositivo de streaming, montaje en pared, configuración e instalación. La TV la pone el negocio.', 'Streaming device, wall mount, setup and installation. The business provides the TV.', 249.0, NULL, NULL, 0, 1, TRUE, TIMESTAMPTZ '2026-10-27T17:00:00Z');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027170000_AddPlatformAddOnsAndScreensOnly') THEN
    INSERT INTO "PlatformAddOns" ("Id", "Key", "NameEs", "NameEn", "DescEs", "DescEn", "Price", "VolumePrice", "VolumeFromQty", "Unit", "SortOrder", "Active", "CreatedAt")
    VALUES ('a1d0c0de-0000-4000-8000-000000000002', 'screen-full-service', 'Servicio completo de pantallas', 'Full screen service', 'Cargamos y mantenemos tu contenido (menú, promos y comerciales), vigilamos cada TV y reemplazamos el dispositivo en 48 h. El diseño se genera solo desde tu catálogo.', 'We load and keep your content up to date (menu, promos and ads), watch every TV and replace the device within 48 h. The design is generated from your catalog.', 39.0, NULL, NULL, 2, 2, TRUE, TIMESTAMPTZ '2026-10-27T17:00:00Z');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027170000_AddPlatformAddOnsAndScreensOnly') THEN
    INSERT INTO "PlatformAddOns" ("Id", "Key", "NameEs", "NameEn", "DescEs", "DescEn", "Price", "VolumePrice", "VolumeFromQty", "Unit", "SortOrder", "Active", "CreatedAt")
    VALUES ('a1d0c0de-0000-4000-8000-000000000003', 'extra-screen', 'Pantalla adicional', 'Extra screen', 'Más pantallas de TV para tu menú o anuncios, además de las incluidas en tu plan.', 'More TV screens for your menu or ads, on top of those included in your plan.', 15.0, 10.0, 2, 1, 3, TRUE, TIMESTAMPTZ '2026-10-27T17:00:00Z');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027170000_AddPlatformAddOnsAndScreensOnly') THEN
    INSERT INTO "PlatformAddOns" ("Id", "Key", "NameEs", "NameEn", "DescEs", "DescEn", "Price", "VolumePrice", "VolumeFromQty", "Unit", "SortOrder", "Active", "CreatedAt")
    VALUES ('a1d0c0de-0000-4000-8000-000000000004', 'social-posts', 'Publicaciones en redes sociales', 'Social media posts', '8 publicaciones + 1 reel al mes (Instagram + Facebook), con diseño y copy.', '8 posts + 1 reel a month (Instagram + Facebook), with design and copy.', 249.0, NULL, NULL, 1, 4, TRUE, TIMESTAMPTZ '2026-10-27T17:00:00Z');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027170000_AddPlatformAddOnsAndScreensOnly') THEN
    INSERT INTO "PlatformAddOns" ("Id", "Key", "NameEs", "NameEn", "DescEs", "DescEn", "Price", "VolumePrice", "VolumeFromQty", "Unit", "SortOrder", "Active", "CreatedAt")
    VALUES ('a1d0c0de-0000-4000-8000-000000000005', 'support-247', 'Soporte 24/7', '24/7 support', 'Respuesta en menos de 1h, cualquier día, incluidos fines de semana y feriados.', 'Response in under 1h, any day, including weekends and holidays.', 200.0, NULL, NULL, 1, 5, TRUE, TIMESTAMPTZ '2026-10-27T17:00:00Z');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027170000_AddPlatformAddOnsAndScreensOnly') THEN
    INSERT INTO "PlatformAddOns" ("Id", "Key", "NameEs", "NameEn", "DescEs", "DescEn", "Price", "VolumePrice", "VolumeFromQty", "Unit", "SortOrder", "Active", "CreatedAt")
    VALUES ('a1d0c0de-0000-4000-8000-000000000006', 'consulting-block', 'Bloque de consultoría', 'Consulting block', '5 horas para integraciones, auditorías o estrategia. No expiran.', '5 hours for integrations, audits or strategy. Never expires.', 400.0, NULL, NULL, 3, 6, TRUE, TIMESTAMPTZ '2026-10-27T17:00:00Z');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027170000_AddPlatformAddOnsAndScreensOnly') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261027170000_AddPlatformAddOnsAndScreensOnly', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027180000_ModulePlanRulesAndCortesia') THEN
    CREATE TABLE "ModulePlanRules" (
        "Token" character varying(40) NOT NULL,
        "MinTier" integer NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "UpdatedBy" character varying(255),
        CONSTRAINT "PK_ModulePlanRules" PRIMARY KEY ("Token")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027180000_ModulePlanRulesAndCortesia') THEN
    ALTER TABLE "Affiliates" ADD "ModulosCortesia" character varying(200);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027180000_ModulePlanRulesAndCortesia') THEN

    UPDATE "Affiliates"
    SET "ModulosActivos" = "ModulosActivos" || ',station'
    WHERE "ModulosActivos" IS NOT NULL
      AND (',' || "ModulosActivos" || ',') LIKE '%,kitchen,%'
      AND (',' || "ModulosActivos" || ',') NOT LIKE '%,station,%';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027180000_ModulePlanRulesAndCortesia') THEN

    UPDATE "Affiliates"
    SET "ModulosActivos" = btrim(regexp_replace(',' || "ModulosActivos" || ',', ',billing,', ',', 'g'), ',')
    WHERE "ModulosActivos" IS NOT NULL
      AND (',' || "ModulosActivos" || ',') LIKE '%,billing,%';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027180000_ModulePlanRulesAndCortesia') THEN

    UPDATE "Affiliates"
    SET "ModulosCortesia" = '*'
    WHERE "ModulosCortesia" IS NULL
      AND ("Plan" = 1
           OR ("Plan" = 0 AND COALESCE("TrialOverrideEndsAt", "CreatedAt" + interval '30 days') < now()));
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261027180000_ModulePlanRulesAndCortesia') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261027180000_ModulePlanRulesAndCortesia', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261028180000_AddPlatformContent') THEN
    CREATE TABLE "PlatformContents" (
        "Id" uuid NOT NULL,
        "Key" character varying(120) NOT NULL,
        "Es" character varying(4000),
        "En" character varying(4000),
        "UpdatedBy" character varying(255),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_PlatformContents" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261028180000_AddPlatformContent') THEN
    CREATE UNIQUE INDEX "IX_PlatformContents_Key" ON "PlatformContents" ("Key");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261028180000_AddPlatformContent') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261028180000_AddPlatformContent', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029190000_AddDailyMenuAndBoardPanels') THEN
    ALTER TABLE "Affiliates" ADD COLUMN IF NOT EXISTS "DailyMenu" text NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029190000_AddDailyMenuAndBoardPanels') THEN
    ALTER TABLE "Affiliates" ADD COLUMN IF NOT EXISTS "BoardOrientation" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029190000_AddDailyMenuAndBoardPanels') THEN
    ALTER TABLE "Affiliates" ADD COLUMN IF NOT EXISTS "BoardPanels" text NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029190000_AddDailyMenuAndBoardPanels') THEN
    ALTER TABLE "Screens" ADD COLUMN IF NOT EXISTS "Orientation" integer NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029190000_AddDailyMenuAndBoardPanels') THEN
    ALTER TABLE "Screens" ADD COLUMN IF NOT EXISTS "Panels" text NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261029190000_AddDailyMenuAndBoardPanels') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261029190000_AddDailyMenuAndBoardPanels', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103050000_AddCommerceFoundations') THEN

    ALTER TABLE "InventoryItems" ADD COLUMN IF NOT EXISTS "Brand" character varying(120) NULL;
    ALTER TABLE "InventoryItems" ADD COLUMN IF NOT EXISTS "WeightOz" numeric(10,2) NULL;
    ALTER TABLE "InventoryItems" ADD COLUMN IF NOT EXISTS "LengthIn" numeric(10,2) NULL;
    ALTER TABLE "InventoryItems" ADD COLUMN IF NOT EXISTS "WidthIn" numeric(10,2) NULL;
    ALTER TABLE "InventoryItems" ADD COLUMN IF NOT EXISTS "HeightIn" numeric(10,2) NULL;
    ALTER TABLE "InventoryItems" ADD COLUMN IF NOT EXISTS "Hazmat" character varying(30) NULL;
    ALTER TABLE "InventoryItems" ADD COLUMN IF NOT EXISTS "ChokingWarning" character varying(30) NULL;
    ALTER TABLE "InventoryItems" ADD COLUMN IF NOT EXISTS "VariantGroup" character varying(80) NULL;
    ALTER TABLE "InventoryItems" ADD COLUMN IF NOT EXISTS "VariantLabel" character varying(60) NULL;
    ALTER TABLE "InventoryItems" ADD COLUMN IF NOT EXISTS "MinOrderQty" integer NULL;
    ALTER TABLE "InventoryItems" ADD COLUMN IF NOT EXISTS "OrderQtyMultiple" integer NULL;
    ALTER TABLE "InventoryItems" ADD COLUMN IF NOT EXISTS "MaxPerOrder" integer NULL;

    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "PriceListId" uuid NULL;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "PriceListName" character varying(120) NULL;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "FulfillmentType" character varying(20) NULL;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ShippingMethodId" uuid NULL;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ShippingMethodName" character varying(120) NULL;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ShippingCost" numeric(18,2) NOT NULL DEFAULT 0;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ShipToName" character varying(120) NULL;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ShipToLine1" character varying(200) NULL;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ShipToLine2" character varying(200) NULL;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ShipToCity" character varying(100) NULL;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ShipToState" character varying(50) NULL;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ShipToZip" character varying(20) NULL;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ShippingCarrier" character varying(30) NULL;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "TrackingNumber" character varying(80) NULL;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "ShippedAt" timestamp with time zone NULL;

    CREATE TABLE IF NOT EXISTS "PriceLists" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Description" character varying(500) NULL,
        "DiscountPercent" numeric(5,2) NOT NULL DEFAULT 0,
        "MinOrderAmount" numeric(18,2) NULL,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "SortOrder" integer NOT NULL DEFAULT 0,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NULL,
        "CreatedBy" text NULL,
        "UpdatedBy" text NULL,
        CONSTRAINT "PK_PriceLists" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PriceLists_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    CREATE INDEX IF NOT EXISTS "IX_PriceLists_AffiliateId" ON "PriceLists" ("AffiliateId");

    CREATE TABLE IF NOT EXISTS "PriceListItems" (
        "Id" uuid NOT NULL,
        "PriceListId" uuid NOT NULL,
        "ItemId" uuid NOT NULL,
        "Price" numeric(18,2) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NULL,
        CONSTRAINT "PK_PriceListItems" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PriceListItems_PriceLists_PriceListId" FOREIGN KEY ("PriceListId") REFERENCES "PriceLists" ("Id") ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IF NOT EXISTS "IX_PriceListItems_PriceListId_ItemId" ON "PriceListItems" ("PriceListId", "ItemId");

    CREATE TABLE IF NOT EXISTS "ShippingMethods" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "NameEn" character varying(120) NULL,
        "Price" numeric(18,2) NOT NULL DEFAULT 0,
        "FreeOverAmount" numeric(18,2) NULL,
        "MinDays" integer NULL,
        "MaxDays" integer NULL,
        "AllowsHazmat" boolean NOT NULL DEFAULT FALSE,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "SortOrder" integer NOT NULL DEFAULT 0,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NULL,
        "CreatedBy" text NULL,
        "UpdatedBy" text NULL,
        CONSTRAINT "PK_ShippingMethods" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ShippingMethods_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    CREATE INDEX IF NOT EXISTS "IX_ShippingMethods_AffiliateId" ON "ShippingMethods" ("AffiliateId");

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103050000_AddCommerceFoundations') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261103050000_AddCommerceFoundations', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103180000_AddStoreCustomers') THEN

    ALTER TABLE "Affiliates" ADD COLUMN IF NOT EXISTS "StoreSignupMode" character varying(20) NOT NULL DEFAULT 'approval';
    ALTER TABLE "Affiliates" ADD COLUMN IF NOT EXISTS "StoreDefaultPriceListId" uuid NULL;
    ALTER TABLE "Affiliates" ADD COLUMN IF NOT EXISTS "StoreSignupNote" character varying(500) NULL;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "StoreCustomerId" uuid NULL;
    CREATE INDEX IF NOT EXISTS "IX_Orders_StoreCustomerId" ON "Orders" ("StoreCustomerId");

    CREATE TABLE IF NOT EXISTS "StoreCustomers" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "SupabaseUserId" character varying(64) NOT NULL,
        "Email" character varying(200) NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Phone" character varying(30) NULL,
        "BusinessName" character varying(160) NULL,
        "TaxId" character varying(40) NULL,
        "Message" character varying(500) NULL,
        "Status" character varying(20) NOT NULL DEFAULT 'pending',
        "PriceListId" uuid NULL,
        "Notes" character varying(1000) NULL,
        "ApprovedAt" timestamp with time zone NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NULL,
        CONSTRAINT "PK_StoreCustomers" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StoreCustomers_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StoreCustomers_PriceLists_PriceListId" FOREIGN KEY ("PriceListId") REFERENCES "PriceLists" ("Id") ON DELETE SET NULL
    );
    CREATE UNIQUE INDEX IF NOT EXISTS "IX_StoreCustomers_AffiliateId_SupabaseUserId" ON "StoreCustomers" ("AffiliateId", "SupabaseUserId");
    CREATE INDEX IF NOT EXISTS "IX_StoreCustomers_AffiliateId_Status" ON "StoreCustomers" ("AffiliateId", "Status");

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103180000_AddStoreCustomers') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261103180000_AddStoreCustomers', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103200000_AddSalesTax') THEN

    ALTER TABLE "Affiliates" ADD COLUMN IF NOT EXISTS "SalesTaxRate" numeric(6,3) NULL;
    ALTER TABLE "Affiliates" ADD COLUMN IF NOT EXISTS "SalesTaxOnShipping" boolean NOT NULL DEFAULT TRUE;
    ALTER TABLE "PriceLists" ADD COLUMN IF NOT EXISTS "TaxExempt" boolean NOT NULL DEFAULT FALSE;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "TaxRate" numeric(6,3) NULL;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103200000_AddSalesTax') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261103200000_AddSalesTax', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103210000_AddSalesTaxState') THEN
    ALTER TABLE "Affiliates" ADD COLUMN IF NOT EXISTS "SalesTaxState" character varying(2) NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103210000_AddSalesTaxState') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261103210000_AddSalesTaxState', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103230000_AddStoreLayoutAndVolumeTiers') THEN

    ALTER TABLE "Affiliates" ADD COLUMN IF NOT EXISTS "StoreLayout" character varying(20) NOT NULL DEFAULT 'boutique';
    ALTER TABLE "InventoryItems" ADD COLUMN IF NOT EXISTS "VolumeTiers" text NULL;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261103230000_AddStoreLayoutAndVolumeTiers') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261103230000_AddStoreLayoutAndVolumeTiers', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104000000_AddProspectIntakes') THEN

    CREATE TABLE IF NOT EXISTS "ProspectIntakes" (
        "Id" uuid NOT NULL,
        "Token" uuid NOT NULL,
        "BusinessName" character varying(160) NOT NULL,
        "ContactName" character varying(120) NULL,
        "ContactEmail" character varying(200) NULL,
        "ContactPhone" character varying(40) NULL,
        "Title" character varying(200) NOT NULL,
        "IntroMessage" character varying(2000) NULL,
        "AdvisorName" character varying(120) NULL,
        "AdvisorContact" character varying(200) NULL,
        "Language" character varying(5) NOT NULL DEFAULT 'es',
        "QuestionsJson" text NOT NULL DEFAULT '[]',
        "AnswersJson" text NULL,
        "Status" character varying(20) NOT NULL DEFAULT 'open',
        "LastSavedAt" timestamp with time zone NULL,
        "SubmittedAt" timestamp with time zone NULL,
        "SubmittedIp" character varying(64) NULL,
        "ReviewedAt" timestamp with time zone NULL,
        "CreatedByEmail" character varying(255) NOT NULL,
        "Notes" character varying(4000) NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NULL,
        CONSTRAINT "PK_ProspectIntakes" PRIMARY KEY ("Id")
    );
    CREATE UNIQUE INDEX IF NOT EXISTS "IX_ProspectIntakes_Token" ON "ProspectIntakes" ("Token");
    CREATE INDEX IF NOT EXISTS "IX_ProspectIntakes_Status" ON "ProspectIntakes" ("Status");

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261104000000_AddProspectIntakes') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261104000000_AddProspectIntakes', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105190000_AddShippingWeightRatesAndStates') THEN
    ALTER TABLE "ShippingMethods" ADD COLUMN IF NOT EXISTS "WeightRates" text NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105190000_AddShippingWeightRatesAndStates') THEN
    ALTER TABLE "ShippingMethods" ADD COLUMN IF NOT EXISTS "States" text NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105190000_AddShippingWeightRatesAndStates') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261105190000_AddShippingWeightRatesAndStates', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105200000_AddCarrierAccounts') THEN

    CREATE TABLE IF NOT EXISTS "CarrierAccounts" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Provider" character varying(30) NOT NULL,
        "ApiKeyEnc" text NULL,
        "ApiKeyLast4" character varying(8) NULL,
        "Service" character varying(40) NOT NULL,
        "Package" character varying(40) NOT NULL,
        "LengthIn" numeric(10,2) NOT NULL DEFAULT 12,
        "WidthIn" numeric(10,2) NOT NULL DEFAULT 9,
        "HeightIn" numeric(10,2) NOT NULL DEFAULT 6,
        "BoxWeightOz" numeric(10,2) NOT NULL DEFAULT 0,
        "FromName" text NULL,
        "FromPhone" text NULL,
        "FromAddress1" text NULL,
        "FromAddress2" text NULL,
        "FromCity" text NULL,
        "FromState" text NULL,
        "FromZip" text NULL,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone NULL,
        "CreatedBy" text NULL,
        "UpdatedBy" text NULL,
        CONSTRAINT "PK_CarrierAccounts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_CarrierAccounts_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    CREATE UNIQUE INDEX IF NOT EXISTS "IX_CarrierAccounts_AffiliateId_Provider" ON "CarrierAccounts" ("AffiliateId", "Provider");
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "LabelUrl" text NULL;
    ALTER TABLE "Orders" ADD COLUMN IF NOT EXISTS "LabelCost" numeric(18,2) NULL;

    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105200000_AddCarrierAccounts') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261105200000_AddCarrierAccounts', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    ALTER TABLE "Customers" ADD "Address" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    ALTER TABLE "Customers" ADD "BillingCycle" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    ALTER TABLE "Customers" ADD "CreditDays" integer;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    ALTER TABLE "Customers" ADD "DeliveryFrequency" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    ALTER TABLE "Customers" ADD "GpsLat" numeric(9,6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    ALTER TABLE "Customers" ADD "GpsLng" numeric(9,6);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    ALTER TABLE "Customers" ADD "PriceListId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    ALTER TABLE "Customers" ADD "Rnc" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    ALTER TABLE "Customers" ADD "RouteId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    CREATE TABLE "Contracts" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "DeliveryFrequency" character varying(20) NOT NULL,
        "BillingCycle" character varying(20) NOT NULL,
        "CreditDays" integer NOT NULL,
        "AgreedPrices" text,
        "StartDate" date NOT NULL,
        "EndDate" date,
        "IsActive" boolean NOT NULL,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Contracts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Contracts_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Contracts_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    CREATE TABLE "Payments" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "InvoiceId" uuid,
        "Amount" numeric(18,2) NOT NULL,
        "Method" character varying(20) NOT NULL,
        "PaidAt" timestamp with time zone NOT NULL,
        "Notes" text,
        "RegisteredBy" text,
        "IdempotencyKey" character varying(100),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Payments" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Payments_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Payments_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Payments_Invoices_InvoiceId" FOREIGN KEY ("InvoiceId") REFERENCES "Invoices" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    CREATE INDEX "IX_Contracts_AffiliateId_CustomerId" ON "Contracts" ("AffiliateId", "CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    CREATE INDEX "IX_Contracts_CustomerId" ON "Contracts" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    CREATE INDEX "IX_Payments_AffiliateId_CustomerId" ON "Payments" ("AffiliateId", "CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    CREATE UNIQUE INDEX "IX_Payments_AffiliateId_IdempotencyKey" ON "Payments" ("AffiliateId", "IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    CREATE INDEX "IX_Payments_CustomerId" ON "Payments" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    CREATE INDEX "IX_Payments_InvoiceId" ON "Payments" ("InvoiceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107195909_AddContractsAndPayments') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261107195909_AddContractsAndPayments', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107214554_AddContainersAndAnaqueles') THEN
    CREATE TABLE "Anaqueles" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "CustomerId" uuid,
        "QrCode" character varying(64) NOT NULL,
        "Label" character varying(50),
        "Location" character varying(200),
        "Capacity" integer NOT NULL,
        "Status" character varying(30) NOT NULL,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Anaqueles" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Anaqueles_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Anaqueles_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107214554_AddContainersAndAnaqueles') THEN
    CREATE TABLE "ContainerTypes" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "CapacityLiters" numeric(8,2),
        "Deposit" numeric(18,2) NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_ContainerTypes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ContainerTypes_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107214554_AddContainersAndAnaqueles') THEN
    CREATE TABLE "ContainerMovements" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "ContainerTypeId" uuid NOT NULL,
        "AnaquelId" uuid,
        "DeliveryId" uuid,
        "BottleId" uuid,
        "Type" character varying(20) NOT NULL,
        "Quantity" integer NOT NULL,
        "OccurredAt" timestamp with time zone NOT NULL,
        "RegisteredBy" character varying(100),
        "Notes" text,
        "IdempotencyKey" character varying(100),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ContainerMovements" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ContainerMovements_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_ContainerMovements_Anaqueles_AnaquelId" FOREIGN KEY ("AnaquelId") REFERENCES "Anaqueles" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ContainerMovements_ContainerTypes_ContainerTypeId" FOREIGN KEY ("ContainerTypeId") REFERENCES "ContainerTypes" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_ContainerMovements_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107214554_AddContainersAndAnaqueles') THEN
    CREATE INDEX "IX_Anaqueles_AffiliateId_CustomerId" ON "Anaqueles" ("AffiliateId", "CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107214554_AddContainersAndAnaqueles') THEN
    CREATE INDEX "IX_Anaqueles_CustomerId" ON "Anaqueles" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107214554_AddContainersAndAnaqueles') THEN
    CREATE UNIQUE INDEX "IX_Anaqueles_QrCode" ON "Anaqueles" ("QrCode");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107214554_AddContainersAndAnaqueles') THEN
    CREATE INDEX "IX_ContainerMovements_AffiliateId_CustomerId_ContainerTypeId" ON "ContainerMovements" ("AffiliateId", "CustomerId", "ContainerTypeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107214554_AddContainersAndAnaqueles') THEN
    CREATE UNIQUE INDEX "IX_ContainerMovements_AffiliateId_IdempotencyKey" ON "ContainerMovements" ("AffiliateId", "IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107214554_AddContainersAndAnaqueles') THEN
    CREATE INDEX "IX_ContainerMovements_AnaquelId" ON "ContainerMovements" ("AnaquelId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107214554_AddContainersAndAnaqueles') THEN
    CREATE INDEX "IX_ContainerMovements_ContainerTypeId" ON "ContainerMovements" ("ContainerTypeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107214554_AddContainersAndAnaqueles') THEN
    CREATE INDEX "IX_ContainerMovements_CustomerId" ON "ContainerMovements" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107214554_AddContainersAndAnaqueles') THEN
    CREATE INDEX "IX_ContainerTypes_AffiliateId_Name" ON "ContainerTypes" ("AffiliateId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107214554_AddContainersAndAnaqueles') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261107214554_AddContainersAndAnaqueles', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE TABLE "Vehicles" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Plate" character varying(20) NOT NULL,
        "Name" character varying(100),
        "CapacityContainers" integer NOT NULL,
        "DriverId" uuid,
        "Status" character varying(20) NOT NULL,
        "IsActive" boolean NOT NULL,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Vehicles" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Vehicles_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE TABLE "DeliveryRoutes" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Name" character varying(100) NOT NULL,
        "RouteDate" date NOT NULL,
        "VehicleId" uuid,
        "DriverId" uuid,
        "Status" character varying(20) NOT NULL,
        "LoadedFull" integer,
        "DepartedAt" timestamp with time zone,
        "ClosedAt" timestamp with time zone,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_DeliveryRoutes" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_DeliveryRoutes_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_DeliveryRoutes_Vehicles_VehicleId" FOREIGN KEY ("VehicleId") REFERENCES "Vehicles" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE TABLE "RouteStops" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "RouteId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "SortOrder" integer NOT NULL,
        "Status" character varying(20) NOT NULL,
        "VisitedAt" timestamp with time zone,
        "Notes" text,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_RouteStops" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RouteStops_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_RouteStops_DeliveryRoutes_RouteId" FOREIGN KEY ("RouteId") REFERENCES "DeliveryRoutes" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE TABLE "Deliveries" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "RouteStopId" uuid NOT NULL,
        "CustomerId" uuid NOT NULL,
        "DeliveredAt" timestamp with time zone NOT NULL,
        "InvoiceId" uuid,
        "PaymentId" uuid,
        "ReceivedBy" character varying(100),
        "Notes" text,
        "RegisteredBy" character varying(100),
        "IdempotencyKey" character varying(100),
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Deliveries" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Deliveries_Customers_CustomerId" FOREIGN KEY ("CustomerId") REFERENCES "Customers" ("Id") ON DELETE RESTRICT,
        CONSTRAINT "FK_Deliveries_RouteStops_RouteStopId" FOREIGN KEY ("RouteStopId") REFERENCES "RouteStops" ("Id") ON DELETE RESTRICT
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE TABLE "DeliveryItems" (
        "Id" uuid NOT NULL,
        "DeliveryId" uuid NOT NULL,
        "ItemId" uuid,
        "Description" character varying(200) NOT NULL,
        "Quantity" numeric(18,2) NOT NULL,
        "UnitPrice" numeric(18,2) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_DeliveryItems" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_DeliveryItems_Deliveries_DeliveryId" FOREIGN KEY ("DeliveryId") REFERENCES "Deliveries" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE INDEX "IX_Deliveries_AffiliateId_CustomerId" ON "Deliveries" ("AffiliateId", "CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE UNIQUE INDEX "IX_Deliveries_AffiliateId_IdempotencyKey" ON "Deliveries" ("AffiliateId", "IdempotencyKey") WHERE "IdempotencyKey" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE INDEX "IX_Deliveries_CustomerId" ON "Deliveries" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE INDEX "IX_Deliveries_RouteStopId" ON "Deliveries" ("RouteStopId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE INDEX "IX_DeliveryItems_DeliveryId" ON "DeliveryItems" ("DeliveryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE INDEX "IX_DeliveryRoutes_AffiliateId_RouteDate" ON "DeliveryRoutes" ("AffiliateId", "RouteDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE INDEX "IX_DeliveryRoutes_VehicleId" ON "DeliveryRoutes" ("VehicleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE INDEX "IX_RouteStops_AffiliateId_CustomerId" ON "RouteStops" ("AffiliateId", "CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE INDEX "IX_RouteStops_CustomerId" ON "RouteStops" ("CustomerId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE INDEX "IX_RouteStops_RouteId_SortOrder" ON "RouteStops" ("RouteId", "SortOrder");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    CREATE INDEX "IX_Vehicles_AffiliateId_Plate" ON "Vehicles" ("AffiliateId", "Plate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107215319_AddVehiclesRoutesDeliveries') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261107215319_AddVehiclesRoutesDeliveries', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107221612_AddContractTemplateAndLoanQuotas') THEN
    ALTER TABLE "Contracts" ADD "BottlesLoaned" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107221612_AddContractTemplateAndLoanQuotas') THEN
    ALTER TABLE "Contracts" ADD "ContractNumber" character varying(20);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107221612_AddContractTemplateAndLoanQuotas') THEN
    ALTER TABLE "Contracts" ADD "RacksLoaned" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107221612_AddContractTemplateAndLoanQuotas') THEN
    CREATE TABLE "ContractTemplates" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Title" character varying(200) NOT NULL,
        "Body" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_ContractTemplates" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ContractTemplates_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107221612_AddContractTemplateAndLoanQuotas') THEN
    CREATE UNIQUE INDEX "IX_Contracts_AffiliateId_ContractNumber" ON "Contracts" ("AffiliateId", "ContractNumber") WHERE "ContractNumber" IS NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107221612_AddContractTemplateAndLoanQuotas') THEN
    CREATE UNIQUE INDEX "IX_ContractTemplates_AffiliateId" ON "ContractTemplates" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261107221612_AddContractTemplateAndLoanQuotas') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261107221612_AddContractTemplateAndLoanQuotas', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108014341_AddExpenses') THEN
    CREATE TABLE "Expenses" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "Category" character varying(30) NOT NULL,
        "Description" character varying(200) NOT NULL,
        "Amount" numeric(18,2) NOT NULL,
        "ExpenseDate" date NOT NULL,
        "Recurrence" character varying(10) NOT NULL,
        "Vendor" character varying(120),
        "PaymentMethod" character varying(10),
        "Notes" text,
        "SourceExpenseId" uuid,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        "CreatedBy" text,
        "UpdatedBy" text,
        CONSTRAINT "PK_Expenses" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Expenses_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108014341_AddExpenses') THEN
    CREATE INDEX "IX_Expenses_AffiliateId_ExpenseDate" ON "Expenses" ("AffiliateId", "ExpenseDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108014341_AddExpenses') THEN
    CREATE INDEX "IX_Expenses_SourceExpenseId" ON "Expenses" ("SourceExpenseId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108014341_AddExpenses') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108014341_AddExpenses', '8.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;


DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108020311_AddReceivablesSettings') THEN
    CREATE TABLE "ReceivablesSettings" (
        "Id" uuid NOT NULL,
        "AffiliateId" uuid NOT NULL,
        "AgingBasis" character varying(10) NOT NULL,
        "TaxMode" character varying(10) NOT NULL,
        "TaxRate" numeric(6,3) NOT NULL,
        "TaxLabel" character varying(20) NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ReceivablesSettings" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ReceivablesSettings_Affiliates_AffiliateId" FOREIGN KEY ("AffiliateId") REFERENCES "Affiliates" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108020311_AddReceivablesSettings') THEN
    CREATE UNIQUE INDEX "IX_ReceivablesSettings_AffiliateId" ON "ReceivablesSettings" ("AffiliateId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108020311_AddReceivablesSettings') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108020311_AddReceivablesSettings', '8.0.0');
    END IF;
END $EF$;
COMMIT;

