using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NorthStar.Audio
{
    /// <summary>
    /// Central audio service: pooled one-shot SFX (no <c>PlayClipAtPoint</c>), two-source
    /// music crossfade, and master/music/SFX volume control. Subscribes to
    /// <c>ZoneEnteredEvent</c> and auto-crossfades to the zone's <see cref="MusicPlaylist"/>.
    ///
    /// <para>Cross-module communication is EventBus-only — this manager references no other
    /// gameplay module. SFX/music clips and zone playlists are supplied as serialized
    /// ScriptableObject/clip data (CLAUDE rule: data lives in assets, not code).</para>
    ///
    /// <para>Pooling and volume math live in <see cref="AudioSourcePool{T}"/> and
    /// <see cref="VolumeMixer"/> (pure, EditMode-tested); this class is the Unity glue.</para>
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        // ── Serialized config ────────────────────────────────────────────────────

        [Serializable]
        public struct ClipEntry
        {
            [Tooltip("Stable lookup id, e.g. sfx-sword-hit. Lowercase-with-hyphens.")]
            public string clipId;
            public AudioClip clip;
        }

        [Serializable]
        public struct ZonePlaylistEntry
        {
            [Tooltip("zoneId published by ZoneEnteredEvent, e.g. forest-zone-01.")]
            public string zoneId;
            public MusicPlaylist playlist;
        }

        [Header("SFX")]
        [Tooltip("Number of pooled AudioSources for one-shot SFX.")]
        [SerializeField] private int _sfxPoolSize = 20;
        [SerializeField] private ClipEntry[] _sfxClips = Array.Empty<ClipEntry>();

        [Header("Music")]
        [Tooltip("Standalone music tracks resolvable by PlayMusic(trackId,...).")]
        [SerializeField] private ClipEntry[] _musicClips = Array.Empty<ClipEntry>();
        [Tooltip("Default crossfade seconds when a playlist sets none.")]
        [SerializeField] private float _defaultCrossfade = 1.5f;

        [Header("Zone Music")]
        [Tooltip("Maps each zoneId to the playlist crossfaded in on ZoneEnteredEvent.")]
        [SerializeField] private ZonePlaylistEntry[] _zonePlaylists = Array.Empty<ZonePlaylistEntry>();

        // ── Runtime state ────────────────────────────────────────────────────────

        private AudioSourcePool<AudioSource> _sfxPool;
        private readonly VolumeMixer _volume = new VolumeMixer();
        private readonly Dictionary<string, AudioClip> _sfxLookup = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, AudioClip> _musicLookup = new Dictionary<string, AudioClip>();
        private readonly ZonePlaylistTable _zoneTable = new ZonePlaylistTable();

        // Two music sources we ping-pong between for crossfades.
        private AudioSource _musicA;
        private AudioSource _musicB;
        private bool _musicAIsActive = true;
        private Coroutine _crossfadeCo;
        private MusicPlaylist _currentPlaylist;
        private int _playlistIndex;

        /// <summary>The clipId/trackId currently playing as music, or null.</summary>
        public string CurrentMusicId { get; private set; }

        // ── Lifecycle ─────────────────────────────────────────────────────────────

        private void Awake()
        {
            BuildLookups();
            BuildSfxPool();
            BuildMusicSources();
        }

        private void OnEnable()  => EventBus.Subscribe<ZoneEnteredEvent>(OnZoneEntered);
        private void OnDisable() => EventBus.Unsubscribe<ZoneEnteredEvent>(OnZoneEntered);

        // ── Public API (INTERFACE.md) ──────────────────────────────────────────────

        /// <summary>Play a one-shot SFX at a world position from the pooled sources.</summary>
        public void PlaySFX(string clipId, Vector3 worldPosition)
        {
            var clip = ResolveSfx(clipId);
            if (clip == null) return;
            PlayPooled(clip, worldPosition, spatial: true);
        }

        /// <summary>Play a one-shot 2D SFX (no spatialization) from the pooled sources.</summary>
        public void PlaySFX(string clipId)
        {
            var clip = ResolveSfx(clipId);
            if (clip == null) return;
            PlayPooled(clip, Vector3.zero, spatial: false);
        }

        /// <summary>
        /// Crossfade music to the track registered under <paramref name="trackId"/> over
        /// <paramref name="fadeTime"/> seconds. Playing the already-current track is a no-op.
        /// </summary>
        public void PlayMusic(string trackId, float fadeTime)
        {
            if (string.IsNullOrEmpty(trackId)) return;
            if (trackId == CurrentMusicId) return;

            if (!_musicLookup.TryGetValue(trackId, out var clip) || clip == null)
            {
                Debug.LogWarning($"[AudioManager] Unknown music trackId '{trackId}'.");
                return;
            }

            _currentPlaylist = null; // a direct track overrides any active playlist
            CrossfadeTo(clip, trackId, Mathf.Max(0f, fadeTime), loop: true);
        }

        /// <summary>Fade the currently playing music out to silence over <paramref name="fadeTime"/> seconds.</summary>
        public void StopMusic(float fadeTime)
        {
            _currentPlaylist = null;
            CurrentMusicId = null;
            CrossfadeTo(null, null, Mathf.Max(0f, fadeTime), loop: false);
        }

        /// <summary>Set the master volume (0–1). Applied immediately to live music.</summary>
        public void SetMasterVolume(float volume)
        {
            _volume.SetMaster(volume);
            ApplyMusicVolume();
        }

        /// <summary>Set the music volume (0–1). Applied immediately to live music.</summary>
        public void SetMusicVolume(float volume)
        {
            _volume.SetMusic(volume);
            ApplyMusicVolume();
        }

        /// <summary>Set the SFX volume (0–1). Affects subsequently played one-shots.</summary>
        public void SetSFXVolume(float volume) => _volume.SetSfx(volume);

        // ── Zone music ──────────────────────────────────────────────────────────────

        private void OnZoneEntered(ZoneEnteredEvent e)
        {
            var playlist = _zoneTable.ResolveByZone(e.zoneId);
            if (playlist == null) return; // no music registered for this zone
            PlayPlaylist(playlist);
        }

        /// <summary>
        /// Start (or restart) a playlist, crossfading to its first/next track using the
        /// playlist's own crossfade time. Public so other in-module systems can trigger it.
        /// </summary>
        public void PlayPlaylist(MusicPlaylist playlist)
        {
            if (playlist == null || playlist.tracks == null || playlist.tracks.Length == 0) return;
            if (_currentPlaylist == playlist) return; // already on this playlist

            _currentPlaylist = playlist;
            _playlistIndex = playlist.shuffle
                ? UnityEngine.Random.Range(0, playlist.tracks.Length)
                : 0;

            var clip = playlist.tracks[_playlistIndex];
            if (clip == null) return;

            float fade = playlist.crossfadeTime > 0f ? playlist.crossfadeTime : _defaultCrossfade;
            CrossfadeTo(clip, playlist.playlistId, fade, loop: true);
        }

        // ── Internals ─────────────────────────────────────────────────────────────

        private void BuildLookups()
        {
            _sfxLookup.Clear();
            foreach (var entry in _sfxClips)
            {
                if (string.IsNullOrEmpty(entry.clipId) || entry.clip == null) continue;
                _sfxLookup[entry.clipId] = entry.clip;
            }

            _musicLookup.Clear();
            foreach (var entry in _musicClips)
            {
                if (string.IsNullOrEmpty(entry.clipId) || entry.clip == null) continue;
                _musicLookup[entry.clipId] = entry.clip;
            }

            foreach (var entry in _zonePlaylists)
                _zoneTable.Add(entry.zoneId, entry.playlist);
        }

        private void BuildSfxPool()
        {
            int size = Mathf.Max(1, _sfxPoolSize);
            _sfxPool = new AudioSourcePool<AudioSource>(size, i =>
            {
                var go = new GameObject($"SFX_Source_{i}");
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                return src;
            });
        }

        private void BuildMusicSources()
        {
            _musicA = CreateMusicSource("Music_A");
            _musicB = CreateMusicSource("Music_B");
        }

        private AudioSource CreateMusicSource(string label)
        {
            var go = new GameObject(label);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.spatialBlend = 0f; // music is 2D
            src.volume = 0f;
            return src;
        }

        private AudioClip ResolveSfx(string clipId)
        {
            if (string.IsNullOrEmpty(clipId)) return null;
            if (_sfxLookup.TryGetValue(clipId, out var clip)) return clip;
            Debug.LogWarning($"[AudioManager] Unknown SFX clipId '{clipId}'.");
            return null;
        }

        private void PlayPooled(AudioClip clip, Vector3 position, bool spatial)
        {
            if (_sfxPool == null) return;
            var src = _sfxPool.Rent(out int index, out bool stolen);
            if (stolen) src.Stop(); // free the stolen source before reusing it

            src.transform.position = position;
            src.spatialBlend = spatial ? 1f : 0f;
            src.clip = clip;
            src.volume = _volume.EffectiveSfx;
            src.loop = false;
            src.Play();

            StartCoroutine(CoReturnWhenDone(index, _sfxPool.RentStamp(index),
                clip.length / Mathf.Max(0.01f, src.pitch)));
        }

        private IEnumerator CoReturnWhenDone(int index, long stamp, float seconds)
        {
            // Unscaled so SFX still release while the game is paused (timeScale 0).
            yield return new WaitForSecondsRealtime(seconds);
            // Only release if this slot is still on the same rent (not stolen since).
            _sfxPool.ReturnIfCurrent(index, stamp);
        }

        // ── Music crossfade ──────────────────────────────────────────────────────

        private void CrossfadeTo(AudioClip clip, string id, float fadeTime, bool loop)
        {
            if (_crossfadeCo != null) StopCoroutine(_crossfadeCo);

            AudioSource incoming = _musicAIsActive ? _musicB : _musicA;
            AudioSource outgoing = _musicAIsActive ? _musicA : _musicB;

            if (clip != null)
            {
                incoming.clip = clip;
                incoming.loop = loop;
                incoming.volume = 0f;
                incoming.Play();
                CurrentMusicId = id;
                _musicAIsActive = !_musicAIsActive;
            }

            _crossfadeCo = StartCoroutine(CoCrossfade(incoming, outgoing, clip != null, fadeTime));
        }

        private IEnumerator CoCrossfade(AudioSource incoming, AudioSource outgoing, bool fadeInIncoming, float fadeTime)
        {
            float target = _volume.EffectiveMusic;
            float startOut = outgoing != null ? outgoing.volume : 0f;

            if (fadeTime <= 0f)
            {
                if (fadeInIncoming) incoming.volume = target;
                if (outgoing != null) { outgoing.Stop(); outgoing.volume = 0f; }
                _crossfadeCo = null;
                yield break;
            }

            float t = 0f;
            while (t < fadeTime)
            {
                // Unscaled time so music keeps fading while paused.
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / fadeTime);
                if (fadeInIncoming) incoming.volume = Mathf.Lerp(0f, target, k);
                if (outgoing != null) outgoing.volume = Mathf.Lerp(startOut, 0f, k);
                yield return null;
            }

            if (fadeInIncoming) incoming.volume = target;
            if (outgoing != null) { outgoing.Stop(); outgoing.volume = 0f; }
            _crossfadeCo = null;
        }

        private void ApplyMusicVolume()
        {
            // Only the active (currently-audible) source tracks the live volume; the other
            // is either silent or mid-fade and will be set by the crossfade coroutine.
            AudioSource active = _musicAIsActive ? _musicA : _musicB;
            if (active != null && active.isPlaying && _crossfadeCo == null)
                active.volume = _volume.EffectiveMusic;
        }
    }
}
