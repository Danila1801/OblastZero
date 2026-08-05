// Assets/_Project/Scripts/OblastZero.Gameplay/AudioManager.cs
using System.Collections.Generic;
using UnityEngine;
using OblastZero.Core;

namespace OblastZero.Gameplay
{
    /// <summary>
    /// The game's single audio authority. Everything that makes a noise asks for a named cue; nothing
    /// else touches an AudioSource. Clips are synthesized at boot by <see cref="ProceduralSfx"/>, so the
    /// project ships with no audio files at all.
    ///
    /// <para><b>Lifetime.</b> Created by <see cref="Bootstrap"/> in the _Bootstrap scene and marked
    /// <c>DontDestroyOnLoad</c>, so it survives every additive load and unload the state machine does.
    /// Resolve it through <see cref="Instance"/> or the static <see cref="Play(string,float,float)"/>
    /// wrappers — never <c>FindObjectOfType</c> (CLAUDE.md §3).</para>
    ///
    /// <para><b>Volume buses.</b> Master / SFX / Music / Ambient are plain multipliers persisted to
    /// PlayerPrefs, not an <c>AudioMixer</c>. An AudioMixer is an Editor-authored native asset — it
    /// cannot be created at runtime and cannot be written as text by the headless tooling this project
    /// is built with, so a mixer here would have been an asset nobody could regenerate. The buses expose
    /// exactly the surface an options menu needs (<see cref="SetBusVolume"/> /
    /// <see cref="GetBusVolume"/>); swapping the implementation for a mixer later touches this file
    /// only.</para>
    ///
    /// <para><b>Event wiring.</b> The manager subscribes to the EventBus itself rather than being called
    /// from gameplay code wherever possible: the pickup, refusal, day, event-modal and timer sounds all
    /// come from events that already existed. That keeps the audio layer as a pure observer, so muting
    /// it can never change game logic.</para>
    /// </summary>
    [DefaultExecutionOrder(-1500)]
    public class AudioManager : MonoBehaviour
    {
        // ─── Cue identifiers (use these, never string literals) ──────────────────────────
        public const string CUE_PICKUP_CRATE = "pickup_crate";
        public const string CUE_PICKUP_METAL = "pickup_metal";
        public const string CUE_PICKUP_PAPER = "pickup_paper";
        public const string CUE_PICKUP_WEAPON = "pickup_weapon";
        public const string CUE_PICKUP_ARTIFACT = "pickup_artifact";
        public const string CUE_PICKUP_CREW = "pickup_crew";
        public const string CUE_PICKUP_REJECTED = "pickup_rejected";
        public const string CUE_FOOTSTEP_CONCRETE = "footstep_concrete";
        public const string CUE_FOOTSTEP_METAL = "footstep_metal";
        public const string CUE_UI_CLICK = "ui_click";
        public const string CUE_UI_HOVER = "ui_hover";
        public const string CUE_EMISSION_WARN = "emission_warn";
        public const string CUE_EMISSION_CRITICAL = "emission_critical";
        public const string CUE_EMISSION_HIT = "emission_hit";
        public const string CUE_DAY_ADVANCE = "day_advance";
        public const string CUE_EVENT_OPEN = "event_open";
        public const string CUE_EVENT_CLOSE = "event_close";
        public const string CUE_VICTORY = "victory_stinger";
        public const string CUE_DEFEAT = "defeat_stinger";
        public const string CUE_AMBIENT_SCAVENGE = "ambient_scavenge";
        public const string CUE_AMBIENT_BUNKER = "ambient_bunker";
        public const string CUE_MUSIC_BUNKER = "music_bunker";

        /// <summary>Every cue, in bake order. The boot warm-up walks this so nothing hitches mid-run.</summary>
        public static readonly string[] AllCues =
        {
            CUE_PICKUP_CRATE, CUE_PICKUP_METAL, CUE_PICKUP_PAPER, CUE_PICKUP_WEAPON,
            CUE_PICKUP_ARTIFACT, CUE_PICKUP_CREW, CUE_PICKUP_REJECTED,
            CUE_FOOTSTEP_CONCRETE, CUE_FOOTSTEP_METAL,
            CUE_UI_CLICK, CUE_UI_HOVER,
            CUE_EMISSION_WARN, CUE_EMISSION_CRITICAL, CUE_EMISSION_HIT,
            CUE_DAY_ADVANCE, CUE_EVENT_OPEN, CUE_EVENT_CLOSE,
            CUE_VICTORY, CUE_DEFEAT,
            CUE_AMBIENT_SCAVENGE, CUE_AMBIENT_BUNKER, CUE_MUSIC_BUNKER,
        };

