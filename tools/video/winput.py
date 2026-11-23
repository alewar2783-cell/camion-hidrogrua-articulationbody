"""Envoltorio fino de ctypes sobre SendInput (Windows) para el bot de video.

Reglas de seguridad de este modulo:
  * Importarlo NO envia ninguna entrada: solo define estructuras y funciones.
  * Solo se usa el cursor por defecto del sistema (nunca SetCursor/SetSystemCursor).
  * Las coordenadas absolutas se normalizan al escritorio virtual
    (MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK).

Solo libreria estandar.
"""
from __future__ import annotations

import ctypes
import sys
import threading
import time
from ctypes import wintypes
from typing import Callable, Dict, Optional, Tuple

IS_WINDOWS = sys.platform == "win32"

# ---------------------------------------------------------------- constantes
INPUT_MOUSE = 0
INPUT_KEYBOARD = 1

MOUSEEVENTF_MOVE = 0x0001
MOUSEEVENTF_LEFTDOWN = 0x0002
MOUSEEVENTF_LEFTUP = 0x0004
MOUSEEVENTF_RIGHTDOWN = 0x0008
MOUSEEVENTF_RIGHTUP = 0x0010
MOUSEEVENTF_WHEEL = 0x0800
MOUSEEVENTF_VIRTUALDESK = 0x4000
MOUSEEVENTF_ABSOLUTE = 0x8000

KEYEVENTF_EXTENDEDKEY = 0x0001
KEYEVENTF_KEYUP = 0x0002
KEYEVENTF_SCANCODE = 0x0008

WHEEL_DELTA = 120

SM_XVIRTUALSCREEN = 76
SM_YVIRTUALSCREEN = 77
SM_CXVIRTUALSCREEN = 78
SM_CYVIRTUALSCREEN = 79

VK_ESCAPE = 0x1B

# Scan codes (conjunto 1). Las flechas son teclas extendidas.
# nombre -> (scancode, extendida)
SCANCODES: Dict[str, Tuple[int, bool]] = {
    "a": (0x1E, False), "d": (0x20, False), "s": (0x1F, False), "w": (0x11, False),
    "f": (0x21, False), "r": (0x13, False), "g": (0x22, False), "t": (0x14, False),
    "q": (0x10, False), "e": (0x12, False), "z": (0x2C, False), "x": (0x2D, False),
    "p": (0x19, False),
    "up": (0x48, True), "down": (0x50, True), "left": (0x4B, True), "right": (0x4D, True),
    "backspace": (0x0E, False), "shift": (0x2A, False), "space": (0x39, False),
}


def normalize_key(name: str) -> str:
    n = name.strip().lower()
    aliases = {"arriba": "up", "abajo": "down", "uparrow": "up", "downarrow": "down",
               "retroceso": "backspace", "lshift": "shift"}
    n = aliases.get(n, n)
    if n not in SCANCODES:
        raise KeyError("tecla no soportada: %r (soportadas: %s)" % (name, sorted(SCANCODES)))
    return n


# ------------------------------------------------------------------ estructuras
ULONG_PTR = ctypes.c_size_t


class MOUSEINPUT(ctypes.Structure):
    _fields_ = [("dx", wintypes.LONG), ("dy", wintypes.LONG), ("mouseData", wintypes.DWORD),
                ("dwFlags", wintypes.DWORD), ("time", wintypes.DWORD),
                ("dwExtraInfo", ULONG_PTR)]


class KEYBDINPUT(ctypes.Structure):
    _fields_ = [("wVk", wintypes.WORD), ("wScan", wintypes.WORD), ("dwFlags", wintypes.DWORD),
                ("time", wintypes.DWORD), ("dwExtraInfo", ULONG_PTR)]


class HARDWAREINPUT(ctypes.Structure):
    _fields_ = [("uMsg", wintypes.DWORD), ("wParamL", wintypes.WORD),
                ("wParamH", wintypes.WORD)]


class _INPUTUNION(ctypes.Union):
    _fields_ = [("mi", MOUSEINPUT), ("ki", KEYBDINPUT), ("hi", HARDWAREINPUT)]


class INPUT(ctypes.Structure):
    _anonymous_ = ("u",)
    _fields_ = [("type", wintypes.DWORD), ("u", _INPUTUNION)]


class POINT(ctypes.Structure):
    _fields_ = [("x", wintypes.LONG), ("y", wintypes.LONG)]


