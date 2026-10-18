"""Deterministic non-musical recovery-case Foley, synchronized to the 1.08 s lid animation.
Uses the two checked-in Kenney CC0 impacts plus original filtered pressure/friction noise.
Does not touch the separately approved rare-item pickup sound.
"""
from pathlib import Path
import json
import wave
import numpy as np

ROOT = Path(__file__).resolve().parents[2]
RATE = 48000
LENGTH = 1.16
rng = np.random.default_rng(20261018)
mix = np.zeros((int(RATE * LENGTH), 2), dtype=np.float64)

def read(name):
    with wave.open(str(ROOT / 'tools/audio/sources/kenney-impact' / name), 'rb') as source:
        assert source.getsampwidth() == 2
        a = np.frombuffer(source.readframes(source.getnframes()), '<i2').astype(float) / 32768
        a = a.reshape(-1, source.getnchannels()).mean(axis=1)
        return np.interp(np.arange(round(len(a) * RATE / source.getframerate())) * source.getframerate() / RATE, np.arange(len(a)), a)

def band(a, low, high):
    freq = np.fft.rfftfreq(len(a), 1 / RATE)
    response = (1 - np.exp(-(freq / low) ** 2)) * np.exp(-(freq / high) ** 4)
    return np.fft.irfft(np.fft.rfft(a) * response, n=len(a))

def add(a, start, gain, pan=0):
    offset = round(start * RATE)
    a = a[:len(mix)-offset]
    mix[offset:offset+len(a), 0] += a * gain * (1 - pan * .18)
    mix[offset:offset+len(a), 1] += a * gain * (1 + pan * .18)

def contact(source, duration, low, high):
    a = source[:round(duration * RATE)].copy()
    a = band(a, low, high)
    t = np.arange(len(a)) / RATE
    a *= (1 - np.exp(-t * 4200)) * np.exp(-t / (duration * .22))
    a[-min(180, len(a)):] *= np.linspace(1, 0, min(180, len(a)))
    return a / max(np.max(np.abs(a)), 1e-8)

plate = read('impactPlate_medium_000.wav')
metal = read('impactMetal_heavy_000.wav')
add(contact(plate, .095, 650, 10800), .025, .78, -.5)
add(contact(plate, .075, 950, 9000)[::-1] * np.linspace(1, 0, round(.075*RATE)), .108, .30, .5)
add(contact(metal, .14, 65, 1500), .125, .54)
for start, duration, gain, low, high in [(.17,.30,.13,650,7500),(.31,.59,.065,180,2800)]:
    t = np.arange(round(duration*RATE)) / RATE
    a = band(rng.normal(0,1,len(t)),low,high)
    envelope = np.sin(np.pi*t/duration) ** 1.4
    # Broadband sliding friction, not a sustained pitched motor tone.
    if start > .2: envelope *= .8 + .2*np.sin(2*np.pi*19*t)
    add(a * envelope, start, gain)
add(contact(metal, .16, 60, 2800), .94, .74)
add(contact(plate, .06, 1500, 9500), .965, .17)
mix *= (10 ** (-1.2/20)) / max(np.max(np.abs(mix)), 1e-8)
mix[-480:] *= np.linspace(1,0,480)[:,None]
out = ROOT / 'Assets/Resources/GameAudio/SFX/recovery_case_open.wav'
out.parent.mkdir(parents=True,exist_ok=True)
with wave.open(str(out),'wb') as target:
    target.setnchannels(2); target.setsampwidth(2); target.setframerate(RATE)
    target.writeframes(np.rint(np.clip(mix,-1,1)*32767).astype('<i2').tobytes())
print(json.dumps({'file': str(out), 'seconds': LENGTH, 'rate': RATE,
    'peak_dbfs': float(20*np.log10(np.max(np.abs(mix)))),
    'rms_dbfs': float(20*np.log10(np.sqrt(np.mean(mix*mix)))), 'clipped_samples': int((np.abs(mix)>=1).sum())},indent=2))
