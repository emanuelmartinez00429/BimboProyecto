-- Permitir que el rol anon verifique la existencia de columnas en los filtros de Supabase Realtime.
-- La seguridad de los datos se mantiene intacta mediante Row Level Security (RLS),
-- ya que las políticas 'to authenticated' impiden que anon lea cualquier registro.
GRANT SELECT ON public.notificaciones_usuario TO anon;
GRANT SELECT ON public.notificaciones TO anon;
GRANT SELECT ON public.tipos_notificacion TO anon;
