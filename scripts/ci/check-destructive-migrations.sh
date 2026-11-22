#!/usr/bin/env bash
# Guardia de migraciones destructivas.
#
# Producción migra sola al arrancar (db.Database.Migrate() en Program.cs), así que una migración
# que borra o renombra algo se ejecuta contra datos reales en cuanto Railway despliega main.
# Este script revisa SOLO las migraciones agregadas o cambiadas en el push, y solo su método Up():
# si encuentra DropTable, DropColumn, RenameTable, RenameColumn o SQL crudo con DROP/TRUNCATE/DELETE,
# falla el CI, salvo que el mensaje del commit lleve la marca [destructiva-ok].
#
# La regla de fondo es expandir y contraer: primero se agrega lo nuevo y se deja de usar lo viejo,
# y el borrado va en un despliegue aparte, a conciencia y con respaldo.
#
# Uso: check-destructive-migrations.sh <sha-base> <sha-nuevo>   (COMMIT_MSG en el entorno)
set -euo pipefail

BASE="${1:-}"
HEAD="${2:-HEAD}"
MIGRATIONS_DIR="src/Maalca.Infrastructure/Migrations"

# Push nuevo de rama (base = ceros) o sin base: compara contra el commit anterior.
if [[ -z "$BASE" || "$BASE" =~ ^0+$ ]] || ! git cat-file -e "$BASE^{commit}" 2>/dev/null; then
  BASE="$(git rev-parse "$HEAD~1" 2>/dev/null || true)"
fi
if [[ -z "$BASE" ]]; then
  echo "Sin commit base para comparar; se omite la guardia."
  exit 0
fi

mapfile -t FILES < <(git diff --name-only --diff-filter=AM "$BASE" "$HEAD" -- "$MIGRATIONS_DIR" \
  | grep -E '\.cs$' | grep -vE '\.Designer\.cs$|ModelSnapshot\.cs$' || true)

if [[ ${#FILES[@]} -eq 0 ]]; then
  echo "Ninguna migración nueva en este push."
  exit 0
fi

# SQL crudo: las migraciones escritas a mano meten el SQL en varias líneas, así que se busca línea por línea
# dentro de Up(), no solo junto a Sql(.
PATTERN='\.(DropTable|DropColumn|RenameTable|RenameColumn)\(|DROP[[:space:]]+(TABLE|COLUMN)|TRUNCATE|DELETE[[:space:]]+FROM|RENAME[[:space:]]+(TO|COLUMN)'
FOUND=0
for f in "${FILES[@]}"; do
  [[ -f "$f" ]] || continue
  # Solo el cuerpo de Up(): Down() deshace la migración y es normal que tenga Drop.
  UP_BODY="$(awk '/override void Up\(/{flag=1} /override void Down\(/{flag=0} flag' "$f")"
  HITS="$(grep -nEi "$PATTERN" <<<"$UP_BODY" || true)"
  if [[ -n "$HITS" ]]; then
    FOUND=1
    echo "::error file=$f::Migración destructiva en Up():"
    echo "$HITS"
  else
    echo "OK: $f"
  fi
done

if [[ $FOUND -eq 1 ]]; then
  if grep -q '\[destructiva-ok\]' <<<"${COMMIT_MSG:-}"; then
    echo "Hay operaciones destructivas, pero el commit lleva [destructiva-ok]: se permite."
    exit 0
  fi
  echo "El push trae una migración destructiva. Divídela (expandir y contraer) o, si es a conciencia"
  echo "y con respaldo de la base, agrega [destructiva-ok] al mensaje del commit."
  exit 1
fi
echo "Sin migraciones destructivas."
