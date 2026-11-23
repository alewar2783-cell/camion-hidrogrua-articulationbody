"""Motor de cursor humano para el bot de video.

Este modulo es PURO: no importa winput ni envia entrada. Habla con un "backend" con esta
interfaz (RealBackend en winput.py, VirtualBackend aqui para --dry-run y pruebas):

    now() -> float, sleep(s), position() -> (x, y), move(x, y),
    button(name, down), wheel(notches), key(name, down), release_all()

Todo el azar sale de un random.Random con semilla (reproducible con --seed).
"""
from __future__ import annotations

import math
import random
from dataclasses import dataclass
from typing import Callable, Dict, List, Optional, Tuple

Point = Tuple[float, float]
Rect = Tuple[float, float, float, float]  # x, y, ancho, alto


# ------------------------------------------------------------------ configuracion
@dataclass
class HumanConfig:
    overshoot_prob: float = 0.18      # pasarse del objetivo y corregir
    mistake_prob: float = 0.12        # apuntar a un punto equivocado y re-apuntar
    double_check_prob: float = 0.15   # pasar por encima, dudar y volver a apuntar
    error_min_distance: float = 160.0  # por debajo de esto no hay overshoot/error
    mistake_offset: Tuple[float, float] = (30.0, 80.0)
    overshoot_px: Tuple[float, float] = (8.0, 35.0)
    fitts_a: float = 0.18             # tiempo de movimiento = a + b*log2(D/W + 1)
    fitts_b: float = 0.12
    fitts_scale: Tuple[float, float] = (0.85, 1.25)
    min_move_time: float = 0.16
    max_move_time: float = 1.6
    burst_max: float = 0.30           # mezcla de perfil con rafaga rapida
    curvature: Tuple[float, float] = (0.02, 0.14)  # fraccion de la distancia
    curvature_cap_px: float = 150.0
    tremor_px: float = 0.9
    sample_dt: float = 0.008
    pre_click_pause: Tuple[float, float] = (0.08, 0.30)
    click_hold: Tuple[float, float] = (0.06, 0.14)
    react_range: Tuple[float, float] = (0.15, 0.70)
    key_jitter: float = 0.10
    band: Tuple[float, float] = (0.15, 0.85)   # franja util del rectangulo
    center_exclusion: float = 0.15             # elipse central excluida (30 % del tamano)
    speed: float = 1.0                         # escala movimientos y pausas del cursor
    bounds: Optional[Tuple[float, float, float, float]] = None  # x0, y0, x1, y1


# ------------------------------------------------------------------ utilidades
def clamp(v: float, lo: float, hi: float) -> float:
    return lo if v < lo else hi if v > hi else v


def minjerk(u: float) -> float:
    u = clamp(u, 0.0, 1.0)
    return u * u * u * (10.0 + u * (-15.0 + 6.0 * u))


def rect_center(r: Rect) -> Point:
    return (r[0] + r[2] / 2.0, r[1] + r[3] / 2.0)


def pick_point(rect: Rect, rng: random.Random, band: Tuple[float, float] = (0.15, 0.85),
               exclusion: float = 0.15) -> Tuple[int, int]:
    """Punto dentro de la franja `band` del rectangulo, NUNCA en el disco central.

    El disco (elipse en coordenadas normalizadas) tiene radio `exclusion` del tamano
    (30 % de diametro con 0.15). Devuelve pixeles enteros distintos del pixel central.
    """
    x, y, w, h = rect
    cx, cy = int(x + w / 2.0), int(y + h / 2.0)
    for _ in range(200):
        u = rng.uniform(band[0], band[1])
        v = rng.uniform(band[0], band[1])
        if ((u - 0.5) / exclusion) ** 2 + ((v - 0.5) / exclusion) ** 2 < 1.0:
            continue
        px, py = int(round(x + u * w)), int(round(y + v * h))
        if (px, py) != (cx, cy):
            return px, py
    return int(x + band[0] * w), int(y + band[0] * h)  # rectangulo degenerado


def is_center(rect: Rect, pt: Tuple[float, float]) -> bool:
    cx, cy = rect_center(rect)
    return int(pt[0]) == int(cx) and int(pt[1]) == int(cy)


