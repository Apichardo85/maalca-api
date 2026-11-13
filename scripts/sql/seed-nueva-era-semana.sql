-- Nueva Era (slug f-brica-de-agua-y-hielo-nueva-era): LIMPIA los datos operativos de prueba y SIEMBRA una operacion
-- realista de 1 semana segun el levantamiento (500 clientes por contrato, 4 camiones/rutas, entregas semanales/diarias/
-- por pedido, contado vs credito 15-30 dias, cobro quincenal/mensual, envases retornables, gastos fijos).
-- Todo lo sembrado lleva la marca 'seed-prueba-semana' en Notes. Clientes SIN correo a proposito (que no salgan
-- recordatorios de facturas a direcciones inventadas).
-- BORRA primero lo mismo que limpieza-nueva-era.sql (no toca negocio, usuarios, equipo, productos, tipos de envase,
-- plantilla de contrato, ajustes de cobro ni cuentas del portal).
-- ENSAYO: termina en ROLLBACK y muestra conteos y totales. Para aplicar de verdad cambia la ultima linea a COMMIT y
-- ejecuta de nuevo el script completo (Alt+Shift+Enter). Despues de COMMIT no se deshace.
START TRANSACTION;

CREATE TEMP TABLE _a ON COMMIT DROP AS
  SELECT "Id" FROM "Affiliates" WHERE "Slug" = 'f-brica-de-agua-y-hielo-nueva-era';
DO $$ BEGIN
  IF (SELECT count(*) FROM _a) <> 1 THEN RAISE EXCEPTION 'No se encontro exactamente 1 negocio con ese slug'; END IF;
END $$;

CREATE TEMP TABLE _log (tabla text, filas int) ON COMMIT DROP;

-- ===================== PARTE 1: limpieza =====================
WITH d AS (DELETE FROM "DeliveryItems" WHERE "DeliveryId" IN (SELECT "Id" FROM "Deliveries" WHERE "AffiliateId" IN (SELECT "Id" FROM _a)) RETURNING 1)
  INSERT INTO _log SELECT 'borrado DeliveryItems', count(*) FROM d;
