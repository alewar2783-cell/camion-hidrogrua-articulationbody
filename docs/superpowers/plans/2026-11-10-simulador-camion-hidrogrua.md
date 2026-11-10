# Simulador de camión volcador con hidrogrúa: Plan de implementación

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar un simulador WebGL de camión volcador con hidrogrúa de 10 `ArticulationBody`, controlable por teclado y sliders, con la maniobra de carga y descarga repetible.

**Architecture:** `TruckSpec` (datos estáticos) alimenta a un generador de Editor (`TruckBuilder`) que arma jerarquía, cargas, UI y cámara. En runtime, `JointAxis` escribe `drive.target` y lee `jointPosition`; `SimulatorController`, `TelemetryUI`, `OrbitCamera` y `SceneResetter` tienen una sola responsabilidad cada uno.

**Tech Stack:** Unity 6000.3.14f1, URP 17.3, Input System 1.19, uGUI 2.0 + TextMeshPro, Unity Test Framework 1.6.

**Spec:** `docs/superpowers/specs/2026-11-10-simulador-camion-hidrogrua-design.md` (el spec manda ante cualquier duda). Estilo: `CODESTYLE.md`. Seguimiento: `ROADMAP.md`.

## Global Constraints

- Proyecto en la carpeta del proyecto; Unity `6000.3.14f1`; escena `Assets/Scenes/Main.unity`.
- Exactamente 10 `ArticulationBody`; raíz `Chasis_Root` con `immovable = true`.
- Todo GameObject con `ArticulationBody` tiene `localScale = (1,1,1)` y no tiene malla ni collider; la forma va en hijos `Visual_*`.
- Joints: Revolute, Prismatic y Spherical. 7 drives controlados en 6 controles (los dedos comparten el comando). 2 pasivos: `Cabezal_Pinza` y `Compuerta_Trasera` (stiffness 0).
- Valores de masas, límites, stiffness, damping y force limit: los de la Tabla Maestra del spec (sección 5), solo dentro de `TruckSpec`.
- Cargas: 4 a 6 cubos de 0.5 m, 8 kg, `Continuous Dynamic`, `Interpolate`. `Mat_GomaPinza` fricción 0.9/0.95, combine Maximum, bounciness 0. `Mat_AceroCaja` fricción 0.25/0.3.
- Entrada solo con Input System (`Keyboard.current`, `Mouse.current`); UI con uGUI + TextMeshPro.
- Teclas: A/D torre, S/W pluma, F/R brazo secundario, G/T telescópico, Q/E pinza, Abajo/Arriba o Z/X caja, Backspace o P reinicio.
- Código e identificadores en inglés; documentos y textos de UI en español.
- Commits locales, un commit por tarea, con el trailer `Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>`. **Nunca `git push`.**
- Los pasos de commit se ejecutan una vez que el usuario eligió el método de ejecución del plan (esa elección es el visto bueno para commitear).
- No se versionan `Library`, `Temp`, `Logs`, `Build`, `UserSettings`.

## Cómo correr los tests

El Editor no puede estar abierto sobre el proyecto al usar la línea de comandos. Alternativa: `Window > General > Test Runner` dentro del Editor.

```bash
UNITY="<carpeta de Unity>/Editor/6000.3.14f1/Editor/Unity.exe"
PROJ="<carpeta del proyecto>"
"$UNITY" -batchmode -projectPath "$PROJ" -runTests -testPlatform EditMode -testResults "$PROJ/Temp/edit.xml" -logFile "$PROJ/Temp/edit.log"
"$UNITY" -batchmode -projectPath "$PROJ" -runTests -testPlatform PlayMode -testResults "$PROJ/Temp/play.xml" -logFile "$PROJ/Temp/play.log"
```

Resultado esperado: exit code 0 y `result="Passed"` en el XML. Filtrar uno: agregar `-testFilter "NombreDelTest"`.

## Review Focus

Entradas y condiciones que el spec implica y que ninguna tarea cubre de forma obvia (el test de cada una está en la tarea indicada):

1. **Dos teclas opuestas a la vez (A+D):** el efecto neto es cero, no gana la última. (Tarea 2)
2. **Frame muy largo (pestaña en segundo plano, WebGL lento):** `deltaTime` se limita a 0.05 s; el target no da un salto brusco. (Tarea 2)
3. **Reinicios repetidos:** reconstruir o recargar no duplica el camión ni deja targets o sliders viejos. (Tareas 3 y 4)
4. **Cerrar la pinza sin carga:** queda estable, sin `NaN` ni dedos atravesándose. (Tarea 5)
5. **Cubos sueltos en reposo:** ningún cubo cae por debajo del suelo ni sale del área en 10 s sin tocarlos. (Tarea 5)

