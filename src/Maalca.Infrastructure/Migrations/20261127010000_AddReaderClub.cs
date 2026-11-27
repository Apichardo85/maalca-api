using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Club de lectores del espacio de autor: lectores, ajustes, cartas, entregas y progreso de lectura.
    /// Solo AGREGA tablas (expandir). Escrita a mano e idempotente (IF NOT EXISTS).
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261127010000_AddReaderClub")]
    public partial class AddReaderClub : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""Readers"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""Email"" character varying(254) NOT NULL,
    ""Name"" character varying(120) NULL,
    ""Language"" character varying(5) NOT NULL DEFAULT 'es',
    ""Status"" character varying(20) NOT NULL DEFAULT 'pending',
    ""LettersOptIn"" boolean NOT NULL DEFAULT TRUE,
    ""ConfirmToken"" uuid NOT NULL,
    ""ConfirmedAt"" timestamp with time zone NULL,
    ""UnsubscribedAt"" timestamp with time zone NULL,
    ""LastLoginAt"" timestamp with time zone NULL,
    ""Source"" character varying(20) NOT NULL DEFAULT 'page',
    ""SourceWorkId"" uuid NULL,
    ""StripeCustomerId"" character varying(100) NULL,
    ""StripeSubscriptionId"" character varying(100) NULL,
    ""MembershipStatus"" character varying(20) NOT NULL DEFAULT 'none',
    ""MembershipCurrentPeriodEnd"" timestamp with time zone NULL,
    ""MembershipStartedAt"" timestamp with time zone NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_Readers"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_Readers_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_Readers_Works_SourceWorkId"" FOREIGN KEY (""SourceWorkId"") REFERENCES ""Works"" (""Id"") ON DELETE SET NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Readers_AffiliateId_Email"" ON ""Readers"" (""AffiliateId"", ""Email"");
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Readers_ConfirmToken"" ON ""Readers"" (""ConfirmToken"");
CREATE INDEX IF NOT EXISTS ""IX_Readers_StripeSubscriptionId"" ON ""Readers"" (""StripeSubscriptionId"");
CREATE INDEX IF NOT EXISTS ""IX_Readers_SourceWorkId"" ON ""Readers"" (""SourceWorkId"");

CREATE TABLE IF NOT EXISTS ""ReaderClubSettings"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""LettersEnabled"" boolean NOT NULL DEFAULT TRUE,
    ""LettersPitch"" character varying(600) NULL,
    ""LettersPitchEn"" character varying(600) NULL,
    ""MembershipEnabled"" boolean NOT NULL DEFAULT FALSE,
    ""MembershipMonthlyPrice"" numeric(10,2) NULL,
    ""MembershipCurrency"" character varying(3) NOT NULL DEFAULT 'USD',
    ""MembershipBenefits"" text NULL,
    ""MembershipBenefitsEn"" text NULL,
    ""StripeProductId"" character varying(100) NULL,
    ""StripePriceId"" character varying(100) NULL,
    ""StripePriceAmount"" numeric(10,2) NULL,
    ""StripePriceCurrency"" character varying(3) NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_ReaderClubSettings"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ReaderClubSettings_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ReaderClubSettings_AffiliateId"" ON ""ReaderClubSettings"" (""AffiliateId"");

CREATE TABLE IF NOT EXISTS ""ReaderLetters"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""Subject"" character varying(200) NOT NULL,
    ""BodyHtml"" text NULL,
    ""Audience"" character varying(20) NOT NULL DEFAULT 'all',
    ""Status"" character varying(20) NOT NULL DEFAULT 'draft',
    ""QueuedAt"" timestamp with time zone NULL,
    ""SentAt"" timestamp with time zone NULL,
    ""RecipientCount"" integer NOT NULL DEFAULT 0,
    ""SentCount"" integer NOT NULL DEFAULT 0,
    ""FailedCount"" integer NOT NULL DEFAULT 0,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_ReaderLetters"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ReaderLetters_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_ReaderLetters_AffiliateId_CreatedAt"" ON ""ReaderLetters"" (""AffiliateId"", ""CreatedAt"");
CREATE INDEX IF NOT EXISTS ""IX_ReaderLetters_Status"" ON ""ReaderLetters"" (""Status"");

CREATE TABLE IF NOT EXISTS ""ReaderLetterDeliveries"" (
    ""Id"" uuid NOT NULL,
    ""LetterId"" uuid NOT NULL,
    ""ReaderId"" uuid NOT NULL,
    ""SentAt"" timestamp with time zone NULL,
    ""Error"" character varying(300) NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_ReaderLetterDeliveries"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ReaderLetterDeliveries_ReaderLetters_LetterId"" FOREIGN KEY (""LetterId"") REFERENCES ""ReaderLetters"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_ReaderLetterDeliveries_Readers_ReaderId"" FOREIGN KEY (""ReaderId"") REFERENCES ""Readers"" (""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ReaderLetterDeliveries_LetterId_ReaderId"" ON ""ReaderLetterDeliveries"" (""LetterId"", ""ReaderId"");
CREATE INDEX IF NOT EXISTS ""IX_ReaderLetterDeliveries_ReaderId"" ON ""ReaderLetterDeliveries"" (""ReaderId"");

CREATE TABLE IF NOT EXISTS ""ReaderProgress"" (
    ""Id"" uuid NOT NULL,
    ""ReaderId"" uuid NOT NULL,
    ""WorkId"" uuid NOT NULL,
    ""ChapterNumber"" integer NOT NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_ReaderProgress"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ReaderProgress_Readers_ReaderId"" FOREIGN KEY (""ReaderId"") REFERENCES ""Readers"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_ReaderProgress_Works_WorkId"" FOREIGN KEY (""WorkId"") REFERENCES ""Works"" (""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ReaderProgress_ReaderId_WorkId"" ON ""ReaderProgress"" (""ReaderId"", ""WorkId"");
CREATE INDEX IF NOT EXISTS ""IX_ReaderProgress_WorkId"" ON ""ReaderProgress"" (""WorkId"");
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS ""ReaderProgress"";
DROP TABLE IF EXISTS ""ReaderLetterDeliveries"";
DROP TABLE IF EXISTS ""ReaderLetters"";
DROP TABLE IF EXISTS ""ReaderClubSettings"";
DROP TABLE IF EXISTS ""Readers"";
");
        }
    }
}
