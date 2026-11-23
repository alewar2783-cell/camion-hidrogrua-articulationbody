"""Guion del video: bot que juega el simulador como una persona.

Uso (ver README.md):
    python play_video.py --dry-run --seed 7            # solo escribe el log de eventos, NO envia entrada
    python play_video.py --seed 7 --no-record --pause-points   # ensayo en vivo sin grabar
    python play_video.py --seed 7                      # grabacion real

El guion es una lista de Step(accion, fase, parametros). El mismo guion se ejecuta en
--dry-run (reloj virtual, sin dormir ni enviar nada) y en vivo; con la misma --seed las
decisiones aleatorias del "humano" son identicas, asi que el log refleja lo que hara el bot.
Este modulo NO importa winput al cargarse (solo dentro del modo en vivo).
"""
from __future__ import annotations

import argparse
import datetime
import json
import math
import os
import sys
import threading
import time
from dataclasses import dataclass, field
from typing import Any, Dict, List, Optional, Tuple

import humanize
from humanize import Human, HumanConfig, VirtualBackend

HERE = os.path.dirname(os.path.abspath(__file__))
DEFAULT_RECTS = os.path.join(HERE, "ui_rects.json")
OUT_DIR = os.path.join(HERE, "out")
PROJECT_NAME = "ArticulationBody"

# Rangos de los sliders (TruckSpec.Controls), para convertir valor -> fraccion.
AXIS_RANGE = {
    "Torre": (-170.0, 170.0), "Pluma": (-15.0, 75.0), "BrazoSecundario": (-125.0, 15.0),
    "Telescopico": (0.0, 2.5), "Pinza": (0.0, 0.35), "Caja": (0.0, 55.0),
}
AXIS_ORDER = ["Torre", "Pluma", "BrazoSecundario", "Telescopico", "Pinza", "Caja"]


# ============================================================================ rectangulos
def default_rects(w: int = 1920, h: int = 1080) -> Dict[str, Any]:
    """Rectangulos SINTETICOS segun TruckUiFactory (solo para --dry-run sin ui_rects.json)."""
    s = 2 ** (0.5 * math.log2(w / 1920.0) + 0.5 * math.log2(h / 1080.0))
    y = 20 + 16 + 8 + 40 + 8 + 26 + 8  # padding + franja + titulo + seccion (con espaciados)
    sliders = []
    for name in AXIS_ORDER:
        lo, hi = AXIS_RANGE[name]
        track = {"x": 28 * s, "y": (y + 6 + 32 + 2) * s, "width": 584 * s, "height": 28 * s}
        v01 = 0.7
        hx = track["x"] + 10 * s + v01 * (track["width"] - 20 * s) - 10 * s
        sliders.append({"name": name, "track": track,
                        "handle": {"x": hx, "y": track["y"], "width": 20 * s, "height": 28 * s},
                        "min": lo, "max": hi, "value": lo + v01 * (hi - lo), "value01": v01})
        y += 74 + 8
    return {"version": 1, "synthetic": True, "screen": {"width": w, "height": h},
            "panel": {"x": 0, "y": 0, "width": 640 * s, "height": h},
            "reset_button": {"x": 20 * s, "y": y * s, "width": 600 * s, "height": 56 * s},
            "sliders": sliders}


def load_rects(path: Optional[str], w: int, h: int) -> Dict[str, Any]:
    if path and os.path.exists(path):
        with open(path, "r", encoding="utf-8") as f:
            data = json.load(f)
        data["synthetic"] = False
        return data
    return default_rects(w, h)


def _rt(d: Dict[str, float]) -> humanize.Rect:
    return (d["x"], d["y"], d["width"], d["height"])


