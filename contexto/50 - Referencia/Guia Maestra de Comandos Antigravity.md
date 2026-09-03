---
title: "Guía Maestra de Comandos de Antigravity (Slash Commands)"
tags:
  - referencia
  - antigravity
  - orquestacion
  - comandos
  - desarrollo
date: 2026-09-02
---

# Guía Maestra de Comandos de Acción (Slash Commands)

> **Propósito:** Esta guía documenta los **8 modos de orquestación y comandos de acción (`/`)** disponibles en el entorno de desarrollo Antigravity. A diferencia de las *Skills* (que son bibliotecas pasivas de consulta), los **Slash Commands** son interruptores que transforman la forma en que el agente piensa, colabora, investiga y ejecuta código en el proyecto Bimbo Honduras.

---

## 📌 Diferencia Fundamental: Slash Commands vs. Skills vs. MCP

| Concepto | Qué es | Ícono en menú | Ejemplo típico |
|---|---|:---:|---|
| **Slash Commands** | Modos de trabajo activos y orquestación del agente. | `[ / ]` o terminal | `/teamwork-preview`, `/goal`, `/grill-me` |
| **Skills** | Manuales de instrucciones y scripts de herramientas empaquetados. | `[ < > ]` | `accidental-data-loss-prevention`, `android-cli` |
| **MCP Servers** | Pasarelas de comunicación externa con servicios locales o remotos. | Red / Enchufe | `supabase`, `engram` |

---

## 1. `/btw` — By The Way (Pregunta Tangencial sin Distracción)

### Definición
Permite hacer una pregunta rápida o pedir una comprobación puntual **sin interrumpir el hilo principal** de trabajo, sin resetear el contexto acumulado y sin que el agente se distraiga editando archivos por error.

> [!NOTE]
> **Analogía de la vida real:**  
> Imagina que estás en una obra supervisando a un maestro albañil que levanta una pared. No quieres que suelte la pala ni el nivel; simplemente te acercas y le preguntas al oído: *"Oye, por cierto, ¿recuerdas de qué grosor compramos las varillas?"*. El maestro te responde en 2 segundos y sigue pegando ladrillos sin detener su ritmo.

### Casos de Uso en BimboProyecto
* **Consultar constantes del sistema:**
  > `/btw ¿cuál era el id_estado para 'Inactivo' en la base de datos?`  
  *Efecto:* Responde `id_estado = 2` y sigue con la tarea activa sin tocar código.
* **Verificar nombres de vistas o tablas:**
  > `/btw ¿cómo se llamaba la vista SQL que aplana usuarios y roles para la búsqueda?`  
  *Efecto:* Responde `vista_usuarios_busqueda` al instante.
* **Aclarar dudas de diseño:**
  > `/btw ¿por qué usamos Result<T> en lugar de lanzar excepciones en los repositorios?`  
  *Efecto:* Resume en 2 líneas el patrón sin salirse de la tarea.

| ✅ Cuándo SÍ usarlo | ❌ Cuándo NO usarlo |
|---|---|
| Dudas rápidas de constantes, rutas o nombres. | Pedir cambios de código o refactors. |
| Consultas de curiosidad mientras el agente trabaja. | Reportar un bug que requiera investigar archivos. |

---

## 2. `/goal` — Meta Autónoma Continua (Ejecución Llave en Mano)

### Definición
Le asigna al agente un objetivo integral de principio a fin, dándole autonomía para investigar, codificar, compilar, diagnosticar errores, corregir y verificar sin detenerse a pedir confirmación en cada micro-paso hasta que la meta esté 100% terminada.

> [!NOTE]
> **Analogía de la vida real:**  
> Es como contratar a un contratista general bajo la modalidad "llave en mano": le dices *"construye el garaje"*. No te va a llamar a las 3 de la madrugada para preguntarte si puede mezclar la bolsa de cemento; él compra el material, hace la mezcla, corrige si una tabla se desalinea y solo te llama cuando el garaje tiene la llave puesta y la luz encendida.

