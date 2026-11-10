# Política de Seguridad — Bimbo Honduras (Proyecto Bimbo)

Esta política describe el procedimiento para reportar vulnerabilidades de seguridad de forma responsable, las versiones soportadas y las pautas para proteger datos y credenciales del sistema de pesaje y gestión.

---

## Versiones Soportadas

Actualmente, solo la versión mayor activa (`1.0.x`) recibe parches de seguridad y actualizaciones de dependencias:

| Versión | Estado de Soporte | Notas |
| :--- | :--- | :--- |
| `1.0.x` |  Soportada | Versión en producción (.NET 10 · WPF) |
| `< 1.0.0` | ❌ No soportada | Versiones prototipo históricas (WinForms / legadas) |

---

## Reporte Privado de Vulnerabilidades

**NUNCA abra un issue público en GitHub para reportar una vulnerabilidad de seguridad o fuga de credenciales.**

Para reportar un fallo de seguridad:
1. Utilice la función de **Reporte Privado de Vulnerabilidades** (*Private Vulnerability Reporting*) en la pestaña **Security** del repositorio de GitHub (`emanuelmartinez00429/BimboProyecto`).
2. Alternativamente, comuníquese de forma privada con los integrantes del equipo integrador:
   - **Fernando** (`@RedFer855`)
   - **Emanuel Martínez** (`@emanuelmartinez00429`)

### Información requerida en el reporte
- Descripción detallada del hallazgo y vector de ataque.
- Pasos precisos para reproducir la condición de fallo o prueba de concepto (PoC).
- Impacto potencial estimado (confidencialidad, integridad, disponibilidad).
- Versión exacta de la aplicación o componente afectado.

---

## Tiempos de Respuesta y Divulgación Responsable

- **Acuse de recibo inicial:** Dentro de las **48 horas** hábiles tras la recepción del reporte.
- **Evaluación y triaje:** Clasificación del impacto y severidad (CVSS) en un plazo no mayor a **5 días hábiles**.
- **Remediación:** Los parches para vulnerabilidades críticas o altas se priorizan de inmediato mediante hotfix y despliegue automatizado por Velopack.
- **Divulgación:** Solicitamos un período de gracia de al menos **30 días** tras la confirmación del reporte antes de cualquier divulgación externa, a fin de permitir el despliegue del parche a los clientes operativos de báscula.

---

## Reglas Estrictas sobre Datos y Credenciales

- Prohibido publicar cadenas de conexión a PostgreSQL, tokens JWT de rol `service_role`, o claves `sb_secret_`.
- La clave `anon` del cliente en `CapaUI/App.config` es de naturaleza pública y está restringida estrictamente por Row Level Security (RLS) en Supabase; sin embargo, no debe alterarse ni reemplazarse por credenciales con privilegios elevados.
- Nunca incluya información personal identificable (PII), números de teléfono, correos de colaboradores ni capturas con datos reales de pesaje de planta en issues o notas públicas.
