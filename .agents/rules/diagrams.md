# Regla de Generación Automática de Diagramas

## Disparadores (Triggers)
Siempre que el usuario en su solicitud utilice la palabra **"diagrama"**, **"digrama"**, **"diagramas"**, **"digramas"**, o términos equivalentes como **"flowchart"**, **"esquema visual"**, **"mapa de arquitectura"**, **"secuencia de proceso"**, **"organigrama"**, etc.:

## Acción Requerida
El agente **DEBE**:
1. Invocar y seguir la habilidad `diagram-design` ([SKILL.md](file:///c:/Users/fbara/OneDrive/Desktop/Proyecto%20de%20BIMBO/BimboProyecto/.agents/skills/diagram-design/SKILL.md)).
2. Identificar el tipo de diagrama adecuado (Arquitectura, Diagrama de flujo, Secuencia, Entidad-Relación, Componentes, etc.).
3. Construir el diagrama utilizando **HTML + SVG inline** autocontenido de calidad editorial profesional.
4. Aplicar la paleta de colores corporativos de Bimbo:
   - Color principal: Naranja Bimbo (`#FFA500`)
   - Fondos y tarjetas: Blanco (`#FFFFFF`) / Gris Claro (`#F8F9FA`)
   - Textos y bordes: Gris Oscuro (`#333333` / `#222222`)
5. Presentar el resultado en la respuesta principal o como un **Artifact** para permitir su visualización interactiva.
