"use strict";
// Si se invoca directamente con node --test scripts/contexto
try {
  require("./lib.test.js");
} catch {
  // Ignora si no se ejecuta en entorno de test
}
module.exports = require("./lib.js");
