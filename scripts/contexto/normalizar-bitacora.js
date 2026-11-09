#!/usr/bin/env node
// Normalizador idempotente de la bitácora de BimboProyecto.
//
//   node scripts/contexto/normalizar-bitacora.js            # Modo --dry-run (por defecto)
//   node scripts/contexto/normalizar-bitacora.js --aplicar   # Aplica los cambios a disco
//
"use strict";
const fs = require("fs");
const path = require("path");
const L = require("./lib.js");

const ROOT = path.resolve(__dirname, "..", "..");
const BITACORA_DIR = path.join(ROOT, L.VAULT, "70 - Bitácora de Cambios");
const CONTROL_DIR = path.join(ROOT, L.VAULT, ".control");
const PENDIENTES_FILE = path.join(CONTROL_DIR, "mapeo-autores-pendiente.md");

const APLICAR = process.argv.includes("--aplicar");

function walk(dir) {
  let res = [];
  if (!fs.existsSync(dir)) return res;
  for (const e of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, e.name);
    if (e.isDirectory()) res = res.concat(walk(full));
    else if (e.name.endsWith(".md")) res.push(full);
  }
  return res;
}

function mapearAutorAgente(autorRaw, texto) {
  const s = String(autorRaw || "").trim();

  // Emanuel
  if (/emanuel/i.test(s)) {
    if (/codex/i.test(s)) return { autor: "emanuel", agente: "codex", modelo: null };
    if (/opencode/i.test(s)) return { autor: "emanuel", agente: "opencode", modelo: null };
    if (/antigravity/i.test(s)) return { autor: "emanuel", agente: "antigravity", modelo: null };
    if (/claude/i.test(s)) return { autor: "emanuel", agente: "claude-code", modelo: null };
    return { autor: "emanuel", agente: "humano", modelo: null };
  }

  // Fernando
  if (/fernando/i.test(s)) {
    if (/claude/i.test(s)) {
      let modelo = null;
      if (/opus\s*5/i.test(s)) modelo = "opus-5";
      else if (/sonnet\s*5/i.test(s)) modelo = "sonnet-5";
      return { autor: "fernando", agente: "claude-code", modelo };
    }
    if (/antigravity/i.test(s)) return { autor: "fernando", agente: "antigravity", modelo: null };
    if (/codex/i.test(s)) return { autor: "fernando", agente: "codex", modelo: null };
    if (/opencode/i.test(s)) return { autor: "fernando", agente: "opencode", modelo: null };
    return { autor: "fernando", agente: "humano", modelo: null };
  }

  // Claude sin persona explícita
  if (/claude/i.test(s)) {
    let modelo = null;
    if (/opus\s*5/i.test(s) && /sonnet\s*5/i.test(s)) modelo = "opus-5/sonnet-5";
    else if (/opus\s*5/i.test(s)) modelo = "opus-5";
    else if (/sonnet\s*5/i.test(s)) modelo = "sonnet-5";
    return { autor: "por-confirmar", agente: "claude-code", modelo, motivo: `Claude sin autor humano explícito ('${s}')` };
  }

  // Codex sin persona explícita
  if (/codex/i.test(s)) {
    const modelo = /gpt-5/i.test(s) ? "gpt-5" : null;
    return { autor: "por-confirmar", agente: "codex", modelo, motivo: `Codex sin autor humano explícito ('${s}')` };
  }

  // Antigravity sin persona explícita
  if (/antigravity/i.test(s)) {
    const modelo = /gemini/i.test(s) ? "gemini-flash" : null;
    return { autor: "por-confirmar", agente: "antigravity", modelo, motivo: `Antigravity sin autor humano explícito ('${s}')` };
  }

  // OpenCode sin persona explícita
  if (/opencode/i.test(s)) {
    return { autor: "por-confirmar", agente: "opencode", modelo: null, motivo: `OpenCode sin autor humano explícito ('${s}')` };
  }

  // Gemini sin persona
  if (/gemini/i.test(s)) {
    return { autor: "por-confirmar", agente: "gemini-cli", modelo: null, motivo: `Gemini sin autor humano explícito ('${s}')` };
  }

  // Osmany
  if (/osmany/i.test(s)) {
    return { autor: "por-confirmar", agente: "claude-code", modelo: null, motivo: `Autor no registrado en equipo.json ('${s}')` };
  }

  // Sin autor declarado
  return { autor: "por-confirmar", agente: "humano", modelo: null, motivo: "Nota sin campo de autor en frontmatter original" };
}

function extraerCommits(texto) {
  const commits = new Set();
  // Busca patrones como "commit 404796c" o "commits 404796c y a05f006" o "(commit abc1234)" o "commit: abc1234"
  const m = texto.matchAll(/\b(?:commit|commits)\s+([0-9a-f]{7,40})(?:\s+y\s+([0-9a-f]{7,40}))?/gi);
  for (const match of m) {
    if (match[1]) commits.add(match[1].slice(0, 7));
    if (match[2]) commits.add(match[2].slice(0, 7));
  }
  return [...commits];
}

