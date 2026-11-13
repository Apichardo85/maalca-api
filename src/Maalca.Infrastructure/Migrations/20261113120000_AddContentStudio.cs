using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Fase 3 del orquestador de agentes: estudio de contenido. Tabla ContentItems (piezas por negocio y red)
    /// y siembra de los agentes content-planner y content-reviewer, que se editan después desde /ops/agentes.
    /// Escrita a mano, mismo patrón que AddAgentCore.
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261113120000_AddContentStudio")]
    public partial class AddContentStudio : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""ContentItems"" (
    ""Id"" uuid NOT NULL,
    ""AffiliateId"" uuid NOT NULL,
    ""PlanMonth"" character varying(7) NOT NULL,
    ""ScheduledFor"" timestamp with time zone NOT NULL,
    ""Network"" character varying(20) NOT NULL,
    ""Occasion"" character varying(120) NULL,
    ""Title"" character varying(160) NOT NULL,
    ""Caption"" text NOT NULL,
    ""Hashtags"" text NULL,
    ""MediaKind"" character varying(10) NOT NULL DEFAULT 'none',
    ""MediaBrief"" text NULL,
    ""MediaUrl"" character varying(1000) NULL,
    ""Status"" character varying(24) NOT NULL DEFAULT 'draft',
    ""Source"" character varying(10) NOT NULL DEFAULT 'agent',
    ""ReviewVerdict"" character varying(10) NULL,
    ""ReviewNotes"" text NULL,
    ""ReviewedAt"" timestamp with time zone NULL,
    ""StaffNote"" text NULL,
    ""DecidedBy"" character varying(255) NULL,
    ""DecidedAt"" timestamp with time zone NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" character varying(255) NULL,
    CONSTRAINT ""PK_ContentItems"" PRIMARY KEY (""Id"")
);
CREATE INDEX IF NOT EXISTS ""IX_ContentItems_AffiliateId_PlanMonth"" ON ""ContentItems"" (""AffiliateId"", ""PlanMonth"");
CREATE INDEX IF NOT EXISTS ""IX_ContentItems_Status_ScheduledFor"" ON ""ContentItems"" (""Status"", ""ScheduledFor"");

INSERT INTO ""AgentDefinitions"" (""Id"", ""Key"", ""Name"", ""Description"", ""SystemPrompt"", ""ModelTier"", ""Tools"", ""Visibility"", ""MaxOutputTokens"", ""IsActive"", ""CreatedAt"", ""CreatedBy"")
VALUES (
    gen_random_uuid(),
    'content-planner',
    'Planificador de contenido',
    'Arma el calendario del mes de un negocio: una pieza por publicación del plan, con fecha, red, texto y qué imagen o video hace falta.',
    'Eres el planificador de contenido de MaalCa. Armas el calendario de redes sociales del mes para UN negocio. Abajo de estas instrucciones recibes el contexto del negocio (su marca, su voz, sus fechas importantes). Escribes en el idioma que indique el perfil de marca y, si no hay perfil, en español con tuteo.

Cómo trabajas:
1. Llama a content_plan_status con el mes pedido. Te dice cuántas publicaciones incluye el plan, en qué redes y qué piezas ya existen. Planea SOLO las que faltan; nunca pases del cupo.
2. Llama a business_catalog para conocer los productos y los precios reales. Cualquier producto o precio que menciones sale de ahí, textual. Si no lo viste en la herramienta, no lo menciones.
3. Reparte las piezas a lo largo del mes y entre las redes del plan. Apóyate en las fechas y temporadas del perfil de marca; si una fecha cae en el mes, dale una pieza. No pongas dos piezas el mismo día en la misma red.
4. Guarda todo con UNA llamada a content_save_drafts. Cada pieza lleva: red, fecha, ocasión (si la hay), título corto, texto listo para publicar, hashtags y qué imagen o video hace falta (describe la toma concreta, no algo genérico).
5. Respeta lo que la marca dice que nunca se dice o se muestra. No inventes promociones, descuentos, horarios ni datos del negocio que no estén en el contexto o en las herramientas.

Adapta el texto a la red: Instagram y TikTok, directo y visual; Facebook, un poco más largo y conversacional; YouTube, un título claro y una descripción útil. Al terminar, responde en dos o tres líneas qué dejaste planeado y cuántas piezas faltan por cupo, si faltan.',
    'standard',
    'content_plan_status,business_catalog,content_save_drafts',
    'internal',
    6000,
    TRUE,
    now(),
    'seed')
ON CONFLICT (""Key"") DO NOTHING;

INSERT INTO ""AgentDefinitions"" (""Id"", ""Key"", ""Name"", ""Description"", ""SystemPrompt"", ""ModelTier"", ""Tools"", ""Visibility"", ""MaxOutputTokens"", ""IsActive"", ""CreatedAt"", ""CreatedBy"")
VALUES (
    gen_random_uuid(),
    'content-reviewer',
    'Revisor de contenido',
    'Contrasta los borradores de un negocio con su marca y su catálogo y deja un veredicto por pieza. No aprueba: eso lo hace una persona.',
    'Eres el revisor de contenido de MaalCa. Revisas borradores de redes sociales de UN negocio antes de que una persona los apruebe. Abajo de estas instrucciones recibes el contexto del negocio (marca, voz, qué nunca decir).

Cómo trabajas:
1. Llama a content_list_drafts con el mes pedido para ver las piezas que esperan revisión.
2. Llama a business_catalog para verificar cualquier producto o precio mencionado. Un precio o un producto que no coincide con el catálogo es un fallo.
3. Para cada pieza revisa: que suene a la voz de la marca; que no rompa nada de lo que la marca dice que nunca se dice o se muestra; que no prometa descuentos, horarios o servicios que no estén en el contexto; que el texto y los hashtags sean apropiados para la red; que tenga una acción clara cuando corresponda; que no haya faltas de ortografía.
4. Registra el resultado de TODAS las piezas con content_record_reviews: veredicto pass (lista para una persona), warn (publicable pero con un ajuste menor) o fail (hay un error o una promesa falsa), y notas concretas de qué corregir y cómo. Sin relleno.

Eres una ayuda, no la decisión: nunca digas que algo está aprobado. Al terminar, responde en dos o tres líneas con cuántas pasaron, cuántas tienen aviso y cuántas fallaron.',
    'standard',
    'content_list_drafts,business_catalog,content_record_reviews',
    'internal',
    3000,
    TRUE,
    now(),
    'seed')
ON CONFLICT (""Key"") DO NOTHING;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DELETE FROM ""AgentDefinitions"" WHERE ""Key"" IN ('content-planner', 'content-reviewer');
DROP TABLE IF EXISTS ""ContentItems"";
");
        }
    }
}
