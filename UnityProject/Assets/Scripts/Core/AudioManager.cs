using System.Collections.Generic;
using UnityEngine;

namespace Ares
{
    /// <summary>
    /// Music + SFX playback. The score reuses the tracks that ship with the art pack's
    /// gramophone prop; the effects are synthesised originals.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public float musicVolume = 0.38f;
        public float sfxVolume = 0.65f;

        private AudioSource[] _sfx;
        private int _sfxCursor;

        private AudioSource _musicA;
        private AudioSource _musicB;
        private bool _aActive;
        private string _musicId = "";
        private float _fadeTarget;
        private float _fadeSpeed = 0.7f;

        private readonly Dictionary<string, AudioClip> _sfxClips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, AudioClip> _musicClips = new Dictionary<string, AudioClip>();

        private static AudioManager _instance;
        public static bool Ready { get { return _instance != null; } }

        private void Awake()
        {
            _instance = this;

            _sfx = new AudioSource[10];
            for (int i = 0; i < 10; i++)
            {
                var go = new GameObject("Sfx" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 0f;
                _sfx[i] = src;
            }

            _musicA = CreateMusicSource("MusicA");
            _musicB = CreateMusicSource("MusicB");

            var sfx = Resources.LoadAll<AudioClip>("Audio/SFX");
            for (int i = 0; i < sfx.Length; i++) _sfxClips[sfx[i].name] = sfx[i];
            var music = Resources.LoadAll<AudioClip>("Audio/Music");
            for (int i = 0; i < music.Length; i++) _musicClips[music[i].name] = music[i];

            Debug.Log("[Ares] Audio ready: " + _sfxClips.Count + " sfx, " + _musicClips.Count + " tracks.");
        }

        private AudioSource CreateMusicSource(string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.loop = true;
            src.volume = 0f;
            src.spatialBlend = 0f;
            return src;
        }

        // ------------------------------------------------------------------ sfx

        public static void PlaySfx(string id, float volume = 1f, float pitchJitter = 0.08f)
        {
            if (_instance == null) return;
            _instance.Play(id, volume, pitchJitter);
        }

        private void Play(string id, float volume, float pitchJitter)
        {
            AudioClip clip;
            if (!_sfxClips.TryGetValue(id, out clip) || clip == null) return;

            var src = _sfx[_sfxCursor];
            _sfxCursor = (_sfxCursor + 1) % _sfx.Length;

            src.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            src.PlayOneShot(clip, Mathf.Clamp01(volume * sfxVolume));
        }

        // ------------------------------------------------------------------ music

        public static void PlayMusic(string id, float fade = 0.9f)
        {
            if (_instance == null) return;
            _instance.SwitchTo(id, fade);
        }

        /// <summary>Per-track trim so a short loud loop cannot bury a longer quiet one.</summary>
        private static float TrackGain(string name)
        {
            switch (name)
            {
                case "music_smith": return 0.45f;
                case "music_merchant": return 0.9f;
                case "music_shelter": return 0.75f;
                case "music_menu": return 0.5f;
                case "music_battle": return 0.7f;
                case "music_battle_alt": return 0.7f;
                case "music_boss": return 0.8f;
                case "music_victory": return 0.85f;
                case "music_defeat": return 0.75f;
                default: return 0.8f;
            }
        }

        private float _trackGain = 1f;

        private void SwitchTo(string id, float fade)
        {
            if (_musicId == id) return;
            AudioClip clip;
            if (!_musicClips.TryGetValue(id, out clip) || clip == null) return;

            _musicId = id;
            _trackGain = TrackGain(id);
            _fadeSpeed = fade > 0.01f ? 1f / fade : 20f;

            var incoming = _aActive ? _musicB : _musicA;
            var outgoing = _aActive ? _musicA : _musicB;

            incoming.clip = clip;
            incoming.volume = 0f;
            incoming.Play();
            _aActive = !_aActive;
            _fadeTarget = 1f;

            if (outgoing.isPlaying) StartCoroutine(FadeOut(outgoing));
        }

        private System.Collections.IEnumerator FadeOut(AudioSource src)
        {
            while (src != null && src.volume > 0.01f)
            {
                src.volume = Mathf.MoveTowards(src.volume, 0f, Time.unscaledDeltaTime * _fadeSpeed * musicVolume);
                yield return null;
            }
            if (src != null) src.Stop();
        }

        private void Update()
        {
            var current = _aActive ? _musicA : _musicB;
            if (current == null) return;
            current.volume = Mathf.MoveTowards(current.volume, musicVolume * _fadeTarget * _trackGain,
                Time.unscaledDeltaTime * _fadeSpeed * musicVolume);
            if (current.clip != null && !current.isPlaying && current.volume > 0.01f) current.Play();
        }

        public static void StopMusic() { if (_instance != null) { _instance._musicA.Stop(); _instance._musicB.Stop(); _instance._musicId = ""; } }
    }
}