function normalizarNota(rutaArchivo) {
  const crudo = fs.readFileSync(rutaArchivo, "utf8");
  const txt = L.normalizar(crudo);
  const nombre = path.basename(rutaArchivo, ".md");
  const rel = path.relative(path.join(ROOT, L.VAULT), rutaArchivo).split(path.sep).join("/");

  // 1. Detección de fecha de la nota
  let fecha = null;
  const mFechaNombre = nombre.match(/(\d{4}-\d\d-\d\d)/);
  if (mFechaNombre) fecha = mFechaNombre[1];

  let fm = null;
  let cuerpo = txt;

  if (txt.startsWith("---\n")) {
    const fin = txt.indexOf("\n---", 3);
    if (fin !== -1) {
      const crudoFM = txt.slice(4, fin);
      cuerpo = txt.slice(fin + 4).replace(/^\n/, "");
      fm = {};
      let listaKey = null;
      for (const linea of crudoFM.split("\n")) {
        if (/^\s*(#.*)?$/.test(linea)) continue;
        const item = linea.match(/^\s*-\s+(.*)$/);
        const kv = linea.match(/^([A-Za-z_][\w-]*):\s*(.*)$/);
        if (listaKey && item) {
          fm[listaKey].push(item[1].trim().replace(/^["'](.*)["']$/, "$1"));
        } else if (kv) {
          const [, k, v] = kv;
          const val = v.trim().replace(/^["'](.*)["']$/, "$1");
          if (val === "") {
            listaKey = k;
            fm[k] = [];
          } else if (val.startsWith("[")) {
            listaKey = null;
            fm[k] = val.slice(1, -1).split(",").map((s) => s.trim().replace(/^["'](.*)["']$/, "$1")).filter(Boolean);
          } else {
            listaKey = null;
            fm[k] = val;
          }
        }
      }
    }
  }

  // Si no había frontmatter, creamos estructura base
  if (!fm) {
    fm = {};
  }

  // Título
  if (!fm.title) {
    const mTitulo = cuerpo.match(/^#\s+(.+)$/m);
    fm.title = mTitulo ? mTitulo[1].trim() : nombre;
  }

  // Tipo
  if (!fm.type || fm.type === "session-log") {
    if (nombre.endsWith("-resumen")) fm.type = "resumen";
    else fm.type = "sesion";
  }

  // Estado
  if (!fm.status || !L.ESTADOS.has(fm.status)) {
    const rawEstado = fm.status || fm.estado;
    if (rawEstado) {
      const e = String(rawEstado).toLowerCase();
      if (e.includes("completad") || e.includes("ejecutad") || e.includes("documentad") || e.includes("fase")) fm.status = "vigente";
      else if (e.includes("archiv")) fm.status = "archivado";
      else if (e.includes("pendient") || e.includes("borrador") || e.includes("progreso")) fm.status = "borrador";
      else fm.status = "vigente";
      delete fm.estado;
    } else {
      fm.status = "vigente";
    }
  }

  // Fecha (fecha -> date)
  if (fm.fecha && !fm.date) {
    fm.date = fm.fecha;
    delete fm.fecha;
  }
  if (!fm.date) {
    const mFechaCuerpo = cuerpo.match(/\*\*Fecha:\*\*\s*(\d{4}-\d\d-\d\d)/i);
    if (mFechaCuerpo) fm.date = mFechaCuerpo[1];
    else if (fecha) fm.date = fecha;
    else fm.date = "2026-05-01"; // Fallback para notas sin fecha
  }

  if (!fm.updated) {
    fm.updated = fm.date;
  }

  // Rama (branch -> rama)
  if (fm.branch && !fm.rama) {
    fm.rama = fm.branch;
    delete fm.branch;
  }

  // Commits
  const commitsDetectados = extraerCommits(cuerpo);
  if (!fm.commits) {
    fm.commits = commitsDetectados;
  } else if (!Array.isArray(fm.commits)) {
    fm.commits = [String(fm.commits).slice(0, 7)];
  }

  // Summary
  if (!fm.summary) {
    const mSuccess = cuerpo.match(/>\s*\[!success\][^\n]*\n(?:>\s*)?([^\n]+)/i);
    const mBloque = cuerpo.match(/##\s+(?:Contexto|Problema)[^\n]*\n+([^\n#]+)/i);
    if (mSuccess && mSuccess[1].trim().length > 10) {
      fm.summary = mSuccess[1].trim().replace(/^["'](.*)["']$/, "$1");
    } else if (mBloque && mBloque[1].trim().length > 10) {
      fm.summary = mBloque[1].trim().slice(0, 150).replace(/^["'](.*)["']$/, "$1");
    } else {
      fm.summary = fm.title.replace(/^Sesión\s+\d{4}-\d\d-\d\d\s*[—–-]\s*/i, "").slice(0, 120);
    }
  }

  // Mapeo autor / agente / modelo
  const autorOrig = fm.autor || fm.autor_cambios;
  const mapeo = mapearAutorAgente(autorOrig, cuerpo);

  // Si ya tiene un autor válido en equipo.json, conservarlo
  if (fm.autor && ["fernando", "emanuel", "carlos"].includes(fm.autor)) {
    // Preservar
    if (!fm.agente) fm.agente = mapeo.agente || "humano";
    if (mapeo.modelo && !fm.modelo) fm.modelo = mapeo.modelo;
  } else {
    fm.autor = mapeo.autor;
    fm.agente = mapeo.agente;
    if (mapeo.modelo) fm.modelo = mapeo.modelo;
  }

  delete fm.autor_cambios;

  // Tags
  if (!fm.tags) {
    fm.tags = [fm.type];
  } else if (!Array.isArray(fm.tags)) {
    fm.tags = [String(fm.tags)];
  }

  // Reconstrucción del frontmatter YAML limpio
  const lineasFM = ["---"];
  lineasFM.push(`title: "${fm.title.replace(/"/g, '\\"')}"`);
  lineasFM.push(`type: ${fm.type}`);
  lineasFM.push(`status: ${fm.status}`);
  lineasFM.push(`date: ${fm.date}`);
  lineasFM.push(`updated: ${fm.updated}`);
  lineasFM.push(`autor: ${fm.autor}`);
  if (fm.agente) lineasFM.push(`agente: ${fm.agente}`);
  if (fm.modelo) lineasFM.push(`modelo: ${fm.modelo}`);
  if (fm.rama) lineasFM.push(`rama: ${fm.rama}`);
  if (fm.commits && fm.commits.length) lineasFM.push(`commits: [${fm.commits.join(", ")}]`);
  if (fm.tags && fm.tags.length) lineasFM.push(`tags: [${fm.tags.join(", ")}]`);
  lineasFM.push(`summary: "${fm.summary.replace(/"/g, '\\"')}"`);
  if (fm.deuda && fm.deuda.length) lineasFM.push(`deuda: [${fm.deuda.join(", ")}]`);
  if (fm.adr && fm.adr.length) lineasFM.push(`adr: [${fm.adr.join(", ")}]`);
  lineasFM.push("---");

  const nuevoContenido = `${lineasFM.join("\n")}\n\n${cuerpo.trimStart()}`;

  return {
    rel,
    rutaArchivo,
    cambiado: nuevoContenido !== crudo,
    nuevoContenido,
    mapeo,
    esPorConfirmar: fm.autor === "por-confirmar",
    motivoPorConfirmar: mapeo.motivo || "Autor ambiguo o no registrado",
  };
}

function main() {
  const archivos = walk(BITACORA_DIR);
  console.log(`Analizando ${archivos.length} notas en 70 - Bitácora de Cambios...`);
  console.log(`Modo: ${APLICAR ? "APLICAR CAMBIOS" : "DRY-RUN (sin escribir a disco)"}`);

  let modificadas = 0;
  const pendientes = [];

  for (const archivo of archivos) {
    const res = normalizarNota(archivo);
    if (res.cambiado) {
      modificadas++;
      if (APLICAR) {
        fs.writeFileSync(archivo, res.nuevoContenido, "utf8");
      }
    }
    if (res.esPorConfirmar) {
      pendientes.push({
        rel: res.rel,
        motivo: res.motivoPorConfirmar,
      });
    }
  }

  console.log(`Notas analizadas : ${archivos.length}`);
  console.log(`Notas a modificar: ${modificadas}`);
  console.log(`Notas por-confirmar: ${pendientes.length}`);

  // Generar reporte de mapeo pendiente
  fs.mkdirSync(CONTROL_DIR, { recursive: true });
  const reporteMD = [
    "# Mapeo de Autores Pendiente de Confirmación",
    "",
    "> Este archivo es generado automáticamente por `scripts/contexto/normalizar-bitacora.js`.",
    "> Lista las notas históricas de bitácora cuyo autor humano no pudo inferirse con absoluta certeza.",
    "> Fernando o Emanuel pueden revisar esta lista y asignar el id correspondiente (`fernando`, `emanuel`, `carlos`).",
    "",
    `**Total pendientes:** ${pendientes.length}`,
    "",
    "| # | Nota de Bitácora | Motivo / Autor Original |",
    "|---|---|---|",
    ...pendientes.map((p, i) => `| ${i + 1} | \`${p.rel}\` | ${p.motivo} |`),
    "",
  ].join("\n");

  fs.writeFileSync(PENDIENTES_FILE, reporteMD, "utf8");
  console.log(`Reporte guardado en: ${path.relative(ROOT, PENDIENTES_FILE)}`);
}

main();