        // ─── Volume buses ────────────────────────────────────────────────────────────────
        public enum Bus { Master, Sfx, Music, Ambient }

        private const string PrefKeyPrefix = "oblast.audio.";

        // ─── Singleton ───────────────────────────────────────────────────────────────────
        public static AudioManager Instance { get; private set; }

        [Header("Voices")]
        [Tooltip("Concurrent 2D one-shots. Beyond this the oldest voice is stolen.")]
        [SerializeField] private int flatVoiceCount = 8;

        [Tooltip("Concurrent positioned one-shots (footsteps, pickups in the world).")]
        [SerializeField] private int spatialVoiceCount = 10;

        [Tooltip("Metres at which a positioned one-shot has fallen silent.")]
        [SerializeField] private float spatialMaxDistance = 26f;

        [Header("Mix")]
        [SerializeField, Range(0f, 1f)] private float masterVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
        [SerializeField, Range(0f, 1f)] private float musicVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float ambientVolume = 0.85f;

        [Header("Emission siren")]
        [Tooltip("Seconds of fade when the siren starts, escalates, or stops.")]
        [SerializeField] private float sirenFadeSeconds = 0.5f;

        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();
        private AudioSource[] _flatVoices;
        private AudioSource[] _spatialVoices;
        private int _nextFlat;
        private int _nextSpatial;

        private AudioSource _ambientSource;
        private AudioSource _musicSource;
        private AudioSource _sirenSource;

        private string _ambientCue;
        private string _sirenCue;
        private float _sirenTargetVolume;
        private bool _subscribed;

        // ─── Boot ────────────────────────────────────────────────────────────────────────

        /// <summary>
        /// Brings the manager up if it is not already running, and returns it. Idempotent — safe to call
        /// from Bootstrap on every scene load, and safe to call from a test that boots the layer alone.
        /// </summary>
        public static AudioManager EnsureExists()
        {
            if (Instance != null) return Instance;

            var go = new GameObject("AudioManager");
            var manager = go.AddComponent<AudioManager>();
            // Awake has already run by the time AddComponent returns, so Instance is set here.
            return manager;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            LoadBusVolumes();
            BuildVoices();
            WarmClips();
            Subscribe();
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (Instance == this) Instance = null;
        }

        private void BuildVoices()
        {
            _flatVoices = new AudioSource[Mathf.Max(1, flatVoiceCount)];
            for (int i = 0; i < _flatVoices.Length; i++)
                _flatVoices[i] = CreateSource("Voice_Flat_" + i, spatial: false, loop: false);

            _spatialVoices = new AudioSource[Mathf.Max(1, spatialVoiceCount)];
            for (int i = 0; i < _spatialVoices.Length; i++)
                _spatialVoices[i] = CreateSource("Voice_3D_" + i, spatial: true, loop: false);

            _ambientSource = CreateSource("Ambient", spatial: false, loop: true);
            _musicSource = CreateSource("Music", spatial: false, loop: true);
            _sirenSource = CreateSource("Siren", spatial: false, loop: true);
        }

        private AudioSource CreateSource(string name, bool spatial, bool loop)
        {
            var child = new GameObject(name);
            child.transform.SetParent(transform, false);

            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 1.5f;
            source.maxDistance = spatialMaxDistance;
            source.dopplerLevel = 0f;    // a 60-second sprint would otherwise pitch-bend every footstep
            return source;
        }

        /// <summary>
        /// Bakes every cue up front. Doing this lazily would put a multi-millisecond synthesis spike on
        /// the first footstep and the first pickup — i.e. exactly during the 60 seconds where a frame
        /// drop is most expensive.
        /// </summary>
        private void WarmClips()
        {
            float startedAt = Time.realtimeSinceStartup;
            int built = 0, bytes = 0;

            for (int i = 0; i < AllCues.Length; i++)
            {
                var clip = ProceduralSfx.Build(AllCues[i]);
                if (clip == null) continue;
                _clips[AllCues[i]] = clip;
                built++;
                bytes += clip.samples * 4;
            }

            Debug.Log($"[AudioManager] Baked {built}/{AllCues.Length} procedural cues in " +
                      $"{(Time.realtimeSinceStartup - startedAt) * 1000f:0} ms ({bytes / 1024f / 1024f:0.0} MB PCM).");
        }

