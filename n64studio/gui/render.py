"""Tiny software renderer for model previews (no OpenGL dependency).

* ``wire``     - wireframe
* ``solid``    - flat shaded, painter's algorithm (fast, used while dragging)
* ``textured`` - z-buffered, perspective-less textured rasteriser in numpy
"""
from __future__ import annotations

import math

import numpy as np
from PIL import Image, ImageDraw

from ..model import Mesh

BG = (40, 42, 48)


class MeshView:
    def __init__(self, mesh: Mesh):
        self.mesh = mesh
        self.pos = np.array([(v.x, v.y, v.z) for v in mesh.vertices], dtype=np.float64).reshape(-1, 3)
        self.uv = np.array([(v.u, v.v) for v in mesh.vertices], dtype=np.float64).reshape(-1, 2)
        self.col = np.array([(v.r, v.g, v.b) for v in mesh.vertices], dtype=np.float64).reshape(-1, 3)
        self.tris = np.array([t[:3] for t in mesh.triangles], dtype=np.int64).reshape(-1, 3)
        self.mats = np.array([t[3] for t in mesh.triangles], dtype=np.int64)
        if len(self.pos):
            lo, hi = self.pos.min(0), self.pos.max(0)
            self.center = (lo + hi) / 2
            self.radius = float(np.linalg.norm(hi - lo) / 2) or 1.0
        else:
            self.center = np.zeros(3)
            self.radius = 1.0
        self.textures = []
        self.mat_colors = []
        for m in mesh.materials:
            if m.texture is not None:
                arr = np.asarray(m.texture.convert("RGBA"), dtype=np.float64) / 255.0
                self.textures.append(arr)
                a = arr[..., 3:4]
                mean = (arr[..., :3] * a).sum((0, 1)) / max(a.sum(), 1e-6)
                self.mat_colors.append(mean * np.array(m.color[:3]))
            else:
                self.textures.append(None)
                self.mat_colors.append(np.array(m.color[:3], dtype=np.float64))
        if not self.mat_colors:
            self.mat_colors.append(np.array([0.8, 0.8, 0.8]))
        self.yaw = math.radians(35)
        self.pitch = math.radians(-20)
        self.zoom = 1.0
        self.pan = np.zeros(2)

    # ----------------------------------------------------------------
    def _project(self, w: int, h: int):
        cy, sy = math.cos(self.yaw), math.sin(self.yaw)
        cp, sp = math.cos(self.pitch), math.sin(self.pitch)
        ry = np.array([[cy, 0, sy], [0, 1, 0], [-sy, 0, cy]])
        rx = np.array([[1, 0, 0], [0, cp, -sp], [0, sp, cp]])
        rot = rx @ ry
        p = (self.pos - self.center) @ rot.T
        s = min(w, h) * 0.45 / self.radius * self.zoom
        sx = p[:, 0] * s + w / 2 + self.pan[0]
        sy_ = -p[:, 1] * s + h / 2 + self.pan[1]
        return np.stack([sx, sy_], 1), p[:, 2], rot

    def _face_light(self, rot) -> np.ndarray:
        if not len(self.tris):
            return np.zeros(0)
        p = (self.pos - self.center) @ rot.T
        a, b, c = p[self.tris[:, 0]], p[self.tris[:, 1]], p[self.tris[:, 2]]
        n = np.cross(b - a, c - a)
        ln = np.linalg.norm(n, axis=1)
        ln[ln == 0] = 1
        n /= ln[:, None]
        light = np.array([0.3, 0.5, -0.8])
        light /= np.linalg.norm(light)
        return 0.35 + 0.65 * np.abs(n @ light)

    def render(self, w: int, h: int, mode: str = "textured", show_wire: bool = False) -> Image.Image:
        w, h = max(w, 16), max(h, 16)
        img = Image.new("RGB", (w, h), BG)
        if not len(self.tris):
            ImageDraw.Draw(img).text((10, 10), "No triangles decoded", fill=(220, 220, 220))
            return img
        scr, depth, rot = self._project(w, h)
        if mode == "wire":
            draw = ImageDraw.Draw(img)
            for t in self.tris:
                pts = [tuple(scr[i]) for i in t]
                draw.line(pts + [pts[0]], fill=(120, 200, 255), width=1)
            return img
        light = self._face_light(rot)
        if mode == "textured":
            img = self._render_textured(w, h, scr, depth, light)
        else:
            draw = ImageDraw.Draw(img)
            order = np.argsort(-depth[self.tris].mean(1))
            vcol = self.col[self.tris].mean(1)
            for fi in order:
                c = self.mat_colors[self.mats[fi]] * vcol[fi] * light[fi]
                rgb = tuple(int(x) for x in np.clip(c * 255, 0, 255))
                draw.polygon([tuple(scr[i]) for i in self.tris[fi]], fill=rgb)
        if show_wire:
            draw = ImageDraw.Draw(img)
            for t in self.tris:
                pts = [tuple(scr[i]) for i in t]
                draw.line(pts + [pts[0]], fill=(20, 20, 20), width=1)
        return img

    def _render_textured(self, w, h, scr, depth, light) -> Image.Image:
        color = np.empty((h, w, 3), dtype=np.float64)
        color[:] = np.array(BG) / 255.0
        zbuf = np.full((h, w), np.inf)
        for fi, (i0, i1, i2) in enumerate(self.tris):
            p0, p1, p2 = scr[i0], scr[i1], scr[i2]
            minx = max(int(math.floor(min(p0[0], p1[0], p2[0]))), 0)
            maxx = min(int(math.ceil(max(p0[0], p1[0], p2[0]))), w - 1)
            miny = max(int(math.floor(min(p0[1], p1[1], p2[1]))), 0)
            maxy = min(int(math.ceil(max(p0[1], p1[1], p2[1]))), h - 1)
            if minx > maxx or miny > maxy:
                continue
            area = (p1[0] - p0[0]) * (p2[1] - p0[1]) - (p2[0] - p0[0]) * (p1[1] - p0[1])
            if abs(area) < 1e-9:
                continue
            xs = np.arange(minx, maxx + 1) + 0.5
            ys = np.arange(miny, maxy + 1) + 0.5
            gx, gy = np.meshgrid(xs, ys)
            w0 = ((p1[0] - gx) * (p2[1] - gy) - (p2[0] - gx) * (p1[1] - gy)) / area
            w1 = ((p2[0] - gx) * (p0[1] - gy) - (p0[0] - gx) * (p2[1] - gy)) / area
            w2 = 1.0 - w0 - w1
            inside = (w0 >= -1e-6) & (w1 >= -1e-6) & (w2 >= -1e-6)
            if not inside.any():
                continue
            z = w0 * depth[i0] + w1 * depth[i1] + w2 * depth[i2]
            sub_z = zbuf[miny:maxy + 1, minx:maxx + 1]
            vis = inside & (z < sub_z)
            if not vis.any():
                continue
            m = self.mats[fi]
            tex = self.textures[m] if m < len(self.textures) else None
            vc = (w0[..., None] * self.col[i0] + w1[..., None] * self.col[i1] + w2[..., None] * self.col[i2])
            if tex is not None:
                u = w0 * self.uv[i0, 0] + w1 * self.uv[i1, 0] + w2 * self.uv[i2, 0]
                v = w0 * self.uv[i0, 1] + w1 * self.uv[i1, 1] + w2 * self.uv[i2, 1]
                th, tw = tex.shape[:2]
                tx = (np.floor(u * tw).astype(np.int64)) % tw
                ty = (np.floor(v * th).astype(np.int64)) % th
                texel = tex[ty, tx]
                vis &= texel[..., 3] > 0.3
                rgb = texel[..., :3] * np.array(self.mesh.materials[m].color[:3])
            else:
                rgb = np.broadcast_to(self.mat_colors[m], vc.shape)
            shade = rgb * vc * light[fi]
            sub_c = color[miny:maxy + 1, minx:maxx + 1]
            sub_c[vis] = shade[vis]
            sub_z[vis] = z[vis]
        return Image.fromarray(np.clip(color * 255, 0, 255).astype(np.uint8), "RGB")


