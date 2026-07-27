-- =============================================================================
-- Migration 004 — Catalog split: products → catalog_items + child tables
--
-- PREREQUISITE: Run `CREATE TABLE products_backup AS SELECT * FROM products`
-- in Supabase BEFORE applying this migration.
--
-- What this does:
--   1. Renames products → catalog_items
--   2. Adds item_type (TEXT) and is_demo (BOOLEAN) columns
--   3. Creates child tables: menu_items, service_items, retail_items
--   4. Adds trigger to enforce item_type matches business_type
--   5. Backfills item_type from parent business.business_type
--   6. Migrates duration_min → service_items, then drops from catalog_items
--   7. Renames old indexes to reflect new table name
--
-- Backward compat: catalog_items keeps all columns products had. Old API
-- routes reading from the table continue to work until migrated.
-- =============================================================================

-- 1. Rename
ALTER TABLE products RENAME TO catalog_items;

-- 2. New columns
ALTER TABLE catalog_items ADD COLUMN item_type TEXT NOT NULL DEFAULT 'generic';
ALTER TABLE catalog_items ADD CONSTRAINT catalog_items_type_check
  CHECK (item_type IN ('menu', 'service', 'retail', 'generic'));

ALTER TABLE catalog_items ADD COLUMN is_demo BOOLEAN NOT NULL DEFAULT false;

-- 3. Child table: menu_items (restaurant)
CREATE TABLE menu_items (
  catalog_item_id  UUID PRIMARY KEY REFERENCES catalog_items(id) ON DELETE CASCADE,
  modifiers        JSONB NOT NULL DEFAULT '[]'::jsonb,
  allergens        TEXT[] NOT NULL DEFAULT '{}',
  spicy_level      INT CHECK (spicy_level BETWEEN 0 AND 3),
  available_from   TIME,
  available_to     TIME
);

-- 4. Child table: service_items (barber + service)
CREATE TABLE service_items (
  catalog_item_id   UUID PRIMARY KEY REFERENCES catalog_items(id) ON DELETE CASCADE,
  duration_min      INT NOT NULL CHECK (duration_min > 0),
  buffer_min        INT NOT NULL DEFAULT 0,
  requires_booking  BOOLEAN NOT NULL DEFAULT false,
  staff_required    INT NOT NULL DEFAULT 1
);

-- 5. Child table: retail_items
CREATE TABLE retail_items (
  catalog_item_id  UUID PRIMARY KEY REFERENCES catalog_items(id) ON DELETE CASCADE,
  stock            INT,
  sku              TEXT,
  variants         JSONB NOT NULL DEFAULT '[]'::jsonb,
  weight_grams     INT
);

-- 6. Trigger: item_type must match parent business_type
CREATE OR REPLACE FUNCTION enforce_item_type_matches_business()
RETURNS TRIGGER AS $$
DECLARE
  bt TEXT;
BEGIN
  SELECT business_type INTO bt FROM businesses WHERE id = NEW.business_id;
  IF bt = 'restaurant' AND NEW.item_type NOT IN ('menu', 'generic') THEN
    RAISE EXCEPTION 'restaurant business cannot have item_type=%', NEW.item_type;
  ELSIF bt = 'barber' AND NEW.item_type NOT IN ('service', 'generic') THEN
    RAISE EXCEPTION 'barber business cannot have item_type=%', NEW.item_type;
  ELSIF bt = 'service' AND NEW.item_type NOT IN ('service', 'generic') THEN
    RAISE EXCEPTION 'service business cannot have item_type=%', NEW.item_type;
  ELSIF bt = 'retail' AND NEW.item_type NOT IN ('retail', 'generic') THEN
    RAISE EXCEPTION 'retail business cannot have item_type=%', NEW.item_type;
  END IF;
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

CREATE TRIGGER catalog_items_type_match
BEFORE INSERT OR UPDATE ON catalog_items
FOR EACH ROW EXECUTE FUNCTION enforce_item_type_matches_business();

-- 7. Backfill item_type from parent business
UPDATE catalog_items ci SET item_type = CASE
  WHEN b.business_type = 'restaurant' THEN 'menu'
  WHEN b.business_type = 'barber'     THEN 'service'
  WHEN b.business_type = 'service'    THEN 'service'
  WHEN b.business_type = 'retail'     THEN 'retail'
  ELSE 'generic'
END
FROM businesses b
WHERE b.id = ci.business_id;

