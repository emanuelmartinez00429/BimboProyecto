"use strict";
// Pruebas unitarias de archivar-bitacora.js para BimboProyecto.
// Correr con: node --test scripts/contexto/
const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("fs");
const os = require("os");
const path = require("path");
const A = require("./archivar-bitacora.js");

test("calcularSha256 produce un hash SHA-256 determinista de 64 caracteres", () => {
  const h1 = A.calcularSha256("Hola Mundo");
  const h2 = A.calcularSha256("Hola Mundo");
  const h3 = A.calcularSha256("Otro texto");

  assert.equal(h1.length, 64);
  assert.equal(h1, h2);
  assert.notEqual(h1, h3);
  assert.equal(h1, "c3a4a2e49d91f2177113a9adfcb9ef9af9679dc4557a0a3a4602e1bd39a6f481");
});

test("calcularMesesArchivables a fecha 2026-11-09 detecta meses >60 días (2026-05 a 2026-08)", () => {
  const meses = A.calcularMesesArchivables("2026-11-09");
  assert.ok(meses.includes("2026-05"));
  assert.ok(meses.includes("2026-06"));
  assert.ok(meses.includes("2026-07"));
  assert.ok(meses.includes("2026-08"));
  assert.ok(!meses.includes("2026-09"), "2026-09 cerró hace 40 días, no debe archivarse todavía");
  assert.ok(!meses.includes("2026-11"), "2026-11 está activo, no debe archivarse");
});

test("validarResumenesMeses valida exitosamente los meses que poseen resumen mensual", () => {
  assert.doesNotThrow(() => {
    A.validarResumenesMeses(["2026-05", "2026-06", "2026-07", "2026-08"]);
  });
});

test("validarResumenesMeses arroja excepción si un mes cerrado carece de resumen", () => {
  assert.throws(
    () => {
      A.validarResumenesMeses(["2026-09"]);
    },
    (err) => {
      return (
        err instanceof Error &&
        err.message.includes("carecen de resumen mensual obligatorio") &&
        err.message.includes("2026-09")
      );
    }
  );
});

test("recolectarSesiones recopila metadatos completos y omite archivos de resumen", () => {
  const sesiones = A.recolectarSesiones(["2026-06"], "test-commit");
  assert.ok(sesiones.length > 0);

  // Ninguna sesión debe ser un resumen
  for (const s of sesiones) {
    assert.ok(!s.ruta_original.endsWith("-resumen.md"), "No debe incluir resumen mensual como sesión individual");
    assert.ok(s.sha256 && s.sha256.length === 64);
    assert.ok(s.date && /^\d{4}-\d{2}-\d{2}$/.test(s.date));
    assert.ok(s.autor);
    assert.ok(s.agente);
    assert.ok(s.titulo);
    assert.ok(s.summary);
  }
});

test("recolectarDeudaResuelta selecciona únicamente deudas cerradas hace más de 60 días", () => {
  const deudas = A.recolectarDeudaResuelta("2026-11-09");
  assert.ok(deudas.length > 0);

  for (const d of deudas) {
    assert.ok(d.id_p.startsWith("P-"));
    assert.ok(d.cerrada_en);
    assert.ok(d.sha256.length === 64);
    assert.ok(d.dias_cerrada >= 60, `La deuda ${d.id_p} fue cerrada hace ${d.dias_cerrada} días (<60)`);
  }

  // Deudas recientes de septiembre u octubre (<60 días) no deben estar incluidas
  const ids = new Set(deudas.map((d) => d.id_p));
  assert.ok(!ids.has("P-065"), "P-065 cerrada el 2026-09-20 (50 días) no debe archivarse");
  assert.ok(!ids.has("P-067"), "P-067 cerrada el 2026-11-08 (1 día) no debe archivarse");
});

test("escribirBackups genera archivos JSONL válidos y con formato esperado", () => {
  const tmpDir = fs.mkdtempSync(path.join(os.tmpdir(), "bimbo-auditoria-test-"));
  try {
    const sesionMuestra = [
      {
        date: "2026-05-21",
        autor: "fernando",
        agente: "antigravity",
        modelo: null,
        rama: "main",
        commits: ["abc1234"],
        titulo: "Prueba Sesion",
        tags: ["test"],
        summary: "Resumen de prueba",
        deuda: ["P-001"],
        adr: ["ADR-001"],
        cuerpo_md: "# Contenido",
        ruta_original: "contexto/70 - Bitácora de Cambios/2026-05/test.md",
        sha256: "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        commit_archivo: "abc1234",
        mes_grupo: "2026-05",
      },
    ];

    const deudaMuestra = [
      {
        id_p: "P-001",
        titulo: "Deuda de prueba",
        prioridad: "Media",
        cerrada_en: "2026-05-28",
        cuerpo_md: "### P-001",
        sha256: "ca978112ca1bbdcafac231b39a23dc4da786eff8147c4e72b9807785afee48bb",
        dias_cerrada: 165,
      },
    ];

    const bkps = A.escribirBackups(tmpDir, sesionMuestra, deudaMuestra);
    assert.equal(bkps.length, 2);

    const f1 = path.join(tmpDir, "auditoria-2026-05.jsonl");
    assert.ok(fs.existsSync(f1));
    const linea1 = JSON.parse(fs.readFileSync(f1, "utf8").trim());
    assert.equal(linea1.titulo, "Prueba Sesion");
    assert.equal(linea1.sha256, "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

    const f2 = path.join(tmpDir, "auditoria-deuda-resuelta.jsonl");
    assert.ok(fs.existsSync(f2));
    const linea2 = JSON.parse(fs.readFileSync(f2, "utf8").trim());
    assert.equal(linea2.id_p, "P-001");
  } finally {
    fs.rmSync(tmpDir, { recursive: true, force: true });
  }
});
