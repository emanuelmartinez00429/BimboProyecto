/**
 * Hook automático: Detecta solicitudes de diagramas
 * Sugiere usar la skill diagram-design
 *
 * Activación: cuando el mensaje contiene palabras clave relacionadas con diagramas
 * Tipo: UserPromptSubmit (se ejecuta después de cada entrada del usuario)
 * Comportamiento: sugerencia explícita (el usuario decide si usarla)
 */

module.exports = {
  hookName: 'diagram-auto-suggest',
  events: ['UserPromptSubmit'],

  handler(context) {
    const { message } = context;

    if (!message) return null;

    // Palabras clave que activan la sugerencia de diagrama
    const diagramKeywords = [
      'diagrama', 'diagramas', 'diagram', 'diagrama de',
      'flowchart', 'flujo', 'arquitectura', 'arquitectónica',
      'secuencia', 'sequence', 'máquina de estado', 'state machine',
      'organigrama', 'org chart', 'árbol', 'tree',
      'red', 'network', 'grafo', 'graph',
      'componentes', 'componente', 'infra', 'infraestructura',
      'entidad-relación', 'ER', 'entity relationship',
      'línea de tiempo', 'timeline', 'cronograma',
      'gantt', 'kanban',
      'diagrama de clase', 'class diagram', 'UML',
      'caso de uso', 'use case',
      'flujo de datos', 'data flow',
      'mockup', 'wireframe', 'prototipo',
      'visual', 'visualizar', 'visualización'
    ];

    // Buscar palabras clave (case-insensitive)
    const messageLower = message.toLowerCase();
    const isDiagramRequest = diagramKeywords.some(kw =>
      messageLower.includes(kw.toLowerCase())
    );

    // Si encuentra diagrama AND el usuario no mencionó explícitamente la skill
    if (isDiagramRequest && !messageLower.includes('diagram-design')) {
      return {
        hookSpecificOutput: {
          hookEventName: 'UserPromptSubmit',
          suggestion: 'diagram-design',
          rationale: 'Se detectó solicitud de diagrama — usa /diagram-design para generar diagramas profesionales sin dependencias externas',
          autoapply: false, // El usuario debe decidir
          keywords: diagramKeywords.filter(kw => messageLower.includes(kw.toLowerCase()))
        }
      };
    }

    return null;
  }
};
