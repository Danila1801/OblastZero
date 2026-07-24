// Assets/_Project/Scripts/Core/RunRng.cs
using System;

namespace OblastZero.Core
{
    /// <summary>
    /// Deterministic, save-safe RNG for a single run. ALL run randomness (event selection, loot rolls,
    /// success rolls, crew-death rolls, future expedition/mutant rolls) MUST flow through here rather than
    /// <c>UnityEngine.Random</c> or ad-hoc <c>System.Random</c>, so a run is fully reproducible from its seed
    /// and reproduces identically after a save/load.
    ///
    /// State is nothing but <see cref="RunData.rngSeed"/> + <see cref="RunData.rngStreamCounter"/>. Each draw
    /// mixes those two with a MurmurHash3-style finalizer and advances the counter, so the returned sequence
    /// is a pure function of (seed, counter). Because the counter is serialized with RunData, reloading
    /// mid-run continues the exact same stream.
    /// </summary>
    public sealed class RunRng
    {
        private readonly RunData _run;

        public RunRng(RunData run)
        {
            _run = run ?? throw new ArgumentNullException(nameof(run));
        }

        /// <summary>Next raw 32-bit value; advances the run's stream counter by one.</summary>
        public uint NextUInt()
        {
            uint value = Mix((uint)_run.rngSeed, (uint)_run.rngStreamCounter);
            _run.rngStreamCounter++;
            return value;
        }

        /// <summary>Uniform double in [0, 1).</summary>
        public double NextDouble() => NextUInt() / 4294967296.0; // 2^32

        /// <summary>Uniform float in [0, 1).</summary>
        public float NextFloat() => (float)NextDouble();

        /// <summary>Integer in [minInclusive, maxInclusive]. Returns min if the range is empty/inverted.</summary>
        public int NextInt(int minInclusive, int maxInclusive)
        {
            if (maxInclusive <= minInclusive) return minInclusive;
            long span = (long)maxInclusive - minInclusive + 1;
            return minInclusive + (int)(NextDouble() * span);
        }

        /// <summary>
        /// True with probability <paramref name="p"/>. p&lt;=0 is always false and p&gt;=1 is always true —
        /// both short-circuit without consuming a draw (still deterministic, just no wasted stream position).
        /// </summary>
        public bool Chance(float p)
        {
            if (p <= 0f) return false;
            if (p >= 1f) return true;
            return NextFloat() < p;
        }

        private static uint Mix(uint seed, uint counter)
        {
            unchecked
            {
                // Combine the two streams, then run MurmurHash3's 32-bit finalizer for a good avalanche so
                // adjacent counters produce well-separated outputs.
                uint h = seed + 0x9E3779B9u + (counter << 6) + (counter >> 2);
                h ^= counter;
                h ^= h >> 16;
                h *= 0x85EBCA6Bu;
                h ^= h >> 13;
                h *= 0xC2B2AE35u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
