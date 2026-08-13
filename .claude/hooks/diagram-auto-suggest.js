// Hook UserPromptSubmit: si el mensaje del usuario pide un diagrama, sugiere
// usar la skill diagram-design (aviso, no invocación forzada — el modelo
// decide si aplica).
//
// Formato de entrada/salida: mismo contrato que vault-trigger.js. El harness
// invoca `node .claude/hooks/diagram-auto-suggest.js` y le pasa el evento por
// STDIN como JSON ({ prompt: "..." }); la salida esperada en STDOUT es
// { hookSpecificOutput: { hookEventName, additionalContext } } — NO existe
// un campo "suggestion"/"rationale"/"autoapply" que el harness interprete.
// La version anterior de este hook solo declaraba `module.exports` con un
// `handler(context)` que nadie llamaba: `node archivo.js` ejecuta el módulo
// y listo, no lee stdin ni imprime nada, asi que nunca disparó en la
// práctica.
let input = "";
process.stdin.on("data", (chunk) => { input += chunk; });
process.stdin.on("end", () => {
  try {
    const data = JSON.parse(input);
    const prompt = data.prompt || "";
    const promptLower = prompt.toLowerCase();

    // Palabras clave que activan la sugerencia de diagrama
    const diagramKeywords = [
      "diagrama", "diagramas", "diagram", "diagrama de",
      "flowchart", "flujo", "arquitectura", "arquitectónica",
      "secuencia", "sequence", "máquina de estado", "state machine",
      "organigrama", "org chart", "árbol", "tree",
      "red", "network", "grafo", "graph",
      "componentes", "componente", "infra", "infraestructura",
      "entidad-relación", "er", "entity relationship",
      "línea de tiempo", "timeline", "cronograma",
      "gantt", "kanban",
      "diagrama de clase", "class diagram", "uml",
      "caso de uso", "use case",
      "flujo de datos", "data flow",
      "mockup", "wireframe", "prototipo",
      "visual", "visualizar", "visualización"
    ];

    const matched = diagramKeywords.filter((kw) => promptLower.includes(kw));
    const isDiagramRequest = matched.length > 0;

    // Si ya lo pidió explícitamente por nombre, no hace falta el aviso
    if (isDiagramRequest && !promptLower.includes("diagram-design")) {
      const instruction =
        "AVISO (hook automático diagram-auto-suggest): el mensaje del usuario parece pedir un diagrama " +
        "(palabras detectadas: " + matched.slice(0, 5).join(", ") + "). Este proyecto tiene una skill local " +
        "en .claude/skills/diagram-design/SKILL.md (invocable con el nombre 'diagram-design') que genera " +
        "diagramas HTML+SVG autocontenidos con la marca Bimbo, sin dependencias externas. Considerá invocarla " +
        "con la herramienta Skill si encaja con el pedido — no es obligatorio, es solo una sugerencia; si el " +
        "usuario ya pidió otro formato (Mermaid, artifact-diagramming, etc.) o esto es un falso positivo, " +
        "ignorá este aviso y seguí con lo que corresponda.";
      console.log(JSON.stringify({
        hookSpecificOutput: {
          hookEventName: "UserPromptSubmit",
          additionalContext: instruction
        }
      }));
    }
  } catch (e) {
    // Entrada inválida: no hacer nada, no romper la sesión.
  }
});
