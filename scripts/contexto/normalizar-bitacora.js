#!/usr/bin/env node
// Normalizador idempotente de la bitácora de BimboProyecto.
//
//   node scripts/contexto/normalizar-bitacora.js            # Modo --dry-run (por defecto)
//   node scripts/contexto/normalizar-bitacora.js --aplicar   # Aplica los cambios a disco
//
"use strict";
const fs = require("fs");
const path = require("path");
const { execFileSync } = require("child_process");
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

// 1. Obtener historial git de creación para todas las notas en una sola ejecución eficiente
function obtenerHistorialGit() {
  const mapa = new Map();
  try {
    const out = execFileSync(
      "git",
      ["-c", "core.quotepath=false", "log", "--diff-filter=A", "--name-only", "--format=COMMIT:%an|%(trailers:key=Co-Authored-By,valueonly)", "--", "contexto/70 - Bitácora de Cambios"],
      { cwd: ROOT, encoding: "utf8" }
    );
    const commits = out.split("COMMIT:");
    for (const c of commits) {
      if (!c.trim()) continue;
      const lines = c.split("\n");
      const [header, ...files] = lines;
      const parts = header.split("|");
      const an = parts[0].trim();
      const tr = parts.slice(1).join("|").trim();
      for (const f of files) {
        const rf = f.trim().replace(/\\/g, "/");
        if (rf && rf.endsWith(".md") && !mapa.has(rf)) {
          mapa.set(rf, { an, tr });
        }
      }
    }
  } catch (e) {
    console.warn("Aviso: no se pudo consultar el historial git:", e.message);
  }
  return mapa;
}

const MAPA_GIT = obtenerHistorialGit();

