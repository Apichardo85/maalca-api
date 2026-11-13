-- Permite varias invitaciones pendientes (SupabaseUserId = '') por negocio.
-- Migración 20261108210400_FilterPendingInviteIndex. Idempotente.
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108210400_FilterPendingInviteIndex') THEN
    DROP INDEX IF EXISTS "IX_UserAffiliateMaps_SupabaseUserId_AffiliateId";

    CREATE UNIQUE INDEX "IX_UserAffiliateMaps_SupabaseUserId_AffiliateId" ON "UserAffiliateMaps" ("SupabaseUserId", "AffiliateId") WHERE "SupabaseUserId" <> '';

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108210400_FilterPendingInviteIndex', '8.0.0');
    END IF;
END $EF$;

COMMIT;
