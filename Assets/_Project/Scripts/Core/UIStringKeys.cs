// Assets/_Project/Scripts/Core/UIStringKeys.cs
namespace OblastZero.Core
{
    /// <summary>
    /// Every localization key the game's own screens look up, as compile-time constants.
    ///
    /// This file exists because <see cref="LocalizedStrings.Get"/> cannot fail: a mistyped key returns the
    /// key itself, so <c>"menu_main_quti"</c> ships as a button that reads <c>menu_main_quti</c> and nothing
    /// in the build, the compiler, or the QA gates notices. Routing every lookup through a constant turns
    /// that class of typo into a compile error, and gives <c>tools/localization_qa.py</c> a single authority
    /// to check both language tables against — the gate parses THIS file, so a key added here and forgotten
    /// in <c>localization_ru.json</c> fails CI rather than shipping as English inside a Russian screen.
    ///
    /// Rules for adding a key:
    ///   1. Add the constant here, in the region for its screen.
    ///   2. Add the same key to Assets/Data/Resources/Locale/localization_en.json AND localization_ru.json.
    ///   3. Format strings keep their <c>{0}</c> placeholders identical across every language — the gate
    ///      compares placeholder sets, because a translator who drops a <c>{1}</c> produces a FormatException
    ///      at runtime, not a typo on screen.
    ///
    /// Keys are snake_case to match the tables that already shipped. Content keys (event titleKey /
    /// narrativeTextKey / choiceLabelKey) are NOT listed here — those are authored per-event in content JSON
    /// and resolved by the same table, but they are content, not chrome.
    /// </summary>
    public static class UIStringKeys
    {
        // ── Main menu ────────────────────────────────────────────────────────
        public const string MenuTitle = "menu_main_title";
        public const string MenuSubtitle = "menu_main_subtitle";
        public const string MenuNewRun = "menu_main_new_run";
        public const string MenuContinue = "menu_main_continue";
        public const string MenuOptions = "menu_main_options";
        public const string MenuQuit = "menu_main_quit";
        public const string MenuRecordNone = "menu_main_record_none";
        public const string MenuRecord = "menu_main_record";
        public const string MenuFileOpen = "menu_main_file_open";
        public const string MenuFileNone = "menu_main_file_none";
        public const string MenuFooter = "menu_main_footer";

        // ── Run setup (expedition registration) ──────────────────────────────
        public const string RunSetupHeader = "run_setup_header";
        public const string RunSetupSubheader = "run_setup_subheader";
        public const string RunSetupSiteHeading = "run_setup_site_heading";
        public const string RunSetupCrewHeading = "run_setup_crew_heading";
        public const string RunSetupNoSites = "run_setup_no_sites";
        public const string RunSetupNoCrew = "run_setup_no_crew";
        public const string RunSetupPendingSurvey = "run_setup_pending_survey";
        public const string RunSetupSeed = "run_setup_seed";
        public const string RunSetupComplete = "run_setup_complete";
        public const string RunSetupIncompleteSite = "run_setup_incomplete_site";
        public const string RunSetupIncompleteCrew = "run_setup_incomplete_crew";
        public const string RunSetupCancel = "run_setup_cancel";
        public const string RunSetupConfirm = "run_setup_confirm";
        public const string RunSetupStatHealth = "run_setup_stat_health";
        public const string RunSetupStatSanity = "run_setup_stat_sanity";
        public const string RunSetupStatCarry = "run_setup_stat_carry";
        public const string RunSetupNoFile = "run_setup_no_file";

        // Crew background labels — the roster card's right-hand status line.
        public const string CrewBackgroundLoner = "crew_background_loner";
        public const string CrewBackgroundExCordon = "crew_background_ex_cordon";
        public const string CrewBackgroundExClerk = "crew_background_ex_clerk";
        public const string CrewBackgroundMedic = "crew_background_medic";
        public const string CrewBackgroundMechanic = "crew_background_mechanic";
        public const string CrewBackgroundKafedraDefector = "crew_background_kafedra_defector";
        public const string CrewBackgroundEcologist = "crew_background_ecologist";
        public const string CrewBackgroundUnclassified = "crew_background_unclassified";

        // ── Scavenge (Blowout) HUD ───────────────────────────────────────────
        public const string ScavengeCountdownLabel = "scavenge_countdown_label";
        public const string ScavengePrompt = "scavenge_prompt";
        public const string ScavengeVerbTake = "scavenge_verb_take";
        public const string ScavengeVerbRescue = "scavenge_verb_rescue";
        public const string ScavengeGrabbedHeader = "scavenge_grabbed_header";
        public const string ScavengeGrabbedEmpty = "scavenge_grabbed_empty";
        public const string ScavengeCrewTag = "scavenge_crew_tag";
        public const string ScavengeLoadHeader = "scavenge_load_header";
        public const string ScavengeLoadValue = "scavenge_load_value";
        public const string ScavengeOverCapacity = "scavenge_over_capacity";
        public const string ScavengeEmission = "scavenge_emission";

