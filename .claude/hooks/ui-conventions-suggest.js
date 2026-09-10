// Hook UserPromptSubmit: si el mensaje del usuario tiene que ver con tocar XAML /
// la capa visual WPF, recuerda leer las convenciones de UI y usar la skill
// ui-conventions (aviso, no invocacion forzada — el modelo decide si aplica).
//
// Mismo contrato que diagram-auto-suggest.js / vault-trigger.js: el harness
// invoca `node .claude/hooks/ui-conventions-suggest.js` y le pasa el evento por
// STDIN como JSON ({ prompt: "..." }); la salida esperada en STDOUT es
// { hookSpecificOutput: { hookEventName, additionalContext } }.
let input = "";
process.stdin.on("data", (chunk) => { input += chunk; });
process.stdin.on("end", () => {
  try {
    const data = JSON.parse(input);
    const prompt = (data.prompt || "").toLowerCase();

    // Palabras clave de trabajo en la capa visual (XAML / WPF).
    const uiKeywords = [
      "xaml", ".xaml", "usercontrol", "user control",
      "resourcedictionary", "styles.xaml", "designtimeresources",
      "app.xaml", "x:static", "staticresource", "dynamicresource",
      "d:designwidth", "d:designheight", "mc:ignorable",
      "ivalueconverter", "value converter", "converter",
      "controltemplate", "datatemplate", "relativesource", "ancestortype",
      "previsualiz", "previsualizar", "lienzo en blanco",
      "diseñador de visual studio", "disenador de visual studio",
      "diseñador de vs", "disenador de vs", "vs designer", "wpf designer",
      "modal.xaml", "modaloverlay", "limitaraloverlay",
      "wpf", "modal", "pantalla wpf", "vista wpf", "code-behind", "code behind"
    ];

    const matched = uiKeywords.filter((kw) => prompt.includes(kw));
    // Umbral: "wpf"/"modal"/"converter" solos dan muchos falsos positivos, asi
    // que solo disparamos si hay una senal fuerte (xaml/usercontrol/...) o
    // al menos dos coincidencias cualquiera.
    const fuertes = ["xaml", ".xaml", "usercontrol", "user control",
      "resourcedictionary", "styles.xaml", "designtimeresources", "app.xaml",
      "x:static", "staticresource", "dynamicresource", "d:designwidth",
      "d:designheight", "mc:ignorable", "ivalueconverter", "previsualiz",
      "diseñador de visual studio", "disenador de visual studio",
      "diseñador de vs", "disenador de vs", "vs designer", "wpf designer",
      "modaloverlay", "limitaraloverlay", "ancestortype"];
    const haySenalFuerte = fuertes.some((kw) => prompt.includes(kw));
    const dispara = haySenalFuerte || matched.length >= 2;

    if (dispara && !prompt.includes("ui-conventions")) {
      const instruction =
        "AVISO (hook automático ui-conventions-suggest): el mensaje parece involucrar " +
        "trabajo en la capa visual WPF/XAML (palabras detectadas: " +
        matched.slice(0, 6).join(", ") + "). ANTES de crear o editar cualquier .xaml, " +
        "UserControl, Window, ResourceDictionary o su code-behind: leé y aplicá " +
        "`contexto/20 - Patrones/Convenciones de UI (WPF) — leer antes de tocar XAML.md` " +
        "(ctor sin parámetros, sin DI en el .ctor, converters por {x:Static conv:X.Instancia} " +
        "y no {StaticResource} de App.xaml, tamaño impuesto por el host y no AncestorType en el " +
        "raíz, estilos compartidos en Styles.xaml, Empresa* solo en App.xaml/DesignTimeResources). " +
        "La skill local `ui-conventions` (.claude/skills/ui-conventions/SKILL.md) es el checklist " +
        "accionable + el arné de verificación. Invocala con la herramienta Skill si encaja. Al " +
        "terminar: build 0/0 + arné de instanciación + prueba visual manual. Si es un falso " +
        "positivo (no vas a tocar XAML), ignorá este aviso.";
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
