#!/usr/bin/env node
// Analiza las corridas de medir-arranque.sh / medir-bateria.sh.
//
//   node comparar.js arranque <dir>
//   node comparar.js bateria  <dir> <bateria.jsonl>
//   node comparar.js ab       <dirAntes> <dirDespues> <bateria.jsonl>
//
// No asume el esquema exacto del JSON de Claude Code: busca los campos de
// tokens donde esten y avisa que ruta uso, para que puedas verificarlo.

const fs = require("fs");
const path = require("path");

const CAMPOS = {
  input_tokens: "entrada",
  output_tokens: "salida",
  cache_creation_input_tokens: "cacheEscrito",
  cache_read_input_tokens: "cacheLeido",
};

// Recorre el objeto y junta los campos de tokens. Si aparecen a varias
// profundidades (resumen + desglose por modelo), se queda con la mas
// superficial para no contar dos veces.
function tokens(obj) {
  const hallazgos = [];
  (function walk(n, prof, ruta) {
    if (!n || typeof n !== "object") return;
    for (const [k, v] of Object.entries(n)) {
      if (k in CAMPOS && typeof v === "number") {
        hallazgos.push({ prof, ruta: ruta || "(raiz)", campo: k, valor: v });
      } else if (v && typeof v === "object") {
        walk(v, prof + 1, ruta ? `${ruta}.${k}` : k);
      }
    }
  })(obj, 0, "");
  if (!hallazgos.length) return { total: 0, _ruta: "no encontrado" };
  const min = Math.min(...hallazgos.map((h) => h.prof));
  const t = { entrada: 0, salida: 0, cacheEscrito: 0, cacheLeido: 0 };
  const rutas = new Set();
  for (const h of hallazgos.filter((x) => x.prof === min)) {
    t[CAMPOS[h.campo]] += h.valor;
    rutas.add(h.ruta);
  }
  t.total = t.entrada + t.salida + t.cacheEscrito + t.cacheLeido;
  t.contexto = t.entrada + t.cacheEscrito + t.cacheLeido; // lo que ocupa ventana
  t._ruta = [...rutas].join(", ");
  return t;
}

const costo = (o) =>
  typeof o.total_cost_usd === "number" ? o.total_cost_usd
  : typeof o.cost_usd === "number" ? o.cost_usd
  : null;

const texto = (o) =>
  typeof o.result === "string" ? o.result
  : typeof o.text === "string" ? o.text
  : JSON.stringify(o);

const leer = (f) => { try { return JSON.parse(fs.readFileSync(f, "utf8")); } catch { return null; } };
const norm = (s) => s.normalize("NFD").replace(/[̀-ͯ]/g, "").toLowerCase();
const mediana = (a) => { const s=[...a].sort((x,y)=>x-y); const m=s.length>>1;
  return s.length%2 ? s[m] : (s[m-1]+s[m])/2; };
const n0 = (x) => String(Math.round(x)).replace(/\B(?=(\d{3})+(?!\d))/g, ".");
const usd = (x) => x == null ? "—" : "$" + x.toFixed(4);

function cargar(dir, filtro = () => true) {
  return fs.readdirSync(dir)
    .filter((f) => f.endsWith(".json") && filtro(f))
    .map((f) => ({ archivo: f, json: leer(path.join(dir, f)) }))
    .filter((x) => x.json);
}

// ---------------------------------------------------------------- arranque
function arranque(dir) {
  const brazo = (p) => {
    const runs = cargar(dir, (f) => f.startsWith(p)).map((x) => ({
      t: tokens(x.json), c: costo(x.json),
    }));
    if (!runs.length) return null;
    return {
      n: runs.length,
      contexto: mediana(runs.map((r) => r.t.contexto)),
      entrada: mediana(runs.map((r) => r.t.entrada)),
      cacheEscrito: mediana(runs.map((r) => r.t.cacheEscrito)),
      cacheLeido: mediana(runs.map((r) => r.t.cacheLeido)),
      costo: mediana(runs.map((r) => r.c ?? 0)),
      ruta: runs[0].t._ruta,
    };
  };
  const normal = brazo("normal"), bare = brazo("bare");
  console.log("\n=== COSTO FIJO DE ARRANQUE (mediana de %d corridas) ===\n", normal?.n ?? 0);
  console.log("  campos leidos de:", normal?.ruta ?? "—", "\n");
  const fila = (et, b) => b &&
    console.log(`  ${et.padEnd(22)} ${n0(b.contexto).padStart(9)} tok   ${usd(b.costo).padStart(9)}`);
  fila("normal (con boveda)", normal);
  fila("piso (dir vacio)", bare);
  if (normal && bare) {
    const d = normal.contexto - bare.contexto;
    console.log("  " + "-".repeat(46));
    console.log(`  ${"lo que cuesta tu contrato".padEnd(22)} ${n0(d).padStart(9)} tok   ${usd((normal.costo||0)-(bare.costo||0)).padStart(9)}`);
    console.log(`\n  → ${n0(d)} tokens es el numero honesto: AGENTS.md +`);
    console.log("     lo que inyecta el hook SessionStart, con el tokenizador real.");
    console.log("     Guardalo y volve a correr esto despues del cambio.\n");
  }
}