function extraerCommits(texto) {
  const commits = new Set();
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
  const rutaGit = `contexto/${rel}`;

  // Extraer texto histórico original si existe en git antes de normalización
  let txtOriginal = txt;
  try {
    txtOriginal = execFileSync("git", ["show", `HEAD~1:${rutaGit}`], { cwd: ROOT, encoding: "utf8", stdio: ["ignore", "pipe", "ignore"] });
  } catch {}

  let fecha = null;
  const mFechaNombre = nombre.match(/(\d{4}-\d\d-\d\d)/);
  if (mFechaNombre) fecha = mFechaNombre[1];

  let { fm, cuerpo } = L.parseFrontmatter(txt);
  if (!fm) fm = {};

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

  // Fecha
  if (fm.fecha && !fm.date) {
    fm.date = fm.fecha;
    delete fm.fecha;
  }
  if (!fm.date) {
    const mFechaCuerpo = cuerpo.match(/\*\*Fecha:\*\*\s*(\d{4}-\d\d-\d\d)/i);
    if (mFechaCuerpo) fm.date = mFechaCuerpo[1];
    else if (fecha) fm.date = fecha;
    else fm.date = "2026-05-01";
  }

  if (!fm.updated) {
    fm.updated = fm.date;
  }

  // Rama
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

  // -------------------------------------------------------------
  // MAPEO AUTOR Y AGENTE
  // -------------------------------------------------------------
  // Regla 1: Autor de la nota original si tenía certeza
  const mAutorOrig = txtOriginal.match(/autor(?:_cambios)?:\s*([^\n]+)/i) || txtOriginal.match(/\*\*(?:Autor(?:_cambios)?|Responsable):\*\*\s*([^\n]+)/i);
  const autorRaw = mAutorOrig ? mAutorOrig[1].trim() : (fm.autor && fm.autor !== "por-confirmar" && !fm.autor_fuente ? fm.autor : "");
  const mAgenteOrig = txtOriginal.match(/agente:\s*([^\n]+)/i) || txtOriginal.match(/\*\*(?:Agente|Modelo):\*\*\s*([^\n]+)/i);
  const agenteRaw = mAgenteOrig ? mAgenteOrig[1].trim() : (fm.agente && fm.agente !== "humano" && fm.agente !== "por-confirmar" ? fm.agente : "");

  let autor = null;
  let autorFuente = null;
  let agente = null;
  let modelo = null;

  if (/fernando/i.test(autorRaw)) autor = "fernando";
  else if (/emanuel/i.test(autorRaw)) autor = "emanuel";
  else if (/carlos/i.test(autorRaw)) autor = "carlos";

  // Agente declarado explícitamente en la nota o frontmatter
  if (/claude/i.test(autorRaw) || /claude/i.test(agenteRaw)) {
    agente = "claude-code";
    if (/opus\s*5/i.test(autorRaw) || /opus\s*5/i.test(agenteRaw)) modelo = "opus-5";
    else if (/sonnet\s*5/i.test(autorRaw) || /sonnet\s*5/i.test(agenteRaw)) modelo = "sonnet-5";
  } else if (/codex/i.test(autorRaw) || /codex/i.test(agenteRaw)) {
    agente = "codex";
    if (/gpt-5/i.test(autorRaw) || /gpt-5/i.test(agenteRaw)) modelo = "gpt-5";
  } else if (/antigravity/i.test(autorRaw) || /antigravity/i.test(agenteRaw)) {
    agente = "antigravity";
    if (/gemini/i.test(autorRaw) || /gemini/i.test(agenteRaw)) modelo = "gemini-2.5-pro";
  } else if (/opencode/i.test(autorRaw) || /opencode/i.test(agenteRaw)) {
    agente = "opencode";
  } else if (/\bhumano\b/i.test(autorRaw) || /\bhumano\b/i.test(agenteRaw)) {
    agente = "humano";
  }

  // Regla 2: Fallback a historial git para autor
  const gitInfo = MAPA_GIT.get(rutaGit) || { an: "", tr: "" };
  if (!autor) {
    if (["RedFer855", "Fernando"].includes(gitInfo.an)) {
      autor = "fernando";
      autorFuente = "git";
    } else if (["warthunderlover", "emanuelmartinez00429"].includes(gitInfo.an)) {
      autor = "emanuel";
      autorFuente = "git";
    } else if (["sanchezcarlosvalle-arch"].includes(gitInfo.an)) {
      autor = "carlos";
      autorFuente = "git";
    } else {
      autor = "por-confirmar";
    }
  }

  // Regla 3: Fallback a trailer Co-Authored-By de git para agente
  if (!agente) {
    if (/claude/i.test(gitInfo.tr)) {
      agente = "claude-code";
      if (/opus\s*5/i.test(gitInfo.tr) && /sonnet\s*5/i.test(gitInfo.tr)) modelo = "opus-5/sonnet-5";
      else if (/opus\s*5/i.test(gitInfo.tr)) modelo = "opus-5";
      else if (/sonnet\s*5/i.test(gitInfo.tr)) modelo = "sonnet-5";
      else if (/haiku\s*4\.5/i.test(gitInfo.tr)) modelo = "haiku-4.5";
      else if (/fable\s*5/i.test(gitInfo.tr)) modelo = "fable-5";
    } else {
      agente = "por-confirmar";
    }
  }

  fm.autor = autor;
  if (autorFuente) fm.autor_fuente = autorFuente;
  else delete fm.autor_fuente;

  fm.agente = agente;
  if (modelo) fm.modelo = modelo;
  else if (!modelo && fm.modelo && agente === "por-confirmar") delete fm.modelo;

  delete fm.autor_cambios;

  // Tags
  if (!fm.tags) {
    fm.tags = [fm.type];
  } else if (!Array.isArray(fm.tags)) {
    fm.tags = [String(fm.tags)];
  }

  const limpiarComillas = (str) => String(str || "").replace(/\\+"/g, '"').replace(/"/g, '\\"');

  // Reconstrucción de frontmatter YAML
  const lineasFM = ["---"];
  lineasFM.push(`title: "${limpiarComillas(fm.title)}"`);
  lineasFM.push(`type: ${fm.type}`);
  lineasFM.push(`status: ${fm.status}`);
  lineasFM.push(`date: ${fm.date}`);
  lineasFM.push(`updated: ${fm.updated}`);
  lineasFM.push(`autor: ${fm.autor}`);
  if (fm.autor_fuente) lineasFM.push(`autor_fuente: ${fm.autor_fuente}`);
  lineasFM.push(`agente: ${fm.agente}`);
  if (fm.modelo) lineasFM.push(`modelo: ${fm.modelo}`);
  if (fm.rama) lineasFM.push(`rama: ${fm.rama}`);
  if (fm.commits && fm.commits.length) lineasFM.push(`commits: [${[...new Set(fm.commits)].join(", ")}]`);
  if (fm.tags && fm.tags.length) lineasFM.push(`tags: [${fm.tags.join(", ")}]`);
  lineasFM.push(`summary: "${limpiarComillas(fm.summary)}"`);
  if (fm.deuda && fm.deuda.length) lineasFM.push(`deuda: [${fm.deuda.join(", ")}]`);
  if (fm.adr && fm.adr.length) lineasFM.push(`adr: [${fm.adr.join(", ")}]`);
  lineasFM.push("---");

  const nuevoContenido = `${lineasFM.join("\n")}\n\n${cuerpo.trimStart()}`;

  return {
    rel,
    rutaArchivo,
    cambiado: nuevoContenido !== crudo,
    nuevoContenido,
    fm,
    esPorConfirmar: fm.autor === "por-confirmar",
    esInferidoGit: fm.autor_fuente === "git",
    motivo: fm.autor === "por-confirmar" ? `Commit autor '${gitInfo.an || "desconocido"}' sin mapeo seguro` : `Inferido del commit autor '${gitInfo.an}'`,
  };
}

function main() {
  const archivos = walk(BITACORA_DIR);
  console.log(`Analizando ${archivos.length} notas en 70 - Bitácora de Cambios...`);
  console.log(`Modo: ${APLICAR ? "APLICAR CAMBIOS" : "DRY-RUN (sin escribir a disco)"}`);

  let modificadas = 0;
  const porConfirmarList = [];
  const inferidosGitList = [];
  const statsAutores = {};
  const statsAgentes = {};

  for (const archivo of archivos) {
    const res = normalizarNota(archivo);
    statsAutores[res.fm.autor] = (statsAutores[res.fm.autor] || 0) + 1;
    statsAgentes[res.fm.agente] = (statsAgentes[res.fm.agente] || 0) + 1;

    if (res.cambiado) {
      modificadas++;
      if (APLICAR) {
        fs.writeFileSync(archivo, res.nuevoContenido, "utf8");
      }
    }

    if (res.esPorConfirmar) {
      porConfirmarList.push({ rel: res.rel, motivo: res.motivo });
    } else if (res.esInferidoGit) {
      inferidosGitList.push({ rel: res.rel, autor: res.fm.autor, motivo: res.motivo });
    }
  }

  console.log("");
  console.log(`Notas analizadas : ${archivos.length}`);
  console.log(`Notas modificadas: ${modificadas}`);
  console.log("Distribución de autores:", statsAutores);
  console.log("Distribución de agentes:", statsAgentes);
  console.log(`Notas con autor por-confirmar: ${porConfirmarList.length}`);
  console.log(`Notas con autor inferido de git: ${inferidosGitList.length}`);

  // Generar reporte de mapeo en .control
  fs.mkdirSync(CONTROL_DIR, { recursive: true });
  const reporteMD = [
    "# Mapeo de Autores y Agentes — Pendientes y Auditoría",
    "",
    "> Generado por `scripts/contexto/normalizar-bitacora.js`.",
    "> La atribución de sesiones sigue el principio de no inventar datos:",
    "> Si no hay certeza en el texto o git, se asigna `por-confirmar`.",
    "> Quien hace el commit en git no siempre es quien llevó la sesión.",
    "",
    `**Fecha de generación:** ${new Date().toISOString().slice(0, 10)}`,
    `**Total por-confirmar:** ${porConfirmarList.length}`,
    `**Total inferidos de git:** ${inferidosGitList.length}`,
    "",
    "## 1. Notas con autor por-confirmar (requieren revisión humana)",
    "",
    "| # | Nota de Bitácora | Detalle |",
    "|---|---|---|",
    ...porConfirmarList.map((p, i) => `| ${i + 1} | \`${p.rel}\` | ${p.motivo} |`),
    "",
    "## 2. Notas con autor inferido de git (`autor_fuente: git` — revisar)",
    "",
    "| # | Nota de Bitácora | Autor Asignado | Detalle Git |",
    "|---|---|---|---|",
    ...inferidosGitList.map((p, i) => `| ${i + 1} | \`${p.rel}\` | \`${p.autor}\` | ${p.motivo} |`),
    "",
  ].join("\n");

  fs.writeFileSync(PENDIENTES_FILE, reporteMD, "utf8");
  console.log(`Reporte guardado en: ${path.relative(ROOT, PENDIENTES_FILE)}`);
}

main();
