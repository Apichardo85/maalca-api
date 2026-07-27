-- =============================================================================
-- MaalCa SaaS — Migration 002: existing affiliates → businesses
-- Run AFTER 001 and AFTER verifying users exist in auth.users
-- =============================================================================

-- MaalCa (admin)
INSERT INTO businesses (
  owner_id, slug, name, business_type, plan, plan_status, published,
  description, primary_color
)
SELECT
  u.id,
  'maalca',
  'MaalCa',
  'service',
  'entrepreneur',
  'active',
  true,
  'Plataforma de negocios locales',
  '#C8102E'
FROM auth.users u
WHERE u.email = 'alejandropichardo85@gmail.com'
ON CONFLICT (slug) DO UPDATE
  SET plan = 'entrepreneur', plan_status = 'active', published = true;

INSERT INTO onboarding_progress (business_id, first_product_added, whatsapp_configured, link_shared, completed_at)
SELECT id, true, true, true, NOW() FROM businesses WHERE slug = 'maalca'
ON CONFLICT DO NOTHING;

-- The Little Dominican Restaurant
INSERT INTO businesses (
  owner_id, slug, name, business_type, plan, plan_status, published,
  description, primary_color
)
SELECT
  u.id,
  'the-little-dominican',
  'The Little Dominican',
  'restaurant',
  'entrepreneur',
  'active',
  true,
  'Auténtica comida dominicana',
  '#C8102E'
FROM auth.users u
WHERE u.email = 'littledominicanarestaurant@gmail.com'
ON CONFLICT (slug) DO UPDATE
  SET plan = 'entrepreneur', plan_status = 'active', published = true;

INSERT INTO onboarding_progress (business_id, first_product_added, whatsapp_configured, link_shared, completed_at)
SELECT id, true, true, true, NOW() FROM businesses WHERE slug = 'the-little-dominican'
ON CONFLICT DO NOTHING;

-- Add Pegote here when you have the email + business_type confirmed
-- INSERT INTO businesses (...) SELECT ... WHERE u.email = 'pegote@...' ...;

-- =============================================================================
-- Verify with:
--   SELECT slug, name, plan, business_type FROM businesses WHERE plan = 'entrepreneur';
-- =============================================================================
