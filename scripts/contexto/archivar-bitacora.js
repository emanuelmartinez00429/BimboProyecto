#!/usr/bin/env node
// =============================================================================
// archivar-bitacora.js — Archivo de auditoría inmutable en Supabase (R4)
//
// Identifica notas de sesiones históricas de meses cerrados (>60 días) y deuda
// técnica resuelta (>60 días), genera respaldos JSONL, valida resúmenes
// mensuales y, con --aplicar, realiza upsert por sha256 en Supabase, verifica
// ida y vuelta, retira las notas con git rm y documenta en los resúmenes.
//
// Uso:
//   node scripts/contexto/archivar-bitacora.js                      # Modo --dry-run (por defecto)
//   node scripts/contexto/archivar-bitacora.js --dry-run            # Modo explícito dry-run
//   node scripts/contexto/archivar-bitacora.js --backup-dir ./bkp   # Genera respaldos JSONL
//   node scripts/contexto/archivar-bitacora.js --aplicar            # Aplica upsert, verifica y git rm
//
// Requisitos: Node 20+, sin dependencias externas.
// =============================================================================

"use strict";
const fs = require("fs");
const path = require("path");
const crypto = require("crypto");
const { execFileSync } = require("child_process");
const L = require("./lib.js");

const ROOT = path.resolve(__dirname, "..", "..");
const BITACORA_DIR = path.join(ROOT, L.VAULT, "70 - Bitácora de Cambios");
const DEUDA_RESUELTA_FILE = path.join(ROOT, L.VAULT, "40 - Proyecto Bimbo", "Deuda - Resuelta.md");

// -----------------------------------------------------------------------------
// Utilidades de Git y Hash
// -----------------------------------------------------------------------------

function obtenerCommitHead() {
  try {
    const out = execFileSync("git", ["rev-parse", "--short", "HEAD"], {
      cwd: ROOT,
      encoding: "utf8",
      stdio: ["ignore", "pipe", "ignore"],
    });
    return out.trim();
  } catch {
    return null;
  }
}

function calcularSha256(texto) {
  return crypto.createHash("sha256").update(texto, "utf8").digest("hex");
}

function fechaUltimoDiaDelMes(mesStr) {
  const [a, m] = mesStr.split("-").map(Number);
  // El día 0 del mes siguiente es el último día del mes deseado
  const ultimoDia = new Date(Date.UTC(a, m, 0));
  return ultimoDia;
}

// -----------------------------------------------------------------------------
// Identificación de meses cerrados hace más de 60 días
// -----------------------------------------------------------------------------

function calcularMesesArchivables(fechaReferenciaStr) {
  const refDate = fechaReferenciaStr ? new Date(fechaReferenciaStr + "T00:00:00Z") : new Date();
  const mesesCandidatos = [];

  if (!fs.existsSync(BITACORA_DIR)) return mesesCandidatos;

  const entradas = fs.readdirSync(BITACORA_DIR, { withFileTypes: true });
  for (const e of entradas) {
    if (e.isDirectory() && /^\d{4}-\d{2}$/.test(e.name)) {
      const mesStr = e.name;
      const finMes = fechaUltimoDiaDelMes(mesStr);
      const diffMs = refDate.getTime() - finMes.getTime();
      const diffDias = Math.floor(diffMs / (24 * 60 * 60 * 1000));
      if (diffDias >= 60) {
        mesesCandidatos.push(mesStr);
      }
    }
  }
  return mesesCandidatos.sort();
}

function validarResumenesMeses(meses) {
  const faltantes = [];
  for (const mes of meses) {
    const resumenPath = path.join(BITACORA_DIR, mes, `${mes}-resumen.md`);
    if (!fs.existsSync(resumenPath)) {
      faltantes.push({ mes, ruta: path.relative(ROOT, resumenPath).replace(/\\/g, "/") });
    }
  }
  if (faltantes.length > 0) {
    const detalle = faltantes.map((f) => `  · Mes ${f.mes}: falta ${f.ruta}`).join("\n");
    throw new Error(
      `Se detuvo el proceso de archivo: los siguientes meses cerrados carecen de resumen mensual obligatorio (AAAA-MM-resumen.md):\n${detalle}\nPor favor cree los resúmenes antes de continuar.`
    );
  }
}

// -----------------------------------------------------------------------------
// Recolección y formateo de sesiones
// -----------------------------------------------------------------------------

