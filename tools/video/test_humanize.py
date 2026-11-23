"""Pruebas unitarias SIN enviar entrada. Ejecutar: python -m unittest (en tools/video)."""
import ast
import json
import math
import os
import random
import sys
import threading
import unittest

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

import humanize as hz  # noqa: E402
import play_video as pv  # noqa: E402
import recorder as rec  # noqa: E402
import winput  # noqa: E402

CFG = hz.HumanConfig()


class TestTargets(unittest.TestCase):
    def test_never_center_and_inside_band(self):
        rng = random.Random(3)
        for rect in [(100, 100, 600, 56), (30, 400, 584, 28), (10, 10, 20, 28), (0, 0, 200, 200)]:
            for _ in range(2000):
                pt = hz.pick_point(rect, rng)
                self.assertFalse(hz.is_center(rect, pt), (rect, pt))
                u, v = (pt[0] - rect[0]) / rect[2], (pt[1] - rect[1]) / rect[3]
                self.assertGreaterEqual(u, 0.14)
                self.assertLessEqual(u, 0.86)
                self.assertGreaterEqual(v, 0.14)
                self.assertLessEqual(v, 0.86)
                # fuera de la elipse central (tolerancia de redondeo a pixel)
                ell = ((u - 0.5) / 0.15) ** 2 + ((v - 0.5) / 0.15) ** 2
                self.assertGreater(ell, 0.7, (rect, pt))


class TestPaths(unittest.TestCase):
    def test_start_and_end_exact(self):
        rng = random.Random(5)
        for _ in range(300):
            a = (rng.randint(0, 1900), rng.randint(0, 1000))
            b = (rng.randint(0, 1900), rng.randint(0, 1000))
            path = hz.plan_move(a, b, rng, CFG)
            s = path.samples()
            if a != b:
                self.assertLessEqual(math.hypot(s[0][1] - a[0], s[0][2] - a[1]), 1.0)
            self.assertEqual((s[-1][1], s[-1][2]), b)
            self.assertEqual(path.end, b)

    def test_error_probabilities(self):
        rng = random.Random(11)
        n, over, mis = 4000, 0, 0
        for _ in range(n):
            path = hz.plan_move((100, 100), (1100, 600), rng, CFG)
            k = path.kinds
            over += "overshoot" in k
            mis += "mistake" in k
        self.assertAlmostEqual(over / n, CFG.overshoot_prob, delta=0.03)
        self.assertAlmostEqual(mis / n, CFG.mistake_prob, delta=0.03)

    def test_no_errors_on_short_moves(self):
        rng = random.Random(2)
        for _ in range(500):
            path = hz.plan_move((500, 500), (580, 540), rng, CFG)
            self.assertEqual(path.kinds, ["direct"])

    def test_mistake_offset_range(self):
        rng = random.Random(9)
        seen = 0
        for _ in range(3000):
            p = hz.plan_move((100, 100), (1100, 600), rng, CFG)
            if "mistake" in p.kinds:
                seen += 1
                wrong = p.segments[0].p3
                d = math.hypot(wrong[0] - 1100, wrong[1] - 600)
                self.assertGreaterEqual(d, 29.9)
                self.assertLessEqual(d, 80.1)
        self.assertGreater(seen, 100)

    def test_peak_velocity_human(self):
        rng = random.Random(21)
        worst = 0.0
        for _ in range(400):
            a = (rng.randint(50, 1850), rng.randint(50, 1000))
            b = (rng.randint(50, 1850), rng.randint(50, 1000))
            path = hz.plan_move(a, b, rng, CFG)
            s = path.samples(0.008)
            for p, q in zip(s, s[1:]):
                dt = q[0] - p[0]
                if dt > 1e-6:
                    worst = max(worst, math.hypot(q[1] - p[1], q[2] - p[2]) / dt)
        self.assertLess(worst, 8000.0, worst)
        self.assertGreater(worst, 500.0)

    def test_duration_scales_with_distance(self):
        def mean_dur(dist):
            rng = random.Random(4)
            return sum(hz.plan_move((100, 500), (100 + dist, 500), rng, CFG, target_w=40).duration
                       for _ in range(300)) / 300.0

        d_near, d_mid, d_far = mean_dur(40), mean_dur(300), mean_dur(1500)
        self.assertLess(d_near, d_mid)
        self.assertLess(d_mid, d_far)
        self.assertGreater(d_near, 0.1)
        self.assertLess(d_far, 2.5)

    def test_speed_scaling(self):
        def run(speed):
            b = hz.VirtualBackend((100, 100))
            h = hz.Human(b, seed=1, cfg=hz.HumanConfig(speed=speed))
            h.move_to(1200, 700)
            return b.now()

        self.assertLess(run(2.0), run(1.0))


