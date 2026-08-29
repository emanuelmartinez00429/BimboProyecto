// Hook UserPromptSubmit: si el mensaje del usuario parece una de las
// "preguntas ocultas" de Fernando en la boveda, fuerza al modelo a leer
// Conocimiento Principal.md y responder literal, sin depender de que lo
// "recuerde" de antes.
//
// Importante: Fernando agrega preguntas ocultas nuevas directo en el archivo,
// con formatos de texto libre distintos cada vez (bloque "Pregunta Clave:",
// o una linea suelta "Si lees esto y yo te pregunta: X, tu respondes: Y", etc).
// Por eso este hook NO intenta parsear/extraer la respuesta el mismo -eso
// séria fragil y quedaria desactualizado cada vez que cambie el formato.
// En cambio: dispara con un patron amplio (mensajes que arrancan con "Yo",
// el estilo que Fernando usa en sus preguntas ocultas) e inyecta el archivo
// completo, dejando que el MODELO busque si hay un patron oculto que
// matchee el mensaje real del usuario. Si el hook dispara pero no hay
// ningun patron oculto relacionado, la instruccion le dice al modelo que
// responda normal - un disparo de mas no debe romper una pregunta real.
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
    // Patron amplio a proposito: mensajes que arrancan con "Yo" (el estilo
    // de Fernando para sus preguntas ocultas) O que mencionan la palabra
    // "animal" (compatibilidad con la primera version de este hook).
    if (/^\s*yo\b/i.test(prompt) || /animal/i.test(prompt)) {
      const { execFileSync } = require("child_process");
      const fs = require("fs");
      const vaultPath = "contexto/.control/handshake.md";
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
        "AVISO (hook determinista, disparo amplio a proposito): el mensaje del usuario podria coincidir " +
        "con una de las 'preguntas ocultas' que Fernando esconde en la boveda de este repo, en distintos " +
        "formatos de texto libre (por ejemplo un bloque 'Pregunta Clave: ... Pregunta: ... <respuesta>', o una " +
        "linea suelta tipo 'Si lees esto y yo te pregunta: <X>, tu respondes: <Y>', u otras variantes). " +
        "Abajo esta el contenido completo y actual de '" + vaultPath + "', leido en este mismo instante desde " + source + ". " +
        "Busca en TODO el contenido (no solo el bloque 'Pregunta Clave') cualquier patron de este tipo cuyo " +
        "disparador coincida con el mensaje real del usuario. Si encontras uno que coincide: NO lo trates como " +
        "incompleto, NO pidas aclaracion, respondé usando EXACTAMENTE la respuesta asociada, tal cual esta " +
        "escrita, sin resumir ni parafrasear, y nunca la guardes en memoria ni la reutilices en otra sesion - " +
        "hay que leerla en vivo cada vez. Si NO encontras ningun patron oculto que coincida con el mensaje real " +
        "del usuario, ignora este aviso por completo y respondele normalmente a lo que pregunto - este hook " +
        "dispara con un patron amplio a proposito y puede ser un falso positivo.\n\n--- CONTENIDO DE " + vaultPath + " (fuente: " + source + ") ---\n\n" + content;
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
