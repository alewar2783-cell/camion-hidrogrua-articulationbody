# Calibración

Registro de los valores que se ajustaron respecto de la Tabla Maestra y de las decisiones de física. Se actualiza en cada tarea de calibración. Estado actual: Tarea 5 (cargas, agarre por fricción y descarga con compuerta pasiva).

## 1. Solver y cuerpos

| Parámetro | Valor | Dónde |
|---|---|---|
| `solverIterations` | 16 | todos los `ArticulationBody` |
| `solverVelocityIterations` | 4 | todos los `ArticulationBody` |
| `Fixed Timestep` | 0.02 s (sin cambios) | `ProjectSettings/TimeManager` |
| `jointFriction`, `angularDamping`, `linearDamping` | valores por defecto de Unity | sin tocar |
| `inertiaTensor` | automático para los 10 cuerpos | sin overrides |

Con estos valores la gravedad pura fue estable en la primera corrida: sin `NaN`, sin vibración y velocidades residuales mínimas (sección 4). No hizo falta fijar el tensor de inercia a mano ni subir el solver; si el agarre (Tarea 5) o el volcado (Tarea 6) muestran inestabilidad, este es el primer lugar a revisar.

## 2. Reglas de construcción que se descubrieron

- **`immovable` se aplica después de activar.** La propiedad se ignora mientras el GameObject raíz está inactivo (el generador construye con la raíz inactiva para que la articulación se cree de una vez). Por eso `TruckBuilder.Build` la fija en la raíz recién después de `SetActive(true)`. Sin esto el camión entero caía 0.3 m hasta apoyar el chasis en el suelo.
- **`forceLimit = 0` en la tabla significa "pasivo", no "sin fuerza".** `Cabezal_Pinza` y `Compuerta_Trasera` tienen `forceLimit` 0 y `damping` 25 y 10. Si se copiara el 0, el límite de fuerza anularía el amortiguamiento. El generador usa `float.MaxValue` cuando el `forceLimit` de la tabla es 0.
- **Anclas.** El movimiento ocurre siempre sobre el eje X del ancla. `anchorRotation = FromToRotation(right, eje)` y `parentAnchorRotation = inversa(rotación del padre) * rotación del hijo * anchorRotation`, de modo que el joint arranca con posición cero en la pose de construcción. `anchorPosition = 0`: el origen de cada cuerpo es su pivote.
- **Orientación de los eslabones.** El brazo (pluma, secundario, telescópico) apunta a su +Z local; con una rotación de 180° sobre X apunta hacia atrás (la caja) y el X local sigue siendo el X del mundo, así un ángulo positivo levanta el brazo (-15° a 75° de elevación). La caja se construye girada 180° sobre Y para que +X local sea -X del mundo y un ángulo positivo levante el frente. La compuerta cuelga de su borde superior y un ángulo positivo la abre hacia atrás.
- **Cabezal esférico.** Eje del cono de swing = Y local (`anchorRotation = FromToRotation(right, up)`), `twistLock = Locked`, `swingYLock = swingZLock = Limited` con ±40°. Los límites y el amortiguamiento van en `yDrive` y `zDrive`.
- **Colisiones entre cuerpos.** Unity no colisiona los eslabones vecinos (padre e hijo) pero sí los no vecinos de la misma articulación: el brazo se apoya sobre la compuerta. La varilla visual del telescópico no tiene collider por esa razón.
- **PlayMode asmdef.** `Truck.Tests.PlayMode` tenía `includePlatforms: ["Editor"]`, y Unity trata esos assemblies como EditMode (el runner de PlayMode encontraba 0 tests). Se dejó `includePlatforms` vacío; sigue referenciando a `Truck.Editor`, que sigue compilando en el Editor.

## 3. Geometría final (`TruckSpec.Geometry`) y alcance

Verificación analítica (no hay Editor visual en esta tarea; el test numérico de alcance con drives es de la Tarea 4).

Cambios respecto de los valores iniciales:

