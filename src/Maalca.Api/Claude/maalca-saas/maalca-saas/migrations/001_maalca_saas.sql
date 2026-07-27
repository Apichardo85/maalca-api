-- =============================================================================
-- MaalCa SaaS — Schema migration 001
-- Run via Supabase SQL editor or `supabase db push`
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. Extend businesses table
-- -----------------------------------------------------------------------------
-- Assumes table exists from prior work. Add new columns idempotently.

ALTER TABLE businesses ADD COLUMN IF NOT EXISTS business_type TEXT NOT NULL DEFAULT 'service';
ALTER TABLE businesses ADD COLUMN IF NOT EXISTS plan TEXT NOT NULL DEFAULT 'free';
ALTER TABLE businesses ADD COLUMN IF NOT EXISTS plan_status TEXT NOT NULL DEFAULT 'active';
ALTER TABLE businesses ADD COLUMN IF NOT EXISTS published BOOLEAN NOT NULL DEFAULT true;
ALTER TABLE businesses ADD COLUMN IF NOT EXISTS whatsapp TEXT;
ALTER TABLE businesses ADD COLUMN IF NOT EXISTS logo_url TEXT;
ALTER TABLE businesses ADD COLUMN IF NOT EXISTS primary_color TEXT DEFAULT '#C8102E';
ALTER TABLE businesses ADD COLUMN IF NOT EXISTS description TEXT;
ALTER TABLE businesses ADD COLUMN IF NOT EXISTS stripe_customer_id TEXT;
ALTER TABLE businesses ADD COLUMN IF NOT EXISTS stripe_subscription_id TEXT;
ALTER TABLE businesses ADD COLUMN IF NOT EXISTS updated_at TIMESTAMPTZ DEFAULT NOW();

-- Constraints
ALTER TABLE businesses DROP CONSTRAINT IF EXISTS businesses_business_type_check;
ALTER TABLE businesses ADD CONSTRAINT businesses_business_type_check
  CHECK (business_type IN ('restaurant', 'barber', 'service', 'retail'));

ALTER TABLE businesses DROP CONSTRAINT IF EXISTS businesses_plan_check;
ALTER TABLE businesses ADD CONSTRAINT businesses_plan_check
  CHECK (plan IN ('free', 'entrepreneur'));

ALTER TABLE businesses DROP CONSTRAINT IF EXISTS businesses_plan_status_check;
ALTER TABLE businesses ADD CONSTRAINT businesses_plan_status_check
  CHECK (plan_status IN ('active', 'past_due', 'canceled'));

-- Slug uniqueness + format
CREATE UNIQUE INDEX IF NOT EXISTS businesses_slug_unique ON businesses (slug);
ALTER TABLE businesses DROP CONSTRAINT IF EXISTS businesses_slug_format;
ALTER TABLE businesses ADD CONSTRAINT businesses_slug_format
  CHECK (slug ~ '^[a-z0-9][a-z0-9-]{1,49}$');

CREATE INDEX IF NOT EXISTS businesses_owner_idx ON businesses (owner_id);
CREATE INDEX IF NOT EXISTS businesses_published_idx ON businesses (published) WHERE published = true;

