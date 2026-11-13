-- Restaura los 4 vehiculos demo (IsDemo) de Jetzel Autos, con precios en DOP (tasa de referencia 60).
-- Idempotente: solo inserta si el negocio no tiene ningun vehiculo.
START TRANSACTION;

INSERT INTO "VehicleListings"
("Id","AffiliateId","Make","Model","Trim","Year","Condition","Price","Mileage","MileageUnit","Transmission","FuelType","BodyType","Drivetrain","ImportOrigin","Status","Featured","IsPubliclyVisible","SortOrder","IsDemo","Description","CreatedAt")
SELECT gen_random_uuid(), a."Id", v.make, v.model, v.trim, v.yr, 'used', v.price, v.km, 'km', 'automatic', v.fuel, v.body, v.drive, v.origin, 'available', v.featured, true, v.ord, true,
       'Vehículo de ejemplo. Reemplázalo con tu inventario real desde el panel.', now()
FROM "Affiliates" a
CROSS JOIN (VALUES
  ('Toyota','RAV4','XLE',2023,1350000::numeric,48000,'suv','gasoline','awd','usa',true,0),
  ('Honda','Civic','EX',2022,894000::numeric,62000,'sedan','gasoline','fwd','usa',false,1),
  ('Hyundai','Tucson','SEL',2024,1188000::numeric,31000,'crossover','gasoline','fwd','korea',false,2),
  ('Toyota','Hilux','SR5',2021,1590000::numeric,90000,'pickup','diesel','4x4','japan',false,3)
) AS v(make,model,trim,yr,price,km,body,fuel,drive,origin,featured,ord)
WHERE a."Slug" = 'jetzel-autos'
  AND NOT EXISTS (SELECT 1 FROM "VehicleListings" x WHERE x."AffiliateId" = a."Id");

-- Debe devolver 4
SELECT count(*) FROM "VehicleListings" l JOIN "Affiliates" a ON a."Id"=l."AffiliateId" WHERE a."Slug"='jetzel-autos';

COMMIT;
