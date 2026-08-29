---
title: MCP Obsidian — Configuración Completa
type: referencia
status: vigente
tags:
  - setup
  - mcp
  - obsidian
  - claude-code
date: 2026-06-06
updated: 2026-06-06
summary: "Guía paso a paso para conectar un vault de Obsidian a Claude Code vía MCP (Model Context Protocol), basada en la experiencia real de configuración con los…"
scope: []
symbols: []
---

# MCP Obsidian — Configuración Completa

Guía paso a paso para conectar un vault de Obsidian a Claude Code vía MCP (Model Context Protocol), basada en la experiencia real de configuración con los errores que surgieron.

---

## Objetivo

Que Claude Code pueda leer y escribir archivos del vault de Obsidian directamente en cada sesión, sin necesidad de indicar rutas manualmente.

---

## Solución final que funcionó

**No se necesita ningún plugin de Obsidian.** El vault son archivos `.md` en disco, así que basta con el MCP oficial de filesystem de Anthropic.

### Paso 1 — Editar `.claude.json` directamente

El archivo está en `C:\Users\<tuUsuario>\.claude.json` (no en `settings.json`).

Buscar la línea al final del archivo:
```json
"mcpServers": {}
```

Reemplazarla por:
```json
"mcpServers": {
  "obsidian": {
    "type": "stdio",
    "command": "cmd",
    "args": ["/c", "npx", "-y", "@modelcontextprotocol/server-filesystem", "C:\\Users\\fbara\\OneDrive\\Desktop\\Proyecto de BIMBO\\ProyectoBimboContexto"],
    "env": {}
  }
}
```

> **Adaptar la ruta** al path real del vault en tu máquina.

### Paso 2 — Verificar

Ejecutar en terminal dentro de Claude Code:
```
claude mcp list
```

Debe aparecer:
```
obsidian: cmd /c npx ... ✓ Connected
```

### Paso 3 — Reiniciar Claude Code

Cerrar y abrir con `claude --continue` para retomar la conversación anterior.

---

## Requisitos

- **Node.js** instalado (verificar con `node --version`)
- **npx** disponible (viene con Node.js)
- El paquete `@modelcontextprotocol/server-filesystem` se descarga automáticamente la primera vez

---

## Errores que surgieron y soluciones

### Error 1 — Plugin inexistente en Obsidian
**Síntoma:** Al buscar `obsidian-claude-code-mcp` en Community Plugins de Obsidian, no aparece.  
**Causa:** El plugin `obsidian-claude-code-mcp` de iansinnott no está publicado en el catálogo oficial de Obsidian.  
**Solución:** Usar el MCP de filesystem directamente, sin plugin de Obsidian.

---

### Error 2 — MCP configurado en `settings.json` no funciona
**Síntoma:** Se agrega `mcpServers` en `C:\Users\<user>\.claude\settings.json` pero Claude Code no lo carga.  
**Causa:** Claude Code CLI guarda la configuración de MCP en `.claude.json` (en la raíz del home), **no** en `settings.json`.  
**Solución:** Editar directamente `C:\Users\<user>\.claude.json`.

---

### Error 3 — `claude mcp add` con args que incluyen `/c`
**Síntoma:** Al ejecutar:
```
claude mcp add obsidian -s user -- cmd /c npx -y ...
```
El argumento `/c` se interpreta como path en lugar de flag.  
**Resultado incorrecto:** `cmd C:/ npx -y ...`  
**Solución:** No usar `claude mcp add` para este caso. Editar `.claude.json` directamente.

---

### Error 4 — Servidor registrado incompleto
**Síntoma:** `claude mcp list` muestra `obsidian: cmd /c npx -y — ✗ Failed to connect` (sin el paquete ni la ruta).  
**Causa:** El comando `claude mcp add` corrido en PowerShell desde dentro de Claude Code fragmentó los argumentos.  
**Solución:**
```
claude mcp remove obsidian -s user
```
Luego editar `.claude.json` manualmente con la configuración completa.

---

### Error 5 — Ruta con espacios en PowerShell
**Síntoma:** La ruta `C:\Users\fbara\OneDrive\Desktop\Proyecto de BIMBO\...` causa errores en PowerShell porque el espacio en "Proyecto de BIMBO" se interpreta como argumentos separados.  
**Solución:** Editar el JSON directamente evita este problema por completo.

---

## Estructura del vault accesible

Una vez conectado, Claude Code puede usar las herramientas MCP:

| Herramienta | Uso |
|---|---|
| `list_directory` | Ver contenido de carpetas |
| `read_file` | Leer notas |
| `write_file` | Crear/modificar notas |
| `create_directory` | Crear carpetas |
| `search_files` | Buscar por contenido |

---

## Notas adicionales

- El MCP se registra a nivel **global** (user scope), disponible en todos los proyectos
- La primera vez que inicia, `npx` descarga el paquete automáticamente (~pocos segundos)
- El vault de Obsidian no necesita estar abierto para que el MCP funcione
- Para verificar en cualquier momento: `claude mcp list`

---

*Relacionado: [[Conocimiento Principal]] · [[Arquitectura Actual]]*