        // ─── Static convenience API ──────────────────────────────────────────────────────

        public static void Play(string cue, float volume = 1f, float pitch = 1f)
        {
            if (Instance != null) Instance.PlayFlat(cue, volume, pitch);
        }

        public static void Play3D(string cue, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            if (Instance != null) Instance.PlaySpatial(cue, position, volume, pitch);
        }

        // ─── Playback ────────────────────────────────────────────────────────────────────

        public void PlayFlat(string cue, float volume = 1f, float pitch = 1f)
        {
            var clip = Resolve(cue);
            if (clip == null) return;

            var voice = _flatVoices[_nextFlat];
            _nextFlat = (_nextFlat + 1) % _flatVoices.Length;

            voice.pitch = pitch;
            voice.volume = Mathf.Clamp01(volume) * BusGain(Bus.Sfx);
            voice.PlayOneShot(clip, 1f);
        }

        public void PlaySpatial(string cue, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            var clip = Resolve(cue);
            if (clip == null) return;

            var voice = _spatialVoices[_nextSpatial];
            _nextSpatial = (_nextSpatial + 1) % _spatialVoices.Length;

            voice.transform.position = position;
            voice.pitch = pitch;
            voice.volume = Mathf.Clamp01(volume) * BusGain(Bus.Sfx);
            voice.PlayOneShot(clip, 1f);
        }

        /// <summary>Starts (or switches to) an ambient loop. Passing the cue already playing is a no-op.</summary>
        public void PlayAmbient(string cue)
        {
            if (_ambientCue == cue && _ambientSource.isPlaying) return;

            var clip = Resolve(cue);
            if (clip == null) return;

            _ambientCue = cue;
            _ambientSource.clip = clip;
            _ambientSource.volume = BusGain(Bus.Ambient);
            _ambientSource.Play();
        }

        public void StopAmbient()
        {
            _ambientCue = null;
            _ambientSource.Stop();
        }

        /// <summary>Starts the bunker drone. Idempotent while it is already running.</summary>
        public void PlayMusic(string cue)
        {
            var clip = Resolve(cue);
            if (clip == null) return;
            if (_musicSource.clip == clip && _musicSource.isPlaying) return;

            _musicSource.clip = clip;
            _musicSource.volume = BusGain(Bus.Music);
            _musicSource.Play();
        }

        public void StopMusic()
        {
            _musicSource.Stop();
        }

        /// <summary>
        /// Transposes the bunker drone. <paramref name="semitones"/> is negative to darken; the drone is
        /// the only music in the game, so this is how the room reacts to a run going badly.
        /// </summary>
        public void SetMusicTranspose(float semitones)
        {
            _musicSource.pitch = Mathf.Pow(2f, Mathf.Clamp(semitones, -12f, 12f) / 12f);
        }

        /// <summary>
        /// Cross-fades the emission siren to <paramref name="cue"/>, or fades it out when
        /// <paramref name="cue"/> is null. Called from the timer handler, so it is driven off whole
        /// seconds and never restarts a siren that is already the right one.
        /// </summary>
        public void SetSiren(string cue)
        {
            if (_sirenCue == cue) return;
            _sirenCue = cue;

            if (cue == null)
            {
                _sirenTargetVolume = 0f;
                return;
            }

            var clip = Resolve(cue);
            if (clip == null) return;

            // A hard swap mid-sweep is audible as a click, but the two sirens are different lengths so
            // there is no sample-aligned handoff to make. Restarting at zero volume and ramping up is
            // the honest compromise: the escalation reads as the siren changing character.
            _sirenSource.clip = clip;
            _sirenSource.volume = 0f;
            _sirenSource.Play();
            _sirenTargetVolume = 1f;
        }

        private void Update()
        {
            if (_sirenSource == null) return;

            float target = _sirenTargetVolume * BusGain(Bus.Sfx);
            float step = sirenFadeSeconds <= 0f
                ? 1f
                : Time.unscaledDeltaTime / sirenFadeSeconds;

            _sirenSource.volume = Mathf.MoveTowards(_sirenSource.volume, target, step);
            if (_sirenSource.volume <= 0f && _sirenTargetVolume <= 0f && _sirenSource.isPlaying)
                _sirenSource.Stop();
        }

