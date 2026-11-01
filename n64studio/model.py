"""Generic in-memory mesh representation shared by decoders and exporters."""
from __future__ import annotations

import math
from dataclasses import dataclass, field

from PIL import Image


@dataclass
class Material:
    name: str
    texture: Image.Image | None = None
    color: tuple[float, float, float, float] = (1.0, 1.0, 1.0, 1.0)
    wrap_s: str = "repeat"  # repeat | mirror | clamp
    wrap_t: str = "repeat"
    texture_info: str = ""

    @property
    def has_alpha(self) -> bool:
        if self.texture is None:
            return self.color[3] < 1.0
        lo, _hi = self.texture.getchannel("A").getextrema()
        return lo < 255


@dataclass
class Vertex:
    x: float
    y: float
    z: float
    u: float = 0.0
    v: float = 0.0
    r: float = 1.0
    g: float = 1.0
    b: float = 1.0
    a: float = 1.0
    nx: float = 0.0
    ny: float = 1.0
    nz: float = 0.0


@dataclass
class Mesh:
    name: str
    vertices: list[Vertex] = field(default_factory=list)
    triangles: list[tuple[int, int, int, int]] = field(default_factory=list)  # (a, b, c, material)
    materials: list[Material] = field(default_factory=list)
    has_normals: bool = False
    has_colors: bool = False
    notes: list[str] = field(default_factory=list)

    def bounds(self):
        if not self.vertices:
            return (0, 0, 0), (0, 0, 0)
        xs = [v.x for v in self.vertices]
        ys = [v.y for v in self.vertices]
        zs = [v.z for v in self.vertices]
        return (min(xs), min(ys), min(zs)), (max(xs), max(ys), max(zs))

    def triangles_by_material(self) -> dict[int, list[tuple[int, int, int]]]:
        groups: dict[int, list[tuple[int, int, int]]] = {}
        for a, b, c, m in self.triangles:
            groups.setdefault(m, []).append((a, b, c))
        return groups

    def compute_normals(self) -> None:
        """Fill in smooth per-vertex normals from face geometry."""
        acc = [[0.0, 0.0, 0.0] for _ in self.vertices]
        for a, b, c, _m in self.triangles:
            va, vb, vc = self.vertices[a], self.vertices[b], self.vertices[c]
            ux, uy, uz = vb.x - va.x, vb.y - va.y, vb.z - va.z
            wx, wy, wz = vc.x - va.x, vc.y - va.y, vc.z - va.z
            n = (uy * wz - uz * wy, uz * wx - ux * wz, ux * wy - uy * wx)
            for i in (a, b, c):
                acc[i][0] += n[0]
                acc[i][1] += n[1]
                acc[i][2] += n[2]
        for v, n in zip(self.vertices, acc):
            ln = math.sqrt(n[0] ** 2 + n[1] ** 2 + n[2] ** 2) or 1.0
            v.nx, v.ny, v.nz = n[0] / ln, n[1] / ln, n[2] / ln
        self.has_normals = True

    def merged_with(self, other: "Mesh", name: str | None = None) -> "Mesh":
        out = Mesh(name or self.name, list(self.vertices), list(self.triangles), list(self.materials),
                   self.has_normals and other.has_normals, self.has_colors or other.has_colors,
                   self.notes + other.notes)
        vo = len(out.vertices)
        mo = len(out.materials)
        out.vertices += other.vertices
        out.materials += other.materials
        out.triangles += [(a + vo, b + vo, c + vo, m + mo) for a, b, c, m in other.triangles]
        return out
