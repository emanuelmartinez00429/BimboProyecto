// Hook SessionStart — inyeccion de contexto de arranque (Bimbo v2).
//
// Reemplaza al node -e inline que inyectaba "00 - MOC/Conocimiento Principal.md"
// COMPLETO en cada sesion de cada agente (~6,7 KB). Ahora inyecta dos archivos
// chicos y con proposito distinto:
//
//   1) contexto/.control/handshake.md  -> el test de conexion viva (preguntas ocultas).
//      Chico a proposito: se lee en cada sesion de cada agente.
//   2) contexto/INDEX.md               -> el indice GENERADO de la boveda: una linea
//      por nota con tipo, estado, fecha y resumen. Es el reemplazo del "orden de
//      lectura obligatorio": el agente elige que abrir en vez de abrir todo.
//
// Si INDEX.md todavia no existe (antes de correr scripts/build-index.js), el hook
// sigue funcionando: inyecta solo el handshake y avisa.
//
// Compartido por Claude Code (.claude/settings.json) y Codex (.codex/hooks.json).

const fs = require("fs");
const path = require("path");

const ROOT = process.env.CLAUDE_PROJECT_DIR || process.cwd();
const read = (rel) => {
  try { return fs.readFileSync(path.join(ROOT, rel), "utf8"); } catch { return null; }
};

const handshake = read("contexto/.control/handshake.md");
const index = read("contexto/INDEX.md");

const partes = [];

partes.push(
  "CONTEXTO DE ARRANQUE — Proyecto Bimbo. El contrato completo esta en AGENTS.md (raiz del repo); " +
  "el protocolo de la boveda en contexto/AGENTS.md. NO leas la boveda entera: usa el indice de abajo " +
  "para elegir que abrir, y abri solo esas notas."
);

if (handshake) {
  partes.push("=== contexto/.control/handshake.md (leido en vivo en este instante) ===\n" + handshake);
} else {
  partes.push("AVISO: no se encontro contexto/.control/handshake.md — el test de conexion no esta disponible.");
}

if (index) {
  partes.push("=== contexto/INDEX.md (generado por scripts/build-index.js) ===\n" + index);
} else {
  partes.push(
    "AVISO: contexto/INDEX.md no existe todavia. Genera con: node scripts/build-index.js . " +
    "Mientras tanto, para ubicar una nota usa grep sobre contexto/ o lee 00 - MOC/Conocimiento Principal.md."
  );
}

process.stdout.write(JSON.stringify({
  hookSpecificOutput: {
    hookEventName: "SessionStart",
    additionalContext: partes.join("\n\n")
  }
}));
