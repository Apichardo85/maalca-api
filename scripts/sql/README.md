# Scripts SQL

Scripts que se corren a mano en DataGrip contra Railway. Las migraciones de EF (carpeta `Migrations`) son la fuente de verdad del esquema; `staging-*.sql`, `migracion-*.sql` y `prod-*.sql` son el script idempotente que sale de `dotnet ef migrations script ... --idempotent` (ya aplicados, se guardan por historial).

- `seed-*.sql`, `limpieza-*.sql`, `cargar-*.sql`: datos. Terminan en `ROLLBACK` como ensayo; para aplicar de verdad cambia la última línea a `COMMIT` y corre el script completo. Después de `COMMIT` no se deshace.
- Nuevos scripts: guárdalos aquí, no en la raíz del repo.
