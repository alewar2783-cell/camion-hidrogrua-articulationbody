# Auditoría de entrega

Auditoría contra la consigna ("Trabajo práctico: prototipo con Articulation Body") hecha sobre el proyecto real (código, tests, escena, `ProjectSettings`, documentos), rama `feature/estilo-industrial`, Unity 6000.3.14f1. Estado: **OK** cumple y está verificado; **PARTIAL** cumple con una salvedad; **GAP** falta (casi todos son pasos del usuario fuera del repositorio).

## 1. Requisitos mínimos

| Requisito | Evidencia | Estado |
|---|---|---|
| Al menos 1 articulación con raíz bien definida | Una sola raíz `Chasis_Root` con `immovable = true`: `TruckBuilder.cs:65`; `TruckSpec.cs:110`; `TruckSpecTests.Bodies_HaveSingleRoot` | OK |
| 6 a 10 Articulation Bodies | 10 cuerpos: `TruckSpec.Bodies` (`TruckSpec.cs:108-136`); `TruckSpecTests.Bodies_Count_IsTen`, `TruckGravityTests.Build_CreatesTenArticulationBodies`, `TruckSavedSceneTests.MainScene_Loaded_HasTruckUiCameraAndLoads` (escena guardada). Está en el tope del rango, no lo pasa | OK |
| Al menos 3 tipos de joint | Revolute (5), Prismatic (3), Spherical (1) más la raíz: `TruckSpecTests.Bodies_UseAtLeastThreeJointKinds`. No usa Fixed (no es obligatorio) | OK |
| Al menos 4 joints con drive controlado | 7 drives controlados por 6 comandos: `TruckSpecTests.Bodies_DrivenCount_IsSeven`, `TruckDriveTests.Full_Build_HasSixAxesAndSevenDrives`, `Axis_ReachesTarget` (por eje) | OK |
| Al menos 1 parte pasiva (gravedad o contacto) | `Cabezal_Pinza` (Spherical, cuelga por gravedad) y `Compuerta_Trasera` (la abren las cargas por contacto): `TruckSpec.cs:123,134`; `TruckSpecTests.Bodies_PassiveCount_IsTwo`, `TruckDriveTests.Passives_HaveNoDrive`, `TruckGravityTests.NoDrives_GripperHangsVertical`, `TruckTaskTests.Dump_OpensGate_AndEmptiesBed` | OK |
| Entrada: UI o Input System | Ambas: teclado con Input System (`SimulatorController.cs:53-89`, `Keyboard.current` con control de `null`) y 6 sliders uGUI (`TelemetryUI.cs`). Ninguna llamada a `UnityEngine.Input`. Tests: `InputGuardTests`, `JointAxisTests.Step_*`, `TruckTaskTests.Ui_Build_CreatesSixSlidersAndResetButton` | OK |
| Lectura de estado: Target y valor real por joint en pantalla | `TelemetryUI.Format` y `Refresh` (`TelemetryUI.cs:29,110`), `jointPosition` convertido en `JointAxis.Real` (`JointAxis.cs:25`); `TelemetryAndCameraTests.Format_*`, `TruckTaskTests.Ui_Telemetry_FollowsAxisAndSliderWritesTarget`, `TruckSavedSceneTests.MainScene_Loaded_TelemetryFollowsAxesAndSliders` | OK |
| Tarea con Rigidbodies comunes, repetible | 5 cubos `Rigidbody` de 8 kg (`TruckLoadsFactory.cs:53-68`); agarre y elevación x3 (`Grasp_Cube_LiftsWithGripper`), volcado (`Dump_*`), reposo 10 s (`Loads_AtRest_StayOnGround`), reinicio (`SceneResetter`). Ver salvedad abajo | PARTIAL |
| Dimensiones, masas y límites verosímiles | Camión de 7 m, cubo de 0.5 m y 8 kg, masas decrecientes a la punta (`TruckSpecTests.Masses_NonIncreasingAlongCraneChain`), límites de `TruckSpec`; justificación en `DIAGRAMA_Y_EXPLICACION.md` secciones 3 y 5 y `CALIBRACION.md`. El chasis (1000 kg) es liviano para un camión real, pero es raíz inmóvil: su masa no entra en la dinámica | OK |
| Unity 6.3 LTS | `ProjectSettings/ProjectVersion.txt`: 6000.3.14f1; URP 17.3, Input System 1.19 (`Packages/manifest.json`) | OK |
| Funciona en build WebGL | `BuildScript.BuildWebGl`; build y prueba de humo en el navegador: sección "Resultados de la corrida" | OK |

**Salvedad de la tarea repetible.** Los tests automáticos cubren cada tramo (agarre, elevación, volcado, compuerta), pero el agarre coloca el cubo entre los dedos con `TruckLoadsFactory.Place` (no hay un test que haga el ciclo completo suelo, caja, volcado con solo los ejes). El ciclo completo con solo física se hizo a mano (`CALIBRACION.md` sección 10). Se agregó `LoadKillPlane` para que un cubo que cae del mundo vuelva a su origen y la maniobra siga siendo repetible sin reiniciar.

