using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Espacio de autor (BusinessType.Creator): obras y capítulos. Solo AGREGA tablas (expandir).
    /// Escrita a mano e idempotente (IF NOT EXISTS), mismo patrón que AddSocialPublishing.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261126220000_AddCreatorWorks")]
    public partial class AddCreatorWorks : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""Works"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""Title"" character varying(200) NOT NULL,
    ""TitleEn"" character varying(200) NULL,
    ""Subtitle"" character varying(200) NULL,
    ""Slug"" character varying(120) NOT NULL,
    ""Kind"" character varying(20) NOT NULL,
    ""Status"" character varying(20) NOT NULL,
    ""Synopsis"" text NULL,
    ""SynopsisEn"" text NULL,
    ""CoverUrl"" character varying(500) NULL,
    ""Year"" integer NULL,
    ""Isbn"" character varying(20) NULL,
    ""Imprint"" character varying(120) NULL,
    ""ContentNotice"" character varying(300) NULL,
    ""BuyLinks"" text NULL,
    ""ReadingAccess"" character varying(20) NOT NULL,
    ""FreeFrom"" timestamp with time zone NULL,
    ""FreeUntil"" timestamp with time zone NULL,
    ""Featured"" boolean NOT NULL DEFAULT FALSE,
    ""SortOrder"" integer NOT NULL DEFAULT 0,
    ""IsDemo"" boolean NOT NULL DEFAULT FALSE,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    CONSTRAINT ""PK_Works"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_Works_Affiliates_AffiliateId"" FOREIGN KEY (""AffiliateId"")
        REFERENCES ""Affiliates"" (""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_Works_AffiliateId_Slug"" ON ""Works"" (""AffiliateId"", ""Slug"");
CREATE INDEX IF NOT EXISTS ""IX_Works_AffiliateId_Status_SortOrder"" ON ""Works"" (""AffiliateId"", ""Status"", ""SortOrder"");

CREATE TABLE IF NOT EXISTS ""WorkChapters"" (
    ""Id"" uuid NOT NULL,
    ""WorkId"" uuid NOT NULL,
    ""Number"" integer NOT NULL,
    ""Title"" character varying(200) NOT NULL,
    ""ContentHtml"" text NULL,
    ""IsSample"" boolean NOT NULL DEFAULT FALSE,
    ""WordCount"" integer NOT NULL DEFAULT 0,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_WorkChapters"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_WorkChapters_Works_WorkId"" FOREIGN KEY (""WorkId"")
        REFERENCES ""Works"" (""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_WorkChapters_WorkId_Number"" ON ""WorkChapters"" (""WorkId"", ""Number"");
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS ""WorkChapters"";
DROP TABLE IF EXISTS ""Works"";
");
        }
    }
}