class Layout:
    """Convierte los rectangulos de la Game view a coordenadas de escritorio."""

    def __init__(self, rects: Dict[str, Any], region: Tuple[int, int, int, int]):
        self.rects = rects
        self.region = region
        rx, ry, rw, rh = region
        self.sx = rw / float(rects["screen"]["width"])
        self.sy = rh / float(rects["screen"]["height"])
        self.synthetic = bool(rects.get("synthetic"))

    def _desk(self, r: humanize.Rect) -> humanize.Rect:
        rx, ry = self.region[0], self.region[1]
        return (rx + r[0] * self.sx, ry + r[1] * self.sy, r[2] * self.sx, r[3] * self.sy)

    def _slider(self, name: str) -> Dict[str, Any]:
        for s in self.rects["sliders"]:
            if s["name"].lower() == name.lower():
                return s
        raise KeyError("slider %r no esta en ui_rects.json" % name)

    def track(self, name: str) -> humanize.Rect:
        return self._desk(_rt(self._slider(name)["track"]))

    def label(self, name: str) -> humanize.Rect:
        x, y, w, h = self.track(name)
        return (x, y - 38 * self.sy, w, 32 * self.sy)

    def slider_range(self, name: str) -> Tuple[float, float]:
        s = self._slider(name)
        return (s.get("min", AXIS_RANGE[s["name"]][0]), s.get("max", AXIS_RANGE[s["name"]][1]))

    def handle_at(self, name: str, value: float) -> Tuple[humanize.Rect, float]:
        """Rect esperado del tirador cuando el slider vale `value`, y su fraccion 0..1."""
        s = self._slider(name)
        lo, hi = self.slider_range(name)
        frac = humanize.clamp((value - lo) / (hi - lo), 0.0, 1.0)
        tx, ty, tw, th = self.track(name)
        hw = _rt(s["handle"])[2] * self.sx
        hx = tx + frac * (tw - hw)
        return (hx, ty, hw, th), frac

    def reset_button(self) -> humanize.Rect:
        return self._desk(_rt(self.rects["reset_button"]))

    def view_point(self, fx: float, fy: float) -> Tuple[int, int]:
        """Punto en la vista 3D (a la derecha del panel); fx, fy en 0..1 de esa zona."""
        rx, ry, rw, rh = self.region
        px, py, pw, ph = self._desk(_rt(self.rects["panel"]))
        left = px + pw + 20
        return int(left + fx * (rx + rw - left)), int(ry + fy * rh)


# ============================================================================ guion
@dataclass
class Step:
    action: str
    phase: str
    params: Dict[str, Any] = field(default_factory=dict)
    gap: bool = True  # pausa de reaccion (150-700 ms) antes del paso


