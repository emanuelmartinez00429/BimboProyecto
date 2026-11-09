"use strict";
// Pruebas unitarias de lib.js para BimboProyecto.
// Correr con: node --test scripts/contexto/
const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("fs");
const os = require("os");
const path = require("path");
const L = require("./lib.js");

const ROOT = path.resolve(__dirname, "..", "..");

// Crea un vault temporal para pruebas aisladas
function crearVaultTemp(notas) {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), "bimbo-contexto-"));
  const vaultDir = path.join(root, "contexto");
  fs.mkdirSync(vaultDir, { recursive: true });
  for (const [rel, fm, cuerpo] of notas) {
    const full = path.join(vaultDir, rel);
    fs.mkdirSync(path.dirname(full), { recursive: true });
    let txt = "";
    if (fm) {
      txt += "---\n";
      for (const [k, v] of Object.entries(fm)) {
        if (Array.isArray(v)) txt += `${k}: [${v.join(", ")}]\n`;
        else txt += `${k}: ${v}\n`;
      }
      txt += "---\n\n";
    }
    txt += cuerpo || `# ${path.basename(rel, ".md")}\n`;
    fs.writeFileSync(full, txt, "utf8");
  }
  return root;
}

const fmBasico = (type, status, summary, extra = {}) => ({
  title: `"Nota de prueba"`,
  type,
  status,
  date: "2026-05-01",
  updated: "2026-11-09",
  summary: `"${summary}"`,
  autor: "fernando",
  ...extra,
});

test("normalizar elimina BOM y normaliza saltos CRLF a LF", () => {
  const crlfConBOM = "\uFEFFlinea1\r\nlinea2\r\n";
  const norm = L.normalizar(crlfConBOM);
  assert.equal(norm, "linea1\nlinea2\n");
});

test("parseFrontmatter extrae campos escalares, listas en línea y cuerpo", () => {
  const doc = `---\ntitle: "Test"\ntags: [a, b, c]\ndate: 2026-11-09\n---\n# Encabezado\nContenido`;
  const { fm, cuerpo } = L.parseFrontmatter(doc);
  assert.ok(fm);
  assert.equal(fm.title, "Test");
  assert.deepEqual(fm.tags, ["a", "b", "c"]);
  assert.equal(fm.date, "2026-11-09");
  assert.ok(cuerpo.includes("# Encabezado"));
});

test("destinoEnlace limpia alias, anclas y escapes de pipe", () => {
  assert.equal(L.destinoEnlace("Nota|Alias"), "Nota");
  assert.equal(L.destinoEnlace("Nota#seccion"), "Nota");
  assert.equal(L.destinoEnlace("Nota\\|Alias"), "Nota");
  assert.equal(L.destinoEnlace("Nota.md"), "Nota");
});

test("listarNotas ignora carpetas excluidas e índices generados", () => {
  const root = crearVaultTemp([
    ["INDEX.md", null, "indice"],
    ["INDEX-bitacora.md", null, "indice bitacora"],
    [".obsidian/cache.json", null, "{}"],
    ["_templates/plantilla.md", null, "plantilla"],
    ["40 - Proyecto Bimbo/Arquitectura Actual.md", fmBasico("arquitectura", "vigente", "Arquitectura viva"), "# Arquitectura"],
    ["45 - Decisiones/ADR-001 - Test.md", fmBasico("adr", "vigente", "ADR 1"), "# ADR 1"],
  ]);

  const notas = L.listarNotas(root);
  assert.ok(!notas.includes("INDEX.md"));
  assert.ok(!notas.includes("INDEX-bitacora.md"));
  assert.ok(notas.some((n) => n.includes("Arquitectura Actual.md")));
  assert.ok(notas.some((n) => n.includes("ADR-001 - Test.md")));
});

test("mesDe extrae correctamente el mes AAAA-MM", () => {
  const n1 = { rel: "70 - Bitácora de Cambios/2026-05/Sesión 2026-05-21 - Test.md", nombre: "Sesión 2026-05-21 - Test", fm: {} };
  assert.equal(L.mesDe(n1), "2026-05");
  const n2 = { rel: "70 - Bitácora de Cambios/2026-11/2026-11-09-fernando-test.md", nombre: "2026-11-09-fernando-test", fm: {} };
  assert.equal(L.mesDe(n2), "2026-11");
});

test("mesesAbiertos identifica el último mes activo y su anterior", () => {
  const notas = [
    { carpeta: "70 - Bitácora de Cambios", rel: "70 - Bitácora de Cambios/2026-05/s1.md", nombre: "s1" },
    { carpeta: "70 - Bitácora de Cambios", rel: "70 - Bitácora de Cambios/2026-10/s2.md", nombre: "s2" },
    { carpeta: "70 - Bitácora de Cambios", rel: "70 - Bitácora de Cambios/2026-11/s3.md", nombre: "s3" },
  ];
  const abiertas = L.mesesAbiertos(notas);
  assert.ok(abiertas.has("2026-11"));
  assert.ok(abiertas.has("2026-10"));
  assert.ok(!abiertas.has("2026-05"));
});

test("generarIndice produce índice determinista que agrupa por taxonomía Bimbo", () => {
  const root = crearVaultTemp([
    ["40 - Proyecto Bimbo/Arquitectura Actual.md", fmBasico("arquitectura", "vigente", "Arquitectura viva"), "# Arquitectura"],
    ["45 - Decisiones/ADR-001 - Test.md", fmBasico("adr", "vigente", "ADR de prueba"), "# ADR"],
    ["45 - Decisiones/ADR-002 - Viejo.md", fmBasico("adr", "archivado", "ADR viejo"), "# ADR viejo"],
  ]);

  const idx = L.generarIndice(root);
  assert.ok(idx.includes("# Índice del contexto"));
  assert.ok(idx.includes("## 40 - Proyecto Bimbo"));
  assert.ok(idx.includes("[[Arquitectura Actual]]"));
  assert.ok(idx.includes("[[ADR-001 - Test]]"));
  // Notas retiradas no deben listarse con enlace de contenido activo
  assert.ok(idx.includes("1 nota retirada: reemplazadas o archivadas, no se listan"));
});
