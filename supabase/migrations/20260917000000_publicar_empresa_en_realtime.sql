-- ============================================================================
-- Migración: 20260917000000_publicar_empresa_en_realtime.sql
-- Propósito:
--   Publicar tabla empresa en supabase_realtime para habilitar eventos WAL remotos
-- ============================================================================

-- Publicar tabla empresa en supabase_realtime para habilitar eventos WAL remotos
ALTER PUBLICATION supabase_realtime ADD TABLE public.empresa;