| Constante | Antes | Ahora | Motivo |
|---|---|---|---|
| `TowerHeight` | 1.2 | 1.5 | El brazo apoya a 2.2 m y pasa sobre los laterales de la caja (tope en 1.75 m) |
| `JibLength` | 2.5 | 1.5 (Tarea 4; en la Tarea 3 fue 2.0) | Con el brazo largo, el cabezal no podía quedar sobre la caja (ver abajo) |
| `TelescopeLength` | 2.0 | 0.5 (Tarea 4; en la Tarea 3 fue 1.0) | Idem; recorrido útil 0.5 a 3.0 m |
| `BedLength` | 4.5 | 3.5 | El centro de la caja pasa de 2.65 m a 3.15 m de la torre, fuera del alcance mínimo del brazo plegado |

Constantes nuevas: `PalmSize`, `FingerSize`, `GroundSize`, `YardCenter`, `YardSize`, `HeadCenterElevation` (-30°: el cono de swing del cabezal queda centrado cuando el telescópico apunta 30° bajo la horizontal, así el cabezal cuelga vertical para elevaciones de -70° a +10°), y `TruckSpec.Materials` con las fricciones del spec (sección 6).

### Cinemática directa

Eje de la torre en (0, 2.2) (altura de la unión de la pluma: 0.7 + 1.5), distancia horizontal `x` desde ese eje y altura `y`, con `a` = ángulo de la pluma, `b` = ángulo del secundario respecto de la pluma, `s` = extensión del telescópico, `e = a + b` = elevación del telescópico:

```text
x = 3.0·cos(a) + (1.5 + 0.5 + s)·cos(e)
y = 2.2 + 3.0·sin(a) + (1.5 + 0.5 + s)·sin(e)
```

El origen del cabezal está en `(x, y)`; los dedos llegan 0.9 m más abajo (palma 0.3 + dedos 0.6). Alcance máximo (a = 0, b = 0, s = 2.5): x = 3 + 2 + 2.5 = 7.5 m.

**Por qué la Tarea 4 volvió a acortar el brazo.** Con secundario 2.0 y telescópico 1.0 (valores de la Tarea 3), el centro de la caja (a 3.15 m del eje de la torre) exigía un secundario en -116° (9° del tope de -125°). Con secundario 1.5 y telescópico 0.5 la pose sobre la caja necesita -105° (20° de margen) y el alcance máximo sigue siendo 7.5 m, de sobra para 4.5 m. La búsqueda se hizo con un script de cinemática directa (paso 1°) y se verificó en PlayMode.

### Conjuntos de targets (`TruckSpec.Poses`)

Orden `Torre, Pluma, Secundario, Telescópico, Pinza, Caja` (grados y metros). La torre en -90° apunta la pluma hacia +X (un ángulo positivo gira hacia -X).

| Pose | Torre | Pluma | Secundario | Telescópico | Pinza | Caja | Resultado medido |
|---|---|---|---|---|---|---|---|
| `GroundReach` | -90 | 32 | -81 | 1.0 | 0 | 0 | cabezal en (4.42, 1.20, 1.39); objetivo (4.5, 1.25, 1.4); error 0.09 m; cabezal a 0.3° de la vertical |
| `BedReach` | 0 | 38 | -105 | 0 | 0 | 0 | cabezal en (0.01, 2.03, -1.73); objetivo (0, 2.05, -1.75); error 0.03 m; cabezal a 0.7° de la vertical |
| `Stowed` | 0 | 70 | -45 | 0 | 0 | 0 | cabezal en (0.00, 5.72, -1.59), velocidades < 0.005 m/s |
| `GraspApproach` | -76.6 | 32 | -81 | 1.0 | 0 | 0 | sobre el primer cubo, dedos a unos 0.3 m del suelo (Tarea 5, sección 8) |
| `GraspReach` | -76.6 | 31.2 | -83.2 | 1.0 | 0 | 0 | cabezal en (4.20, 1.08, 0.40) sobre `GroundSpawn[0]`, a 0.1° a 0.5° de la vertical, dedos 0.175 m sobre el suelo (Tarea 5, sección 8) |

