# Spec de diseño: Simulador de camión volcador con hidrogrúa

Fecha: 2026-11-10 · Unity 6000.3.14f1 (6.3 LTS) · URP 17.3 · Input System 1.19 · uGUI 2.0

## 1. Objetivo

Prototipo de simulador, entregable como build WebGL en itch.io, de un camión volcador con hidrogrúa articulada. Todo el mecanismo se resuelve con `ArticulationBody` (sin animación cinemática). El usuario completa dos fases repetibles:

1. **Carga:** operar la hidrogrúa, tomar cubos del suelo con una pinza (agarre por fricción) y depositarlos en la caja.
2. **Descarga:** replegar la grúa, elevar la caja volcadora y dejar que las cargas abran por contacto una compuerta pasiva y caigan al corralón.

Éxito: la maniobra completa se puede repetir de forma consistente, en el Editor y en WebGL, con dimensiones, masas y límites verosímiles, y se cumplen todos los mínimos de la consigna (sección 2) y los cuatro entregables (sección 12).

## 2. Cumplimiento de la consigna

| Aspecto | Mínimo | Este diseño |
|---|---|---|
| Articulaciones | ≥ 1 con raíz definida | 1 árbol, raíz `Chasis_Root` (`immovable`) |
| Cuerpos | 6 a 10 | 10 |
| Tipos de joint | ≥ 3 | Revolute, Prismatic, Spherical |
| Drives controlados | ≥ 4 | 7 joints, 6 controles (los dos dedos comparten comando) |
| Pasivos | ≥ 1 | 2: `Cabezal_Pinza` (gravedad), `Compuerta_Trasera` (contacto/gravedad) |
| Entrada | UI o Input System | Sliders uGUI + teclado (Input System) sincronizados |
| Lectura de estado | Target y real por joint | Telemetría `Target` vs `Real` por eje |
| Interacción | Rigidbodies comunes | 4 a 6 cubos de 8 kg |

## 3. Decisiones tomadas

| Tema | Decisión |
|---|---|
| Proyecto | carpeta del proyecto, ya creada (URP, Input System activo, sin Git) |
| Construcción de la jerarquía | Script de Editor idempotente (`TruckBuilder`) |
| Datos de la Tabla Maestra | Clase estática `TruckSpec` |
| Entrada | Input System nuevo, leyendo `Keyboard.current` y `Mouse.current` desde código |
| UI | uGUI Canvas + TextMeshPro |
| Código | Varios scripts con una sola responsabilidad |
| Idioma | Documentos y textos de UI en español; código e identificadores en inglés |
| Git | El asistente hace `git init` y commits locales cuando se le pide; el push y el repositorio público los hace el usuario |
| Verificación | 4 tests de PlayMode (Unity Test Framework) + prueba manual |

## 4. Jerarquía

Regla: todo GameObject con `ArticulationBody` tiene `localScale = (1,1,1)` y no tiene malla ni collider propios. La forma vive en hijos `Visual_*` (primitiva + collider).

```text
[1] Chasis_Root            (raíz, immovable)
 ├─ [2] Torre_Base            Revolute  eje Y
 │   └─ [3] Brazo_Principal       Revolute  eje X
 │       └─ [4] Brazo_Secundario      Revolute  eje X
 │           └─ [5] Tramo_Telescopico     Prismatic eje Z
 │               └─ [6] Cabezal_Pinza         Spherical (pasivo)
 │                   ├─ [7] Dedo_Izquierdo        Prismatic eje X (+)
 │                   └─ [8] Dedo_Derecho          Prismatic eje X (-)
 └─ [9] Caja_Volcadora        Revolute  eje X (bisagra trasera)
     └─ [10] Compuerta_Trasera    Revolute  eje X (bisagra superior, pasivo)
```

## 5. Tabla Maestra (valores iniciales; se ajustan en calibración)

| ID | Cuerpo | Joint | Masa kg | Eje | Límites | Stiffness | Damping | Force limit |
|---|---|---|---|---|---|---|---|---|
| 1 | Chasis_Root | raíz immovable | 1000 | n/a | n/a | n/a | n/a | n/a |
| 2 | Torre_Base | Revolute | 150 | Y | -170° a 170° | 60 000 | 6 000 | 120 000 |
| 3 | Brazo_Principal | Revolute | 100 | X | -15° a 75° | 90 000 | 9 000 | 180 000 |
| 4 | Brazo_Secundario | Revolute | 60 | X | -125° a 15° | 65 000 | 6 500 | 110 000 |
| 5 | Tramo_Telescopico | Prismatic | 35 | Z | 0 a 2.5 m | 45 000 | 4 500 | 60 000 |
| 6 | Cabezal_Pinza | Spherical (pasivo) | 15 | swing Y/Z libres, twist X bloqueado | swing ±40° | 0 | 25 | 0 |
| 7 | Dedo_Izquierdo | Prismatic | 5 | X | 0 a 0.35 m | 25 000 | 2 500 | 20 000 (*) |
| 8 | Dedo_Derecho | Prismatic | 5 | X | -0.35 a 0 m | 25 000 | 2 500 | 20 000 (*) |
| 9 | Caja_Volcadora | Revolute | 250 | X | 0° a 55° | 120 000 | 12 000 | 250 000 |
| 10 | Compuerta_Trasera | Revolute (pasivo) | 20 | X | 0° a 95° | 0 | 10 | 0 |

