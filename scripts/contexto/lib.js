// Utilidades compartidas por build-index.js, verificar.js y normalizar-bitacora.js.
// Adaptado a la taxonomía de BimboProyecto desde el estándar de FOE 360 Móvil y Análogos.
// Sin dependencias externas: solo módulos nativos de Node 20+.
"use strict";
const fs = require("fs");
const path = require("path");

const VAULT = "contexto";
const INDICES = ["INDEX.md", "INDEX-bitacora.md", "AGENTS.md"];
const SESIONES_AL_ARRANCAR = 3;
const EXCLUIR = new Set([".obsidian", ".control", "_templates", "node_modules"]);

const CARPETAS = [
  ["00 - MOC", "Mapas de contenido y dashboard"],
  ["10 - Arquitectura", "Directrices arquitectónicas"],
  ["20 - Patrones", "Patrones de código y UI"],
  ["30 - Casos de Uso", "Casos de uso y flujos"],
  ["40 - Proyecto Bimbo", "Estado vivo: módulos, arquitectura y deuda"],
  ["45 - Decisiones", "Registros de decisión (ADR)"],
  ["50 - Referencia", "Hechos externos, SDKs y APIs"],
  ["60 - Revisiones QA", "Revisiones de calidad y auditorías"],
  ["70 - Bitácora de Cambios", "Sesiones por mes y resúmenes"],
];

const TIPOS = new Set([
  "sesion", "resumen", "adr", "patron", "referencia", "arquitectura", "qa", "deuda", "moc",
  "caso-uso", "decision-pendiente", "investigacion", "regla", "diagrama", "modulo", "submodulo",
  "datos", "seguridad", "producto", "estandar", "mapa", "informe", "arnes",
]);

const ESTADOS = new Set(["borrador", "vigente", "reemplazado", "archivado"]);
const OBLIGATORIOS = ["title", "type", "status", "date", "updated", "summary", "autor"];
const AGENTES = new Set(["claude-code", "codex", "antigravity", "gemini-cli", "copilot", "cursor", "opencode", "humano"]);
const RETIRADOS = new Set(["reemplazado", "archivado"]);

function leerEquipo(root) {
  try {
    const raw = fs.readFileSync(path.join(root, VAULT, ".control", "equipo.json"), "utf8");
    const parsed = JSON.parse(raw);
    return Array.isArray(parsed) ? parsed : null;
  } catch {
    return null;
  }
}

function listarNotas(root) {
  const base = path.join(root, VAULT);
  const out = [];
  if (!fs.existsSync(base)) return out;
  (function walk(dir) {
    for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
      if (e.isDirectory()) {
        if (!EXCLUIR.has(e.name)) walk(path.join(dir, e.name));
      } else if (e.isFile() && e.name.endsWith(".md")) {
        const rel = path.relative(base, path.join(dir, e.name)).split(path.sep).join("/");
        if (!INDICES.includes(rel)) out.push(rel);
      }
    }
  })(base);
  return out.sort();
}

function normalizar(txt) {
  return txt.replace(/^\uFEFF/, "").replace(/\r\n?/g, "\n");
}