class TestHuman(unittest.TestCase):
    def make(self, seed=1, bounds=None):
        b = hz.VirtualBackend((800, 500))
        b.record_trace = True
        return b, hz.Human(b, seed=seed, cfg=hz.HumanConfig(bounds=bounds))

    def test_click_timings_and_not_center(self):
        rect = (600, 300, 500, 56)
        for seed in range(30):
            b, h = self.make(seed)
            info = h.click(rect)
            self.assertFalse(hz.is_center(rect, info["target"]))
            self.assertGreaterEqual(info["hold"], 0.06)
            self.assertLessEqual(info["hold"], 0.14)
            self.assertEqual(b.held_buttons, set())
            downs = [t for t in b.trace if t[1] == "button" and t[2][1]]
            ups = [t for t in b.trace if t[1] == "button" and not t[2][1]]
            self.assertEqual(len(downs), 1)
            self.assertEqual(len(ups), 1)
            self.assertGreaterEqual(ups[0][0] - downs[0][0], 0.059)
            self.assertLessEqual(ups[0][0] - downs[0][0], 0.141)
            # pausa previa al clic (80-300 ms) desde la ultima accion de movimiento
            last_move = max(t[0] for t in b.trace if t[1] == "move" and t[0] <= downs[0][0])
            self.assertGreaterEqual(downs[0][0] - last_move, 0.079)

    def test_key_hold_jitter(self):
        for seed in range(100):
            b, h = self.make(seed)
            info = h.key_hold("a", 1.7)
            self.assertGreaterEqual(info["actual"], 1.7 * 0.9 - 1e-3)
            self.assertLessEqual(info["actual"], 1.7 * 1.1 + 1e-3)
            self.assertEqual(b.held_keys, set())

    def test_reaction_delay_range(self):
        for seed in range(200):
            _, h = self.make(seed)
            d = h.react()["seconds"]
            self.assertGreaterEqual(d, 0.08 - 1e-3)
            self.assertLessEqual(d, 0.38 + 1e-3)

    def test_reproducible_seed(self):
        a = pv.run_dry(None, 5, 1.0)
        b = pv.run_dry(None, 5, 1.0)
        self.assertEqual(json.dumps(a, sort_keys=True), json.dumps(b, sort_keys=True))
        c = pv.run_dry(None, 6, 1.0)
        self.assertNotEqual(a["total_duration_s"], c["total_duration_s"])

    def test_drag_ends_at_expected_pixel_and_releases(self):
        track = (28, 200, 584, 28)
        handle = (28 + 0.7 * 564, 200, 20, 28)
        b, h = self.make(2)
        info = h.drag(handle, track, to_frac=0.4, from_frac=0.7)
        self.assertAlmostEqual(info["dx_px"], -0.3 * 564, delta=0.1)
        self.assertEqual(b.held_buttons, set())
        self.assertAlmostEqual(h.pos[0] - info["grab"][0], info["dx_px"], delta=2.0)

    def test_bounds_respected_and_corner_never_reached(self):
        bounds = (6, 6, 1913, 1073)
        b, h = self.make(3, bounds)
        for x, y in [(0, 0), (5000, 5000), (3, 900), (1000, -50)]:
            h.move_to(x, y)
        for t in b.trace:
            if t[1] == "move":
                self.assertGreaterEqual(t[2][0], 6)
                self.assertGreaterEqual(t[2][1], 6)

    def test_right_drag_and_wheel(self):
        b, h = self.make(4)
        h.right_drag(-200, -30)
        self.assertEqual(b.held_buttons, set())
        h.wheel(3)
        self.assertEqual(b.counters["wheel"], 3)