### Casos de Uso en BimboProyecto
* **Migraciones completas de formularios:**
  > `/goal Migra los 5 modales restantes al ValidadorFormulario, quita los MaxLength estáticos de los XAML, compila y asegúrate de que pasen todos los tests.`  
  *Efecto:* Investiga los XAML, edita archivos, corre `dotnet build`, repara errores y concluye cuando todo está en verde.
* **Saneamiento de deuda técnica:**
  > `/goal Resuelve el ítem de deuda P-042 eliminando la dependencia legacy de Pesaje y documenta el resultado.`  
  *Efecto:* Rastrea dependencias, refactoriza y actualiza la bitácora automáticamente.

| ✅ Cuándo SÍ usarlo | ❌ Cuándo NO usarlo |
|---|---|
| Tareas largas y bien delimitadas (refactors, migraciones). | Tareas con requisitos ambiguos donde no sabes cómo quieres la UI. |
| Cuando quieres dejar al agente trabajando de fondo. | Cuando quieres aprobar cada color o espaciado de un botón. |

---

## 3. `/schedule` — Programador y Temporizador Asíncrono

### Definición
Programa una instrucción o recordatorio para ejecutarse después de un tiempo específico (timer de una sola vez) o de forma periódica/recurrente (cron job) en segundo plano.

> [!NOTE]
> **Analogía de la vida real:**  
> Es como poner una alarma en tu teléfono o contratar un vigilante que revise la entrada cada 30 minutos. Tú te vas a tomar un café o a otra reunión, y el sistema te notificará exactamente cuando el temporizador venza.

### Casos de Uso en BimboProyecto
* **Monitoreo de tareas pesadas:**
  > `/schedule En 10 minutos revisa el log de compilación y si terminó avísame.`  
  *Efecto:* Pone un timer interno; al cumplirse los 10 minutos se despierta, lee el log y te da el veredicto.
* **Revisiones periódicas de salud:**
  > `/schedule Cada 30 minutos revisa si aparecieron errores nuevos en Supabase.`  
  *Efecto:* Crea un cron que monitorea la telemetría periódicamente.

| ✅ Cuándo SÍ usarlo | ❌ Cuándo NO usarlo |
|---|---|
| Esperar procesos largos sin bloquearte la conversación. | Comandos que esperas que respondan de inmediato. |
| Recordatorios programados o sondeos recurrentes. | Tareas de edición de código directa. |

---

## 4. `/browser` — Navegador Web en Vivo

### Definición
Invoca a un subagente especializado con un navegador Chromium real integrado, capaz de acceder a páginas web, desplazarse, hacer clics, leer documentación técnica en línea e inspeccionar repositorios o incidencias en GitHub.

> [!NOTE]
> **Analogía de la vida real:**  
> Es como mandar a tu asistente a la biblioteca pública a buscar un libro técnico que tú no tienes en tu estante, tomarle fotos a la página con el diagrama que necesitas y traértelo traducido a tu mesa.

### Casos de Uso en BimboProyecto
* **Consultar cambios en SDKs externos:**
  > `/browser Entra a la documentación de supabase-csharp v1.4 y revisa cómo se configura el Timeout en postgrest.`  
  *Efecto:* Abre GitHub o la web oficial, extrae la signatura exacta del método y te muestra el snippet funcional.
* **Investigar errores de dependencias:**
  > `/browser Busca en los issues de CommunityToolkit.Mvvm por qué el generador de código omite métodos parciales en .NET 8.`  
  *Efecto:* Localiza el issue exacto y te explica la solución recomendada por la comunidad.

| ✅ Cuándo SÍ usarlo | ❌ Cuándo NO usarlo |
|---|---|
| Consultar documentación viva en internet o APIs de terceros. | Leer código del proyecto (para eso el agente usa lectura local directa). |
| Investigar bugs o CVEs recientes en GitHub / StackOverflow. | Tareas que no requieran conexión a internet. |

---

## 5. `/grill-me` — Entrevístame (Alineación Previa de Requisitos)

