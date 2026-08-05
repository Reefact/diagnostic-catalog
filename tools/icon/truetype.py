"""Reading glyph outlines out of a TrueType file.

Only what drawing a badge needs: the character map, a glyph's advance, and its contours as
polygons. Composite glyphs, hinting and kerning are absent — the badges are two or three
unaccented capitals, none of which is composite, and at this size kerning moves nothing a
reader could see.

Here rather than in template.py because it answers a different question: template.py knows what
the mark is, this knows what a letter looks like, and only render-icon.py needs both.
"""
import struct, math


def _tables(d):
    n = struct.unpack('>H', d[4:6])[0]
    t = {}
    for i in range(n):
        o = 12 + 16 * i
        tag = d[o:o + 4].decode('latin1')
        off, ln = struct.unpack('>II', d[o + 8:o + 16])
        t[tag] = (off, ln)
    return t


def _flags(d, p, npts):
    """The per-point flag bytes, expanded through the repeat bit."""
    flags = []
    while len(flags) < npts:
        f = d[p]; p += 1
        flags.append(f)
        if f & 8:
            r = d[p]; p += 1
            flags += [f] * r
    return flags, p


def _coords(d, p, flags, short, same):
    """One axis of the point deltas, accumulated into absolute font units.

    x and y are encoded identically and only the flag bits differ — 2/16 for x, 4/32 for y —
    so reading them apart would be this loop written twice.
    """
    v, out = 0, []
    for f in flags:
        if f & short:
            delta = d[p]; p += 1
            v += delta if f & same else -delta
        elif not f & same:
            v += struct.unpack('>h', d[p:p + 2])[0]; p += 2
        out.append(v)
    return out, p


def _quadratic(start, control, end):
    """A quadratic flattened to 16 segments, `start` excluded — the caller already has it."""
    out = []
    for k in range(1, 17):
        t = k / 16
        out.append(((1 - t) ** 2 * start[0] + 2 * (1 - t) * t * control[0] + t * t * end[0],
                    (1 - t) ** 2 * start[1] + 2 * (1 - t) * t * control[1] + t * t * end[1]))
    return out


def _from_on_curve(pts):
    """The contour rotated to begin on-curve, inserting the implied point if it has none."""
    if not any(on for _, _, on in pts):
        x0, y0, _ = pts[0]; x1, y1, _ = pts[-1]
        pts.insert(0, ((x0 + x1) / 2, (y0 + y1) / 2, True))
    while not pts[0][2]:
        pts.append(pts.pop(0))
    return pts


def _polygon(pts):
    """One closed contour: on-curve points kept, quadratics flattened between them."""
    pts = _from_on_curve(pts)
    n = len(pts)
    cur = (pts[0][0], pts[0][1])
    poly = [cur]
    i = 1
    while i <= n:
        px, py, on = pts[i % n]
        if on:
            cur = (px, py)
            poly.append(cur)
            i += 1
            continue
        nx, ny, non = pts[(i + 1) % n]
        end = (nx, ny) if non else ((px + nx) / 2, (py + ny) / 2)
        poly += _quadratic(cur, (px, py), end)
        cur = end
        # An on-curve neighbour IS the segment's endpoint and is consumed with it; an off-curve
        # one only supplied the implied midpoint above and is read again on the next turn.
        i += 2 if non else 1
    return poly