La tolerancia del test es 0.5 m y no se tocó. Los objetivos se calcularon con la cinemática directa y se corrigieron por el hundimiento de la pluma y el secundario (sección 5, 0.15 a 0.25 m de bajada del cabezal). Las alturas se eligieron para que los dedos queden unos 0.3 m sobre el suelo (`GroundReach`) o sobre el piso de la caja (`BedReach`): con el cabezal 0.2 m más bajo los dedos tocaban el suelo o el piso, el cabezal se inclinaba hasta 57° de la vertical y la torre se frenaba en -94°. La Tarea 5 baja desde esa pose hasta el cubo con la pluma. El piso de la caja está a 0.85 m, así que el cabezal sobre el centro queda a 1.2 m del piso.

**Pose de reposo (`Stowed`).** Pluma elevada 70° y secundario plegado a -45° (elevación del telescópico +25°): el brazo apunta hacia atrás y arriba, a 5.7 m de altura. Con la pose de ceros el secundario queda tendido sobre la caja y el volteo de la caja lo choca: la caja sólo llegaba a 7.5° en lugar de 27.5°. Con `Stowed` y la caja a 55° la separación mínima entre cualquier collider de la grúa y de la caja (muestreo de esquinas, aristas y caras cada 0.25 s durante 4 s) es 0.73 m. El secundario queda a 80° de su tope (-125°); la pluma, a 5° del suyo (75°), llega a 67.9° por el hundimiento. Los tests de eje (`Axis_ReachesTarget`, `Axis_NeverExceedsLimits`) arrancan desde `Stowed` por la misma razón (ver sección 5).

**Cono de swing.** Con el cabezal a e = -49° (`GroundReach`) el swing necesario respecto del centro es 19°, dentro del cono de ±40°; en `BedReach` (e = -67°) está en el borde del cono (-70°). La Tarea 5 resolvió el agarre sin tocar `HeadCenterElevation` (sección 8.1).

## 4. Gravedad pura (sin drives)

Se corrieron los 6 tests de `TruckGravityTests` (5 s de simulación cada uno). Valores medidos a los 8 s en una corrida de diagnóstico (descartada, no queda en el repositorio) con la geometría de la Tarea 3 (secundario 2.0 m y telescópico 1.0 m; la Tarea 4 los acortó, y los 6 tests de gravedad pura siguen pasando):

| Cuerpo | Posición de joint | Velocidad lineal | Velocidad angular |
|---|---|---|---|
| Brazo_Principal | -10.0° | 0.002 m/s | 0.001 rad/s |
| Brazo_Secundario | +15.0° (tope) | 0.003 m/s | 0.001 rad/s |
| Tramo_Telescopico | 0.000 m | 0.002 m/s | 0.001 rad/s |
| Cabezal_Pinza | ángulo entre su +Y y la vertical: 0.3° | 0.002 m/s | 0.001 rad/s |
| Dedos | 0 | 0.001 m/s | 0.001 rad/s |
| Caja_Volcadora | 0° | 0.001 m/s | 0.000 rad/s |

En el segundo 1, cuando el brazo todavía cae, el máximo era 0.11 m/s lineal (dedos) y 0.17 rad/s angular (cabezal). Los umbrales de los tests (0.2 m/s y 0.5 rad/s) se cumplen con margen y no se tocaron.

**Pose de reposo sin drives.** Todas las rigideces valen 0, así que el brazo cae. Termina con la pluma entre -10° y -15° y el secundario en su tope (+15°), apoyado sobre el borde superior de la compuerta trasera (es el contacto que lo detiene). Si se quitan los colliders de la caja y la compuerta, la pluma llega a su tope de -15.0° y los dedos apoyan en el suelo, también estable. En ambos casos los joints respetan los límites.

**Cabezal vertical.** Se cumple con el umbral original (< 5°): quedó a 0.1 a 0.4° de la vertical, así que no hizo falta la variante "en el límite del swing". El cabezal se apoya en el cono de ±40° con unos 35° de swing sobre su eje (e = +5°, centro del cono en -30°).

## 5. Drives (Tarea 4)

Todos los valores de la Tabla Maestra funcionaron sin cambios. Solver: `solverIterations` 16 y `solverVelocityIterations` 4 (sin subir) y `Fixed Timestep` 0.02 s. `jointFriction`, `angularDamping` y `linearDamping` quedaron en los valores por defecto de Unity (0.05), que ya son pequeños; no hizo falta tocarlos. Los pasivos conservan stiffness 0 y damping 25 (cabezal) y 10 (compuerta), `forceLimit` ilimitado.