// ---------------------------------------------------------------- bateria
function analizarBateria(dir, bateriaPath) {
  const preguntas = fs.readFileSync(bateriaPath, "utf8").trim().split("\n")
    .filter(Boolean).map((l) => JSON.parse(l));
  const porId = new Map(preguntas.map((p) => [p.id, p]));
  const acc = new Map();

  for (const { archivo, json } of cargar(dir)) {
    const id = archivo.split("-r")[0];
    const p = porId.get(id); if (!p) continue;
    const t = tokens(json), c = costo(json), r = norm(texto(json));
    const aciertos = p.espera.filter((e) => r.includes(norm(e))).length;
    if (!acc.has(id)) acc.set(id, { p, runs: [] });
    acc.get(id).runs.push({
      contexto: t.contexto, total: t.total, costo: c ?? 0,
      ok: aciertos === p.espera.length, frac: aciertos / p.espera.length,
    });
  }
  return { acc, preguntas };
}

function resumen(acc) {
  let ctx = 0, cst = 0, ok = 0, n = 0;
  for (const { runs } of acc.values()) {
    ctx += mediana(runs.map((r) => r.contexto));
    cst += mediana(runs.map((r) => r.costo));
    ok  += runs.filter((r) => r.ok).length / runs.length;
    n++;
  }
  return { ctx, cst, ok, n, porCorrecta: ok > 0 ? ctx / ok : Infinity };
}

function bateria(dir, bateriaPath) {
  const { acc } = analizarBateria(dir, bateriaPath);
  console.log("\n=== BATERIA (mediana por pregunta) ===\n");
  console.log("  id     mide                        contexto     costo   acierto");
  console.log("  " + "-".repeat(63));
  for (const [id, { p, runs }] of [...acc].sort()) {
    const c = mediana(runs.map((r) => r.contexto));
    const $ = mediana(runs.map((r) => r.costo));
    const a = runs.filter((r) => r.ok).length + "/" + runs.length;
    console.log(`  ${id}   ${(p.mide||"").padEnd(26)} ${n0(c).padStart(8)}  ${usd($).padStart(8)}   ${a.padStart(5)}`);
  }
  const s = resumen(acc);
  console.log("  " + "-".repeat(63));
  console.log(`  TOTAL  ${String(s.n+" preguntas").padEnd(26)} ${n0(s.ctx).padStart(8)}  ${usd(s.cst).padStart(8)}   ${s.ok.toFixed(1)}/${s.n}`);
  console.log(`\n  tokens por respuesta correcta: ${n0(s.porCorrecta)}`);
  console.log("  (esta es LA metrica: bajar tokens rompiendo respuestas no es una mejora)\n");
}

// ---------------------------------------------------------------- A/B
function ab(dirA, dirB, bateriaPath) {
  const A = analizarBateria(dirA, bateriaPath), B = analizarBateria(dirB, bateriaPath);
  const sA = resumen(A.acc), sB = resumen(B.acc);
  const pct = (a, b) => (a === 0 ? "—" : (((b - a) / a) * 100).toFixed(1).replace("-", "−") + " %");

  console.log("\n=== ANTES vs DESPUES ===\n");
  console.log("  id     mide                          antes   despues    cambio");
  console.log("  " + "-".repeat(66));
  for (const [id, { p, runs }] of [...A.acc].sort()) {
    const b = B.acc.get(id); if (!b) continue;
    const ca = mediana(runs.map((r) => r.contexto));
    const cb = mediana(b.runs.map((r) => r.contexto));
    const okA = runs.filter((r) => r.ok).length / runs.length;
    const okB = b.runs.filter((r) => r.ok).length / b.runs.length;
    const alerta = okB < okA ? "  ⚠ acierto bajo" : "";
    console.log(`  ${id}   ${(p.mide||"").padEnd(26)} ${n0(ca).padStart(7)} ${n0(cb).padStart(9)} ${pct(ca,cb).padStart(9)}${alerta}`);
  }
  console.log("  " + "-".repeat(66));
  console.log(`  contexto total          ${n0(sA.ctx).padStart(13)} ${n0(sB.ctx).padStart(9)} ${pct(sA.ctx,sB.ctx).padStart(9)}`);
  console.log(`  costo total             ${usd(sA.cst).padStart(13)} ${usd(sB.cst).padStart(9)} ${pct(sA.cst,sB.cst).padStart(9)}`);
  console.log(`  respuestas correctas    ${(sA.ok.toFixed(1)+"/"+sA.n).padStart(13)} ${(sB.ok.toFixed(1)+"/"+sB.n).padStart(9)}`);
  console.log(`  tokens/correcta         ${n0(sA.porCorrecta).padStart(13)} ${n0(sB.porCorrecta).padStart(9)} ${pct(sA.porCorrecta,sB.porCorrecta).padStart(9)}`);

  const mejora = sB.porCorrecta < sA.porCorrecta * 0.6;
  const sinRegresion = sB.ok >= sA.ok - 0.5;
  console.log("\n  VEREDICTO");
  console.log(`   ${sinRegresion ? "✓" : "✗"} el acierto no bajo`);
  console.log(`   ${mejora ? "✓" : "✗"} tokens/correcta bajo al menos 40 %`);
  console.log(`   ${sinRegresion && mejora ? "→ el cambio se sostiene" : "→ el cambio NO cumple lo prometido"}\n`);
}

const [modo, ...args] = process.argv.slice(2);
if (modo === "arranque") arranque(args[0]);
else if (modo === "bateria") bateria(args[0], args[1]);
else if (modo === "ab") ab(args[0], args[1], args[2]);
else { console.log("modos: arranque <dir> | bateria <dir> <bateria.jsonl> | ab <antes> <despues> <bateria.jsonl>"); process.exit(1); }
