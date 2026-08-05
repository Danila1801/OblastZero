// Assets/_Project/Scripts/OblastZero.Gameplay/EmissionFlashOverlay.cs
using UnityEngine;
using UnityEngine.UI;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// A fullscreen white flash, one frame up and then a fade. Built and driven by
    /// <see cref="EmissionVfxController"/>.
    ///
    /// <para><b>Its own canvas, not the HUD's.</b> The brief put this Image on the ScavengeHUD canvas.
    /// It lives on a separate canvas at a higher sorting order instead, for two reasons: a flash from
    /// something detonating outside the depot has to wash over the countdown and the load bar, not sit
    /// behind them; and ScavengeHUD builds its canvas privately and is documented as presentation-only
    /// state driven by the EventBus — handing it a mutable "flash now" method would make the VFX layer a
    /// caller into the UI layer, which CLAUDE.md §3 puts on the wrong side of the line. The HUD keeps
    /// owning the load-bar refusal flash, which really is a HUD element.</para>
    /// </summary>
    public class EmissionFlashOverlay : MonoBehaviour
    {
        /// <summary>Above ScavengeHUD's canvas, which sorts at 100.</summary>
        private const int SortingOrder = 200;

        private Image _image;
        private float _peakAlpha = 0.6f;
        private float _fadeSeconds = 0.2f;
        private float _alpha;

        /// <summary>Builds the overlay under <paramref name="parent"/> and returns it.</summary>
        public static EmissionFlashOverlay Create(Transform parent, float peakAlpha, float fadeSeconds)
        {
            var go = new GameObject("Emission_Flash_Overlay",
                                    typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = SortingOrder;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);

            // No GraphicRaycaster and no raycastTarget: a fullscreen Image that swallowed clicks would
            // block every button underneath it for the whole phase.
            var imageGo = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            imageGo.transform.SetParent(go.transform, false);

            var rect = (RectTransform)imageGo.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var overlay = go.AddComponent<EmissionFlashOverlay>();
            overlay._image = imageGo.GetComponent<Image>();
            overlay._image.color = new Color(1f, 1f, 1f, 0f);
            overlay._image.raycastTarget = false;
            overlay._peakAlpha = Mathf.Clamp01(peakAlpha);
            overlay._fadeSeconds = Mathf.Max(0.01f, fadeSeconds);
            return overlay;
        }

        /// <summary>Spikes to peak alpha. Retriggering while still fading restarts from the peak.</summary>
        public void Flash()
        {
            _alpha = _peakAlpha;
            if (_image != null) _image.color = new Color(1f, 1f, 1f, _alpha);
        }

        private void Update()
        {
            if (_alpha <= 0f || _image == null) return;

            // Unscaled: a pause or a slow-motion effect must not stretch a flash into a white screen.
            _alpha = Mathf.Max(0f, _alpha - Time.unscaledDeltaTime * (_peakAlpha / _fadeSeconds));
            _image.color = new Color(1f, 1f, 1f, _alpha);
        }
    }
}
