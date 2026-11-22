# CLAUDE.md — maalca-api

.NET 10, Clean Architecture (`Maalca.Domain` → `Maalca.Application` → `Maalca.Infrastructure` → `Maalca.Api`),
PostgreSQL en Railway. Supabase solo autentica; todos los datos de dominio viven aquí.

## Despliegue (no cambiar sin el dueño)

- `develop` → Railway **staging** (`maalca-api-staging.up.railway.app`, base propia).
- `main` → Railway **producción** (`maalca-api-production.up.railway.app`).
- Orden de despliegue de una función: SQL → API → web. Commits de maalca-api y maalca-web separados.
- Claude empuja solo a `develop`. A `main` solo con orden explícita del dueño, y el mismo commit que ya pasó por staging.

## Migraciones: expandir y contraer

El API ejecuta `db.Database.Migrate()` al arrancar: cada migración corre contra producción en cuanto se despliega `main`.

- Una migración solo **agrega** (tablas, columnas que admiten null o tienen default, índices).
- Borrar o renombrar va en un despliegue aparte, cuando ningún código usa ya lo viejo, y con respaldo de la base.
- El CI (`scripts/ci/check-destructive-migrations.sh`) frena `DropTable`, `DropColumn`, `RenameTable`, `RenameColumn` y SQL con DROP/TRUNCATE/DELETE en `Up()`. Para pasarlo a conciencia, el mensaje del commit lleva `[destructiva-ok]`.

## CI (`.github/workflows/build.yml`)

1. Compila la solución.
2. Guardia de migraciones destructivas.
3. Arranque en frío: levanta el API contra un Postgres 18 vacío y espera `/health`. Si una migración no aplica, el CI queda rojo.