class TestScenario(unittest.TestCase):
    def test_dry_run_duration_and_schema(self):
        for seed in range(1, 9):
            res = pv.run_dry(None, seed, 1.0)
            self.assertGreaterEqual(res["total_duration_s"], 80.0, seed)
            self.assertLessEqual(res["total_duration_s"], 120.0, seed)
            self.assertEqual(res["event_count"], len(res["events"]))
            json.loads(json.dumps(res))  # serializable
            last_t = -1.0
            for i, e in enumerate(res["events"]):
                for key in ("index", "t", "duration", "action", "phase", "params", "result"):
                    self.assertIn(key, e)
                self.assertEqual(e["index"], i)
                self.assertGreaterEqual(e["t"], last_t - 2e-3)
                self.assertGreaterEqual(e["duration"], 0.0)
                last_t = e["t"]
            ends = res["events"][-1]
            self.assertAlmostEqual(res["total_duration_s"], ends["t"] + ends["duration"], 2)

    def test_script_follows_corrected_order(self):
        keys = [s.params["key"] for s in pv.build_script() if s.action == "key_hold"]
        self.assertEqual(keys[:4], ["a", "t", "f", "s"])
        self.assertEqual(keys[4:9], ["e", "w", "d", "d", "g"])
        self.assertEqual(keys[-2:], ["up", "down"])
        acts = [s.action for s in pv.build_script()]
        self.assertIn("drag_slider", acts)
        self.assertEqual(acts[-2:], ["click_reset", "wait"])

    def test_reset_click_never_center(self):
        rects = pv.default_rects()
        layout = pv.Layout(rects, (0, 0, 1920, 1080))
        res = pv.run_dry(None, 3, 1.0)
        ev = [e for e in res["events"] if e["action"] == "click_reset"][0]
        self.assertFalse(hz.is_center(layout.reset_button(), ev["result"]["target"]))

    def test_layout_scaling_and_handle(self):
        rects = pv.default_rects(1920, 1080)
        layout = pv.Layout(rects, (100, 50, 1920, 1080))
        tx, ty, tw, th = layout.track("Pluma")
        self.assertGreater(tx, 100)
        hrect, frac = layout.handle_at("Pluma", 70.0)
        self.assertAlmostEqual(frac, 85.0 / 90.0, places=6)
        self.assertGreaterEqual(hrect[0], tx)
        self.assertLessEqual(hrect[0] + hrect[2], tx + tw + 0.01)

    def test_ui_rects_file_if_present(self):
        path = pv.DEFAULT_RECTS
        if not os.path.exists(path):
            self.skipTest("ui_rects.json aun no exportado")
        data = json.load(open(path, encoding="utf-8"))
        self.assertEqual(len(data["sliders"]), 6)


