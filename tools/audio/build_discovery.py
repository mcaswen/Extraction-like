#!/usr/bin/env python3
"""Reproducible ASTRA rare-item cue: CC0 metal texture + original sound design.

Requires numpy only. No input from earlier discovery mixes; rebuilding is idempotent.
The dry strike stays centred; a restrained stereo tail folds safely to mono.
This is an original rare-discovery treatment, not a recreation of Delta Force audio.
"""

import argparse
import hashlib
import json
from pathlib import Path
import wave

import numpy as np


RATE = 48000
DURATION = 1.95
PLAYBACK = .85
SOURCE_DIR = Path(__file__).resolve().parent / "sources" / "kenney-impact"


def read_pcm(path):
    with wave.open(str(path), "rb") as stream:
        if stream.getsampwidth() != 2:
            raise ValueError("Expected 16-bit PCM")
        rate, channels = stream.getframerate(), stream.getnchannels()
        audio = np.frombuffer(stream.readframes(stream.getnframes()), dtype="<i2")
    return audio.reshape(-1, channels).astype(np.float64) / 32768, rate


def write_pcm(path, audio, rate=RATE):
    path.parent.mkdir(parents=True, exist_ok=True)
    audio = np.asarray(audio)
    if audio.ndim == 1:
        audio = audio[:, None]
    if not np.isfinite(audio).all() or np.max(np.abs(audio)) >= 1:
        raise ValueError("Refusing to write clipped or non-finite audio")
    with wave.open(str(path), "wb") as stream:
        stream.setnchannels(audio.shape[1])
        stream.setsampwidth(2)
        stream.setframerate(rate)
        stream.writeframes(np.rint(audio * 32767).astype("<i2").tobytes())


def band(x, low, high):
    size = 1 << (2 * len(x) - 1).bit_length()
    f = np.fft.rfftfreq(size, 1 / RATE)
    window = (1 - 1 / (1 + (f / low) ** 4)) / (1 + (f / high) ** 6)
    return np.fft.irfft(np.fft.rfft(x, size) * window, size)[:len(x)]


def env(t, attack, decay):
    return -np.expm1(-t / attack) * np.exp(-t / decay)


def place(target, audio, at, gain=1):
    start = round(at * RATE)
    count = min(len(target) - start, len(audio))
    if count > 0:
        target[start:start + count] += audio[:count] * gain


def source(name, speed, low, high, decay):
    audio, rate = read_pcm(SOURCE_DIR / name)
    mono = audio.mean(axis=1)
    positions = np.arange(0, len(mono) - 1, rate / RATE * speed)
    x = np.interp(positions, np.arange(len(mono)), mono)
    x = band(x, low, high)
    x /= max(np.max(np.abs(x)), 1e-9)
    t = np.arange(len(x)) / RATE
    x *= env(t, .001, decay)
    x *= np.clip((len(x) / RATE - t) / .025, 0, 1)
    return x


def build():
    n = round(DURATION * RATE)
    t = np.arange(n) / RATE
    rng = np.random.default_rng(1042026)
    dry = np.zeros(n)
    # Immediate physical contact: a real short metal strike, not a kick drum.
    place(dry, source("impactMetal_heavy_000.wav", .84, 190, 4300, .085), .012, .32)
    plate = source("impactPlate_medium_000.wav", .76, 280, 3700, .30)
    place(dry, plate, .053, .18)

    # A brief gathering of energy leads to the main reveal at 70 ms.
    noise = band(rng.normal(size=n), 580, 4600)
    noise /= np.sqrt(np.mean(noise ** 2))
    gather = np.clip(t / .070, 0, 1) ** 1.6 * np.exp(-np.maximum(t - .07, 0) / .018)
    dry += noise * gather * .028

    u = np.maximum(t - .068, 0)
    active = (t >= .068).astype(float)
    # Stable low body with audible harmonics. Only a tiny pitch settling;
    # the previous wide downward bass sweep was too dominant.
    phase = 2 * np.pi * (110 * u + 11 * .022 * (1 - np.exp(-u / .022)))
    body = (.135 * np.sin(phase) + .055 * np.sin(2 * phase + .17)
            + .024 * np.sin(3 * phase + .30)) * env(u, .003, .19) * active
    dry += np.tanh(body * 1.15) / 1.15

    # One cohesive, harmonically related metal bloom: not a sequence of UI beeps.
    bloom = np.zeros(n)
    for f, gain, decay, onset in [
        (293.665, .042, .31, .068), (440.498, .033, .27, .070),
        (587.330, .190, .40, .071), (880.995, .155, .43, .078),
        (1174.660, .080, .39, .085), (1761.990, .033, .25, .094),
        (2349.320, .010, .16, .102),
    ]:
        local = np.maximum(t - onset, 0)
        fm = .63 * np.exp(-local / .036) * np.sin(2 * np.pi * f * 1.417 * local)
        fundamental = np.sin(2 * np.pi * f * local + fm)
        # Very small detune avoids sterile sine-wave tone without an out-of-tune chord.
        overtone = .12 * np.sin(2 * np.pi * f * 1.0017 * local + .36)
        bloom += gain * (fundamental + overtone) * env(local, .008, decay) * (t >= onset)
    dry += bloom

    # Subtle, later upper resonance identifies the reward; no sharp hiss or whistle.
    reveal = np.zeros(n)
    for f, gain in [(1174.66, .020), (1761.99, .014), (2349.32, .007)]:
        u = np.maximum(t - .18, 0)
        reveal += gain * np.sin(2 * np.pi * f * u) * env(u, .026, .37) * (t >= .18)
    dry += reveal

    # Dense short early reflections rather than a conspicuous echo. Independent
    # channels contain only quiet upper-band ambience; the core stays mono safe.
    send = band(.75 * bloom + .22 * dry, 330, 5300)
    stereo = np.column_stack((dry, dry))
    for channel in range(2):
        wet = np.zeros(n)
        for delay, gain in [(.031, .16), (.053, .12), (.083, .085), (.127, .060)]:
            place(wet, send, delay + channel * .003, gain)
        for delay in np.linspace(.14, .76, 48):
            jitter = rng.uniform(-.008, .008)
            gain = .024 * np.exp(-(delay - .14) / .20) * rng.uniform(.6, 1)
            place(wet, send, delay + jitter, gain)
        stereo[:, channel] += wet
        stereo[:, channel] = band(stereo[:, channel], 42, 7600)
    stereo -= np.mean(stereo, axis=0)
    stereo *= np.minimum(1, t / .002)[:, None]
    stereo *= np.minimum(1, (DURATION - t) / .20)[:, None]
    # Light, linked transient rounding. Leave intersample headroom after normalization.
    stereo = np.tanh(stereo * 1.25) / 1.25
    peak = true_peak(stereo)
    stereo *= 10 ** (-1.6 / 20) / max(peak, 1e-9)
    stereo[0] = stereo[-1] = 0
    return stereo


