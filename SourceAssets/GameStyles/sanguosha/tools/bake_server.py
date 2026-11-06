"""Offline baker for original Sanguosha (三国杀) multi-kill Spine animations.

Serves the game's own LayaAir + Spine 4.0 runtime (extracted from laya.sgs) and the
original .skel/.atlas/.png files to headless Chrome, which steps the animation at a
fixed fps and posts back the raw premultiplied RGBA framebuffer.  Frames are stored
as PNG whose channels hold *premultiplied* values (needed to keep additive glow that
straight alpha cannot represent).
"""
import argparse
import json
import os
import shutil
import subprocess
import sys
import threading
import time
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
from urllib.parse import urlparse, parse_qs

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
# Game runtime extracted from libs/min/laya.sgs and the original killEffect folder (not committed).
ENGINE = os.environ.get("SGS_LAYA_LIB", os.path.join(HERE, "laya_lib"))
ANIM = os.environ.get("SGS_KILLEFFECT_DIR", os.path.join(HERE, "killEffect"))
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"

MIME = {".js": "application/javascript", ".html": "text/html", ".png": "image/png",
        ".atlas": "text/plain", ".skel": "application/octet-stream"}


def make_handler(out_dir, done_evt):
    class H(BaseHTTPRequestHandler):
        def log_message(self, *a):
            pass

        def _send(self, code, body=b"", ctype="text/plain"):
            self.send_response(code)
            self.send_header("Content-Type", ctype)
            self.send_header("Content-Length", str(len(body)))
            self.send_header("Access-Control-Allow-Origin", "*")
            self.end_headers()
            self.wfile.write(body)

        def do_GET(self):
            p = urlparse(self.path).path
            if p.startswith("/engine/"):
                fp = os.path.join(ENGINE, p[len("/engine/"):])
            elif p.startswith("/anim/"):
                fp = os.path.join(ANIM, p[len("/anim/"):])
            else:
                fp = os.path.join(HERE, p.lstrip("/"))
            if not os.path.isfile(fp):
                return self._send(404, b"nf")
            with open(fp, "rb") as f:
                self._send(200, f.read(), MIME.get(os.path.splitext(fp)[1], "application/octet-stream"))

        def do_POST(self):
            u = urlparse(self.path)
            q = parse_qs(u.query)
            n = int(self.headers.get("Content-Length", 0))
            body = self.rfile.read(n) if n else b""
            if u.path == "/frame":
                anim, i = q["anim"][0], int(q["i"][0])
                w, h = int(q["w"][0]), int(q["h"][0])
                img = Image.frombytes("RGBA", (w, h), body).transpose(Image.FLIP_TOP_BOTTOM)
                d = os.path.join(out_dir, "raw", anim)
                os.makedirs(d, exist_ok=True)
                img.save(os.path.join(d, f"{i:04d}.png"), compress_level=1)
                if i % 20 == 0:
                    print(f"  {anim} frame {i}", flush=True)
            elif u.path == "/meta":
                anim = q["anim"][0]
                with open(os.path.join(out_dir, "raw", anim, "meta.json"), "wb") as f:
                    f.write(body)
                print(f"  {anim} meta written", flush=True)
            elif u.path == "/drawlist":
                anim = q["anim"][0]
                d = os.path.join(out_dir, "drawlists")
                os.makedirs(d, exist_ok=True)
                with open(os.path.join(d, anim + ".json"), "wb") as f:
                    f.write(body)
                print(f"  {anim} drawlist {len(body)//1024} KiB", flush=True)
            elif u.path == "/log":
                print("[page]", body.decode("utf-8", "replace"), flush=True)
            elif u.path == "/done":
                done_evt.set()
            self._send(200, b"ok")

        def do_OPTIONS(self):
            self._send(200)
    return H


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--anims", default="shoupo,lianpo,sanpo,sipo,wupo,liupo,qipo")
    ap.add_argument("--fps", type=int, default=60)
    ap.add_argument("--scale", type=float, default=1.0)
    ap.add_argument("--out", default=os.path.join(HERE, "out"))
    ap.add_argument("--port", type=int, default=18765)
    ap.add_argument("--page", default="bake.html")
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    done = threading.Event()
    srv = ThreadingHTTPServer(("127.0.0.1", a.port), make_handler(a.out, done))
    threading.Thread(target=srv.serve_forever, daemon=True).start()
    W, H = int(1600 * a.scale), int(900 * a.scale)
    url = f"http://127.0.0.1:{a.port}/{a.page}?anims={a.anims}&fps={a.fps}&scale={a.scale}"
    prof = os.path.join(HERE, "chrome-profile-" + str(a.port))
    shutil.rmtree(prof, ignore_errors=True)
    cmd = [CHROME, "--headless=new", f"--user-data-dir={prof}", "--force-device-scale-factor=1",
           f"--window-size={W},{H}", "--enable-webgl", "--ignore-gpu-blocklist",
           "--enable-unsafe-swiftshader", "--use-angle=swiftshader", "--disable-background-timer-throttling",
           "--disable-renderer-backgrounding", "--no-first-run", url]
    proc = subprocess.Popen(cmd, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    t0 = time.time()
    try:
        while not done.wait(1):
            if proc.poll() is not None:
                print("chrome exited early", proc.returncode)
                break
            if time.time() - t0 > 3600:
                print("timeout")
                break
    finally:
        proc.kill()
        srv.shutdown()
    print(f"done in {time.time()-t0:.1f}s")


if __name__ == "__main__":
    main()