def build_script() -> List[Step]:
    """El guion completo, por fases. Retocar tiempos aqui tras el primer ensayo."""
    S: List[Step] = []

    def add(action: str, phase: str, gap: bool = True, **params) -> None:
        S.append(Step(action, phase, params, gap))

    def hold(phase: str, key: str, secs: float, settle: float, jitter: float = 0.04) -> None:
        add("key_hold", phase, key=key, seconds=secs, jitter=jitter)
        add("wait", phase, gap=False, seconds=settle + 0.6)  # margen: que el Real asiente

    # -- 0. Intro: el cursor entra, orbita y zoom para encuadrar el camion
    p = "intro"
    add("focus", p, gap=False)
    add("wait", p, gap=False, seconds=1.2)
    add("move_view", p, fx=0.70, fy=0.65)
    add("idle", p, seconds=0.5)
    add("right_drag", p, dx=-250, dy=-30)
    add("right_drag", p, dx=130, dy=15)
    add("wheel", p, notches=3)
    add("right_drag", p, dx=-90, dy=-20)
    add("pause_point", p, gap=False, label="fin intro")

    # -- 1. Telemetria: arrastrar la pluma un poco y volver, mirar Target/Real
    p = "telemetria"
    add("hover_label", p, name="Pluma", dwell=0.6)
    add("drag_slider", p, name="Pluma", from_value=70.0, to_value=55.0, duration=1.5)
    add("hover_label", p, name="Pluma", dwell=1.0)
    add("drag_slider", p, name="Pluma", from_value=55.0, to_value=70.0, duration=1.3)
    add("hover_label", p, name="Telescopico", dwell=0.4)
    add("hover_label", p, name="Torre", dwell=0.4)
    add("pause_point", p, gap=False, label="fin telemetria")

    # -- 2. Aproximacion al primer cubo, por arriba (orden corregido de GUION_VIDEO.md)
    p = "aproximacion"
    add("move_view", p, fx=0.55, fy=0.85)
    hold(p, "a", 1.7, 0.6)       # torre a -76.6 grados
    hold(p, "t", 1.3, 0.6)       # telescopico a 1.0 m
    hold(p, "f", 1.0, 1.0)       # secundario, con la pluma todavia alta
    hold(p, "s", 0.93, 0.8, jitter=0.02)  # pluma de 70 a ~42 grados
    add("hover_label", p, name="Pluma", dwell=1.2)   # esperar que el Real asiente
    add("move_view", p, fx=0.50, fy=0.75)
    add("wheel", p, notches=2)   # acercarse al cabezal
    add("right_drag", p, dx=60, dy=10)
    add("wait", p, gap=False, seconds=0.5)
    add("pause_point", p, gap=False, label="antes del descenso fino")

    # -- 3. Descenso fino con el slider de la pluma: 42 -> 31.2 grados
    p = "descenso"
    add("drag_slider", p, name="Pluma", from_value=42.0, to_value=31.2, duration=2.2)
    add("hover_label", p, name="Pluma", dwell=1.2)
    add("wait", p, gap=False, seconds=1.2)
    add("pause_point", p, gap=False, label="antes del agarre")

    # -- 4. Agarre
    p = "agarre"
    add("move_view", p, fx=0.55, fy=0.80)
    hold(p, "e", 1.0, 1.4, jitter=0.05)
    add("pause_point", p, gap=False, label="antes de elevar")

    # -- 5. Elevacion y giro hacia la caja
    p = "elevacion_giro"
    hold(p, "w", 0.8, 0.8, jitter=0.05)
    add("wheel", p, notches=-2)  # alejarse para ver grua y caja
    hold(p, "d", 1.0, 0.2, jitter=0.03)   # torre de vuelta, despacio, en dos tandas
    hold(p, "d", 0.7, 1.4, jitter=0.03)
    add("pause_point", p, gap=False, label="antes de colocar")

    # -- 6. Colocacion sobre el piso de la caja y soltar
    p = "colocacion"
    hold(p, "g", 1.3, 0.8)       # telescopico a 0
    hold(p, "s", 0.6, 0.6)       # pluma de 55 a 38
    hold(p, "f", 0.6, 1.2)       # secundario a -105
    add("move_view", p, fx=0.55, fy=0.80)
    hold(p, "q", 0.9, 1.4, jitter=0.05)  # abrir pinza: el cubo cae
    add("pause_point", p, gap=False, label="antes del repliegue")

    # -- 7. Repliegue de la grua a Stowed
    p = "repliegue"
    hold(p, "w", 1.1, 0.4)
    hold(p, "r", 1.7, 1.2)
    add("wheel", p, notches=-1)
    add("right_drag", p, dx=-110, dy=-15)
    add("pause_point", p, gap=False, label="antes del volcado")

    # -- 8. Volcado, espera, regreso
    p = "volcado"
    hold(p, "up", 2.75, 3.4, jitter=0.03)   # caja a 55 grados; esperar que resbale el cubo
    add("hover_label", p, name="Caja", dwell=0.8)
    hold(p, "down", 2.75, 1.0, jitter=0.03)
    add("pause_point", p, gap=False, label="antes de reiniciar")

    # -- 9. Cierre: clic humano en "Reiniciar Escena" (nunca al centro)
    p = "cierre"
    add("click_reset", p)
    add("wait", p, gap=False, seconds=1.2)
    return S


