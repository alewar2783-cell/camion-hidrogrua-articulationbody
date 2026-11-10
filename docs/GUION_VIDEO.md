# Guion del video (2 a 3 minutos)

Objetivo: mostrar en vivo, sin cortes de edición en la maniobra, la carga de un cubo, el depósito en la caja, el repliegue de la grúa y la descarga con apertura de la compuerta, con la telemetría `Target` vs `Real` a la vista. Grabar la build de itch.io (o el Editor) con el panel izquierdo completo en pantalla.

## Cómo salen los tiempos

Los tiempos de mantener cada tecla son `recorrido / velocidad` con las velocidades de `TruckSpec` (torre 45 °/s, pluma 30 °/s, secundario 35 °/s, telescópico 0.8 m/s, pinza 0.4 m/s, caja 20 °/s) y los recorridos entre las poses de `TruckSpec.Poses`. El `Real` llega al `Target` con un retardo de uno a dos segundos, así que después de cada movimiento conviene esperar a que el `Real` alcance al `Target` antes de seguir. Son valores aproximados: ensayar la maniobra varias veces antes de grabar.

Poses (torre, pluma, secundario, telescópico), de `TruckSpec.Poses`:

| Pose | Valores | Qué es |
|---|---|---|
| `Stowed` | 0°, 70°, -45°, 0 m | grúa plegada (pose inicial) |
| `GraspApproach` | -76.6°, 32°, -81°, 1.0 m | cabezal sobre el primer cubo, dedos a unos 0.3 m del suelo |
| `GraspReach` | -76.6°, 31.2°, -83.2°, 1.0 m | descenso: dedos a 0.175 m del suelo, alrededor del cubo |
| `BedReach` | 0°, 38°, -105°, 0 m | cabezal a 1.2 m del piso de la caja |

## Guion

| Tiempo | Qué se ve | Controles y valores | Texto en pantalla |
|---|---|---|---|
| 0:00 a 0:15 | Camión visto desde atrás; orbitar con el botón derecho y acercar con la rueda | grúa plegada: pluma 70°, secundario -45° | Título (ver abajo) |
| 0:15 a 0:30 | Telemetría | arrastrar un slider (por ejemplo la pluma) y señalar `Target` y `Real` | Llamada de telemetría (ver abajo) |
| 0:30 a 0:55 | Aproximación al primer cubo | `A` 1.7 s (torre a -76.6°) con la grúa alta; luego `S` 1.3 s (pluma a 32°), `F` 1.0 s (secundario a -81°) y `T` 1.3 s (telescópico a 1.0 m); esperar que el `Real` asiente | "Fase 1: carga" |
| 0:55 a 1:05 | Descenso a los dedos alrededor del cubo | pluma 32° a 31.2° y secundario -81° a -83.2°: es un ajuste fino, hacerlo con los sliders | |
| 1:05 a 1:15 | Agarre | `E` 1 s (cierra: cada dedo llega a su tope de 0.35 m en 0.9 s, pero se frena contra el cubo); esperar 1 s | "Agarre por fricción: 300 N por dedo" |
| 1:15 a 1:30 | Elevación y giro hacia la caja | `W` 1.3 s (pluma a unos 55°, el cubo queda a más de 2 m del suelo); `D` 1.7 s (torre de -76.6° a 0°) | |
| 1:30 a 1:45 | Colocación sobre el piso de la caja | `S` 0.6 s (pluma a 38°), `F` 0.7 s (secundario a -105°), `G` 1.3 s (telescópico a 0); ajustar con sliders hasta quedar a 1.2 m sobre el piso | |
| 1:45 a 1:50 | Soltar | `Q` 0.9 s (abre); el cubo cae unos 0.3 m al piso | |
| 1:50 a 2:05 | Repliegue de la grúa | `W` 1.1 s (pluma a 70°) y `R` 1.7 s (secundario a -45°); telescópico en 0 y torre en 0 | "Fase 2: descarga" |
| 2:05 a 2:30 | Volcado | `↑` o `X` 2.75 s (caja a 55°) y soltar; esperar unos 4 s a que el cubo resbale (a unos 27°), abra la compuerta (hasta 70° a 80°) y caiga al corralón | |
| 2:30 a 2:50 | Regreso | `↓` o `Z` 2.75 s (caja a 0°); la compuerta vuelve a cerrar sola (menos de 5°) | "Compuerta pasiva: se abre por contacto y se cierra por gravedad" |
| 2:50 a 3:00 | Cierre | `Retroceso` o `P` para reiniciar la escena | Texto final |

Si falta tiempo para llegar a 3:00, se acorta la espera del volcado; si sobra, repetir la carga con un segundo cubo antes del repliegue (ver las notas).

## Texto en pantalla

- **Intro:** "Camión volcador con hidrogrúa. 10 cuerpos, 3 tipos de joint (Revolute, Prismatic, Spherical), 7 drives y 2 articulaciones pasivas, todo con ArticulationBody en Unity 6."
- **Telemetría:** "Target = lo que se pide. Real = lo que mide la articulación. La diferencia es el hundimiento por peso: por ejemplo 28.7° reales para 30° pedidos en la pluma."
- **Cierre:** "Repositorio y build en el enlace de la descripción."

## Notas honestas sobre el control con teclado

- **El teclado no alcanza para el descenso final.** El paso de `GraspApproach` a `GraspReach` mueve la pluma 0.8° y el secundario 2.2°: una pulsación de un cuadro mueve la pluma unos 0.5° (a 60 cuadros por segundo) y el secundario unos 0.6°, y el agarre necesita que los dedos queden a 0.175 m del suelo, no más abajo (si las puntas rozan el suelo el cabezal se inclina y se traba). Hacer ese ajuste con los sliders, mirando el `Real`.
- **El orden importa.** Girar la torre con la grúa alta y recién después bajar; si se baja primero, el brazo empuja el cubo. Antes de volcar hay que replegar la grúa a `Stowed`: con la grúa tendida sobre la caja el volteo choca contra el brazo.
- **Girar con carga.** Subir la pluma a unos 55° antes de girar la torre hacia la caja, para que el cubo pase sobre las paredes (tope a 1.75 m). Es una estimación por cinemática: comprobarla en el ensayo.
- **Poses calibradas solo para el primer cubo.** `GraspApproach` y `GraspReach` apuntan al cubo en (4.2, 0.4). Para el segundo cubo, en (5.2, 0.9), por geometría hacen falta aproximadamente torre -84.5° y 0.9 m más de telescópico; no está medido, así que hay que ajustar a ojo con los sliders.
- **Cabezal en el borde.** En `BedReach` el cabezal está a unos 3° del borde de su cono de swing (±40°); si se ve inclinado, subir un poco la pluma o extender el secundario.
- **Pendiente de probar en el navegador:** teclas sostenidas, reinicio y orbitar con botón derecho (`CALIBRACION.md`, sección 9). Verificar antes de grabar que el teclado responde en la página de itch.io.
- **Cámara.** El botón derecho orbita y la rueda acerca; encuadrar la grúa y la caja al mismo tiempo, y acercarse al cabezal en el agarre.