WITH d AS (DELETE FROM "ContainerMovements" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'borrado ContainerMovements', count(*) FROM d;
WITH d AS (DELETE FROM "Deliveries" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'borrado Deliveries', count(*) FROM d;
WITH d AS (DELETE FROM "VehiclePositions" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'borrado VehiclePositions', count(*) FROM d;
UPDATE "Customers" SET "RouteId" = NULL WHERE "AffiliateId" IN (SELECT "Id" FROM _a) AND "RouteId" IS NOT NULL;
WITH d AS (DELETE FROM "RouteStops" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'borrado RouteStops', count(*) FROM d;
WITH d AS (DELETE FROM "DeliveryRoutes" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'borrado DeliveryRoutes', count(*) FROM d;
WITH d AS (DELETE FROM "Anaqueles" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'borrado Anaqueles', count(*) FROM d;
WITH d AS (DELETE FROM "Payments" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'borrado Payments', count(*) FROM d;
WITH d AS (DELETE FROM "Contracts" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'borrado Contracts', count(*) FROM d;
WITH d AS (DELETE FROM "Orders" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'borrado Orders', count(*) FROM d;
WITH d AS (DELETE FROM "InvoiceItems" WHERE "InvoiceId" IN (SELECT "Id" FROM "Invoices" WHERE "AffiliateId" IN (SELECT "Id" FROM _a)) RETURNING 1)
  INSERT INTO _log SELECT 'borrado InvoiceItems', count(*) FROM d;
WITH d AS (DELETE FROM "Invoices" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'borrado Invoices', count(*) FROM d;
WITH d AS (DELETE FROM "Vehicles" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'borrado Vehicles', count(*) FROM d;
WITH d AS (DELETE FROM "Customers" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'borrado Customers', count(*) FROM d;
WITH d AS (DELETE FROM "Expenses" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) RETURNING 1)
  INSERT INTO _log SELECT 'borrado Expenses', count(*) FROM d;

-- ===================== PARTE 2: siembra de 1 semana =====================
DO $SEED$
DECLARE
  v_aff uuid;
  v_n   int  := 500;                                   -- clientes por contrato (levantamiento)
  v_tz  text := 'America/Santo_Domingo';
  v_today date := (now() AT TIME ZONE 'America/Santo_Domingo')::date;
  v_ct uuid;
  v_bot uuid; v_far uuid; v_btl uuid;
  p_bot numeric; p_far numeric; p_btl numeric;
  v_nom  text[] := ARRAY['María','José','Juan','Ana','Luis','Carmen','Pedro','Rosa','Miguel','Altagracia','Rafael','Yesenia','Francisco','Mercedes','Manuel','Josefina','Carlos','Milagros','Ramón','Dilcia'];
  v_ape  text[] := ARRAY['Pérez','Rodríguez','Martínez','Díaz','Santos','Reyes','Ramírez','Cabrera','Mejía','Féliz','De los Santos','Matos','Encarnación','Peña','Ortiz','Medina','Castillo','Heredia','Suero','Montero'];
  v_tipo text[] := ARRAY['Colmado','Farmacia','Ferretería','Escuela','Iglesia','Oficina','Salón','Taller','Comedor','Clínica','Pensión','Supermercado'];
  v_barrio text[] := ARRAY['Centro','Los Jardines','Las Flores','El Dique','Villa Esperanza','Pueblo Nuevo','La Cumbre','Los Mina','San Antonio','Cristo Rey'];
  v_calle text[] := ARRAY['Calle Duarte','Calle Sánchez','Av. Padre Ayala','Calle Mella','Calle Colón','Calle Altagracia','Calle 27 de Febrero','Calle Prudhomme','Calle Pedro Santana','Calle Santomé'];
BEGIN
  SELECT "Id" INTO v_aff FROM _a;

  -- Productos del catalogo real: se buscan por nombre en InventoryItems (Tienda) y, si no estan, en Products
  SELECT "Id","UnitPrice" INTO v_bot,p_bot FROM "InventoryItems" WHERE "AffiliateId"=v_aff AND NOT "IsDemo" AND "Name" ILIKE 'botell_n%' ORDER BY "CreatedAt" LIMIT 1;
  SELECT "Id","UnitPrice" INTO v_far,p_far FROM "InventoryItems" WHERE "AffiliateId"=v_aff AND NOT "IsDemo" AND ("Name" ILIKE '%fardo%' OR "Name" ILIKE '%fundita%') ORDER BY "CreatedAt" LIMIT 1;
  SELECT "Id","UnitPrice" INTO v_btl,p_btl FROM "InventoryItems" WHERE "AffiliateId"=v_aff AND NOT "IsDemo" AND "Name" ILIKE 'botellita%' ORDER BY "CreatedAt" LIMIT 1;
  IF v_bot IS NULL THEN SELECT "Id","Price" INTO v_bot,p_bot FROM "Products" WHERE "AffiliateId"=v_aff AND NOT "IsDemo" AND "Name" ILIKE 'botell_n%' ORDER BY "CreatedAt" LIMIT 1; END IF;
  IF v_far IS NULL THEN SELECT "Id","Price" INTO v_far,p_far FROM "Products" WHERE "AffiliateId"=v_aff AND NOT "IsDemo" AND ("Name" ILIKE '%fardo%' OR "Name" ILIKE '%fundita%') ORDER BY "CreatedAt" LIMIT 1; END IF;
  IF v_btl IS NULL THEN SELECT "Id","Price" INTO v_btl,p_btl FROM "Products" WHERE "AffiliateId"=v_aff AND NOT "IsDemo" AND "Name" ILIKE 'botellita%' ORDER BY "CreatedAt" LIMIT 1; END IF;
  IF v_bot IS NULL OR v_far IS NULL OR v_btl IS NULL THEN
    RAISE EXCEPTION 'No encontre los 3 productos reales (botellon / fardo de funditas / botellitas) en InventoryItems ni Products';
  END IF;
  RAISE NOTICE 'Precios usados: botellon %, fardo %, botellitas %', p_bot, p_far, p_btl;

  -- Tipo de envase (se reutiliza el existente; si no hay, se crea el botellon)
  SELECT "Id" INTO v_ct FROM "ContainerTypes" WHERE "AffiliateId"=v_aff AND "IsActive" ORDER BY "CreatedAt" LIMIT 1;
  IF v_ct IS NULL THEN
    v_ct := gen_random_uuid();
    INSERT INTO "ContainerTypes"("Id","AffiliateId","Name","Deposit","IsActive","CreatedAt") VALUES (v_ct, v_aff, 'Botellón', 0, true, now());
  END IF;

  -- 4 vehiculos (levantamiento: 4 vehiculos / rutas)
  INSERT INTO "Vehicles"("Id","AffiliateId","Plate","Name","CapacityContainers","Status","IsActive","Notes","CreatedAt")
  SELECT gen_random_uuid(), v_aff, 'L00'||n||'234', 'Camión '||n, 120, 'disponible', true, 'seed-prueba-semana', now()
  FROM generate_series(1,4) n;
  CREATE TEMP TABLE _v ON COMMIT DROP AS
    SELECT "Id" AS id, (row_number() OVER (ORDER BY "Name"))::int AS n FROM "Vehicles" WHERE "AffiliateId"=v_aff;

  -- Clientes (500): 25% contado, 50% credito 15 dias, 25% credito 30 dias
  CREATE TEMP TABLE _c ON COMMIT DROP AS
  SELECT i, gen_random_uuid() AS id,
         CASE i%4 WHEN 0 THEN 0 WHEN 2 THEN 30 ELSE 15 END AS credit,
         ((i/4)%4)+1 AS veh,
         CASE WHEN i%10=0 THEN 'daily' WHEN i%10 IN (1,2) THEN 'on_demand' ELSE 'weekly' END AS freq,
         CASE WHEN i%10<7 THEN 'biweekly' ELSE 'monthly' END AS bill,
         CASE WHEN i%15=0 THEN 45::numeric ELSE NULL END AS agreed,
         1 + i%4 AS btl
  FROM generate_series(1,v_n) i;

  -- Nombres: persona o negocio, sin repetir (si un nombre sale dos veces se le anade el barrio)
  ALTER TABLE _c ADD COLUMN nm text;
  UPDATE _c SET nm = CASE i%3
      WHEN 0 THEN v_tipo[i%12+1]||' '||v_ape[(i/12)%20+1]||' '||v_ape[(i*7+i/20)%20+1]
      ELSE v_nom[i%20+1]||' '||v_ape[(i/20+i%7)%20+1]||' '||v_ape[(i*3+i/7)%20+1] END;
  UPDATE _c c SET nm = c.nm||' ('||v_barrio[(x.rn%10)+1]||')'
  FROM (SELECT i, row_number() OVER (PARTITION BY nm ORDER BY i) AS rn FROM _c) x
  WHERE x.i=c.i AND x.rn>1;

  INSERT INTO "Customers"("Id","AffiliateId","Name","Phone","Notes","Status","TotalVisits","NoShows","Address","GpsLat","GpsLng","BillingCycle","DeliveryFrequency","CreditDays","CreatedAt")
  SELECT c.id, v_aff, c.nm,
         '809-'||(500+(c.i*7)%400)::text||'-'||lpad(((c.i*37)%10000)::text,4,'0'),
         'seed-prueba-semana', 'Active', 0, 0,
         v_calle[(c.i*3)%10+1]||' #'||((c.i%120)+1)||', Azua',
         (18.43 + ((c.i*13)%100)/2500.0)::numeric(9,6), (-70.75 + ((c.i*17)%100)/2500.0)::numeric(9,6),
         c.bill, c.freq, c.credit, now() - interval '60 days'
  FROM _c c;

  -- Contratos (precio acordado de 45 para el botellon en 1 de cada 15 clientes)
  INSERT INTO "Contracts"("Id","AffiliateId","CustomerId","DeliveryFrequency","BillingCycle","CreditDays","AgreedPrices","StartDate","IsActive","Notes","ContractNumber","RacksLoaned","BottlesLoaned","CreatedAt")
  SELECT gen_random_uuid(), v_aff, c.id, c.freq, c.bill, c.credit,
         CASE WHEN c.agreed IS NOT NULL THEN '[{"itemId":"'||v_bot::text||'","price":'||c.agreed::text||'}]' END,
         v_today - (30 + c.i%200), true, 'seed-prueba-semana', 'C-'||lpad(c.i::text,4,'0'), 0, c.btl, now() - interval '30 days'
  FROM _c c;

  -- Botellones prestados al inicio (saldo de envases por cliente)
  INSERT INTO "ContainerMovements"("Id","AffiliateId","CustomerId","ContainerTypeId","Type","Quantity","OccurredAt","RegisteredBy","Notes","CreatedAt")
  SELECT gen_random_uuid(), v_aff, c.id, v_ct, 'delivery', c.btl,
         ((v_today - 30)::timestamp + interval '9 hours') AT TIME ZONE v_tz, 'Carga inicial prueba', 'seed-prueba-semana', now()
  FROM _c c;

  -- Dias laborables de la ultima semana (lunes a sabado, sin hoy)
  CREATE TEMP TABLE _days ON COMMIT DROP AS
    SELECT k, v_today - k AS d, extract(dow FROM (v_today - k))::int AS dow
    FROM generate_series(1,7) k WHERE extract(dow FROM (v_today - k)) <> 0;

  -- Rutas: una por camion por dia
  CREATE TEMP TABLE _r ON COMMIT DROP AS
    SELECT gen_random_uuid() AS id, d.k, d.d, d.dow, v.n AS veh, v.id AS vid,
           'Ruta '||v.n||' — '||to_char(d.d,'DD/MM') AS name
    FROM _days d CROSS JOIN _v v;
  INSERT INTO "DeliveryRoutes"("Id","AffiliateId","Name","RouteDate","VehicleId","Status","DepartedAt","ClosedAt","Notes","CreatedAt")
  SELECT r.id, v_aff, r.name, r.d, r.vid, 'completed',
         (r.d::timestamp + interval '7 hours 30 minutes') AT TIME ZONE v_tz,
         (r.d::timestamp + interval '15 hours') AT TIME ZONE v_tz,
         'seed-prueba-semana', now()
  FROM _r r;

  -- Paradas segun frecuencia: diario todos los dias, semanal 1 dia fijo, por pedido ~1 de cada 5 dias
  CREATE TEMP TABLE _s ON COMMIT DROP AS
  SELECT gen_random_uuid() AS id, r.id AS rid, r.name AS rname, r.d, r.k, c.id AS cid, c.i, c.credit, c.agreed,
         (row_number() OVER (PARTITION BY r.id ORDER BY c.i))::int AS so,
         CASE WHEN (c.i + r.k) % 37 = 0 THEN 'failed' ELSE 'visited' END AS st
  FROM _r r JOIN _c c ON c.veh = r.veh
  WHERE c.freq='daily' OR (c.freq='weekly' AND c.i%6 = r.dow-1) OR (c.freq='on_demand' AND (c.i + r.k)%5 = 0);
  ALTER TABLE _s ADD COLUMN at timestamptz;
  UPDATE _s SET at = ((d::timestamp + interval '7 hours 30 minutes') + (so*6)*interval '1 minute') AT TIME ZONE v_tz;

  INSERT INTO "RouteStops"("Id","AffiliateId","RouteId","CustomerId","SortOrder","Status","VisitedAt","ArrivedAt","DepartedAt","Notes","CreatedAt")
  SELECT s.id, v_aff, s.rid, s.cid, s.so, s.st, s.at, s.at, s.at + interval '4 minutes',
         CASE WHEN s.st='failed' THEN 'Cliente cerrado' END, now()
  FROM _s s;

  -- Entregas (solo paradas visitadas)
  CREATE TEMP TABLE _d ON COMMIT DROP AS
  SELECT gen_random_uuid() AS id, s.id AS sid, s.rid, s.rname, s.cid, s.i, s.k, s.credit, s.at,
         1 + (s.i + s.k) % 5 AS qb,
         CASE WHEN s.i%3=0 THEN 1 + s.i%2 ELSE 0 END AS qf,
         CASE WHEN s.i%7=0 THEN 1 ELSE 0 END AS qs,
         s.agreed
  FROM _s s WHERE s.st='visited';
  ALTER TABLE _d ADD COLUMN tot numeric, ADD COLUMN inv uuid, ADD COLUMN pay uuid, ADD COLUMN payamt numeric;
  UPDATE _d SET tot = qb*COALESCE(agreed,p_bot) + qf*p_far + qs*p_btl;
  -- contado: factura y cobro en el momento; credito: 1 de cada 6 entregas deja un abono (50%) sin factura
  UPDATE _d SET inv = CASE WHEN credit=0 THEN gen_random_uuid() END,
                pay = CASE WHEN credit=0 OR (i+k)%6=0 THEN gen_random_uuid() END,
                payamt = CASE WHEN credit=0 THEN tot WHEN (i+k)%6=0 THEN round(tot*0.5,2) END;

  INSERT INTO "Invoices"("Id","AffiliateId","CustomerId","InvoiceNumber","Subtotal","Tax","Total","Status","IssueDate","DueDate","PaidDate","Notes","CreatedAt")
  SELECT d.inv, v_aff, d.cid, 'INV-'||to_char(d.at,'YYYYMMDD')||'-'||substr(md5(d.inv::text),1,8),
         d.tot, 0, d.tot, 'Paid', d.at, d.at, d.at, 'Entrega en ruta: '||d.rname, now()
  FROM _d d WHERE d.inv IS NOT NULL;
  INSERT INTO "InvoiceItems"("Id","InvoiceId","Description","Quantity","UnitPrice","Total","CreatedAt")
  SELECT gen_random_uuid(), d.inv, 'Botellón de agua', d.qb, COALESCE(d.agreed,p_bot), d.qb*COALESCE(d.agreed,p_bot), now() FROM _d d WHERE d.inv IS NOT NULL
  UNION ALL SELECT gen_random_uuid(), d.inv, 'Fardo de funditas', d.qf, p_far, d.qf*p_far, now() FROM _d d WHERE d.inv IS NOT NULL AND d.qf>0
  UNION ALL SELECT gen_random_uuid(), d.inv, 'Botellitas', d.qs, p_btl, d.qs*p_btl, now() FROM _d d WHERE d.inv IS NOT NULL AND d.qs>0;

  INSERT INTO "Payments"("Id","AffiliateId","CustomerId","InvoiceId","Amount","Method","PaidAt","Notes","RegisteredBy","CreatedAt")
  SELECT d.pay, v_aff, d.cid, d.inv, d.payamt, CASE WHEN d.i%3=0 THEN 'transfer' ELSE 'cash' END, d.at,
         'Cobro en ruta: '||d.rname, 'Conductor prueba', now()
  FROM _d d WHERE d.pay IS NOT NULL;

  INSERT INTO "Deliveries"("Id","AffiliateId","RouteStopId","CustomerId","DeliveredAt","InvoiceId","PaymentId","ReceivedBy","Notes","RegisteredBy","CreatedAt")
  SELECT d.id, v_aff, d.sid, d.cid, d.at, d.inv, d.pay, 'Recibe en el local', 'seed-prueba-semana', 'Conductor prueba', now()
  FROM _d d;
  INSERT INTO "DeliveryItems"("Id","DeliveryId","ItemId","Description","Quantity","UnitPrice","CreatedAt")
  SELECT gen_random_uuid(), d.id, v_bot, 'Botellón de agua', d.qb, COALESCE(d.agreed,p_bot), now() FROM _d d
  UNION ALL SELECT gen_random_uuid(), d.id, v_far, 'Fardo de funditas', d.qf, p_far, now() FROM _d d WHERE d.qf>0
  UNION ALL SELECT gen_random_uuid(), d.id, v_btl, 'Botellitas', d.qs, p_btl, now() FROM _d d WHERE d.qs>0;

  -- Envases: entrega de botellones; devuelve los vacios (a veces falta 1; a veces 1 danado)
  INSERT INTO "ContainerMovements"("Id","AffiliateId","CustomerId","ContainerTypeId","DeliveryId","Type","Quantity","OccurredAt","RegisteredBy","CreatedAt")
  SELECT gen_random_uuid(), v_aff, d.cid, v_ct, d.id, 'delivery', d.qb, d.at, 'Conductor prueba', now() FROM _d d
  UNION ALL SELECT gen_random_uuid(), v_aff, d.cid, v_ct, d.id, 'return', greatest(d.qb - CASE WHEN (d.i+d.k)%9=0 THEN 1 ELSE 0 END - CASE WHEN (d.i+d.k)%53=0 THEN 1 ELSE 0 END, 0), d.at, 'Conductor prueba', now()
            FROM _d d WHERE d.qb - CASE WHEN (d.i+d.k)%9=0 THEN 1 ELSE 0 END - CASE WHEN (d.i+d.k)%53=0 THEN 1 ELSE 0 END > 0
  UNION ALL SELECT gen_random_uuid(), v_aff, d.cid, v_ct, d.id, 'damaged', 1, d.at, 'Conductor prueba', now() FROM _d d WHERE (d.i+d.k)%53=0;

  -- Facturacion por ciclo: 40% de los clientes a credito ya tiene facturadas sus entregas de hace 4+ dias
  CREATE TEMP TABLE _ci ON COMMIT DROP AS
  SELECT gen_random_uuid() AS id, d.cid, max(d.i) AS i, max(d.credit) AS credit, sum(d.tot) AS tot
  FROM _d d WHERE d.credit>0 AND d.k>=4 AND d.i%5<2 GROUP BY d.cid;
  INSERT INTO "Invoices"("Id","AffiliateId","CustomerId","InvoiceNumber","Subtotal","Tax","Total","Status","IssueDate","DueDate","Notes","CreatedAt")
  SELECT ci.id, v_aff, ci.cid, 'INV-'||to_char(v_today-3,'YYYYMMDD')||'-'||substr(md5(ci.id::text),1,8),
         ci.tot, 0, ci.tot, 'Pending', ((v_today-3)::timestamp + interval '10 hours') AT TIME ZONE v_tz,
         ((v_today-3+ci.credit)::timestamp + interval '10 hours') AT TIME ZONE v_tz, 'Facturación del ciclo (prueba)', now()
  FROM _ci ci;
  UPDATE "Deliveries" dl SET "InvoiceId" = ci.id FROM _d d JOIN _ci ci ON ci.cid=d.cid WHERE dl."Id"=d.id AND d.credit>0 AND d.k>=4;
  INSERT INTO "InvoiceItems"("Id","InvoiceId","Description","Quantity","UnitPrice","Total","CreatedAt")
  SELECT gen_random_uuid(), ci.id, di."Description", sum(di."Quantity")::int, di."UnitPrice", sum(di."Quantity")*di."UnitPrice", now()
  FROM _ci ci JOIN _d d ON d.cid=ci.cid AND d.credit>0 AND d.k>=4
  JOIN "DeliveryItems" di ON di."DeliveryId"=d.id
  GROUP BY ci.id, di."Description", di."UnitPrice";

  -- Historico previo para antiguedad de saldos: ~25 clientes con factura de hace 20-34 dias (algunas ya vencidas)
  CREATE TEMP TABLE _old ON COMMIT DROP AS
  SELECT gen_random_uuid() AS id, c.id AS cid, c.i, c.credit,
         (v_today - (20 + c.i%15)) AS idate, round(300 + (c.i%9)*120, 2) AS tot
  FROM _c c WHERE c.credit>0 AND c.i%12=1;
  INSERT INTO "Invoices"("Id","AffiliateId","CustomerId","InvoiceNumber","Subtotal","Tax","Total","Status","IssueDate","DueDate","Notes","CreatedAt")
  SELECT o.id, v_aff, o.cid, 'INV-'||to_char(o.idate,'YYYYMMDD')||'-'||substr(md5(o.id::text),1,8),
         o.tot, 0, o.tot, CASE WHEN o.idate + o.credit < v_today THEN 'Overdue' ELSE 'Pending' END,
         (o.idate::timestamp + interval '10 hours') AT TIME ZONE v_tz, ((o.idate + o.credit)::timestamp + interval '10 hours') AT TIME ZONE v_tz,
         'Facturación del periodo anterior (prueba)', now()
  FROM _old o;
  INSERT INTO "InvoiceItems"("Id","InvoiceId","Description","Quantity","UnitPrice","Total","CreatedAt")
  SELECT gen_random_uuid(), o.id, 'Entregas del periodo anterior', 1, o.tot, o.tot, now() FROM _old o;
  -- abonos a cuenta (sin factura) de algunos de esos clientes
  INSERT INTO "Payments"("Id","AffiliateId","CustomerId","Amount","Method","PaidAt","Notes","RegisteredBy","CreatedAt")
  SELECT gen_random_uuid(), v_aff, o.cid, round(o.tot*0.4,2), 'transfer',
         ((v_today-5)::timestamp + interval '11 hours') AT TIME ZONE v_tz, 'Abono a cuenta (prueba)', 'Contabilidad prueba', now()
  FROM _old o WHERE o.i%24=1;

  -- Carga de cada camion, ultima visita y total de visitas
  UPDATE "DeliveryRoutes" r SET "LoadedFull" = x.q + 10
  FROM (SELECT d.rid, sum(d.qb)::int AS q FROM _d d GROUP BY d.rid) x WHERE r."Id"=x.rid;
  UPDATE "Customers" c SET "LastVisit"=m.mx, "TotalVisits"=m.n
  FROM (SELECT cid, max(at) AS mx, count(*)::int AS n FROM _d GROUP BY cid) m WHERE c."Id"=m.cid;

  -- Listas de precio: los negocios que revenden (colmado, supermercado, farmacia, ferretería) van a Mayorista;
  -- el resto de los clientes con contrato, a Detalle. Lo demás queda a precio público. Las listas se crean solo si
  -- el negocio no las tiene ya, para no pisar las que el dueño haya configurado.
  DECLARE
    v_may uuid; v_det uuid;
  BEGIN
    SELECT "Id" INTO v_may FROM "PriceLists" WHERE "AffiliateId"=v_aff AND "Name" ILIKE 'mayorista%' ORDER BY "CreatedAt" LIMIT 1;
    IF v_may IS NULL THEN
      v_may := gen_random_uuid();
      INSERT INTO "PriceLists"("Id","AffiliateId","Name","Description","DiscountPercent","MinOrderAmount","TaxExempt","IsActive","SortOrder","CreatedAt")
      VALUES (v_may, v_aff, 'Mayorista', 'Colmados y negocios que revenden', 10, 500, false, true, 0, now());
      INSERT INTO "PriceListItems"("Id","PriceListId","ItemId","Price","CreatedAt")
      VALUES (gen_random_uuid(), v_may, v_bot, round(p_bot*0.80,2), now());
    END IF;

    SELECT "Id" INTO v_det FROM "PriceLists" WHERE "AffiliateId"=v_aff AND "Name" ILIKE 'detalle%' ORDER BY "CreatedAt" LIMIT 1;
    IF v_det IS NULL THEN
      v_det := gen_random_uuid();
      INSERT INTO "PriceLists"("Id","AffiliateId","Name","Description","DiscountPercent","MinOrderAmount","TaxExempt","IsActive","SortOrder","CreatedAt")
      VALUES (v_det, v_aff, 'Detalle', 'Clientes con contrato, sin reventa', 5, NULL, false, true, 1, now());
    END IF;

    UPDATE "Customers" c SET "PriceListId" = v_may
    FROM _c x WHERE c."Id"=x.id AND x.i%3=0;
    UPDATE "Customers" c SET "PriceListId" = v_det
    FROM _c x WHERE c."Id"=x.id AND x.i%3=1;
  END;

  -- Pedidos: lo que entra por la pagina y lo que se cobra en el punto de venta. Las entregas del camion NO son
  -- pedidos (crean entrega + factura), asi que sin esto la pantalla de Pedidos queda vacia y parece rota.
  INSERT INTO "Orders"("Id","AffiliateId","CustomerId","CustomerName","CustomerPhone","Notes","ItemsJson",
                       "Subtotal","Tax","Tip","Total","Currency","Status","Channel","PaymentMethod",
                       "CollectedAt","CollectedMethod","CreatedAt")
  SELECT gen_random_uuid(), v_aff, c.id, c.nm, cu."Phone", 'seed-prueba-semana',
         '[{"itemId":"'||v_bot::text||'","name":"Botellón de agua","price":'||p_bot::text||',"qty":'||q.qty::text||'}]',
         round(p_bot*q.qty,2), 0, 0, round(p_bot*q.qty,2), 'DOP',
         CASE WHEN q.n%7=0 THEN 0 ELSE 2 END,            -- 0 Pending (por atender), 2 Fulfilled
         CASE WHEN q.n%2=0 THEN 'POS' ELSE 'Online' END,
         CASE WHEN q.n%2=0 THEN 'Cash' ELSE 'PayAtPickup' END,
         CASE WHEN q.n%7=0 THEN NULL ELSE ((v_today - (q.n%6))::timestamp + interval '11 hours') AT TIME ZONE v_tz END,
         CASE WHEN q.n%7=0 THEN NULL ELSE 'Cash' END,
         ((v_today - (q.n%6))::timestamp + interval '10 hours' + (q.n*17)*interval '1 minute') AT TIME ZONE v_tz
  FROM (SELECT i, (row_number() OVER (ORDER BY i))::int AS n, 1 + i%4 AS qty FROM _c WHERE i%23=0 LIMIT 22) q
  JOIN _c c ON c.i=q.i
  JOIN "Customers" cu ON cu."Id"=c.id;

  -- Gastos fijos de la semana (RD$)
  INSERT INTO "Expenses"("Id","AffiliateId","Category","Description","Amount","ExpenseDate","Recurrence","Vendor","PaymentMethod","Notes","IsActive","CreatedAt")
  SELECT gen_random_uuid(), v_aff, e.cat, e.descr, e.amt, v_today - e.ago, 'none', e.vendor, e.pm, 'seed-prueba-semana', true, now()
  FROM (VALUES
    ('luz',           'Factura de energía eléctrica',      18500.00, 5, 'Edesur',         'transfer'),
    ('agua',          'Agua de la planta',                  2300.00, 4, 'CORAASAN',       'transfer'),
    ('alquiler',      'Alquiler del local',                25000.00, 6, 'Propietario',    'transfer'),
    ('nomina',        'Nómina quincenal',                  62000.00, 2, NULL,             'transfer'),
    ('internet',      'Internet y teléfono',                2200.00, 3, 'Claro',          'transfer'),
    ('mantenimiento', 'Mantenimiento de equipos',           5400.00, 1, 'Taller Santos',  'cash'),
    ('prestamos',     'Cuota de préstamo',                  9800.00, 4, 'Banco',          'transfer')
  ) AS e(cat,descr,amt,ago,vendor,pm);
  INSERT INTO "Expenses"("Id","AffiliateId","Category","Description","Amount","ExpenseDate","Recurrence","Vendor","PaymentMethod","Notes","IsActive","CreatedAt")
  SELECT gen_random_uuid(), v_aff, 'combustible', 'Combustible camiones', 3200.00, d.d, 'none', 'Estación de servicio', 'cash', 'seed-prueba-semana', true, now()
  FROM _days d;
END $SEED$;

-- ===================== Resumen del ensayo =====================
SELECT * FROM _log ORDER BY tabla;
SELECT 'Customers' AS tabla, count(*) FROM "Customers" WHERE "AffiliateId" IN (SELECT "Id" FROM _a)
UNION ALL SELECT 'Contracts',  count(*) FROM "Contracts"  WHERE "AffiliateId" IN (SELECT "Id" FROM _a)
UNION ALL SELECT 'DeliveryRoutes', count(*) FROM "DeliveryRoutes" WHERE "AffiliateId" IN (SELECT "Id" FROM _a)
UNION ALL SELECT 'RouteStops', count(*) FROM "RouteStops" WHERE "AffiliateId" IN (SELECT "Id" FROM _a)
UNION ALL SELECT 'Deliveries', count(*) FROM "Deliveries" WHERE "AffiliateId" IN (SELECT "Id" FROM _a)
UNION ALL SELECT 'Invoices',   count(*) FROM "Invoices"   WHERE "AffiliateId" IN (SELECT "Id" FROM _a)
UNION ALL SELECT 'Payments',   count(*) FROM "Payments"   WHERE "AffiliateId" IN (SELECT "Id" FROM _a)
UNION ALL SELECT 'ContainerMovements', count(*) FROM "ContainerMovements" WHERE "AffiliateId" IN (SELECT "Id" FROM _a)
UNION ALL SELECT 'Expenses',   count(*) FROM "Expenses"   WHERE "AffiliateId" IN (SELECT "Id" FROM _a)
UNION ALL SELECT 'Vehicles',   count(*) FROM "Vehicles"   WHERE "AffiliateId" IN (SELECT "Id" FROM _a)
UNION ALL SELECT 'Orders',     count(*) FROM "Orders"     WHERE "AffiliateId" IN (SELECT "Id" FROM _a)
UNION ALL SELECT 'PriceLists', count(*) FROM "PriceLists" WHERE "AffiliateId" IN (SELECT "Id" FROM _a)
UNION ALL SELECT 'Clientes con lista', count(*) FROM "Customers" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) AND "PriceListId" IS NOT NULL;
SELECT
  (SELECT coalesce(sum("Total"),0) FROM "Invoices" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) AND "Status"='Paid')            AS facturado_cobrado,
  (SELECT coalesce(sum("Total"),0) FROM "Invoices" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) AND "Status" IN ('Pending','Overdue')) AS facturas_por_cobrar,
  (SELECT coalesce(sum("Total"),0) FROM "Invoices" WHERE "AffiliateId" IN (SELECT "Id" FROM _a) AND "Status"='Overdue')         AS vencido,
  (SELECT coalesce(sum("Amount"),0) FROM "Payments" WHERE "AffiliateId" IN (SELECT "Id" FROM _a))                              AS total_pagos,
  (SELECT coalesce(sum("Amount"),0) FROM "Expenses" WHERE "AffiliateId" IN (SELECT "Id" FROM _a))                              AS total_gastos;

COMMIT;