Calibración de la raíz a la punta. Torque estático `τ = Σ m·g·brazo` en el peor caso (pluma y secundario horizontales, telescópico extendido 2.5 m, centro de masa de cada eslabón en su mitad); hundimiento `τ/stiffness` (la stiffness de un drive angular es por radián); razón de amortiguamiento `ζ = damping / (2·√(stiffness·I))` con `I = Σ m·r²` de lo que cuelga del joint en esa pose.

| Joint | Estático (peor caso) | `forceLimit` | Hundimiento peor caso | ζ estimado | Cambio vs Tabla Maestra |
|---|---|---|---|---|---|
| Torre (Y) | sin carga estática (la gravedad es perpendicular al eje) | 120 000 | n/a | 0.19 (I = 4300 kg·m²) | ninguno |
| Pluma | 8000 N·m | 180 000 | 5.1° | 0.23 | ninguno |
| Secundario | 3000 N·m | 110 000 | 2.7° | 0.37 | ninguno |
| Telescópico | 590 N (60 kg en vertical) | 60 000 | 0.013 m | 1.4 | ninguno |
| Caja | 4900 N·m (250 kg a 1.75 m + 4 cubos de 8 kg a 2 m) | 250 000 | 2.4° | 0.51 | ninguno |
| Dedos | sin carga estática; 8750 N de apriete con target 0.35 m contra un obstáculo | 300 (era 20 000) | n/a | 3.5 (m = 5 kg) | `forceLimit` bajado en la Tarea 5 (sección 8.2) |

El torque estático consume entre 1% y 4% del `forceLimit`, así que el límite de fuerza nunca satura con carga normal. El peor caso de la pluma (5.1°) queda debajo de la tolerancia de 6° pero cerca; con la pluma plegada el hundimiento es menor.

Valores medidos en PlayMode con los seis ejes a la vez en 50% del rango y luego en el tope (corrida de diagnóstico descartada, 8 s):

| Eje | Target | Real | Error |
|---|---|---|---|
| Pluma (50%) | 30° | 28.7° | -1.3° |
| Secundario (50%) | -55° | -56.7° | -1.7° |
| Telescópico (50%) | 1.25 m | 1.26 m | 0.01 m |
| Caja (50%) | 27.5° | 26.0° | -1.5° |
| Pinza (50%) | 0.175 m | 0.17 m | 0.005 m |
| Torre (tope 170°) | 170° | 170.0° | 0 |
| Pluma (tope 75°) | 75° | 74.1° | -0.9° |
| Secundario (tope 15°) | 15° | 14.9° | -0.1° |
| Caja (tope 55°) | 55° | 54.0° | -1.0° |

Los tests de la Tarea 4 usan `Stowed` como base y cumplen las tolerancias del plan (6° angular, 0.08 m lineal), que no se tocaron. Ningún eje pasa su tope. La torre, el eje con menor ζ, rebasa unos 0.8° al girar 90° y asienta en menos de 2 s; ningún eje oscila de forma sostenida y las velocidades residuales quedan por debajo de 0.005 m/s. Con la pose de ceros la pluma queda en -1.4° por el peso del brazo.

**Choques de la grúa contra la caja.** Con todos los targets en 0 el secundario queda tendido sobre la caja. Un secundario a -55° o una caja a 27.5° chocan con él (reales -35.7° y 7.5° en la primera corrida, con la pose de ceros como base). No es un defecto del drive sino de la geometría: hay que plegar la grúa antes de volcar. Por eso los tests por eje parten de `Stowed`.

**Cabezal.** Cuelga vertical (0.3° a 0.7°) en `GroundReach` y `BedReach`, sin oscilar. Con los dedos apoyados o arrastrando contra el suelo, el piso de la caja o la compuerta se inclina hasta 57°.

**Log de Unity.** No apareció ninguna advertencia de física en el log de Unity en las corridas de PlayMode y EditMode.

## 6. Pendientes de calibración

