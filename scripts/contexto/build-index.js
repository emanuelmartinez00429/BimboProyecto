#!/usr/bin/env node
// Regenera contexto/INDEX.md (arranque) e INDEX-bitacora.md (consulta) desde el frontmatter de las notas.
//
//   node scripts/contexto/build-index.js
//
// Correrlo después de crear, mover o cambiar el frontmatter de una nota.
// CI y verificar.js fallan si INDEX.md o INDEX-bitacora.md no coinciden con lo que produce este script.
"use strict";
const fs = require("fs");
const path = require("path");
const L = require("./lib.js");

const ROOT = path.resolve(__dirname, "..", "..");
const indice = L.generarIndice(ROOT);
const bitacora = L.generarIndiceBitacora(ROOT);
fs.writeFileSync(path.join(ROOT, L.VAULT, "INDEX.md"), indice, "utf8");
fs.writeFileSync(path.join(ROOT, L.VAULT, "INDEX-bitacora.md"), bitacora, "utf8");
console.log(`INDEX.md regenerado · ${L.listarNotas(ROOT).length} notas · ${Buffer.byteLength(indice)} B`);
console.log(`INDEX-bitacora.md regenerado · ${Buffer.byteLength(bitacora)} B`);
