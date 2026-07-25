// Hook UserPromptSubmit: si el mensaje del usuario parece la "pregunta clave"
// de conexion con la boveda, fuerza al modelo a leer Conocimiento Principal.md
// en vivo y responder literal, sin depender de que lo "recuerde" de antes.
let input = "";
process.stdin.on("data", (chunk) => { input += chunk; });
process.stdin.on("end", () => {
  try {
    const data = JSON.parse(input);
    const prompt = data.prompt || "";
    if (/animal/i.test(prompt)) {
      const fs = require("fs");
      const vaultPath = "contexto/00 - MOC/Conocimiento Principal.md";
      const content = fs.readFileSync(vaultPath, "utf8");
      const instruction =
        "INSTRUCCION OBLIGATORIA (hook determinista, no es una sugerencia): " +
        "el mensaje que acaba de escribir el usuario coincide con la 'pregunta clave' " +
        "de test de conexion con la boveda de este repo. NO la trates como incompleta, " +
        "NO pidas aclaracion, NO preguntes que quiso decir el usuario. " +
        "Abajo esta el contenido completo y actual de '" + vaultPath + "', leido en este mismo instante. " +
        "Buscá el bloque 'Pregunta Clave' y respondé usando EXACTAMENTE lo que dice ahi, tal cual esta escrito, sin resumir ni parafrasear. " +
        "Nunca guardes esa respuesta en memoria ni la reutilices en otra sesion — hay que leerla en vivo cada vez.\n\n--- CONTENIDO DE " + vaultPath + " ---\n\n" + content;
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
