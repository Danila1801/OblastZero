// Assets/_Project/Scripts/UI/OblastUIAudio.cs
using UnityEngine;
using UnityEngine.EventSystems;
using OblastZero.Gameplay;

namespace OblastZero.UI
{
    /// <summary>
    /// The click and hover sounds for every button in the game. <see cref="OblastUI.Button"/> calls into
    /// this, so a screen written next year gets UI audio without knowing it exists.
    ///
    /// <para>Presentation stays presentation: this raises no events and reads no game state — it asks
    /// <see cref="AudioManager"/> for a cue and nothing else. If the AudioManager is absent (a UI test
    /// booted without Bootstrap, or <c>initializeAudio</c> off) every call is a silent no-op rather than a
    /// null reference, which is why the UI layer talks to the static wrappers instead of
    /// <c>AudioManager.Instance</c>.</para>
    /// </summary>
    public static class OblastUIAudio
    {
        public static void PlayClick()
        {
            AudioManager.Play(AudioManager.CUE_UI_CLICK);
        }

        public static void PlayHover()
        {
            AudioManager.Play(AudioManager.CUE_UI_HOVER);
        }

        /// <summary>
        /// Adds pointer-enter audio to <paramref name="target"/>. Uses the EventSystem's own
        /// <see cref="EventTrigger"/> rather than a bespoke <c>IPointerEnterHandler</c> component,
        /// because a hand-rolled handler on the same object as the Button competes for the same pointer
        /// events, and whichever one Unity dispatches to first is not something to rely on.
        /// </summary>
        public static void AttachHover(GameObject target)
        {
            if (target == null) return;

            var trigger = target.GetComponent<EventTrigger>();
            if (trigger == null) trigger = target.AddComponent<EventTrigger>();

            var entry = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
            entry.callback.AddListener(OnPointerEnter);
            trigger.triggers.Add(entry);
        }

        /// <summary>
        /// A hover on a disabled button must stay silent, or a greyed-out "CONFIRM" answers back. The
        /// pointer data carries the object that was entered, so the check does not need captured state.
        /// </summary>
        private static void OnPointerEnter(BaseEventData data)
        {
            var pointer = data as PointerEventData;
            var entered = pointer != null ? pointer.pointerEnter : null;
            if (entered != null)
            {
                var selectable = entered.GetComponentInParent<UnityEngine.UI.Selectable>();
                if (selectable != null && !selectable.IsInteractable()) return;
            }

            PlayHover();
        }
    }
}