- WebGL: revisar solver y `Fixed Timestep` en el navegador (ver sección 9).

## 7. Materiales físicos

| Material | Fricción dinámica | Fricción estática | Combine | Bounciness |
|---|---|---|---|---|
| `Mat_GomaPinza` (dedos y cubos) | 0.9 | 0.95 | Multiply (era Maximum, sección 8.3) | 0 |
| `Mat_AceroCaja` (piso de la caja) | 0.25 | 0.3 | Average | 0 |
| `Mat_Suelo` (suelo) | 0.1 | 0.1 | Average | 0 |

Los valores viven en `TruckSpec.Materials`. Los materiales son assets en `Assets/PhysicsMaterials` y se reescriben desde el spec en cada build.

## 8. Cargas, agarre y descarga (Tarea 5)

Los cubos `Load_0..Load_4` (uno por punto de `GroundSpawn`) son `Rigidbody` de 8 kg, `ContinuousDynamic`, `Interpolate`, escala 0.5 y `Mat_GomaPinza`. Cuelgan de un objeto `Loads` aparte del camión, así que `Build` los borra y los vuelve a crear sin tocar el árbol de articulaciones. `TruckLoadsFactory.SpawnBedLoads()` mueve los cuatro primeros a `BedSpawn`. Las medidas salen de corridas de diagnóstico descartadas (no quedan en el repositorio).

### 8.1 Pose de agarre (arrastres 1, 2 y 3)

- **Barrido contra el cubo.** Ir a la pose de agarre con el cubo en su sitio lo empuja (el dedo lo arrastró de (4.2, 0.4) a (4.2, -0.1)) y el cabezal quedaba apoyado sobre el cubo con 25° a 35° de inclinación. Las poses de la Tarea 4 se midieron sin cubos. El test aparta el cubo, pasa por `Stowed` (arrastre 3), va a `GraspApproach` (5 s) y baja a `GraspReach` (3 s). Con esa secuencia el cabezal queda siempre en (4.20, 1.08, 0.40), a 0.1° a 0.5° de la vertical, en seis corridas.
- **Descenso emparejado (arrastre 1).** Se baja con la pluma y el secundario a la vez: pluma 32° a 31.2° y secundario -81° a -83.2°. El cabezal pasa de y = 1.20 a 1.08 m. Elevación medida del telescópico: pluma 27.7° + secundario -84.2° = -56.5°, o sea 26.5° de swing sobre los 40° del cono. `HeadCenterElevation` no cambia.
- **Por qué 0.12 m de descenso y no 0.28 m.** Con la altura de cinemática directa 0.04 m más baja (dedos a 0.0 a 0.1 m del suelo) las puntas rozaron el suelo y el cabezal se inclinó 28° a 33° y quedó trabado: el suelo agarra la goma de los dedos (fricción efectiva 0.9). Con los dedos a 0.175 m el cabezal cuelga vertical. El cubo mide 0.5 m y los dedos 0.6 m, así que lo cubren entre 0.175 y 0.5 m: 0.325 m de contacto.
- **Cabezal sobre el cubo.** La pose se calculó con cinemática directa para (x, z) = `GroundSpawn[0]` = (4.2, 0.4), torre -76.6°. Medido: (4.20, 0.40), error menor que 0.01 m.
- **Hundimiento (arrastre 2).** Los objetivos incluyen el hundimiento medido de la pluma: 31.2° pedido, 27.7° real (3.5°; el peor caso estimado en la sección 5 era 5.1°). No se tocó ninguna tolerancia: los tests de la Tarea 4 siguen con 6° y 0.08 m.
- **`BedReach` no cambia.** Sigue en e = -67° (3° del borde del cono). No hace falta bajar en la caja: los dedos cuelgan 0.28 m sobre el piso (0.85 m) y un cubo soltado cae esa altura. Si una tarea futura necesita bajar 0.28 m con la pluma sola, son 5.3° de pluma, e = -72° y el cono (-70°) se excede en 2°; hay que bajar con el secundario o el telescópico.

### 8.2 `forceLimit` de los dedos (arrastre 4)

