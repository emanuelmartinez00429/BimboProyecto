## Qué cambia

<!-- Descripción clara del cambio y comportamiento que queda funcionando. -->

## Checklist DevSecOps

- [ ] **Tamaño del PR:** Unas 2000 líneas de código o menos (tope 2500; el CI lo revisa automáticamente).
- [ ] **Sin secretos:** Sin contraseñas, tokens JWT privados, `service_role`, `sb_secret_`, ni rutas absolutas de disco o datos personales en el código o notas.
- [ ] **Seguridad de Base de Datos:** RLS revisado y verificado si toca migraciones (`supabase/migrations/` o `auditoria/migrations/`).
- [ ] **Documentación y Contexto:** `contexto/` actualizado (nota de sesión, ADR o deuda técnica) o justificación explícita abajo (`sin-doc: <motivo>`).
- [ ] **Verificador en verde:** `node scripts/contexto/verificar.js` ejecutado con 0 errores.
- [ ] **Build y Tests:** `dotnet build BimboProyecto.sln -c Release` con 0 errores y 0 advertencias, y `dotnet test` pasando con skips explícitos.

<!-- Si el PR toca código sin tocar contexto/, explicar por qué:
sin-doc: <motivo justificado>
-->