class TestRecorderAndWinput(unittest.TestCase):
    def test_ffmpeg_command(self):
        cmd = rec.build_command((10, 20, 1281, 721), "o.mp4")
        self.assertEqual(cmd[0], "ffmpeg")
        joined = " ".join(cmd)
        for frag in ("-f gdigrab", "-framerate 60", "-draw_mouse 1", "-offset_x 10",
                     "-offset_y 20", "-video_size 1280x720", "-i desktop", "-c:v libx264",
                     "-preset ultrafast", "-crf 20", "-pix_fmt yuv420p"):
            self.assertIn(frag, joined)
        self.assertEqual(rec.parse_region("0,0,1920x1080"), (0, 0, 1920, 1080))

    @unittest.skipUnless(sys.platform == "win32", "solo Windows")
    def test_input_struct_and_normalize(self):
        import ctypes
        self.assertEqual(ctypes.sizeof(winput.INPUT), 40 if ctypes.sizeof(ctypes.c_void_p) == 8 else 28)
        self.assertEqual(winput.normalize_abs(0, 0, (0, 0, 1920, 1080)), (0, 0))
        self.assertEqual(winput.normalize_abs(1919, 1079, (0, 0, 1920, 1080)), (65535, 65535))
        self.assertEqual(winput.normalize_abs(-1920, 0, (-1920, 0, 3840, 1080))[0], 0)
        for k in "adswfrgtqezxp":
            self.assertIn(k, winput.SCANCODES)
        for k in ("up", "down", "left", "right", "backspace", "shift", "space"):
            self.assertIn(k, winput.SCANCODES)
        self.assertTrue(winput.SCANCODES["up"][1])

    def test_failsafe_esc_and_corner(self):
        # Esc mantenida
        t = [0.0]
        ev = threading.Event()
        fs = winput.Failsafe(ev, lambda r: None, esc_hold=1.0, get_esc=lambda: True,
                             get_cursor=lambda: (500, 500), clock=lambda: t[0])
        self.assertIsNone(fs.check_once())
        t[0] = 0.5
        self.assertIsNone(fs.check_once())
        t[0] = 1.1
        self.assertIn("Esc", fs.check_once())
        # Esc soltada reinicia el contador
        state = {"esc": True}
        fs2 = winput.Failsafe(ev, lambda r: None, get_esc=lambda: state["esc"],
                              get_cursor=lambda: (500, 500), clock=lambda: t[0])
        t[0] = 10.0
        fs2.check_once()
        state["esc"] = False
        fs2.check_once()
        state["esc"] = True
        t[0] = 10.9
        self.assertIsNone(fs2.check_once())
        # esquina
        fs3 = winput.Failsafe(ev, lambda r: None, get_esc=lambda: False,
                              get_cursor=lambda: (0, 0))
        self.assertIn("esquina", fs3.check_once())

    def test_failsafe_thread_triggers_callback(self):
        ev = threading.Event()
        called = []
        fs = winput.Failsafe(ev, called.append, poll=0.005, get_esc=lambda: False,
                             get_cursor=lambda: (1, 1))
        fs.start()
        self.assertTrue(ev.wait(2.0))
        fs._thread.join(1.0)
        self.assertEqual(len(called), 1)


class TestNoInputAtImport(unittest.TestCase):
    BANNED = {"SendInput", "send_input", "mouse_event", "keybd_event", "SetCursorPos",
              "mouse_move_abs", "mouse_button", "mouse_wheel", "key_event", "Popen", "SetCursor",
              "SetSystemCursor"}

    def _tree(self, name):
        with open(os.path.join(HERE, name), encoding="utf-8") as f:
            return ast.parse(f.read())

    def _toplevel_calls(self, tree):
        """Llamadas ejecutadas al importar: fuera de def/class (el cuerpo de if/try cuenta)."""
        found = []

        def visit(node):
            if isinstance(node, (ast.FunctionDef, ast.AsyncFunctionDef, ast.ClassDef, ast.Lambda)):
                return
            if isinstance(node, ast.Call):
                f = node.func
                found.append(f.attr if isinstance(f, ast.Attribute) else getattr(f, "id", ""))
            for child in ast.iter_child_nodes(node):
                visit(child)

        for stmt in tree.body:
            visit(stmt)
        return found

    def test_modules_do_not_send_input_when_imported(self):
        for name in ("humanize.py", "play_video.py", "recorder.py", "winput.py"):
            calls = set(self._toplevel_calls(self._tree(name)))
            self.assertFalse(calls & self.BANNED, (name, calls & self.BANNED))

    def test_pure_modules_do_not_import_winput_at_top(self):
        for name in ("humanize.py", "play_video.py", "recorder.py"):
            for stmt in self._tree(name).body:
                if isinstance(stmt, ast.Import):
                    self.assertNotIn("winput", [a.name for a in stmt.names], name)
                if isinstance(stmt, ast.ImportFrom):
                    self.assertNotEqual(stmt.module, "winput", name)

    def test_no_cursor_replacement_apis(self):
        for name in ("winput.py", "humanize.py", "play_video.py", "recorder.py"):
            with open(os.path.join(HERE, name), encoding="utf-8") as f:
                text = f.read()
            self.assertNotIn("SetSystemCursor(", text)
            self.assertNotIn("SetCursor(", text)
            self.assertNotIn("LoadCursor", text)


if __name__ == "__main__":
    unittest.main()
