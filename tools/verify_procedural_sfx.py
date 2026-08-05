#!/usr/bin/env python3
"""
Independent numerical check of the DSP primitives in ProceduralSfx.cs.

Why this exists: "it compiles" and "it makes a sound" are different claims, and with the Unity
bridge down there is no way to hear the second one. Every cue in the game is synthesized from four
primitives — a deterministic noise source, a one-pole lowpass, a two-pole resonator, and a loop-seam
cross-fade — and each has a specific way of failing that produces a clip which loads fine, plays
fine, and is inaudible or broken:

  * Resonator with a mistuned normalisation gain -> output near zero. Every pickup goes silent.
  * Resonator with pole radius >= 1            -> output diverges to infinity, then NaN. SetData
                                                  on a NaN buffer is accepted and plays as a click
                                                  or as nothing.
  * Noise mapped to the wrong range            -> a DC offset instead of noise. Reads as a thump.
  * Frequency sweep written as sin(2*pi*f(t)*t) -> instantaneous frequency is f + t*df/dt, so the
                                                  siren overshoots its top note and the loop clicks.
  * Loop cross-fade off by the seam length     -> an audible tick once per loop, forever.

Each primitive below is a line-by-line port of the C#. That is the point: if the port and the
original ever disagree, this file is wrong and should be re-derived from the C#, which is the
authority. Pure standard library — no numpy, so it runs anywhere the other tools do.

Usage:
    python tools/verify_procedural_sfx.py
"""

import math
import sys

SAMPLE_RATE = 44100
TAU = 6.2831853

failures, checks = [], 0


def check(label, ok, detail=""):
    global checks
    checks += 1
    print("  [%s] %s%s" % ("PASS" if ok else "FAIL", label, " — " + detail if detail else ""))
    if not ok:
        failures.append(label)
    return ok


# ─── Ports of the C# primitives ─────────────────────────────────────────────────────────

class Noise:
    """xorshift32 seeded by FNV-1a over the cue name, matching ProceduralSfx.Noise."""

    def __init__(self, seed):
        h = 2166136261
        for ch in seed:
            h ^= ord(ch)
            h = (h * 16777619) & 0xFFFFFFFF
        self.state = h if h != 0 else 1

    def white(self):
        s = self.state
        s ^= (s << 13) & 0xFFFFFFFF
        s ^= s >> 17
        s ^= (s << 5) & 0xFFFFFFFF
        self.state = s & 0xFFFFFFFF
        return (self.state / 2147483648.0) - 1.0


class OnePoleLow:
    def __init__(self, cutoff):
        self.z = 0.0
        self.a = 0.0
        self.set_cutoff(cutoff)

    def set_cutoff(self, cutoff):
        cutoff = max(10.0, min(cutoff, SAMPLE_RATE * 0.45))
        self.a = 1.0 - math.exp(-TAU * cutoff / SAMPLE_RATE)

    def process(self, x):
        self.z += self.a * (x - self.z)
        return self.z


class Resonator:
    """Second parameter is a ring time in SECONDS — see the note in the C# on why it is not a Q."""

    def __init__(self, freq, ring_seconds):
        seconds = max(0.001, min(ring_seconds, 4.0))
        self.r = math.exp(-1.0 / (seconds * SAMPLE_RATE))
        theta = TAU * max(20.0, min(freq, SAMPLE_RATE * 0.45)) / SAMPLE_RATE
        self.b1 = 2.0 * self.r * math.cos(theta)
        self.b2 = -self.r * self.r
        self.gain = (1.0 - self.r) * math.sqrt(
            1.0 + self.r * self.r - 2.0 * self.r * math.cos(2.0 * theta))
        self.y1 = 0.0
        self.y2 = 0.0

    def process(self, x):
        y = self.gain * x + self.b1 * self.y1 + self.b2 * self.y2
        self.y2 = self.y1
        self.y1 = y
        return y


def decay(t, attack, dec):
    if t < 0.0:
        return 0.0
    if t < attack:
        return 1.0 if attack <= 0.0 else t / attack
    return math.exp(-(t - attack) / max(0.0001, dec))


def seal_loop(source, seam):
    length = len(source) - seam
    if length <= 0 or seam <= 0:
        return source
    out = list(source[:length])
    for i in range(seam):
        k = i / float(seam)
        out[i] = out[i] * k + source[length + i] * (1.0 - k)
    return out