# ============================================================================ ejecutor
class Executor:
    def __init__(self, human: Human, layout: Layout, backend, live: bool = False,
                 pause_points: bool = False, log=print):
        self.h = human
        self.layout = layout
        self.b = backend
        self.live = live
        self.pause_points = pause_points
        self.log = log
        self.events: List[Dict[str, Any]] = []
        self._t0 = backend.now()

    def _run(self, step: Step) -> Dict[str, Any]:
        h, L, a, p = self.h, self.layout, step.action, step.params
        if a == "focus":
            if self.live:
                import winput
                hwnd = winput.find_unity_window(PROJECT_NAME)
                if hwnd:
                    winput.bring_to_foreground(hwnd)
                    return {"hwnd": int(hwnd)}
                return {"warning": "ventana de Unity no encontrada"}
            return {"dry": True}
        if a == "wait":
            return h.wait(p["seconds"])
        if a == "idle":
            return h.idle(p["seconds"])
        if a == "move_view":
            x, y = L.view_point(p["fx"], p["fy"])
            return h.move_to(x, y, target_w=120)
        if a == "hover_label":
            return h.hover(L.label(p["name"]), dwell=p.get("dwell", 1.5))
        if a == "drag_slider":
            hrect, ffrom = L.handle_at(p["name"], p["from_value"])
            _, fto = L.handle_at(p["name"], p["to_value"])
            return h.drag(hrect, L.track(p["name"]), fto, ffrom, duration=p.get("duration"))
        if a == "right_drag":
            return h.right_drag(p["dx"] * L.sx, p["dy"] * L.sy)
        if a == "wheel":
            return h.wheel(p["notches"])
        if a == "key_hold":
            return h.key_hold(p["key"], p["seconds"], jitter=p.get("jitter"))
        if a == "key_tap":
            return h.key_tap(p["key"])
        if a == "click_reset":
            return h.click(L.reset_button())
        if a == "pause_point":
            if self.live and self.pause_points:
                input("[pause-point] %s - Enter para continuar... " % p.get("label", ""))
            return {"label": p.get("label", "")}
        raise ValueError("accion desconocida: %s" % a)

    def run(self, script: List[Step]) -> List[Dict[str, Any]]:
        for step in script:
            if step.gap and step.action != "pause_point":
                tr = self.b.now() - self._t0
                self._record(Step("react", step.phase), self.h.react(), tr)
            t = self.b.now() - self._t0
            res = self._run(step)
            self._record(step, res, t)
            if self.live:
                self.log("%7.2fs  %-12s %s" % (t, step.phase, step.action))
        return self.events

    def _record(self, step: Step, result: Dict[str, Any], t: Optional[float] = None) -> None:
        now = self.b.now() - self._t0
        if t is None:  # evento "react" ya consumido: empezo `seconds` antes
            t = now - result.get("seconds", 0.0)
        self.events.append({"index": len(self.events), "t": round(t, 3),
                            "duration": round(now - t, 3), "action": step.action,
                            "phase": step.phase, "params": step.params, "result": result})


def summarize(events: List[Dict[str, Any]], seed: int, speed: float, dry: bool,
              counters: Optional[Dict[str, int]] = None) -> Dict[str, Any]:
    total = events[-1]["t"] + events[-1]["duration"] if events else 0.0
    return {"version": 1, "seed": seed, "speed": speed, "dry_run": dry,
            "total_duration_s": round(total, 3), "total_duration": "%d:%02d" % divmod(int(round(total)), 60),
            "event_count": len(events), "low_level_counters": counters or {}, "events": events}


# ============================================================================ modos
def run_dry(rects_path: Optional[str], seed: int, speed: float, region=(0, 0, 1920, 1080),
            log=print) -> Dict[str, Any]:
    rects = load_rects(rects_path, region[2], region[3])
    layout = Layout(rects, region)
    backend = VirtualBackend(start_pos=(region[0] + region[2] - 200, region[1] + region[3] - 150))
    cfg = HumanConfig(speed=speed, bounds=_bounds(region))
    human = Human(backend, seed=seed, cfg=cfg)
    ex = Executor(human, layout, backend, live=False, log=log)
    events = ex.run(build_script())
    result = summarize(events, seed, speed, True, backend.counters)
    result["synthetic_rects"] = layout.synthetic
    return result


def _bounds(region) -> Tuple[float, float, float, float]:
    x, y, w, h = region
    return (x + 6, y + 6, x + w - 7, y + h - 7)  # nunca cerca de la esquina (0,0)


