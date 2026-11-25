using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Llave de contacto del cliente: Customers."PhoneKey" y Customers."EmailKey" como columnas
    /// GENERADAS (STORED) a partir de Phone/Email, más sus índices. Antes el cliente se buscaba
    /// comparando el texto del teléfono tal cual ("c.Phone == phone"), así que quien escribía
    /// "(908) 560-9244" en vez de "9085609244" quedaba como cliente NUEVO y su historial se partía
    /// en dos. Al ser generadas por Postgres, ningún camino de escritura puede dejarlas fuera de
    /// sincronía — tampoco un UPDATE a mano. La misma regla en C# está en ContactKey.
    ///
    /// Además rellena Orders."CustomerId" en los pedidos viejos que lo tienen en null: de ahí en
    /// adelante los enlaces se hacen por esa llave y no volviendo a comparar teléfonos.
    /// Solo agrega columnas e índices y rellena un campo vacío: nada existente cambia de valor.
    /// Escrita a mano, mismo patrón que AddRouteStopKindAndPlannedLines.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261125170000_AddCustomerContactKeys")]
    public partial class AddCustomerContactKeys : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Últimos 10 dígitos (quita el 1 de país de EE.UU./RD y todo el formato). Menos de 10
            // dígitos se deja tal cual. Se usa [^0-9] en vez de \D para que coincida exactamente con
            // ContactKey.Phone en C#, sin depender del locale de la base.
            migrationBuilder.Sql(@"
ALTER TABLE ""Customers"" ADD COLUMN IF NOT EXISTS ""PhoneKey"" text
GENERATED ALWAYS AS (
    CASE WHEN length(regexp_replace(COALESCE(""Phone"", ''), '[^0-9]', '', 'g')) >= 10
         THEN right(regexp_replace(COALESCE(""Phone"", ''), '[^0-9]', '', 'g'), 10)
         ELSE NULLIF(regexp_replace(COALESCE(""Phone"", ''), '[^0-9]', '', 'g'), '')
    END
) STORED;");

            migrationBuilder.Sql(@"
ALTER TABLE ""Customers"" ADD COLUMN IF NOT EXISTS ""EmailKey"" text
GENERATED ALWAYS AS (NULLIF(lower(btrim(COALESCE(""Email"", ''))), '')) STORED;");

            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Customers_AffiliateId_PhoneKey"" ON ""Customers"" (""AffiliateId"", ""PhoneKey"");");
            migrationBuilder.Sql(@"CREATE INDEX IF NOT EXISTS ""IX_Customers_AffiliateId_EmailKey"" ON ""Customers"" (""AffiliateId"", ""EmailKey"");");

            // Pedidos viejos sin enlace: primero por teléfono (la llave que manda) y después por
            // correo los que sigan en null, para que el resultado no dependa del orden de las filas.
            migrationBuilder.Sql(@"
UPDATE ""Orders"" o
SET ""CustomerId"" = c.""Id""
FROM ""Customers"" c
WHERE o.""CustomerId"" IS NULL
  AND c.""AffiliateId"" = o.""AffiliateId""
  AND c.""PhoneKey"" IS NOT NULL
  AND c.""PhoneKey"" = CASE WHEN length(regexp_replace(COALESCE(o.""CustomerPhone"", ''), '[^0-9]', '', 'g')) >= 10
                            THEN right(regexp_replace(COALESCE(o.""CustomerPhone"", ''), '[^0-9]', '', 'g'), 10)
                            ELSE NULLIF(regexp_replace(COALESCE(o.""CustomerPhone"", ''), '[^0-9]', '', 'g'), '')
                       END;");

            migrationBuilder.Sql(@"
UPDATE ""Orders"" o
SET ""CustomerId"" = c.""Id""
FROM ""Customers"" c
WHERE o.""CustomerId"" IS NULL
  AND c.""AffiliateId"" = o.""AffiliateId""
  AND c.""EmailKey"" IS NOT NULL
  AND c.""EmailKey"" = NULLIF(lower(btrim(COALESCE(o.""CustomerEmail"", ''))), '');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // El relleno de Orders."CustomerId" no se deshace: no hay forma de saber cuáles estaban
            // en null antes, y tener el enlace correcto nunca estorba.
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Customers_AffiliateId_EmailKey"";");
            migrationBuilder.Sql(@"DROP INDEX IF EXISTS ""IX_Customers_AffiliateId_PhoneKey"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Customers"" DROP COLUMN IF EXISTS ""EmailKey"";");
            migrationBuilder.Sql(@"ALTER TABLE ""Customers"" DROP COLUMN IF EXISTS ""PhoneKey"";");
        }
    }
}
