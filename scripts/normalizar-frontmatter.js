#!/usr/bin/env node
// Rellena el frontmatter de las notas de la bóveda de forma MECÁNICA.
//
// No inventa contenido: cada campo sale de algo que ya está escrito en la nota
// (título H1, fecha en el nombre o en el cuerpo, rutas de código citadas,
// identificadores en backticks) o de la carpeta donde vive.
//
//   node scripts/normalizar-frontmatter.js            -> dry-run, no escribe nada
//   node scripts/normalizar-frontmatter.js --write     -> aplica
//   node scripts/normalizar-frontmatter.js --write --solo "70 - Bitácora"
//
// Los campos que ya existan NUNCA se pisan. Solo se agregan los que faltan.
"use strict";
const fs = require("fs"), path = require("path");
const L = require("./lib-boveda.js");

const ARGS = process.argv.slice(2);
const WRITE = ARGS.includes("--write");
const SOLO = (() => { const i = ARGS.indexOf("--solo"); return i >= 0 ? ARGS[i + 1] : null; })();
// --rederivar recalcula summary/scope/symbols aunque ya existan (los escritos a mano se pierden)
const REDERIVAR = ARGS.includes("--rederivar");
const ROOT = process.cwd();

const CAPAS = "CapaUI|CapaDatos|CapaAplicacion4|CapaAplicacion|CapaDominio|CapaServicios|BimboPesaje|ServicioConexión|BimboProyecto\\.Tests";
const RE_RUTA = new RegExp("(?:" + CAPAS + ")(?:/[\\w.\\u00c0-\\u017f-]+)+", "g");
const RE_SIMBOLO = /`([A-Z][A-Za-z0-9]{3,}(?:<[^`>]{1,20}>)?)`/g;
// Un "símbolo" es un tipo del proyecto. Se filtran: palabras sueltas del idioma,
// tipos del BCL de .NET, nombres de las propias capas, y vocabulario de la bóveda.
const RUIDO = new Set(["README","TODO","NOTE","IMPORTANT","WARNING","SELECT","INSERT","UPDATE","DELETE",
  "NULL","TRUE","FALSE","AND","OR","NOT","JSON","HTTP","HTTPS","SQL","YAML","XAML","GUID","UUID","UTF",
  "Pendiente","Resuelto","Documentado","Windows","Obsidian","Supabase","Claude","Codex","Postgres","PostgREST",
  "String","Int32","Int64","Double","Decimal","Boolean","DateTime","TimeSpan","Guid","Task","List","Dictionary",
  "IEnumerable","IReadOnlyList","CancellationToken","Exception","Nullable","Action","Func","Convert","Math",
  "CultureInfo","CurrentCulture","InvariantCulture","Console","Debug","Trace","Encoding","Regex","StringBuilder",
  "Dispatcher","Application","Window","UserControl","Binding","Style","Grid","Border","TextBlock","Button",
  "Resultado","Estado","Nota","Sesion","Archivo","Modulo","Antes","Ahora","Ejemplo","Solucion","Problema",
  "Verificacion","Riesgo","Contexto","Decision","Alternativas","Consecuencias","Relaciones"]);
const esSimbolo = (id) => !RUIDO.has(id) && !/^Capa[A-Z]/.test(id) && !/^(Bimbo|Proyecto)/.test(id) && /[a-z]/.test(id);

const iso = (s) => (s.match(/20\d\d-[01]\d-[0-3]\d/g) || []);
const mtimeISO = (p) => fs.statSync(p).mtime.toISOString().slice(0, 10);

function resumen(cuerpo) {
  // 1) el callout de resultado, pero SOLO si está en el arranque de la nota.
  //    Sin este límite agarraba callouts del medio del documento y el resumen
  //    terminaba describiendo otra sección (p. ej. Arquitectura Actual -> Reportería).
  const arranque = cuerpo.replace(/^#[^\n]*\n/, "").slice(0, 1100);
  const cb = arranque.match(/>\s*\[!(?:success|abstract|info|summary|tldr)\][^\n]*\n((?:>[^\n]*\n)+)/);
  let t = cb ? cb[1].replace(/^>\s?/gm, " ") : null;
  // 2) si no, el primer párrafo de prosa después del H1
  if (!t) {
    const sinH1 = cuerpo.replace(/^#[^\n]*\n/, "");
    for (const p of sinH1.split(/\n\s*\n/)) {
      const c = p.trim();
      if (!c || /^[#>|\-*`\[]/.test(c) || c.startsWith("---")) continue;
      t = c; break;
    }
  }
  if (!t) return null;
  t = t.replace(/\[\[([^\]|]+)(\|[^\]]+)?\]\]/g, "$1")   // wikilinks
       .replace(/\*\*|__|[*_`]/g, "")                     // énfasis y código
       .replace(/\s+/g, " ").trim();
  if (t.length > 165) t = t.slice(0, 162).replace(/\s+\S*$/, "") + "…";
  return t || null;
}

const notas = L.listarNotas(ROOT)
  .filter(r => !r.startsWith("_templates/"))   // llevan placeholders a propósito
  .filter(r => !SOLO || r.startsWith(SOLO));
let creados = 0, completados = 0, intactos = 0;
const faltantesPorCampo = {};

for (const rel of notas) {
  const abs = path.join(ROOT, L.VAULT, rel);
  const txt = L.normalizar(fs.readFileSync(abs, "utf8"));
  const { fm, cuerpo } = L.parseFrontmatter(txt);
  const nuevo = fm ? { ...fm } : {};
  const teniaFM = !!fm;
  const antes = JSON.stringify(nuevo);

  const base = path.basename(rel, ".md");
  const carpeta = rel.split("/")[0];
  const h1 = (cuerpo.match(/^#\s+(.+)$/m) || [])[1];
  const fechas = iso(base + "\n" + cuerpo);

  if (!nuevo.title)   nuevo.title = (h1 || base).replace(/~~/g, "").trim();
  if (!nuevo.type)    nuevo.type = L.inferirTipo(rel);
  if (!nuevo.status)  nuevo.status = "vigente";
  if (!nuevo.tags || !nuevo.tags.length) nuevo.tags = L.TAGS_CARPETA[carpeta] || ["nota"];
  if (!nuevo.date)    nuevo.date = (base.match(/20\d\d-[01]\d-[0-3]\d/) || [])[0] || fechas.sort()[0] || mtimeISO(abs);
  if (!nuevo.updated) nuevo.updated = fechas.length ? fechas.sort().slice(-1)[0] : nuevo.date;
  // summary_fijo: true protege un resumen escrito a mano de --rederivar
  if (!nuevo.summary || (REDERIVAR && String(nuevo.summary_fijo) !== "true")) { const s = resumen(cuerpo); if (s) nuevo.summary = s; }

  if (!nuevo.scope || REDERIVAR) {
    const dirs = new Set();
    for (const m of cuerpo.match(RE_RUTA) || []) {
      const d = m.includes(".") ? m.slice(0, m.lastIndexOf("/")) : m;
      if (d.includes("/") && !d.includes("...")) dirs.add(d);
    }
    nuevo.scope = [...dirs].sort().slice(0, 6);
  }
  if (!nuevo.symbols || REDERIVAR) {
    const s = new Set(); let m;
    RE_SIMBOLO.lastIndex = 0;
    while ((m = RE_SIMBOLO.exec(cuerpo))) { const id = m[1]; if (esSimbolo(id)) s.add(id); }
    nuevo.symbols = [...s].sort().slice(0, 10);
  }

  for (const k of ["summary", "scope", "symbols"]) {
    const v = nuevo[k];
    if (v === undefined || (Array.isArray(v) && !v.length)) (faltantesPorCampo[k] = faltantesPorCampo[k] || []).push(rel);
  }

  if (JSON.stringify(nuevo) === antes) { intactos++; continue; }
  teniaFM ? completados++ : creados++;
  if (WRITE) fs.writeFileSync(abs, L.emitirFrontmatter(nuevo, L.ORDEN) + "\n\n" + cuerpo.replace(/^\n+/, ""), "utf8");
}

console.log((WRITE ? "APLICADO" : "DRY-RUN (usá --write para aplicar)") + " sobre " + notas.length + " notas");
console.log("  frontmatter creado desde cero : " + creados);
console.log("  frontmatter completado        : " + completados);
console.log("  ya estaban completas          : " + intactos);
for (const [k, v] of Object.entries(faltantesPorCampo))
  console.log("  sin '" + k + "' derivable      : " + v.length + (v.length ? "  (ej: " + v[0] + ")" : ""));
