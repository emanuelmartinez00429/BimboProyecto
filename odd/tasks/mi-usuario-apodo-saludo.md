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

## Registro de rutas y evidencia
- Ruta elegida: delegado directo → inline (dispatcher de subagentes caído 2×, mismo defecto de runtime "json: unknown field __managed_by").
- Ejecutado inline el 2026-09-20 por el orquestador con verificación build.
- T5 VERIFICADO: `dotnet build BimboProyecto.sln` → "Compilación correcta. 0 Advertencia(s), 0 Errores" (2026-09-20).
- Cambios SIN COMMIT (pendiente prueba visual del usuario y aprobación).
- Archivos tocados: ClavesPreferencia.cs, PerfilUsuario.cs, PerfilUsuarioService.cs, MiUsuarioViewModel.cs, MiUsuarioView.xaml (+ los 3 del rediseño del botón).

## Historial
- 2026-09-20 — creado tras aprobación del usuario del enfoque "apodo personal".