### Definición
Invierte la dinámica: en lugar de que tú des órdenes y el agente programe asumiendo supuestos, **el agente se convierte en un entrevistador técnico inquisitivo** que te hace preguntas profundas sobre diseño, flujos, casos borde y reglas de negocio antes de tocar el código.

> [!NOTE]
> **Analogía de la vida real:**  
> Es como ir al sastre antes de confeccionar un traje a medida. No quieres que el sastre corte la tela de inmediato; quieres que tome la cinta métrica, te pregunte por la ocasión, la caída de la tela, el tipo de botones y la comodidad del cuello hasta que el diseño sea milimétrico.

### Casos de Uso en BimboProyecto
* **Diseño de un nuevo módulo o pantalla:**
  > `/grill-me Quiero agregar un módulo de conciliación de pesajes vs tickets de báscula.`  
  *Efecto:* Te preguntará: ¿Quién tiene permiso? ¿Qué tolerancia de peso se permite (ej. 1%)? ¿Se genera reporte automático? ¿Qué pasa con pesajes anulados?
* **Reglas de seguridad y permisos:**
  > `/grill-me Vamos a diseñar una política de expiración de sesiones para usuarios de báscula.`  
  *Efecto:* Te interroga sobre tiempos de inactividad, bloqueo de pantalla y roles excluidos.

| ✅ Cuándo SÍ usarlo | ❌ Cuándo NO usarlo |
|---|---|
| Antes de empezar una pantalla o feature compleja. | Para arreglar un error de sintaxis evidente. |
| Cuando la idea está 'verde' y necesitas aterrizar el alcance. | Cuando ya tienes una especificación milimétrica cerrada. |

---

## 6. `/teamwork-preview` — Equipo de Agentes en Cuadrilla

### Definición
Despliega un equipo completo de agentes autónomos especializados trabajando simultáneamente en paralelo: un líder/orquestador, auditores de arquitectura, programadores frontend/backend, revisores de código y testers.

> [!NOTE]
> **Analogía de la vida real:**  
> En lugar de contratar a una sola persona para que haga los planos, instale los tubos, tire el cableado y pinte las paredes, contratas a una cuadrilla completa de especialistas coordinados por un jefe de obra que supervisa que cada uno cumpla su estándar sin estorbarse.

### Casos de Uso en BimboProyecto
* **Refactors multidimensionales a gran escala:**
  > `/teamwork-preview Implementa el plan completo de validación de longitud máxima en las 4 capas del sistema.`  
  *Efecto:* Un agente audita la BD, dos editan los modales XAML/code-behinds, otro repara GhostTextBox y otro escribe los tests unitarios y la documentación en la bóveda (exactamente lo que hicimos hoy con éxito).
* **Migraciones masivas de infraestructura:**
  > `/teamwork-preview Migrar todos los repositorios CRUD hacia ResultPattern y RepositorioBase.`  
  *Efecto:* Reparte los 15 repositorios entre varios trabajadores en paralelo y los prueba simultáneamente.

| ✅ Cuándo SÍ usarlo | ❌ Cuándo NO usarlo |
|---|---|
| Proyectos masivos que tocan 10 o más archivos en varias capas. | Modificar un solo archivo o una función pequeña. |
| Tareas con fases paralelas claras (UI, Datos, Tests, Docs). | Cuando una sola respuesta concisa es suficiente. |

---

## 7. `/learn` — Aprender y Fijar Reglas en Piedra

### Definición
Le ordena al agente analizar la conversación reciente, extraer las correcciones, preferencias o directrices que le diste, y consolidarlas en archivos de reglas formales ([[reglas-bimbo]]) o en la memoria permanente para que nunca más se le olviden.

> [!NOTE]
> **Analogía de la vida real:**  
> Es como el manual de operaciones de una fábrica: cada vez que una máquina falla o los operarios descubren una mejor manera de empaquetar el producto, no se lo guardan en la memoria; lo redactan, lo imprimen y lo cuelgan en la pared para que ningún turno vuelva a cometer el mismo error.

### Casos de Uso en BimboProyecto
* **Fijar preferencias visuales:**
  > `/learn Guarda la regla de que en todos los formularios los ComboBox deben ser blancos y con IsTextSearchEnabled=True.`  
  *Efecto:* Genera o actualiza el archivo de directrices para futuros agentes.