def checkerboard(w: int, h: int, cell: int = 8) -> Image.Image:
    yy, xx = np.mgrid[0:h, 0:w]
    on = ((xx // cell) + (yy // cell)) % 2 == 0
    arr = np.where(on[..., None], 0xCC, 0x99).astype(np.uint8)
    return Image.fromarray(np.repeat(arr, 3, axis=2), "RGB")


def waveform(pcm: np.ndarray, w: int, h: int) -> Image.Image:
    img = Image.new("RGB", (max(w, 16), max(h, 16)), (24, 26, 30))
    d = ImageDraw.Draw(img)
    d.line([(0, h // 2), (w, h // 2)], fill=(60, 60, 70))
    if len(pcm) == 0:
        return img
    n = len(pcm)
    data = np.asarray(pcm, dtype=np.float64)
    edges = np.linspace(0, n, w + 1).astype(np.int64)
    for x in range(w):
        a, b = edges[x], max(edges[x + 1], edges[x] + 1)
        c = data[a:min(b, n)]
        if not len(c):
            continue
        lo, hi = c.min() / 32768.0, c.max() / 32768.0
        d.line([(x, h / 2 - hi * h / 2 * 0.95), (x, h / 2 - lo * h / 2 * 0.95)], fill=(90, 200, 140))
    return img
