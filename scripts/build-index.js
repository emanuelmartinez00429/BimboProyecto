#!/usr/bin/env node
// Genera contexto/INDEX.md — el índice que reemplaza al "orden de lectura obligatorio".
//
// Sale del frontmatter de cada nota. Se lee ENTERO al arrancar una sesión (es barato)
// y sirve para elegir qué 3-6 notas abrir. La nota completa solo se abre si el índice
// dice que hace falta.
//
//   node scripts/build-index.js
//
// NO editar contexto/INDEX.md a mano: se sobreescribe en cada corrida.
"use strict";
const fs = require("fs"), path = require("path");
const L = require("./lib-boveda.js");

const ROOT = process.cwd();
const HOY = new Date().toISOString().slice(0, 10);
const MAX_SUMMARY = 78;

const ORDEN_CARPETAS = [
  ["40 - Proyecto Bimbo", "Estado vivo del sistema — empezá acá si vas a tocar código"],
  ["45 - Decisiones", "ADRs — por qué las cosas son como son"],
  ["20 - Patrones", "Patrones reutilizables del proyecto"],
  ["50 - Referencia", "Hechos externos: SDKs, WPF, Postgrest, bugs de librerías"],
  ["30 - Casos de Uso", "Recetas end-to-end"],
  ["10 - Arquitectura", "Conceptos arquitectónicos generales"],
  ["60 - Revisiones QA", "Planes y hallazgos de QA"],
  ["70 - Bitácora de Cambios", "Historial de sesiones — consultá por fecha, no lo leas de corrido"],
];

const notas = [];
for (const rel of L.listarNotas(ROOT)) {
  if (rel.startsWith("_templates/")) continue;
  const { fm } = L.parseFrontmatter(fs.readFileSync(path.join(ROOT, L.VAULT, rel), "utf8"));
  if (!fm) continue;
  notas.push({ rel, ...fm });
}

const corta = (s) => {
  if (!s) return "";
  s = String(s).replace(/\s+/g, " ").trim();
  return s.length > MAX_SUMMARY ? s.slice(0, MAX_SUMMARY - 1).replace(/\s+\S*$/, "") + "…" : s;
};
const marca = (n) => (n.status && n.status !== "vigente" ? ` \`${n.status}\`` : "");

const out = [];
out.push("---");
out.push('title: "INDEX — Índice generado de la bóveda"');
out.push("type: indice");
out.push("status: vigente");
out.push("tags: [indice, generado]");
out.push("date: 2026-08-23");
out.push("updated: " + HOY);
out.push('summary: "Índice generado de las notas de la bóveda: una línea por nota con tipo, fecha, resumen y los símbolos de código que declara."');
out.push("scope: []");
out.push("symbols: []");
out.push("---", "");
out.push("# INDEX — Índice de la bóveda");
out.push("");
out.push("> [!warning] Archivo GENERADO — no editar a mano");
out.push("> Sale del frontmatter de cada nota. Regeneralo con `node scripts/build-index.js` cada vez que agregues o cambies una nota.");
out.push("> Si hay conflicto de merge acá, no lo resuelvas: regeneralo.");
out.push("");
out.push(`**${notas.length} notas** · actualizado ${HOY}`);
out.push("");
out.push("Leé este archivo entero — es barato. Después abrí **solo** las 3–6 notas que la tarea necesita.");
out.push("Si no encontrás lo que buscás: `grep -ri \"<término>\" contexto/ --include=\"*.md\" -l`.");
out.push("");
out.push("---");
out.push("");