class Font:
    def __init__(self, path):
        # Read through a context manager: the handle was left to the collector before, which is
        # the kind of thing that holds a file open on an interpreter that does not refcount.
        with open(path, 'rb') as source:
            self.d = d = source.read()
        self.t = t = _tables(d)
        if 'glyf' not in t:
            raise ValueError('not a glyf font (CFF/OTF outlines unsupported)')
        self.upem = struct.unpack('>H', d[t['head'][0] + 18:t['head'][0] + 20])[0]
        fmt = struct.unpack('>h', d[t['head'][0] + 50:t['head'][0] + 52])[0]
        n = struct.unpack('>H', d[t['maxp'][0] + 4:t['maxp'][0] + 6])[0]
        lo = t['loca'][0]
        if fmt == 0:
            self.loca = [2 * struct.unpack('>H', d[lo + 2 * i:lo + 2 * i + 2])[0] for i in range(n + 1)]
        else:
            self.loca = [struct.unpack('>I', d[lo + 4 * i:lo + 4 * i + 4])[0] for i in range(n + 1)]
        self._cmap()

    def _cmap(self):
        d, co = self.d, self.t['cmap'][0]
        ntab = struct.unpack('>H', d[co + 2:co + 4])[0]
        sub = None
        for i in range(ntab):
            pid, eid, off = struct.unpack('>HHI', d[co + 4 + 8 * i:co + 12 + 8 * i])
            if (pid, eid) in ((3, 1), (0, 3), (0, 4), (3, 10)):
                sub = co + off
        if sub is None or struct.unpack('>H', d[sub:sub + 2])[0] != 4:
            raise ValueError('no format 4 cmap')
        segx2 = struct.unpack('>H', d[sub + 6:sub + 8])[0]
        seg = segx2 // 2
        self._end = [struct.unpack('>H', d[sub + 14 + 2 * i:sub + 16 + 2 * i])[0] for i in range(seg)]
        self._start = [struct.unpack('>H', d[sub + 16 + segx2 + 2 * i:sub + 18 + segx2 + 2 * i])[0] for i in range(seg)]
        self._delta = [struct.unpack('>h', d[sub + 16 + 2 * segx2 + 2 * i:sub + 18 + 2 * segx2 + 2 * i])[0] for i in range(seg)]
        self._rbase = sub + 16 + 3 * segx2
        self._rng = [struct.unpack('>H', d[self._rbase + 2 * i:self._rbase + 2 * i + 2])[0] for i in range(seg)]

    def gid(self, ch):
        c = ord(ch)
        for i in range(len(self._end)):
            if self._start[i] <= c <= self._end[i]:
                if self._rng[i] == 0:
                    return (c + self._delta[i]) & 0xFFFF
                p = self._rbase + 2 * i + self._rng[i] + 2 * (c - self._start[i])
                g = struct.unpack('>H', self.d[p:p + 2])[0]
                return (g + self._delta[i]) & 0xFFFF if g else 0
        return 0

    def advance(self, ch):
        d, t = self.d, self.t
        nhm = struct.unpack('>H', d[t['hhea'][0] + 34:t['hhea'][0] + 36])[0]
        g = self.gid(ch)
        i = min(g, nhm - 1)
        return struct.unpack('>H', d[t['hmtx'][0] + 4 * i:t['hmtx'][0] + 4 * i + 2])[0]

    def contours(self, ch):
        """Closed polygons in font units, y up. Quadratics flattened."""
        g = self.gid(ch)
        if g <= 0 or g + 1 >= len(self.loca) or self.loca[g] == self.loca[g + 1]:
            return None
        d = self.d
        o = self.t['glyf'][0] + self.loca[g]
        nc = struct.unpack('>h', d[o:o + 2])[0]
        if nc < 0:
            return None                                   # composite: not needed for these letters
        ends = [struct.unpack('>H', d[o + 10 + 2 * i:o + 12 + 2 * i])[0] for i in range(nc)]
        npts = ends[-1] + 1
        p = o + 10 + 2 * nc
        ilen = struct.unpack('>H', d[p:p + 2])[0]
        flags, p = _flags(d, p + 2 + ilen, npts)
        xs, p = _coords(d, p, flags, 2, 16)
        ys, _ = _coords(d, p, flags, 4, 32)

        out, start = [], 0
        for e in ends:
            pts = [(xs[i], ys[i], bool(flags[i] & 1)) for i in range(start, e + 1)]
            start = e + 1
            if pts:
                out.append(_polygon(pts))
        return out