user32 = ctypes.WinDLL("user32", use_last_error=True) if IS_WINDOWS else None
kernel32 = ctypes.WinDLL("kernel32", use_last_error=True) if IS_WINDOWS else None

if IS_WINDOWS:
    user32.SendInput.argtypes = (wintypes.UINT, ctypes.POINTER(INPUT), ctypes.c_int)
    user32.SendInput.restype = wintypes.UINT
    user32.GetSystemMetrics.argtypes = (ctypes.c_int,)
    user32.GetSystemMetrics.restype = ctypes.c_int
    user32.GetCursorPos.argtypes = (ctypes.POINTER(POINT),)
    user32.GetCursorPos.restype = wintypes.BOOL
    user32.GetAsyncKeyState.argtypes = (ctypes.c_int,)
    user32.GetAsyncKeyState.restype = ctypes.c_short
    user32.EnumWindows.argtypes = (ctypes.c_void_p, wintypes.LPARAM)
    user32.EnumWindows.restype = wintypes.BOOL
    user32.GetWindowTextLengthW.argtypes = (wintypes.HWND,)
    user32.GetWindowTextW.argtypes = (wintypes.HWND, wintypes.LPWSTR, ctypes.c_int)
    user32.IsWindowVisible.argtypes = (wintypes.HWND,)
    user32.IsIconic.argtypes = (wintypes.HWND,)
    user32.ShowWindow.argtypes = (wintypes.HWND, ctypes.c_int)
    user32.SetForegroundWindow.argtypes = (wintypes.HWND,)
    user32.GetForegroundWindow.restype = wintypes.HWND
    user32.GetWindowThreadProcessId.argtypes = (wintypes.HWND, ctypes.POINTER(wintypes.DWORD))
    user32.GetWindowThreadProcessId.restype = wintypes.DWORD
    user32.AttachThreadInput.argtypes = (wintypes.DWORD, wintypes.DWORD, wintypes.BOOL)
    user32.GetClientRect.argtypes = (wintypes.HWND, ctypes.POINTER(wintypes.RECT))
    user32.ClientToScreen.argtypes = (wintypes.HWND, ctypes.POINTER(POINT))
    user32.BringWindowToTop.argtypes = (wintypes.HWND,)
    kernel32.GetCurrentThreadId.restype = wintypes.DWORD


# --------------------------------------------------------------------- DPI / timer
def set_dpi_awareness() -> bool:
    """DPI per-monitor v2 para que las coordenadas sean pixeles reales."""
    if not IS_WINDOWS:
        return False
    try:
        fn = user32.SetProcessDpiAwarenessContext
        fn.argtypes = (ctypes.c_void_p,)
        if fn(ctypes.c_void_p(-4)):  # DPI_AWARENESS_CONTEXT_PER_MONITOR_AWARE_V2
            return True
    except (AttributeError, OSError):
        pass
    try:
        ctypes.WinDLL("shcore").SetProcessDpiAwareness(2)
        return True
    except (AttributeError, OSError):
        pass
    try:
        return bool(user32.SetProcessDPIAware())
    except (AttributeError, OSError):
        return False


def begin_high_res_timer(ms: int = 1) -> None:
    if IS_WINDOWS:
        ctypes.WinDLL("winmm").timeBeginPeriod(ms)


def end_high_res_timer(ms: int = 1) -> None:
    if IS_WINDOWS:
        ctypes.WinDLL("winmm").timeEndPeriod(ms)


# ------------------------------------------------------------------ geometria
def virtual_desktop() -> Tuple[int, int, int, int]:
    """(x, y, ancho, alto) del escritorio virtual."""
    return (user32.GetSystemMetrics(SM_XVIRTUALSCREEN), user32.GetSystemMetrics(SM_YVIRTUALSCREEN),
            user32.GetSystemMetrics(SM_CXVIRTUALSCREEN), user32.GetSystemMetrics(SM_CYVIRTUALSCREEN))


def normalize_abs(x: float, y: float, desktop: Tuple[int, int, int, int]) -> Tuple[int, int]:
    """Pixeles de pantalla -> 0..65535 sobre el escritorio virtual (funcion pura)."""
    vx, vy, vw, vh = desktop
    nx = int(round((x - vx) * 65535.0 / max(1, vw - 1)))
    ny = int(round((y - vy) * 65535.0 / max(1, vh - 1)))
    return max(0, min(65535, nx)), max(0, min(65535, ny))


