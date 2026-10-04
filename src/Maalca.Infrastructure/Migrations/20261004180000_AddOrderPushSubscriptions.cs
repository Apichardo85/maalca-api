using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// OrderPushSubscriptions: dispositivos del cliente con "Avísame" activado en /t/{token}. Tabla nueva, sin backfill.
    /// Escrita a mano (sin Designer): el atributo [Migration] la registra y el snapshot ya incluye la entidad.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261004180000_AddOrderPushSubscriptions")]
    public partial class AddOrderPushSubscriptions : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""OrderPushSubscriptions"" (
    ""Id"" uuid NOT NULL,
    ""OrderId"" uuid NOT NULL,
    ""Endpoint"" character varying(1000) NOT NULL,
    ""P256dh"" character varying(300) NOT NULL,
    ""Auth"" character varying(100) NOT NULL,
    ""Lang"" character varying(5) NOT NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_OrderPushSubscriptions"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_OrderPushSubscriptions_Orders_OrderId"" FOREIGN KEY (""OrderId"") REFERENCES ""Orders"" (""Id"") ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS ""IX_OrderPushSubscriptions_OrderId"" ON ""OrderPushSubscriptions"" (""OrderId"");
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_OrderPushSubscriptions_OrderId_Endpoint"" ON ""OrderPushSubscriptions"" (""OrderId"", ""Endpoint"");
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TABLE IF EXISTS \"OrderPushSubscriptions\";");
        }
    }
}
