// Assets/_Project/Scripts/Core/InputBindingTable.cs
using System;
using System.Collections.Generic;
using UnityEngine.InputSystem;

namespace OblastZero.Core
{
    /// <summary>
    /// The rebindable actions of the 3D Blowout. Look is mouse / right-stick and is not rebindable — there
    /// is nothing a player would want to rebind it to.
    ///
    /// Named <c>OblastAction</c>, not <c>InputAction</c>: <see cref="UnityEngine.InputSystem.InputAction"/>
    /// already owns that name, and every file that polls devices imports that namespace. A same-named enum
    /// in <c>OblastZero.Core</c> would make the identifier ambiguous in exactly the files that need both.
    /// </summary>
    public enum OblastAction
    {
        MoveForward,
        MoveBackward,
        MoveLeft,
        MoveRight,
        Sprint,
        Interact,
        Pause
    }

    /// <summary>
    /// The keyboard half of the control scheme: the default key per <see cref="OblastAction"/>, the
    /// localization key for each action's label, and what to print on a key cap.
    ///
    /// <para>Bindings are stored as <see cref="Key"/> — the Input System's layout-independent enum — not as
    /// a raw integer and not as a control path string. An integer silently repoints at a different key the
    /// moment Unity reorders the enum, and a control path ("&lt;Keyboard&gt;/w") has to be re-parsed on every
    /// poll. <see cref="Key"/> round-trips through Newtonsoft as its name, so a preferences file stays
    /// readable by a human and survives an engine upgrade.</para>
    ///
    /// <para>Everything here is data plus pure functions — no MonoBehaviour, no singleton — so the player
    /// controller, the options screen and the preferences file all agree without any of them owning the
    /// others.</para>
    /// </summary>
    public static class InputBindingTable
    {
        /// <summary>Every action, in the order the controls screen lists them.</summary>
        public static readonly OblastAction[] AllActions =
        {
            OblastAction.MoveForward,
            OblastAction.MoveBackward,
            OblastAction.MoveLeft,
            OblastAction.MoveRight,
            OblastAction.Sprint,
            OblastAction.Interact,
            OblastAction.Pause
        };

        private static readonly Dictionary<OblastAction, Key> kDefaults = new Dictionary<OblastAction, Key>
        {
            { OblastAction.MoveForward,  Key.W },
            { OblastAction.MoveBackward, Key.S },
            { OblastAction.MoveLeft,     Key.A },
            { OblastAction.MoveRight,    Key.D },
            { OblastAction.Sprint,       Key.LeftShift },
            { OblastAction.Interact,     Key.E },
            { OblastAction.Pause,        Key.Escape }
        };

        /// <summary>Standard issue. Restoring defaults copies this table; nothing mutates it.</summary>
        public static IReadOnlyDictionary<OblastAction, Key> Defaults => kDefaults;

        /// <summary>A fresh mutable copy of the defaults.</summary>
        public static Dictionary<OblastAction, Key> CreateDefaults()
            => new Dictionary<OblastAction, Key>(kDefaults);

        /// <summary>The default key for one action.</summary>
        public static Key DefaultFor(OblastAction action)
            => kDefaults.TryGetValue(action, out var key) ? key : Key.None;

        /// <summary>Localization key for an action's label in the controls list.</summary>
        public static string LabelKeyFor(OblastAction action)
        {
            switch (action)
            {
                case OblastAction.MoveForward:  return UIStringKeys.ActionMoveForward;
                case OblastAction.MoveBackward: return UIStringKeys.ActionMoveBackward;
                case OblastAction.MoveLeft:     return UIStringKeys.ActionMoveLeft;
                case OblastAction.MoveRight:    return UIStringKeys.ActionMoveRight;
                case OblastAction.Sprint:       return UIStringKeys.ActionSprint;
                case OblastAction.Interact:     return UIStringKeys.ActionInteract;
                case OblastAction.Pause:        return UIStringKeys.ActionPause;
                default:                        return action.ToString();
            }
        }

        /// <summary>
        /// What to print on a key cap. The enum name reads correctly for letters and digits but wrong for
        /// the modifiers and navigation keys a player actually looks for, so those are spelled out.
        ///
        /// Deliberately NOT localized: a key cap should read what is printed on the physical key, and
        /// "L SHIFT" is what is printed on a Russian keyboard too.
        /// </summary>
        public static string DisplayName(Key key)
        {
            switch (key)
            {
                case Key.None:       return "—";
                case Key.LeftShift:  return "L SHIFT";
                case Key.RightShift: return "R SHIFT";
                case Key.LeftCtrl:   return "L CTRL";
                case Key.RightCtrl:  return "R CTRL";
                case Key.LeftAlt:    return "L ALT";
                case Key.RightAlt:   return "R ALT";
                case Key.Space:      return "SPACE";
                case Key.Escape:     return "ESC";
                case Key.Enter:      return "ENTER";
                case Key.Tab:        return "TAB";
                case Key.Backspace:  return "BKSP";
                case Key.UpArrow:    return "UP";
                case Key.DownArrow:  return "DOWN";
                case Key.LeftArrow:  return "LEFT";
                case Key.RightArrow: return "RIGHT";
                default:             return key.ToString().ToUpperInvariant();
            }
        }

        /// <summary>
        /// Whether a captured key may become a new binding.
        ///
        /// Escape is refused because it is the universal "get me out of this rebind" gesture: if a player
        /// could bind Escape to Sprint, the next rebind they start would have no cancel. Pause still
        /// DEFAULTS to Escape — a default is issued by the game, not captured from the player, so it is
        /// not routed through this check.
        /// </summary>
        public static bool IsBindable(Key key)
        {
            if (key == Key.None) return false;
            if (key == Key.Escape) return false;
            if (key == Key.PrintScreen) return false;
            return true;
        }

        /// <summary>
        /// Reads a key name back from a preferences file. An unknown or removed name falls back to that
        /// action's default rather than to <see cref="Key.None"/> — an unbound movement key is
        /// indistinguishable, in play, from a broken game.
        ///
        /// A stored value equal to the action's own default is always accepted even when
        /// <see cref="IsBindable"/> would refuse it, which is what lets Pause keep Escape across a
        /// save/load round trip.
        /// </summary>
        public static Key ParseOrDefault(string keyName, OblastAction action)
        {
            Key fallback = DefaultFor(action);

            if (!string.IsNullOrEmpty(keyName) &&
                Enum.TryParse(keyName, ignoreCase: true, result: out Key parsed) &&
                (IsBindable(parsed) || parsed == fallback))
            {
                return parsed;
            }

            return fallback;
        }
    }
}