const desquote = (s) => String(s).trim().replace(/^["'](.*)["']$/, "$1").trim();

function sinComentario(v) {
  let comilla = null;
  let inicio = true;
  for (let i = 0; i < v.length; i++) {
    const c = v[i];
    if (comilla === '"') {
      if (c === "\\") i++;
      else if (c === '"') comilla = null;
    } else if (comilla === "'") {
      if (c === "'" && v[i + 1] === "'") i++;
      else if (c === "'") comilla = null;
    } else if (c === "#" && i > 0 && /\s/.test(v[i - 1])) {
      return v.slice(0, i).trim();
    } else if (inicio && (c === '"' || c === "'")) {
      comilla = c;
    }
    if (!/\s/.test(c)) inicio = c === "[" || c === ",";
  }
  return v.trim();
}

function parseFrontmatter(entrada) {
  const txt = normalizar(entrada);
  if (!txt.startsWith("---\n")) return { fm: null, cuerpo: txt };
  const fin = txt.indexOf("\n---", 3);
  if (fin === -1) return { fm: null, cuerpo: txt };
  const crudo = txt.slice(4, fin);
  const cuerpo = txt.slice(fin + 4).replace(/^\n/, "");
  const fm = {};
  let lista = null;
  for (const linea of crudo.split("\n")) {
    if (/^\s*(#.*)?$/.test(linea)) continue;
    const itemMapa = linea.match(/^\s*-\s+([A-Za-z_][\w-]*):\s*(.*)$/);
    const item = linea.match(/^\s*-\s+(.*)$/);
    const sub = linea.match(/^\s+([A-Za-z_][\w-]*):\s*(.*)$/);
    const kv = linea.match(/^([A-Za-z_][\w-]*):\s*(.*)$/);
    if (lista && itemMapa && !/^https?$/.test(itemMapa[1])) {
      fm[lista].push({ [itemMapa[1]]: desquote(itemMapa[2]) });
    } else if (lista && item) {
      fm[lista].push(desquote(item[1]));
    } else if (lista && sub && typeof fm[lista].at(-1) === "object") {
      fm[lista].at(-1)[sub[1]] = desquote(sub[2]);
    } else if (kv) {
      const [, k, v] = kv;
      const valor = sinComentario(v);
      if (valor === "") { lista = k; fm[k] = []; }
      else if (valor.startsWith("[")) { lista = null; fm[k] = valor.slice(1, -1).split(",").map(desquote).filter(Boolean); }
      else { lista = null; fm[k] = desquote(valor); }
    }
  }
  return { fm, cuerpo };
}

function destinoEnlace(interno) {
  return interno.replace(/\\(?=\|)/g, "").split("|")[0].split("#")[0].replace(/\.md$/, "").trim();
}

function leerNotas(root) {
  return listarNotas(root).map((rel) => {
    const txt = normalizar(fs.readFileSync(path.join(root, VAULT, rel), "utf8"));
    const { fm, cuerpo } = parseFrontmatter(txt);
    return { rel, nombre: path.basename(rel, ".md"), carpeta: rel.split("/")[0], txt, fm, cuerpo };
  });
}

function estaVivo(n) {
  const fm = n.fm || {};
  if (fm.status && RETIRADOS.has(fm.status)) return false;
  if (fm.estado && /reemplazad|archiv/i.test(fm.estado)) return false;
  return true;
}

function mesDe(n) {
  // Extrae AAAA-MM del directorio o del nombre o de la fecha
  const mRel = n.rel.match(/(?:^|\/)(\d{4}-\d\d)(?:\/|$)/);
  if (mRel) return mRel[1];
  const mNom = n.nombre.match(/(\d{4}-\d\d)/);
  if (mNom) return mNom[1];
  if (n.fm && typeof n.fm.date === "string") {
    const mDate = n.fm.date.match(/^(\d{4}-\d\d)/);
    if (mDate) return mDate[1];
  }
  return null;
}

function mesAnterior(mes) {
  const [a, m] = mes.split("-").map(Number);
  return m === 1 ? `${a - 1}-12` : `${a}-${String(m - 1).padStart(2, "0")}`;
}

function mesesAbiertos(notas) {
  // Considera meses activos de 70 - Bitácora de Cambios (excluyendo subcarpetas de archivo)
  const meses = notas
    .filter((n) => n.carpeta === "70 - Bitácora de Cambios" && !n.rel.includes("/archivo/"))
    .map(mesDe)
    .filter(Boolean)
    .sort();
  if (meses.length === 0) return new Set();
  const ultimo = meses[meses.length - 1];
  return new Set([ultimo, mesAnterior(ultimo)]);
}

function lineaNota(n, sangria = "") {
  const fm = n.fm || {};
  let meta;
  if (fm.type === "sesion") {
    meta = [fm.type, fm.autor || "?", fm.agente || "?", fm.date || fm.updated || "?"];
  } else if (fm.type === "resumen") {
    meta = [fm.type, fm.autor || "?", fm.date || fm.updated || "?"];
  } else {
    let tipo = fm.type;
    if (!tipo) {
      if (n.carpeta.includes("Decisiones")) tipo = "adr";
      else if (n.carpeta.includes("Patrones")) tipo = "patron";
      else if (n.carpeta.includes("Referencia")) tipo = "referencia";
      else if (n.carpeta.includes("QA")) tipo = "qa";
      else if (n.carpeta.includes("Arquitectura")) tipo = "arquitectura";
      else if (n.carpeta.includes("Casos")) tipo = "caso-uso";
      else if (n.carpeta.includes("MOC")) tipo = "moc";
      else tipo = "doc";
    }
    const estado = fm.status || (fm.estado === "propuesto" ? "propuesto" : "vigente");
    meta = [tipo, estado];
  }
  let sum = "";
  if (fm.summary) {
    let s = fm.summary.trim();
    if (s.length > 20) s = s.slice(0, 17) + "...";
    sum = ` — ${s}`;
  }
  return `${sangria}- [[${n.nombre}]] · ${meta.join(" · ")}${sum}`;
}

function lineasBitacora(notas) {
  const abiertos = mesesAbiertos(notas);
  const notasBitacora = notas.filter((n) => n.carpeta === "70 - Bitácora de Cambios");
  const meses = [...new Set(notasBitacora.map(mesDe).filter(Boolean))].sort().reverse();
  const out = [];
  for (const mes of meses) {
    const delMes = notasBitacora.filter((n) => mesDe(n) === mes);
    const resumen = delMes.find((n) => (n.fm || {}).type === "resumen");
    const sesiones = delMes.filter((n) => (n.fm || {}).type !== "resumen").sort((a, b) => (a.nombre < b.nombre ? 1 : -1));
    if (abiertos.has(mes)) {
      if (resumen) out.push(lineaNota(resumen));
      sesiones.forEach((n) => out.push(lineaNota(n)));
    } else if (resumen) {
      out.push(lineaNota(resumen));
    } else {
      const cuantas = sesiones.length === 1 ? "1 sesión" : `${sesiones.length} sesiones`;
      out.push(`- ${mes}: ${cuantas} sin resumen mensual (buscar en 70 - Bitácora de Cambios/${mes}/)`);
    }
  }
  return out;
}

function lineasBitacoraArranque(notas) {
  const abiertos = mesesAbiertos(notas);
  const sesiones = notas
    .filter((n) => n.carpeta === "70 - Bitácora de Cambios" && (n.fm || {}).type === "sesion" && abiertos.has(mesDe(n)))
    .sort((a, b) => (a.nombre < b.nombre ? 1 : -1));
  const resumenes = notas.filter((n) => n.carpeta === "70 - Bitácora de Cambios" && (n.fm || {}).type === "resumen").length;
  const out = sesiones.slice(0, SESIONES_AL_ARRANCAR).map((n) => lineaNota(n));
  const resto = [];
  const mas = sesiones.length - SESIONES_AL_ARRANCAR;
  if (mas > 0) resto.push(mas === 1 ? "1 sesión más" : `${mas} sesiones más`);
  if (resumenes > 0) resto.push(resumenes === 1 ? "1 resumen mensual" : `${resumenes} resúmenes mensuales`);
  if (resto.length) out.push(`- _(${resto.join(" y ")}: ver contexto/INDEX-bitacora.md)_`);
  return out;
}

function generarIndiceBitacora(root) {
  const notas = leerNotas(root).filter((n) => n.carpeta === "70 - Bitácora de Cambios");
  const vivas = notas.filter(estaVivo);
  const retiradas = notas.length - vivas.length;
  const out = [
    "<!-- GENERADO por scripts/contexto/build-index.js — no editar a mano -->",
    "# Índice de la bitácora",
    "",
    "Todas las sesiones vigentes: una por una las del último mes con notas y del anterior; los meses anteriores,",
    "por su resumen mensual. No se inyecta al arrancar: se consulta cuando la pregunta es sobre el trabajo pasado.",
    "Sesiones: `sesion · autor · agente · fecha` — resumen.",
    "",
  ];
  const lineas = lineasBitacora(vivas);
  out.push(...(lineas.length ? lineas : ["_(vacía)_"]));
  if (retiradas > 0) out.push(`_(${retiradas} ${retiradas === 1 ? "nota retirada" : "notas retiradas"}: reemplazadas o archivadas, no se listan)_`);
  out.push("");
  return out.join("\n");
}

function generarIndice(root) {
  const notas = leerNotas(root);
  const out = [
    "<!-- GENERADO por scripts/contexto/build-index.js — no editar a mano -->",
    "# Índice del contexto",
    "",
    "Una línea por nota: `tipo · estado · actualizado` — resumen.",
    "Lee este índice y abre solo las 3 a 6 notas necesarias para la tarea.",
    `De bitácora, solo las ${SESIONES_AL_ARRANCAR} sesiones más recientes; el resto en contexto/INDEX-bitacora.md.`,
    "",
  ];
  const conocidas = new Set(CARPETAS.map(([c]) => c));
  const extra = [...new Set(notas.map((n) => n.carpeta))].filter((c) => !conocidas.has(c)).sort();
  for (const [carpeta, desc] of [...CARPETAS, ...extra.map((c) => [c, "Carpeta no registrada en lib.js"])]) {
    const deCarpeta = notas.filter((n) => n.carpeta === carpeta);
    const vivas = deCarpeta.filter(estaVivo);
    const retiradas = deCarpeta.length - vivas.length;
    out.push(`## ${carpeta} — ${desc}`, "");
    if (vivas.length === 0 && retiradas === 0) out.push("_(vacía)_");
    const lineas = carpeta === "70 - Bitácora de Cambios" ? lineasBitacoraArranque(vivas) : vivas.map((n) => lineaNota(n));
    out.push(...lineas);
    if (retiradas > 0) out.push(`_(${retiradas} ${retiradas === 1 ? "nota retirada" : "notas retiradas"}: reemplazadas o archivadas, no se listan)_`);
    out.push("");
  }
  return out.join("\n");
}

module.exports = {
  VAULT, INDICES, SESIONES_AL_ARRANCAR, CARPETAS, TIPOS, ESTADOS, OBLIGATORIOS, AGENTES, RETIRADOS,
  leerEquipo, listarNotas, leerNotas, normalizar, parseFrontmatter, destinoEnlace, estaVivo,
  mesDe, mesAnterior, mesesAbiertos, lineaNota, generarIndice, generarIndiceBitacora,
};
