// Assets/_Project/Scripts/OblastZero.Gameplay/Anomalies/BacklogMotes.cs
using UnityEngine;

namespace OblastZero.Gameplay.Anomalies
{
    /// <summary>
    /// The visible tell on a Backlog (ANM-Χ-21/BL): dust that hangs where it is instead of drifting.
    /// Builds its own <see cref="ParticleSystem"/> at runtime for the same reason
    /// <see cref="ScavengeDustField"/> does — a ParticleSystem serializes as several hundred lines of
    /// module YAML that the scene generator would have to emit and keep correct across Unity versions.
    ///
    /// <para><b>It runs from the first frame and never stops, and that is the whole point.</b> The bible
    /// makes the Backlog the one anomaly that is meant to be seen and avoided: "distorted air, hanging dust
    /// motes — skilled players identify and avoid it." An effect that only started once the player was
    /// already inside would convert a legible trap into a gotcha, and the trap only works as a decision if
    /// the player can see the cost before paying it.</para>
    ///
    /// <para><b>Why not reuse ScavengeDustField.</b> That component anchors itself to
    /// <c>Camera.main</c> every frame so the room's ambient dust is always resident around the player —
    /// correct for atmosphere, exactly wrong for a fixed volume, which would slide around the level with
    /// the camera and stop marking the thing it exists to mark.</para>
    /// </summary>
    [RequireComponent(typeof(ParticleSystem))]
    public class BacklogMotes : MonoBehaviour
    {
        [Tooltip("Unlit transparent particle material. Generated as M_Dust by tools/generate_scavenge_scene.py.")]
        [SerializeField] private Material moteMaterial;

        [Tooltip("Volume the motes fill. Should match the zone's trigger box.")]
        [SerializeField] private Vector3 boxSize = new Vector3(6f, 4f, 5.5f);

        [Tooltip("Motes resident in the volume. Dense enough to read as a haze from outside it.")]
        [SerializeField] private int moteCount = 260;

        [SerializeField] private Color tint = new Color(0.82f, 0.80f, 0.72f, 0.42f);
        [SerializeField] private float sizeMin = 0.03f;
        [SerializeField] private float sizeMax = 0.09f;

        private ParticleSystem _system;

        private void Awake()
        {
            _system = GetComponent<ParticleSystem>();
            if (_system == null) _system = gameObject.AddComponent<ParticleSystem>();
            Configure();
        }

        private void Configure()
        {
            var main = _system.main;
            main.loop = true;
            main.prewarm = true;              // the haze is already there on frame one, not in ten seconds
            main.playOnAwake = true;
            main.duration = 5f;

            // The motes do not expire. A finite lifetime makes them fade and respawn, which reads as
            // ordinary drifting dust — the precise impression this effect must not give. Effectively
            // infinite lifetime plus zero velocity is what "hanging motionless" actually is.
            main.startLifetime = new ParticleSystem.MinMaxCurve(9999f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0f);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin, sizeMax);
            main.startColor = tint;
            main.maxParticles = Mathf.Max(1, moteCount);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.gravityModifier = 0f;

            // A single burst rather than a rate: the population is fixed, so every mote is placed once
            // and then stays. A continuous rate against an infinite lifetime would fill to maxParticles
            // and then silently stop emitting, which works but hides the intent.
            var emission = _system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[]
            {
                new ParticleSystem.Burst(0f, (short)Mathf.Max(1, moteCount))
            });

            var shape = _system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = boxSize;

            var renderer = GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.alignment = ParticleSystemRenderSpace.View;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                if (moteMaterial != null) renderer.sharedMaterial = moteMaterial;
            }

            _system.Play(true);
        }

        /// <summary>
        /// Sets the volume the motes fill, in local units. Called by the zone so the haze always matches
        /// the trigger box rather than being two numbers that can drift apart in the level plan.
        /// </summary>
        public void SetVolume(Vector3 size)
        {
            boxSize = size;
            if (_system == null) return;
            var shape = _system.shape;
            shape.scale = size;
        }
    }
}