def _bezier(p0: Point, p1: Point, p2: Point, p3: Point, t: float) -> Point:
    mt = 1.0 - t
    a, b, c, d = mt * mt * mt, 3 * mt * mt * t, 3 * mt * t * t, t * t * t
    return (a * p0[0] + b * p1[0] + c * p2[0] + d * p3[0],
            a * p0[1] + b * p1[1] + c * p2[1] + d * p3[1])


# ------------------------------------------------------------------ segmentos / trayectorias
class Segment:
    """Un tramo: Bezier cubica con perfil de velocidad suave + rafaga y temblor."""

    TABLE = 48

    def __init__(self, p0: Point, p1: Point, p2: Point, p3: Point, duration: float,
                 burst: float, tremor: float, rng: random.Random, kind: str = "direct",
                 pause_after: float = 0.0):
        self.p0, self.p1, self.p2, self.p3 = p0, p1, p2, p3
        self.duration = max(0.0, duration)
        self.burst = burst
        self.tremor = tremor
        self.kind = kind
        self.pause_after = pause_after
        self._ph = [rng.uniform(0, 2 * math.pi) for _ in range(6)]
        self._fr = [rng.uniform(6.0, 9.0), rng.uniform(10.0, 14.0), rng.uniform(15.0, 22.0)]
        # tabla de longitud de arco para avanzar a velocidad geometrica coherente
        pts = [_bezier(p0, p1, p2, p3, i / self.TABLE) for i in range(self.TABLE + 1)]
        acc = [0.0]
        for a, b in zip(pts, pts[1:]):
            acc.append(acc[-1] + math.hypot(b[0] - a[0], b[1] - a[1]))
        self.length = acc[-1]
        self._acc = acc

    def profile(self, u: float) -> float:
        m = minjerk(u)
        return (1.0 - self.burst) * m + self.burst * minjerk(m)

    def _param_at_arc(self, s: float) -> float:
        if self.length <= 1e-9:
            return 1.0
        target = clamp(s, 0.0, 1.0) * self.length
        lo, hi = 0, self.TABLE
        while hi - lo > 1:
            mid = (lo + hi) // 2
            if self._acc[mid] <= target:
                lo = mid
            else:
                hi = mid
        span = self._acc[hi] - self._acc[lo]
        frac = 0.0 if span <= 1e-9 else (target - self._acc[lo]) / span
        return (lo + frac) / self.TABLE

    def pos(self, t: float) -> Point:
        if self.duration <= 1e-9 or t >= self.duration:
            return self.p3
        u = clamp(t / self.duration, 0.0, 1.0)
        x, y = _bezier(self.p0, self.p1, self.p2, self.p3, self._param_at_arc(self.profile(u)))
        env = math.sin(math.pi * u)  # el temblor se anula en los extremos
        if self.tremor > 0 and env > 0:
            tt = t
            nx = sum(math.sin(2 * math.pi * f * tt + self._ph[i]) for i, f in enumerate(self._fr))
            ny = sum(math.sin(2 * math.pi * f * tt + self._ph[i + 3])
                     for i, f in enumerate(self._fr))
            x += self.tremor * env * nx / 3.0
            y += self.tremor * env * ny / 3.0
        return (x, y)


class Path:
    """Secuencia de segmentos (con pausas entre ellos). Termina EXACTAMENTE en `end`."""

    def __init__(self, segments: List[Segment], end: Tuple[int, int]):
        self.segments = segments
        self.end = end

    @property
    def kinds(self) -> List[str]:
        return [s.kind for s in self.segments]

    @property
    def duration(self) -> float:
        return sum(s.duration + s.pause_after for s in self.segments)

    def pos(self, t: float) -> Tuple[int, int]:
        acc = 0.0
        for s in self.segments:
            if t < acc + s.duration:
                x, y = s.pos(t - acc)
                return int(round(x)), int(round(y))
            acc += s.duration
            if t < acc + s.pause_after:
                return int(round(s.p3[0])), int(round(s.p3[1]))
            acc += s.pause_after
        return self.end

    def samples(self, dt: float = 0.008) -> List[Tuple[float, int, int]]:
        out = []
        total = self.duration
        n = int(total / dt)
        for i in range(n + 1):
            t = i * dt
            x, y = self.pos(t)
            out.append((t, x, y))
        out.append((total, self.end[0], self.end[1]))
        return out


