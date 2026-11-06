"""Software replay of an SGSD clip frame (numpy) compared to the browser-baked reference."""
import gzip, struct, sys
import numpy as np
from PIL import Image

import os
ANIM = os.environ.get("SGS_KILLEFFECT_DIR", os.path.join(os.path.dirname(os.path.abspath(__file__)), "killEffect"))


def read(path):
    b = gzip.open(path).read(); p = 0
    def u(fmt):
        nonlocal p
        v = struct.unpack_from("<" + fmt, b, p); p += struct.calcsize("<" + fmt); return v
    def s():
        (n,) = u("H"); nonlocal p
        t = b[p:p+n].decode(); p += n; return t
    assert b[:4] == b"SGSD"; p = 4; u("H")
    fps, dur, W, H, ox, oy = u("6f")
    pages = []
    for _ in range(u("H")[0]):
        name = s(); w, h = u("HH"); pages.append((name, w, h))
    regions = [u("H8f") for _ in range(u("H")[0])]
    events = [(s(), u("f")[0]) for _ in range(u("H")[0])]
    frames = []
    for _ in range(u("H")[0]):
        frames.append([u("HBI8f") for _ in range(u("H")[0])])
    return dict(fps=fps, W=W, H=H, ox=ox, oy=oy, pages=pages, regions=regions, events=events, frames=frames)


def render(clip, fi):
    W, H = int(clip["W"]), int(clip["H"])
    tex = []
    for name, w, h in clip["pages"]:
        im = np.asarray(Image.open(ANIM + "\\" + name).convert("RGBA")).astype(np.float64) / 255
        im[..., :3] *= im[..., 3:4]  # premultiply like WebGL UNPACK_PREMULTIPLY_ALPHA
        tex.append(im)
    dst = np.zeros((H, W, 4))
    for rid, blend, rgba, *v in clip["frames"][fi]:
        page, *uv = clip["regions"][rid]
        T = tex[page]; th, tw = T.shape[:2]
        V = np.array(v).reshape(4, 2) + [clip["ox"], clip["oy"]]
        U = np.array(uv).reshape(4, 2) * [tw, th]
        # affine screen->texture from 3 points
        A = np.array([[V[i, 0], V[i, 1], 1] for i in range(3)])
        M = np.linalg.solve(A, U[:3])  # 3x2
        x0, y0 = np.floor(V.min(0)).astype(int); x1, y1 = np.ceil(V.max(0)).astype(int)
        x0, y0 = max(x0, 0), max(y0, 0); x1, y1 = min(x1, W), min(y1, H)
        if x1 <= x0 or y1 <= y0: continue
        ys, xs = np.mgrid[y0:y1, x0:x1] + 0.5
        tu = xs * M[0, 0] + ys * M[1, 0] + M[2, 0]
        tv = xs * M[0, 1] + ys * M[1, 1] + M[2, 1]
        umin, vmin = U.min(0); umax, vmax = U.max(0)
        inside = (tu >= umin) & (tu <= umax) & (tv >= vmin) & (tv <= vmax)
        fx, fy = tu - 0.5, tv - 0.5
        ix, iy = np.floor(fx).astype(int), np.floor(fy).astype(int)
        ax, ay = (fx - ix)[..., None], (fy - iy)[..., None]
        def g(yy, xx): return T[np.clip(yy, 0, th - 1), np.clip(xx, 0, tw - 1)]
        c = (g(iy, ix) * (1 - ax) * (1 - ay) + g(iy, ix + 1) * ax * (1 - ay) + g(iy + 1, ix) * (1 - ax) * ay + g(iy + 1, ix + 1) * ax * ay)
        r, gg, bb, a = [(rgba >> (8 * k)) & 255 for k in range(4)]
        a /= 255
        tint = np.array([r / 255 * a, gg / 255 * a, bb / 255 * a, a])
        S = c * tint * inside[..., None]
        D = dst[y0:y1, x0:x1]
        if blend == 0: D[:] = S + D * (1 - S[..., 3:4])
        elif blend == 1: D[:] = S + D
        else: D[:] = S + D * (1 - S)
        np.clip(D, 0, 1, out=D)
    return dst


if __name__ == "__main__":
    anim, fi = sys.argv[1], int(sys.argv[2])
    clip = read(f"rec60/sgsd/{anim}.sgsd.gz")
    out = render(clip, fi)
    ref = np.asarray(Image.open(f"out60/raw/{anim}/{fi:04d}.png").convert("RGBA")).astype(np.float64) / 255
    diff = np.abs(out - ref)
    print(f"{anim} f{fi}: mean abs diff {diff.mean()*255:.3f}/255, p99 {np.percentile(diff,99)*255:.1f}, max {diff.max()*255:.0f}; draws {len(clip['frames'][fi])}")
    vis = np.concatenate([ref[..., :3], out[..., :3], np.clip(diff[..., :3] * 8, 0, 1)], axis=1)
    Image.fromarray((vis * 255).astype("uint8")).resize((1800, 337)).save(f"cmp_{anim}_{fi}.png")