-- 8. Migrate duration_min → service_items for existing barber/service items
INSERT INTO service_items (catalog_item_id, duration_min, requires_booking)
SELECT ci.id, COALESCE(ci.duration_min, 30), false
FROM catalog_items ci
JOIN businesses b ON b.id = ci.business_id
WHERE b.business_type IN ('barber', 'service')
  AND ci.duration_min IS NOT NULL
ON CONFLICT DO NOTHING;

-- 9. Drop duration_min from catalog_items (now lives in service_items)
ALTER TABLE catalog_items DROP COLUMN duration_min;

-- 10. Rename old indexes to match new table name
ALTER INDEX IF EXISTS products_business_active_idx RENAME TO catalog_items_business_active_idx;
ALTER INDEX IF EXISTS products_business_idx        RENAME TO catalog_items_business_idx;

-- 11. RLS for new child tables (inherit via catalog_items → businesses check)
ALTER TABLE menu_items     ENABLE ROW LEVEL SECURITY;
ALTER TABLE service_items  ENABLE ROW LEVEL SECURITY;
ALTER TABLE retail_items   ENABLE ROW LEVEL SECURITY;

-- Owner: full access if they own the parent business
CREATE POLICY "menu_items_owner_all" ON menu_items
  FOR ALL USING (
    EXISTS (
      SELECT 1 FROM catalog_items ci
      JOIN businesses b ON b.id = ci.business_id
      WHERE ci.id = menu_items.catalog_item_id AND b.owner_id = auth.uid()
    )
  ) WITH CHECK (
    EXISTS (
      SELECT 1 FROM catalog_items ci
      JOIN businesses b ON b.id = ci.business_id
      WHERE ci.id = menu_items.catalog_item_id AND b.owner_id = auth.uid()
    )
  );

CREATE POLICY "service_items_owner_all" ON service_items
  FOR ALL USING (
    EXISTS (
      SELECT 1 FROM catalog_items ci
      JOIN businesses b ON b.id = ci.business_id
      WHERE ci.id = service_items.catalog_item_id AND b.owner_id = auth.uid()
    )
  ) WITH CHECK (
    EXISTS (
      SELECT 1 FROM catalog_items ci
      JOIN businesses b ON b.id = ci.business_id
      WHERE ci.id = service_items.catalog_item_id AND b.owner_id = auth.uid()
    )
  );

CREATE POLICY "retail_items_owner_all" ON retail_items
  FOR ALL USING (
    EXISTS (
      SELECT 1 FROM catalog_items ci
      JOIN businesses b ON b.id = ci.business_id
      WHERE ci.id = retail_items.catalog_item_id AND b.owner_id = auth.uid()
    )
  ) WITH CHECK (
    EXISTS (
      SELECT 1 FROM catalog_items ci
      JOIN businesses b ON b.id = ci.business_id
      WHERE ci.id = retail_items.catalog_item_id AND b.owner_id = auth.uid()
    )
  );

-- Public read: if parent business is published and item is active
CREATE POLICY "menu_items_public_read" ON menu_items
  FOR SELECT USING (
    EXISTS (
      SELECT 1 FROM catalog_items ci
      JOIN businesses b ON b.id = ci.business_id
      WHERE ci.id = menu_items.catalog_item_id
        AND ci.active = true AND b.published = true
    )
  );

CREATE POLICY "service_items_public_read" ON service_items
  FOR SELECT USING (
    EXISTS (
      SELECT 1 FROM catalog_items ci
      JOIN businesses b ON b.id = ci.business_id
      WHERE ci.id = service_items.catalog_item_id
        AND ci.active = true AND b.published = true
    )
  );

CREATE POLICY "retail_items_public_read" ON retail_items
  FOR SELECT USING (
    EXISTS (
      SELECT 1 FROM catalog_items ci
      JOIN businesses b ON b.id = ci.business_id
      WHERE ci.id = retail_items.catalog_item_id
        AND ci.active = true AND b.published = true
    )
  );

-- =============================================================================
-- Verification queries (run after applying):
--
--   SELECT count(*) FROM catalog_items;           -- must equal old products count
--   SELECT count(*) FROM service_items;           -- must equal items with duration_min
--   SELECT item_type, count(*) FROM catalog_items GROUP BY item_type;
--   SELECT * FROM catalog_items LIMIT 1;          -- confirm no duration_min column
--
-- Test trigger (must fail):
--   INSERT INTO catalog_items (business_id, name, item_type)
--   SELECT id, 'test', 'retail' FROM businesses
--   WHERE business_type = 'restaurant' LIMIT 1;
-- =============================================================================
