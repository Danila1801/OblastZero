// Assets/_Project/Scripts/UI/ExpeditionUI.cs
using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using OblastZero.Core;
using OblastZero.Gameplay;
using OblastZero.Gameplay.ExpeditionSystem;

namespace OblastZero.UI
{
    /// <summary>
    /// The dispatch order: pick a subject, a region, and up to three items of kit to send with them.
    /// Opened from the bunker HUD, spawned and destroyed by <c>SurvivalPhase2DState</c>.
    ///
    /// <para><b>Reads state, raises one intent.</b> Everything the screen shows comes from
    /// <see cref="ExpeditionManager"/>, and the only thing it does is call <c>Dispatch</c>. It does not
    /// remove the loadout, mark the crew member deployed, or write the return day — all three belong to
    /// the manager, which is the sole owner of <c>RunData.ExpeditionsInFlight</c>.</para>
    ///
    /// <para><b>The regions offered are all seven, including ones with no built level.</b> An expedition
    /// is not a scavenge run — nobody loads a scene for it, the crew member walks off the roster and a
    /// report comes back. So the geography the player can reach on foot from the bunker is the whole
    /// oblast, not the subset that happens to have a Blowout map. That is also what stops the bunker
    /// phase from feeling like a waiting room attached to one warehouse.</para>
    /// </summary>
    public class ExpeditionUI : MonoBehaviour
    {
        private ExpeditionManager _expeditions;
        private InventoryManager _inventory;
        private Action _onClose;

        private RectTransform _crewColumn;
        private RectTransform _regionColumn;
        private RectTransform _loadoutColumn;
        private RectTransform _inFlightColumn;
        private TextMeshProUGUI _status;

        private string _selectedCrewId;
        private string _selectedRegionId = OblastRegions.OuterCordon;
        private readonly List<string> _loadout = new List<string>();

        /// <summary>Spawns the screen. <paramref name="onClose"/> fires once, when it closes itself.</summary>
        public static ExpeditionUI Open(Action onClose)
        {
            var go = new GameObject("ExpeditionUI");
            var ui = go.AddComponent<ExpeditionUI>();
            ui._onClose = onClose;
            return ui;
        }

        private void Awake()
        {
            var gm = GameManager.Instance;
            _expeditions = gm != null ? gm.Expeditions : null;
            _inventory = gm != null ? gm.Inventory : null;

            BuildChrome();
            RefreshAll();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<ExpeditionDispatchedEvent>(OnDispatched);
            EventBus.Subscribe<ExpeditionResolvedEvent>(OnResolved);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<ExpeditionDispatchedEvent>(OnDispatched);
            EventBus.Unsubscribe<ExpeditionResolvedEvent>(OnResolved);
        }

        private void OnDispatched(ExpeditionDispatchedEvent e) => RefreshAll();
        private void OnResolved(ExpeditionResolvedEvent e) => RefreshAll();

        // ── Chrome ───────────────────────────────────────────────────────────

