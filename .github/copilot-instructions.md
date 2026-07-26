Este proyecto usa `AGENTS.md` en la raíz como fuente única de instrucciones para agentes.

Antes de sugerir o modificar código, seguí `../AGENTS.md`:
- Reglas de oro: solo trabajar en `CapaUI`, nunca en `BimboPesaje`; `CapaAplicacion` nunca referencia `CapaDatos`; MVVM con CommunityToolkit (`[ObservableProperty]`/`[RelayCommand]`); repos con `RepositorioBase` + `TryAsync` + `Result`.
- Build de verificación: `dotnet build BimboProyecto.sln` (debe dar 0 errores).
- Arquitectura vigente: `contexto/40 - Proyecto Bimbo/Arquitectura Actual.md`.
- Para documentar cambios en la bóveda: `contexto/AGENTS.md`.

- @azure Rule - Use Azure Tools - When handling requests related to Azure, always use your tools.
- @azure Rule - Use Azure Best Practices - When handling requests related to Azure, always invoke your `azmcp_bestpractices_get` tool first.
- @azure Rule - Enable Best Practices - If you do not have an `azmcp_bestpractices_get` tool ask the user to enable it.
