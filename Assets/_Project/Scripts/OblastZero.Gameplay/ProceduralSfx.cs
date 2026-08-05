// Assets/_Project/Scripts/OblastZero.Gameplay/ProceduralSfx.cs
using UnityEngine;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Bakes every sound in the game as PCM at load time. There are no audio files in this project and
    /// there are not meant to be: <see cref="AudioManager"/> asks this class for a cue and gets back a
    /// finished <see cref="AudioClip"/>.
    ///
    /// <para><b>Why baked clips and not OnAudioFilterRead.</b> A filter callback runs on the audio
    /// thread, cannot allocate, cannot read <c>Time</c>, and needs one live AudioSource per voice — so
    /// overlapping one-shots, 3D positioning and pitch variation all have to be rebuilt by hand on the
    /// wrong side of a thread boundary. Baking the same synthesis into a clip once puts all of that back
    /// in Unity's hands: <c>PlayOneShot</c> mixes overlaps, <c>spatialBlend</c> does the 3D, and
    /// <c>pitch</c> does the variation. The synthesis code below is identical either way; only where it
    /// runs changes, and running it once at boot costs a few hundred milliseconds instead of a
    /// per-sample budget forever.</para>
    ///
    /// <para><b>Determinism.</b> Noise comes from a local xorshift seeded per cue, never from
    /// <see cref="UnityEngine.Random"/>. Two boots of the same build therefore produce bit-identical
    /// audio, which keeps the run-reproducibility rule in CLAUDE.md §6 honest and makes a regression in
    /// a sound something you can actually diff.</para>
    ///
    /// <para><b>Loop seams.</b> Tonal loops are sized to a whole number of periods of every partial they
    /// contain, so they wrap with no discontinuity by construction. Noise loops cannot be, so they are
    /// synthesized long and the tail is cross-faded back over the head (<see cref="SealLoop"/>).</para>
    /// </summary>
    public static class ProceduralSfx
    {
        /// <summary>Every clip is baked at this rate. 44.1 kHz so nothing needs resampling on output.</summary>
        public const int SAMPLE_RATE = 44100;

        /// <summary>Seconds of ambient/music loop. 8 s at 44.1 kHz is exactly 480 periods of 60 Hz.</summary>
        public const float LOOP_SECONDS = 8f;

        /// <summary>Seconds of the noise-loop tail that is folded back over the head to hide the seam.</summary>
        private const float LOOP_SEAM_SECONDS = 0.75f;

        private const float TAU = 6.2831853f;

        // ─── Public API ──────────────────────────────────────────────────────────────────

        /// <summary>
        /// Bakes one cue. Returns null for an unknown cue rather than throwing, so a caller that has
        /// drifted from the cue table degrades to silence instead of taking the boot down.
        /// </summary>
        public static AudioClip Build(string cue)
        {
            switch (cue)
            {
                case AudioManager.CUE_PICKUP_CRATE:     return PickupThud(cue);
                case AudioManager.CUE_PICKUP_METAL:     return PickupMetal(cue);
                case AudioManager.CUE_PICKUP_PAPER:     return PickupPaper(cue);
                case AudioManager.CUE_PICKUP_WEAPON:    return PickupWeapon(cue);
                case AudioManager.CUE_PICKUP_ARTIFACT:  return PickupArtifact(cue);
                case AudioManager.CUE_PICKUP_CREW:      return PickupCrew(cue);
                case AudioManager.CUE_PICKUP_REJECTED:  return PickupRejected(cue);
                case AudioManager.CUE_FOOTSTEP_CONCRETE: return FootstepConcrete(cue);
                case AudioManager.CUE_FOOTSTEP_METAL:   return FootstepMetal(cue);
                case AudioManager.CUE_UI_CLICK:         return UiClick(cue);
                case AudioManager.CUE_UI_HOVER:         return UiHover(cue);
                case AudioManager.CUE_EMISSION_WARN:    return EmissionSiren(cue, 400f, 1200f, 2.0f, 0f);
                case AudioManager.CUE_EMISSION_CRITICAL: return EmissionSiren(cue, 520f, 1650f, 0.9f, 7f);
                case AudioManager.CUE_EMISSION_HIT:     return EmissionHit(cue);
                case AudioManager.CUE_DAY_ADVANCE:      return DayAdvance(cue);
                case AudioManager.CUE_EVENT_OPEN:       return EventChime(cue, 196f, 262f, 0.22f);
                case AudioManager.CUE_EVENT_CLOSE:      return EventChime(cue, 262f, 175f, 0.20f);
                case AudioManager.CUE_VICTORY:          return VictoryStinger(cue);
                case AudioManager.CUE_DEFEAT:           return DefeatStinger(cue);
                case AudioManager.CUE_AMBIENT_SCAVENGE: return AmbientScavenge(cue);
                case AudioManager.CUE_AMBIENT_BUNKER:   return AmbientBunker(cue);
                case AudioManager.CUE_MUSIC_BUNKER:     return BunkerDrone(cue);
                default:
                    Debug.LogWarning($"[ProceduralSfx] No generator for cue '{cue}'.");
                    return null;
            }
        }

        /// <summary>True when this cue is meant to be played on a looping source.</summary>
        public static bool IsLoop(string cue)
        {
            return cue == AudioManager.CUE_AMBIENT_SCAVENGE
                || cue == AudioManager.CUE_AMBIENT_BUNKER
                || cue == AudioManager.CUE_MUSIC_BUNKER
                || cue == AudioManager.CUE_EMISSION_WARN
                || cue == AudioManager.CUE_EMISSION_CRITICAL;
        }

        // ─── Pickups ─────────────────────────────────────────────────────────────────────

        /// <summary>Wooden crate: broadband knock through a low resonator. Body, no ring.</summary>
        private static AudioClip PickupThud(string cue)
        {
            var rng = new Noise(cue);
            float[] s = new float[Samples(0.16f)];
            var body = new Resonator(178f, 0.045f);   // dull, dead timber
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float strike = rng.White() * Decay(t, 0.004f, 0.030f);
                s[i] = body.Process(strike) * 0.9f + strike * 0.25f;
            }
            return Finish(cue, s, 0.62f);
        }

        /// <summary>Tin can / metal box: two detuned partials over a short scrape.</summary>
        private static AudioClip PickupMetal(string cue)
        {
            var rng = new Noise(cue);
            float[] s = new float[Samples(0.14f)];
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float env = Decay(t, 0.001f, 0.045f);
                float ring = Mathf.Sin(TAU * 2380f * t) * 0.6f
                           + Mathf.Sin(TAU * 3115f * t) * 0.35f
                           + Mathf.Sin(TAU * 4670f * t) * 0.15f;
                s[i] = ring * env + rng.White() * Decay(t, 0.0005f, 0.010f) * 0.35f;
            }
            return Finish(cue, s, 0.42f);
        }

        /// <summary>Paperwork: bandpassed noise with amplitude jitter, so it rustles rather than hisses.</summary>
        private static AudioClip PickupPaper(string cue)
        {
            var rng = new Noise(cue);
            float[] s = new float[Samples(0.20f)];
            var high = new OnePoleHigh(1400f);
            var low = new OnePoleLow(6200f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                // Two slow jitter terms at incommensurate rates: a single one reads as tremolo.
                float jitter = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(TAU * 31f * t) * Mathf.Sin(TAU * 17f * t));
                s[i] = low.Process(high.Process(rng.White())) * Decay(t, 0.010f, 0.060f) * jitter;
            }
            return Finish(cue, s, 0.34f);
        }

        /// <summary>Firearm: a heavier clack than the can — lower resonance, more body.</summary>
        private static AudioClip PickupWeapon(string cue)
        {
            var rng = new Noise(cue);
            float[] s = new float[Samples(0.17f)];
            var body = new Resonator(318f, 0.055f);   // the frame taking the weight
            var click = new Resonator(1720f, 0.090f); // the working part, ringing twice as long
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float strike = rng.White() * Decay(t, 0.001f, 0.018f);
                s[i] = body.Process(strike) * 0.8f + click.Process(strike) * 0.45f;
            }
            return Finish(cue, s, 0.55f);
        }

        /// <summary>Artifact: a rising fifth that keeps ringing. The only sound in the depot that is not tired.</summary>
        private static AudioClip PickupArtifact(string cue)
        {
            float[] s = new float[Samples(0.55f)];
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float k = t / 0.55f;
                float root = Mathf.Lerp(620f, 1180f, k * k);   // squared so the sweep accelerates
                float env = Decay(t, 0.030f, 0.240f);
                s[i] = (Mathf.Sin(TAU * root * t) * 0.6f
                      + Mathf.Sin(TAU * root * 1.5f * t) * 0.3f
                      + Mathf.Sin(TAU * root * 2.02f * t) * 0.12f) * env;
            }
            return Finish(cue, s, 0.40f);
        }

        /// <summary>Crew rescue: a low sustained tone under a breath. Relief, not triumph.</summary>
        private static AudioClip PickupCrew(string cue)
        {
            var rng = new Noise(cue);
            float[] s = new float[Samples(0.45f)];
            var breath = new OnePoleLow(900f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float env = Decay(t, 0.040f, 0.180f);
                float tone = Mathf.Sin(TAU * 174f * t) * 0.55f + Mathf.Sin(TAU * 261f * t) * 0.25f;
                s[i] = tone * env + breath.Process(rng.White()) * Decay(t, 0.060f, 0.120f) * 0.30f;
            }
            return Finish(cue, s, 0.44f);
        }

        /// <summary>Refusal: two dull descending knocks. The pack is full; the Oblast files a form.</summary>
        private static AudioClip PickupRejected(string cue)
        {
            var rng = new Noise(cue);
            float[] s = new float[Samples(0.30f)];
            var first = new Resonator(132f, 0.040f);
            var second = new Resonator(104f, 0.040f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float a = rng.White() * Decay(t, 0.002f, 0.026f);
                float b = rng.White() * Decay(t - 0.095f, 0.002f, 0.030f);
                s[i] = first.Process(a) * 0.85f + second.Process(b) * 0.85f;
            }
            return Finish(cue, s, 0.58f);
        }

        // ─── Footsteps ───────────────────────────────────────────────────────────────────

        /// <summary>Concrete: noise through a lowpass swept 2 kHz → 200 Hz across 90 ms.</summary>
        private static AudioClip FootstepConcrete(string cue)
        {
            var rng = new Noise(cue);
            int n = Samples(0.09f);
            float[] s = new float[n];
            var low = new OnePoleLow(2000f);
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                low.SetCutoff(Mathf.Lerp(2000f, 200f, i / (float)n));
                s[i] = low.Process(rng.White()) * Decay(t, 0.002f, 0.028f);
            }
            return Finish(cue, s, 0.30f);
        }

        /// <summary>Steel walkway or dock plate: the same strike plus a mid ring.</summary>
        private static AudioClip FootstepMetal(string cue)
        {
            var rng = new Noise(cue);
            int n = Samples(0.12f);
            float[] s = new float[n];
            var low = new OnePoleLow(3600f);
            var ring = new Resonator(915f, 0.070f);   // a dock plate under a boot
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                low.SetCutoff(Mathf.Lerp(3600f, 420f, i / (float)n));
                float strike = low.Process(rng.White()) * Decay(t, 0.001f, 0.024f);
                s[i] = strike * 0.75f + ring.Process(strike) * 0.55f;
            }
            return Finish(cue, s, 0.28f);
        }

        // ─── UI ──────────────────────────────────────────────────────────────────────────

        private static AudioClip UiClick(string cue)
        {
            float[] s = new float[Samples(0.022f)];
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                s[i] = Mathf.Sin(TAU * 800f * t) * Decay(t, 0.0025f, 0.006f);
            }
            return Finish(cue, s, 0.30f);
        }

        private static AudioClip UiHover(string cue)
        {
            float[] s = new float[Samples(0.016f)];
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                s[i] = Mathf.Sin(TAU * 520f * t) * Decay(t, 0.002f, 0.005f);
            }
            return Finish(cue, s, 0.14f);
        }

        // ─── Emission ────────────────────────────────────────────────────────────────────

        /// <summary>
        /// A seamless siren: <paramref name="cycleSeconds"/> of sine sweep from <paramref name="lowHz"/>
        /// to <paramref name="highHz"/> and back. Phase is integrated rather than computed as
        /// <c>sin(2*pi*f(t)*t)</c>, which is the classic sweep bug — that expression's instantaneous
        /// frequency is <c>f + t*df/dt</c>, not <c>f</c>, so the pitch overshoots and the loop clicks.
        /// </summary>
        private static AudioClip EmissionSiren(string cue, float lowHz, float highHz,
                                               float cycleSeconds, float tremoloHz)
        {
            int n = Samples(cycleSeconds);
            float[] s = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float k = i / (float)n;
                // Triangle sweep up then down, so the end frequency equals the start frequency.
                float shape = k < 0.5f ? k * 2f : (1f - k) * 2f;
                float freq = Mathf.Lerp(lowHz, highHz, shape);
                phase += TAU * freq / SAMPLE_RATE;
                if (phase > TAU) phase -= TAU;

                float amp = 0.55f + 0.45f * shape;
                if (tremoloHz > 0f)
                    amp *= 0.65f + 0.35f * Mathf.Sin(TAU * tremoloHz * (i / (float)SAMPLE_RATE));

                // A little third harmonic keeps it from sounding like a test tone.
                s[i] = (Mathf.Sin(phase) * 0.8f + Mathf.Sin(phase * 3f) * 0.12f) * amp;
            }
            return Finish(cue, s, 0.34f);
        }

        /// <summary>The emission landing: sub-bass impact with a long noise tail.</summary>
        private static AudioClip EmissionHit(string cue)
        {
            var rng = new Noise(cue);
            float[] s = new float[Samples(1.8f)];
            var low = new OnePoleLow(140f);
            float phase = 0f;
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                // Pitch drops as the shockwave passes: 58 Hz down to 34 Hz.
                phase += TAU * Mathf.Lerp(58f, 34f, Mathf.Clamp01(t / 1.2f)) / SAMPLE_RATE;
                if (phase > TAU) phase -= TAU;
                float sub = Mathf.Sin(phase) * Decay(t, 0.006f, 0.520f);
                float roar = low.Process(rng.White()) * Decay(t, 0.020f, 0.700f);
                s[i] = sub * 0.85f + roar * 0.75f;
            }
            return Finish(cue, s, 0.95f);
        }

        // ─── Bunker ──────────────────────────────────────────────────────────────────────

        /// <summary>Day rolls over: a low rumble with a bell on top. Institutional, not heroic.</summary>
        private static AudioClip DayAdvance(string cue)
        {
            var rng = new Noise(cue);
            float[] s = new float[Samples(1.9f)];
            var low = new OnePoleLow(90f);
            for (int i = 0; i < s.Length; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float rumble = low.Process(rng.White()) * Decay(t, 0.080f, 0.560f);
                // Struck-bar partials (1 : 2.76 : 5.40) — a tubular bell, not a sine.
                float bellEnv = Decay(t - 0.060f, 0.004f, 0.620f);
                float bell = (Mathf.Sin(TAU * 233f * t) * 0.55f
                            + Mathf.Sin(TAU * 643f * t) * 0.22f
                            + Mathf.Sin(TAU * 1258f * t) * 0.10f) * bellEnv;
                s[i] = rumble * 0.8f + bell * 0.75f;
            }
            return Finish(cue, s, 0.52f);
        }

        private static AudioClip EventChime(string cue, float fromHz, float toHz, float seconds)
        {
            int n = Samples(seconds);
            float[] s = new float[n];
            float phase = 0f;
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                phase += TAU * Mathf.Lerp(fromHz, toHz, i / (float)n) / SAMPLE_RATE;
                if (phase > TAU) phase -= TAU;
                s[i] = (Mathf.Sin(phase) * 0.8f + Mathf.Sin(phase * 2f) * 0.16f)
                       * Decay(t, 0.008f, seconds * 0.32f);
            }
            return Finish(cue, s, 0.26f);
        }

        // ─── Stingers ────────────────────────────────────────────────────────────────────

        /// <summary>C–E–G–C ascending, sine plus harmonics, notes overlapping into a held chord.</summary>
        private static AudioClip VictoryStinger(string cue)
        {
            float[] notes = { 261.63f, 329.63f, 392.00f, 523.25f };
            float[] s = new float[Samples(3.0f)];
            for (int note = 0; note < notes.Length; note++)
            {
                float start = note * 0.34f;
                for (int i = 0; i < s.Length; i++)
                {
                    float t = i / (float)SAMPLE_RATE - start;
                    if (t < 0f) continue;
                    float env = Decay(t, 0.020f, note == notes.Length - 1 ? 1.10f : 0.62f);
                    float f = notes[note];
                    s[i] += (Mathf.Sin(TAU * f * t) * 0.55f
                           + Mathf.Sin(TAU * f * 2f * t) * 0.18f
                           + Mathf.Sin(TAU * f * 3f * t) * 0.07f) * env * 0.5f;
                }
            }
            return Finish(cue, s, 0.62f);
        }

        /// <summary>A–F–D descending, soft-clipped so it reads as damaged equipment rather than as a fanfare.</summary>
        private static AudioClip DefeatStinger(string cue)
        {
            float[] notes = { 220.00f, 174.61f, 146.83f };
            float[] s = new float[Samples(4.0f)];
            for (int note = 0; note < notes.Length; note++)
            {
                float start = note * 0.52f;
                for (int i = 0; i < s.Length; i++)
                {
                    float t = i / (float)SAMPLE_RATE - start;
                    if (t < 0f) continue;
                    float env = Decay(t, 0.030f, note == notes.Length - 1 ? 1.80f : 0.95f);
                    float f = notes[note];
                    float raw = Mathf.Sin(TAU * f * t) * 0.7f
                              + Mathf.Sin(TAU * f * 2f * t) * 0.22f
                              + Mathf.Sin(TAU * f * 2.94f * t) * 0.14f;   // detuned partial = sour
                    s[i] += SoftClip(raw * 2.1f) * env * 0.42f;
                }
            }
            return Finish(cue, s, 0.66f);
        }

        // ─── Ambient loops ───────────────────────────────────────────────────────────────

        /// <summary>
        /// Depot ambience: sub-bass wind bed with 40–80 Hz emphasis, a high air hiss, and periodic
        /// structural creaks placed at fixed offsets so the loop is deterministic.
        /// </summary>
        private static AudioClip AmbientScavenge(string cue)
        {
            var rng = new Noise(cue);
            int seam = Samples(LOOP_SEAM_SECONDS);
            int n = Samples(LOOP_SECONDS) + seam;
            float[] s = new float[n];

            var rumbleLow = new OnePoleLow(80f);
            var rumbleHigh = new OnePoleHigh(40f);
            var airLow = new OnePoleLow(5200f);
            var airHigh = new OnePoleHigh(2200f);

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float white = rng.White();
                float rumble = rumbleLow.Process(rumbleHigh.Process(white));
                // Slow swell so the wind breathes instead of sitting flat.
                float swell = 0.72f + 0.28f * Mathf.Sin(TAU * 0.125f * t);   // 8 s period = the loop
                float air = airLow.Process(airHigh.Process(white));
                s[i] = rumble * 3.4f * swell + air * 0.11f;
            }

            // Creaks: a resonant bar excited at four fixed points in the loop.
            float[] creakAt = { 1.35f, 3.10f, 4.85f, 6.60f };
            float[] creakHz = { 168f, 214f, 141f, 197f };
            for (int c = 0; c < creakAt.Length; c++)
            {
                var bar = new Resonator(creakHz[c], 0.260f);  // a long groan, not a knock
                var creakRng = new Noise(cue + "::creak" + c);
                int start = Samples(creakAt[c]);
                int len = Samples(0.62f);
                for (int i = 0; i < len && start + i < n; i++)
                {
                    float t = i / (float)SAMPLE_RATE;
                    // A slow-rising excitation makes wood groan; an impulse makes it knock.
                    float push = creakRng.White() * Mathf.Sin(Mathf.PI * (i / (float)len)) * 0.5f;
                    s[start + i] += bar.Process(push) * 0.85f;
                }
            }

            return Finish(cue, SealLoop(s, seam), 0.50f);
        }

        /// <summary>
        /// Bunker ambience: a 60 Hz generator fundamental with its harmonic stack, plus ventilation
        /// hiss. Every partial is an integer multiple of 60 Hz and the loop is a whole number of 60 Hz
        /// periods, so the tonal half wraps exactly; only the hiss needs the seam fold.
        /// </summary>
        private static AudioClip AmbientBunker(string cue)
        {
            var rng = new Noise(cue);
            int seam = Samples(LOOP_SEAM_SECONDS);
            int n = Samples(LOOP_SECONDS) + seam;
            float[] s = new float[n];

            var ventLow = new OnePoleLow(2600f);
            var ventHigh = new OnePoleHigh(600f);

            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float hum = Mathf.Sin(TAU * 60f * t) * 0.50f
                          + Mathf.Sin(TAU * 120f * t) * 0.26f
                          + Mathf.Sin(TAU * 180f * t) * 0.13f
                          + Mathf.Sin(TAU * 300f * t) * 0.05f;
                // 0.25 Hz wobble = 2 whole cycles across an 8 s loop, so this wraps too.
                float wobble = 0.88f + 0.12f * Mathf.Sin(TAU * 0.25f * t);
                float vent = ventLow.Process(ventHigh.Process(rng.White()));
                s[i] = hum * 0.42f * wobble + vent * 0.30f;
            }

            return Finish(cue, SealLoop(s, seam), 0.34f);
        }

        /// <summary>
        /// The bunker's music: a slow drone on A2 with a detuned fifth and a very slow beat between two
        /// nearly-identical partials. <see cref="AudioManager"/> transposes it with source pitch as the
        /// bunker's radiation pool climbs, so the room gets lower and more wrong over a long run without
        /// ever playing a second piece of music.
        /// </summary>
        private static AudioClip BunkerDrone(string cue)
        {
            int seam = Samples(LOOP_SEAM_SECONDS);
            int n = Samples(LOOP_SECONDS) + seam;
            float[] s = new float[n];
            for (int i = 0; i < n; i++)
            {
                float t = i / (float)SAMPLE_RATE;
                float drone = Mathf.Sin(TAU * 110.00f * t) * 0.42f      // A2
                            + Mathf.Sin(TAU * 110.75f * t) * 0.30f      // 0.75 Hz beat against it
                            + Mathf.Sin(TAU * 165.00f * t) * 0.20f      // fifth
                            + Mathf.Sin(TAU * 220.00f * t) * 0.10f      // octave
                            + Mathf.Sin(TAU * 55.00f * t) * 0.24f;      // sub
                float breath = 0.80f + 0.20f * Mathf.Sin(TAU * 0.125f * t);
                s[i] = drone * 0.55f * breath;
            }
            return Finish(cue, SealLoop(s, seam), 0.30f);
        }

        // ─── Synthesis helpers ───────────────────────────────────────────────────────────

        private static int Samples(float seconds)
        {
            return Mathf.Max(1, Mathf.RoundToInt(seconds * SAMPLE_RATE));
        }

        /// <summary>Attack/decay envelope. Returns 0 before t=0 so a delayed hit costs no branch upstream.</summary>
        private static float Decay(float t, float attackSeconds, float decaySeconds)
        {
            if (t < 0f) return 0f;
            if (t < attackSeconds) return attackSeconds <= 0f ? 1f : t / attackSeconds;
            return Mathf.Exp(-(t - attackSeconds) / Mathf.Max(0.0001f, decaySeconds));
        }

        /// <summary>tanh-ish saturation. Cheap, monotonic, and bounded to ±1.</summary>
        private static float SoftClip(float x)
        {
            return x / (1f + Mathf.Abs(x));
        }

        /// <summary>
        /// Folds the last <paramref name="seam"/> samples back over the first <paramref name="seam"/>
        /// with a linear cross-fade and returns the shortened buffer. The result loops without a click
        /// even though the source is noise.
        /// </summary>
        private static float[] SealLoop(float[] source, int seam)
        {
            int length = source.Length - seam;
            if (length <= 0 || seam <= 0) return source;

            float[] outp = new float[length];
            System.Array.Copy(source, outp, length);
            for (int i = 0; i < seam; i++)
            {
                float k = i / (float)seam;                 // 0 at the head, 1 by the end of the seam
                outp[i] = outp[i] * k + source[length + i] * (1f - k);
            }
            return outp;
        }

        /// <summary>
        /// Normalises to <paramref name="peak"/>, applies a short fade at both ends so a one-shot can
        /// never start or stop on a non-zero sample, and wraps the buffer in a mono AudioClip.
        /// </summary>
        private static AudioClip Finish(string cue, float[] samples, float peak)
        {
            float max = 0f;
            for (int i = 0; i < samples.Length; i++)
            {
                float a = samples[i] < 0f ? -samples[i] : samples[i];
                if (a > max) max = a;
            }
            float gain = max > 0.0001f ? peak / max : 0f;

            // Edge ramp, so no clip can begin or end on a non-zero sample.
            //
            // 3 ms rather than 1: a resonator with a 90 ms ring time is still at roughly 15% of peak
            // when a 170 ms buffer runs out, and cutting that off over a single millisecond is audible
            // as a tick on the end of every weapon and tool pickup. Capped at a tenth of the clip so
            // the 22 ms UI blip does not lose its attack to the ramp — its own envelope already has a
            // 2.5 ms attack, so the two are the same order and neither dominates.
            int edge = Mathf.Min(Samples(0.003f), samples.Length / 10);
            for (int i = 0; i < samples.Length; i++)
            {
                float ramp = 1f;
                if (i < edge) ramp = i / (float)edge;
                else if (i >= samples.Length - edge) ramp = (samples.Length - 1 - i) / (float)edge;
                samples[i] *= gain * ramp;
            }

            var clip = AudioClip.Create(cue, samples.Length, 1, SAMPLE_RATE, false);
            clip.SetData(samples, 0);
            return clip;
        }

        // ─── Deterministic noise + one-pole filters ──────────────────────────────────────

        /// <summary>
        /// xorshift32 white noise, seeded from the cue name. Deliberately not
        /// <see cref="UnityEngine.Random"/>: that draws from a shared global stream, so the audio
        /// baked at boot would depend on whatever else had rolled a number first.
        /// </summary>
        private struct Noise
        {
            private uint _state;

            public Noise(string seed)
            {
                uint h = 2166136261u;
                for (int i = 0; i < seed.Length; i++)
                {
                    h ^= (uint)seed[i];   // explicit: uint ^ char resolves through long and will not assign back
                    h *= 16777619u;
                }
                _state = h == 0u ? 1u : h;
            }

            /// <summary>Uniform in [-1, 1).</summary>
            public float White()
            {
                _state ^= _state << 13;
                _state ^= _state >> 17;
                _state ^= _state << 5;
                return (_state / 2147483648f) - 1f;
            }
        }

        /// <summary>One-pole lowpass. Cutoff is settable per sample so it can be swept.</summary>
        private struct OnePoleLow
        {
            private float _a;
            private float _z;

            public OnePoleLow(float cutoffHz)
            {
                _z = 0f;
                _a = 0f;
                SetCutoff(cutoffHz);
            }

            public void SetCutoff(float cutoffHz)
            {
                float x = Mathf.Exp(-TAU * Mathf.Clamp(cutoffHz, 10f, SAMPLE_RATE * 0.45f) / SAMPLE_RATE);
                _a = 1f - x;
            }

            public float Process(float input)
            {
                _z += _a * (input - _z);
                return _z;
            }
        }

        /// <summary>One-pole highpass, built as input minus its own lowpass.</summary>
        private struct OnePoleHigh
        {
            private OnePoleLow _low;

            public OnePoleHigh(float cutoffHz)
            {
                _low = new OnePoleLow(cutoffHz);
            }

            public float Process(float input)
            {
                return input - _low.Process(input);
            }
        }

        /// <summary>
        /// Two-pole resonant bandpass — the thing that turns a noise burst into a struck object.
        ///
        /// <para><b>The second parameter is a ring time in seconds, not a Q.</b> It was a Q, and that was
        /// a bug: the pole radius of a resonator derives from its <i>bandwidth</i>, and bandwidth is
        /// <c>freq / Q</c>, so a fixed Q gives a ring time that shortens as the centre frequency rises.
        /// A "Q 26 metal plate" at 1720 Hz therefore decayed in 22 ms while a "Q 4.5 dull crate" at
        /// 132 Hz rang for 51 ms — the bright object died faster than the dead one, the exact inverse of
        /// what a plate and a packing crate do. Taking the decay time directly and solving for r
        /// (<c>r = exp(-1 / (seconds * rate))</c>) makes each call site state the thing it actually
        /// cares about, and makes the ordering independent of pitch.</para>
        /// </summary>
        private struct Resonator
        {
            /// <summary>Ceiling on the ring time. Past this the pole radius rounds to 1 and the filter rings forever.</summary>
            private const float MaxRingSeconds = 4f;

            private readonly float _b1;
            private readonly float _b2;
            private readonly float _gain;
            private float _y1;
            private float _y2;

            public Resonator(float freqHz, float ringSeconds)
            {
                float seconds = Mathf.Clamp(ringSeconds, 0.001f, MaxRingSeconds);
                float r = Mathf.Exp(-1f / (seconds * SAMPLE_RATE));
                float theta = TAU * Mathf.Clamp(freqHz, 20f, SAMPLE_RATE * 0.45f) / SAMPLE_RATE;
                _b1 = 2f * r * Mathf.Cos(theta);
                _b2 = -r * r;
                _gain = (1f - r) * Mathf.Sqrt(1f + r * r - 2f * r * Mathf.Cos(2f * theta));
                _y1 = 0f;
                _y2 = 0f;
            }

            public float Process(float input)
            {
                float y = _gain * input + _b1 * _y1 + _b2 * _y2;
                _y2 = _y1;
                _y1 = y;
                return y;
            }
        }
    }
}