        private void BuildChrome()
        {
            var root = OblastUI.CreateScreenCanvas(transform, "Canvas", sortingOrder: 320);
            OblastUI.Stretch(root);

            var scrim = OblastUI.Rect(root, "Scrim", new Color(0.02f, 0.02f, 0.024f, 0.92f), raycast: true);
            OblastUI.Stretch(scrim.rectTransform);

            var panel = OblastUI.Rect(root, "Panel", OblastUI.Panel);
            OblastUI.Center(panel.rectTransform, Vector2.zero, new Vector2(1500f, 780f));

            OblastUI.StretchTop(OblastUI.Rect(panel.transform, "Edge", OblastUI.Hairline).rectTransform, 1f);

            var heading = OblastUI.Label(panel.transform, "Heading", "DISPATCH ORDER", 30f, FontStyles.Bold,
                                         TextAlignmentOptions.Left, OblastUI.TextPrimary);
            OblastUI.TopLeft(heading.rectTransform, new Vector2(40f, -32f), new Vector2(700f, 36f));

            var sub = OblastUI.Label(panel.transform, "Sub",
                                     "One subject, one destination, up to " +
                                     BalanceConstants.EXPEDITION_MAX_LOADOUT_ITEMS +
                                     " items of issued kit. Kit leaves stores on departure.",
                                     18f, FontStyles.Italic, TextAlignmentOptions.Left, OblastUI.TextFaint);
            OblastUI.TopLeft(sub.rectTransform, new Vector2(40f, -70f), new Vector2(900f, 24f));

            OblastUI.TopLeft(OblastUI.Rule(panel.transform, "Rule", 1420f, OblastUI.Hairline).rectTransform,
                             new Vector2(40f, -104f), new Vector2(1420f, 1f));

            _crewColumn = Column(panel.transform, "Crew", "SUBJECT", 40f);
            _regionColumn = Column(panel.transform, "Region", "DESTINATION", 400f);
            _loadoutColumn = Column(panel.transform, "Loadout", "KIT", 760f);
            _inFlightColumn = Column(panel.transform, "InFlight", "IN THE FIELD", 1120f);

            _status = OblastUI.Label(panel.transform, "Status", string.Empty, 19f, FontStyles.Italic,
                                     TextAlignmentOptions.Left, OblastUI.Olive);
            _status.enableWordWrapping = true;
            OblastUI.BottomLeft(_status.rectTransform, new Vector2(40f, 100f), new Vector2(1100f, 48f));

            TextMeshProUGUI dispatchLabel;
            var dispatch = OblastUI.Button(panel.transform, "Dispatch", "CONFIRM DISPATCH", 21f,
                                           OnConfirmDispatch, out dispatchLabel);
            OblastUI.BottomLeft(dispatch.GetComponent<RectTransform>(), new Vector2(40f, 36f),
                                new Vector2(340f, 50f));

            TextMeshProUGUI closeLabel;
            var close = OblastUI.Button(panel.transform, "Close", "CLOSE", 21f, Close, out closeLabel);
            OblastUI.BottomLeft(close.GetComponent<RectTransform>(), new Vector2(400f, 36f),
                                new Vector2(200f, 50f));
        }

        private RectTransform Column(Transform parent, string name, string title, float x)
        {
            var heading = OblastUI.Label(parent, name + "Heading", title, 17f, FontStyles.Bold,
                                         TextAlignmentOptions.TopLeft, OblastUI.TextFaint);
            OblastUI.TopLeft(heading.rectTransform, new Vector2(x, -124f), new Vector2(330f, 20f));

            var column = OblastUI.Group(parent, name);
            OblastUI.TopLeft(column, new Vector2(x, -152f), new Vector2(330f, 480f));
            return column;
        }

        // ── Population ───────────────────────────────────────────────────────

        private void RefreshAll()
        {
            RefreshCrew();
            RefreshRegions();
            RefreshLoadout();
            RefreshInFlight();
            RefreshStatus();
        }

        private void RefreshCrew()
        {
            ClearChildren(_crewColumn);
            if (_expeditions == null) return;

            var available = _expeditions.AvailableCrew();
            if (available.Count == 0)
            {
                Note(_crewColumn, "Nobody on strength is available. Everyone is out, or worse.");
                _selectedCrewId = null;
                return;
            }

            if (string.IsNullOrEmpty(_selectedCrewId) ||
                available.Find(c => c.instanceId == _selectedCrewId) == null)
                _selectedCrewId = available[0].instanceId;

            for (int i = 0; i < available.Count && i < 6; i++)
            {
                var member = available[i];
                bool chosen = member.instanceId == _selectedCrewId;

                TextMeshProUGUI label;
                var button = OblastUI.Button(_crewColumn, "Crew_" + member.instanceId,
                                             (chosen ? "> " : "  ") + member.instanceId, 17f,
                                             () => { _selectedCrewId = member.instanceId; RefreshStatus(); },
                                             out label);
                label.alignment = TextAlignmentOptions.Left;
                label.color = chosen ? OblastUI.Stamp : OblastUI.TextPrimary;
                OblastUI.TopLeft(button.GetComponent<RectTransform>(),
                                 new Vector2(0f, -i * 46f), new Vector2(320f, 40f));
            }
        }

