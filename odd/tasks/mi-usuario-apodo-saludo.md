# Feature: Apodo personal para el saludo del menú principal (Mi Usuario)

## Objetivo
Botón/campo dentro de Mi Usuario para que cada usuario elija un apodo ("Fernando" → "Paco")
que reemplaza su nombre en el saludo "Bienvenido, X" de la pantalla principal.

## Alcance autorizado
- NUEVO: guardar el apodo como preferencia personal (`usuario_preferencias`, clave `apodo`,
  ámbito `global`). No requiere migración: el CHECK es formato, no whitelist.
- NO toca datos de empleados (nombreEmpleado/apellidoEmpleado quedan intactos): decisión
  del usuario 2026-09-20 — apodo personal, no dato RRHH.
- Al guardar (o vaciar) el apodo se refresca el perfil para que el saludo cambie al
  navegar de nuevo al menú principal.

## Tareas
- [x] T1 — ClavesPreferencia.Apodo (clave "apodo") — CapaAplicacion4/Preferencias/ClavesPreferencia.cs
- [x] T2 — PerfilUsuario.Apodo (propiedad) + PerfilUsuarioService: leer preferencia en
      CargarAsync y overridear NombreCompleto con el apodo — CapaDominio/Perfil, CapaDatos/Perfil
- [x] T3 — MiUsuarioViewModel: ApodoEditable + CargarApodoAsync + GuardarApodoCommand
      (empty = restablecer nombre real) — CapaUI .../MiUsuario
- [x] T4 — MiUsuarioView.xaml: editor de apodo en la tarjeta Perfil con preview
      "Bienvenido, X"
- [x] T5 — Build 0 errores / 0 warnings
- [ ] T6 — Prueba visual del usuario (arranca app, cambia apodo, vuelve al menú principal)
- [x] T8 — Cambio de contraseña real vía re-autenticación + Auth.Update (ver registro abajo)

## Registro de rutas y evidencia
- 2026-09-20 — T8 (nuevo): cambio de contraseña REAL desde Mi Usuario. Análisis: OTP (recuperación) vs re-autenticación con contraseña actual + Auth.Update sobre la sesión real (la OTP es para quien NO sabe la contraseña; aquí el usuario ya está autenticado). Decisión: re-autenticación (SignInWithPassword, mismo método de login) → Auth.Update (misma primitiva de la recuperación) SIN SignOut. Nuevos ICambioPropiaPasswordService/CambioPropiaPasswordService + DI; VM stub → comando real con CambiandoPassword (doble envío bloqueado). Build 0/0 + arné OK. Sin commit: prueba del usuario pendiente.
- Ruta elegida: delegado directo → inline (dispatcher de subagentes caído 2×, mismo defecto de runtime "json: unknown field __managed_by").
- Ejecutado inline el 2026-09-20 por el orquestador con verificación build.
- T5 VERIFICADO: `dotnet build BimboProyecto.sln` → "Compilación correcta. 0 Advertencia(s), 0 Errores" (2026-09-20).
- Cambios SIN COMMIT (pendiente prueba visual del usuario y aprobación).
- Archivos tocados: ClavesPreferencia.cs, PerfilUsuario.cs, PerfilUsuarioService.cs, MiUsuarioViewModel.cs, MiUsuarioView.xaml (+ los 3 del rediseño del botón).

## Historial
- 2026-09-20 — creado tras aprobación del usuario del enfoque "apodo personal".
- 2026-09-20 — T7 (nuevo, pedido del usuario): reestructuración visual integral de MiUsuarioView al lenguaje del sistema: EncabezadoCatalogo (componente compartido, icono IcoMiUsuario), tarjetas radio 10 con sombra desacoplada (AP-06), banner compartido MiUsuarioBanner, bloques alineados de Seguridad (actual/confirmar a la izquierda, nueva + medidor + checklist a la derecha), Padding/etiquetas unificadas. Build 0/0 + arné de instanciación OK (978x900). Sin commit: prueba visual pendiente.
