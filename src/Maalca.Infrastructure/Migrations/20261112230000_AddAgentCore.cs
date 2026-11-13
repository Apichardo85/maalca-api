using Maalca.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Maalca.Infrastructure.Migrations
{
    /// <summary>
    /// Núcleo del orquestador de agentes (fase 1): fichas de agente, proveedores, registro de modelos,
    /// cola/bitácora de trabajos y pausa. Siembra el proveedor Anthropic, los tres niveles de modelo y
    /// el asistente de /ops; todo eso se edita después desde /ops, sin desplegar.
    /// Escrita a mano, mismo patrón que AddIntegrationConnections. No toca AgentExecutions (tabla antigua).
    /// </summary>
    [DbContext(typeof(AppDbContext))]
    [Migration("20261112230000_AddAgentCore")]
    public partial class AddAgentCore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
CREATE TABLE IF NOT EXISTS ""AgentDefinitions"" (
    ""Id"" uuid NOT NULL,
    ""Key"" character varying(60) NOT NULL,
    ""Name"" character varying(120) NOT NULL,
    ""Description"" text NULL,
    ""SystemPrompt"" text NOT NULL,
    ""ModelTier"" character varying(60) NOT NULL,
    ""Tools"" text NOT NULL,
    ""Visibility"" character varying(20) NOT NULL,
    ""MaxOutputTokens"" integer NOT NULL,
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    CONSTRAINT ""PK_AgentDefinitions"" PRIMARY KEY (""Id"")
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_AgentDefinitions_Key"" ON ""AgentDefinitions"" (""Key"");

CREATE TABLE IF NOT EXISTS ""AgentProviders"" (
    ""Id"" uuid NOT NULL,
    ""Key"" character varying(60) NOT NULL,
    ""Kind"" character varying(30) NOT NULL,
    ""BaseUrl"" character varying(300) NOT NULL,
    ""ApiKeyEnc"" text NULL,
    ""ApiKeyLast4"" character varying(8) NULL,
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    CONSTRAINT ""PK_AgentProviders"" PRIMARY KEY (""Id"")
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_AgentProviders_Key"" ON ""AgentProviders"" (""Key"");

CREATE TABLE IF NOT EXISTS ""AgentModels"" (
    ""Id"" uuid NOT NULL,
    ""Tier"" character varying(60) NOT NULL,
    ""ProviderKey"" character varying(60) NOT NULL,
    ""ModelId"" character varying(120) NOT NULL,
    ""InputPricePerMTok"" numeric(12,4) NOT NULL,
    ""OutputPricePerMTok"" numeric(12,4) NOT NULL,
    ""IsActive"" boolean NOT NULL DEFAULT TRUE,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    ""CreatedBy"" text NULL,
    ""UpdatedBy"" text NULL,
    CONSTRAINT ""PK_AgentModels"" PRIMARY KEY (""Id"")
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_AgentModels_Tier"" ON ""AgentModels"" (""Tier"");

CREATE TABLE IF NOT EXISTS ""AgentJobs"" (
    ""Id"" uuid NOT NULL,
    ""AgentKey"" character varying(60) NOT NULL,
    ""AffiliateId"" uuid NULL,
    ""Kind"" character varying(20) NOT NULL,
    ""InputJson"" text NOT NULL,
    ""Status"" character varying(20) NOT NULL,
    ""Attempts"" integer NOT NULL DEFAULT 0,
    ""MaxAttempts"" integer NOT NULL DEFAULT 3,
    ""RunAfter"" timestamp with time zone NOT NULL,
    ""LockedAt"" timestamp with time zone NULL,
    ""StartedAt"" timestamp with time zone NULL,
    ""FinishedAt"" timestamp with time zone NULL,
    ""ResultText"" text NULL,
    ""ErrorCode"" character varying(40) NULL,
    ""Error"" text NULL,
    ""ModelId"" character varying(120) NULL,
    ""TokensInput"" integer NOT NULL DEFAULT 0,
    ""TokensOutput"" integer NOT NULL DEFAULT 0,
    ""CostUsd"" numeric(14,6) NOT NULL DEFAULT 0,
    ""DurationMs"" bigint NOT NULL DEFAULT 0,
    ""ToolCalls"" integer NOT NULL DEFAULT 0,
    ""RequestedBy"" character varying(255) NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_AgentJobs"" PRIMARY KEY (""Id"")
);
CREATE INDEX IF NOT EXISTS ""IX_AgentJobs_Status_RunAfter"" ON ""AgentJobs"" (""Status"", ""RunAfter"");
CREATE INDEX IF NOT EXISTS ""IX_AgentJobs_CreatedAt"" ON ""AgentJobs"" (""CreatedAt"");

CREATE TABLE IF NOT EXISTS ""AgentControls"" (
    ""Id"" uuid NOT NULL,
    ""Scope"" character varying(60) NOT NULL,
    ""Paused"" boolean NOT NULL DEFAULT FALSE,
    ""Reason"" text NULL,
    ""UpdatedBy"" character varying(255) NULL,
    ""CreatedAt"" timestamp with time zone NOT NULL,
    ""UpdatedAt"" timestamp with time zone NULL,
    CONSTRAINT ""PK_AgentControls"" PRIMARY KEY (""Id"")
);
CREATE UNIQUE INDEX IF NOT EXISTS ""IX_AgentControls_Scope"" ON ""AgentControls"" (""Scope"");

INSERT INTO ""AgentProviders"" (""Id"", ""Key"", ""Kind"", ""BaseUrl"", ""IsActive"", ""CreatedAt"", ""CreatedBy"")
VALUES (gen_random_uuid(), 'anthropic', 'anthropic', 'https://api.anthropic.com', TRUE, now(), 'seed')
ON CONFLICT (""Key"") DO NOTHING;

-- Precios en USD por millón de tokens, tomados de la página de precios de Anthropic el 12 nov 2026.
INSERT INTO ""AgentModels"" (""Id"", ""Tier"", ""ProviderKey"", ""ModelId"", ""InputPricePerMTok"", ""OutputPricePerMTok"", ""IsActive"", ""CreatedAt"", ""CreatedBy"")
VALUES
    (gen_random_uuid(), 'fast', 'anthropic', 'claude-haiku-5-5', 0.10, 0.50, TRUE, now(), 'seed'),
    (gen_random_uuid(), 'standard', 'anthropic', 'claude-sonnet-5-5', 2.00, 10.00, TRUE, now(), 'seed'),
    (gen_random_uuid(), 'premium', 'anthropic', 'claude-opus-5-5', 4.00, 20.00, TRUE, now(), 'seed')
ON CONFLICT (""Tier"") DO NOTHING;

INSERT INTO ""AgentDefinitions"" (""Id"", ""Key"", ""Name"", ""Description"", ""SystemPrompt"", ""ModelTier"", ""Tools"", ""Visibility"", ""MaxOutputTokens"", ""IsActive"", ""CreatedAt"", ""CreatedBy"")
VALUES (
    gen_random_uuid(),
    'ops-assistant',
    'Asistente de operaciones',
    'Asistente interno de /ops: estado de la plataforma y de los negocios.',
    'Eres el asistente interno del panel de operaciones de MaalCa, una plataforma que da a pequeños negocios su página web, catálogo, pedidos y pagos.

Hablas en español, con tuteo (tú, tienes), en tono directo y práctico, como un analista que ya conoce el negocio. No inventes datos: antes de responder con cifras o nombres, consulta las herramientas para ver el estado actual de la plataforma y de los negocios. Si algo no aparece en lo que devuelven, dilo.

Cuando te pregunten por riesgo de churn, prioriza negocios en plan Growth con alertas activas (sin conectar pagos, sin pedidos en 30 días) y poca actividad reciente. Cuando te pidan redactar algo (un email, un mensaje), hazlo directamente, listo para copiar.',
    'standard',
    'ops_overview,ops_affiliates',
    'internal',
    1024,
    TRUE,
    now(),
    'seed')
ON CONFLICT (""Key"") DO NOTHING;
");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
DROP TABLE IF EXISTS ""AgentControls"";
DROP TABLE IF EXISTS ""AgentJobs"";
DROP TABLE IF EXISTS ""AgentModels"";
DROP TABLE IF EXISTS ""AgentProviders"";
DROP TABLE IF EXISTS ""AgentDefinitions"";
");
        }
    }
}