        private void RefreshRegions()
        {
            ClearChildren(_regionColumn);

            var regions = OblastRegions.All;
            for (int i = 0; i < regions.Count; i++)
            {
                string id = regions[i];
                bool chosen = id == _selectedRegionId;

                TextMeshProUGUI label;
                var button = OblastUI.Button(_regionColumn, "Region_" + id,
                                             (chosen ? "> " : "  ") + OblastRegions.DisplayNameOf(id), 17f,
                                             () => { _selectedRegionId = id; RefreshStatus(); }, out label);
                label.alignment = TextAlignmentOptions.Left;
                label.color = chosen ? OblastUI.Stamp : OblastUI.TextPrimary;
                OblastUI.TopLeft(button.GetComponent<RectTransform>(),
                                 new Vector2(0f, -i * 46f), new Vector2(320f, 40f));
            }
        }

        private void RefreshLoadout()
        {
            ClearChildren(_loadoutColumn);
            if (_inventory == null) return;

            // Distinct ids, so a stack of six tins is one row rather than six. The loadout is a list of
            // ids and the manager removes one unit per entry, so the same id can be picked more than
            // once up to the slot limit.
            var stores = _inventory.Get(InventoryChannel.Bunker);
            var seen = new List<string>();
            for (int i = 0; i < stores.Count; i++)
            {
                var stack = stores[i];
                if (stack == null || stack.quantity <= 0) continue;
                if (!seen.Contains(stack.itemDataId)) seen.Add(stack.itemDataId);
            }

            if (seen.Count == 0)
            {
                Note(_loadoutColumn, "Stores are empty. They go with what they are wearing.");
                return;
            }

            int row = 0;
            var summary = OblastUI.Label(_loadoutColumn, "Slots",
                                         $"ASSIGNED {_loadout.Count}/{BalanceConstants.EXPEDITION_MAX_LOADOUT_ITEMS}"
                                         + (_loadout.Count > 0 ? "  (click again to withdraw)" : string.Empty),
                                         16f, FontStyles.Bold, TextAlignmentOptions.TopLeft, OblastUI.Olive);
            OblastUI.TopLeft(summary.rectTransform, Vector2.zero, new Vector2(320f, 20f));
            row++;

            for (int i = 0; i < seen.Count && i < 8; i++)
            {
                string id = seen[i];
                int assigned = CountAssigned(id);

                TextMeshProUGUI label;
                var button = OblastUI.Button(_loadoutColumn, "Item_" + id,
                                             (assigned > 0 ? $"[{assigned}] " : "     ") + Shorten(id), 15f,
                                             () => ToggleLoadout(id), out label);
                label.alignment = TextAlignmentOptions.Left;
                label.color = assigned > 0 ? OblastUI.Stamp : OblastUI.TextPrimary;
                OblastUI.TopLeft(button.GetComponent<RectTransform>(),
                                 new Vector2(0f, -row * 44f - 6f), new Vector2(320f, 38f));
                row++;
            }
        }

        private void RefreshInFlight()
        {
            ClearChildren(_inFlightColumn);
            if (_expeditions == null) return;

            var inFlight = _expeditions.InFlight;
            if (inFlight.Count == 0)
            {
                Note(_inFlightColumn, "No expeditions on the board.");
                return;
            }

            var run = GameManager.Instance != null ? GameManager.Instance.CurrentRun : null;
            int today = run != null ? run.currentDay : 0;

            for (int i = 0; i < inFlight.Count; i++)
            {
                var expedition = inFlight[i];
                int due = ExpeditionManager.ReturnDayOf(expedition);
                int daysLeft = due - today;

                var entry = OblastUI.Label(_inFlightColumn, "Flight_" + i,
                                           $"{expedition.crewInstanceId}\n" +
                                           $"{OblastRegions.DisplayNameOf(expedition.regionTag)}\n" +
                                           (daysLeft > 0
                                               ? $"due day {due} ({daysLeft} day(s))"
                                               : $"overdue since day {due}"),
                                           16f, FontStyles.Normal, TextAlignmentOptions.TopLeft,
                                           daysLeft > 0 ? OblastUI.TextPrimary : OblastUI.Danger);
                entry.enableWordWrapping = true;
                OblastUI.TopLeft(entry.rectTransform, new Vector2(0f, -i * 90f), new Vector2(320f, 84f));
            }
        }