const vistas = new Set();
for (const [carpeta, desc] of ORDEN_CARPETAS) {
  const grupo = notas.filter(n => n.rel.startsWith(carpeta + "/"));
  if (!grupo.length) continue;
  grupo.forEach(n => vistas.add(n.rel));
  out.push(`## ${carpeta} — ${grupo.length}`);
  out.push("");
  out.push(`*${desc}*`);
  out.push("");
  if (carpeta.startsWith("70")) {
    // El historial completo vive en INDEX-bitacora.md: son 96 notas que nadie
    // necesita al arrancar. Acá solo las 10 últimas, para responder "¿en qué íbamos?".
    const recientes = grupo.slice().sort((a, b) => String(b.date).localeCompare(String(a.date))).slice(0, 10);
    for (const n of recientes)
      out.push(`- \`${n.date}\` ${String(n.title).replace(/^Sesión \d{4}-\d\d-\d\d\s*[—-]\s*/, "")}`);
    out.push("");
    out.push(`Historial completo de las ${grupo.length} sesiones: [[INDEX-bitacora]] (\`contexto/INDEX-bitacora.md\`).`);
    out.push("");
  } else {
    for (const n of grupo.sort((a, b) => String(a.title).localeCompare(String(b.title), "es"))) {
      const archivo = n.rel.slice(carpeta.length + 1).replace(/\.md$/, "");
      out.push(`- **${archivo}**${marca(n)} · ${n.updated} — ${corta(n.summary)}`);
    }
    out.push("");
  }
}

const sueltas = notas.filter(n => !vistas.has(n.rel));
if (sueltas.length) {
  out.push("## Raíz de la bóveda y otros");
  out.push("");
  for (const n of sueltas.sort((a, b) => a.rel.localeCompare(b.rel)))
    out.push(`- **${n.title}** · \`${n.type}\` — ${corta(n.summary)} · \`${n.rel}\``);
  out.push("");
}

out.push("---");
out.push("");
out.push("## Antes de tocar código");
out.push("");
out.push("El mapa `código → notas` y el índice de símbolos compartidos viven en [[INDEX-codigo]]");
out.push("(`contexto/INDEX-codigo.md`). Abrilo cuando vayas a modificar un archivo: te dice qué notas");
out.push("lo describen y cuáles hay que actualizar después.");
out.push("");
out.push("---");
out.push("");
out.push("## Relaciones");
out.push("");
out.push("- [[AGENTS]] — protocolo de la bóveda: cómo clasificar y guardar");
out.push("- [[Conocimiento Principal]] — dashboard para humanos");
out.push("- [[Arquitectura Actual]] — estado vivo del sistema");
out.push("- [[Deuda Técnica - Pendientes]] — deuda abierta");
out.push("");

const txt = out.join("\n").replace(/\n{3,}/g, "\n\n");
fs.writeFileSync(path.join(ROOT, L.VAULT, "INDEX.md"), txt, "utf8");

// ---- índice separado de la bitácora: se abre solo cuando hace falta el historial ----
const bit = notas.filter(n => n.rel.startsWith("70 - Bitácora de Cambios/"));
const b = ["---", 'title: "INDEX — Bitácora de sesiones"', "type: indice", "status: vigente",
  "tags: [indice, generado, bitacora]", "date: 2026-08-23", "updated: " + HOY,
  'summary: "Índice generado del historial de sesiones, agrupado por mes. Consultá acá cuando necesites saber qué pasó y cuándo."',
  "scope: []", "symbols: []", "---", "",
  "# INDEX — Bitácora de sesiones", "",
  "> [!warning] Archivo GENERADO — no editar a mano (`node scripts/build-index.js`)", "",
  `**${bit.length} sesiones** · actualizado ${HOY}`, "",
  "No hace falta leerlo entero. Buscá el mes o el tema y abrí solo esa nota.", "", "---", ""];
const meses = {};
for (const n of bit) { const m = n.rel.split("/")[1] || "sin-mes"; (meses[m] = meses[m] || []).push(n); }
for (const m of Object.keys(meses).sort().reverse()) {
  b.push(`## ${m} — ${meses[m].length}`, "");
  for (const n of meses[m].sort((x, y) => String(y.date).localeCompare(String(x.date)))) {
    const titulo = String(n.title).replace(/^Sesión \d{4}-\d\d-\d\d\s*[—-]\s*/, "");
    b.push(`- \`${n.date}\` **${titulo}** — ${corta(n.summary)}`);
  }
  b.push("");
}
b.push("---", "", "## Relaciones", "", "- [[INDEX]] — índice principal de la bóveda", "- [[AGENTS]] — protocolo de la bóveda", "");
const btxt = b.join("\n").replace(/\n{3,}/g, "\n\n");
fs.writeFileSync(path.join(ROOT, L.VAULT, "INDEX-bitacora.md"), btxt, "utf8");

