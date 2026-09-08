---
title: "ADR-027 — Código privado y distribución pública de actualizaciones"
tags: [adr, decision, despliegue, seguridad, velopack]
date: 2026-09-08
estado: aceptado
implementacion: no_iniciada
---

# ADR-027 — Código privado y distribución pública de actualizaciones

## Estado y alcance

Aceptado como dirección de diseño en la conversación del 2026-09-08. No significa implementado ni autoriza publicar repositorios/paquetes. La autorización de esta sesión se limita a guardar documentación. Firma, responsables, controles efectivos de publicación y pruebas por máquina siguen siendo condiciones pendientes.

## Contexto

La aplicación C#/.NET WPF se instalará por primera vez en dos computadoras Windows 11 x64 y podría llegar a diez en seis meses. Se requiere publicar actualizaciones remotamente y descargarlas dentro del programa, desde ubicaciones distintas, sin presupuesto ni plan GitHub de pago declarado.

La primera restricción de mantener privados los binarios fue reconsiderada al distinguir código fuente, instalador y acceso a datos. También se aclaró que alojar archivos públicamente no obliga a abrir GitHub ni usar un navegador para actualizar.

## Decisión

1. Mantener privado el repositorio fuente. Usar un repositorio público separado para instaladores, paquetes e índices de distribución mediante GitHub Releases; no convertir el repositorio de desarrollo en público.
2. Entregar un instalador inicial por máquina. IT puede facilitarlo directamente a las estaciones; el usuario no necesita cuenta GitHub.
3. Integrar Velopack con la aplicación WPF para detectar y descargar versiones dentro del programa. No desarrollar un motor propio de instalación y reemplazo de ejecutables desde cero.
4. Ofrecer «Actualización disponible», descarga voluntaria y «Instalar y reiniciar» después de comprobar que no se pierde trabajo local. No aplicar automáticamente solo porque se descargó un paquete.
5. Mantener la arquitectura online actual. El actualizador no depende de SQLCipher/outbox ni aprueba el plan offline-first.
6. No incluir credenciales privilegiadas en el cliente ni tokens GitHub para acceder a archivos públicos. El acceso a datos continúa sujeto a autenticación y autorización del backend, verificadas independientemente del alojamiento.

Los detalles de canales beta/stable, construcción única, piloto, promoción, firma, recuperación y telemetría se desarrollan en [[Plan de CI-CD y Actualizaciones Remotas]]. Su implementación requiere completar las puertas de autorización indicadas allí.

## Alternativas consideradas

| Alternativa | Ventaja | Coste o límite | Resultado |
|---|---|---|---|
| Hacer público el repositorio fuente y usar sus Releases | Un solo repositorio | Publica fuentes e historial, innecesario para actualizar | No elegida |
| Fuente privada + repositorio binario público | Descarga sin secreto embebido; separación simple de distribución | Cualquiera puede obtener/analizar los binarios | Elegida |
| Releases privados | Restringe quién obtiene el paquete | Exige autenticación o intermediario; no distribuir un PAT común en el cliente | No elegida por ahora |
| Storage privado con enlaces temporales | Control de acceso a paquetes | Servicio de autorización y revisión de cuotas, tamaño y mantenimiento | Alternativa futura si cambia la confidencialidad |
| Instalador/actualizador propio en C# | Control total del comportamiento | Responsabilidad adicional sobre elevación, reemplazo, integridad y recuperación | Preferir integración de Velopack |
| Carpeta compartida de planta | Distribución local sencilla | Depende de conectividad de LAN/VPN | No satisface el requisito de ubicaciones distintas |

## Consecuencias y riesgos

- Publicar binarios expone lógica/configuración incorporada y permite descompilación; no equivale a publicar fuentes, pero tampoco conserva secreto absoluto de implementación.
- Login y RLS no son certificación automática de seguridad. Revisar privilegios, RPC y demás superficies relevantes; una credencial privilegiada nunca debe viajar en la app.
- Una URL pública o un hash no reemplazan firma y control del publicador. La alternativa de firma sigue sin decidirse; no se asume despliegue productivo sin firma.
- El cliente puede presentar la experiencia completamente dentro de WPF aunque descargue desde GitHub.
- La instalación por máquina requiere validar elevación y convivencia de usuarios con IT; no se promete actualización silenciosa bajo cuenta estándar.
- Una política beta/stable gobierna estaciones administradas, pero los paquetes públicos pueden descargarse fuera de ella.
- Si posteriormente se exige confidencialidad de paquetes, crear un ADR que reemplace esta decisión y rediseñar autenticación de descarga. Retirar una Release no recupera copias ya descargadas.

## Referencias

- [GitHub: Releases](https://docs.github.com/en/repositories/releasing-projects-on-github/about-releases).
- [Velopack: fuentes de actualización](https://docs.velopack.io/integrating/update-sources).
- [Velopack: integración](https://docs.velopack.io/integrating/overview).
- [[Velopack y GitHub Actions - Referencias para actualizaciones WPF]].

## Relaciones

- [[Conocimiento Principal]]
- [[Arquitectura Actual]]
- [[Plan de CI-CD y Actualizaciones Remotas]]
- [[Sesión 2026-09-08 - Plan de CI-CD y actualizaciones remotas]]
