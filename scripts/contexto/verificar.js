#!/usr/bin/env node
// Portón de calidad y validador del contexto para BimboProyecto.
// Adaptado desde el estándar de FOE 360 Móvil y Análogos.
//
//   node scripts/contexto/verificar.js
//
// Sale con código 1 si hay errores; código 0 si pasa.
// Avisos informan sin bloquear.
"use strict";
const fs = require("fs");
const path = require("path");
const L = require("./lib.js");

const ROOT = path.resolve(__dirname, "..", "..");
const notas = L.leerNotas(ROOT);
const errores = [];
const avisos = [];
const hoy = new Date().toISOString().slice(0, 10);

const esFecha = (s) => typeof s === "string" && /^\d{4}-\d\d-\d\d$/.test(s) && !isNaN(Date.parse(s + "T00:00:00Z"));
const sinCodigo = (t) => t.replace(/```[\s\S]*?```/g, "").replace(/`[^`\n]*`/g, "");
const sinCitas = (t) => sinCodigo(t).replace(/"[^"\n]*"/g, "").replace(/«[^»\n]*»/g, "").replace(/“[^”\n]*”/g, "");

// Rutas absolutas de disco: unidades Windows (C:\, D:\, C:/, D:/) y rutas git-bash (/c/, /d/)
const RUTA_ABSOLUTA = [
  /(?<!\w)[A-Za-z]:\\/,
  /(?<![\w:])[A-Za-z]:\/(?!\/)/,
  /(?:^|[\s(`"'])\/[a-z]\/[A-Za-z]/m,
];