function recolectarSesiones(mesesArchivables, commitActual) {
  const sesiones = [];
  const rutasVistas = new Set();

  function procesarArchivo(rutaCompleta, mesAsociado) {
    const rel = path.relative(ROOT, rutaCompleta).replace(/\\/g, "/");
    if (rutasVistas.has(rel)) return;
    rutasVistas.add(rel);

    const nombreArchivo = path.basename(rutaCompleta);
    if (nombreArchivo.endsWith("-resumen.md")) return; // El resumen mensual no se archiva fuera

    const raw = fs.readFileSync(rutaCompleta, "utf8");
    const txt = L.normalizar(raw);
    const { fm, cuerpo } = L.parseFrontmatter(txt);
    const sha = calcularSha256(txt);

    // Extraer fecha
    let fecha = null;
    if (fm && fm.date && typeof fm.date === "string" && /^\d{4}-\d{2}-\d{2}/.test(fm.date)) {
      fecha = fm.date.slice(0, 10);
    } else {
      const mFecha = nombreArchivo.match(/(\d{4}-\d{2}-\d{2})/);
      if (mFecha) fecha = mFecha[1];
      else if (mesAsociado) fecha = `${mesAsociado}-01`;
      else fecha = "2026-05-01";
    }

    // Extraer campos de frontmatter con fallbacks
    const autor = (fm && fm.autor) || "por-confirmar";
    const agente = (fm && fm.agente) || "por-confirmar";
    const modelo = (fm && fm.modelo) || null;
    const rama = (fm && fm.rama) || null;

    let commits = [];
    if (fm && fm.commits) {
      if (Array.isArray(fm.commits)) commits = fm.commits.map(String);
      else if (typeof fm.commits === "string") commits = [fm.commits];
    }

    const titulo = (fm && fm.title) || path.basename(nombreArchivo, ".md");

    let tags = [];
    if (fm && fm.tags) {
      if (Array.isArray(fm.tags)) tags = fm.tags.map(String);
      else if (typeof fm.tags === "string") tags = [fm.tags];
    }

    const summary = (fm && fm.summary) || titulo;

    let deuda = [];
    if (fm && fm.deuda) {
      if (Array.isArray(fm.deuda)) deuda = fm.deuda.map(String);
      else if (typeof fm.deuda === "string") deuda = [fm.deuda];
    } else {
      const mDeuda = txt.matchAll(/\b(P-\d{3})\b/g);
      const setDeuda = new Set();
      for (const m of mDeuda) setDeuda.add(m[1]);
      deuda = [...setDeuda];
    }

    let adr = [];
    if (fm && fm.adr) {
      if (Array.isArray(fm.adr)) adr = fm.adr.map(String);
      else if (typeof fm.adr === "string") adr = [fm.adr];
    } else {
      const mAdr = txt.matchAll(/\b(ADR-\d{3})\b/g);
      const setAdr = new Set();
      for (const m of mAdr) setAdr.add(m[1]);
      adr = [...setAdr];
    }

    sesiones.push({
      date: fecha,
      autor,
      agente,
      modelo,
      rama,
      commits,
      titulo,
      tags,
      summary,
      deuda,
      adr,
      cuerpo_md: cuerpo,
      ruta_original: rel,
      sha256: sha,
      commit_archivo: commitActual || null,
      mes_grupo: mesAsociado || fecha.slice(0, 7),
      ruta_absoluta: rutaCompleta,
    });
  }

  // 1. Escanear meses archivables
  for (const mes of mesesArchivables) {
    const dirMes = path.join(BITACORA_DIR, mes);
    if (!fs.existsSync(dirMes)) continue;
    const files = fs.readdirSync(dirMes, { withFileTypes: true });
    for (const f of files) {
      if (f.isFile() && f.name.endsWith(".md")) {
        procesarArchivo(path.join(dirMes, f.name), mes);
      }
    }
  }

  // 2. Escanear carpeta archivo/ completa
  const dirArchivo = path.join(BITACORA_DIR, "archivo");
  if (fs.existsSync(dirArchivo)) {
    function walkArchivo(d) {
      for (const e of fs.readdirSync(d, { withFileTypes: true })) {
        const full = path.join(d, e.name);
        if (e.isDirectory()) {
          walkArchivo(full);
        } else if (e.isFile() && e.name.endsWith(".md")) {
          // Determinar mes asociado si la subcarpeta indica año-mes
          const mSub = full.match(/(\d{4}-\d{2})/);
          const mesSub = mSub ? mSub[1] : "2026-05";
          procesarArchivo(full, mesSub);
        }
      }
    }
    walkArchivo(dirArchivo);
  }

  return sesiones.sort((a, b) => a.date.localeCompare(b.date) || a.titulo.localeCompare(b.titulo));
}