def run_live(args) -> int:
    import winput
    from recorder import Recorder, even_region, parse_region

    winput.set_dpi_awareness()
    hwnd = winput.find_unity_window(PROJECT_NAME)
    if args.region:
        region = parse_region(args.region)
    elif hwnd:
        region = winput.get_client_rect_screen(hwnd)
    else:
        print("No se encontro la ventana del Editor ni se dio --region.")
        return 2
    region = even_region(region)
    print("Region de captura/bot: x=%d y=%d %dx%d" % region)

    rects = load_rects(args.rects, region[2], region[3])
    if rects.get("synthetic") and not args.allow_synthetic_rects:
        print("No hay ui_rects.json valido (%s). Use el menu Tools/Truck/Export UI Rects "
              "o --allow-synthetic-rects." % args.rects)
        return 2
    layout = Layout(rects, region)

    abort = threading.Event()
    backend = winput.RealBackend(abort)
    recorder = None if args.no_record else Recorder(region, args.out)
    state = {"recorder": recorder}

    def on_abort(reason: str) -> None:
        print("\n*** FAILSAFE: %s -> abortando ***" % reason)
        backend.release_all()
        rec = state["recorder"]
        if rec is not None:
            rec.stop()

    failsafe = winput.Failsafe(abort, on_abort)
    winput.begin_high_res_timer(1)
    code = 0
    try:
        failsafe.start()
        cfg = HumanConfig(speed=args.speed, bounds=_bounds(region))
        human = Human(backend, seed=args.seed, cfg=cfg)
        ex = Executor(human, layout, backend, live=True, pause_points=args.pause_points)
        print("Empieza en %d s. Mueva el raton a la esquina (0,0) o mantenga Esc 1 s para abortar."
              % args.countdown)
        backend.sleep(args.countdown)
        if hwnd:
            winput.bring_to_foreground(hwnd)
            backend.sleep(0.6)
        if recorder is not None:
            recorder.start()
            backend.sleep(1.0)  # ffmpeg necesita un instante para arrancar
        events = ex.run(build_script())
        result = summarize(events, args.seed, args.speed, False)
        _write_log(result, args.log or os.path.join(OUT_DIR, "events_live_%d.json" % args.seed))
    except winput.AbortError:
        code = 3
    except KeyboardInterrupt:
        print("Interrumpido.")
        code = 3
    finally:
        failsafe.stop()
        backend.release_all()
        if state["recorder"] is not None:
            state["recorder"].stop()
        winput.end_high_res_timer(1)
    return code


def _write_log(result: Dict[str, Any], path: str) -> None:
    os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
    with open(path, "w", encoding="utf-8") as f:
        json.dump(result, f, indent=1, ensure_ascii=False)


def parse_args(argv=None) -> argparse.Namespace:
    ap = argparse.ArgumentParser(description="Bot de video del simulador del camion volcador.")
    ap.add_argument("--dry-run", action="store_true",
                    help="no envia entrada ni graba: escribe el log de eventos")
    ap.add_argument("--seed", type=int, default=1)
    ap.add_argument("--no-record", action="store_true", help="en vivo, sin ffmpeg")
    ap.add_argument("--rects", default=DEFAULT_RECTS, help="ruta de ui_rects.json")
    ap.add_argument("--out", default=None, help="mp4 de salida (por defecto tools/video/out/)")
    ap.add_argument("--speed", type=float, default=1.0,
                    help="escala movimientos y pausas del cursor (no los tiempos de las teclas)")
    ap.add_argument("--pause-points", action="store_true",
                    help="en vivo, espera Enter al final de cada fase (ensayos)")
    ap.add_argument("--region", default=None, help="X,Y,WxH del area a grabar (por defecto, "
                                                   "el area cliente de la ventana del Editor)")
    ap.add_argument("--log", default=None, help="donde escribir el log JSON de eventos")
    ap.add_argument("--countdown", type=float, default=5.0, help="segundos antes de empezar")
    ap.add_argument("--allow-synthetic-rects", action="store_true",
                    help="en vivo, permite rectangulos aproximados si falta ui_rects.json")
    return ap.parse_args(argv)


def main(argv=None) -> int:
    args = parse_args(argv)
    if args.dry_run:
        result = run_dry(args.rects, args.seed, args.speed)
        path = args.log or os.path.join(OUT_DIR, "events_dry_%d.json" % args.seed)
        _write_log(result, path)
        print("DRY-RUN seed=%d: %s (%.1f s), %d eventos, rects %s -> %s" % (
            args.seed, result["total_duration"], result["total_duration_s"],
            result["event_count"], "sinteticos" if result["synthetic_rects"] else "reales", path))
        return 0
    return run_live(args)


if __name__ == "__main__":
    sys.exit(main())
