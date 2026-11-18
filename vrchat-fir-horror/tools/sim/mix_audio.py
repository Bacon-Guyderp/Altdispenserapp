#!/usr/bin/env python3
"""Mixes the sounds the simulated player heard (sounds.json: [time, clip]) over a quiet drone, then muxes
them with the rendered frames into an MP4. Usage: mix_audio.py <framesDir> <audioDir> <out.mp4>"""
import json, subprocess, sys, os
frames, audio, out = sys.argv[1], sys.argv[2], sys.argv[3]
sounds = json.load(open(os.path.join(frames, 'sounds.json')))
n = len([f for f in os.listdir(frames) if f.endswith('.png')])
dur = n / 30.0
args = ['ffmpeg', '-y', '-loglevel', 'error', '-framerate', '30', '-i', os.path.join(frames, 'f%05d.png'),
        '-stream_loop', '-1', '-i', os.path.join(audio, 'amb_drone.ogg')]
filt, labels = ['[1:a]volume=0.25,atrim=0:%.2f[bg]' % dur], ['[bg]']
for k, (t, clip) in enumerate(sounds):
    path = os.path.join(audio, clip + '.ogg')
    if not os.path.exists(path): continue
    args += ['-i', path]
    idx = len(labels) + 1
    ms = int(t * 1000)
    filt.append('[%d:a]adelay=%d|%d,volume=1.0[s%d]' % (idx, ms, ms, k))
    labels.append('[s%d]' % k)
filt.append(''.join(labels) + 'amix=inputs=%d:normalize=0,atrim=0:%.2f[a]' % (len(labels), dur))
args += ['-filter_complex', ';'.join(filt), '-map', '0:v', '-map', '[a]', '-c:v', 'libx264', '-pix_fmt', 'yuv420p',
         '-crf', '20', '-preset', 'medium', '-c:a', 'aac', '-b:a', '160k', '-movflags', '+faststart', '-shortest', out]
subprocess.run(args, check=True)
print('wrote', out, '(%.0f s)' % dur)
