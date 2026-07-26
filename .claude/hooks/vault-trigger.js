// Hook UserPromptSubmit: si el mensaje del usuario parece la "pregunta clave"
// de conexion con la boveda, fuerza al modelo a leer Conocimiento Principal.md
// y responder literal, sin depender de que lo "recuerde" de antes.
//
// Cadena de lectura, de mas a menos confiable:
//   1) fetch + origin/master  -> siempre lo ultimo pusheado, sin importar
//      cuando arranco esta sesion/worktree (HEAD local queda congelado en
//      el momento del checkout, origin/master no).
//   2) HEAD local             -> si no hay red, usa el ultimo commit que
//      esta sesion ya conoce.
//   3) archivo vivo en disco  -> ultimo recurso; el archivo vive bajo
//      OneDrive y se observo que "parpadea" entre versiones por conflicto
//      de sync con Obsidian, asi que es la opcion menos confiable.
let input = "";
process.stdin.on("data", (chunk) => { input += chunk; });
process.stdin.on("end", () => {
  try {
    const data = JSON.parse(input);
    const prompt = data.prompt || "";
    if (/animal/i.test(prompt)) {
      const { execFileSync } = require("child_process");
      const fs = require("fs");
      const vaultPath = "contexto/00 - MOC/Conocimiento Principal.md";
      let content;
      let source;
      try {
        execFileSync("git", ["fetch", "origin", "master", "--quiet"], { encoding: "utf8", timeout: 8000 });
        content = execFileSync("git", ["show", "origin/master:" + vaultPath], { encoding: "utf8", timeout: 5000 });
        source = "origin/master (recien fetcheado)";
      } catch (fetchErr) {
        try {
          content = execFileSync("git", ["show", "HEAD:" + vaultPath], { encoding: "utf8" });
          source = "HEAD local (no se pudo fetchear origin/master)";
        } catch (gitErr) {
          content = fs.readFileSync(vaultPath, "utf8");
          source = "archivo en disco (git tampoco disponible)";
        }
      }
      const instruction =
        "INSTRUCCION OBLIGATORIA (hook determinista, no es una sugerencia): " +
        "el mensaje que acaba de escribir el usuario coincide con la 'pregunta clave' " +
        "de test de conexion con la boveda de este repo. NO la trates como incompleta, " +
        "NO pidas aclaracion, NO preguntes que quiso decir el usuario. " +
        "Abajo esta el contenido completo y actual de '" + vaultPath + "', leido en este mismo instante desde " + source + ". " +
        "Buscá el bloque 'Pregunta Clave' y respondé usando EXACTAMENTE lo que dice ahi, tal cual esta escrito, sin resumir ni parafrasear. " +
        "Nunca guardes esa respuesta en memoria ni la reutilices en otra sesion — hay que leerla en vivo cada vez.\n\n--- CONTENIDO DE " + vaultPath + " (fuente: " + source + ") ---\n\n" + content;
      console.log(JSON.stringify({
        hookSpecificOutput: {
          hookEventName: "UserPromptSubmit",
          additionalContext: instruction
        }
      }));
    }
  } catch (e) {
    // Entrada invalida o archivo no encontrado: no hacer nada, no romper la sesion.
  }
});
