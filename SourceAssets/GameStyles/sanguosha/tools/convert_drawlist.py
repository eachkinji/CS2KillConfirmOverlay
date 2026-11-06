"""Convert recorded Laya/Spine draw lists into the compact SGSD binary clip format.

SGSD v1 (little endian, whole file gzip-compressed):
  char[4] 'SGSD'; u16 version=1
  f32 fps; f32 duration; f32 designW; f32 designH; f32 originX; f32 originY
  u16 pageCount;   { u16 nameLen; utf8 name; u16 width; u16 height }
  u16 regionCount; { u16 page; f32 u0,v0,u1,v1,u2,v2,u3,v3 }
  u16 eventCount;  { u16 nameLen; utf8 name; f32 time }
  u16 frameCount;  { u16 drawCount; { u16 region; u8 blend; u32 rgba; f32 x0,y0,x1,y1,x2,y2,x3,y3 } }
blend: 0 normal (ONE, ONE_MINUS_SRC_ALPHA), 1 Laya "light" (ONE, ONE), 2 screen (ONE, ONE_MINUS_SRC_COLOR)
rgba: Laya's packed vertex colour, byte0=R byte1=G byte2=B byte3=A (straight; Laya premultiplies in the VS)
vertices: skeleton-local Laya coordinates (Spine Y already negated), relative to the design origin.
"""
import gzip
import json
import struct
import sys
import os


def convert(src, dst):
    d = json.load(open(src, encoding="utf-8"))
    assert d["format"] == "sanguosha-spine-drawlist" and d["version"] == 1
    assert d["check"]["nonQuadDraws"] == 0 and d["check"]["maxParallelogramError"] < 0.01
    out = bytearray()
    w = out.extend
    w(b"SGSD"); w(struct.pack("<H", 1))
    des = d["design"]
    w(struct.pack("<6f", d["fps"], d["duration"], des["width"], des["height"], des["originX"], des["originY"]))

    def s(txt):
        b = txt.encode("utf-8")
        w(struct.pack("<H", len(b))); w(b)
    w(struct.pack("<H", len(d["pages"])))
    for p in d["pages"]:
        s(p["file"]); w(struct.pack("<HH", p["width"], p["height"]))
    w(struct.pack("<H", len(d["regions"])))
    for r in d["regions"]:
        assert len(r) == 9
        w(struct.pack("<H8f", int(r[0]), *r[1:]))
    w(struct.pack("<H", len(d["events"])))
    for e in d["events"]:
        s(e["name"]); w(struct.pack("<f", e["time"]))
    w(struct.pack("<H", len(d["frames"])))
    for fr in d["frames"]:
        w(struct.pack("<H", len(fr)))
        for dr in fr:
            assert dr[1] in (0, 1, 2), dr
            w(struct.pack("<HBI8f", dr[0], dr[1], dr[2], *dr[3:11]))
    with open(dst, "wb") as raw:
        with gzip.GzipFile(filename="", fileobj=raw, mode="wb", compresslevel=9, mtime=0) as f:
            f.write(bytes(out))
    return len(out), os.path.getsize(dst)


if __name__ == "__main__":
    src_dir, dst_dir = sys.argv[1], sys.argv[2]
    os.makedirs(dst_dir, exist_ok=True)
    for name in sorted(os.listdir(src_dir)):
        if name.endswith(".json"):
            raw, gz = convert(os.path.join(src_dir, name), os.path.join(dst_dir, name[:-5] + ".sgsd.gz"))
            print(f"{name}: {raw/1024:.0f} KiB -> {gz/1024:.0f} KiB")