def finish(samples, peak):
    """Normalise + 3 ms edge ramp (capped at length/10), matching ProceduralSfx.Finish."""
    mx = max(abs(v) for v in samples) if samples else 0.0
    gain = peak / mx if mx > 0.0001 else 0.0
    edge = min(int(round(0.003 * SAMPLE_RATE)), len(samples) // 10)
    out = []
    for i, v in enumerate(samples):
        ramp = 1.0
        if i < edge:
            ramp = i / float(edge)
        elif i >= len(samples) - edge:
            ramp = (len(samples) - 1 - i) / float(edge)
        out.append(v * gain * ramp)
    return out


def rms(xs):
    return math.sqrt(sum(v * v for v in xs) / len(xs)) if xs else 0.0


def finite(xs):
    return all(not (math.isnan(v) or math.isinf(v)) for v in xs)


# ─── Checks ─────────────────────────────────────────────────────────────────────────────

def check_noise():
    print("\n=== 1. deterministic noise source ===")
    a = Noise("pickup_crate")
    b = Noise("pickup_crate")
    c = Noise("pickup_metal")

    sa = [a.white() for _ in range(20000)]
    sb = [b.white() for _ in range(20000)]
    sc = [c.white() for _ in range(20000)]

    check("same seed reproduces the same stream", sa == sb)
    check("different seeds diverge", sa != sc)
    check("range is within [-1, 1)", all(-1.0 <= v < 1.0 for v in sa),
          "min %.4f max %.4f" % (min(sa), max(sa)))
    # A broken xorshift or a wrong divisor shows up here as a DC offset, which is a thump, not noise.
    check("mean is near zero (no DC offset)", abs(sum(sa) / len(sa)) < 0.02,
          "mean %.5f" % (sum(sa) / len(sa)))
    check("RMS is that of uniform noise (~0.577)", 0.5 < rms(sa) < 0.65,
          "rms %.4f" % rms(sa))
    # xorshift32's period is 2^32-1; a stuck state would show as an immediately repeating short cycle.
    check("no short cycle in the first 20k samples", len(set(sa)) > 19000,
          "%d distinct of 20000" % len(set(sa)))


def check_resonator():
    print("\n=== 2. resonator (turns a noise burst into a struck object) ===")
    # Every (freq, ring_seconds) pair actually used by a cue in ProceduralSfx.cs.
    used = [(178.0, 0.045), (318.0, 0.055), (1720.0, 0.090), (132.0, 0.040), (104.0, 0.040),
            (915.0, 0.070), (168.0, 0.260), (214.0, 0.260), (141.0, 0.260), (197.0, 0.260)]

    stable = all(Resonator(f, q).r < 1.0 for f, q in used)
    check("every pole radius < 1 (filter cannot diverge)", stable,
          "max r %.6f" % max(Resonator(f, q).r for f, q in used))

    audible = True
    worst = None
    for f, q in used:
        res = Resonator(f, q)
        noise = Noise("test::%g::%g" % (f, q))
        out = [res.process(noise.white() * decay(i / float(SAMPLE_RATE), 0.002, 0.03))
               for i in range(int(0.3 * SAMPLE_RATE))]
        if not finite(out):
            audible = False
            worst = (f, q, "non-finite")
            break
        peak = max(abs(v) for v in out)
        # The bar is deliberately low: Finish() normalises every clip, so what matters is that the
        # resonator produces a signal at all rather than a level. Anything below 1e-4 would normalise
        # noise floor up to full scale and sound like hiss.
        if peak < 1e-4:
            audible = False
            worst = (f, q, "peak %.3e" % peak)
            break
    check("every tuning produces a finite, non-negligible signal", audible,
          "" if audible else "%g Hz / %gs -> %s" % worst)

    def ring_seconds(f, requested):
        """Measured time for an impulse response to fall to 1% of peak."""
        res = Resonator(f, requested)
        res.process(1.0)
        out = [res.process(0.0) for _ in range(SAMPLE_RATE)]
        pk = max(abs(v) for v in out) or 1.0
        for i in range(len(out) - 1, -1, -1):
            if abs(out[i]) > pk * 0.01:
                return i / float(SAMPLE_RATE)
        return 0.0

    # The regression this whole check exists for. The parameter used to be a Q, and pole radius
    # derives from bandwidth = freq/Q — so a fixed Q shortened the ring as pitch rose, and the
    # "metal plate" tuning decayed in 22 ms while the "dull crate" rang for 51 ms. Requesting a time
    # has to give an ordering that holds regardless of centre frequency.
    dull = ring_seconds(132.0, 0.040)
    bright = ring_seconds(1720.0, 0.090)
    check("a longer requested ring outlasts a shorter one, across a 13x pitch gap", bright > dull,
          "1720 Hz/0.090s rings %.0f ms vs 132 Hz/0.040s at %.0f ms" % (bright * 1000, dull * 1000))

    # And the requested time must actually be delivered: 1% of peak is ~4.6 time constants, so a
    # correct implementation lands near 4.6x the request.
    accurate = True
    for f, requested in ((178.0, 0.045), (915.0, 0.070), (1720.0, 0.090), (168.0, 0.260)):
        ratio = ring_seconds(f, requested) / requested
        if not 3.5 < ratio < 5.5:
            accurate = False
            worst = (f, requested, ratio)
            break
    check("measured decay matches the requested time constant", accurate,
          "" if accurate else "%g Hz / %gs measured %.2f time constants" % worst)

    # Pitch independence: the same requested time at wildly different frequencies must ring for the
    # same duration. This is precisely the property the Q formulation did not have.
    times = [ring_seconds(f, 0.080) for f in (110.0, 440.0, 1760.0, 5000.0)]
    spread = (max(times) - min(times)) / max(times)
    check("ring time is independent of centre frequency", spread < 0.05,
          "spread %.1f%% across 110-5000 Hz" % (spread * 100))


def check_lowpass():
    print("\n=== 3. one-pole lowpass (footstep sweep, ambient bands) ===")
    def response(cutoff, freq):
        flt = OnePoleLow(cutoff)
        n = int(0.5 * SAMPLE_RATE)
        out = [flt.process(math.sin(TAU * freq * i / SAMPLE_RATE)) for i in range(n)]
        return max(abs(v) for v in out[n // 2:])       # skip the transient

    passband = response(2000.0, 200.0)
    stopband = response(200.0, 4000.0)
    check("passes below cutoff", passband > 0.8, "gain %.3f at 200 Hz through a 2 kHz lowpass" % passband)
    check("attenuates above cutoff", stopband < 0.15,
          "gain %.4f at 4 kHz through a 200 Hz lowpass" % stopband)

    # The footstep sweeps the cutoff every sample; a sweep that is not monotonic in gain would
    # click rather than close.
    prev, monotonic = None, True
    for cutoff in (2000.0, 1500.0, 1000.0, 600.0, 300.0, 200.0):
        g = response(cutoff, 1200.0)
        if prev is not None and g > prev + 1e-6:
            monotonic = False
        prev = g
    check("gain at a fixed tone falls monotonically as cutoff sweeps down", monotonic)


def check_sweep_phase():
    print("\n=== 4. siren sweep (integrated phase, not sin(2*pi*f(t)*t)) ===")
    # Port of EmissionSiren's phase loop for the warning cue.
    low, high, cycle = 400.0, 1200.0, 2.0
    n = int(cycle * SAMPLE_RATE)
    phase = 0.0
    out, freqs = [], []
    for i in range(n):
        k = i / float(n)
        shape = k * 2.0 if k < 0.5 else (1.0 - k) * 2.0
        freq = low + (high - low) * shape
        freqs.append(freq)
        phase += TAU * freq / SAMPLE_RATE
        if phase > TAU:
            phase -= TAU
        out.append((math.sin(phase) * 0.8 + math.sin(phase * 3.0) * 0.12) * (0.55 + 0.45 * shape))

    check("finite output", finite(out))
    check("sweep peaks at the requested top note", abs(max(freqs) - high) < 1.0,
          "max %.1f Hz, wanted %.1f" % (max(freqs), high))
    check("sweep returns to the start note (loop wraps in pitch)",
          abs(freqs[0] - freqs[-1]) < (high - low) * 0.01,
          "start %.1f Hz, end %.1f Hz" % (freqs[0], freqs[-1]))

    # The naive formulation, for contrast: its instantaneous frequency overshoots badly, which is
    # exactly the bug the integrated form avoids. Asserted so the comparison cannot silently rot.
    naive_max = 0.0
    for i in range(1, n):
        k = i / float(n)
        shape = k * 2.0 if k < 0.5 else (1.0 - k) * 2.0
        f = low + (high - low) * shape
        t = i / float(SAMPLE_RATE)
        kp = (i - 1) / float(n)
        shapep = kp * 2.0 if kp < 0.5 else (1.0 - kp) * 2.0
        fp = low + (high - low) * shapep
        tp = (i - 1) / float(SAMPLE_RATE)
        inst = (f * t - fp * tp) * SAMPLE_RATE      # d(f*t)/dt
        naive_max = max(naive_max, abs(inst))
    check("naive sin(2*pi*f(t)*t) really would overshoot (control)", naive_max > high * 1.5,
          "naive instantaneous peak %.0f Hz vs intended %.0f" % (naive_max, high))


def check_loop_seam():
    print("\n=== 5. loop seam cross-fade ===")
    seam = int(0.75 * SAMPLE_RATE)
    body = int(8.0 * SAMPLE_RATE)

    noise = Noise("ambient_scavenge")
    low = OnePoleLow(80.0)
    raw = [low.process(noise.white()) * 3.4 for _ in range(body + seam)]

    sealed = seal_loop(raw, seam)
    check("sealed buffer is the intended length", len(sealed) == body,
          "%d samples = %.2f s" % (len(sealed), len(sealed) / float(SAMPLE_RATE)))
    check("finite", finite(sealed))
    check("still has signal after sealing", rms(sealed) > 1e-3, "rms %.5f" % rms(sealed))

    # The seam test: the step from the last sample back to the first must be no worse than a typical
    # sample-to-sample step inside the buffer. A bigger jump there is an audible tick, once per loop.
    wrap_step = abs(sealed[0] - sealed[-1])
    inner = [abs(sealed[i + 1] - sealed[i]) for i in range(0, len(sealed) - 1, 97)]
    inner_p99 = sorted(inner)[int(len(inner) * 0.99)]
    check("wrap discontinuity is within the normal sample-to-sample range",
          wrap_step <= inner_p99,
          "wrap %.6f vs 99th-percentile inner step %.6f" % (wrap_step, inner_p99))

    unsealed_wrap = abs(raw[0] - raw[body - 1])
    check("an unsealed loop really would tick (control)", unsealed_wrap > inner_p99,
          "unsealed wrap %.6f vs %.6f" % (unsealed_wrap, inner_p99))


def check_tonal_loop_periodicity():
    print("\n=== 6. tonal loops wrap by construction ===")
    loop_samples = int(8.0 * SAMPLE_RATE)
    check("8 s at 44100 Hz is a whole number of samples", loop_samples == 352800,
          "%d samples" % loop_samples)

    # Every partial in the bunker hum and the drone, plus their modulation rates.
    partials = [60.0, 120.0, 180.0, 300.0, 0.25,          # ambient_bunker
                110.0, 110.75, 165.0, 220.0, 55.0, 0.125]  # music_bunker + its breath
    non_integer = [f for f in partials
                   if abs(f * 8.0 - round(f * 8.0)) > 1e-9]
    check("every partial completes a whole number of cycles in 8 s", not non_integer,
          "" if not non_integer else "offenders: %s" % non_integer)


def check_finish_normalisation():
    print("\n=== 7. Finish(): normalisation and edge ramp ===")
    noise = Noise("pickup_crate")
    body = Resonator(178.0, 5.5)
    raw = []
    for i in range(int(0.16 * SAMPLE_RATE)):
        t = i / float(SAMPLE_RATE)
        strike = noise.white() * decay(t, 0.004, 0.030)
        raw.append(body.process(strike) * 0.9 + strike * 0.25)

    out = finish(raw, 0.62)
    check("finite", finite(out))
    peak = max(abs(v) for v in out)
    check("peak lands on the requested level", abs(peak - 0.62) < 0.02, "peak %.4f, wanted 0.62" % peak)
    check("never exceeds full scale", peak <= 1.0)
    check("starts at silence", abs(out[0]) < 1e-9)
    check("ends at silence", abs(out[-1]) < 1e-9)
    check("has audible content, not just ramps", rms(out) > 0.01, "rms %.4f" % rms(out))

    # A silent input must stay silent rather than normalising a zero buffer to full scale.
    silent = finish([0.0] * 1000, 0.62)
    check("a silent buffer normalises to silence, not to noise",
          max(abs(v) for v in silent) == 0.0)


def main():
    print("=== ProceduralSfx.cs DSP verification (independent port) ===")
    check_noise()
    check_resonator()
    check_lowpass()
    check_sweep_phase()
    check_loop_seam()
    check_tonal_loop_periodicity()
    check_finish_normalisation()

    print("\n" + "=" * 46)
    if failures:
        print("%d/%d checks passed" % (checks - len(failures), checks))
        for f in failures:
            print("  FAILED: " + f)
        return 1
    print("%d/%d checks passed" % (checks, checks))
    print("ALL GREEN")
    return 0


if __name__ == "__main__":
    sys.exit(main())
