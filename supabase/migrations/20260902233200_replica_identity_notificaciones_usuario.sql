-- Configurar REPLICA IDENTITY FULL para notificaciones_usuario
-- Necesario para permitir filtros por columnas no-PK (id_usuario) en Supabase Realtime (postgres_changes)
ALTER TABLE public.notificaciones_usuario REPLICA IDENTITY FULL;
