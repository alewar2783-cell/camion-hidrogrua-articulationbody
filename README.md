# Simulador de camión volcador con hidrogrúa

Prototipo de simulador hecho en **Unity 6000.3.14f1 (6.3 LTS)** con **URP**, **Input System** y **uGUI**. Todo el mecanismo se resuelve con `ArticulationBody` (sin animación cinemática): un árbol de 10 cuerpos con articulaciones Revolute, Prismatic y Spherical, 7 drives controlados y 2 articulaciones pasivas.

## Qué simula

Un camión con caja volcadora y una grúa articulada (hidrogrúa) con pinza. La operación tiene dos fases repetibles:

1. **Carga:** con la grúa se toman cubos de 8 kg del suelo (agarre por fricción de los dos dedos) y se depositan en la caja.
2. **Descarga:** se repliega la grúa, se eleva la caja volcadora y las cargas, al resbalar, abren por contacto la compuerta pasiva y caen al corralón.

Hay 5 cubos en el suelo. La maniobra se puede repetir con el botón de reinicio; un cubo que cae fuera del mundo (por debajo de y = -20 m) vuelve solo a su punto de origen (`LoadKillPlane`).

## Enlaces

- Build jugable (itch.io): **[COMPLETAR: URL de la página en itch.io]**
- Video de la maniobra (YouTube): **[COMPLETAR: URL del video]**
- Documento con diagrama y explicación: [`docs/DIAGRAMA_Y_EXPLICACION.md`](docs/DIAGRAMA_Y_EXPLICACION.md)

## Cómo abrirlo y ejecutarlo

1. Instalar **Unity 6000.3.14f1** (Unity Hub, módulo *WebGL Build Support* si se quiere compilar para web).
2. Clonar el repositorio y abrir la carpeta con Unity Hub. La primera importación tarda varios minutos.
3. Abrir la escena `Assets/Scenes/Main.unity` y pulsar *Play*. No hace falta ningún paso manual: la escena ya contiene el camión generado.
4. Si hiciera falta regenerar la escena: menú `Tools > Truck > Build And Save Main Scene` (reconstruye el camión, la interfaz, las cargas y el entorno de forma idempotente y guarda `Main`). Por línea de comandos:

```text
Unity.exe -batchmode -quit -projectPath <ruta> -executeMethod Truck.TruckSceneSaver.BuildAndSave
```

`Main.unity` se regenera con `Tools > Truck > Build And Save Main Scene`; no editarla a mano (re-guardarla a mano produce diffs de unas 5000 líneas).

Los objetos del camión se generan con `TruckBuilder`; no se editan a mano. Para cambiar algo se modifica `TruckSpec` (única fuente de masas, límites, drives y velocidades) o el generador y se vuelve a ejecutar.

## Controles

Los controles de teclado y los sliders del panel izquierdo están sincronizados: el teclado mueve el *Target* de cada eje a velocidad constante y el slider refleja ese valor; arrastrar un slider escribe el *Target*. La telemetría muestra `Target` (lo pedido) y `Real` (lo que mide la articulación).

| Eje | Teclas (− / +) | Rango | Velocidad |
|---|---|---|---|
| Torre | `A` / `D` | -170° a 170° | 45 °/s |
| Pluma principal | `S` / `W` | -15° a 75° | 30 °/s |
| Brazo secundario | `F` / `R` | -125° a 15° | 35 °/s |
| Telescópico | `G` / `T` | 0 a 2.5 m | 0.8 m/s |
| Pinza | `Q` (abrir) / `E` (cerrar) | 0 a 0.35 m por dedo | 0.4 m/s |
| Caja volcadora | `↓` o `Z` / `↑` o `X` | 0° a 55° | 20 °/s |
| Reinicio | `Retroceso` o `P`, o botón *Reiniciar Escena* | n/a | n/a |
| Cámara | botón derecho + arrastrar (orbitar), rueda (zoom) | n/a | n/a |

**En el navegador (itch.io), hacer click en el canvas para darle foco al teclado.** Sin foco las teclas no llegan al juego, y si la pestaña pierde el foco el simulador ignora las teclas hasta que vuelva.

