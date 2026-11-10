# Calibración

Registro de los valores que se ajustaron respecto de la Tabla Maestra y de las decisiones de física. Se actualiza en cada tarea de calibración. Estado actual: Tarea 3 (gravedad pura, sin drives).

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
| `JibLength` | 2.5 | 2.0 | Con el brazo largo, el cabezal no podía quedar sobre la caja (ver abajo) |
| `TelescopeLength` | 2.0 | 1.0 | Idem; recorrido útil 1.0 a 3.5 m |
| `BedLength` | 4.5 | 3.5 | El centro de la caja pasa de 2.65 m a 3.15 m de la torre, fuera del alcance mínimo del brazo plegado |

Constantes nuevas: `PalmSize`, `FingerSize`, `GroundSize`, `YardCenter`, `YardSize`, `HeadCenterElevation` (-30°: el cono de swing del cabezal queda centrado cuando el telescópico apunta 30° bajo la horizontal, así el cabezal cuelga vertical para elevaciones de -70° a +10°), y `TruckSpec.Materials` con las fricciones del spec (sección 6).

### Cinemática directa

Eje de la torre en (0, 2.2) (altura de la unión de la pluma: 0.7 + 1.5), distancia horizontal `x` desde ese eje y altura `y`, con `a` = ángulo de la pluma, `b` = ángulo del secundario respecto de la pluma, `s` = extensión del telescópico, `e = a + b` = elevación del telescópico:

```text
x = 3.0·cos(a) + (2.0 + 1.0 + s)·cos(e)
y = 2.2 + 3.0·sin(a) + (2.0 + 1.0 + s)·sin(e)
```

El origen del cabezal está en `(x, y)`; los dedos llegan 0.9 m más abajo (palma 0.3 + dedos 0.6).

Alcance máximo (a = 0, b = 0, s = 2.5): x = 3 + 2 + 1 + 2.5 = 8.5 m.

**Garra al suelo a 4.5 m lateral del eje del camión.** La torre gira 90° y el punto queda a 4.5 m del eje de la torre (las cargas están a ±1 m en Z de la torre, así que como mucho 4.6 m con la torre girada unos grados más). Se necesita el origen del cabezal a y ≈ 0.95 m (dedos a ras del suelo). Con a = 23°, b = -78°, s = 0:

```text
e = -55°
x = 3.0·cos(23°) + 3.0·cos(-55°) = 2.761 + 1.721 = 4.48 m
y = 2.2 + 3.0·sin(23°) + 3.0·sin(-55°) = 2.2 + 1.172 - 2.457 = 0.92 m
```

Dentro de los límites (pluma -15 a 75, secundario -125 a 15, telescópico 0 a 2.5). Una búsqueda exhaustiva (paso 1° y 0.1 m) con un margen de 10° respecto de los límites da más de 150 soluciones, con holgura de telescópico incluida. El cono de swing (-70° a +10°) cubre e = -55°: el cabezal cuelga vertical.

**Garra sobre el centro de la caja a más de 1 m de altura.** El centro de la caja está en z = -3.5 + 3.5/2 = -1.75, a 1.4 - (-1.75) = 3.15 m del eje de la torre. Con a = 53°, b = -114°, s = 0:

```text
e = -61°
x = 3.0·cos(53°) + 3.0·cos(-61°) = 1.806 + 1.454 = 3.26 m
y = 2.2 + 3.0·sin(53°) + 3.0·sin(-61°) = 2.2 + 2.396 - 2.624 = 1.97 m
```

Dedos a 1.07 m del suelo y unos 0.2 m sobre el piso de la caja (0.85 m). Con a = 60°, b = -110° el cabezal queda en (3.43, 2.50): dedos a 1.6 m. Con la geometría original (pluma 3.0, secundario 2.5, telescópico 2.0, caja de 4.5 m) no existía ninguna combinación dentro de los límites que ubicara el cabezal sobre el centro de la caja, porque secundario más telescópico miden al menos 4.5 m y el límite de -125° del secundario no deja inclinarlos lo suficiente hacia abajo y atrás. Hay que confirmarlo con el test numérico de la Tarea 4 y a ojo en el Editor.

**Pendiente de las tareas siguientes.** Con el cabezal a e = -55° el swing necesario respecto del centro es 25°, dentro del cono de ±40°. Si el agarre de la Tarea 5 necesita otra elevación, ajustar `HeadCenterElevation`.

## 4. Gravedad pura (sin drives)

Se corrieron los 6 tests de `TruckGravityTests` (5 s de simulación cada uno). Valores medidos a los 8 s en una corrida de diagnóstico (descartada, no queda en el repositorio):

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

## 5. Pendientes de calibración

- Agarre: `forceLimit` de los dedos (spec: bajar a cientos de newtons), fricción cubo contra dedo.
- Volcado: fricción del piso de la caja contra el cubo (resbala a ~25°) y apertura de la compuerta.
- Drives: stiffness y damping de la tabla con carga real; hundimiento τ/stiffness.
- WebGL: revisar solver y `Fixed Timestep` en el navegador.

## 6. Materiales físicos

| Material | Fricción dinámica | Fricción estática | Combine | Bounciness |
|---|---|---|---|---|
| `Mat_GomaPinza` (dedos y cubos) | 0.9 | 0.95 | Maximum | 0 |
| `Mat_AceroCaja` (piso de la caja) | 0.25 | 0.3 | Average | 0 |

Los valores viven en `TruckSpec.Materials`. Los materiales se crean en memoria en cada build (no son assets).