---

## File Structure

```text
Assets/Scenes/Main.unity
Assets/Scripts/Runtime/  Truck.Runtime.asmdef
  TruckSpec.cs  (datos: BodySpec, ControlSpec, geometría, cargas)
  JointAxis.cs
  SimulatorController.cs
  SceneResetter.cs
  TelemetryUI.cs
  OrbitCamera.cs
Assets/Scripts/Editor/   Truck.Editor.asmdef
  TruckBuilder.cs        (menú + orquestación)
  TruckBodyFactory.cs    (nodos físicos, anclas, drives)
  TruckLoadsFactory.cs   (materiales, suelo, cubos)
  TruckUiFactory.cs      (Canvas, cámara, cableado)
Assets/Tests/EditMode/   Truck.Tests.EditMode.asmdef
Assets/Tests/PlayMode/   Truck.Tests.PlayMode.asmdef  (incluye plataforma Editor para usar TruckBuilder)
docs/CALIBRACION.md  docs/DIAGRAMA_Y_EXPLICACION.md  docs/ENTREGA.md  docs/GUION_VIDEO.md
README.md  ROADMAP.md  CODESTYLE.md
```

---

### Task 0: Base del repositorio

**Files:**
- Delete: `Assets/TutorialInfo/`, `Assets/Readme.asset` (+ `.meta`)
- Rename: `Assets/Scenes/SampleScene.unity` → `Assets/Scenes/Main.unity`
- Create: los cuatro `.asmdef` de la File Structure; carpetas vacías con `.gitkeep` donde haga falta.
- Modify: `ProjectSettings/EditorBuildSettings.asset` (la escena `Main` queda como única escena de la build)

**Interfaces:**
- Produces: assemblies `Truck.Runtime`, `Truck.Editor`, `Truck.Tests.EditMode`, `Truck.Tests.PlayMode`. Referencias: `Truck.Runtime` → `Unity.InputSystem`, `Unity.TextMeshPro`, `UnityEngine.UI`; `Truck.Editor` (solo plataforma Editor) → `Truck.Runtime` + las mismas; tests → `Truck.Runtime`, `UnityEngine.TestRunner`, `UnityEditor.TestRunner` (los de PlayMode además → `Truck.Editor`, con `includePlatforms: ["Editor"]`).

