using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Sello editorial: contratos autor ↔ sello (ImprintAuthors) y, en cada obra, la llave al sello (ImprintId)
    /// y el nombre del autor como sale en el libro (Byline). Solo AGREGA (expandir): una tabla, dos columnas
    /// que admiten null, índices y llaves foráneas. El texto libre Works.Imprint se queda para sellos de fuera.
    /// Escrita a mano e idempotente.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261128070000_AddImprints")]
    public partial class AddImprints : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""ImprintAuthors"" (
    ""Id"" uuid NOT NULL,
    ""ImprintId"" uuid NOT NULL,
    ""AuthorId"" uuid NOT NULL,
    ""Status"" character varying(20) NOT NULL DEFAULT 'invited',
    ""RoyaltyEbookPercent"" numeric(5,2) NOT NULL DEFAULT 0,
    ""RoyaltyPrintPercent"" numeric(5,2) NOT NULL DEFAULT 0,
    ""Rights"" character varying(100) NOT NULL DEFAULT 'ebook',
    ""StartsOn"" timestamp with time zone NULL,
    ""EndsOn"" timestamp with time zone NULL,
    ""Notes"" character varying(2000) NULL,
    ""RespondedAt"" timestamp with time zone NULL,
    ""EndedAt"" timestamp with time zone NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    CONSTRAINT ""PK_ImprintAuthors"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_ImprintAuthors_Affiliates_ImprintId"" FOREIGN KEY (""ImprintId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_ImprintAuthors_Affiliates_AuthorId"" FOREIGN KEY (""AuthorId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_ImprintAuthors_ImprintId_AuthorId"" ON ""ImprintAuthors"" (""ImprintId"", ""AuthorId"");
CREATE INDEX IF NOT EXISTS ""IX_ImprintAuthors_AuthorId_Status"" ON ""ImprintAuthors"" (""AuthorId"", ""Status"");

ALTER TABLE ""Works"" ADD COLUMN IF NOT EXISTS ""ImprintId"" uuid NULL;
ALTER TABLE ""Works"" ADD COLUMN IF NOT EXISTS ""Byline"" character varying(120) NULL;
CREATE INDEX IF NOT EXISTS ""IX_Works_ImprintId"" ON ""Works"" (""ImprintId"");
DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_Works_Affiliates_ImprintId') THEN
        ALTER TABLE ""Works"" ADD CONSTRAINT ""FK_Works_Affiliates_ImprintId""
            FOREIGN KEY (""ImprintId"") REFERENCES ""Affiliates"" (""Id"") ON DELETE SET NULL;
    END IF;
END $$;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
ALTER TABLE ""Works"" DROP CONSTRAINT IF EXISTS ""FK_Works_Affiliates_ImprintId"";
DROP INDEX IF EXISTS ""IX_Works_ImprintId"";
ALTER TABLE ""Works"" DROP COLUMN IF EXISTS ""Byline"";
ALTER TABLE ""Works"" DROP COLUMN IF EXISTS ""ImprintId"";
DROP TABLE IF EXISTS ""ImprintAuthors"";
");
        }
    }
}