        private AudioClip Resolve(string cue)
        {
            if (string.IsNullOrEmpty(cue)) return null;

            AudioClip clip;
            if (_clips.TryGetValue(cue, out clip)) return clip;

            // Not in the warm set — bake it now rather than dropping the sound. This is the path a cue
            // added to the switch but not to AllCues takes, so it also logs.
            clip = ProceduralSfx.Build(cue);
            if (clip == null) return null;

            Debug.LogWarning($"[AudioManager] Cue '{cue}' was baked on demand — add it to AllCues " +
                             "so it is warmed at boot instead.");
            _clips[cue] = clip;
            return clip;
        }

        // ─── Buses ───────────────────────────────────────────────────────────────────────

        public float GetBusVolume(Bus bus)
        {
            switch (bus)
            {
                case Bus.Sfx: return sfxVolume;
                case Bus.Music: return musicVolume;
                case Bus.Ambient: return ambientVolume;
                default: return masterVolume;
            }
        }

        /// <summary>Sets a bus and persists it. Live sources pick the new level up immediately.</summary>
        public void SetBusVolume(Bus bus, float value)
        {
            value = Mathf.Clamp01(value);
            switch (bus)
            {
                case Bus.Sfx: sfxVolume = value; break;
                case Bus.Music: musicVolume = value; break;
                case Bus.Ambient: ambientVolume = value; break;
                default: masterVolume = value; break;
            }

            PlayerPrefs.SetFloat(PrefKeyPrefix + bus, value);
            PlayerPrefs.Save();

            if (_ambientSource != null) _ambientSource.volume = BusGain(Bus.Ambient);
            if (_musicSource != null) _musicSource.volume = BusGain(Bus.Music);
        }

        private float BusGain(Bus bus)
        {
            return masterVolume * GetBusVolume(bus);
        }

        private void LoadBusVolumes()
        {
            masterVolume = PlayerPrefs.GetFloat(PrefKeyPrefix + Bus.Master, masterVolume);
            sfxVolume = PlayerPrefs.GetFloat(PrefKeyPrefix + Bus.Sfx, sfxVolume);
            musicVolume = PlayerPrefs.GetFloat(PrefKeyPrefix + Bus.Music, musicVolume);
            ambientVolume = PlayerPrefs.GetFloat(PrefKeyPrefix + Bus.Ambient, ambientVolume);
        }

        // ─── EventBus wiring ─────────────────────────────────────────────────────────────

        private void Subscribe()
        {
            if (_subscribed) return;
            _subscribed = true;

            EventBus.Subscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Subscribe<RunEndedEvent>(OnRunEnded);
            EventBus.Subscribe<ScavengeTimerTickEvent>(OnTimerTick);
            EventBus.Subscribe<ScavengeTimerExpiredEvent>(OnTimerExpired);
            EventBus.Subscribe<ScavengePickupRejectedEvent>(OnPickupRejected);
            EventBus.Subscribe<CrewRescuedEvent>(OnCrewRescued);
            EventBus.Subscribe<DayAdvancedEvent>(OnDayAdvanced);
            EventBus.Subscribe<EventPresentedEvent>(OnEventPresented);
            EventBus.Subscribe<EventResolvedEvent>(OnEventResolved);
        }

        private void Unsubscribe()
        {
            if (!_subscribed) return;
            _subscribed = false;

            EventBus.Unsubscribe<GameStateChangedEvent>(OnGameStateChanged);
            EventBus.Unsubscribe<RunEndedEvent>(OnRunEnded);
            EventBus.Unsubscribe<ScavengeTimerTickEvent>(OnTimerTick);
            EventBus.Unsubscribe<ScavengeTimerExpiredEvent>(OnTimerExpired);
            EventBus.Unsubscribe<ScavengePickupRejectedEvent>(OnPickupRejected);
            EventBus.Unsubscribe<CrewRescuedEvent>(OnCrewRescued);
            EventBus.Unsubscribe<DayAdvancedEvent>(OnDayAdvanced);
            EventBus.Unsubscribe<EventPresentedEvent>(OnEventPresented);
            EventBus.Unsubscribe<EventResolvedEvent>(OnEventResolved);
        }

