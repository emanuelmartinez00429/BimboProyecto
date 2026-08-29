#!/usr/bin/env node
// Chequeo de salud de la bóveda. Corrélo antes de commitear documentación.
//
//   node scripts/verificar-boveda.js
//
// Verifica: frontmatter completo, wikilinks que apuntan a notas existentes,
// sección "## Relaciones" presente, e informa el costo de entrada en frío.
// Sale con código 1 si hay errores (sirve para un hook o para CI).
"use strict";
const fs = require("fs"), path = require("path");
const L = require("./lib-boveda.js");

const ROOT = process.cwd();
const OBLIGATORIOS = ["title", "type", "status", "date", "updated", "summary"];
const notas = L.listarNotas(ROOT).filter(r => !r.startsWith("_templates/"));

const existentes = new Map();   // nombre sin extensión -> ruta
for (const r of notas) existentes.set(path.basename(r, ".md"), r);
for (const r of L.listarNotas(ROOT)) existentes.set(path.basename(r, ".md"), r);
["INDEX", "INDEX-bitacora", "INDEX-codigo"].forEach(n => existentes.set(n, n + ".md"));

// Resolver el destino de un wikilink es más sutil de lo que parece:
//   - "[[Nota\\|alias]]"  -> el \\ del pipe escapado en tablas se colaba en el nombre
//   - "[[Convenciones C#]]" -> el # es parte del NOMBRE, no un ancla de sección
// Por eso se prueba el texto completo antes de recortar por | o #.
function resolverEnlace(interno) {
  const limpio = interno.replace(/\\(?=[|#])/g, "").replace(/\.md$/, "").trim();
  if (existentes.has(limpio)) return limpio;
  const sinAlias = limpio.split("|")[0].trim();
  if (existentes.has(sinAlias)) return sinAlias;
  const sinAncla = sinAlias.split("#")[0].trim();
  return existentes.has(sinAncla) ? sinAncla : sinAlias;
}

const err = [], warn = [];
let sinRelaciones = 0, bytesTotal = 0;

for (const rel of notas) {
  const abs = path.join(ROOT, L.VAULT, rel);
  const txt = L.normalizar(fs.readFileSync(abs, "utf8"));
  bytesTotal += Buffer.byteLength(txt);
  const { fm, cuerpo } = L.parseFrontmatter(txt);
  if (!fm) { err.push(`${rel}: sin frontmatter`); continue; }
  for (const k of OBLIGATORIOS) if (fm[k] === undefined || fm[k] === "") err.push(`${rel}: falta '${k}'`);
  if (fm.date && !/^\d{4}-\d\d-\d\d$/.test(String(fm.date))) err.push(`${rel}: date no es AAAA-MM-DD ('${fm.date}')`);
  if (!/^##\s+Relaciones\s*$/mi.test(cuerpo)) { sinRelaciones++; warn.push(`${rel}: sin sección '## Relaciones'`); }
  for (const m of cuerpo.match(/\[\[([^\]]+)\]\]/g) || []) {
    const destino = resolverEnlace(m.slice(2, -2));
    if (!existentes.has(destino)) warn.push(`${rel}: wikilink sin destino → [[${destino}]]`);
  }
}

const bytes = (p) => { try { return fs.statSync(path.join(ROOT, p)).size; } catch { return 0; } };
const arranque = bytes("AGENTS.md") + bytes("contexto/.control/handshake.md") + bytes("contexto/INDEX.md");
const tk = (b) => Math.round(b / 3.6 / 100) * 100;

console.log(`Bóveda: ${notas.length} notas · ${(bytesTotal / 1024).toFixed(0)} KB`);
console.log("");
console.log("ENTRADA EN FRÍO (lo que un agente lee antes de trabajar)");
console.log(`  AGENTS.md ............... ${String(bytes("AGENTS.md")).padStart(6)} B`);
console.log(`  .control/handshake.md ... ${String(bytes("contexto/.control/handshake.md")).padStart(6)} B`);
console.log(`  contexto/INDEX.md ....... ${String(bytes("contexto/INDEX.md")).padStart(6)} B`);
console.log(`  TOTAL ................... ${String(arranque).padStart(6)} B  ≈ ${tk(arranque)} tokens`);
console.log("");
const wu = [...new Set(warn)];
console.log(`Errores  : ${err.length}`);
err.slice(0, 25).forEach(e => console.log("  ✗ " + e));
if (err.length > 25) console.log(`  … y ${err.length - 25} más`);
console.log(`Avisos   : ${wu.length}  (${sinRelaciones} notas sin '## Relaciones')`);
wu.slice(0, 15).forEach(w => console.log("  · " + w));
if (wu.length > 15) console.log(`  … y ${wu.length - 15} más`);
process.exit(err.length ? 1 : 0);