def fitts_time(dist: float, width: float, cfg: HumanConfig, rng: random.Random) -> float:
    width = max(8.0, width)
    t = (cfg.fitts_a + cfg.fitts_b * math.log2(dist / width + 1.0)) * rng.uniform(*cfg.fitts_scale)
    return clamp(t, cfg.min_move_time, cfg.max_move_time)


def _make_segment(p0: Point, p3: Point, duration: float, rng: random.Random, cfg: HumanConfig,
                  kind: str, curvature_scale: float = 1.0, pause_after: float = 0.0,
                  burst: Optional[float] = None) -> Segment:
    dx, dy = p3[0] - p0[0], p3[1] - p0[1]
    d = math.hypot(dx, dy)
    if d < 1e-6:
        return Segment(p0, p0, p3, p3, 0.0, 0.0, 0.0, rng, kind, pause_after)
    ux, uy = dx / d, dy / d
    nx, ny = -uy, ux
    sign = rng.choice((-1.0, 1.0))
    mag = min(cfg.curvature_cap_px, rng.uniform(*cfg.curvature) * d) * curvature_scale
    k1 = sign * mag * rng.uniform(0.6, 1.2)
    k2 = sign * mag * rng.uniform(-0.3, 1.0)  # a veces forma de S
    r1, r2 = rng.uniform(0.15, 0.40), rng.uniform(0.60, 0.88)
    c1 = (p0[0] + dx * r1 + nx * k1, p0[1] + dy * r1 + ny * k1)
    c2 = (p0[0] + dx * r2 + nx * k2, p0[1] + dy * r2 + ny * k2)
    b = rng.uniform(0.0, cfg.burst_max) if burst is None else burst
    tremor = cfg.tremor_px * min(1.0, d / 60.0)
    return Segment(p0, c1, c2, p3, duration, b, tremor, rng, kind, pause_after)


def plan_move(start: Point, end: Tuple[int, int], rng: random.Random, cfg: HumanConfig,
              target_w: float = 40.0, allow_mistake: bool = True, allow_overshoot: bool = True,
              duration: Optional[float] = None, curvature_scale: float = 1.0) -> Path:
    """Trayectoria humana de `start` a `end` (extremo exacto)."""
    end = (int(end[0]), int(end[1]))
    dist = math.hypot(end[0] - start[0], end[1] - start[1])
    if dist < 1.0:
        return Path([], end)
    if dist < 6.0:  # micro-ajuste: movimiento corto y casi recto
        seg = _make_segment(start, end, rng.uniform(0.07, 0.14), rng, cfg, "direct",
                            curvature_scale=0.2, burst=0.0)
        return Path([seg], end)

    base = duration if duration is not None else fitts_time(dist, target_w, cfg, rng)
    segs: List[Segment] = []
    roll = rng.random()
    big = dist >= cfg.error_min_distance
    p_mis = cfg.mistake_prob if (allow_mistake and big) else 0.0
    p_over = cfg.overshoot_prob if (allow_overshoot and big) else 0.0

    def _inside(p: Point) -> Point:
        if cfg.bounds is None:
            return p
        x0, y0, x1, y1 = cfg.bounds
        return (clamp(p[0], x0, x1), clamp(p[1], y0, y1))

    if roll < p_mis:
        ang = rng.uniform(0, 2 * math.pi)
        off = rng.uniform(*cfg.mistake_offset)
        wrong = _inside((end[0] + math.cos(ang) * off, end[1] + math.sin(ang) * off))
        segs.append(_make_segment(start, wrong, base * 0.85, rng, cfg, "mistake",
                                  curvature_scale, pause_after=rng.uniform(0.12, 0.32)))
        d2 = math.hypot(end[0] - wrong[0], end[1] - wrong[1])
        segs.append(_make_segment(wrong, end, max(0.2, fitts_time(d2, target_w, cfg, rng)),
                                  rng, cfg, "correction", 0.6))
    elif roll < p_mis + p_over:
        ux, uy = (end[0] - start[0]) / dist, (end[1] - start[1]) / dist
        o = rng.uniform(*cfg.overshoot_px)
        lat = rng.uniform(-0.25, 0.25) * o
        over = _inside((end[0] + ux * o - uy * lat, end[1] + uy * o + ux * lat))
        segs.append(_make_segment(start, over, base * 0.92, rng, cfg, "overshoot",
                                  curvature_scale, pause_after=rng.uniform(0.04, 0.12)))
        segs.append(_make_segment(over, end, rng.uniform(0.12, 0.24) + 0.003 * o, rng, cfg,
                                  "correction", 0.3, burst=0.0))
    else:
        segs.append(_make_segment(start, end, base, rng, cfg, "direct", curvature_scale))
    return Path(segs, end)


