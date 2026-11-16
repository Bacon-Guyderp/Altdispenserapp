# Jumpscare preview (no Unity needed)

`index.html` rebuilds the face scare in three.js. The tree mesh, face layout, scream animation and
lantern lighting are ported line for line from `FirWorldBuilder.cs`, `FirAnimatronic.cs`,
`FirGameManager.cs` and `FirLantern.shader`. `render.js` drives it frame by frame in headless
Chromium (Playwright).

```
cd tools/preview
npm pack three@0.160.0 && tar xzf three-0.160.0.tgz && cp package/build/three.module.min.js .
cp ../../Assets/FirHorror/Textures/{wallpaper,needles,wood_floor,ceiling,poster_warning_scrawl}.png .
python -m http.server 8765 &          # serve this folder
mkdir frames && node render.js frames index.html 4.6   # 138 frames at 30 fps
ffmpeg -framerate 30 -i frames/f%04d.png -i ../../Assets/FirHorror/Audio/scream1.ogg \
       -filter_complex "[1]adelay=2780|2780[a]" -map 0:v -map "[a]" -pix_fmt yuv420p preview.mp4
```
`closet.html` is the RED SKELETON closet easter egg (ported from `BuildSkeletonCloset` and `FirCloset.cs`):
`node render.js frames closet.html 4.9`.

If you change the tree, face, closet or lighting code in the world, change it here too.
