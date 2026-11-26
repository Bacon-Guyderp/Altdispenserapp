# Video Corruptor

A one-page web app that wrecks videos in funny ways and lets you download the result.
Everything runs in your browser, so your video is never uploaded anywhere.

**To use it:** open `index.html` in Chrome, Edge, Firefox or Safari. Double-clicking the file works, and so does hosting it anywhere static.

## Two ways to ruin a video

**🎨 Glitch Render** plays your video through a stack of effects and records the result (MP4 where the browser supports H.264 recording, otherwise WebM). The output always plays.

| Effect | What it does |
|---|---|
| Datamosh | Real block-matching motion estimation drags the *old* picture around with the *new* motion, the classic "missing keyframe" smear. It kicks in at scene cuts. |
| Melt | The picture drips down the screen in columns. |
| Block glitch | Strips of the frame jump sideways, sometimes inverted or hue-shifted. |
| VHS wobble | Wavy lines, tracking tears, scanlines, noise. |
| RGB split | Colour channels drift apart. |
| Deep fry | Pixelation, posterised colour, extreme saturation and contrast. |
| Colour chaos | Spinning hue, inverted flashes, swapped colour channels. |
| Stutter | Freezes and skipping-CD loops. |
| Bitcrush / Speed / Tape warble | Crunchy audio, chipmunk or demon pitch, and seasick wobble. |

There are presets (Datamosh Dream, Deep Fried, Cursed VHS, Meltdown, Chipmunk Panic, Surprise me) and a live preview.
Rendering runs in real time, so keep the tab visible.

**💾 Byte Bitrot** damages the file's actual bytes: random bytes, copied chunks, shuffled chunks or the letters "LOL".
With "Keep the file playable" on, it parses MP4/MOV (`mdat`) and AVI (`movi`) files and only damages the media data, so the file's index survives and the decoder produces glitches instead of refusing the file.
These files usually look best in VLC or mpv.