        // ── Bunker HUD ───────────────────────────────────────────────────────
        public const string BunkerDay = "bunker_day";
        public const string BunkerDayUnknown = "bunker_day_unknown";
        public const string BunkerCrewHeader = "bunker_crew_header";
        public const string BunkerCrewNone = "bunker_crew_none";
        public const string BunkerCrewDeceased = "bunker_crew_deceased";
        public const string BunkerStatHealth = "bunker_stat_health";
        public const string BunkerStatSanity = "bunker_stat_sanity";
        public const string BunkerStatFatigue = "bunker_stat_fatigue";
        public const string BunkerStatRadiation = "bunker_stat_radiation";
        public const string BunkerRations = "bunker_rations";
        public const string BunkerRationsDays = "bunker_rations_days";
        public const string BunkerStores = "bunker_stores";
        public const string BunkerEndDay = "bunker_end_day";
        public const string BunkerPlaceholder = "bunker_placeholder";

        // Short faction labels for the standing panel. The long legal names stay in faction_* keys,
        // which content and the summary screen use; the HUD panel is 360 px wide and needs these.
        public const string FactionShortScaleSociety = "faction_short_scale_society";
        public const string FactionShortCordon = "faction_short_cordon";
        public const string FactionShortKafedra = "faction_short_kafedra";

        // ── Event modal ──────────────────────────────────────────────────────
        public const string EventSuccess = "event_outcome_success";
        public const string EventFailure = "event_outcome_failure";
        public const string EventFollowUp = "event_outcome_follow_up";
        public const string EventChoiceUnavailable = "event_choice_unavailable";
        public const string EventContinue = "event_continue";

        // ── Run summary (closed case file) ───────────────────────────────────
        public const string SummaryLeftHeading = "summary_heading_record";
        public const string SummaryRightHeading = "summary_heading_standing";
        public const string SummaryRowSite = "summary_row_site";
        public const string SummaryRowDays = "summary_row_days";
        public const string SummaryRowLost = "summary_row_lost";
        public const string SummaryRowRemaining = "summary_row_remaining";
        public const string SummaryRowRecovered = "summary_row_recovered";
        public const string SummaryRowSalvage = "summary_row_salvage";
        public const string SummaryRowFiled = "summary_row_filed";
        public const string SummaryRowReturned = "summary_row_returned";
        public const string SummaryReturn = "summary_return";

        public const string RepBandTrusted = "rep_band_trusted";
        public const string RepBandCooperative = "rep_band_cooperative";
        public const string RepBandNeutral = "rep_band_neutral";
        public const string RepBandObstructive = "rep_band_obstructive";
        public const string RepBandHostile = "rep_band_hostile";

        // Run verdicts — headline / subheadline / closing line per RunEndReason.
        public const string VerdictAllCrewDead = "verdict_all_crew_dead";
        public const string VerdictBunkerBreach = "verdict_bunker_breach";
        public const string VerdictQuit = "verdict_quit";
        public const string VerdictExtracted = "verdict_extracted";
        public const string VerdictStabilization = "verdict_stabilization";
        public const string VerdictRelief = "verdict_relief";
        public const string VerdictAdaptation = "verdict_adaptation";
        public const string VerdictIndependent = "verdict_independent";
        public const string VerdictDefault = "verdict_default";

        public const string VerdictSubAllCrewDead = "verdict_sub_all_crew_dead";
        public const string VerdictSubBunkerBreach = "verdict_sub_bunker_breach";
        public const string VerdictSubQuit = "verdict_sub_quit";
        public const string VerdictSubDefault = "verdict_sub_default";

        public const string VerdictCloseAllCrewDead = "verdict_close_all_crew_dead";
        public const string VerdictCloseBunkerBreach = "verdict_close_bunker_breach";
        public const string VerdictCloseQuit = "verdict_close_quit";
        public const string VerdictCloseDefault = "verdict_close_default";

        // ── Options ──────────────────────────────────────────────────────────
        public const string OptionsTitle = "options_title";
        public const string OptionsSubtitle = "options_subtitle";
        public const string OptionsTabAudio = "options_tab_audio";
        public const string OptionsTabDisplay = "options_tab_display";
        public const string OptionsTabControls = "options_tab_controls";
        public const string OptionsTabLanguage = "options_tab_language";

        public const string OptionsVolumeMaster = "options_volume_master";
        public const string OptionsVolumeSfx = "options_volume_sfx";
        public const string OptionsVolumeMusic = "options_volume_music";
        public const string OptionsVolumeAmbient = "options_volume_ambient";
        public const string OptionsVolumeNote = "options_volume_note";

        public const string OptionsQuality = "options_quality";
        public const string OptionsResolution = "options_resolution";
        public const string OptionsDisplayMode = "options_display_mode";
        public const string OptionsVsync = "options_vsync";
        public const string OptionsDisplayModeExclusive = "options_display_mode_exclusive";
        public const string OptionsDisplayModeBorderless = "options_display_mode_borderless";
        public const string OptionsDisplayModeWindowed = "options_display_mode_windowed";