// Datos personales: teléfonos de Honduras y correos fuera de dominios de prueba/sistema
const TELEFONO = /(?<![\w.,\/#\-])(?:\+?504[\s-]?)?[2389]\d{3}[\s-]?\d{4}(?![\w.,\/-])/;
const CORREO = /[a-zA-Z0-9_.+-]+@([a-zA-Z0-9-]+\.[a-zA-Z0-9-.]*[a-zA-Z]{2,})/g;
const DOMINIOS_PERMITIDOS = new Set(["example.com", "example.org", "empresa.com", "anthropic.com", "users.noreply.github.com"]);

// Patrones de secretos: tokens JWT, sb_secret_ o asignaciones de service_role
const PATRON_SECRETO = [
  /sb_secret_[a-zA-Z0-9_-]{10,}/,
  /eyJ[a-zA-Z0-9_-]{10,}\.eyJ[a-zA-Z0-9_-]{10,}\.[a-zA-Z0-9_-]{10,}/,
  /(?:service_role\s*[:=]\s*['"][a-zA-Z0-9._-]{20,}['"])/i,
];

function revisarTexto(origen, txt) {
  if (RUTA_ABSOLUTA.some((r) => r.test(txt))) {
    errores.push(`${origen}: contiene una ruta absoluta de disco`);
  }
  const tel = txt.match(TELEFONO);
  if (tel) {
    errores.push(`${origen}: posible teléfono (${tel[0].trim()}); los datos personales están prohibidos en notas`);
  }
  for (const m of txt.matchAll(CORREO)) {
    if (!DOMINIOS_PERMITIDOS.has(m[1].toLowerCase())) {
      errores.push(`${origen}: correo personal no permitido (${m[0]}); usar dominio de ejemplo`);
    }
  }
  for (const sec of PATRON_SECRETO) {
    if (sec.test(txt)) {
      errores.push(`${origen}: posible secreto o clave detectada (service_role, sb_secret_, JWT)`);
    }
  }
}

const existentes = new Set(notas.map((n) => n.nombre.toLowerCase()));
["index", "index-bitacora", "agents"].forEach((n) => existentes.add(n));
const porNombre = new Map(notas.map((n) => [n.nombre.toLowerCase(), n]));
const entrantes = new Map(notas.map((n) => [n.nombre.toLowerCase(), 0]));
const HISTORICOS = new Set(["sesion", "resumen"]);

const equipo = L.leerEquipo(ROOT);
if (!Array.isArray(equipo)) {
  errores.push("contexto/.control/equipo.json no existe o no es una lista válida");
}
const aliasValidos = new Set((equipo || []).map((p) => p.id || p.alias));

const idsADR = new Map();
const idsP = new Map();

for (const n of notas) {
  const { rel, fm, cuerpo, txt } = n;
  revisarTexto(rel, txt);

  // Detección de identificadores ADR y P en registro de deuda
  const mADR = n.nombre.match(/^(ADR-\d{3})\b/);
  if (mADR) {
    idsADR.set(mADR[1], [...(idsADR.get(mADR[1]) || []), rel]);
  }
  if (n.carpeta === "40 - Proyecto Bimbo" && n.nombre.startsWith("Deuda")) {
    for (const mP of cuerpo.matchAll(/^#{2,4}\s+(P-\d{3})\b/gm)) {
      idsP.set(mP[1], [...(idsP.get(mP[1]) || []), rel]);
    }
  }

  const esBitacora = n.carpeta === "70 - Bitácora de Cambios";
  const esNueva = (fm && fm.date && fm.date >= "2026-11-09") || n.rel.includes("2026-11-09");

  if (!fm) {
    if (esBitacora || esNueva) {
      errores.push(`${rel}: sin frontmatter YAML`);
    } else {
      avisos.push(`${rel}: sin frontmatter YAML (nota histórica anterior a 2026-11-09)`);
    }
    continue;
  }

  // Campos obligatorios:
  // - En bitácora y en cualquier nota nueva (date >= 2026-11-09) se exige el conjunto completo de L.OBLIGATORIOS.
  // - En notas históricas fuera de bitácora, se exige 'title' y se validan type/status/date/autor si están presentes.
  if (esBitacora || esNueva) {
    for (const k of L.OBLIGATORIOS) {
      if (fm[k] === undefined || fm[k] === "" || (Array.isArray(fm[k]) && fm[k].length === 0)) {
        errores.push(`${rel}: falta '${k}'`);
      }
    }
  } else {
    if (!fm.title) {
      errores.push(`${rel}: falta 'title'`);
    }
  }

  if (fm.type && !L.TIPOS.has(fm.type)) {
    errores.push(`${rel}: type '${fm.type}' no es válido (${[...L.TIPOS].join(", ")})`);
  }
  if (fm.status && !L.ESTADOS.has(fm.status)) {
    errores.push(`${rel}: status '${fm.status}' no es válido (${[...L.ESTADOS].join(", ")})`);
  }

  // Validación de autor
  if (fm.autor) {
    if (fm.autor === "por-confirmar") {
      if (fm.date && fm.date >= "2026-11-10") {
        errores.push(`${rel}: 'por-confirmar' solo se permite en notas anteriores a 2026-11-10`);
      }
    } else if (equipo && !aliasValidos.has(fm.autor)) {
      errores.push(`${rel}: autor '${fm.autor}' no está registrado en equipo.json (${[...aliasValidos].join(", ")})`);
    }
  }

  if (fm.agente) {
    if (fm.agente === "por-confirmar") {
      if (fm.date && fm.date >= "2026-11-10") {
        errores.push(`${rel}: 'por-confirmar' en agente solo se permite en notas anteriores a 2026-11-10`);
      }
    } else if (!L.AGENTES.has(fm.agente)) {
      errores.push(`${rel}: agente '${fm.agente}' no es válido (${[...L.AGENTES].join(", ")}, por-confirmar)`);
    }
  }

  // Validación de sesiones
  if (fm.type === "sesion") {
    if (!fm.agente) errores.push(`${rel}: una sesión debe declarar 'agente'`);

    // Convención de nombre para notas nuevas (desde 2026-11-09 o que sigan el patrón AAAA-MM-DD-autor-...)
    const esNombreNuevo = /^(\d{4}-\d\d-\d\d)-([a-z0-9]+)-[a-z0-9-]+$/.test(n.nombre);
    const esFechaNueva = fm.date && fm.date >= "2026-11-09";
    if (esNombreNuevo || esFechaNueva) {
      const m = n.nombre.match(/^(\d{4}-\d\d-\d\d)-([a-z0-9]+)-[a-z0-9-]+$/);
      if (!m) {
        errores.push(`${rel}: el nombre de una sesión nueva debe ser AAAA-MM-DD-autor-titulo-corto.md`);
      } else {
        if (fm.date && m[1] !== fm.date) {
          errores.push(`${rel}: la fecha del nombre (${m[1]}) no coincide con date (${fm.date})`);
        }
        if (fm.autor && m[2] !== fm.autor) {
          errores.push(`${rel}: el autor del nombre (${m[2]}) no coincide con autor (${fm.autor})`);
        }
        const esperado = `70 - Bitácora de Cambios/${m[1].slice(0, 7)}/${n.nombre}.md`;
        if (rel !== esperado) {
          errores.push(`${rel}: la sesión debe ubicarse en 70 - Bitácora de Cambios/${m[1].slice(0, 7)}/`);
        }
      }
    }
  }

  // Resumen mensual
  if (fm.type === "resumen") {
    const m = n.nombre.match(/^(\d{4}-\d\d)-resumen$/);
    if (!m || rel !== `70 - Bitácora de Cambios/${m[1]}/${n.nombre}.md`) {
      errores.push(`${rel}: un resumen mensual va en 70 - Bitácora de Cambios/AAAA-MM/AAAA-MM-resumen.md`);
    }
  }

  // Reemplazo
  if (fm.status === "reemplazado") {
    const destino = fm.reemplazado_por ? L.destinoEnlace(String(fm.reemplazado_por).replace(/^\[\[|\]\]$/g, "")) : "";
    if (!destino) errores.push(`${rel}: una nota reemplazada debe indicar 'reemplazado_por'`);
    else if (!existentes.has(destino.toLowerCase())) errores.push(`${rel}: reemplazado_por '${destino}' no existe`);
  }

  // Fechas válidas
  for (const k of ["date", "updated"]) {
    if (fm[k] && !esFecha(fm[k])) errores.push(`${rel}: ${k} no es una fecha AAAA-MM-DD válida ('${fm[k]}')`);
  }
  if (esFecha(fm.date || "") && esFecha(fm.updated || "") && fm.updated < fm.date) {
    errores.push(`${rel}: updated (${fm.updated}) es anterior a date (${fm.date})`);
  }

  // Wikilinks entrantes y rotos (avisos)
  for (const m of sinCodigo(cuerpo).matchAll(/\[\[([^\]]+)\]\]/g)) {
    const destino = L.destinoEnlace(m[1]);
    const clave = destino.toLowerCase();
    if (!existentes.has(clave)) {
      avisos.push(`${rel}: wikilink sin destino [[${destino}]]`);
      continue;
    }
    if (clave !== n.nombre.toLowerCase() && entrantes.has(clave)) {
      entrantes.set(clave, entrantes.get(clave) + 1);
    }
  }

  if (!/^##\s+Relaciones\s*$/m.test(cuerpo)) {
    avisos.push(`${rel}: sin sección '## Relaciones'`);
  }
}

// Notas huérfanas (solo aviso)
for (const n of notas) {
  const fm = n.fm || {};
  if (HISTORICOS.has(fm.type) || L.RETIRADOS.has(fm.status) || n.carpeta === "70 - Bitácora de Cambios") continue;
  if (entrantes.get(n.nombre.toLowerCase()) === 0) {
    avisos.push(`${n.rel}: ninguna nota la enlaza; conectar con wikilink si corresponde`);
  }
}

// Meses cerrados sin resumen (solo aviso)
const abiertas = L.mesesAbiertos(notas.filter((n) => !L.RETIRADOS.has((n.fm || {}).status)));
const mesesBitacora = new Map();
for (const n of notas.filter((x) => x.carpeta === "70 - Bitácora de Cambios" && !x.rel.includes("/archivo/"))) {
  const mes = L.mesDe(n);
  if (!mes) continue;
  const r = mesesBitacora.get(mes) || { resumen: false };
  if ((n.fm || {}).type === "resumen") r.resumen = true;
  mesesBitacora.set(mes, r);
}
for (const [mes, r] of mesesBitacora) {
  if (!abiertas.has(mes) && !r.resumen) {
    avisos.push(`70 - Bitácora de Cambios/${mes}: mes cerrado sin resumen (${mes}-resumen.md)`);
  }
}

// Identificadores duplicados (error)
for (const [id, rutas] of idsADR) {
  if (rutas.length > 1) errores.push(`identificador ADR ${id} repetido en: ${rutas.join(", ")}`);
}
for (const [id, rutas] of idsP) {
  if (rutas.length > 1) errores.push(`deuda ${id} aparece repetida en: ${rutas.join(", ")}`);
}

// Verificación de archivos de control
const leer = (p) => {
  try { return L.normalizar(fs.readFileSync(path.join(ROOT, p), "utf8")); } catch { return null; }
};

const agents = leer("AGENTS.md");
if (agents === null) {
  errores.push("AGENTS.md no existe en la raíz");
} else {
  revisarTexto("AGENTS.md", agents);
  const bytesAG = Buffer.byteLength(agents, "utf8");
  if (bytesAG > 8192) {
    errores.push(`AGENTS.md pesa ${bytesAG} B; el tope estricto es 8.192 B (8 KB)`);
  }
}

const indice = leer(path.join(L.VAULT, "INDEX.md"));
if (indice === null) {
  errores.push("contexto/INDEX.md no existe: correr node scripts/contexto/build-index.js");
} else {
  if (indice !== L.generarIndice(ROOT)) {
    errores.push("contexto/INDEX.md está desactualizado respecto a las notas: correr node scripts/contexto/build-index.js");
  }
  const bytesIdx = Buffer.byteLength(indice, "utf8");
  if (bytesIdx > 10240) {
    errores.push(`contexto/INDEX.md pesa ${bytesIdx} B; el tope es 10.240 B (10 KB)`);
  }
}

const indiceBitacora = leer(path.join(L.VAULT, "INDEX-bitacora.md"));
if (indiceBitacora === null) {
  errores.push("contexto/INDEX-bitacora.md no existe: correr node scripts/contexto/build-index.js");
} else if (indiceBitacora !== L.generarIndiceBitacora(ROOT)) {
  errores.push("contexto/INDEX-bitacora.md está desactualizado: correr node scripts/contexto/build-index.js");
}

// Reporte final
const arranque = Buffer.byteLength(agents || "", "utf8") + Buffer.byteLength(indice || "", "utf8");
console.log(`Contexto: ${notas.length} notas analizadas`);
console.log(`Arranque: AGENTS.md + INDEX.md = ${arranque} B (≈ ${Math.round(arranque / 4.5)} tokens estimados a 4,5 B/token)`);
console.log("");
console.log(`Errores: ${errores.length}`);
errores.forEach((e) => console.log("  ✗ " + e));
const unicosAvisos = [...new Set(avisos)];
console.log(`Avisos : ${unicosAvisos.length}`);
unicosAvisos.forEach((a) => console.log("  · " + a));

process.exit(errores.length ? 1 : 0);
