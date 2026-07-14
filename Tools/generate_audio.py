#!/usr/bin/env python3
"""Procedural placeholder audio for the vertical slice (stdlib only — no deps).

Generates the SFX one-shots the slice glue plays by clipId (pickup, battle start,
grass footstep, UI click) and two ambient music loops for the zone playlists,
as 22.05 kHz 16-bit mono WAVs under Assets/_Game/Audio/{SFX,Music}.

Deterministic; re-run to regenerate identical files:
    python3 Tools/generate_audio.py
"""
import math
import os
import random
import struct
import wave

RATE = 22050
ROOT = os.path.join(os.path.dirname(__file__), "..", "Assets", "_Game", "Audio")


def write_wav(rel_path, samples):
    path = os.path.normpath(os.path.join(ROOT, rel_path))
    os.makedirs(os.path.dirname(path), exist_ok=True)
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        frames = b"".join(
            struct.pack("<h", max(-32767, min(32767, int(s * 32767)))) for s in samples
        )
        w.writeframes(frames)
    print(f"WROTE {path}  ({len(samples) / RATE:.2f}s)")


def env(i, n, attack=0.01, release=0.3):
    """Simple attack/release envelope over n samples."""
    t = i / RATE
    total = n / RATE
    a = min(1.0, t / attack) if attack > 0 else 1.0
    r = min(1.0, (total - t) / release) if release > 0 else 1.0
    return a * min(a, r)


def tone(freq, dur, vol=0.5, harmonics=((1, 1.0),), attack=0.01, release=0.1):
    n = int(dur * RATE)
    out = []
    for i in range(n):
        t = i / RATE
        s = sum(a * math.sin(math.tau * freq * h * t) for h, a in harmonics)
        out.append(vol * env(i, n, attack, release) * s)
    return out


def mix(*layers):
    n = max(len(l) for l in layers)
    return [sum(l[i] for l in layers if i < len(l)) for i in range(n)]


def concat(*parts):
    out = []
    for p in parts:
        out.extend(p)
    return out


# ── SFX ──────────────────────────────────────────────────────────────────────

def sfx_pickup():
    # two quick rising blips (E5 → B5)
    return concat(
        tone(659.3, 0.09, vol=0.45, harmonics=((1, 1.0), (2, 0.3)), release=0.05),
        tone(987.8, 0.12, vol=0.45, harmonics=((1, 1.0), (2, 0.3)), release=0.08),
    )


def sfx_battle():
    # dramatic low hit: detuned saw-ish stack + noise burst
    rng = random.Random(7)
    n = int(0.55 * RATE)
    out = []
    for i in range(n):
        t = i / RATE
        e = env(i, n, attack=0.005, release=0.45)
        s = 0.0
        for f in (110.0, 111.5, 220.0, 55.0):
            s += 0.22 * math.sin(math.tau * f * t) + 0.08 * math.sin(math.tau * f * 2 * t)
        s += 0.25 * (rng.random() * 2 - 1) * max(0.0, 1 - t * 6)  # attack noise
        out.append(0.8 * e * s)
    return out


def sfx_step_grass():
    # short filtered-noise scuff
    rng = random.Random(3)
    n = int(0.11 * RATE)
    out, prev = [], 0.0
    for i in range(n):
        white = rng.random() * 2 - 1
        prev = 0.75 * prev + 0.25 * white  # cheap low-pass
        out.append(0.5 * env(i, n, attack=0.004, release=0.08) * prev)
    return out


def sfx_ui_click():
    return tone(1760.0, 0.045, vol=0.3, harmonics=((1, 1.0),), attack=0.002, release=0.03)


# ── music (ambient pad loops; chord per 3 seconds, loopable end-to-start) ────

def pad_loop(chords, seconds_per_chord=3.0, vol=0.16, seed=11):
    n_total = int(len(chords) * seconds_per_chord * RATE)
    out = [0.0] * n_total
    per = int(seconds_per_chord * RATE)
    for c, freqs in enumerate(chords):
        for i in range(per):
            gi = c * per + i
            t = gi / RATE
            # crossfade chords for a seamless pad (also wraps to loop cleanly)
            fade = min(1.0, i / (0.5 * RATE), (per - i) / (0.5 * RATE))
            s = sum(
                math.sin(math.tau * f * t) + 0.35 * math.sin(math.tau * f * 2 * t + 0.7)
                for f in freqs
            )
            out[gi] += vol * fade * s / len(freqs)
            # let each chord bleed into the next slot for overlap
            nxt = (gi + int(0.4 * RATE)) % n_total
            out[nxt] += 0.25 * vol * (1 - fade) * s / len(freqs)
    return out


A3, C4, E4, F3, G3, D4, B3, D3, Bb3, F4, A4, C5, G4 = (
    220.0, 261.63, 329.63, 174.61, 196.0, 293.66, 246.94,
    146.83, 233.08, 349.23, 440.0, 523.25, 392.0,
)


def mus_wildwood():
    # Am — F — C — G, airy and green
    return pad_loop([
        (A3, C4, E4), (F3, A3, C4), (C4, E4, G4), (G3, B3, D4),
    ])


def mus_outpost():
    # Dm — Bb — F — C, slower and warmer
    return pad_loop([
        (D3, F3 * 2, A3), (Bb3, D4, F4), (F3, A3, C4), (C4, E4, G4),
    ], seconds_per_chord=3.5, vol=0.15, seed=23)


if __name__ == "__main__":
    write_wav("SFX/SFX_Pickup.wav", sfx_pickup())
    write_wav("SFX/SFX_Battle.wav", sfx_battle())
    write_wav("SFX/SFX_Step_Grass.wav", sfx_step_grass())
    write_wav("SFX/SFX_UIClick.wav", sfx_ui_click())
    write_wav("Music/MUS_Wildwood.wav", mus_wildwood())
    write_wav("Music/MUS_Outpost.wav", mus_outpost())
    print("AUDIO_PACK_OK")
