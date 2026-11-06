"""Download every harvested panorama, shrink it to VRChat's limit (2048x1024 JPEG)
and lay out a static site that GitHub Pages can host:

  site/locations.json   - the place table (only places whose photo worked)
  site/p/00042.jpg      - photo for place id 42
  site/index.html       - credits page (CC BY-SA needs attribution)

VRChat only loads images from trusted hosts unless players opt in, and
*.github.io is one of them, which is why we re-host instead of hot-linking.
"""
import argparse, html, io, json, os, sys, time
import requests
from PIL import Image

UA = "GeoQuestWorldBuilder/1.0 (VRChat geography game; https://github.com/bacon-guyderp/altdispenserapp)"
HERE = os.path.dirname(os.path.abspath(__file__))


def fetch(s, url, tries=6):
    delay = 5
    for _ in range(tries):
        try:
            r = s.get(url, timeout=90)
            if r.status_code == 200:
                return r.content
            if r.status_code == 404:
                return None
            print(f"  HTTP {r.status_code}, waiting {delay}s", file=sys.stderr)
        except requests.RequestException as e:
            print(f"  {e}, waiting {delay}s", file=sys.stderr)
        time.sleep(delay)
        delay = min(delay * 2, 120)
    return None


def encode(im, budget):
    """Highest JPEG quality that fits the per-photo budget."""
    data = b""
    for q in (82, 76, 70, 64, 58, 52, 46, 40):
        buf = io.BytesIO()
        im.save(buf, "JPEG", quality=q, optimize=True, subsampling="4:2:0")
        data = buf.getvalue()
        if len(data) <= budget:
            break
    return data


def source_url(p):
    page = p.get("page", "")
    return page if page.startswith("http") else "https://commons.wikimedia.org/wiki/" + page.replace(" ", "_")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--locations", default=os.path.join(HERE, "..", "Assets", "GeoQuest", "Data", "locations.json"))
    ap.add_argument("--out", default=os.path.join(HERE, "..", "site"))
    ap.add_argument("--max-site-mb", type=float, default=980,
                    help="GitHub Pages sites are capped at 1 GB; photo quality adapts to fit")
    ap.add_argument("--sleep", type=float, default=0.4)
    a = ap.parse_args()

    locs = json.load(open(a.locations, encoding="utf-8"))["locations"]
    pdir = os.path.join(a.out, "p")
    os.makedirs(pdir, exist_ok=True)
    s = requests.Session()
    s.headers["User-Agent"] = UA
    # Share the size budget across every photo: ~5,000 places is about 190 KB each,
    # which is still a clean 2048x1024 street scene at JPEG quality ~55-70.
    budget_total = a.max_site_mb * 1024 * 1024 - 8 * 1024 * 1024  # leave room for json/html
    per_photo = min(450 * 1024, budget_total / max(1, len(locs)))
    print(f"{len(locs)} places, about {per_photo / 1024:.0f} KB per photo")
    used = 0
    ok = []
    for i, p in enumerate(locs):
        path = os.path.join(pdir, p.get("file") or f"{p['id']:05d}.jpg")
        if not os.path.exists(path):
            data = fetch(s, p["src"])
            time.sleep(a.sleep)
            if not data:
                print(f"skip {p['id']} (download failed)")
                continue
            try:
                im = Image.open(io.BytesIO(data)).convert("RGB").resize((2048, 1024), Image.LANCZOS)
                with open(path, "wb") as f:
                    f.write(encode(im, per_photo))
            except Exception as e:  # corrupt / odd formats
                print(f"skip {p['id']} ({e})")
                continue
        size = os.path.getsize(path)
        if used + size > budget_total:
            print(f"site is full at {len(ok)} places; skipping the rest")
            break
        used += size
        ok.append(p)
        if i % 50 == 0:
            print(f"{i + 1}/{len(locs)} photos")

    meta = json.load(open(a.locations, encoding="utf-8"))
    meta.update(generator="build_site.py", count=len(ok), locations=ok)
    with open(os.path.join(a.out, "locations.json"), "w", encoding="utf-8") as f:
        json.dump(meta, f, ensure_ascii=False, indent=0)
    rows = "\n".join(
        f"<tr><td>{p['id']}</td><td>{html.escape(p['title'])}</td><td>{html.escape(p['country'])}</td>"
        f"<td>{html.escape(p['author'])}</td><td>{html.escape(p['license'])}</td>"
        f"<td><a href=\"{html.escape(source_url(p))}\">{html.escape(p.get('source', 'source'))}</a></td></tr>"
        for p in ok)
    with open(os.path.join(a.out, "index.html"), "w", encoding="utf-8") as f:
        f.write(f"""<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>GeoQuest photo credits</title>
<style>body{{font:14px system-ui;margin:16px}}table{{border-collapse:collapse}}td{{border-bottom:1px solid #ddd;padding:3px 8px}}</style>
<h1>GeoQuest photo credits</h1>
<p>{len(ok)} panoramas from Mapillary and Wikimedia Commons contributors, resized to 2048&times;1024. Each photo keeps its original license
(see the source link); the resized copies are shared under the same terms.</p>
<table><tr><th>#</th><th>Place</th><th>Country</th><th>Author</th><th>License</th><th></th></tr>
{rows}
</table>""")
    open(os.path.join(a.out, ".nojekyll"), "w").close()
    print(f"site ready: {len(ok)} places, {used / 1024 / 1024:.0f} MB of photos in {a.out}")


if __name__ == "__main__":
    main()