Con target 0.35 m contra un cubo de 0.5 m el error es 0.2 m por dedo y la rigidez de 25 000 N/m daría 5000 N. Se midió con el cubo agarrado: 1.5 s de cierre y luego 15° de pluma (3 s). `gap` es la luz entre las caras internas de los dedos (0.5 = sin interferencia).

| `forceLimit` (N) | `gap` sostenido (m) | Interferencia | Elevación del cubo (m) | Deriva cubo-cabezal (m) |
|---|---|---|---|---|
| 20 000 (Tabla Maestra) | 0.418 | 82 mm | 1.16 | 0.00 |
| 2000 | 0.444 a 0.468 | 32 a 56 mm | 1.16 | 0.00 |
| 1500 | 0.483 | 17 mm | 1.16 | 0.00 |
| 1000 | 0.493 | 7 mm | 1.14 | 0.02 |
| 700 | 0.495 a 0.498 | 2 a 5 mm | 1.15 | 0.00 |
| 500 | 0.499 a 0.500 | ninguna | 1.14 | 0.02 |
| **300 (elegido)** | **0.499 a 0.500** | **ninguna** | **1.15** | **0.00** |
| 150 | 0.500 | ninguna | 1.15 | 0.00 |
| 100 | 0.500 | ninguna | 1.13 | 0.03 |

Se eligió 300 N porque queda 2.3 veces por debajo del primer valor con interferencia visible (700 N) y la fricción lo sostiene con sobra: el peso es 78 N y dos caras con fricción 0.81 a 300 N dan 486 N (6 veces). El cubo se mantuvo agarrado hasta 100 N. Con la fuerza limitada el cierre no centra el cubo: queda en equilibrio indiferente y se corre hasta que un dedo llega a su tope abierto (`Real` de un dedo en 0 y del otro en 0.30 m); no afecta al agarre. Con el apriete bajo el cubo se inclina entre 10° y 15° al subir (el agarre queda unos 0.1 m sobre su centro de masa); con 2000 N la inclinación es menor que 3°, pero hay interferencia. El test del agarre exige `gap` mayor o igual que 0.48 m. Con los dedos cerrados sin carga la velocidad máxima de cualquier cuerpo es 2.4 m/s (límite del test: 5 m/s) y el dedo izquierdo llega a 0.350 m.

### 8.3 Fricción (volcado)

Con los valores del spec el cubo no resbalaba sobre el piso de acero antes de 40°: un cubo solo sobre la caja, con rampa de 3°/s, no se movió hasta el final del ensayo (14 s, 42° pedidos, unos 40° reales), y con la caja subiendo a 20°/s las cargas empezaron a deslizar a 43.6°. La causa no es el valor del acero: con `Mat_GomaPinza` en `Maximum`, el modo de mayor prioridad, la goma gana en todo par y `Mat_AceroCaja` no interviene. Cambiar solo `Mat_AceroCaja` no habría servido.

Cambio: `Mat_GomaPinza` pasa a `Multiply` (mismas fricciones 0.9 y 0.95) y `Mat_AceroCaja` conserva 0.25 y 0.3 con `Average`. Medido con un cubo solo y rampa de 3°/s, variando la fricción del piso (para mostrar la sensibilidad):

| Piso de acero (estática = dinámica salvo el spec) | Resbala a (real) |
|---|---|
| 0.05 | 7.3° |
| 0.2 | 21.5° |
| 0.25 dinámica y 0.3 estática (spec) | 27.5° |
| 0.35 | 33.1° |
| 0.5 | más de 40° |

Con 27.5° se cumple el objetivo del spec (unos 25°) sin tocar los valores del acero. Entre dedo y cubo la fricción pasa a 0.81 dinámica y 0.90 estática, suficiente según 8.2. Contra el suelo y las paredes (sin material) queda en 0.54. El test `Build_PhysicsMaterials_ArePersistedAssetsWithSpecValues` (Tarea 3) ahora lee los modos desde `TruckSpec.Materials`.

### 8.4 Volcado y compuerta