def cursor_pos() -> Tuple[int, int]:
    p = POINT()
    user32.GetCursorPos(ctypes.byref(p))
    return p.x, p.y


def key_is_down(vk: int) -> bool:
    return bool(user32.GetAsyncKeyState(vk) & 0x8000)


# --------------------------------------------------------------------- entrada
def _send(inp: INPUT) -> None:
    n = user32.SendInput(1, ctypes.byref(inp), ctypes.sizeof(INPUT))
    if n != 1:
        raise OSError("SendInput fallo (error %d)" % ctypes.get_last_error())


def _mouse(flags: int, dx: int = 0, dy: int = 0, data: int = 0) -> None:
    inp = INPUT(type=INPUT_MOUSE)
    inp.mi = MOUSEINPUT(dx, dy, data & 0xFFFFFFFF, flags, 0, 0)
    _send(inp)


def mouse_move_abs(x: float, y: float) -> None:
    nx, ny = normalize_abs(x, y, virtual_desktop())
    _mouse(MOUSEEVENTF_MOVE | MOUSEEVENTF_ABSOLUTE | MOUSEEVENTF_VIRTUALDESK, nx, ny)


def mouse_button(button: str, down: bool) -> None:
    flags = {("left", True): MOUSEEVENTF_LEFTDOWN, ("left", False): MOUSEEVENTF_LEFTUP,
             ("right", True): MOUSEEVENTF_RIGHTDOWN, ("right", False): MOUSEEVENTF_RIGHTUP
             }[(button, down)]
    _mouse(flags)


def mouse_wheel(notches: int) -> None:
    """Positivo = rueda hacia delante (alejarse de uno), negativo = hacia atras."""
    _mouse(MOUSEEVENTF_WHEEL, data=int(notches) * WHEEL_DELTA)


def key_event(name: str, down: bool) -> None:
    scan, ext = SCANCODES[normalize_key(name)]
    flags = KEYEVENTF_SCANCODE
    if ext:
        flags |= KEYEVENTF_EXTENDEDKEY
    if not down:
        flags |= KEYEVENTF_KEYUP
    inp = INPUT(type=INPUT_KEYBOARD)
    inp.ki = KEYBDINPUT(0, scan, flags, 0, 0)
    _send(inp)


# --------------------------------------------------------------------- ventanas
def _enum_windows():
    found = []
    proto = ctypes.WINFUNCTYPE(wintypes.BOOL, wintypes.HWND, wintypes.LPARAM)

    def cb(hwnd, _lp):
        if user32.IsWindowVisible(hwnd):
            n = user32.GetWindowTextLengthW(hwnd)
            if n:
                buf = ctypes.create_unicode_buffer(n + 1)
                user32.GetWindowTextW(hwnd, buf, n + 1)
                found.append((hwnd, buf.value))
        return True

    user32.EnumWindows(ctypes.cast(proto(cb), ctypes.c_void_p), 0)
    return found


def find_unity_window(project: str = "ArticulationBody") -> Optional[int]:
    """HWND de la ventana principal del Editor: titulo con 'Unity' y el nombre del proyecto."""
    for hwnd, title in _enum_windows():
        if "Unity" in title and project in title:
            return hwnd
    return None


def bring_to_foreground(hwnd: int) -> bool:
    if user32.IsIconic(hwnd):
        user32.ShowWindow(hwnd, 9)  # SW_RESTORE
    fg = user32.GetForegroundWindow()
    cur = kernel32.GetCurrentThreadId()
    fg_thread = user32.GetWindowThreadProcessId(fg, None) if fg else 0
    attached = False
    if fg_thread and fg_thread != cur:
        attached = bool(user32.AttachThreadInput(cur, fg_thread, True))
    try:
        user32.BringWindowToTop(hwnd)
        ok = bool(user32.SetForegroundWindow(hwnd))
    finally:
        if attached:
            user32.AttachThreadInput(cur, fg_thread, False)
    return ok


def get_client_rect_screen(hwnd: int) -> Tuple[int, int, int, int]:
    """(x, y, ancho, alto) del area cliente en coordenadas de pantalla."""
    r = wintypes.RECT()
    user32.GetClientRect(hwnd, ctypes.byref(r))
    p = POINT(0, 0)
    user32.ClientToScreen(hwnd, ctypes.byref(p))
    return p.x, p.y, r.right - r.left, r.bottom - r.top