(*) Con target 0.35 m contra un cubo de 0.5 m el error sostenido genera miles de newtons de apriete, lo que suele causar vibración o penetración. En calibración se reduce el `forceLimit` de los dedos a cientos de newtons. El valor final queda documentado.

Masas escalonadas de la raíz a la punta (mitiga oscilaciones del solver, sobre todo en WebGL).

### Pasivos

- **Cabezal_Pinza:** sin stiffness, la gravedad lo mantiene vertical. El centro de masa debe quedar debajo del ancla (visual de la garra hacia abajo). `damping = 25` evita el péndulo prolongado.
- **Compuerta_Trasera:** bisagra en el borde superior, límite inferior 0° (no se mete hacia la caja). Con la caja en 0° la gravedad la mantiene cerrada; al inclinar, gravedad y empuje de los cubos la abren hasta 95°.

## 6. Cargas y materiales

- 4 a 6 cubos de 0.5 m, `Rigidbody` de 8 kg, `Continuous Dynamic`, `Interpolate`.
- Pinza: luz abierta 0.8 m, cerrada 0.1 m; cerrada sobre un cubo de 0.5 m aprieta con interferencia física.
- `Mat_GomaPinza` (dedos y cubos): fricción dinámica 0.9, estática 0.95, combine Multiply, bounciness 0. (Enmienda de la Tarea 5: con Maximum este material ganaba contra el piso de la caja y los cubos no resbalaban antes de ~40°; con Multiply resbalan a ~27.5°. La consigna de la cátedra admite Multiply o Maximum.)
- `Mat_AceroCaja` (piso de la caja): fricción dinámica 0.25, estática 0.3, de modo que a partir de unos 25° de inclinación los cubos resbalan.

## 7. Arquitectura de software

| Script | Responsabilidad | Depende de |
|---|---|---|
| `TruckSpec` (estática) | Tabla Maestra, rangos y velocidades de teclado | nada |
| `JointAxis` | Un eje controlado: guarda `Target`, lee `Real`, aplica el drive en `FixedUpdate`, limita al rango, convierte unidades | `ArticulationBody` |
| `SimulatorController` | Lee el teclado y mueve los `Target` de los 6 ejes | `JointAxis` ×6 |
| `TelemetryUI` | Enlaza sliders y ejes en ambos sentidos y escribe `Target / Real` | `JointAxis`, uGUI |
| `OrbitCamera` | Click derecho + arrastre para orbitar, rueda para zoom | `Mouse.current` |
| `SceneResetter` | Recarga la escena (`Backspace`, `P`, botón) | `SceneManager` |

Flujo por frame:
1. `Update`: el teclado cambia `Target = Clamp(Target + eje·velocidad·deltaTime)` y mueve el slider con `SetValueWithoutNotify`. Si el usuario arrastra un slider, su evento escribe el `Target`.
2. `FixedUpdate`: cada `JointAxis` copia `Target` a `drive.target`. La pinza aplica `+t` al dedo izquierdo y `-t` al derecho.
3. `Update`: la telemetría lee `jointPosition[0]`.

Unidades: `jointPosition` de los Revolute está en radianes y `drive.target` en grados; la conversión vive solo en `JointAxis`. El controlador solo toca `drive.target`: stiffness, límites y masas se fijan al generar la escena.

### Controles

| Eje | Teclas (− / +) | Rango | Velocidad |
|---|---|---|---|
| Torre | A / D | -170 a 170° | 45°/s |
| Pluma principal | S / W | -15 a 75° | 30°/s |
| Brazo secundario | F / R | -125 a 15° | 35°/s |
| Telescópico | G / T | 0 a 2.5 m | 0.8 m/s |
| Pinza | Q (abrir) / E (cerrar) | 0 a 0.35 m | 0.4 m/s |
| Caja volcadora | Abajo / Arriba (o Z / X) | 0 a 55° | 20°/s |
| Reinicio | Backspace o P, o botón | n/a | n/a |