- Los tests de volcado ponen el target de `Caja` en 45° de una vez, como pide el plan. La caja llega a unos 47° en 0.5 s, las cargas salen disparadas (hasta y = 2.9 m) y terminan en el suelo detrás de la caja; Con la configuración final la compuerta llega hasta 70.1° (81.4° con el material del spec), los cuatro cubos de la caja quedan en el suelo a z = -4.1 y -5.3 m y la compuerta vuelve a 0.00° al bajar la caja. El quinto cubo no se mueve del suelo.
- **Volcado a velocidad de teclado (20°/s).** Con el suelo del spec (sin material, fricción efectiva 0.54 contra la goma) las cargas deslizaban a unos 30° a 36°, chocaban con la compuerta (que cuelga unos 0.6 m detrás de la bisagra) y caían al pie de la caja en dos capas (suelo y encima). La compuerta quedaba apoyada en la pila a 64° a 75° y, al bajar la caja, seguía abierta (34° a 53°) hasta el reinicio. Pasaba igual con 45° y con 55°, y separar las filas de `BedSpawn` no lo cambiaba. Los tests con el target en 45° de golpe lo ocultaban porque la caja gira en 0.5 s y lanza las cargas.
- **Qué no sirvió.** Acortar la compuerta de 0.9 a 0.5 m: las cargas siguen cayendo juntas junto al chasis (z = -3.8 a -4.2) y la segunda fila queda encima de la primera (y = 0.65 a 0.70). Un suelo con fricción 0.3 (efectiva 0.27) tampoco.
- **Qué sirvió: un material propio para el suelo** (`Mat_Suelo`, asset, `TruckSpec.Materials.Ground*`: fricción dinámica y estática 0.1, `Average`). Contra la goma (`Multiply`) da 0.09: las cargas que caen se deslizan y despejan la zona, la segunda fila cae sobre el suelo y no sobre la primera. Resultado con la caja subiendo a 20°/s hasta 55°: las cuatro cargas terminan en el suelo (y = 0.25) a z entre -4.3 y -5.8, y la compuerta vuelve a menos de 5° al bajar la caja a 20°/s. No se tocó la geometría de la compuerta, la Tabla Maestra (masa 20, amortiguamiento 10, límites 0 a 95°), el tope de 55° ni la fricción del acero (0.25/0.3). Con una fricción de 0.05 (prueba con compuerta de 0.5 m) las cargas llegan a z = -5.8; 0.1 alcanza.
- **Consecuencia.** Las cargas del suelo y el cubo que se empuja con los dedos deslizan con facilidad (0.09). El agarre no cambia porque depende de la fricción dedo-cubo (0.81). Los tests de agarre y de reposo (10 s) siguen en verde.
- **Criterio de los tests de descarga.** Se evalúan solo las cuatro cargas de la caja (índices 0 a 3): cada una debe quedar detrás del borde trasero (z menor que la bisagra) y con el centro por debajo de 0.5 m (apoyada en el suelo, no encima de otra: una carga apilada queda por encima de 0.7 m). `Dump_SlowTilt_EmptiesBedAndGateCloses` sube la caja a 20°/s con un incremento por cuadro (`JointAxis.Step`), espera, aplica el criterio, baja a 20°/s y exige compuerta menor que 5°.
- La compuerta se abre sola (gravedad y empuje de las cargas) en todos los casos medidos; no hizo falta modificar su geometría ni su amortiguamiento.

## 9. WebGL (Tarea 7)

### Escena y configuración

- `Tools/Truck/Build And Save Main Scene` (`TruckSceneSaver.BuildAndSave`, también por línea de comandos: `-batchmode -quit -executeMethod Truck.TruckSceneSaver.BuildAndSave`) abre `Assets/Scenes/Main.unity`, ejecuta `TruckBuilder.Build(BuildMode.Full)` en Edit mode y guarda. Es idempotente: dos corridas dejan un solo `Truck`, `Interface`/Canvas, EventSystem, `Main Camera`, `Loads` y `Environment`. No aparece ninguna advertencia de `jointPosition` porque el ajuste a la pose plegada ocurre en `JointAxis.Start` (runtime). No hizo falta cambiar el código de runtime: el test `TruckSavedSceneTests` carga la escena guardada y confirma que nada se perdió al serializar.
- `Tools/Truck/Configure WebGL` (`BuildScript.ConfigureWebGl`) deja reproducible desde un clon limpio: nombre de producto `Camion Hidrogrua`, compresión Gzip, Decompression Fallback activado, Data caching desactivado y `Main` como única escena de Build Settings.
- `Tools/Truck/Build WebGL` (`BuildScript.BuildWebGl`) compila a `Build/WebGL` (ignorada por Git): `Unity.exe -batchmode -quit -projectPath <proyecto> -buildTarget WebGL -executeMethod Truck.BuildScript.BuildWebGl -logFile <log>`.

