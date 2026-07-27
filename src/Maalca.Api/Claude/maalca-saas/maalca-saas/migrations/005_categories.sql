-- =============================================================================
-- Migration 005 — Categories table + link catalog_items → categories
--
-- PREREQUISITE: 004_catalog_split.sql must be applied first.
--
-- What this does:
--   1. Creates categories table with RLS
--   2. Backfills default categories for all existing businesses by type
--   3. Adds category_id FK to catalog_items (alongside existing category TEXT)
--   4. Backfills category_id where catalog_items.category matches categories.name
--
-- Note: catalog_items.category (TEXT) is kept for backward compat. New code
-- should write both fields; old code can still read the TEXT column.
-- =============================================================================

-- 1. Categories table
CREATE TABLE categories (
  id           UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  business_id  UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
  name         TEXT NOT NULL,
  sort_order   INT NOT NULL DEFAULT 0,
  is_default   BOOLEAN NOT NULL DEFAULT false,
  created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  UNIQUE (business_id, name)
);

CREATE INDEX categories_business_idx ON categories (business_id, sort_order);

-- 2. RLS
ALTER TABLE categories ENABLE ROW LEVEL SECURITY;

CREATE POLICY "categories_owner_all" ON categories
  FOR ALL USING (
    EXISTS (SELECT 1 FROM businesses b WHERE b.id = categories.business_id AND b.owner_id = auth.uid())
  ) WITH CHECK (
    EXISTS (SELECT 1 FROM businesses b WHERE b.id = categories.business_id AND b.owner_id = auth.uid())
  );

CREATE POLICY "categories_public_read" ON categories
  FOR SELECT USING (
    EXISTS (SELECT 1 FROM businesses b WHERE b.id = categories.business_id AND b.published = true)
  );

-- 3. Backfill default categories for all existing businesses
INSERT INTO categories (business_id, name, sort_order, is_default)
SELECT b.id, defaults.cat, defaults.ord, true
FROM businesses b
CROSS JOIN LATERAL (
  VALUES
    ('restaurant', 'Entradas',      0),
    ('restaurant', 'Principales',   1),
    ('restaurant', 'Bebidas',       2),
    ('restaurant', 'Postres',       3),
    ('barber',     'Cortes',        0),
    ('barber',     'Barba',         1),
    ('barber',     'Tratamientos',  2),
    ('barber',     'Combos',        3),
    ('service',    'Servicios',     0),
    ('service',    'Paquetes',      1),
    ('retail',     'Destacados',    0),
    ('retail',     'Nuevos',        1)
) AS defaults(btype, cat, ord)
WHERE b.business_type = defaults.btype
ON CONFLICT (business_id, name) DO NOTHING;

-- 4. Add category_id FK to catalog_items
ALTER TABLE catalog_items
  ADD COLUMN category_id UUID REFERENCES categories(id) ON DELETE SET NULL;

CREATE INDEX catalog_items_category_idx
  ON catalog_items (category_id) WHERE category_id IS NOT NULL;

-- 5. Backfill category_id where text name matches
UPDATE catalog_items ci
SET category_id = c.id
FROM categories c
WHERE c.business_id = ci.business_id
  AND c.name = ci.category;

-- =============================================================================
-- Verification queries (run after applying):
--
--   SELECT b.name, count(c.id) AS cat_count
--   FROM businesses b LEFT JOIN categories c ON c.business_id = b.id
--   GROUP BY b.name;
--   -- Every business should have ≥2 rows (service/retail) or 4 rows (restaurant/barber)
--
--   SELECT count(*) FROM catalog_items WHERE category_id IS NULL AND category IS NOT NULL;
--   -- Items with a category text that didn't match defaults → needs 12-E manual audit
-- =============================================================================