- [ ] **Step 1: Renombrar la escena y borrar la plantilla** (con el Editor o con `git mv` más borrar el `.meta` correspondiente). La escena `Main` conserva solo cámara y luz direccional.
- [ ] **Step 2: Crear los cuatro `.asmdef`** con las referencias de la sección Interfaces. En los tests, `overrideReferences` con `nunit.framework.dll` y la restricción `UNITY_INCLUDE_TESTS`.
- [ ] **Step 3: Importar TMP Essentials** (`Window > TextMeshPro > Import TMP Essential Resources`) para que `TMP_Text` tenga fuente por defecto.
- [ ] **Step 4: Verificar.** Abrir el proyecto: consola sin errores; `Main` abre. Correr los tests EditMode: 0 tests, exit code 0.
- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "chore: estructura de carpetas, assemblies y escena Main" -m "Co-Authored-By: Claude Sonnet 5.5 <noreply@anthropic.com>"
```

---

### Task 1: `TruckSpec` (datos de la Tabla Maestra)

**Files:**
- Create: `Assets/Scripts/Runtime/TruckSpec.cs`
- Test: `Assets/Tests/EditMode/TruckSpecTests.cs`

**Interfaces:**
- Produces (namespace `Truck`):
  - `enum BodyId { ChasisRoot = 1, TorreBase, BrazoPrincipal, BrazoSecundario, TramoTelescopico, CabezalPinza, DedoIzquierdo, DedoDerecho, CajaVolcadora, CompuertaTrasera }`
  - `enum JointKind { Root, Revolute, Prismatic, Spherical }`
  - `enum ControlId { Torre, Pluma, BrazoSecundario, Telescopico, Pinza, Caja }`
  - `readonly struct BodySpec` con `BodyId Id; string Name; BodyId? Parent; JointKind Joint; float Mass; Vector3 Axis; float Lower; float Upper; float Stiffness; float Damping; float ForceLimit; bool IsPassive; ControlId? Control` (`Lower/Upper` en grados o metros según el joint).
  - `readonly struct ControlSpec` con `ControlId Id; string Label; float Min; float Max; float Speed; bool IsAngular; Key[] DecreaseKeys; Key[] IncreaseKeys` (`Key` = `UnityEngine.InputSystem.Key`).
  - `static class TruckSpec` con `BodySpec[] Bodies`, `ControlSpec[] Controls`, `BodySpec Get(BodyId id)`, `ControlSpec Get(ControlId id)`, `static class Geometry` (tamaños y posiciones de pivotes en metros) y `static class Loads` (`Vector3[] GroundSpawn`, `Vector3[] BedSpawn`, `float CubeSize = 0.5f`, `float CubeMass = 8f`).

- [ ] **Step 1: Escribir los tests** en `TruckSpecTests` (todos deben fallar por compilación):
  - `Bodies_Count_IsTen`: `Assert.AreEqual(10, TruckSpec.Bodies.Length)`.
  - `Bodies_UseAtLeastThreeJointKinds`: distintos `Joint` ≥ 3.
  - `Bodies_DrivenCount_IsSeven`: cuerpos con `Control != null` == 7; `Controls.Length == 6`.
  - `Bodies_PassiveCount_IsTwo`: `IsPassive` == 2 y ambos con `Stiffness == 0`.
  - `Bodies_HaveSingleRoot`: un solo `Parent == null` y es `ChasisRoot`.
  - `Masses_NonIncreasingAlongCraneChain`: 150 ≥ 100 ≥ 60 ≥ 35 ≥ 15 ≥ 5 siguiendo `Parent` de `DedoIzquierdo` a la raíz de la grúa.
  - `Controls_RangesMatchSpec`: Torre -170..170, Pluma -15..75, BrazoSecundario -125..15, Telescópico 0..2.5, Pinza 0..0.35, Caja 0..55; velocidades 45, 30, 35, 0.8, 0.4, 20.
  - `Loads_CubeCount_BetweenFourAndSix`: `GroundSpawn.Length` en 4..6.
- [ ] **Step 2: Correr EditMode.** Esperado: no compila (`TruckSpec` no existe).
- [ ] **Step 3: Implementar `TruckSpec`** con los valores de la Tabla Maestra del spec (sección 5), las teclas de la sección 7 y la geometría inicial de abajo. Los dos dedos comparten `ControlId.Pinza`; el derecho tiene `Lower = -0.35`, `Upper = 0`.
  - Convención de ejes: X derecha, Y arriba, Z hacia adelante (cabina en +Z). Suelo en y = 0.
  - Valores iniciales de `Geometry`: chasis 2.4 × 0.4 × 7.0 con pivote en (0, 0.5, 0); cabina 2.2 × 1.6 × 1.6 centrada en z = 2.6; bisagra de la caja en (0, 0.8, -3.5), caja de 4.5 m de largo hacia adelante, paredes de 0.9 m; torre en (0, 0.7, 1.4) de 1.2 m de alto; pluma 3.0 m, brazo secundario 2.5 m, tramo telescópico 2.0 m (+2.5 m de recorrido); pinza abierta 0.8 m de luz; compuerta 2.2 × 0.9 m bisagra en el borde superior trasero.
  - `Loads.GroundSpawn`: 5 posiciones sobre y = 0.25 en x = 4.2..5.2, z = 0.4..2.4. `Loads.BedSpawn`: 4 posiciones dentro de la caja.
  - Estos números son iniciales; se corrigen en las Tareas 3 a 5 hasta cumplir los criterios de alcance (Tarea 3, Step 6).
- [ ] **Step 4: Correr EditMode.** Esperado: 8 tests PASS.
- [ ] **Step 5: Commit** (`feat: TruckSpec con la tabla maestra y tests de la consigna`).

---

### Task 2: `JointAxis` y lógica de entrada

**Files:**
- Create: `Assets/Scripts/Runtime/JointAxis.cs`, `Assets/Scripts/Runtime/SimulatorController.cs`
- Test: `Assets/Tests/EditMode/JointAxisTests.cs`

**Interfaces:**
- Consumes: `ControlSpec`, `ControlId` (Tarea 1).
- Produces:
  - `sealed class JointAxis : MonoBehaviour` con `ControlId Control`, `ControlSpec Spec`, `float Target`, `float Real` (grados o metros), `void Initialize(ControlSpec spec, ArticulationBody primary, ArticulationBody mirror = null)`, `void SetTarget(float value)` (con clamp), `void ResetTarget()` (vuelve a 0), y `FixedUpdate` que aplica `drive.target` (y `-Target` al `mirror`).
  - Estáticos puros: `float ClampTarget(float v, float min, float max)`, `float Step(float target, float input, float speed, float deltaTime, float min, float max)` (limita `deltaTime` a `MaxDeltaTime = 0.05f`), `float ToDisplay(bool isAngular, float jointPosition)` (radianes → grados si `isAngular`).
  - `sealed class SimulatorController : MonoBehaviour` con `JointAxis[] Axes` (un eje por `ControlId`, en ese orden), `static float ReadInput(bool decrease, bool increase)` que devuelve -1, 0 o 1 (ambas = 0), y `Update` que lee `Keyboard.current` (comprobando `null`) y llama a `Step`.

- [ ] **Step 1: Tests** en `JointAxisTests`:
  - `Step_BothKeys_NoChange`: `ReadInput(true, true) == 0` y `Step(10, 0, 45, 0.02f, -170, 170) == 10`.
  - `Step_LargeDeltaTime_IsCapped`: `Step(0, 1, 45, 5f, -170, 170)` ≈ `45 * 0.05` (2.25).
  - `Step_ClampsToRange`: `Step(74.9f, 1, 30, 0.05f, -15, 75) == 75`.
  - `ToDisplay_Angular_ConvertsRadiansToDegrees`: `ToDisplay(true, Mathf.PI) ≈ 180`; `ToDisplay(false, 1.25f) == 1.25f`.
  - `ClampTarget_OutOfRange_Clamps`.
- [ ] **Step 2: Correr EditMode.** Esperado: FAIL por compilación.
- [ ] **Step 3: Implementar `JointAxis`.** `xDrive` es el drive de todos los ejes controlados: el builder (Tarea 3) orienta el ancla de modo que el movimiento ocurra sobre su eje X. Copiar el struct, asignar `target` y reasignar.
- [ ] **Step 4: Implementar `SimulatorController`** (sin lógica de física; solo entrada → `Step` → `SetTarget`).
- [ ] **Step 5: Correr EditMode.** Esperado: todos PASS.
- [ ] **Step 6: Commit** (`feat: JointAxis y controlador de teclado con tests`).

---

### Task 3: Generador, parte 1: gravedad pura

**Files:**
- Create: `Assets/Scripts/Editor/TruckBuilder.cs`, `TruckBodyFactory.cs`, `TruckLoadsFactory.cs` (solo materiales y suelo en esta tarea)
- Test: `Assets/Tests/PlayMode/TruckGravityTests.cs`

**Interfaces:**
- Consumes: `TruckSpec` (Tarea 1).
- Produces:
  - `enum BuildMode { NoDrives, Full }`
  - `static class TruckBuilder`: `const string RootName = "Truck"`; `GameObject Build(BuildMode mode)` (destruye el objeto `Truck` previo si existe, crea y devuelve el nuevo); ítems de menú `Tools/Truck/Build Scene (No Drives)` y `Tools/Truck/Build Scene (Full)`.
  - `GameObject` por cuerpo con el nombre de `BodySpec.Name`, encontrable con `root.transform.Find` recursivo; hijos `Visual_*` con collider.

- [ ] **Step 1: Tests** (PlayMode, `[UnityTest]`) en `TruckGravityTests`; cada test llama `TruckBuilder.Build(BuildMode.NoDrives)` y espera con `yield return new WaitForSeconds(5f)`:
  - `Build_CreatesTenArticulationBodies`: `GetComponentsInChildren<ArticulationBody>().Length == 10`, todos con `transform.localScale == Vector3.one`, ninguno con `MeshRenderer` ni `Collider` en el mismo GameObject.
  - `Build_Twice_LeavesSingleRoot`: dos llamadas seguidas; `GameObject.FindObjectsByType` de objetos llamados `Truck` == 1 y siguen siendo 10 cuerpos.
  - `NoDrives_Settles_WithoutNaN`: tras 5 s, `jointPosition[0]` de cada cuerpo con grado de libertad es finito y la posición mundial de cada cuerpo es finita.
  - `NoDrives_RespectsLimits`: para cada cuerpo no raíz y no esférico, `jointPosition[0]` convertido a la unidad del spec está dentro de `[Lower - tol, Upper + tol]`, con `tol` = 1° para Revolute y 0.01 m para Prismatic.
  - `NoDrives_Settles_AtRest`: tras 5 s, `linearVelocity.magnitude < 0.2` y `angularVelocity.magnitude < 0.5` en cada cuerpo móvil.
  - `NoDrives_GripperHangsVertical`: el eje local +Y de `Cabezal_Pinza` forma menos de 5° con `Vector3.up` (la garra cuelga a lo largo de su -Y local).
- [ ] **Step 2: Correr PlayMode.** Esperado: FAIL por compilación.
- [ ] **Step 3: Implementar `TruckLoadsFactory`**: materiales físicos (`Mat_GomaPinza`, `Mat_AceroCaja`) creados con valores del spec, y un suelo grande (collider estático, al menos 60 × 60 m, con una marca visual del corralón detrás del camión).
- [ ] **Step 4: Implementar `TruckBodyFactory.CreateBody(BodySpec spec, Transform parent, ...)`**. Para cada nodo: GameObject con `localScale = 1`, `ArticulationBody` con masa, `jointType` según `JointKind`, límites (`xDrive.lowerLimit/upperLimit`, `linearLockX`/`angularLockX = Limited`), `anchorPosition`, `parentAnchorPosition` y `anchorRotation`. Reglas fijas:
  - El movimiento ocurre siempre sobre el eje X del ancla; si `BodySpec.Axis` es Y o Z, `anchorRotation = Quaternion.FromToRotation(Vector3.right, axis)` (y el mismo valor para el padre, vía `parentAnchorRotation`).
  - `Cabezal_Pinza` es `SphericalJoint` con `swingYLock = swingZLock = Limited` (±40°), `twistLock = Locked`; su ancla va en la unión con el telescópico y su `Visual_*` cuelga hacia abajo.
  - Modo `NoDrives`: stiffness 0 y damping de la tabla (para que los brazos se asienten sin rebotar indefinidamente); `forceLimit` de la tabla.
  - Si el tensor de inercia automático resulta inestable en alguna pieza fina, fijar `inertiaTensor` a mano y dejarlo anotado en `docs/CALIBRACION.md`.
  - Cada `Visual_*` lleva una primitiva escalada y un `BoxCollider` (o el de la primitiva); la caja se arma con varios colliders (piso, frente, paredes) y el piso usa `Mat_AceroCaja`; los dedos usan `Mat_GomaPinza`.
- [ ] **Step 5: Implementar `TruckBuilder.Build`** y los ítems de menú. Correr PlayMode: los 6 tests PASS.
- [ ] **Step 6: Verificación manual de geometría** (con el Editor): `Tools > Truck > Build Scene (No Drives)`, entrar en Play. Comprobar con el Scene view: anclas en los pivotes, nada salta. Criterios de alcance a lograr ajustando `TruckSpec.Geometry` y poniendo los targets a mano: la garra llega al suelo a 4.5 m lateral del eje del camión; la garra puede ubicarse sobre el centro de la caja a más de 1 m de altura. Anotar en `docs/CALIBRACION.md` los valores finales de geometría.
- [ ] **Step 7: Commit** (`feat: generador de jerarquia sin drives y test de gravedad pura`).

---

### Task 4: Drives y controlador

**Files:**
- Modify: `TruckBodyFactory.cs` (modo `Full`), `TruckBuilder.cs` (cableado de `JointAxis` y `SimulatorController`)
- Create: `Assets/Scripts/Runtime/SceneResetter.cs`
- Test: `Assets/Tests/PlayMode/TruckDriveTests.cs`
- Create: `docs/CALIBRACION.md`

**Interfaces:**
- Consumes: `JointAxis.Initialize/SetTarget/Target/Real`, `TruckSpec.Controls`.
- Produces: `Build(BuildMode.Full)` deja en el objeto `Truck` un `SimulatorController` con sus 6 `JointAxis`; `SceneResetter` con `static void Reload()` (recarga la escena activa) y `Update` que escucha `Backspace` y `P`; `SimulatorController` expone `JointAxis GetAxis(ControlId id)`.

- [ ] **Step 1: Tests** en `TruckDriveTests` (cada uno construye con `BuildMode.Full`):
  - `Full_Build_HasSixAxesAndSevenDrives`: `GetAxis` devuelve un eje por cada `ControlId`; `Pinza` mueve `DedoIzquierdo` y `DedoDerecho` con signos opuestos.
  - `Axis_ReachesTarget` (parametrizado por `ControlId`): `SetTarget` al 50 % del rango, esperar 4 s; `|Real - Target|` ≤ tolerancia (angulares 6°, lineales 0.08 m; si la calibración no logra estos valores, ajustar `TruckSpec` y no la tolerancia, salvo decisión anotada en `CALIBRACION.md`).
  - `Axis_NeverExceedsLimits`: `SetTarget(Max)` por 4 s; `Real` ≤ `Max + tolerancia` y finito.
  - `Passives_HaveNoDrive`: `Cabezal_Pinza` y `Compuerta_Trasera` con `stiffness == 0` en `Full`.
  - `ResetTargets_ReturnsToZero`: `ResetTarget()` en todos y esperar 4 s; cada `Real` cerca de 0 (o del `Min` si 0 queda fuera del rango, que no es el caso en esta tabla).
  - `Full_BuildTwice_NoDuplicates`: construir `Full` dos veces: 10 cuerpos, un solo `SimulatorController`.
- [ ] **Step 2: Correr PlayMode.** Esperado: FAIL.
- [ ] **Step 3: Implementar el modo `Full`** en `TruckBodyFactory`: stiffness, damping y `forceLimit` de `BodySpec`, activar `xDrive` solo en los cuerpos con `Control`; `solverIterations` 16 y `solverVelocityIterations` 4 en todos los cuerpos como punto de partida; `jointFriction` y `angularDamping` pequeños para estabilizar. Cablear `JointAxis` y `SimulatorController` en el builder. Implementar `SceneResetter`.
- [ ] **Step 4: Calibrar de la raíz a la punta** (torre, pluma, brazo secundario, telescópico, caja, dedos). Para cada joint, anotar en `docs/CALIBRACION.md`: torque o fuerza estática estimada (`m·g·brazo`), hundimiento `τ/stiffness`, amortiguamiento observado y cualquier cambio respecto de la Tabla Maestra con su motivo. Si el solver oscila, probar primero más `solverIterations`, luego `Fixed Timestep` 0.01 s.
- [ ] **Step 5: Correr PlayMode.** Esperado: todos PASS (incluidos los de la Tarea 3).
- [ ] **Step 6: Verificación manual:** `Build (Full)`, Play, mover cada eje con el teclado; los brazos no vibran y la pinza no oscila indefinidamente.
- [ ] **Step 7: Commit** (`feat: drives calibrados, controlador y reinicio de escena`).

---

### Task 5: Cargas y tarea completa

**Files:**
- Modify: `TruckLoadsFactory.cs` (cubos y área de carga), `TruckBuilder.cs`
- Test: `Assets/Tests/PlayMode/TruckTaskTests.cs`

**Interfaces:**
- Consumes: `TruckSpec.Loads`, `SimulatorController.GetAxis`.
- Produces: `Build` crea los cubos `Load_0..Load_n` (n = `GroundSpawn.Length`); `TruckLoadsFactory.SpawnBedLoads()` mueve los cubos a `Loads.BedSpawn` (usado por los tests de descarga).

- [ ] **Step 1: Tests** en `TruckTaskTests`:
  - `Loads_AtRest_StayOnGround`: construir `Full`; esperar 10 s sin input; todo cubo con `position.y > -0.1` y dentro de 30 m del origen.
  - `Loads_HaveSpecProperties`: masa 8, `collisionDetectionMode == ContinuousDynamic`, `interpolation == Interpolate`, escala 0.5.
  - `Grasp_Cube_LiftsWithGripper` (se repite 3 veces, reconstruyendo cada vez): teletransportar `Load_0` al centro entre los dedos abiertos; `Pinza` al máximo; esperar 1.5 s; subir `Pluma` 15° y esperar 3 s. Esperado: `Load_0.y` sube ≥ 0.2 m respecto de la altura inicial y la distancia del cubo al `Cabezal_Pinza` cambia menos de 0.15 m.
  - `Gripper_ClosedOnNothing_IsStable`: cerrar al máximo sin carga y esperar 3 s: valores finitos, `Real` de los dos dedos dentro de sus límites, sin interpenetración que dispare velocidades mayores a 5 m/s.
  - `Dump_OpensGate_AndEmptiesBed`: `SpawnBedLoads()`, esperar 1 s, `Caja` a 45°; esperar 10 s. Esperado: la compuerta alcanzó ≥ 30° en algún momento (muestrear cada frame) y todos los cubos quedaron detrás del borde trasero de la caja (z menor que la bisagra) o en el suelo.
  - `Dump_ReturnsGateClosed`: luego de volver `Caja` a 0° y esperar 6 s, la compuerta queda a menos de 5°.
- [ ] **Step 2: Correr PlayMode.** Esperado: FAIL.
- [ ] **Step 3: Implementar cubos y área de carga** con los valores del spec (sección 6) y `Mat_GomaPinza`.
- [ ] **Step 4: Calibrar el agarre.** Empezar con el `forceLimit` de los dedos en el valor de la tabla; si hay vibración o penetración, bajarlo (propuesta: cientos de newtons) y registrar el cambio y el motivo en `docs/CALIBRACION.md`. Ajustar fricción de la caja si la inclinación necesaria para el deslizamiento es mayor a ~35°.
- [ ] **Step 5: Correr PlayMode.** Esperado: todos PASS, tres veces seguidas (repetibilidad).
- [ ] **Step 6: Verificación manual de la maniobra completa:** tomar un cubo del suelo, subirlo a la caja, repetir con otro, plegar la grúa, volcar y ver que la compuerta se abre sola y los cubos caen al corralón.
- [ ] **Step 7: Commit** (`feat: cargas, agarre por friccion y descarga con compuerta pasiva`).

---

### Task 6: UI, telemetría y cámara

**Files:**
- Create: `Assets/Scripts/Runtime/TelemetryUI.cs`, `Assets/Scripts/Runtime/OrbitCamera.cs`, `Assets/Scripts/Editor/TruckUiFactory.cs`
- Modify: `TruckBuilder.cs`
- Test: `Assets/Tests/EditMode/TelemetryAndCameraTests.cs`

**Interfaces:**
- Consumes: `JointAxis` (`Target`, `Real`, `SetTarget`, `Spec`).
- Produces:
  - `static string TelemetryUI.Format(string label, float target, float real, bool isAngular)` → `"Brazo 1 | Target: 45.0° | Real: 43.8°"` (`m` en lugar de `°` si es lineal; cultura invariante, un decimal para grados y dos para metros).
  - `TelemetryUI.Bind(JointAxis axis, Slider slider, TMP_Text label)`; el slider escribe `SetTarget`, y `Update` hace `SetValueWithoutNotify(axis.Target)` y reescribe el texto solo si el string cambió.
  - `OrbitCamera` con `static float ClampPitch(float degrees)` (-10..85), `static float ClampDistance(float d)` (3..25), y `Update` que usa `Mouse.current` (botón derecho + delta; rueda).
  - El builder crea el Canvas (escala por resolución 1920×1080), 6 filas (etiqueta + slider con rango de `ControlSpec`), botón "Reiniciar Escena" (llama a `SceneResetter.Reload`), leyenda de teclas, `EventSystem` con `InputSystemUIInputModule`, y deja cada slider con `navigation = None` y deseleccionado al soltar.

- [ ] **Step 1: Tests:**
  - `Format_Angular_UsesDegreesAndOneDecimal`: `Format("Brazo 1", 45f, 43.84f, true) == "Brazo 1 | Target: 45.0° | Real: 43.8°"`.
  - `Format_Linear_UsesMetersAndTwoDecimals`: `Format("Telescópico", 1f, 0.987f, false) == "Telescópico | Target: 1.00 m | Real: 0.99 m"`.
  - `Format_UsesInvariantCulture`: con `CultureInfo` de coma decimal, el resultado sigue usando punto.
  - `ClampPitch_OutOfRange_Clamps`, `ClampDistance_OutOfRange_Clamps`.
- [ ] **Step 2: Correr EditMode.** Esperado: FAIL.
- [ ] **Step 3: Implementar `TelemetryUI` y `OrbitCamera`.** Sin allocations por frame salvo cuando cambia el texto.
- [ ] **Step 4: Implementar `TruckUiFactory`** y cablearlo en `Build`. Agregar un test PlayMode `Ui_Build_CreatesSixSlidersAndResetButton` en `TruckTaskTests.cs`: seis `Slider` en el Canvas, cada uno con `minValue/maxValue` iguales al `ControlSpec`, y un `Button`.
- [ ] **Step 5: Correr EditMode y PlayMode.** Esperado: PASS.
- [ ] **Step 6: Prueba manual completa:** mover un eje con teclado y ver que el slider lo sigue; arrastrar el slider; ver `Target` vs `Real`; las flechas no cambian el foco de la UI; reiniciar con `Backspace`, `P` y el botón; orbitar con click derecho.
- [ ] **Step 7: Commit** (`feat: panel de sliders, telemetria Target/Real y camara orbital`).

---

### Task 7: Build WebGL temprana

**Files:**
- Modify: `ProjectSettings` (Player WebGL: compresión Gzip, Decompression Fallback activado, nombre de producto), `EditorBuildSettings` (escena `Main`)
- Create: `docs/CALIBRACION.md` (sección "WebGL")

**Interfaces:** ninguna nueva.

- [ ] **Step 1: Configurar Player para WebGL:** Gzip + Decompression Fallback, `Data caching` desactivado, escena `Main` en la lista.
- [ ] **Step 2: Compilar** `File > Build Profiles > Web > Build` a `Build/WebGL` (carpeta ignorada por Git).
- [ ] **Step 3: Servir localmente** (`python -m http.server` dentro de `Build/WebGL`) y probar en una ventana privada del navegador. Revisar: carga sin errores en la consola del navegador; los 6 controles responden; el click derecho no abre el menú contextual sobre el canvas y las flechas no desplazan la página.
- [ ] **Step 4: Comparar la física con el Editor** repitiendo la maniobra completa (agarre y descarga). Si algo difiere, ajustar solver, `Fixed Timestep` o `forceLimit` y anotarlo en `docs/CALIBRACION.md`. Si el menú contextual o el scroll molestan, agregar una plantilla WebGL mínima que los bloquee.
- [ ] **Step 5: Commit** (`build: configuracion WebGL y ajustes de fisica para el navegador`).

---

### Task 8: Documentación y entrega

**Files:**
- Create: `README.md`, `docs/DIAGRAMA_Y_EXPLICACION.md`, `docs/ENTREGA.md`, `docs/GUION_VIDEO.md`
- Modify: `ROADMAP.md`

**Interfaces:** las tablas del documento salen de `TruckSpec` y de `docs/CALIBRACION.md` (valores finales, no los iniciales).

- [ ] **Step 1: `README.md`:** qué simula, versión de Unity, cómo abrir y ejecutar (abrir el proyecto, escena `Main`, `Tools > Truck > Build Scene (Full)` si hiciera falta regenerar), tabla de controles, enlaces a itch.io y YouTube con marcadores para completar.
- [ ] **Step 2: `docs/DIAGRAMA_Y_EXPLICACION.md`:** diagrama Mermaid del árbol (tipo de joint, eje y límites en cada unión), tabla de masas/drives finales, qué máquina y operación simula, cómo se controla y por qué esos valores de drive (con las estimaciones de `CALIBRACION.md`).
- [ ] **Step 3: `docs/ENTREGA.md`:** checklist de la consigna (repositorio público sin `Library/Temp/Logs/Build`, itch.io visible y funcional en ventana privada, video visible sin iniciar sesión con la maniobra completa, diagrama con tipo/eje/límites).
- [ ] **Step 4: `docs/GUION_VIDEO.md`:** guion de 2 a 3 minutos de la maniobra completa con los controles a usar en cada momento.
- [ ] **Step 5: Verificación final:** `git status` limpio; `git ls-files` no contiene `Library/`, `Temp/`, `Logs/`, `Build/`; clonar en una carpeta temporal, abrir con Unity 6000.3.14f1 sin pasos manuales y correr todos los tests (EditMode y PlayMode en verde). Marcar el `ROADMAP.md`.
- [ ] **Step 6: Commit** (`docs: README, diagrama, explicacion, checklist de entrega y guion del video`).

---

## Self-Review

- **Cobertura del spec:** jerarquía y Tabla Maestra (T1, T3, T4), cargas y materiales (T3, T5), arquitectura y controles (T2, T4), generador con dos modos (T3, T4), calibración y 4 tests de PlayMode (T3 estabilidad, T4 drives, T5 agarre y volcado), UI y cámara (T6), WebGL (T7), entregables y limpieza (T0, T8), riesgos (inercia en T3, dedos en T5, WebGL en T7, slider y menú contextual en T6/T7).
- **Consistencia de tipos:** `JointAxis`, `ControlSpec`, `BodySpec`, `BuildMode`, `TruckBuilder.Build`, `SimulatorController.GetAxis` y `TelemetryUI.Format` se usan con la misma firma en todas las tareas.
- **Review Focus:** cada uno de los 5 puntos tiene su test (T2: A+D y `deltaTime`; T3/T4: reconstrucción sin duplicados; T5: pinza vacía y cubos en reposo).
- **Valores a confirmar durante la ejecución:** la geometría de `TruckSpec.Geometry` y las tolerancias de los tests de drives son iniciales; los cambios se documentan en `docs/CALIBRACION.md`.
