#!/usr/bin/env python3
"""Build deterministic, non-generative WAV cues for dream music mechanisms."""

from __future__ import annotations

import argparse
import hashlib
import math
import struct
import wave
from pathlib import Path


SAMPLE_RATE = 22_050
OUTPUTS = (
    "music-room-ambience.wav",
    "music-metronome-72.wav",
    "music-tuning-forks-a3-c4-e4-a4.wav",
    "music-acoustic-wall.wav",
    "auditorium-master-white-noise.wav",
)


def clamp(value: float) -> float:
    return max(-1.0, min(1.0, value))


def wav_bytes(samples: list[float]) -> bytes:
    pcm = b"".join(struct.pack("<h", round(clamp(sample) * 32767)) for sample in samples)
    header = struct.pack(
        "<4sI4s4sIHHIIHH4sI",
        b"RIFF",
        36 + len(pcm),
        b"WAVE",
        b"fmt ",
        16,
        1,
        1,
        SAMPLE_RATE,
        SAMPLE_RATE * 2,
        2,
        16,
        b"data",
        len(pcm),
    )
    return header + pcm


def ambience() -> list[float]:
    duration = 12.0
    samples: list[float] = []
    noise_state = 0x31415926
    for index in range(round(duration * SAMPLE_RATE)):
        time = index / SAMPLE_RATE
        noise_state = (1664525 * noise_state + 1013904223) & 0xFFFFFFFF
        noise = ((noise_state >> 8) / 0xFFFFFF) * 2.0 - 1.0
        air = 0.018 * noise * (0.65 + 0.35 * math.sin(math.tau * 0.09 * time))
        room = (
            0.025 * math.sin(math.tau * 43.0 * time)
            + 0.018 * math.sin(math.tau * 57.0 * time + 0.7)
            + 0.010 * math.sin(math.tau * 87.0 * time + 1.6)
        )
        creak = 0.0
        for start in (2.7, 7.9, 10.6):
            local = time - start
            if 0.0 <= local <= 0.55:
                envelope = math.sin(math.pi * local / 0.55) ** 2
                creak += 0.045 * envelope * math.sin(
                    math.tau * (105.0 - 38.0 * local) * local
                )
        samples.append(air + room + creak)
    return samples


def metronome_72() -> list[float]:
    beat_seconds = 60.0 / 72.0
    duration = beat_seconds * 4
    samples = [0.0] * round(duration * SAMPLE_RATE)
    for beat in range(4):
        start = round(beat * beat_seconds * SAMPLE_RATE)
        click_duration = 0.095
        for offset in range(round(click_duration * SAMPLE_RATE)):
            time = offset / SAMPLE_RATE
            envelope = math.exp(-42.0 * time)
            click = 0.34 * envelope * (
                math.sin(math.tau * 1450.0 * time)
                + 0.35 * math.sin(math.tau * 730.0 * time)
            )
            if beat == 3:
                click += 0.16 * math.exp(-7.0 * time) * math.sin(math.tau * 55.0 * time)
            samples[start + offset] += click
    return samples


def tuning_forks() -> list[float]:
    frequencies = (220.0, 261.6256, 329.6276, 440.0)
    tone_seconds = 1.1
    gap_seconds = 0.18
    duration = len(frequencies) * (tone_seconds + gap_seconds)
    samples = [0.0] * round(duration * SAMPLE_RATE)
    for tone_index, frequency in enumerate(frequencies):
        start = round(tone_index * (tone_seconds + gap_seconds) * SAMPLE_RATE)
        for offset in range(round(tone_seconds * SAMPLE_RATE)):
            time = offset / SAMPLE_RATE
            attack = min(1.0, time / 0.012)
            envelope = attack * math.exp(-2.2 * time)
            value = 0.24 * envelope * (
                math.sin(math.tau * frequency * time)
                + 0.34 * math.sin(math.tau * frequency * 2.01 * time)
            )
            samples[start + offset] += value
    return samples


def acoustic_wall() -> list[float]:
    duration = 2.4
    frequencies = (220.0, 261.6256, 329.6276, 440.0)
    samples: list[float] = []
    for index in range(round(duration * SAMPLE_RATE)):
        time = index / SAMPLE_RATE
        envelope = min(1.0, time / 0.025) * math.exp(-1.45 * time)
        chord = sum(
            math.sin(math.tau * frequency * time + voice * 0.23)
            for voice, frequency in enumerate(frequencies)
        ) / len(frequencies)
        latch = 0.0
        if 0.62 <= time <= 0.78:
            local = time - 0.62
            latch = 0.18 * math.exp(-24.0 * local) * math.sin(math.tau * 920.0 * local)
        samples.append(0.38 * envelope * chord + latch)
    return samples


def master_white_noise() -> list[float]:
    duration = 8.0
    state = 0xA17D10
    samples: list[float] = []
    previous = 0.0
    for _ in range(round(duration * SAMPLE_RATE)):
        state = (1103515245 * state + 12345) & 0x7FFFFFFF
        raw = (state / 0x7FFFFFFF) * 2.0 - 1.0
        previous = 0.72 * previous + 0.28 * raw
        samples.append(previous * 0.105)
    return samples


def expected_files() -> dict[str, bytes]:
    builders = (ambience, metronome_72, tuning_forks, acoustic_wall, master_white_noise)
    return {name: wav_bytes(builder()) for name, builder in zip(OUTPUTS, builders)}


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("mode", choices=("build", "check"), nargs="?", default="build")
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    args = parser.parse_args()
    output_dir = args.root / "assets" / "generated" / "dream_escape" / "audio"
    expected = expected_files()
    if args.mode == "build":
        output_dir.mkdir(parents=True, exist_ok=True)
        for name, content in expected.items():
            (output_dir / name).write_bytes(content)
    else:
        mismatches = [
            name
            for name, content in expected.items()
            if not (output_dir / name).is_file()
            or (output_dir / name).read_bytes() != content
        ]
        if mismatches:
            raise SystemExit(f"Dream music audio mismatch: {', '.join(mismatches)}")
    digest = hashlib.sha256(b"".join(expected[name] for name in OUTPUTS)).hexdigest()
    print(
        "DREAM_MUSIC_AUDIO_CHECK_PASS "
        f"files={len(expected)} sampleRate={SAMPLE_RATE} sha256={digest} runtimeGeneration=false"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