        private void RefreshStatus()
        {
            if (_expeditions == null) { _status.text = "No expedition board available."; return; }

            var check = _expeditions.CanDispatch(_selectedCrewId, _selectedRegionId);
            _status.color = check == ExpeditionManager.DispatchResult.Success
                ? OblastUI.Olive : OblastUI.TextDim;

            _status.text = check == ExpeditionManager.DispatchResult.Success
                ? $"Ready. {BalanceConstants.EXPEDITION_MIN_DAYS}-{BalanceConstants.EXPEDITION_MAX_DAYS} " +
                  "days out. They eat nothing here while they are gone, and nothing here answers for them."
                : Explain(check);
        }

        // ── Actions ──────────────────────────────────────────────────────────

        private void ToggleLoadout(string itemId)
        {
            if (_loadout.Contains(itemId)) _loadout.Remove(itemId);
            else if (_loadout.Count < BalanceConstants.EXPEDITION_MAX_LOADOUT_ITEMS) _loadout.Add(itemId);

            RefreshLoadout();
        }

        private void OnConfirmDispatch()
        {
            if (_expeditions == null) return;

            ActiveExpedition dispatched;
            var result = _expeditions.Dispatch(_selectedCrewId, _selectedRegionId, _loadout, out dispatched);

            if (result != ExpeditionManager.DispatchResult.Success)
            {
                _status.text = Explain(result);
                _status.color = OblastUI.Danger;
                return;
            }

            _loadout.Clear();
            RefreshAll();

            _status.text = $"Order issued. '{dispatched.crewInstanceId}' departs for " +
                           $"{OblastRegions.DisplayNameOf(dispatched.regionTag)}. " +
                           $"Expected back day {ExpeditionManager.ReturnDayOf(dispatched)}.";
            _status.color = OblastUI.Stamp;
        }

        private static string Explain(ExpeditionManager.DispatchResult result)
        {
            switch (result)
            {
                case ExpeditionManager.DispatchResult.NoRun:
                    return "No expedition on record.";
                case ExpeditionManager.DispatchResult.CrewUnavailable:
                    return "That subject is unavailable — already in the field, or no longer on strength.";
                case ExpeditionManager.DispatchResult.TooManyInFlight:
                    return $"{BalanceConstants.EXPEDITION_MAX_CONCURRENT} parties are already out. " +
                           "The bunker cannot be left thinner than this.";
                case ExpeditionManager.DispatchResult.UnknownRegion:
                    return "That destination is not on the survey.";
                default:
                    return "Refused. No reason recorded.";
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private int CountAssigned(string itemId)
        {
            int n = 0;
            for (int i = 0; i < _loadout.Count; i++) if (_loadout[i] == itemId) n++;
            return n;
        }

        /// <summary>Trims the "item_" prefix and underscores so a row reads as goods, not as an id.</summary>
        private static string Shorten(string itemId)
        {
            string s = itemId.StartsWith("item_") ? itemId.Substring(5) : itemId;
            return s.Replace('_', ' ');
        }

        private static void Note(Transform parent, string text)
        {
            var label = OblastUI.Label(parent, "Note", text, 16f, FontStyles.Italic,
                                       TextAlignmentOptions.TopLeft, OblastUI.TextDim);
            label.enableWordWrapping = true;
            OblastUI.TopLeft(label.rectTransform, Vector2.zero, new Vector2(320f, 90f));
        }

        private void ClearChildren(RectTransform parent)
        {
            if (parent == null) return;
            for (int i = parent.childCount - 1; i >= 0; i--) Destroy(parent.GetChild(i).gameObject);
        }

        private void Close()
        {
            var callback = _onClose;
            _onClose = null;
            Destroy(gameObject);
            if (callback != null) callback();
        }
    }
}
