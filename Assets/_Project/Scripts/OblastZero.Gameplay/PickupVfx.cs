// Assets/_Project/Scripts/OblastZero.Gameplay/PickupVfx.cs
using UnityEngine;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// The half-second of feedback that tells the player a grab landed: the object pops and shrinks out
    /// of existence, a burst of sparks leaves in its place, and a point light flashes once in the
    /// archetype's colour.
    ///
    /// <para><b>Ownership of the destroy.</b> <see cref="ScavengeController"/> used to call
    /// <c>Destroy(pickup.gameObject)</c> the instant the managers accepted the item. It now hands the
    /// object to <see cref="Play"/> instead, and this component destroys it when the animation finishes.
    /// That ordering matters: the pickup is already committed to <c>RunData</c> before the visual starts,
    /// so nothing about the run depends on the animation completing — a scene unload mid-pop costs the
    /// player nothing. The collider and the <see cref="ScavengePickup"/> are stripped on the first frame,
    /// so the shrinking husk cannot be picked up twice or steal the crosshair.</para>
    ///
    /// <para><b>Why the particles and light are built here and not in the scene.</b> Same reason as
    /// <see cref="ScavengeDustField"/>: Scavenge.unity is generated (CLAUDE.md §14) and a serialized
    /// ParticleSystem is a few hundred lines of version-sensitive module YAML per pickup. Building one
    /// on demand costs an allocation on a frame the player just spent pressing E.</para>
    /// </summary>
    public class PickupVfx : MonoBehaviour
    {
        private const float PopSeconds = 0.20f;
        private const float PopPeakScale = 1.20f;
        private const float LightSeconds = 0.15f;
        private const float LightPeakIntensity = 3f;

        private Transform _target;
        private Vector3 _baseScale;
        private Light _flash;
        private float _elapsed;

        /// <summary>
        /// Starts the pop on <paramref name="pickup"/> and takes over responsibility for destroying it.
        /// Safe to call with a null pickup.
        /// </summary>
        public static void Play(GameObject pickup, VisualArchetype archetype)
        {
            if (pickup == null) return;

            Color tint = ColorFor(archetype);
            Vector3 at = pickup.transform.position;

            // The burst and the light are parented to nothing, so they outlive the husk and finish
            // playing where the object was rather than following a transform that is being scaled to 0.
            SpawnBurst(at, tint);

            var vfx = pickup.AddComponent<PickupVfx>();
            vfx.Begin(pickup.transform, tint);
        }

        /// <summary>The refusal shake: the object is NOT taken, so nothing is destroyed.</summary>
        public static void PlayRefusal(GameObject pickup)
        {
            if (pickup == null) return;
            var shake = pickup.GetComponent<PickupRefusalShake>();
            if (shake == null) shake = pickup.AddComponent<PickupRefusalShake>();
            shake.Restart();
        }

        private void Begin(Transform target, Color tint)
        {
            _target = target;
            _baseScale = target.localScale;
            _elapsed = 0f;

            // Retire the object as an interactable immediately — it is already in RunData.
            var pickup = GetComponent<ScavengePickup>();
            if (pickup != null) pickup.enabled = false;
            var colliders = GetComponents<Collider>();
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;
            var highlight = GetComponent<PickupHoverHighlight>();
            if (highlight != null)
            {
                highlight.OnHoverEnd();
                highlight.enabled = false;
            }

            var lightGo = new GameObject("Pickup_Flash");
            lightGo.transform.position = target.position;
            _flash = lightGo.AddComponent<Light>();
            _flash.type = LightType.Point;
            _flash.color = tint;
            _flash.range = 4.5f;
            _flash.intensity = 0f;
            _flash.shadows = LightShadows.None;
        }

        private void Update()
        {
            _elapsed += Time.deltaTime;

            if (_flash != null)
            {
                // Triangle ramp: 0 up to peak at the halfway point, back to 0.
                float k = Mathf.Clamp01(_elapsed / LightSeconds);
                float shape = k < 0.5f ? k * 2f : (1f - k) * 2f;
                _flash.intensity = shape * LightPeakIntensity;
                if (_elapsed >= LightSeconds) Destroy(_flash.gameObject);
            }

            if (_target != null)
            {
                float k = Mathf.Clamp01(_elapsed / PopSeconds);
                // Overshoot to PopPeakScale by a third of the way in, then collapse to nothing.
                float scale = k < 0.33f
                    ? Mathf.Lerp(1f, PopPeakScale, k / 0.33f)
                    : Mathf.Lerp(PopPeakScale, 0f, (k - 0.33f) / 0.67f);
                _target.localScale = _baseScale * scale;
            }

            if (_elapsed >= PopSeconds) Destroy(gameObject);
        }

        private void OnDestroy()
        {
            // The husk can be destroyed by a scene unload before the pop finishes; do not leak the light.
            if (_flash != null) Destroy(_flash.gameObject);
        }

        /// <summary>
        /// A short-lived particle burst. Uses the same URP particle material the dust field does, loaded
        /// off the dust field in the scene so there is exactly one particle material to keep in the build.
        /// </summary>
        private static void SpawnBurst(Vector3 position, Color tint)
        {
            var go = new GameObject("Pickup_Burst");
            go.transform.position = position;

            var system = go.AddComponent<ParticleSystem>();

            var main = system.main;
            main.loop = false;
            main.duration = 0.9f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.75f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = tint;
            main.gravityModifier = 0.55f;
            main.maxParticles = 16;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.Destroy;

            var emission = system.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            // Explicit shorts: Burst has both (float, short, short) and (float, MinMaxCurve) overloads,
            // and letting int literals pick between them is not worth the ambiguity.
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)8, (short)12) });

            var shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 34f;
            shape.radius = 0.06f;
            shape.rotation = new Vector3(-90f, 0f, 0f);   // cone opens upward

            var fade = system.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            if (renderer != null)
            {
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                var material = ResolveParticleMaterial();
                if (material != null) renderer.sharedMaterial = material;
            }

            system.Play();
        }

        private static Material _cachedParticleMaterial;

        /// <summary>
        /// Borrows the dust field's material. It is the one transparent particle material this project
        /// ships, and it is referenced by the scene, so it is guaranteed to be in the build — unlike a
        /// <c>Shader.Find</c>, which returns null for any shader the build has stripped and would leave
        /// the burst rendering as magenta error quads in a player but fine in the Editor.
        /// </summary>
        private static Material ResolveParticleMaterial()
        {
            if (_cachedParticleMaterial != null) return _cachedParticleMaterial;

            var field = Object.FindAnyObjectByType<ScavengeDustField>(FindObjectsInactive.Include);
            var renderer = field != null ? field.GetComponent<ParticleSystemRenderer>() : null;
            if (renderer != null) _cachedParticleMaterial = renderer.sharedMaterial;
            return _cachedParticleMaterial;
        }

        /// <summary>Archetype colour coding, matching the pickup proxy materials in the scene.</summary>
        private static Color ColorFor(VisualArchetype archetype)
        {
            switch (archetype)
            {
                case VisualArchetype.Medical: return new Color(0.95f, 0.95f, 0.92f);
                case VisualArchetype.Artifact: return new Color(0.25f, 0.90f, 0.82f);
                case VisualArchetype.WeaponSidearm:
                case VisualArchetype.WeaponLong: return new Color(1.00f, 0.55f, 0.20f);
                case VisualArchetype.AmmunitionBox: return new Color(0.95f, 0.72f, 0.25f);
                case VisualArchetype.MetalCan: return new Color(0.55f, 0.85f, 0.45f);
                case VisualArchetype.Tool: return new Color(0.95f, 0.85f, 0.30f);
                case VisualArchetype.Document: return new Color(0.88f, 0.84f, 0.66f);
                case VisualArchetype.Clothing: return new Color(0.62f, 0.66f, 0.45f);
                case VisualArchetype.Crew: return new Color(0.75f, 0.85f, 1.00f);
                default: return new Color(0.80f, 0.70f, 0.50f);   // Crate and anything unclassified
            }
        }
    }
}