// -----------------------------------------------------------------------------
// Recolección y formateo de Deuda Resuelta
// -----------------------------------------------------------------------------

function recolectarDeudaResuelta(fechaReferenciaStr) {
  const refDate = fechaReferenciaStr ? new Date(fechaReferenciaStr + "T00:00:00Z") : new Date();
  const resueltas = [];

  if (!fs.existsSync(DEUDA_RESUELTA_FILE)) return resueltas;

  const raw = fs.readFileSync(DEUDA_RESUELTA_FILE, "utf8");
  const txt = L.normalizar(raw);
  const sections = txt.split(/\n(?=### )/);

  for (let i = 1; i < sections.length; i++) {
    const s = sections[i].trim();
    const lines = s.split("\n");
    const header = lines[0];

    const mId = header.match(/(P-\d+)/);
    if (!mId) continue;
    const id_p = mId[1];

    // Extraer título limpio
    let rawTitle = header.replace(/^###\s+/, "").replace(/~~/g, "").trim();
    rawTitle = rawTitle.replace(/^[·-]\s*/, "").replace(/^✅\s*/, "").trim();
    rawTitle = rawTitle.replace(/^(P-\d+)\s*[·-]\s*/, "").trim();
    rawTitle = rawTitle.replace(/^✅\s*/, "").trim();
    rawTitle = rawTitle
      .replace(/\s*—\s*resuelto.*$/i, "")
      .replace(/\s*✅\s*Resuelto.*$/i, "")
      .trim();

    // Extraer fecha de resolución
    let dateStr = null;
    const mDateHeader = header.match(/(?:resuelto|verificado)?\s*(\d{4}-\d{2}-\d{2})/i);
    if (mDateHeader) {
      dateStr = mDateHeader[1];
    } else {
      const mDateBody =
        s.match(/(?:resuelto|verificado|solución aplicada)\s*(?:en|de)?\s*\(?(\d{4}-\d{2}-\d{2})\)?/i) ||
        s.match(/\b(2026-\d{2}-\d{2})\b/);
      if (mDateBody) dateStr = mDateBody[1];
      else dateStr = "2026-05-28";
    }

    const dt = new Date(dateStr + "T00:00:00Z");
    const diffDias = Math.floor((refDate.getTime() - dt.getTime()) / (24 * 60 * 60 * 1000));

    if (diffDias >= 60) {
      // Extraer prioridad si existe
      let prioridad = null;
      const mPrio = s.match(/\*\*Prioridad:\*\*\s*([^\n\r]+)/i) || s.match(/🔴\s*Crítico|Alta|Media|Baja/i);
      if (mPrio) {
        prioridad = mPrio[1] ? mPrio[1].trim() : mPrio[0].trim();
      }

      const sha = calcularSha256(s);
      resueltas.push({
        id_p,
        titulo: rawTitle,
        prioridad,
        cerrada_en: dateStr,
        cuerpo_md: s,
        sha256: sha,
        dias_cerrada: diffDias,
      });
    }
  }

  return resueltas.sort((a, b) => a.id_p.localeCompare(b.id_p));
}

// -----------------------------------------------------------------------------
// Generación de respaldos JSONL
// -----------------------------------------------------------------------------

function escribirBackups(backupDir, sesiones, deudas) {
  if (!backupDir) return [];
  const absDir = path.isAbsolute(backupDir) ? backupDir : path.resolve(ROOT, backupDir);
  if (!fs.existsSync(absDir)) {
    fs.mkdirSync(absDir, { recursive: true });
  }

  const archivosGenerados = [];

  // Agrupar sesiones por mes
  const porMes = {};
  for (const s of sesiones) {
    const m = s.mes_grupo || s.date.slice(0, 7);
    if (!porMes[m]) porMes[m] = [];
    // Objeto para DB de auditoría
    porMes[m].push({
      date: s.date,
      autor: s.autor,
      agente: s.agente,
      modelo: s.modelo,
      rama: s.rama,
      commits: s.commits,
      titulo: s.titulo,
      tags: s.tags,
      summary: s.summary,
      deuda: s.deuda,
      adr: s.adr,
      cuerpo_md: s.cuerpo_md,
      ruta_original: s.ruta_original,
      sha256: s.sha256,
      commit_archivo: s.commit_archivo,
    });
  }

  for (const [mes, lista] of Object.entries(porMes)) {
    const filename = `auditoria-${mes}.jsonl`;
    const dest = path.join(absDir, filename);
    const lineas = lista.map((item) => JSON.stringify(item)).join("\n") + "\n";
    fs.writeFileSync(dest, lineas, "utf8");
    archivosGenerados.push({ archivo: dest, cantidad: lista.length });
  }

  // Deuda resuelta
  if (deudas.length > 0) {
    const destDeuda = path.join(absDir, "auditoria-deuda-resuelta.jsonl");
    const lineasDeuda =
      deudas
        .map((d) =>
          JSON.stringify({
            id_p: d.id_p,
            titulo: d.titulo,
            prioridad: d.prioridad,
            cerrada_en: d.cerrada_en,
            cuerpo_md: d.cuerpo_md,
            sha256: d.sha256,
          })
        )
        .join("\n") + "\n";
    fs.writeFileSync(destDeuda, lineasDeuda, "utf8");
    archivosGenerados.push({ archivo: destDeuda, cantidad: deudas.length });
  }

  return archivosGenerados;
}

// -----------------------------------------------------------------------------
// Operaciones con Supabase (Modo --aplicar)
// -----------------------------------------------------------------------------

async function aplicarEnSupabase(url, key, sesiones, deudas) {
  if (!url || !key) {
    throw new Error(
      "Variables de entorno requeridas faltantes: AUDIT_SUPABASE_URL y AUDIT_SUPABASE_SERVICE_KEY deben estar definidas para ejecutar con --aplicar."
    );
  }

  const cleanUrl = url.replace(/\/+$/, "");

  console.log(`[APLICAR] Conectando a Supabase en ${cleanUrl}...`);

  // 1. Upsert de sesiones
  const payloadSesiones = sesiones.map((s) => ({
    date: s.date,
    autor: s.autor,
    agente: s.agente,
    modelo: s.modelo,
    rama: s.rama,
    commits: s.commits,
    titulo: s.titulo,
    tags: s.tags,
    summary: s.summary,
    deuda: s.deuda,
    adr: s.adr,
    cuerpo_md: s.cuerpo_md,
    ruta_original: s.ruta_original,
    sha256: s.sha256,
    commit_archivo: s.commit_archivo,
  }));

  const resSesiones = await fetch(`${cleanUrl}/rest/v1/sesiones?on_conflict=sha256`, {
    method: "POST",
    headers: {
      apikey: key,
      Authorization: `Bearer ${key}`,
      "Content-Type": "application/json",
      Prefer: "resolution=merge-duplicates,return=representation",
      "Accept-Profile": "auditoria",
      "Content-Profile": "auditoria",
    },
    body: JSON.stringify(payloadSesiones),
  });

  if (!resSesiones.ok) {
    const err = await resSesiones.text();
    throw new Error(`Error en upsert de auditoria.sesiones: ${resSesiones.status} ${err}`);
  }

  // 2. Upsert de deuda
  const payloadDeuda = deudas.map((d) => ({
    id_p: d.id_p,
    titulo: d.titulo,
    prioridad: d.prioridad,
    cerrada_en: d.cerrada_en,
    cuerpo_md: d.cuerpo_md,
    sha256: d.sha256,
  }));

  const resDeuda = await fetch(`${cleanUrl}/rest/v1/deuda_resuelta?on_conflict=sha256`, {
    method: "POST",
    headers: {
      apikey: key,
      Authorization: `Bearer ${key}`,
      "Content-Type": "application/json",
      Prefer: "resolution=merge-duplicates,return=representation",
      "Accept-Profile": "auditoria",
      "Content-Profile": "auditoria",
    },
    body: JSON.stringify(payloadDeuda),
  });

  if (!resDeuda.ok) {
    const err = await resDeuda.text();
    throw new Error(`Error en upsert de auditoria.deuda_resuelta: ${resDeuda.status} ${err}`);
  }

  // 3. Verificación de ida y vuelta: consulta de hashes
  console.log(`[APLICAR] Verificando conteo y hashes de ida y vuelta en Supabase...`);

  const resCheckSesiones = await fetch(`${cleanUrl}/rest/v1/sesiones?select=sha256`, {
    headers: {
      apikey: key,
      Authorization: `Bearer ${key}`,
      "Accept-Profile": "auditoria",
    },
  });
  if (!resCheckSesiones.ok) {
    throw new Error(`Error al verificar lectura de sesiones: ${resCheckSesiones.status}`);
  }
  const dataSesiones = await resCheckSesiones.json();
  const dbHashesSesiones = new Set(dataSesiones.map((x) => x.sha256));

  const sesionesFaltantes = sesiones.filter((s) => !dbHashesSesiones.has(s.sha256));
  if (sesionesFaltantes.length > 0) {
    throw new Error(
      `Verificación de integridad fallida: ${sesionesFaltantes.length} sesiones no coinciden en Supabase. Abortando sin modificar el repositorio.`
    );
  }

  const resCheckDeuda = await fetch(`${cleanUrl}/rest/v1/deuda_resuelta?select=sha256`, {
    headers: {
      apikey: key,
      Authorization: `Bearer ${key}`,
      "Accept-Profile": "auditoria",
    },
  });
  if (!resCheckDeuda.ok) {
    throw new Error(`Error al verificar lectura de deuda_resuelta: ${resCheckDeuda.status}`);
  }
  const dataDeuda = await resCheckDeuda.json();
  const dbHashesDeuda = new Set(dataDeuda.map((x) => x.sha256));

  const deudasFaltantes = deudas.filter((d) => !dbHashesDeuda.has(d.sha256));
  if (deudasFaltantes.length > 0) {
    throw new Error(
      `Verificación de integridad fallida: ${deudasFaltantes.length} ítems de deuda no coinciden en Supabase. Abortando sin modificar el repositorio.`
    );
  }

  console.log(`[APLICAR] Verificación 100% exitosa: coinciden todos los hashes y conteos.`);

  // 4. Actualizar resúmenes mensuales con lista de IDs archivados
  const porMes = {};
  for (const s of sesiones) {
    const m = s.mes_grupo || s.date.slice(0, 7);
    if (!porMes[m]) porMes[m] = [];
    porMes[m].push(s);
  }

  for (const [mes, lista] of Object.entries(porMes)) {
    const resumenPath = path.join(BITACORA_DIR, mes, `${mes}-resumen.md`);
    if (fs.existsSync(resumenPath)) {
      let contenidoResumen = fs.readFileSync(resumenPath, "utf8");
      if (!contenidoResumen.includes("## Sesiones archivadas en base de auditoría")) {
        let bloqueArchivadas = `\n\n## Sesiones archivadas en base de auditoría\n\n`;
        bloqueArchivadas += `> Registros migrados a la base inmutable de auditoría (\`bimbo-auditoria-dev\`). Total: **${lista.length} sesiones**.\n\n`;
        bloqueArchivadas += `| Fecha | Título | Autor | Agente | SHA256 |\n|:---|:---|:---|:---|:---|\n`;
        for (const it of lista) {
          bloqueArchivadas += `| ${it.date} | ${it.titulo} | ${it.autor} | ${it.agente} | \`${it.sha256.slice(0, 8)}\` |\n`;
        }
        contenidoResumen = contenidoResumen.trimEnd() + bloqueArchivadas;
        fs.writeFileSync(resumenPath, contenidoResumen, "utf8");
      }
    }
  }

  // 5. git rm de las notas archivadas
  console.log(`[APLICAR] Ejecutando git rm de ${sesiones.length} notas de sesión archivadas...`);
  for (const s of sesiones) {
    try {
      execFileSync("git", ["rm", "-f", s.ruta_original], {
        cwd: ROOT,
        stdio: ["ignore", "pipe", "pipe"],
      });
    } catch (e) {
      console.warn(`Aviso: git rm falló para ${s.ruta_original}:`, e.message);
    }
  }

  console.log(`[APLICAR] Proceso de archivo completado con éxito.`);
}

// -----------------------------------------------------------------------------
// Función Principal
// -----------------------------------------------------------------------------

async function main() {
  const args = process.argv.slice(2);
  const aplicar = args.includes("--aplicar");
  const dryRun = !aplicar || args.includes("--dry-run");

  if (args.includes("--help") || args.includes("-h")) {
    console.log(`
Uso: node scripts/contexto/archivar-bitacora.js [opciones]

Opciones:
  --dry-run                     Ejecuta en modo simulación (por defecto). No modifica BD ni repo.
  --aplicar                     Ejecuta upsert en Supabase, verifica integridad y retira notas con git rm.
  --backup-dir <ruta>           Exporta respaldo de auditoría en formato JSONL en la ruta indicada.
  --fecha-referencia <AAAA-MM-DD>  Fija fecha de referencia para el cálculo de 60 días (ej. 2026-11-09).
  --help, -h                    Muestra esta ayuda.
`);
    process.exit(0);
  }

  let backupDir = null;
  const idxBackup = args.indexOf("--backup-dir");
  if (idxBackup !== -1 && args[idxBackup + 1]) {
    backupDir = args[idxBackup + 1];
  }

  let fechaRef = "2026-11-09";
  const idxFecha = args.indexOf("--fecha-referencia");
  if (idxFecha !== -1 && args[idxFecha + 1]) {
    fechaRef = args[idxFecha + 1];
  }

  console.log("=============================================================================");
  console.log(" ARCHIVADOR DE AUDITORÍA (SUPABASE WORM) — PROYECTO BIMBO");
  console.log(` Fecha de corte (referencia): ${fechaRef}`);
  console.log(` Modo de ejecución: ${aplicar ? "🚀 APLICAR (Upsert + git rm)" : "🔍 DRY-RUN (Simulación segura)"}`);
  console.log("=============================================================================");

  const commitHead = obtenerCommitHead();
  console.log(`Commit HEAD actual: ${commitHead || "desconocido"}`);

  // 1. Meses cerrados hace >60 días
  const meses = calcularMesesArchivables(fechaRef);
  console.log(`\nMeses cerrados hace >60 días detectados (${meses.length}): ${meses.join(", ")}`);
  console.log(`Carpeta histórica adicional incluida: '70 - Bitácora de Cambios/archivo/'`);

  // 2. Validar resúmenes mensuales
  try {
    validarResumenesMeses(meses);
    console.log("✔ Todos los meses cerrados cuentan con su archivo de resumen obligatorio (AAAA-MM-resumen.md).");
  } catch (err) {
    console.error(`\n❌ ${err.message}`);
    process.exit(1);
  }

  // 3. Recolectar sesiones
  const sesiones = recolectarSesiones(meses, commitHead);
  console.log(`\nTotal de notas de sesión identificadas para archivo: ${sesiones.length}`);

  const conteoPorMes = {};
  for (const s of sesiones) {
    const m = s.mes_grupo;
    conteoPorMes[m] = (conteoPorMes[m] || 0) + 1;
  }
  for (const [m, c] of Object.entries(conteoPorMes)) {
    console.log(`  · ${m}: ${c} sesiones`);
  }

  // 4. Recolectar deuda resuelta cerrada hace >60 días
  const deudas = recolectarDeudaResuelta(fechaRef);
  console.log(`\nTotal de ítems de deuda técnica resuelta (>60 días): ${deudas.length}`);
  if (deudas.length > 0) {
    console.log(
      `  Rango de IDs: ${deudas[0].id_p} a ${deudas[deudas.length - 1].id_p} (cerradas entre ${deudas[0].cerrada_en} y ${deudas[deudas.length - 1].cerrada_en})`
    );
  }

  // 5. Respaldos JSONL
  if (backupDir) {
    console.log(`\nGenerando respaldos JSONL en '${backupDir}'...`);
    const bkps = escribirBackups(backupDir, sesiones, deudas);
    for (const b of bkps) {
      console.log(`  ✔ ${path.basename(b.archivo)} (${b.cantidad} registros)`);
    }
  }

  // 6. Aplicar o Simular
  if (aplicar) {
    const url = process.env.AUDIT_SUPABASE_URL;
    const key = process.env.AUDIT_SUPABASE_SERVICE_KEY;
    try {
      await aplicarEnSupabase(url, key, sesiones, deudas);
      console.log("\n✔ Ejecución completada exitosamente.");
    } catch (e) {
      console.error(`\n❌ Fallo en ejecución con --aplicar: ${e.message}`);
      process.exit(1);
    }
  } else {
    console.log("\n-----------------------------------------------------------------------------");
    console.log(" RESUMEN DRY-RUN:");
    console.log(`   · Sesiones a archivar : ${sesiones.length}`);
    console.log(`   · Deudas a archivar   : ${deudas.length}`);
    console.log(`   · Total registros     : ${sesiones.length + deudas.length}`);
    console.log(" No se realizaron llamadas a la red ni modificaciones en el repositorio.");
    console.log(" Para ejecutar la ingestión real, use: --aplicar con credenciales en entorno.");
    console.log("-----------------------------------------------------------------------------");
  }
}

if (require.main === module) {
  main().catch((err) => {
    console.error("Error inesperado:", err);
    process.exit(1);
  });
}

module.exports = {
  calcularMesesArchivables,
  validarResumenesMeses,
  recolectarSesiones,
  recolectarDeudaResuelta,
  escribirBackups,
  calcularSha256,
};
