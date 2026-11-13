-- Aceptación de contrato en línea desde el portal del cliente (migración 20261108204734_AddContractAcceptance). Idempotente.
-- Requiere que 20261108202239_AddAccessProfile ya esté aplicada.
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108204734_AddContractAcceptance') THEN
    ALTER TABLE "Contracts" ADD "AcceptedAt" timestamp with time zone;
    ALTER TABLE "Contracts" ADD "AcceptedByEmail" character varying(256);
    ALTER TABLE "Contracts" ADD "AcceptedByName" character varying(160);
    ALTER TABLE "Contracts" ADD "AcceptedByStoreCustomerId" uuid;
    ALTER TABLE "Contracts" ADD "AcceptedDocumentHash" character varying(64);

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108204734_AddContractAcceptance', '8.0.0');
    END IF;
END $EF$;

COMMIT;
