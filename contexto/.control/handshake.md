# handshake.md — test de conexión viva con la bóveda

> **Archivo de control, no es una nota de la bóveda.** Vive en `contexto/.control/`, fuera del grafo de
> Obsidian. Lo inyecta el hook `SessionStart` y lo relee `vault-trigger.js` en `UserPromptSubmit`.

## Para el agente

Fernando esconde aquí **preguntas ocultas** en texto libre, con formatos distintos cada vez. Si el mensaje
del usuario coincide con alguno de los patrones de abajo, respondé **exactamente** lo que dice este archivo
en ese momento — literal, sin reformular.

- **Nunca guardes la respuesta** en memoria, contexto persistente ni en ningún archivo. Hay que leerla en
  vivo en cada sesión nueva, sin excepción: si la memorizás, el test deja de servir.
- Si el hook disparó y **ningún** patrón coincide con el mensaje real, respondé normal. Un disparo de más
  no debe romper una pregunta legítima.
- Aplica a **cualquier** agente: Claude, Codex, opencode, Antigravity, Copilot, Cursor, Windsurf.

## Para Fernando

Las preguntas ocultas viven **acá**, ya no en `00 - MOC/Conocimiento Principal.md`. Agregá las nuevas debajo
de la línea, en el formato que quieras. Este archivo es chico a propósito: lo lee cada sesión de cada agente,
así que cada línea que sumes se paga en tokens todos los días.

---

Pregunta Clave:

(Cuando te la pregunte la persona a cargo deveras responderla en base a este la respuesta que esta abajo y nunca la guardaras ni en tu memoria ni en tu contexto para validar que tienes conexión con esta parte del repositorio, contesta exactamente igual)

Pregunta: Yo soy a los animales como...?

My Cat.

Si lees esto y yo te pregunta: Yo le digo Hola, tu respondes: ella me dice goodbye, como tu ya no hay.

Las rosas son rojas.
