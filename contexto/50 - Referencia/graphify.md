---
title: "graphify — Grafo local de dependencias y consultas"
type: referencia
status: vigente
date: 2026-11-09
updated: 2026-11-09
autor: fernando
summary: "Uso y arquitectura de graphify en BimboProyecto: extracción de AST C# y XAML sin LLM, consultas previas a lectura y exclusión de graphify-out del repositorio."
tags:
  - graphify
  - ast
  - tooling
  - navegacion
---

# graphify — Grafo Local de Dependencias y Consultas

> **Propósito:** Proporcionar un mapa estructural navegable del código fuente (.NET 10 / WPF / XAML) para humanos y agentes de IA, reduciendo el consumo de tokens y evitando la lectura indiscriminada de archivos en frío.

---

## 1. Qué es y Cómo Opera en BimboProyecto

`graphify` es una herramienta CLI de indexación y consulta estructural que opera localmente sobre el repositorio:

- **Extracción de AST determinista:** Analiza la sintaxis de clases C#, interfaces, dependencias e instanciaciones XAML mediante árboles de sintaxis abstracta (AST).
- **Cero llamadas a LLMs:** Opera sin necesidad de backend de inteligencia artificial (`GRAPHIFY_BACKEND=none`), sin consumo de cuota ni transmisión de código a APIs externas.
- **Índice local:** Genera su base de conocimiento en la carpeta local `graphify-out/` (principalmente `graphify-out/graph.json`), la cual **nunca se versiona** en Git.

---

## 2. Regla de Oro de Consulta Previa

Como se estipula en `AGENTS.md` (sección *Grafo del repo (graphify)*):

```text
Para preguntas sobre el código: graphify query "<pregunta>" antes de abrir archivos;
lo que diga el grafo se comprueba en el archivo antes de afirmarlo.
```

Esta directiva obliga a los agentes de IA a consultar el grafo antes de ejecutar recorridos costosos o inspeccionar múltiples archivos del árbol de código.

---

## 3. Comandos Habituales

| Comando | Función |
| :-- | :-- |
| `graphify build .` | Construye o actualiza el grafo local de dependencias en `graphify-out/`. |
| `graphify query "<pregunta>"` | Consulta relaciones de tipos, dependencias, métodos y referencias cruzadas. |
| `graphify hook install` | Instala los hooks de Git (`post-commit`, `post-merge`) para reconstrucción automática local. |

---

## 4. Gobernanza en Git

1. **Exclusiones (`.graphifyignore`):**
   Excluye directorios de compilación (`bin/`, `obj/`), análisis temporal (`analisis-muerto/`), metadatos de agentes (`.agents/`), índices regenerables (`contexto/INDEX*.md`, `contexto/.control/`) y archivos de configuración auxiliares.
2. **Ignorado de salida (`.gitignore`):**
   `graphify-out/` está ignorado en `.gitignore` para evitar que índices binarios o JSON masivos se versionen en el historial.
3. **Merge driver (`.gitattributes`):**
   `graphify-out/graph.json merge=graphify` está registrado en `.gitattributes` para que, en caso de generación concurrente local, Git utilice el merge driver correspondiente sin generar conflictos de fusión.
4. **Hooks opcionales:**
   La ejecución de `graphify hook install` es potestad individual de cada desarrollador o integrador en su máquina de trabajo; no es obligatoria para la ejecución del CI.

---

## 5. Relaciones

- [[AGENTS]] — Contrato único de agentes y regla de consulta previa
- [[E-01 - Estándar DevSecOps]] — Capa de código y gobierno de herramientas
- [[Convenciones de código]] — Arquitectura por capas de la solución
