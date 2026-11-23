"""Grabacion de pantalla con ffmpeg gdigrab (cursor real de Windows dibujado).

Importar este modulo no inicia ninguna grabacion; solo Recorder.start() lanza ffmpeg.
"""
from __future__ import annotations

import datetime
import os
import re
import subprocess
from typing import List, Optional, Tuple

HERE = os.path.dirname(os.path.abspath(__file__))
OUT_DIR = os.path.join(HERE, "out")

Region = Tuple[int, int, int, int]  # x, y, ancho, alto


def parse_region(text: str) -> Region:
    """Acepta 'X,Y,WxH' o 'X,Y,W,H'."""
    nums = [int(n) for n in re.findall(r"-?\d+", text)]
    if len(nums) != 4:
        raise ValueError("region invalida %r; use X,Y,WxH" % text)
    return nums[0], nums[1], nums[2], nums[3]


def even_region(r: Region) -> Region:
    """libx264 + yuv420p exige ancho y alto pares."""
    x, y, w, h = r
    return x, y, w - (w % 2), h - (h % 2)


def default_output() -> str:
    os.makedirs(OUT_DIR, exist_ok=True)
    return os.path.join(OUT_DIR, "play_%s.mp4" % datetime.datetime.now().strftime("%Y%m%d_%H%M%S"))


def build_command(region: Region, out_path: str, fps: int = 30, ffmpeg: str = "ffmpeg") -> List[str]:
    x, y, w, h = even_region(region)
    return [ffmpeg, "-y", "-f", "gdigrab", "-framerate", str(fps), "-draw_mouse", "1",
            "-offset_x", str(x), "-offset_y", str(y), "-video_size", "%dx%d" % (w, h),
            "-i", "desktop", "-c:v", "libx264", "-preset", "veryfast", "-crf", "18",
            "-pix_fmt", "yuv420p", out_path]


class Recorder:
    def __init__(self, region: Region, out_path: Optional[str] = None, fps: int = 30,
                 ffmpeg: str = "ffmpeg", log=print):
        self.region = even_region(region)
        self.out_path = out_path or default_output()
        self.fps = fps
        self.ffmpeg = ffmpeg
        self.log = log
        self.proc: Optional[subprocess.Popen] = None
        self._log_file = None

    @property
    def command(self) -> List[str]:
        return build_command(self.region, self.out_path, self.fps, self.ffmpeg)

    def start(self) -> None:
        os.makedirs(os.path.dirname(os.path.abspath(self.out_path)), exist_ok=True)
        cmd = self.command
        self.log("ffmpeg: " + subprocess.list2cmdline(cmd))
        self._log_file = open(self.out_path + ".ffmpeg.log", "wb")
        self.proc = subprocess.Popen(cmd, stdin=subprocess.PIPE, stdout=self._log_file,
                                     stderr=subprocess.STDOUT)

    def stop(self, timeout: float = 20.0) -> None:
        """Cierre ordenado: 'q' por stdin para que ffmpeg finalice el mp4."""
        p = self.proc
        if p is None:
            return
        self.proc = None
        try:
            if p.poll() is None:
                try:
                    p.stdin.write(b"q")
                    p.stdin.flush()
                    p.stdin.close()
                except (OSError, ValueError):
                    pass
                try:
                    p.wait(timeout=timeout)
                except subprocess.TimeoutExpired:
                    p.terminate()
                    try:
                        p.wait(timeout=5)
                    except subprocess.TimeoutExpired:
                        p.kill()
        finally:
            if self._log_file:
                self._log_file.close()
                self._log_file = None
        self.log("ffmpeg terminado (codigo %s): %s" % (p.returncode, self.out_path))
