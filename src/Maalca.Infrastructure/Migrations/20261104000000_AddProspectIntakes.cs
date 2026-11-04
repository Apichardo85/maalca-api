using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Tabla ProspectIntakes: levantamientos de datos de prospectos (/ops/levantamientos →
    /// enlace público /levantamiento/{token}). Escrita a mano (sin Designer), mismo patrón que
    /// AddStoreCustomers; IF NOT EXISTS para que reaplicar no falle.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261104000000_AddProspectIntakes")]
    public partial class AddProspectIntakes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""ProspectIntakes"" (
    ""Id"" uuid NOT NULL,
    ""Token"" uuid NOT NULL,
    ""BusinessName"" character varying(160) NOT NULL,
    ""ContactName"" character varying(120) NULL,
    ""ContactEmail"" character varying(200) NULL,
    ""ContactPhone"" character varying(40) NULL,
    ""Title"" character varying(200) NOT NULL,
    ""IntroMessage"" character varying(2000) NULL,
    ""AdvisorName"" character varying(120) NULL,
    ""AdvisorContact"" character varying(200) NULL,
    ""Language"" character varying(5) NOT NULL DEFAULT 'es',
    ""QuestionsJson"" text NOT NULL DEFAULT '[]',
    ""AnswersJson"" text NULL,
    ""Status"" character varying(20) NOT NULL DEFAULT 'open',
    ""LastSavedAt"" timestamp with time zone NULL,
    ""SubmittedAt"" timestamp with time zone NULL,
    ""SubmittedIp"" character varying(64) NULL,
    ""ReviewedAt"" timestamp with time zone NULL,
    ""CreatedByEmail"" character varying(255) NOT NULL,
    ""Notes"" character varying(4000) NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_ProspectIntakes"" PRIMARY KEY (""Id"")
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ProspectIntakes_Token"" ON ""ProspectIntakes"" (""Token"");
CREATE INDEX IF NOT EXISTS ""IX_ProspectIntakes_Status"" ON ""ProspectIntakes"" (""Status"");
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TABLE IF EXISTS ""ProspectIntakes"";");
        }
    }
}
