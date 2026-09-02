-- El cliente obtiene catálogo y detalle únicamente por RPC.
revoke select on public.tipos_notificacion, public.notificaciones from authenticated;

-- notificaciones_usuario conserva SELECT porque Postgres Changes debe evaluar RLS.
create index if not exists notificaciones_modulo_idx on public.notificaciones(id_modulo);
create index if not exists notificaciones_accion_idx on public.notificaciones(id_accion);
create index if not exists notificaciones_actor_idx on public.notificaciones(id_usuario_actor);
create index if not exists notificaciones_usuario_resolucion_idx
  on public.notificaciones(id_usuario_resolucion)
  where id_usuario_resolucion is not null;
