-- =============================================================================
-- Migration 003 — Reserve new internal route slugs
-- Reason: /space/[slug] layout adds sub-routes (settings, catalog, categories,
--         upgrade) that weren't reserved in 001. A business with one of these
--         slugs would shadow the internal routes.
-- Safe to run multiple times (ON CONFLICT DO NOTHING).
-- =============================================================================

INSERT INTO reserved_slugs (slug) VALUES
  ('settings'),
  ('catalog'),
  ('categories'),
  ('upgrade')
ON CONFLICT DO NOTHING;
