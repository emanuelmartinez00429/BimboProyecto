// Utilidades compartidas por scripts/normalizar-frontmatter.js y scripts/build-index.js
"use strict";
const fs = require("fs");
const path = require("path");

const VAULT = "contexto";
const EXCLUIR = new Set([".obsidian", ".control", "node_modules"]);

function listarNotas(root) {
  const out = [];
  (function walk(dir) {
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      if (e.isDirectory()) {
        if (EXCLUIR.has(e.name)) continue;
        walk(path.join(dir, e.name));
      } else if (e.isFile() && e.name.endsWith(".md")) {
        const rel = path.relative(path.join(root, VAULT), path.join(dir, e.name)).split(path.sep).join("/");
        if (rel === "INDEX.md") continue;
        out.push(rel);
      }
    }
  })(path.join(root, VAULT));
  return out.sort();
}

// Parser de frontmatter deliberadamente simple: clave: valor y listas con "- ".
// No es YAML completo — la bóveda no lo necesita y así no hay dependencias.
// Normaliza BOM y CRLF antes de parsear: 72 de las 186 notas venían con CRLF
// y 3 con BOM, y eso hacía que el frontmatter existente pasara desapercibido.
function normalizar(txt) {
  return txt.replace(/^\uFEFF/, "").replace(/\r\n/g, "\n").replace(/\r/g, "\n");
}

function parseFrontmatter(entrada) {
  const txt = normalizar(entrada);
  if (!txt.startsWith("---\n")) return { fm: null, cuerpo: txt, crudo: "" };
  const fin = txt.indexOf("\n---", 3);
  if (fin === -1) return { fm: null, cuerpo: txt, crudo: "" };
  const crudo = txt.slice(4, fin);
  const cuerpo = txt.slice(fin + 4).replace(/^\n/, "");
  const fm = {};
  let claveLista = null;
  for (const linea of crudo.split("\n")) {
    const item = linea.match(/^\s*-\s+(.*)$/);
    if (item && claveLista) { fm[claveLista].push(desquote(item[1])); continue; }
    const kv = linea.match(/^([A-Za-z_][\w-]*):\s*(.*)$/);
    if (!kv) continue;
    const [, k, v] = kv;
    if (v === "") { claveLista = k; fm[k] = []; }
    else if (v.startsWith("[")) { claveLista = null; fm[k] = v.slice(1, -1).split(",").map(s => desquote(s.trim())).filter(Boolean); }
    else { claveLista = null; fm[k] = desquote(v); }
  }
  return { fm, cuerpo, crudo };
}

const desquote = (s) => s.replace(/^["'](.*)["']$/, "$1").trim();
const quote = (s) => /[:#\[\]{}",]|^\s|\s$/.test(s) ? '"' + String(s).replace(/"/g, '\\"') + '"' : s;

function emitirFrontmatter(fm, orden) {
  const claves = orden.filter(k => fm[k] !== undefined && fm[k] !== null)
    .concat(Object.keys(fm).filter(k => !orden.includes(k)));
  const out = ["---"];
  for (const k of claves) {
    const v = fm[k];
    if (Array.isArray(v)) {
      if (v.length === 0) out.push(`${k}: []`);
      else { out.push(`${k}:`); v.forEach(x => out.push(`  - ${quote(String(x))}`)); }
    } else out.push(`${k}: ${quote(String(v))}`);
  }
  out.push("---");
  return out.join("\n");
}

const ORDEN = ["title","type","status","tags","date","updated","summary","summary_fijo","scope","symbols",
               "branch","autor_cambios","revisor","estado","supersedes","lifecycle","aliases"];

// tipo derivado de la carpeta y del nombre — mecánico, sin criterio
function inferirTipo(rel) {
  const base = path.basename(rel);
  if (rel.startsWith("00 - MOC")) return "moc";
  if (rel.startsWith("10 - Arquitectura")) return "arquitectura";
  if (rel.startsWith("20 - Patrones")) return "patron";
  if (rel.startsWith("30 - Casos de Uso")) return "caso";
  if (rel.startsWith("45 - Decisiones")) return "adr";
  if (rel.startsWith("50 - Referencia")) return "referencia";
  if (rel.startsWith("60 - Revisiones QA")) return "qa";
  if (rel.startsWith("70 - Bitácora")) return /^Plan /.test(base) ? "plan" : "sesion";
  if (rel.startsWith("_templates")) return "plantilla";
  if (rel === "AGENTS.md" || rel === "CLAUDE.md") return "protocolo";
  if (rel.startsWith("40 - Proyecto Bimbo")) {
    if (/^Deuda/.test(base)) return "deuda";
    if (/^Módulo/.test(base)) return "modulo";
    if (/^Plan /.test(base)) return "plan";
    return "estado";
  }
  return "nota";
}

const TAGS_CARPETA = {
  "00 - MOC": ["moc"], "10 - Arquitectura": ["arquitectura"], "20 - Patrones": ["patron"],
  "30 - Casos de Uso": ["caso-de-uso"], "40 - Proyecto Bimbo": ["proyecto"],
  "45 - Decisiones": ["adr"], "50 - Referencia": ["referencia"],
  "60 - Revisiones QA": ["qa"], "70 - Bitácora de Cambios": ["sesion"], "_templates": ["plantilla"],
};

module.exports = { VAULT, normalizar, listarNotas, parseFrontmatter, emitirFrontmatter, ORDEN, inferirTipo, TAGS_CARPETA, quote, desquote };