* **Aprender de una corrección técnica:**
  > `/learn Recuerda que en este proyecto el límite de contraseñas de Bcrypt son 72 caracteres y siempre debemos verificar 0 errores y 0 warnings.`  
  *Efecto:* Persiste la regla en `.agents/rules/reglas-bimbo.md` y en Engram.

| ✅ Cuándo SÍ usarlo | ❌ Cuándo NO usarlo |
|---|---|
| Justo después de corregir al agente en una convención importante. | Si la instrucción fue un experimento temporal que no quieres repetir. |
| Para estandarizar patrones descubiertos en la sesión. | Para tareas operativas de codificación directa. |

---

## 8. `/boost` — Modo Boost (Máxima Potencia y Razonamiento)

### Definición
Activa un orquestador de máxima capacidad cognitiva enfocado en pensamiento profundo, verificación cruzada estricta y eliminación radical de suposiciones erróneas para problemas de alta complejidad.

> [!NOTE]
> **Analogía de la vida real:**  
> Es como convocar a una junta médica de especialistas antes de una cirugía a corazón abierto. Ningún cirujano actúa por impulso; cada uno revisa los análisis del otro, discuten posibles complicaciones y planifican cada milímetro antes de hacer la primera incisión.

### Casos de Uso en BimboProyecto
* **Bugs fantasmas o de concurrencia:**
  > `/boost La aplicación se congela intermitentemente al guardar pesajes simultáneos desde dos básculas a la vez.`  
  *Efecto:* El agente analiza semáforos, bloqueos de hilos en WPF (Dispatcher), transacciones y sockets a nivel microscópico.
* **Auditorías críticas de seguridad y RBAC:**
  > `/boost Audita todo el flujo de autenticación, JWT, roles inmutables y sesiones activas buscando cualquier vector de elevación de privilegios.`  
  *Efecto:* Análisis exhaustivo de cobertura de seguridad.

| ✅ Cuándo SÍ usarlo | ❌ Cuándo NO usarlo |
|---|---|
| Errores intermitentes o problemas de concurrencia complejos. | Consultas simples o preguntas rápidas de sintaxis. |
| Seguridad sensible y decisiones de arquitectura estructurales. | Maquetación visual rutinaria de botones o márgenes. |

---

## 📊 Matriz Comparativa de Elección Rápida

| Comando | Nivel de Autonomía | Velocidad | Mejor Para... |
|---|:---:|:---:|---|
| **/btw** | Cero (Informativo) | ⚡ Instantáneo | Consultas puntuales sin mover al agente de su tarea. |
| **/goal** | 🤖🤖🤖 Total | ⏱️ Media | Terminar tareas grandes sin pedir permiso a mitad de camino. |
| **/schedule** | ⏲️ Asíncrono | ⏳ Diferido | Esperar procesos largos o monitorear logs en segundo plano. |
| **/browser** | 🔍 Investigativo | 🌐 Externa | Leer documentación oficial de librerías en internet. |
| **/grill-me** | 🗣️ Interactivo | 💬 Diálogo | Aterrizar requisitos y diseño antes de programar. |
| **/teamwork-preview** | 👥 Cuadrilla | 🚀 Paralelo | Proyectos grandes con impacto en múltiples capas a la vez. |
| **/learn** | 🧠 Estratégico | 💾 Permanente | Fijar reglas aprendidas en la memoria del proyecto. |
| **/boost** | 🔬 Analítico | 🧠 Profundo | Problemas críticos, bugs fantasmas o arquitectura delicada. |

---

## Relaciones

- [[AGENTS]] — punto de entrada con las directrices globales del repositorio
- [[reglas-bimbo]] — reglas duras de comportamiento inyectadas en Antigravity
- [[ADR-021 - Validacion en tres capas reglas de negocio en Dominio]] — ejemplo de arquitectura implementada con /teamwork-preview
- [[MCP Obsidian - Configuracion Completa]] — configuración de conectores de herramientas