// ---- INDEX-codigo.md: los mapas que conectan la bóveda con el código ----
const c = ["---", 'title: "INDEX — Mapa código a notas"', "type: indice", "status: vigente",
  "tags: [indice, generado, codigo]", "date: 2026-08-23", "updated: " + HOY,
  'summary: "Mapa generado de qué notas describen cada área del código y qué símbolos aparecen en varias notas. Abrilo antes de modificar un archivo."',
  "scope: []", "symbols: []", "---", "",
  "# INDEX — Mapa código → notas", "",
  "> [!warning] Archivo GENERADO — no editar a mano (`node scripts/build-index.js`)", "",
  "Sale de los campos `scope` y `symbols` del frontmatter. Si tocaste un archivo bajo una de estas",
  "rutas, revisá si la nota que lo describe quedó desactualizada y actualizá su `updated`.", "", "---", ""];
// ---- mapa código -> notas: la respuesta a "toqué este archivo, ¿qué notas revisar?" ----
c.push("---");
c.push("");
c.push("## Mapa código → notas");
c.push("");
c.push("Qué notas describen cada área del código. Sale del campo `scope` del frontmatter.");
c.push("Si tocaste un archivo bajo una de estas rutas, revisá si su nota quedó desactualizada.");
c.push("");
const porArea = {};
for (const n of notas) for (const s of n.scope || []) {
  const area = s.split("/").slice(0, 2).join("/");
  (porArea[area] = porArea[area] || new Set()).add(n.title);
}
for (const area of Object.keys(porArea).sort()) {
  const t = [...porArea[area]].sort();
  const lista = t.slice(0, 6).map(x => `[[${x}]]`).join(", ") + (t.length > 6 ? ` … +${t.length - 6}` : "");
  c.push(`- \`${area}\` → ${lista}`);
}
c.push("");

// ---- símbolos compartidos: solo los que aparecen en 2+ notas ----
const porSimbolo = {};
for (const n of notas) for (const s of n.symbols || []) (porSimbolo[s] = porSimbolo[s] || []).push(n.title);
const compartidos = Object.entries(porSimbolo).filter(([, v]) => v.length >= 2)
  .sort((a, b) => b[1].length - a[1].length || a[0].localeCompare(b[0])).slice(0, 50);
if (compartidos.length) {
  c.push("## Símbolos que aparecen en varias notas");
  c.push("");
  c.push("Clases, interfaces y servicios documentados en más de un lugar. Cambiar uno de estos toca varias notas.");
  c.push("");
  for (const [s, t] of compartidos) {
    const u = [...new Set(t)].sort();
    c.push(`- \`${s}\` → ${u.slice(0, 3).map(x => `[[${x}]]`).join(", ")}${u.length > 3 ? ` … +${u.length - 3}` : ""}`);
  }
  c.push("");
}


c.push("---", "", "## Relaciones", "", "- [[INDEX]] — índice principal de la bóveda", "- [[AGENTS]] — protocolo de la bóveda", "");
const ctxt = c.join("\n").replace(/\n{3,}/g, "\n\n");
fs.writeFileSync(path.join(ROOT, L.VAULT, "INDEX-codigo.md"), ctxt, "utf8");

const kb = (t) => (Buffer.byteLength(t) / 1024).toFixed(1) + " KB";
const tk = (t) => "~" + Math.round(Buffer.byteLength(t) / 3.6 / 100) * 100 + " tk";
console.log(`INDEX.md          ${notas.length - bit.length} notas + mapas   ${kb(txt)}  ${tk(txt)}`);
console.log(`INDEX-bitacora.md ${bit.length} sesiones            ${kb(btxt)}  ${tk(btxt)}`);
console.log(`INDEX-codigo.md   mapas scope + símbolos   ${kb(ctxt)}  ${tk(ctxt)}`);