# ------------------------------------------------------------------ backend virtual
class VirtualBackend:
    """Backend para --dry-run y pruebas: reloj virtual, no duerme, no envia nada."""

    def __init__(self, start_pos: Tuple[int, int] = (1500, 900)):
        self.t = 0.0
        self._pos = start_pos
        self.held_keys: set = set()
        self.held_buttons: set = set()
        self.counters: Dict[str, int] = {"moves": 0, "buttons": 0, "wheel": 0, "keys": 0}
        self.trace: List[Tuple[float, str, tuple]] = []  # para pruebas (opcional)
        self.record_trace = False

    def now(self) -> float:
        return self.t

    def sleep(self, seconds: float) -> None:
        self.t += max(0.0, seconds)

    def position(self) -> Tuple[int, int]:
        return self._pos

    def _log(self, what: str, *args) -> None:
        if self.record_trace:
            self.trace.append((self.t, what, args))

    def move(self, x: float, y: float) -> None:
        self._pos = (int(x), int(y))
        self.counters["moves"] += 1
        self._log("move", int(x), int(y))

    def button(self, name: str, down: bool) -> None:
        (self.held_buttons.add if down else self.held_buttons.discard)(name)
        self.counters["buttons"] += 1
        self._log("button", name, down)

    def wheel(self, notches: int) -> None:
        self.counters["wheel"] += 1
        self._log("wheel", notches)

    def key(self, name: str, down: bool) -> None:
        (self.held_keys.add if down else self.held_keys.discard)(name)
        self.counters["keys"] += 1
        self._log("key", name, down)

    def release_all(self) -> None:
        self.held_keys.clear()
        self.held_buttons.clear()


