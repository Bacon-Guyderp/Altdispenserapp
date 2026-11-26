# Video Corruptor

A one-page web app that wrecks videos in funny ways and lets you download the result.
Everything runs in your browser, so your video is never uploaded anywhere.

**To use it:** open `index.html` in Chrome, Edge, Firefox or Safari. Double-clicking the file works, and so does hosting it anywhere static.

## Three ways to ruin a video

**🎨 Glitch Render** plays your video through a stack of effects and records the result (MP4 where the browser supports H.264 recording, otherwise WebM). The output always plays.

| Effect | What it does |
|---|---|
| Datamosh | Real block-matching motion estimation drags the *old* picture around with the *new* motion, the classic "missing keyframe" smear. It kicks in at scene cuts. |
| Mosh intensity | Sets how extreme the datamosh is: motion gets amplified and keeps its momentum, so the smear blooms and refuses to clear up. |
| Melt | The picture drips down the screen in columns. |
| Block glitch | Strips of the frame jump sideways, sometimes inverted or hue-shifted. |
| VHS wobble | Wavy lines, tracking tears, scanlines, noise. |
| RGB split | Colour channels drift apart. |
| Deep fry | Pixelation, posterised colour, extreme saturation and contrast. |
| Colour chaos | Spinning hue, inverted flashes, swapped colour channels. |
| Stutter | Freezes and skipping-CD loops. |
| Bitcrush / Speed / Tape warble | Crunchy audio, chipmunk or demon pitch, and seasick wobble. |

There are presets (Datamosh Dream, Deep Fried, Cursed VHS, Meltdown, Chipmunk Panic, Surprise me) and a live preview.
Rendering decodes and re-encodes the video with WebCodecs (via [Mediabunny](https://mediabunny.dev), loaded from jsDelivr only when you render) as fast as the computer can process frames, instead of playing it in real time. No frames get dropped, and the sound is processed offline to match.
Colour and pixel effects run as a WebGL shader on computers with a graphics card, and as optimised CPU loops otherwise.
Browsers without WebCodecs fall back to recording in real time.

**🌊 Datamosh** is the real thing. It finds the video track in an MP4/MOV (regular or fragmented), reads every frame's position and turns the keyframes into filler data that decoders skip. Frame sizes stay the same, so the file stays valid.
With no new full picture, the decoder keeps applying motion to the old one, which produces the melting smear.
The **Intensity** slider sets what share of keyframes get removed (the first is always kept), and **Extra chaos** also scrambles some motion frames.
It handles H.264 and H.265, but with H.265 most decoders fill the missing picture with grey, so H.264 gives the classic look.

**💾 Byte Bitrot** damages the file's actual bytes: random bytes, copied chunks, shuffled chunks or the letters "LOL".
With "Keep the file playable" on, it parses MP4/MOV (`mdat`) and AVI (`movi`) files and only damages the media data, so the file's index survives and the decoder produces glitches instead of refusing the file.
These files usually look best in VLC or mpv.