        /// <summary>
        /// Ambience and music follow the phase, so no state class has to remember to start or stop them.
        /// The scavenge ambience is deliberately killed on the way out even though the emission hit is
        /// still ringing — the door has closed.
        /// </summary>
        private void OnGameStateChanged(GameStateChangedEvent e)
        {
            switch (e.Current)
            {
                case GameState.ScavengePhase3D:
                    SetSiren(null);
                    StopMusic();
                    PlayAmbient(CUE_AMBIENT_SCAVENGE);
                    break;

                case GameState.SurvivalPhase2D:
                    SetSiren(null);
                    PlayAmbient(CUE_AMBIENT_BUNKER);
                    PlayMusic(CUE_MUSIC_BUNKER);
                    break;

                case GameState.TransitionCutscene:
                    SetSiren(null);
                    StopAmbient();
                    break;

                default:
                    SetSiren(null);
                    StopAmbient();
                    StopMusic();
                    break;
            }
        }

        /// <summary>
        /// Escalates the siren on the same thresholds the HUD colours use. The tick event fires once per
        /// whole second, which is all the resolution a two-stage siren needs.
        /// </summary>
        private void OnTimerTick(ScavengeTimerTickEvent e)
        {
            if (e.SecondsRemaining <= BalanceConstants.SCAVENGE_TIMER_CRITICAL_THRESHOLD)
                SetSiren(CUE_EMISSION_CRITICAL);
            else if (e.SecondsRemaining <= BalanceConstants.SCAVENGE_TIMER_WARNING_THRESHOLD)
                SetSiren(CUE_EMISSION_WARN);
            else
                SetSiren(null);
        }

        private void OnTimerExpired(ScavengeTimerExpiredEvent e)
        {
            SetSiren(null);
            PlayFlat(CUE_EMISSION_HIT);
        }

        /// <summary>
        /// The run-end stingers, keyed off the reason the run closed.
        ///
        /// <para>Driven from <see cref="RunEndedEvent"/> rather than from the five run-end state classes
        /// the brief named. GameManager raises this exactly once per run, at the moment of closure, and
        /// it already carries the outcome — so one subscription covers all four victories and both
        /// failures, and an ending added later gets its stinger for free instead of being the one that
        /// forgot to play it. <c>Quit</c> and <c>Extracted</c> are deliberately silent: neither is a
        /// verdict, and stinging a voluntary withdrawal as a defeat would tell the player they lost when
        /// they did not.</para>
        /// </summary>
        private void OnRunEnded(RunEndedEvent e)
        {
            switch (e.Reason)
            {
                case RunEndReason.VictoryStabilization:
                case RunEndReason.VictoryRelief:
                case RunEndReason.VictoryAdaptation:
                case RunEndReason.VictoryIndependent:
                    PlayFlat(CUE_VICTORY);
                    break;

                case RunEndReason.AllCrewDead:
                case RunEndReason.BunkerBreach:
                    PlayFlat(CUE_DEFEAT);
                    break;
            }
        }

        private void OnPickupRejected(ScavengePickupRejectedEvent e) => PlayFlat(CUE_PICKUP_REJECTED);

        private void OnCrewRescued(CrewRescuedEvent e) => PlayFlat(CUE_PICKUP_CREW);

        /// <summary>
        /// The day chime, and the one place the music reacts to the state of the run: the drone drops as
        /// the bunker's radiation pool climbs. Read on the day tick rather than every frame because the
        /// pool only moves on a day tick, and read straight off RunData because this is a read — the
        /// manager-only rule in CLAUDE.md §6 governs mutation.
        /// </summary>
        private void OnDayAdvanced(DayAdvancedEvent e)
        {
            PlayFlat(CUE_DAY_ADVANCE);

            var run = GameManager.Instance != null ? GameManager.Instance.CurrentRun : null;
            if (run == null) return;

            float contamination = Mathf.Clamp01(run.bunkerRadiationPool /
                                                (float)BalanceConstants.CREW_RADIATION_MAX);
            SetMusicTranspose(Mathf.Lerp(0f, -MusicMaxTransposeSemitones, contamination));
        }

        /// <summary>
        /// How far down the drone can sag at a fully contaminated bunker, in semitones. Five is a fourth:
        /// far enough that a long run sounds different from a fresh one, close enough that it still reads
        /// as the same room rather than as a bug in the audio.
        /// </summary>
        private const float MusicMaxTransposeSemitones = 5f;

        private void OnEventPresented(EventPresentedEvent e) => PlayFlat(CUE_EVENT_OPEN);

        private void OnEventResolved(EventResolvedEvent e) => PlayFlat(CUE_EVENT_CLOSE);
    }
}