-- -----------------------------------------------------------------------------
-- 2. Products table
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS products (
  id            UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  business_id   UUID NOT NULL REFERENCES businesses(id) ON DELETE CASCADE,
  name          TEXT NOT NULL,
  description   TEXT,
  price         NUMERIC(10, 2),
  category      TEXT,
  image_url     TEXT,
  duration_min  INT,                          -- for barber/service
  active        BOOLEAN NOT NULL DEFAULT true,
  sort_order    INT NOT NULL DEFAULT 0,
  created_at    TIMESTAMPTZ NOT NULL DEFAULT NOW(),
  updated_at    TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS products_business_active_idx
  ON products (business_id, active, sort_order)
  WHERE active = true;

CREATE INDEX IF NOT EXISTS products_business_idx ON products (business_id);

-- -----------------------------------------------------------------------------
-- 3. Onboarding progress (persisted checklist)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS onboarding_progress (
  business_id            UUID PRIMARY KEY REFERENCES businesses(id) ON DELETE CASCADE,
  first_product_added    BOOLEAN NOT NULL DEFAULT false,
  whatsapp_configured    BOOLEAN NOT NULL DEFAULT false,
  link_shared            BOOLEAN NOT NULL DEFAULT false,
  completed_at           TIMESTAMPTZ,
  updated_at             TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

-- -----------------------------------------------------------------------------
-- 4. Analytics events (server-side source of truth)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS analytics_events (
  id           BIGSERIAL PRIMARY KEY,
  user_id      UUID REFERENCES auth.users(id) ON DELETE SET NULL,
  business_id  UUID REFERENCES businesses(id) ON DELETE SET NULL,
  event_name   TEXT NOT NULL,
  properties   JSONB NOT NULL DEFAULT '{}'::jsonb,
  session_id   TEXT,
  user_agent   TEXT,
  created_at   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);

CREATE INDEX IF NOT EXISTS analytics_events_name_time_idx
  ON analytics_events (event_name, created_at DESC);
CREATE INDEX IF NOT EXISTS analytics_events_business_idx
  ON analytics_events (business_id, created_at DESC) WHERE business_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS analytics_events_user_idx
  ON analytics_events (user_id, created_at DESC) WHERE user_id IS NOT NULL;

-- -----------------------------------------------------------------------------
-- 5. Reserved slugs (prevents conflicts with app routes)
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS reserved_slugs (
  slug TEXT PRIMARY KEY
);

INSERT INTO reserved_slugs (slug) VALUES
  ('servicios'), ('login'), ('signup'), ('register'),
  ('onboarding'), ('space'), ('dashboard'), ('admin'),
  ('api'), ('auth'), ('app'), ('www'),
  ('about'), ('contact'), ('contacto'), ('pricing'),
  ('terms'), ('privacy'), ('legal'), ('help'),
  ('blog'), ('docs'), ('_next'), ('static'),
  ('public'), ('assets'), ('images'),
  ('maalca'), ('the-little-dominican'), ('pegote'),
  ('settings'), ('catalog'), ('categories'), ('upgrade')
ON CONFLICT DO NOTHING;
-- Note: 'maalca', 'the-little-dominican' are reserved here so onboarding
-- can't reuse them — they're claimed by the migration script.
-- Note: 'settings', 'catalog', 'categories', 'upgrade' match sub-routes of
-- /space/[slug] added in MODEL_PLAN v2 (see 003_reserved_slugs.sql).

-- -----------------------------------------------------------------------------
-- 6. RLS policies
-- -----------------------------------------------------------------------------
ALTER TABLE businesses ENABLE ROW LEVEL SECURITY;
ALTER TABLE products ENABLE ROW LEVEL SECURITY;
ALTER TABLE onboarding_progress ENABLE ROW LEVEL SECURITY;
ALTER TABLE analytics_events ENABLE ROW LEVEL SECURITY;
ALTER TABLE reserved_slugs ENABLE ROW LEVEL SECURITY;

-- businesses: owner full access; public can read published
DROP POLICY IF EXISTS "businesses_owner_all" ON businesses;
CREATE POLICY "businesses_owner_all" ON businesses
  FOR ALL USING (auth.uid() = owner_id) WITH CHECK (auth.uid() = owner_id);

DROP POLICY IF EXISTS "businesses_public_read" ON businesses;
CREATE POLICY "businesses_public_read" ON businesses
  FOR SELECT USING (published = true);

-- products: owner full access via business; public read if business published & product active
DROP POLICY IF EXISTS "products_owner_all" ON products;
CREATE POLICY "products_owner_all" ON products
  FOR ALL USING (
    EXISTS (SELECT 1 FROM businesses b WHERE b.id = products.business_id AND b.owner_id = auth.uid())
  ) WITH CHECK (
    EXISTS (SELECT 1 FROM businesses b WHERE b.id = products.business_id AND b.owner_id = auth.uid())
  );

DROP POLICY IF EXISTS "products_public_read" ON products;
CREATE POLICY "products_public_read" ON products
  FOR SELECT USING (
    active = true AND
    EXISTS (SELECT 1 FROM businesses b WHERE b.id = products.business_id AND b.published = true)
  );

-- onboarding_progress: owner only
DROP POLICY IF EXISTS "onboarding_owner_all" ON onboarding_progress;
CREATE POLICY "onboarding_owner_all" ON onboarding_progress
  FOR ALL USING (
    EXISTS (SELECT 1 FROM businesses b WHERE b.id = onboarding_progress.business_id AND b.owner_id = auth.uid())
  ) WITH CHECK (
    EXISTS (SELECT 1 FROM businesses b WHERE b.id = onboarding_progress.business_id AND b.owner_id = auth.uid())
  );

-- analytics_events: insert from authenticated, owner reads own business events; service_role bypasses RLS
DROP POLICY IF EXISTS "analytics_authenticated_insert" ON analytics_events;
CREATE POLICY "analytics_authenticated_insert" ON analytics_events
  FOR INSERT TO authenticated WITH CHECK (auth.uid() = user_id OR user_id IS NULL);

DROP POLICY IF EXISTS "analytics_owner_read" ON analytics_events;
CREATE POLICY "analytics_owner_read" ON analytics_events
  FOR SELECT USING (
    user_id = auth.uid() OR
    EXISTS (SELECT 1 FROM businesses b WHERE b.id = analytics_events.business_id AND b.owner_id = auth.uid())
  );

-- reserved_slugs: read-only for everyone
DROP POLICY IF EXISTS "reserved_slugs_read" ON reserved_slugs;
CREATE POLICY "reserved_slugs_read" ON reserved_slugs FOR SELECT USING (true);

-- -----------------------------------------------------------------------------
-- 7. Trigger: auto-update updated_at
-- -----------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION trigger_set_updated_at()
RETURNS TRIGGER AS $$
BEGIN
  NEW.updated_at = NOW();
  RETURN NEW;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS set_updated_at ON businesses;
CREATE TRIGGER set_updated_at BEFORE UPDATE ON businesses
  FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

DROP TRIGGER IF EXISTS set_updated_at ON products;
CREATE TRIGGER set_updated_at BEFORE UPDATE ON products
  FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

DROP TRIGGER IF EXISTS set_updated_at ON onboarding_progress;
CREATE TRIGGER set_updated_at BEFORE UPDATE ON onboarding_progress
  FOR EACH ROW EXECUTE FUNCTION trigger_set_updated_at();

-- =============================================================================
-- End of migration 001
-- Verify with:
--   SELECT table_name FROM information_schema.tables WHERE table_schema='public';
--   SELECT slug FROM reserved_slugs ORDER BY slug;
-- =============================================================================
