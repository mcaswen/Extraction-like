#!/usr/bin/env python3
"""Publish the approved 'precision lock, weighted' master without resynthesizing.

The user selected audition 03 on 2026-10-08. Preserve its PCM bytes exactly,
including quantization, rather than rebuilding the rejected pitched 1.95 s cue.
The checked-in master and Unity asset share the same Git LFS object.
"""

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import wave

import numpy as np


RATE = 48000
DURATION = .84
PLAYBACK = .85
MASTER = Path(__file__).resolve().parent / "sources" / "approved" / "precision_lock_weighted.wav"
APPROVED_SHA256 = "f6d684435787282a18c3dc3cb4bd669026a824bb9592297bba1cec6699ee2ba6"


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


def verify_master():
    data = MASTER.read_bytes()
    if data.startswith(b"version https://git-lfs.github.com/spec/v1"):
        raise ValueError("Approved master is an LFS pointer. Run git lfs pull first.")
    if hashlib.sha256(data).hexdigest() != APPROVED_SHA256:
        raise ValueError("Approved master does not match the user's selected audition.")


def build():
    verify_master()
    audio, rate = read_pcm(MASTER)
    if rate != RATE or audio.shape != (round(DURATION * RATE), 2):
        raise ValueError("Approved master format changed.")
    return audio


def publish(output):
    verify_master()
    output = Path(output)
    output.parent.mkdir(parents=True, exist_ok=True)
    if output.resolve() != MASTER.resolve():
        shutil.copyfile(MASTER, output)
    if hashlib.sha256(output.read_bytes()).hexdigest() != APPROVED_SHA256:
        raise ValueError("Published audio differs from the approved master.")


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
    build()  # Validate the master format as well as its identity before publishing.
    publish(args.output)
    delivered, rate = read_pcm(args.output)
    stats = measure(delivered, rate)
    # Signal quality, not a claim that these numbers validate artistic taste.
    assert stats["full_scale_samples"] == 0
    assert -3.85 < stats["true_peak_4x_dbfs"] < -3.65
    assert max(abs(v) for v in stats["dc_per_channel"]) < .001
    assert stats["stereo_correlation"] > .7
    assert stats["mono_fold_rms_change_db"] > -.5
    assert stats["tail_last_200ms_rms_dbfs"] < -42
    assert (delivered[0] == 0).all() and (delivered[-1] == 0).all()
    report = {"design": "厚实锁定 · 加重版 / User-approved audition 03, exact PCM master",
              "source": "https://kenney.nl/assets/impact-sounds", "license": "CC0-1.0",
              "not_a_recreation_of": "Delta Force / 三角洲行动",
              "audio": stats, "approved_sha256": APPROVED_SHA256,
              "output_sha256": hashlib.sha256(args.output.read_bytes()).hexdigest()}
    args.report.parent.mkdir(parents=True, exist_ok=True)
    args.report.write_text(json.dumps(report, indent=2, ensure_ascii=False) + "\n")
    if args.preview:
        write_pcm(args.preview, delivered * PLAYBACK)
    print(json.dumps(report, indent=2, ensure_ascii=False))


if __name__ == "__main__":
    main()
