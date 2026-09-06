-- ============================================================================
-- Migración: 20260906170000_publicar_roles_en_realtime.sql
-- Propósito:
--   InvalidadorCacheRealtime.cs (commit a05f006) agregó una suscripción a la
--   tabla "roles" para purgar la caché de RolRepository (TagsCache.TablaRoles)
--   cuando otra sesión edita un rol, pero "roles" nunca se agregó a la
--   publicación supabase_realtime — la suscripción se abre y nunca recibe
--   ningún evento: exactamente el mismo modo de falla silenciosa que P-034
--   documentó para fabricante/proveedores/presentacion_producto/tara en su
--   momento (ver Sesión 2026-08-14 - Realtime en columnas de join de
--   Productos).
--
--   RLS de "roles" ya es "USING (true)" para authenticated (roles_select_
--   authenticated), así que publicarla no expone nada que un usuario
--   autenticado no pudiera leer ya por SELECT directo.
-- ============================================================================

ALTER PUBLICATION supabase_realtime ADD TABLE public.roles;