# ---------------------------------------------------------------- backend real
class AbortError(RuntimeError):
    """El failsafe pidio abortar."""


class RealBackend:
    """Implementa la interfaz de backend que usa humanize.Human, sobre SendInput."""

    def __init__(self, abort_event: Optional[threading.Event] = None):
        self.abort = abort_event or threading.Event()
        self.held_keys = set()
        self.held_buttons = set()
        self._lock = threading.Lock()

    def now(self) -> float:
        return time.perf_counter()

    def _check(self) -> None:
        if self.abort.is_set():
            raise AbortError("abortado por el failsafe")

    def sleep(self, seconds: float) -> None:
        end = time.perf_counter() + max(0.0, seconds)
        while True:
            self._check()
            rem = end - time.perf_counter()
            if rem <= 0:
                return
            time.sleep(min(rem, 0.02))

    def position(self) -> Tuple[int, int]:
        return cursor_pos()

    def move(self, x: float, y: float) -> None:
        self._check()
        mouse_move_abs(x, y)

    def button(self, name: str, down: bool) -> None:
        if down:
            self._check()
        with self._lock:
            (self.held_buttons.add if down else self.held_buttons.discard)(name)
        mouse_button(name, down)

    def wheel(self, notches: int) -> None:
        self._check()
        mouse_wheel(notches)

    def key(self, name: str, down: bool) -> None:
        if down:
            self._check()
        name = normalize_key(name)
        with self._lock:
            (self.held_keys.add if down else self.held_keys.discard)(name)
        key_event(name, down)

    def release_all(self) -> None:
        """Suelta todas las teclas y botones que sigan pulsados (seguro desde otro hilo)."""
        with self._lock:
            keys, buttons = list(self.held_keys), list(self.held_buttons)
            self.held_keys.clear()
            self.held_buttons.clear()
        for k in keys:
            try:
                key_event(k, False)
            except OSError:
                pass
        for b in buttons:
            try:
                mouse_button(b, False)
            except OSError:
                pass


# --------------------------------------------------------------------- failsafe
class Failsafe:
    """Hilo de vigilancia. Aborta si:
      * Esc se mantiene pulsada `esc_hold` segundos, o
      * el cursor salta a la esquina superior izquierda (0,0) de la pantalla.
    Al disparar: marca `abort_event`, llama a `on_abort` (soltar teclas/botones, parar
    ffmpeg). Las funciones de lectura son inyectables para probarlo sin entrada real.
    """

    def __init__(self, abort_event: threading.Event, on_abort: Callable[[str], None],
                 esc_hold: float = 1.0, poll: float = 0.03, corner_px: int = 2,
                 get_esc: Optional[Callable[[], bool]] = None,
                 get_cursor: Optional[Callable[[], Tuple[int, int]]] = None,
                 clock: Callable[[], float] = time.perf_counter):
        self.abort_event = abort_event
        self.on_abort = on_abort
        self.esc_hold = esc_hold
        self.poll = poll
        self.corner_px = corner_px
        self._get_esc = get_esc or (lambda: key_is_down(VK_ESCAPE))
        self._get_cursor = get_cursor or cursor_pos
        self._clock = clock
        self._stop = threading.Event()
        self._esc_since: Optional[float] = None
        self.reason: Optional[str] = None
        self._thread = threading.Thread(target=self._run, name="failsafe", daemon=True)

    def start(self) -> None:
        self._thread.start()

    def stop(self) -> None:
        self._stop.set()

    def check_once(self) -> Optional[str]:
        """Una pasada de vigilancia; devuelve el motivo si hay que abortar."""
        x, y = self._get_cursor()
        if 0 <= x <= self.corner_px and 0 <= y <= self.corner_px:
            return "cursor en la esquina (0,0)"
        if self._get_esc():
            now = self._clock()
            if self._esc_since is None:
                self._esc_since = now
            elif now - self._esc_since >= self.esc_hold:
                return "Esc mantenida %.1f s" % self.esc_hold
        else:
            self._esc_since = None
        return None

    def _run(self) -> None:
        while not self._stop.is_set():
            reason = self.check_once()
            if reason:
                self.reason = reason
                self.abort_event.set()
                try:
                    self.on_abort(reason)
                except Exception:  # el failsafe nunca debe propagar
                    pass
                return
            self._stop.wait(self.poll)
