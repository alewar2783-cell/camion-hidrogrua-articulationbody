# CODESTYLE

Guía de estilo del proyecto. Los documentos van en español; el código, los identificadores y los comentarios técnicos van en inglés.

## Estructura y nombres

- Namespace `Truck` para todo el código. Un tipo público por archivo; el archivo se llama como el tipo.
- Cuatro assemblies: `Truck.Runtime`, `Truck.Editor` (solo Editor), `Truck.Tests.EditMode`, `Truck.Tests.PlayMode`. Runtime nunca referencia a Editor.
- `PascalCase` para tipos, métodos, propiedades y constantes públicas. `camelCase` para parámetros y locales. Campos privados `_camelCase`.
- Campos serializados: `[SerializeField] private` con prefijo `_`. No usar campos públicos.
- Enums en singular (`JointKind`). Booleanos con prefijo `Is`/`Has`/`Can`.
- Tests: `Metodo_Condicion_Resultado`, por ejemplo `Step_BothKeys_NoChange`.

## Formato

- Llaves en línea propia (estilo Allman), 4 espacios, sin tabs. Máximo ~100 columnas.
- `var` solo cuando el tipo se ve a la derecha. Un `using` por línea, ordenados, sin los que no se usan.
- Sin comentarios que repitan el código. Comentar el porqué (por ejemplo por qué hay una conversión de unidades).
- Sin código muerto ni comentado. Sin `TODO` sin dueño ni fecha en el commit.

## Reglas de Unity del proyecto

- **Valores del mecanismo:** masas, límites, stiffness, damping, force limit, velocidades y posiciones viven solo en `TruckSpec`. Prohibido repetirlos como números sueltos en otros scripts.
- **Escala:** todo GameObject con `ArticulationBody` tiene `localScale = (1,1,1)` y no tiene malla ni collider. La forma va en hijos `Visual_*`.
- **Física:** escribir targets y leer `jointPosition` en `FixedUpdate`/`Update` según corresponda; nunca mover un `ArticulationBody` con `transform`. Para cambiar un drive: copiar el struct (`var d = body.xDrive`), modificarlo y reasignarlo.
- **Unidades:** `jointPosition` de un Revolute está en radianes; `drive.target` en grados. La conversión vive en `JointAxis` y en ningún otro lugar.
- **Rendimiento (WebGL):** sin `Find*`, `GetComponent` ni LINQ en `Update`/`FixedUpdate`; cachear referencias en `Awake`/`Initialize`. No generar strings cada frame: actualizar un texto solo si su valor cambió.
- **Entrada:** Input System nuevo con `Keyboard.current` y `Mouse.current`, siempre comprobando que no sean `null`. Nada de `UnityEngine.Input`.
- **Escena:** los objetos del camión se generan con `TruckBuilder`; no se editan a mano en la escena. Si algo hay que cambiar, se cambia el generador o `TruckSpec` y se vuelve a ejecutar.

## Git

- Repositorio local, rama `main`. El push lo hace el usuario.
- Un commit por tarea del plan. Mensaje en español, imperativo, con prefijo: `feat:`, `fix:`, `test:`, `docs:`, `chore:`, `build:`.
- Se versionan los `.meta`. No se versionan `Library`, `Temp`, `Logs`, `Build`, `UserSettings`.
- Antes de commitear: los tests EditMode y PlayMode pasan y la consola de Unity no tiene errores.