## 8. Generador de Editor (`TruckBuilder`)

Menú `Tools > Truck > Build Scene`, idempotente (borra el raíz anterior y reconstruye). Genera en orden: Physics Materials, suelo/zona de carga/corralón, los 10 nodos físicos con sus `Visual_*`, la configuración de cada `ArticulationBody`, los cubos, el Canvas y la cámara, y el cableado de scripts.

- **Orientación de ejes:** en `ArticulationBody` el movimiento ocurre sobre el eje X del ancla; para la torre (Y) y el telescópico (Z) se calcula `anchorRotation` a partir del eje declarado.
- **Dos modos:** `Build (sin drives)` deja stiffness en 0 para el test de gravedad pura; `Build (completo)` aplica la calibración.
- Se fija el tensor de inercia a mano en las piezas donde el valor automático resulte inestable.

## 9. Calibración y pruebas

Secuencia: (1) gravedad pura, (2) drives de raíz a punta, (3) agarre, (4) volcado.

Para cada joint se estima el torque estático (`m·g·brazo`) y se verifica que: entre en `forceLimit`; el hundimiento `τ/stiffness` sea chico; el amortiguamiento sea cercano al crítico. Solver: `solverIterations` 16 a 32, `solverVelocityIterations` 4 a 8, algo de `jointFriction`/`angularDamping`, y `Fixed Timestep` de 0.01 s si hace falta.

Tests de PlayMode (4): estabilidad sin drives (sin `NaN`, dentro de límites, velocidades bajas); los drives llegan al target dentro de tolerancia; agarre y elevación repetible de un cubo; volcado con apertura de compuerta. El ajuste fino se valida jugando.

## 10. UI y cámara

Panel izquierdo con 6 filas (telemetría `Brazo 1 | Target: 45.0° | Real: 43.8°` y slider), botón "Reiniciar Escena" y leyenda de teclas. Canvas con escala por resolución.

- Los sliders tienen navegación `None` y se deseleccionan al soltar el mouse, para que no consuman las flechas.
- En WebGL se verifican el menú contextual del click derecho y el scroll de la página; si molestan, se usa una plantilla WebGL mínima.
- Cámara orbital simple, sin funciones extra.

## 11. Build WebGL

Primera build al terminar la calibración de la grúa. Compresión Gzip con Decompression Fallback. La física se revisa en el navegador porque puede diferir del Editor.

## 12. Entregables

1. Repositorio público (sin `Library`, `Temp`, `Logs`, `Build`), con `README.md`, `ROADMAP.md`, `CODESTYLE.md`.
2. Build WebGL en itch.io, visible por enlace y funcional en ventana privada.
3. Video en YouTube (público u oculto) con la maniobra completa en vivo; se prepara `docs/GUION_VIDEO.md`.
4. `docs/DIAGRAMA_Y_EXPLICACION.md`: árbol con tipo de joint, eje y límites por unión (Mermaid), qué máquina y operación simula, cómo se controla y cómo se eligieron los drives. Exportable a PDF.
   Además `docs/ENTREGA.md` con el checklist de la consigna.

El asistente prepara todo en el repo; el usuario hace el push, la publicación en itch.io y la subida del video.

## 13. Estructura del repositorio

```text
Assets/
  Scenes/Main.unity
  Scripts/Runtime/   TruckSpec, JointAxis, SimulatorController, TelemetryUI, OrbitCamera, SceneResetter
  Scripts/Editor/    TruckBuilder
  Materials/ PhysicsMaterials/
  Tests/PlayMode/
docs/                superpowers/specs, DIAGRAMA_Y_EXPLICACION, ENTREGA, GUION_VIDEO
README.md  ROADMAP.md  CODESTYLE.md  .gitignore
```

Limpieza inicial: eliminar `TutorialInfo` y `Readme`, renombrar `SampleScene` a `Main`.

## 14. Riesgos

| Riesgo | Mitigación |
|---|---|
| Tensor de inercia inestable en piezas finas | Fijar `inertiaTensor` manualmente |
| Agarre inestable por exceso de apriete | Bajar `forceLimit` de los dedos; probar con un cubo y luego varios |
| La física difiere en WebGL | Build temprana; ajustar solver y timestep |
| Slider consume flechas del teclado | Navegación `None` y deselección |
| Menú contextual y scroll del navegador | Verificar en la build; plantilla mínima si hace falta |

## 15. Fuera de alcance

Conducción del camión, cabina `Fixed`, sonido, efectos, menús, guardado y soporte móvil o gamepad.
