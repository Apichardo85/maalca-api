-- Perfiles de acceso del equipo (migración 20261108202239_AddAccessProfile). Idempotente.
-- Requiere que 20261108190131_AddStoreCustomerDeliveryInfo ya esté aplicada.
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108202239_AddAccessProfile') THEN
    ALTER TABLE "UserAffiliateMaps" ADD "Profile" character varying(24);

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108202239_AddAccessProfile', '8.0.0');
    END IF;
END $EF$;

COMMIT;