El camión arranca en una pose plegada (pluma 70°, secundario -45°). Antes de volcar hay que replegar la grúa a esa pose: si no, el brazo choca con la caja.

## La maniobra en breve

1. Girar la torre hacia el cubo (`A`), bajar pluma y secundario (`S`, `F`) y extender el telescópico (`T`) hasta quedar sobre el cubo; ajustar fino con los sliders.
2. Cerrar la pinza (`E`), subir la pluma (`W`), girar la torre hacia la caja (`D`) y bajar la carga sobre el piso de la caja.
3. Abrir la pinza (`Q`) para soltar el cubo.
4. Replegar la grúa, subir la caja con `↑`/`X` hasta 55° y esperar a que las cargas abran la compuerta y caigan; bajar la caja y comprobar que la compuerta vuelve a cerrarse.

El guion paso a paso con tiempos está en [`docs/GUION_VIDEO.md`](docs/GUION_VIDEO.md).

## Paquetes de Unity

El `manifest.json` conserva `com.unity.ai.assistant`, `com.unity.pipeline` y `com.unity.ai.inference`. Son herramientas del autor (asistente de IA, CLI y MCP del Editor), algunas pre-release o experimentales; solo funcionan en el Editor y el simulador no las necesita. Sus versiones están fijadas en `packages-lock.json`. La build WebGL se verificó con ellos presentes sin problemas.

## Tests

Los tests usan Unity Test Framework: `Window > General > Test Runner` (pestañas *EditMode* y *PlayMode*), o por línea de comandos:

```text
Unity.exe -batchmode -nographics -projectPath <ruta> -runTests -testPlatform EditMode -testResults edit.xml -logFile edit.log
Unity.exe -batchmode -nographics -projectPath <ruta> -runTests -testPlatform PlayMode -testResults play.xml -logFile play.log
```

- **EditMode:** `TruckSpec` (requisitos de la consigna: 10 cuerpos, 3 tipos, 7 drives, 2 pasivos), `JointAxis`, telemetría y cámara.
- **PlayMode:** gravedad pura, alcance de cada drive, agarre y elevación de un cubo, volcado con compuerta, cubos que caen del mundo y reaparecen, y la escena `Main` guardada (`TruckSavedSceneTests`).

## Build WebGL

Menú `Tools > Truck > Build WebGL` (`BuildScript.BuildWebGl`: aplica la configuración WebGL y compila `Main` en `Build/WebGL`). Por línea de comandos (tarda unos 10 a 15 minutos):

```text
Unity.exe -batchmode -quit -projectPath <ruta> -buildTarget WebGL -executeMethod Truck.BuildScript.BuildWebGl -logFile build.log
```

`Tools > Truck > Configure WebGL` solo aplica los ajustes (producto `Camion Hidrogrua`, compresión Gzip con *Decompression Fallback*, escena `Main`). `Build/` no se versiona. Cómo comprimirla y subirla a itch.io: [`docs/ENTREGA.md`](docs/ENTREGA.md).

## Estructura del repositorio

```text
Assets/
  Scenes/Main.unity          escena con el camión generado
  Scripts/Runtime/           TruckSpec, JointAxis, SimulatorController, TelemetryUI, OrbitCamera, SceneResetter, LoadKillPlane
  Scripts/Editor/            TruckBuilder y fábricas, TruckSceneSaver, BuildScript
  Shaders/                   IndustrialToon y GradientSky (estilo, ver docs/ESTILO.md)
  Materials/ PhysicsMaterials/
  Tests/EditMode/ Tests/PlayMode/
docs/
  DIAGRAMA_Y_EXPLICACION.md  árbol de joints, máquina, control y drives
  diagrama_articulaciones.svg  el mismo diagrama como imagen
  ENTREGA.md  GUION_VIDEO.md  CALIBRACION.md
  superpowers/               spec de diseño y plan
README.md  ROADMAP.md  CODESTYLE.md
```

Otros documentos: [`ROADMAP.md`](ROADMAP.md) (hitos), [`CODESTYLE.md`](CODESTYLE.md) (estilo), [`docs/CALIBRACION.md`](docs/CALIBRACION.md) (valores ajustados y mediciones).
