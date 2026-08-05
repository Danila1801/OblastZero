// Assets/_Project/Scripts/OblastZero.Gameplay/PickupHoverHighlight.cs
using UnityEngine;
using OblastZero.Core;
using OblastZero.Data;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// Sits on every <see cref="ScavengePickup"/>. When the player's crosshair raycast lands on it,
    /// <see cref="ScavengePlayerController"/> calls <see cref="OnHoverStart"/>: the object's emission is
    /// boosted and a world-space label appears above it giving the name, the weight and the silhouette
    /// class. Carry weight is the whole decision in Phase A, so the number has to be legible *before*
    /// the player commits the pickup, not afterwards in the HUD list.
    ///
    /// <para><b>Highlight is a MaterialPropertyBlock, not a material swap.</b> CLAUDE.md §14 is explicit
    /// about this and the reason bites in both directions. Writing to <c>renderer.sharedMaterial</c>
    /// mutates the shared asset, so hovering one crate lights up all of them and leaves the <c>.mat</c>
    /// dirty in the Editor. Reading <c>renderer.material</c> — which the brief's sample code does, twice,
    /// including in <c>Awake</c> to "save the originals" — silently instantiates a per-object copy of
    /// every pickup's material at scene load, so the depot allocates twenty-five material clones before
    /// anything has been hovered, and the "originals" it saved are the clones. A property block writes
    /// per-renderer override values with no allocation, no shared-asset mutation, and nothing to restore
    /// beyond clearing it.</para>
    ///
    /// <para><b>Label is a TextMesh, not TextMeshPro.</b> TMP needs a one-time Editor import step
    /// (Window → TextMeshPro → Import TMP Essential Resources) that this project's own HUD warns about;
    /// a world label that renders blank on a fresh clone is worse than a plainer one that always draws.
    /// TextMesh uses the built-in Arial and has no import step.</para>
    /// </summary>
    [RequireComponent(typeof(ScavengePickup))]
    public class PickupHoverHighlight : MonoBehaviour
    {
        /// <summary>URP Lit's emission colour property. Also the name the built-in pipeline uses.</summary>
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        [Tooltip("Emission added to the pickup while the crosshair is on it. Kept low — this is a hint " +
                 "that the object is interactive, not a highlight from a different game.")]
        [SerializeField] private Color highlightEmission = new Color(0.30f, 0.32f, 0.38f, 1f);

        [Tooltip("Metres above the pickup's origin the label floats.")]
        [SerializeField] private float labelHeight = 0.55f;

        [Tooltip("World height of a line of label text, in metres.")]
        [SerializeField] private float labelCharacterSize = 0.035f;

        private ScavengePickup _pickup;
        private Renderer[] _renderers;
        private Color[] _baseEmission;
        private MaterialPropertyBlock _block;
        private TextMesh _label;
        private Transform _labelTransform;
        private bool _isHovered;

        private void Awake()
        {
            _pickup = GetComponent<ScavengePickup>();
            _block = new MaterialPropertyBlock();
        }

        /// <summary>
        /// Captures the renderers to highlight, and their authored emission, on demand.
        ///
        /// <para>Deliberately not done in Awake. <see cref="ScavengePropDresser"/> replaces eight
        /// of the twenty-five pickups' primitives with GLB meshes from a <b>coroutine</b> started in
        /// <c>Start</c>, so the renderer that the player will actually see does not exist yet at Awake
        /// time and may not exist for several frames. A set captured that early would highlight the
        /// hidden primitive and leave the visible prop untouched — the effect would simply not appear on
        /// exactly the eight pickups that have real art. Re-checking the child count on each hover costs
        /// one <c>GetComponentsInChildren</c> per hover transition and cannot go stale.</para>
        /// </summary>
        private void EnsureRenderers()
        {
            var current = GetComponentsInChildren<Renderer>(true);

            if (_renderers != null && _renderers.Length == current.Length)
            {
                bool same = true;
                for (int i = 0; i < current.Length; i++)
                {
                    if (ReferenceEquals(_renderers[i], current[i])) continue;
                    same = false;
                    break;
                }
                if (same) return;
            }

            _renderers = current;

            // Read the authored emission off sharedMaterial — reading .material here would instantiate
            // the per-object copy this class exists to avoid. A material with no _EmissionColor reports
            // black, which is the correct base to add a boost to.
            _baseEmission = new Color[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++)
            {
                var shared = _renderers[i] != null ? _renderers[i].sharedMaterial : null;
                _baseEmission[i] = shared != null && shared.HasProperty(EmissionColorId)
                    ? shared.GetColor(EmissionColorId)
                    : Color.black;
            }
        }

        private void OnDisable()
        {
            // The phase can be torn down mid-hover; leave nothing overridden behind.
            if (_isHovered) OnHoverEnd();
        }

        /// <summary>Crosshair is now on this pickup. Idempotent.</summary>
        public void OnHoverStart()
        {
            if (_isHovered) return;
            _isHovered = true;

            EnsureRenderers();
            EnsureLabel();
            if (_labelTransform != null) _labelTransform.gameObject.SetActive(true);

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                _renderers[i].GetPropertyBlock(_block);
                _block.SetColor(EmissionColorId, _baseEmission[i] + highlightEmission);
                _renderers[i].SetPropertyBlock(_block);
            }
        }

        /// <summary>Crosshair has left this pickup. Idempotent.</summary>
        public void OnHoverEnd()
        {
            if (!_isHovered) return;
            _isHovered = false;

            if (_labelTransform != null) _labelTransform.gameObject.SetActive(false);
            if (_renderers == null) return;

            for (int i = 0; i < _renderers.Length; i++)
            {
                if (_renderers[i] == null) continue;
                // Write the authored value back rather than clearing the block: the artifact and the
                // fixture materials ship with a real emission colour, and an empty block would drop
                // them to the shader default instead of restoring what the .mat authored.
                _renderers[i].GetPropertyBlock(_block);
                _block.SetColor(EmissionColorId, _baseEmission[i]);
                _renderers[i].SetPropertyBlock(_block);
            }
        }

        private void LateUpdate()
        {
            if (!_isHovered || _labelTransform == null) return;

            var cam = Camera.main;
            if (cam == null) return;

            // Billboard. Facing away from the camera rather than toward it, because a TextMesh's front
            // face is -Z; LookRotation(position - camera) puts the readable side toward the viewer.
            _labelTransform.rotation = Quaternion.LookRotation(
                _labelTransform.position - cam.transform.position, Vector3.up);

            // The parent may be a rifle scaled 0.86 x 0.11 x 0.14, and a label inherits that squash.
            // Divide the parent scale back out so every label is the same size in the world.
            Vector3 parentScale = transform.lossyScale;
            _labelTransform.localScale = new Vector3(
                parentScale.x != 0f ? 1f / parentScale.x : 1f,
                parentScale.y != 0f ? 1f / parentScale.y : 1f,
                parentScale.z != 0f ? 1f / parentScale.z : 1f);
        }

        /// <summary>
        /// Builds the label on first hover, not in Awake. Twenty-five pickups each eagerly creating a
        /// TextMesh plus its own mesh and renderer is twenty-five renderers the depot pays for whether
        /// or not the player ever looks at them; the text also cannot be resolved until the
        /// GameDatabase is up, which is not guaranteed at scene-load time.
        /// </summary>
        private void EnsureLabel()
        {
            if (_labelTransform != null) return;

            var go = new GameObject("Hover_Label");
            go.transform.SetParent(transform, false);
            _labelTransform = go.transform;

            // Position in the parent's local space, then undo the parent's non-uniform scale so the
            // offset is a true 55 cm regardless of the archetype's proportions.
            Vector3 parentScale = transform.lossyScale;
            _labelTransform.localPosition = new Vector3(
                0f, parentScale.y != 0f ? labelHeight / parentScale.y : labelHeight, 0f);

            _label = go.AddComponent<TextMesh>();
            _label.fontSize = 48;                 // high glyph resolution, scaled down by characterSize
            _label.characterSize = labelCharacterSize;
            _label.anchor = TextAnchor.LowerCenter;
            _label.alignment = TextAlignment.Center;
            _label.color = new Color(0.88f, 0.87f, 0.84f, 1f);
            _label.text = BuildLabelText();

            var renderer = go.GetComponent<MeshRenderer>();
            if (renderer != null)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        /// <summary>
        /// Name, weight and silhouette class for an item; name and the rescue verb for a crew member.
        /// Falls back to the raw data id if the database cannot resolve it, which is the honest thing to
        /// show — a blank label would hide a content bug that this is the only place a player would see.
        /// </summary>
        private string BuildLabelText()
        {
            var db = GameManager.Instance != null ? GameManager.Instance.Database : null;

            if (_pickup.Kind == ScavengePickup.PickupKind.Crew)
            {
                var crew = db != null ? db.GetCrew(_pickup.DataId) : null;
                string crewName = crew != null && !string.IsNullOrEmpty(crew.displayName)
                    ? crew.displayName : _pickup.DataId;
                return crewName + "\nCREW\n[RESCUE]";
            }

            var item = db != null ? db.GetItem(_pickup.DataId) : null;
            if (item == null) return _pickup.DataId;

            string itemName = string.IsNullOrEmpty(item.displayName) ? _pickup.DataId : item.displayName;
            var archetype = VisualArchetypeMapping.Resolve(item);

            // Quantity matters as much as unit weight: three rations are three times the load, and the
            // manifest ships stacks of up to six. Show the stack total, which is what the cap sees.
            int quantity = _pickup.Quantity;
            float totalKg = item.weightKg * quantity;

            string weightLine = quantity > 1
                ? $"{totalKg:0.0} kg  ({quantity} x {item.weightKg:0.0})"
                : $"{totalKg:0.0} kg";

            return $"{itemName}\n{weightLine}\n[{archetype.ToString().ToUpperInvariant()}]";
        }
    }
}
