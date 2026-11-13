-- Registro corto de cliente con datos de entrega (migración 20261108190131_AddStoreCustomerDeliveryInfo). Idempotente.
-- Requiere que 20261108180702_AddRouteTracking ya esté aplicada.
START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261108190131_AddStoreCustomerDeliveryInfo') THEN
    ALTER TABLE "StoreCustomers" ADD "Address" character varying(250);
    ALTER TABLE "StoreCustomers" ADD "DeliveryFrequency" character varying(20);
    ALTER TABLE "StoreCustomers" ADD "GpsLat" numeric(9,6);
    ALTER TABLE "StoreCustomers" ADD "GpsLng" numeric(9,6);
    ALTER TABLE "StoreCustomers" ADD "WantsCredit" boolean NOT NULL DEFAULT FALSE;

    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261108190131_AddStoreCustomerDeliveryInfo', '8.0.0');
    END IF;
END $EF$;

COMMIT;