def true_peak(audio):
    # Four-times Fourier interpolation; output is band-limited below 8 kHz.
    return max(float(np.max(np.abs(np.fft.irfft(np.fft.rfft(audio[:, c]),
                   n=len(audio) * 4) * 4))) for c in range(audio.shape[1]))


def measure(audio, rate=RATE):
    db = lambda v: round(float(20 * np.log10(max(v, 1e-12))), 3)
    mono = audio.mean(axis=1)
    f = np.fft.rfftfreq(len(mono), 1 / rate)
    energy = abs(np.fft.rfft(mono)) ** 2
    result = {
        "duration_seconds": len(audio) / rate, "sample_rate": rate, "channels": audio.shape[1],
        "sample_peak_dbfs": db(np.max(abs(audio))), "true_peak_4x_dbfs": db(true_peak(audio)),
        "rms_dbfs": db(np.sqrt(np.mean(audio ** 2))), "playback_volume": PLAYBACK,
        "playback_rms_dbfs": db(np.sqrt(np.mean(audio ** 2)) * PLAYBACK),
        "first_400ms_rms_dbfs": db(np.sqrt(np.mean(audio[:int(rate * .4)] ** 2))),
        "tail_last_200ms_rms_dbfs": db(np.sqrt(np.mean(audio[-int(rate * .2):] ** 2))),
        "dc_per_channel": np.mean(audio, axis=0).tolist(),
        "stereo_correlation": float(np.corrcoef(audio.T)[0, 1]),
        "mono_fold_rms_change_db": db(np.sqrt(np.mean(mono ** 2))) - db(np.sqrt(np.mean(audio ** 2))),
        "full_scale_samples": int(np.sum(abs(audio) >= .9999)),
    }
    for lo, hi in [(20, 80), (80, 300), (300, 600), (600, 2500), (2500, 8000)]:
        result[f"energy_{lo}_{hi}hz"] = round(float(energy[(f >= lo) & (f < hi)].sum() / energy.sum()), 4)
    return result


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--report", required=True, type=Path)
    parser.add_argument("--preview", type=Path)
    args = parser.parse_args()
    audio = build()
    write_pcm(args.output, audio)
    delivered, rate = read_pcm(args.output)
    stats = measure(delivered, rate)
    # Signal quality, not a claim that these numbers validate artistic taste.
    assert stats["full_scale_samples"] == 0
    assert -1.65 < stats["true_peak_4x_dbfs"] < -1.5
    assert max(abs(v) for v in stats["dc_per_channel"]) < .001
    assert stats["stereo_correlation"] > .7
    assert stats["mono_fold_rms_change_db"] > -.5
    assert stats["tail_last_200ms_rms_dbfs"] < -42
    assert (delivered[0] == 0).all() and (delivered[-1] == 0).all()
    report = {"design": "Original ASTRA rare discovery; CC0 foley + synthesized resonant reveal",
              "source": "https://kenney.nl/assets/impact-sounds", "license": "CC0-1.0",
              "not_a_recreation_of": "Delta Force / 三角洲行动",
              "audio": stats, "output_sha256": hashlib.sha256(args.output.read_bytes()).hexdigest()}
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n")
    if args.preview:
        write_pcm(args.preview, delivered * PLAYBACK)
    print(json.dumps(report, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
