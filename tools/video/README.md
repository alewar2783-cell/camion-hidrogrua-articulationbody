# Bot de video ("player bot")

Maneja el simulador del camion volcador como lo haria una persona, para grabar el video de entrega: raton de Windows con movimiento humano (cursor por defecto del sistema, sin cursores propios), teclado por scan codes, sin texto superpuesto. Solo biblioteca estandar de Python 3 (ctypes); no hace falta `pip install` y no se usa pyautogui.

## Archivos

| Archivo | Que hace |
|---|---|
| `humanize.py` | Motor de cursor humano (puro, no envia entrada): Bezier cubica aleatoria con curvatura variable, perfil suave con rafaga corta, temblor, overshoot y correccion, "errores" (apuntar 30-80 px desviado y re-aimar), pausa previa al clic (80-300 ms), clics de 60-140 ms, doble comprobacion, deriva en reposo, tiempos tipo Fitts (cerca rapido, lejos mas largo), puntos objetivo en la franja 15-85 % del rectangulo **excluyendo la elipse central** (nunca el centro). `move_to`, `click`, `drag` (slider), `right_drag` (orbitar), `wheel`, `key_hold` (+-10 %), `key_tap`, `idle`, `hover`. Semilla con `--seed`. |
| `winput.py` | Envoltorio ctypes de `SendInput` (raton absoluto normalizado al escritorio virtual, botones, rueda, teclado por scan code: A D S W F R G T Q E Z X P, flechas, Retroceso, Shift, Espacio), DPI per-monitor v2, ventana del Editor (titulo con "Unity" y "ArticulationBody"), traer al frente, rectangulo cliente, `RealBackend` y `Failsafe`. Importarlo no envia nada. |
| `recorder.py` | Lanza/para `ffmpeg` con gdigrab (`-draw_mouse 1`, 30 fps, libx264 veryfast crf 18, yuv420p). Para con `q` por stdin. Escribe en `out/` y deja `*.ffmpeg.log`. Imprime el comando usado. |
| `play_video.py` | El guion (`build_script()`, por fases) y su ejecutor. `--dry-run` simula con reloj virtual y escribe el log JSON; en vivo ejecuta exactamente el mismo guion. |
| `test_humanize.py` | Pruebas unitarias sin entrada real. |
| `UnityHelper/ExportUiRects.cs.txt` | Ayudante del Editor (como `.txt` para que NO compile todavia). |
| `ui_rects.json` | Lo escribe el ayudante (no versionado a mano). |
| `out/` | Videos y logs (ignorada por git). |

## Requisitos

- Windows 10/11, Python 3.10+, `ffmpeg` en el PATH con gdigrab (Gyan.FFmpeg full).
- Editor de Unity en Play Mode con la **Game view maximizada** (Maximize On Play o clic derecho en la pestana > Maximize) y la relacion **Free Aspect** para que la vista llene el area.
- Pantalla con la escala de Windows que sea: el bot activa DPI per-monitor v2 y trabaja en pixeles reales.

## Contrato con el ayudante de Unity

1. Copiar `UnityHelper/ExportUiRects.cs.txt` a `Assets/Scripts/Editor/ExportUiRects.cs`.
2. Con la escena en Play y la Game view maximizada, menu **Tools > Truck > Export UI Rects**. Escribe `tools/video/ui_rects.json`.
3. Formato (pixeles de la Game view, origen arriba a la izquierda, `Canvas` Screen Space Overlay; ver `TruckUiFactory.cs`: `Interface/Canvas/Panel/Row_<ControlId>/Slider`, `Panel/ResetButton`):

```json
{
  "version": 1,
  "screen": { "width": 1920, "height": 1017 },
  "panel":        { "x": 0, "y": 0, "width": 640, "height": 1017 },
  "reset_button": { "x": 20, "y": 618, "width": 600, "height": 56 },
  "sliders": [
    { "name": "Pluma",
      "track":  { "x": 28, "y": 214, "width": 584, "height": 28 },
      "handle": { "x": 510, "y": 214, "width": 20,  "height": 28 },
      "min": -15, "max": 75, "value": 70, "value01": 0.944 }
  ]
}
```

   `name` es el `ControlId` (Torre, Pluma, BrazoSecundario, Telescopico, Pinza, Caja). `track` es el RectTransform del Slider (pista completa). El bot reescala todo por `region / screen`, y calcula la posicion del tirador a partir de los valores esperados (el tirador sigue al `Target`, asi que el del JSON queda desfasado en cuanto se mueve la grua). Sin `ui_rects.json`, `--dry-run` usa rectangulos sinteticos; en vivo se niega salvo `--allow-synthetic-rects`.

## Uso

```
cd tools/video
python -m unittest                       # pruebas (sin entrada real)
python play_video.py --dry-run --seed 7  # escribe out/events_dry_7.json, no toca raton ni teclado
python play_video.py --seed 7 --no-record --pause-points   # ensayo en vivo, sin grabar
python play_video.py --seed 7                              # grabacion real
```

Opciones: `--rects`, `--out mp4`, `--speed` (escala cursor y pausas humanas; NO las teclas ni las esperas de simulacion), `--region X,Y,WxH` (por defecto el area cliente de la ventana del Editor), `--log`, `--countdown N` (5 s por defecto), `--pause-points` (espera Enter al final de cada fase), `--allow-synthetic-rects`.

### Ensayo

1. Play, Game view maximizada, `Export UI Rects`.
2. `--no-record --pause-points`: verificar fase por fase (orden, slider de la pluma 42 a 31.2 grados, que el cubo quede agarrado). Los tiempos de las teclas y las esperas estan en `build_script()` en `play_video.py` (una linea por accion; `hold(fase, tecla, segundos, espera)`).
3. Retocar tiempos, repetir hasta que salga limpio. La misma `--seed` repite las mismas decisiones aleatorias; cambiar la semilla cambia el "caracter" de los movimientos.
4. Grabar sin `--no-record`. El video queda en `out/play_AAAAMMDD_HHMMSS.mp4`.

### Log de eventos

`out/events_dry_<seed>.json` (y `events_live_<seed>.json` tras una corrida en vivo): `{version, seed, speed, dry_run, total_duration_s, total_duration, event_count, low_level_counters, events:[{index, t, duration, action, phase, params, result}]}` con `t` absoluto en segundos desde el inicio del guion.

## Seguridad

- **El bot toma el control del raton y del teclado.** No usar el PC durante la corrida; el Editor debe estar visible y en primer plano.
- **Failsafe** (hilo en segundo plano, cada 30 ms): aborta si **mantienes Esc 1 s** o si el **cursor salta a la esquina (0,0)** de la pantalla (lleva el raton alli). Al abortar suelta todas las teclas y botones pulsados y para ffmpeg de forma ordenada. Ctrl+C en la consola tambien suelta todo. El bot nunca navega a menos de 6 px de los bordes de la region.
- Solo se usa el cursor por defecto del sistema; no se crean ni se reemplazan cursores.
- Esc se usa solo como freno; el guion no lo pulsa (en el Editor sacaria del Play Mode).
