# Calibración

Registro de los valores que se ajustaron respecto de la Tabla Maestra y de las decisiones de física. Se actualiza en cada tarea de calibración. Estado actual: Tarea 4 (drives calibrados, controlador, poses de alcance y de reposo).

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

La tolerancia del test es 0.5 m y no se tocó. Los objetivos se calcularon con la cinemática directa y se corrigieron por el hundimiento de la pluma y el secundario (sección 5, 0.15 a 0.25 m de bajada del cabezal). Las alturas se eligieron para que los dedos queden unos 0.3 m sobre el suelo (`GroundReach`) o sobre el piso de la caja (`BedReach`): con el cabezal 0.2 m más bajo los dedos tocaban el suelo o el piso, el cabezal se inclinaba hasta 57° de la vertical y la torre se frenaba en -94°. La Tarea 5 baja desde esa pose hasta el cubo con la pluma. El piso de la caja está a 0.85 m, así que el cabezal sobre el centro queda a 1.2 m del piso.

**Pose de reposo (`Stowed`).** Pluma elevada 70° y secundario plegado a -45° (elevación del telescópico +25°): el brazo apunta hacia atrás y arriba, a 5.7 m de altura. Con la pose de ceros el secundario queda tendido sobre la caja y el volteo de la caja lo choca: la caja sólo llegaba a 7.5° en lugar de 27.5°. Con `Stowed` y la caja a 55° la separación mínima entre cualquier collider de la grúa y de la caja (muestreo de esquinas, aristas y caras cada 0.25 s durante 4 s) es 0.73 m. El secundario queda a 80° de su tope (-125°); la pluma, a 5° del suyo (75°), llega a 67.9° por el hundimiento. Los tests de eje (`Axis_ReachesTarget`, `Axis_NeverExceedsLimits`) arrancan desde `Stowed` por la misma razón (ver sección 5).

**Pendiente de las tareas siguientes.** Con el cabezal a e = -49° (`GroundReach`) el swing necesario respecto del centro es 19°, dentro del cono de ±40°; en `BedReach` (e = -67°) está en el borde del cono (-70°). Si el agarre de la Tarea 5 necesita otra elevación, ajustar `HeadCenterElevation`.

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
| Dedos | sin carga estática; 8750 N de apriete con target 0.35 m contra un obstáculo | 20 000 | n/a | 3.5 (m = 5 kg) | ninguno aún; el `forceLimit` se baja en la Tarea 5 |

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

- Agarre: `forceLimit` de los dedos (spec: bajar a cientos de newtons), fricción cubo contra dedo.
- Volcado: fricción del piso de la caja contra el cubo (resbala a ~25°) y apertura de la compuerta.
- WebGL: revisar solver y `Fixed Timestep` en el navegador.

## 7. Materiales físicos

| Material | Fricción dinámica | Fricción estática | Combine | Bounciness |
|---|---|---|---|---|
| `Mat_GomaPinza` (dedos y cubos) | 0.9 | 0.95 | Maximum | 0 |
| `Mat_AceroCaja` (piso de la caja) | 0.25 | 0.3 | Average | 0 |

Los valores viven en `TruckSpec.Materials`. Los materiales se crean en memoria en cada build (no son assets).