        public const string OptionsControlsNote = "options_controls_note";
        public const string OptionsRebindPrompt = "options_rebind_prompt";
        public const string OptionsRebindConflict = "options_rebind_conflict";
        public const string OptionsUnbound = "options_unbound";

        public const string OptionsLanguageNote = "options_language_note";
        public const string OptionsLanguageEnglish = "options_language_english";
        public const string OptionsLanguageRussian = "options_language_russian";

        public const string OptionsReset = "options_reset";
        public const string OptionsBack = "options_back";
        public const string OptionsOn = "options_on";
        public const string OptionsOff = "options_off";
        public const string OptionsPercent = "options_percent";

        // Input action labels — one per InputAction enum member.
        public const string ActionMoveForward = "action_move_forward";
        public const string ActionMoveBackward = "action_move_backward";
        public const string ActionMoveLeft = "action_move_left";
        public const string ActionMoveRight = "action_move_right";
        public const string ActionSprint = "action_sprint";
        public const string ActionInteract = "action_interact";
        public const string ActionPause = "action_pause";

        // ── Pause menu ───────────────────────────────────────────────────────
        public const string PauseTitle = "pause_title";
        public const string PauseSubtitle = "pause_subtitle";
        public const string PauseResume = "pause_resume";
        public const string PauseOptions = "pause_options";
        public const string PauseAbandon = "pause_abandon";

        // ── First-run guidance ───────────────────────────────────────────────
        public const string FirstRunBanner = "first_run_banner";
        public const string FirstRunDismiss = "first_run_dismiss";
        public const string FirstRunSetupSite = "first_run_setup_site";
        public const string FirstRunSetupCrew = "first_run_setup_crew";
        public const string FirstRunSetupCarry = "first_run_setup_carry";
        public const string FirstRunScavengeControls = "first_run_scavenge_controls";
        public const string FirstRunBunkerEndDay = "first_run_bunker_end_day";
        public const string FirstRunBunkerEvents = "first_run_bunker_events";

        // ── Supply office (meta-progression / salvage tokens) ────────────────
        public const string SupplyHeader = "supply_header";
        public const string SupplySubheader = "supply_subheader";
        public const string SupplyBalance = "supply_balance";
        public const string SupplyOnFile = "supply_on_file";
        public const string SupplyApprove = "supply_approve";
        public const string SupplyCost = "supply_cost";
        public const string SupplyFooter = "supply_footer";
        public const string SupplyReturn = "supply_return";
        public const string SupplyPurchased = "supply_purchased";
        public const string SupplyRefused = "supply_refused";
        public const string MenuSupplyOffice = "menu_main_supply_office";
        public const string MenuTokens = "menu_main_tokens";

        // ── Meta-unlock catalogue (one name + one description per entry) ─────
        public const string UnlockRationsName = "unlock_rations_name";
        public const string UnlockRationsDesc = "unlock_rations_desc";
        public const string UnlockMedkitName = "unlock_medkit_name";
        public const string UnlockMedkitDesc = "unlock_medkit_desc";
        public const string UnlockCarryName = "unlock_carry_name";
        public const string UnlockCarryDesc = "unlock_carry_desc";
        public const string UnlockHealthName = "unlock_health_name";
        public const string UnlockHealthDesc = "unlock_health_desc";
        public const string UnlockSanityName = "unlock_sanity_name";
        public const string UnlockSanityDesc = "unlock_sanity_desc";
        public const string UnlockRepSocietyName = "unlock_rep_society_name";
        public const string UnlockRepSocietyDesc = "unlock_rep_society_desc";
        public const string UnlockRepCordonName = "unlock_rep_cordon_name";
        public const string UnlockRepCordonDesc = "unlock_rep_cordon_desc";
        public const string UnlockRepKafedraName = "unlock_rep_kafedra_name";
        public const string UnlockRepKafedraDesc = "unlock_rep_kafedra_desc";
        public const string UnlockSecondSiteName = "unlock_second_site_name";
        public const string UnlockSecondSiteDesc = "unlock_second_site_desc";
        public const string UnlockSecondOperatorName = "unlock_second_operator_name";
        public const string UnlockSecondOperatorDesc = "unlock_second_operator_desc";

        // ── Run summary: token award ─────────────────────────────────────────
        public const string SummaryRowTokens = "summary_row_tokens";
        public const string SummaryRowTokenBalance = "summary_row_token_balance";

        // ── Run setup: crew traits + second operator ─────────────────────────
        public const string RunSetupTraits = "run_setup_traits";
        public const string RunSetupNoTraits = "run_setup_no_traits";
        public const string RunSetupSecondCrewHeading = "run_setup_second_crew_heading";
        public const string RunSetupSecondCrewNone = "run_setup_second_crew_none";
    }
}
