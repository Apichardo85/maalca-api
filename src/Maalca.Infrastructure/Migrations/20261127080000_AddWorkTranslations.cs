using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Traducción de obras: ajustes por obra e idioma (guía, glosario, crédito) y la traducción de cada capítulo,
    /// que también hace de cola del worker. Solo AGREGA tablas (expandir). Escrita a mano e idempotente.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261127080000_AddWorkTranslations")]
    public partial class AddWorkTranslations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""WorkTranslationSettings"" (
    ""Id"" uuid NOT NULL,
    ""WorkId"" uuid NOT NULL,
    ""Language"" character varying(5) NOT NULL DEFAULT 'en',
    ""Brief"" text NULL,
    ""Glossary"" text NULL,
    ""Credit"" character varying(200) NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    CONSTRAINT ""PK_WorkTranslationSettings"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_WorkTranslationSettings_Works_WorkId"" FOREIGN KEY (""WorkId"") REFERENCES ""Works"" (""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_WorkTranslationSettings_WorkId_Language"" ON ""WorkTranslationSettings"" (""WorkId"", ""Language"");

CREATE TABLE IF NOT EXISTS ""WorkChapterTranslations"" (
    ""Id"" uuid NOT NULL,
    ""WorkId"" uuid NOT NULL,
    ""ChapterId"" uuid NOT NULL,
    ""Language"" character varying(5) NOT NULL DEFAULT 'en',
    ""Title"" character varying(200) NULL,
    ""ContentHtml"" text NULL,
    ""WordCount"" integer NOT NULL DEFAULT 0,
    ""Status"" character varying(20) NOT NULL DEFAULT 'queued',
    ""SourceHash"" character varying(64) NULL,
    ""Error"" text NULL,
    ""Attempts"" integer NOT NULL DEFAULT 0,
    ""ModelId"" character varying(100) NULL,
    ""TokensInput"" integer NOT NULL DEFAULT 0,
    ""TokensOutput"" integer NOT NULL DEFAULT 0,
    ""CostUsd"" numeric(10,4) NOT NULL DEFAULT 0,
    ""GeneratedAt"" timestamp with time zone NULL,
    ""ReviewedAt"" timestamp with time zone NULL,
    ""PublishedAt"" timestamp with time zone NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    CONSTRAINT ""PK_WorkChapterTranslations"" PRIMARY KEY (""Id""),
    CONSTRAINT ""FK_WorkChapterTranslations_Works_WorkId"" FOREIGN KEY (""WorkId"") REFERENCES ""Works"" (""Id"") ON DELETE CASCADE,
    CONSTRAINT ""FK_WorkChapterTranslations_WorkChapters_ChapterId"" FOREIGN KEY (""ChapterId"") REFERENCES ""WorkChapters"" (""Id"") ON DELETE CASCADE
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_WorkChapterTranslations_ChapterId_Language"" ON ""WorkChapterTranslations"" (""ChapterId"", ""Language"");
CREATE INDEX IF NOT EXISTS ""IX_WorkChapterTranslations_WorkId_Language"" ON ""WorkChapterTranslations"" (""WorkId"", ""Language"");
CREATE INDEX IF NOT EXISTS ""IX_WorkChapterTranslations_Status"" ON ""WorkChapterTranslations"" (""Status"");
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS ""WorkChapterTranslations"";
DROP TABLE IF EXISTS ""WorkTranslationSettings"";
");
        }
    }
}
