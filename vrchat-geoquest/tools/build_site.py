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


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--locations", default=os.path.join(HERE, "..", "Assets", "GeoQuest", "Data", "locations.json"))
    ap.add_argument("--out", default=os.path.join(HERE, "..", "site"))
    ap.add_argument("--quality", type=int, default=72)
    ap.add_argument("--sleep", type=float, default=0.4)
    a = ap.parse_args()

    locs = json.load(open(a.locations, encoding="utf-8"))["locations"]
    pdir = os.path.join(a.out, "p")
    os.makedirs(pdir, exist_ok=True)
    s = requests.Session()
    s.headers["User-Agent"] = UA
    ok = []
    for i, p in enumerate(locs):
        path = os.path.join(pdir, f"{p['id']:05d}.jpg")
        if not os.path.exists(path):
            data = fetch(s, p["src"])
            time.sleep(a.sleep)
            if not data:
                print(f"skip {p['id']} (download failed)")
                continue
            try:
                im = Image.open(io.BytesIO(data)).convert("RGB").resize((2048, 1024), Image.LANCZOS)
                im.save(path, "JPEG", quality=a.quality, optimize=True)
            except Exception as e:  # corrupt / odd formats
                print(f"skip {p['id']} ({e})")
                continue
        ok.append(p)
        if i % 50 == 0:
            print(f"{i + 1}/{len(locs)} photos")

    with open(os.path.join(a.out, "locations.json"), "w", encoding="utf-8") as f:
        json.dump({"generator": "build_site.py", "count": len(ok), "locations": ok}, f, ensure_ascii=False, indent=0)
    rows = "\n".join(
        f"<tr><td>{p['id']}</td><td>{html.escape(p['title'])}</td><td>{html.escape(p['country'])}</td>"
        f"<td>{html.escape(p['author'])}</td><td>{html.escape(p['license'])}</td>"
        f"<td><a href=\"https://commons.wikimedia.org/wiki/{html.escape(p['page'].replace(' ', '_'))}\">source</a></td></tr>"
        for p in ok)
    with open(os.path.join(a.out, "index.html"), "w", encoding="utf-8") as f:
        f.write(f"""<!doctype html><meta charset="utf-8"><meta name="viewport" content="width=device-width">
<title>GeoQuest photo credits</title>
<style>body{{font:14px system-ui;margin:16px}}table{{border-collapse:collapse}}td{{border-bottom:1px solid #ddd;padding:3px 8px}}</style>
<h1>GeoQuest photo credits</h1>
<p>{len(ok)} panoramas from Wikimedia Commons, resized to 2048&times;1024. Each photo keeps its original license
(see the source link); the resized copies are shared under the same terms.</p>
<table><tr><th>#</th><th>Place</th><th>Country</th><th>Author</th><th>License</th><th></th></tr>
{rows}
</table>""")
    open(os.path.join(a.out, ".nojekyll"), "w").close()
    print(f"site ready: {len(ok)} places in {a.out}")


if __name__ == "__main__":
    main()