## 2. Reglas de Git y entrega

| Regla | Evidencia | Estado |
|---|---|---|
| `.gitignore` de Unity | `.gitignore`: `/[Ll]ibrary/`, `/[Tt]emp/`, `/[Ll]ogs/`, `/[Bb]uild/`, `/[Uu]ser[Ss]ettings/`, `*.csproj`, `*.sln` | OK |
| Sin `Library`, `Temp`, `Logs`, `Build` en el repo | `git ls-files` no contiene esas carpetas ni `.csproj`/`.sln` (verificado); `.meta` versionados | OK |
| Sin rutas absolutas locales | `git grep` de `C:\`/`D:\` en archivos versionados (salvo `docs/superpowers`): sin resultados | OK |
| Un clon limpio abre y ejecuta | Clon limpio de `HEAD` probado en esta auditoría: abre, compila y pasa los tests (ver resultados). Salvedad: el manifest conserva paquetes de IA pre-release (`com.unity.ai.assistant 2.21.0-pre.2`, `com.unity.pipeline 0.10.0-exp.1`) que el simulador no usa | PARTIAL |
| Repositorio público | Solo existe el repo local; sin `remote` ni push (lo hace el usuario) | GAP (usuario) |
| Build en itch.io, utilizable desde la página | `docs/ENTREGA.md` secciones 2 y 3; falta subirla y probarla en ventana privada | GAP (usuario) |
| Video de YouTube con la maniobra en vivo | Guion en `docs/GUION_VIDEO.md`; sin grabar | GAP (usuario) |
| Documento breve con diagrama y explicación | `docs/DIAGRAMA_Y_EXPLICACION.md` (árbol Mermaid con tipo, eje y límites por unión) y `docs/diagrama_articulaciones.svg`: qué máquina y operación (sección 1), cómo se controla (4) y cómo se eligieron los drives (5) | OK |
| Enlaces en el README | `README.md` tiene `[COMPLETAR: URL ...]` para itch.io y YouTube | GAP (usuario) |

## 3. Pasos sugeridos

| Paso | Evidencia | Estado |
|---|---|---|
| 1. Repo desde el primer día, commits frecuentes | Historial de commits pequeños con prefijo; el repo no está publicado | PARTIAL (publicar) |
| 2 y 3. Árbol en papel, eje, límites y masa por joint | `DIAGRAMA_Y_EXPLICACION.md` secciones 2 y 3, `TruckSpec.Bodies` | OK |
| 4. Escala (1,1,1) y anclas en los pivotes | `TruckGravityTests.Build_CreatesTenArticulationBodies` y `TruckSavedSceneTests` comprueban `localScale == 1`; anclas en `TruckBodyFactory.Configure` | OK |
| 5. Play sin drives: cae por gravedad y respeta límites | `TruckGravityTests` (`NoDrives_Settles_WithoutNaN`, `_RespectsLimits`, `_Settles_AtRest`, `_GripperHangsVertical`) | OK |
| 6. Stiffness, damping y forceLimit de raíz a punta | `TruckSpec.cs:108-136`; estimaciones en `DIAGRAMA_Y_EXPLICACION.md` sección 5 y `CALIBRACION.md` sección 5 | OK |
| 7. Target en `FixedUpdate`, `jointPosition` a la UI | `JointAxis.FixedUpdate` (`JointAxis.cs:119`); `TelemetryUI.Update` (`TelemetryUI.cs:94`) | OK |
| 8. Rigidbodies de la tarea, repetible | Ver la fila de tarea repetible | PARTIAL |
| 9. Build WebGL temprana | Hito 7 del `ROADMAP.md` (primera build en `CALIBRACION.md` sección 9); repetida en esta auditoría | OK |

## 4. Correcciones hechas en esta auditoría

- `LoadKillPlane` (`Assets/Scripts/Runtime/LoadKillPlane.cs`), agregado por `TruckLoadsFactory.CreateLoads` a cada cubo: si `y < TruckSpec.Loads.KillPlaneY` (-20 m) o la posición es NaN, el cubo vuelve a su punto de origen sin velocidad. No toca la física (solo reubica). Tests: `LoadKillPlaneTests` (3, EditMode), `TruckTaskTests.Loads_FallenOutOfWorld_RespawnAtSpawn` y la comprobación del componente en `TruckSavedSceneTests`. `Main.unity` regenerada.
- Textos desactualizados en `README.md` y `docs/ENTREGA.md` (conteo de tests, estructura del repositorio, rama).

## Pendiente / decisión del usuario

- **Publicar:** repositorio público, itch.io, YouTube y completar las URL del README (los 4 GAP).
- **Cubo lanzado por un giro rápido de la torre.** Con el brazo extendido y un cubo agarrado, mover el slider de la torre de golpe (o mantener `A`/`D` a 45 °/s con el brazo estirado) puede lanzarlo: el drive de la torre es rígido (60 000 N·m/rad) y una pinza limitada a 300 N por dedo (fricción máxima unos 486 N contra 78 N de peso) no sostiene la aceleración centrípeta a 6 a 7 m del eje. Es consecuencia de los valores calibrados, no un error de código, y con teclado la velocidad ya está acotada. No se tocó la física. Recomendación: documentarlo en el video ("girar despacio con carga") y, si se quiere evitarlo, limitar la velocidad del target que escriben los sliders (el eje avanza hacia el valor del slider a la velocidad de `ControlSpec.Speed`); es un cambio de comportamiento de la interfaz que hay que decidir.
- **Paquetes de IA pre-release en `manifest.json`** (`com.unity.ai.assistant`, `com.unity.pipeline`, `com.unity.ai.inference`): el simulador no los usa y una versión pre-release puede dejar de resolverse en el clon del docente. Recomendación: quitarlos antes de entregar y volver a probar un clon limpio. No se tocó por ser un cambio de dependencias.
- **Test del ciclo completo** (grúa toma un cubo del suelo, lo lleva a la caja, vuelca) solo con ejes y sin teletransportar: sería un test largo (decenas de segundos) y sensible a la calibración; se dejó la verificación manual de `CALIBRACION.md` sección 10. Recomendación: agregarlo si se quiere cerrar la salvedad.
- **Verificación manual en navegador real** de teclas sostenidas, orbitar con botón derecho y flechas sin scroll (pendiente desde `CALIBRACION.md` sección 9; la herramienta de prueba usa eventos sintéticos).
- Los 3 avisos de shader de `Hidden/Sentis/*` en la build vienen de `com.unity.ai.inference` (mismo motivo para quitar los paquetes de IA).
- `ProjectSettings.asset` pierde el define `SENTIS_ANALYTICS_ENABLED` cuando Unity reimporta y lo vuelve a agregar al compilar (deriva del Editor); no se versiona.

## Resultados de la corrida

Fecha 2026-11-23, Unity 6000.3.14f1, commit de partida `2c8bea6` más las correcciones de la sección 4. Resultados en `D:/Programas/Unity/audit_out` (fuera del repositorio).

| Prueba | Resultado |
|---|---|
| EditMode | 38 de 38 en verde (35 previos + 3 de `LoadKillPlaneTests`) |
| PlayMode completo | 44 de 44 en verde, unos 4.6 minutos (43 previos + `Loads_FallenOutOfWorld_RespawnAtSpawn`) |
| Log de Unity (edit, play, guardado de escena) | Sin errores de compilación ni de shader (`IndustrialToon`, `GradientSky` compilan); solo avisos de conexión del relay MCP del paquete de IA del Editor |
| Clon limpio de `HEAD` (Library nueva) | Abre y compila sin pasos manuales: EditMode 38 de 38 y `TruckSavedSceneTests` 3 de 3 |
| Build WebGL (`BuildScript.BuildWebGl`) | `Succeeded`, 19 312 035 bytes (18.4 MiB: wasm 12.05 MB, data 7.11 MB), 3 min 14 s de compilación (con caché de IL2CPP; la primera tardó 10 min) |
| Log de la build | Ninguna línea "not supported on this GPU" (esas salen en el navegador). Avisos: shaders `Terrain/Lit` de URP "All subshaders removed" (no se usan) y 3 avisos de `Hidden/Sentis/*` del paquete `com.unity.ai.inference`, que entra en la build aunque el simulador no lo usa |

**Prueba de humo en el navegador** (`python -m http.server 8765 --bind 127.0.0.1` en `Build/WebGL`, navegador integrado, Decompression Fallback):

- Carga completa en unos 20 s. Se ve el camión con el estilo toon y los contornos (no hay magenta ni negro), el panel industrial, los 5 cubos azules, el cielo en degradado y la pose plegada (Pluma Target 70.0° / Real 67.9°, Secundario -45.0° / -46.2°).
- Consola: solo las tres líneas conocidas de URP (`Hidden/CoreSRP/CoreCopy`, `StencilDitherMaskSeed`, `HDRDebugView`) y los avisos `INVALID_ENUM: getInternalformatParameter`; ningún error de JavaScript ni otras líneas de shader.
- Arrastrar el slider de la pluma: Target 70.0° pasó a 49.8° y Real a 46.6°, y la grúa bajó.
- FPS medidos con `requestAnimationFrame` durante 5 s: unos 8 FPS. El panel del navegador integrado es pequeño (686x525 con el canvas de 960x600) y probablemente renderiza por software; no sirve como medida de rendimiento. Medir en un navegador real con GPU.
- Queda el servidor local en el puerto 8765 (PID 31412).