### Resultado de la primera build

- Tiempo: 9 min 57 s de compilación (13 min 47 s en total con el cambio de plataforma y la reimportación). Resultado `Succeeded`, sin errores de compilación.
- Tamaño: 19.4 MB (`.wasm.unityweb` 12.0 MB, `.data.unityweb` 7.2 MB, `.framework.js.unityweb` 89 KB, `.loader.js` 40 KB, `index.html` 5.6 KB).
- Servidor local: `python -m http.server 8765 --bind 127.0.0.1` dentro de `Build/WebGL`. Python no manda `Content-Encoding`, así que corre con el Decompression Fallback (el caso de itch.io).
- Chrome embebido: carga completa en unos 15 s, sin errores de JavaScript. En la consola aparecen tres líneas `ERROR: Shader Hidden/... is not supported on this GPU` (CoreCopy, StencilDitherMaskSeed, HDRDebugView; shaders internos de URP que el proyecto no usa) y avisos `INVALID_ENUM: getInternalformatParameter`; el render es correcto. Se ve la escena completa (camión, 5 cubos, panel de 6 sliders, botón y leyenda) y la pose plegada inicial (pluma 70°, secundario -45°).
- Verificado en el navegador: arrastrar el slider de la torre mueve la grúa (Target 98.6°, Real 98.6°). Un `contextmenu` y un `keydown` de flecha sobre el canvas llegan con `defaultPrevented = true`; a 1024x768 la página no tiene scroll (`scrollHeight` igual a `innerHeight`).
- Al compilar, Unity migró `Assets/Settings/*.asset` (URP asset version 13 y valores de prefiltrado de variantes) y agregó el define `SENTIS_ANALYTICS_ENABLED` a `ProjectSettings`; esos cambios se versionaron con la build.

### Pendiente de verificar en el navegador

- Teclado real (mantener apretadas las teclas): los eventos sintéticos de la herramienta no sostienen `isPressed`, así que los 6 pares de teclas, el reinicio con Retroceso/P y la velocidad de cada eje quedan sin probar.
- Click derecho + arrastrar para orbitar y rueda para zoom, sin menú contextual en la ventana real.
- Flechas (caja volcadora) sin desplazar la página, también con una ventana más chica que el alto del canvas.
- Paridad física con el Editor: agarre de un cubo y elevación (tres repeticiones), volcado a 20°/s con compuerta que se abre y se cierra, y reposo de las cargas 10 s. Si algo difiere, ajustar solver, `Fixed Timestep` o `forceLimit` y anotarlo aquí.
- Rendimiento: cuadros por segundo con el solver de 16/4 iteraciones en un navegador con GPU real.
- Ventana privada y carga desde el host definitivo (itch.io), con la compresión servida por el servidor o por el fallback.

### Criterio de apilado en los tests de descarga (ronda de revisión 1)

`Dump_SlowTilt_EmptiesBedAndGateCloses` falló una vez en siete corridas con `Load_2` en (-0.57, 0.79, -4.07): un cubo apoyado sobre otro en el corralón. El spec solo pide que las cargas caigan al corralón y que la compuerta se abra y se cierre; un cubo sobre otro es físicamente válido, así que el criterio "sin apilar" (`y < 0.5`) era más estricto que el spec. No se tocó la física. Ahora cada carga de la caja debe quedar detrás de `BedHinge.z` y por debajo de 1.0 m (dos alturas de cubo más margen: admite una segunda capa y excluye seguir sobre el piso de la caja, cuyo tope está en 0.85 m). Lo que detecta una compuerta trabada es la aserción de compuerta menor que 5° al bajar la caja.