# ------------------------------------------------------------------ el humano
class Human:
    """Acciones de alto nivel. Cada una devuelve un dict con lo que realmente hizo."""

    def __init__(self, backend, seed: Optional[int] = None, cfg: Optional[HumanConfig] = None,
                 rng: Optional[random.Random] = None):
        self.b = backend
        self.cfg = cfg or HumanConfig()
        self.rng = rng or random.Random(seed)
        self.pos: Tuple[int, int] = tuple(int(v) for v in backend.position())  # type: ignore

    # -- tiempo
    def _hsleep(self, seconds: float) -> None:
        """Pausa humana del cursor (se escala con --speed)."""
        self.b.sleep(seconds / max(0.05, self.cfg.speed))

    def wait(self, seconds: float) -> Dict:
        """Espera de simulacion (tiempo real de juego; no se escala)."""
        self.b.sleep(seconds)
        return {"seconds": round(seconds, 3)}

    def react(self) -> Dict:
        lo, hi = self.cfg.react_range
        d = lo + (hi - lo) * self.rng.betavariate(1.6, 3.0)
        self._hsleep(d)
        return {"seconds": round(d / max(0.05, self.cfg.speed), 3)}

    # -- cursor
    def _clamp(self, p: Tuple[float, float]) -> Tuple[int, int]:
        x, y = p
        if self.cfg.bounds is not None:
            x0, y0, x1, y1 = self.cfg.bounds
            x, y = clamp(x, x0, x1), clamp(y, y0, y1)
        return int(round(x)), int(round(y))

    def _play(self, path: Path) -> None:
        sp = max(0.05, self.cfg.speed)
        dur = path.duration / sp
        dt = self.cfg.sample_dt
        t0 = self.b.now()
        last = None
        while True:
            el = self.b.now() - t0
            if el >= dur:
                break
            p = self._clamp(path.pos(el * sp))
            if p != last:
                self.b.move(*p)
                last = p
            self.b.sleep(dt)
        end = self._clamp(path.end)
        self.b.move(*end)
        self.pos = end

    def move_to(self, x: float, y: float, target_w: float = 40.0, **kw) -> Dict:
        start = self.pos
        tgt = self._clamp((x, y))
        t0 = self.b.now()
        path = plan_move(start, tgt, self.rng, self.cfg, target_w=target_w, **kw)
        self._play(path)
        return {"from": list(start), "to": list(tgt), "kinds": path.kinds or ["none"],
                "duration": round(self.b.now() - t0, 3)}

    def idle(self, seconds: float, amplitude: Optional[float] = None) -> Dict:
        """Deriva suave del cursor mientras la persona "piensa" / mira."""
        amp = amplitude if amplitude is not None else self.rng.uniform(3.0, 12.0)
        w1, w2 = self.rng.uniform(0.5, 1.3), self.rng.uniform(0.4, 1.1)
        p1, p2 = self.rng.uniform(0, 6.28), self.rng.uniform(0, 6.28)
        x0, y0 = self.pos
        o0 = (amp * math.sin(p1), amp * math.sin(p2))
        t0 = self.b.now()
        step = 0.03
        while self.b.now() - t0 < seconds:
            t = self.b.now() - t0
            ox = amp * math.sin(w1 * t + p1) - o0[0] + self.rng.gauss(0, 0.35)
            oy = amp * math.sin(w2 * t + p2) - o0[1] + self.rng.gauss(0, 0.35)
            p = self._clamp((x0 + ox, y0 + oy))
            self.b.move(*p)
            self.pos = p
            self.b.sleep(step)
        return {"seconds": round(seconds, 3), "amplitude_px": round(amp, 1),
                "end": list(self.pos)}

    def hover(self, rect: Rect, dwell: float = 1.0, target_w: Optional[float] = None) -> Dict:
        pt = pick_point(rect, self.rng, self.cfg.band, self.cfg.center_exclusion)
        info = self.move_to(pt[0], pt[1], target_w or min(rect[2], rect[3]))
        info["dwell"] = self.idle(dwell, amplitude=self.rng.uniform(2.0, 6.0))
        return info

    # -- clicks
    def click(self, rect: Rect, button: str = "left") -> Dict:
        pt = pick_point(rect, self.rng, self.cfg.band, self.cfg.center_exclusion)
        tw = min(rect[2], rect[3])
        t0 = self.b.now()
        info: Dict = {"target": list(pt), "rect": [round(v, 1) for v in rect]}
        if self.rng.random() < self.cfg.double_check_prob:
            ang = self.rng.uniform(0, 2 * math.pi)
            off = self.rng.uniform(8.0, 22.0)
            self.move_to(pt[0] + math.cos(ang) * off, pt[1] + math.sin(ang) * off, tw)
            self._hsleep(self.rng.uniform(0.2, 0.45))
            info["double_check"] = True
        mv = self.move_to(pt[0], pt[1], tw)
        info["kinds"] = mv["kinds"]
        self._hsleep(self.rng.uniform(*self.cfg.pre_click_pause))
        self.b.button(button, True)
        hold = self.rng.uniform(*self.cfg.click_hold)
        self.b.sleep(hold)
        self.b.button(button, False)
        self._hsleep(self.rng.uniform(0.05, 0.15))
        info["hold"] = round(hold, 3)
        info["duration"] = round(self.b.now() - t0, 3)
        return info

    # -- sliders
    @staticmethod
    def slider_px_per_unit(track: Rect, handle: Rect) -> float:
        """Pixeles de recorrido del cursor por unidad de fraccion (0..1) del slider."""
        return max(1.0, track[2] - handle[2])

    def drag(self, handle_rect: Rect, track_rect: Rect, to_frac: float, from_frac: float,
             duration: Optional[float] = None) -> Dict:
        """Agarra el tirador (nunca por el centro) y lo arrastra de from_frac a to_frac."""
        t0 = self.b.now()
        grab = pick_point(handle_rect, self.rng, (0.3, 0.7), 0.15)
        self.move_to(grab[0], grab[1], handle_rect[2])
        self._hsleep(self.rng.uniform(*self.cfg.pre_click_pause))
        self.b.button("left", True)
        self._hsleep(self.rng.uniform(0.04, 0.10))
        dx = (to_frac - from_frac) * self.slider_px_per_unit(track_rect, handle_rect)
        end = (grab[0] + dx, grab[1] + self.rng.uniform(-3.0, 3.0))
        dur = duration if duration is not None else clamp(abs(dx) / self.rng.uniform(110, 220),
                                                          0.6, 4.0)
        stops = 2 if (abs(dx) > 120 and self.rng.random() < 0.35) else 1
        waypoints = [end]
        if stops == 2:
            waypoints = [(grab[0] + dx * self.rng.uniform(0.45, 0.65), end[1]), end]
        for i, wp in enumerate(waypoints):
            path = plan_move(self.pos, self._clamp(wp), self.rng, self.cfg, allow_mistake=False,
                             duration=dur / stops, curvature_scale=0.15)
            self._play(path)
            if i + 1 < stops:
                self._hsleep(self.rng.uniform(0.25, 0.6))
        self._hsleep(self.rng.uniform(0.06, 0.16))
        self.b.button("left", False)
        self._hsleep(self.rng.uniform(0.05, 0.15))
        return {"grab": list(grab), "dx_px": round(dx, 1), "from_frac": round(from_frac, 3),
                "to_frac": round(to_frac, 3), "stops": stops,
                "duration": round(self.b.now() - t0, 3)}

    def right_drag(self, dx: float, dy: float, duration: Optional[float] = None) -> Dict:
        """Orbita la camara: boton derecho pulsado y arrastre relativo desde la posicion actual."""
        t0 = self.b.now()
        start = self.pos
        end = self._clamp((start[0] + dx, start[1] + dy))
        self._hsleep(self.rng.uniform(0.08, 0.2))
        self.b.button("right", True)
        self._hsleep(self.rng.uniform(0.06, 0.15))
        dist = math.hypot(end[0] - start[0], end[1] - start[1])
        dur = duration if duration is not None else clamp(dist / self.rng.uniform(250, 450),
                                                          0.5, 2.5)
        path = plan_move(self.pos, end, self.rng, self.cfg, allow_mistake=False,
                         allow_overshoot=False, duration=dur, curvature_scale=0.5)
        self._play(path)
        self._hsleep(self.rng.uniform(0.06, 0.15))
        self.b.button("right", False)
        self._hsleep(self.rng.uniform(0.05, 0.15))
        return {"from": list(start), "to": list(self.pos), "duration": round(self.b.now() - t0, 3)}

    def wheel(self, notches: int) -> Dict:
        t0 = self.b.now()
        sign = 1 if notches >= 0 else -1
        for i in range(abs(int(notches))):
            self.b.wheel(sign)
            if i + 1 < abs(notches):
                self._hsleep(self.rng.uniform(0.25, 0.5) if self.rng.random() < 0.2
                             else self.rng.uniform(0.05, 0.18))
        self._hsleep(self.rng.uniform(0.05, 0.12))
        return {"notches": int(notches), "duration": round(self.b.now() - t0, 3)}

    # -- teclado
    def key_hold(self, key: str, seconds: float, jitter: Optional[float] = None,
                 drift: bool = True) -> Dict:
        j = self.cfg.key_jitter if jitter is None else jitter
        actual = seconds * (1.0 + self.rng.uniform(-j, j))
        t0 = self.b.now()
        self.b.key(key, True)
        try:
            elapsed = 0.0
            while elapsed < actual - 1e-9:
                sl = min(actual - elapsed, self.rng.uniform(0.15, 0.30))
                self.b.sleep(sl)
                elapsed += sl
                if drift and elapsed < actual - 1e-9 and self.rng.random() < 0.3:
                    p = self._clamp((self.pos[0] + self.rng.choice((-2, -1, 1, 2)),
                                     self.pos[1] + self.rng.choice((-2, -1, 1, 2))))
                    self.b.move(*p)
                    self.pos = p
        finally:
            self.b.key(key, False)
        return {"key": key, "requested": seconds, "actual": round(actual, 3)}

    def key_tap(self, key: str) -> Dict:
        d = self.rng.uniform(0.05, 0.11)
        self.b.key(key, True)
        self.b.sleep(d)
        self.b.key(key, False)
        return {"key": key, "actual": round(d, 3)}
