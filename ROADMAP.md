# ROADMAP

Simulador de camión volcador con hidrogrúa (Unity 6.3 LTS, `ArticulationBody`, WebGL).
Diseño: `docs/superpowers/specs/2026-11-10-simulador-camion-hidrogrua-design.md`.
Detalle de cada tarea: `docs/superpowers/plans/2026-11-10-simulador-camion-hidrogrua.md`.

## Hitos

| # | Hito | Resultado verificable | Estado |
|---|---|---|---|
| 0 | Base del repo | Estructura de carpetas, assemblies, escena `Main`, plantilla limpia | [ ] |
| 1 | `TruckSpec` | Tabla Maestra como datos, con tests de la consigna (10 cuerpos, 3 tipos, 7 drives, 2 pasivos) | [ ] |
| 2 | `JointAxis` | Eje con target, lectura real y conversión de unidades, con tests | [ ] |
| 3 | Gravedad pura | Build sin drives: nada salta, todo respeta límites, la pinza cuelga vertical | [ ] |
| 4 | Drives y controlador | Los 6 controles llegan a su target; teclado y reinicio funcionan | [ ] |
| 5 | Cargas y tarea | Cubos se agarran, suben y se descargan abriendo la compuerta, de forma repetible | [ ] |
| 6 | UI y cámara | Sliders, telemetría `Target / Real`, botón de reinicio y cámara orbital | [ ] |
| 7 | Build WebGL temprana | Build probada en el navegador; ajustes de física si hace falta | [ ] |
| 8 | Entrega | README, diagrama y explicación, checklist, guion del video, repo limpio | [ ] |

## Fuera del repositorio (lo hace el usuario)

- [ ] Crear el repositorio público en GitHub y hacer `git push`.
- [ ] Subir el `.zip` de la build WebGL a itch.io y probarla en ventana privada.
- [ ] Grabar el video siguiendo `docs/GUION_VIDEO.md` y subirlo a YouTube (público u oculto).
- [ ] Entregar los cuatro enlaces/documentos de la consigna (ver `docs/ENTREGA.md`).

## Definición de terminado (por tarea)

1. Tests EditMode y PlayMode en verde.
2. Consola de Unity sin errores ni advertencias nuevas.
3. Commit local con mensaje según `CODESTYLE.md`.
4. Este archivo actualizado.
