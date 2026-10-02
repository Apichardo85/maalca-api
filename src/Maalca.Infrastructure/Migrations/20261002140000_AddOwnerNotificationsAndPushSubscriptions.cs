using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// OwnerNotifications (avisos para el dueño: badges/campana/push) y PushSubscriptions (dispositivos
    /// con Web Push activado). Tablas nuevas, sin backfill. Escrita a mano (sin Designer): el atributo
    /// [Migration] la registra y el snapshot ya incluye ambas entidades.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261002140000_AddOwnerNotificationsAndPushSubscriptions")]
    public partial class AddOwnerNotificationsAndPushSubscriptions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // IF NOT EXISTS en todo: las tablas pudieron crearse a mano antes del deploy; la migracion no debe tumbar el arranque.
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""OwnerNotifications"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""Type"" character varying(30) NOT NULL,
    ""Title"" character varying(200) NOT NULL,
    ""Body"" character varying(500) NULL,
    ""TitleEn"" character varying(200) NOT NULL,
    ""BodyEn"" character varying(500) NULL,
    ""Url"" character varying(200) NULL,
    ""EntityId"" uuid NULL,
    ""ReadAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_OwnerNotifications"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_OwnerNotifications_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_OwnerNotifications_AffiliateId_ReadAt_CreatedAt"" ON ""OwnerNotifications"" (""AffiliateId"", ""ReadAt"", ""CreatedAt"");

CREATE TABLE IF NOT EXISTS ""PushSubscriptions"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""Endpoint"" character varying(1000) NOT NULL,
    ""P256dh"" character varying(300) NOT NULL,
    ""Auth"" character varying(100) NOT NULL,
    ""Lang"" character varying(5) NOT NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_PushSubscriptions"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_PushSubscriptions_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_PushSubscriptions_AffiliateId"" ON ""PushSubscriptions"" (""AffiliateId"");
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_PushSubscriptions_Endpoint"" ON ""PushSubscriptions"" (""Endpoint"");
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"OwnerNotifications\"; DROP TABLE IF EXISTS \"PushSubscriptions\";");
        }
    }
}
