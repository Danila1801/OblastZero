// Assets/_Project/Scripts/UI/ControllerNavigationUI.cs
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OblastZero.UI
{
    /// <summary>
    /// Makes a code-built screen navigable with a gamepad or the keyboard arrows.
    ///
    /// <para>Unity's <c>InputSystemUIInputModule</c> already reads the stick and the D-pad and already moves
    /// the selection between Selectables — but only once something IS selected. Every screen in this project
    /// builds its canvas at runtime, so nothing is ever selected on entry, and a pad-only player sees a menu
    /// with no highlight that does not respond to the stick. That is the whole bug this component fixes, and
    /// it is why the fix is "select something", not "reimplement navigation".</para>
    ///
    /// <para>Three jobs, all of them small:</para>
    /// <list type="number">
    ///   <item>Select a sensible first control on enable, once the EventSystem exists.</item>
    ///   <item>Re-select when the selection is lost — clicking empty background clears it, after which the
    ///         pad appears dead until the player finds something to click.</item>
    ///   <item>Rebuild the explicit navigation graph, so movement follows the visual column order rather
    ///         than Unity's automatic nearest-neighbour search, which crosses between unrelated columns on
    ///         a two-column screen like the registration form.</item>
    /// </list>
    ///
    /// <para>Presentation only, like everything else in this namespace: it selects controls, it never
    /// invokes them.</para>
    /// </summary>
    public class ControllerNavigationUI : MonoBehaviour
    {
        [Tooltip("Control focused when the screen opens. Falls back to the first interactable Selectable found.")]
        [SerializeField] private Selectable firstSelected;

        [Tooltip("Rebuild vertical navigation from the visual top-to-bottom order of the interactable controls.")]
        [SerializeField] private bool buildVerticalChain = true;

        private readonly List<Selectable> _ordered = new List<Selectable>();

        /// <summary>
        /// Adds navigation to a screen root and names the control that starts focused. Call it once, after
        /// the screen has finished building — the component reads the hierarchy on enable, so attaching it
        /// mid-build would capture half a screen.
        /// </summary>
        public static ControllerNavigationUI Attach(GameObject screenRoot, Selectable firstSelected)
        {
            if (screenRoot == null) return null;

            var nav = screenRoot.GetComponent<ControllerNavigationUI>();
            if (nav == null) nav = screenRoot.AddComponent<ControllerNavigationUI>();
            nav.firstSelected = firstSelected;
            return nav;
        }

        private void OnEnable()
        {
            RefreshOrder();
            SelectFirst();
        }

        private void Update()
        {
            // A click on empty background clears the selection; without this the pad goes dead until the
            // player clicks a control again, which a pad-only player cannot do.
            var events = EventSystem.current;
            if (events == null) return;

            var selected = events.currentSelectedGameObject;
            if (selected != null && selected.activeInHierarchy) return;

            SelectFirst();
        }

        /// <summary>
        /// Re-reads the screen. Screens that rebuild themselves — on a language change, or when a tab
        /// swaps its contents — must call this, or the navigation graph still points at destroyed controls.
        /// </summary>
        public void RefreshOrder()
        {
            _ordered.Clear();
            GetComponentsInChildren(false, _ordered);

            // Visual order, top of the screen first. Selectables are built in arbitrary hierarchy order on
            // these screens, so sorting by screen position is what makes "down" mean down.
            _ordered.RemoveAll(s => s == null || !s.IsInteractable());
            _ordered.Sort(CompareByScreenPosition);

            if (buildVerticalChain) BuildChain();
        }

        /// <summary>Explicitly focuses a control, e.g. after a tab switch rebuilds a panel.</summary>
        public void Select(Selectable target)
        {
            if (target == null || EventSystem.current == null) return;
            EventSystem.current.SetSelectedGameObject(target.gameObject);
        }

        private void SelectFirst()
        {
            var events = EventSystem.current;
            if (events == null) return;

            Selectable target = firstSelected != null && firstSelected.gameObject.activeInHierarchy &&
                                firstSelected.IsInteractable()
                ? firstSelected
                : (_ordered.Count > 0 ? _ordered[0] : null);

            if (target == null) return;
            events.SetSelectedGameObject(target.gameObject);
        }

        /// <summary>
        /// Wires each control's up/down explicitly and wraps at the ends. Unity's automatic mode picks the
        /// nearest Selectable in the pressed direction, which on the two-column registration screen jumps
        /// from the site list into the crew list halfway down and reads as the stick misfiring.
        /// </summary>
        private void BuildChain()
        {
            for (int i = 0; i < _ordered.Count; i++)
            {
                var nav = new Navigation { mode = Navigation.Mode.Explicit };

                nav.selectOnUp = _ordered[(i - 1 + _ordered.Count) % _ordered.Count];
                nav.selectOnDown = _ordered[(i + 1) % _ordered.Count];

                // Left/right stay unset: on these screens they would need to mean "other column", and the
                // columns hold different numbers of rows, so any mapping is wrong for most rows. Up/down
                // through the full list reaches every control, which is what matters.
                _ordered[i].navigation = nav;
            }
        }

        private static int CompareByScreenPosition(Selectable a, Selectable b)
        {
            var rectA = a.transform as RectTransform;
            var rectB = b.transform as RectTransform;
            if (rectA == null || rectB == null) return 0;

            Vector3 posA = rectA.position;
            Vector3 posB = rectB.position;

            // Screen space: higher y is further up, so descending y is top-to-bottom.
            int byY = posB.y.CompareTo(posA.y);
            return byY != 0 ? byY : posA.x.CompareTo(posB.x);
        }
    }
}
