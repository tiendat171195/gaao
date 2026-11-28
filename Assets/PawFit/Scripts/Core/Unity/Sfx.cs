using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using PawFit.Core.Audio;
using UnityEngine;

namespace PawFit.Core.Unity
{
    /// <summary>
    /// Phát âm thanh của cả game (Phase 8.1). Âm tạo bằng code ở <see cref="SoundSynth"/> (C# thuần);
    /// lớp này chỉ đổi mảng mẫu thành AudioClip và phát:
    ///  * tiếng ngắn (12 kênh xoay vòng, mỗi tiếng chỉnh được to nhỏ / cao độ / trái phải);
    ///  * nhạc nền (2 kênh chuyển mượt 1 giây);
    ///  * tiếng lặp (gió, nước hồ, tắm, thở khi ngủ, gừ gừ) — to nhỏ dần, không bật / tắt cụt;
    ///  * nền vườn: gió + lá xào xạc + chim hót ngẫu nhiên.
    /// Mở game là tạo sẵn toàn bộ âm ở luồng nền (không giật); tiếng nào cần trước khi xong thì tạo ngay lúc đó.
    /// Nhạc nền và tiếng bật / tắt riêng (hòm thư).
    /// </summary>
    public static class Sfx
    {
        private static SfxRunner _runner;

        /// <summary>Tiếng (thú, vườn, trận, giao diện, nền gió).</summary>
        public static bool SoundOn
        {
            get => Runner.SoundOn;
            set => Runner.SoundOn = value;
        }

        public static bool MusicOn
        {
            get => Runner.MusicOn;
            set => Runner.MusicOn = value;
        }

        /// <summary>Bắt đầu tạo sẵn âm ở luồng nền (gọi lúc mở game).</summary>
        public static void Prewarm() => _ = Runner;

        public static void Play(SoundId id, float volume = 1f, float pitch = 1f, float pan = 0f) => Runner.Play(id, volume, pitch, pan);

        /// <summary>Như Play nhưng cao độ lệch ngẫu nhiên ±`jitter` (lặp lại nhiều lần không bị nhàm).</summary>
        public static void PlayVaried(SoundId id, float volume = 1f, float jitter = 0.07f) =>
            Runner.Play(id, volume, 1f + UnityEngine.Random.Range(-jitter, jitter), 0f);

        /// <summary>Đổi nhạc nền (chuyển mượt). None = tắt nhạc.</summary>
        public static void Music(MusicId id, float volume = 0.32f) => Runner.SetMusic(id, volume);

        /// <summary>Bật / tắt 1 tiếng lặp với độ to mong muốn (gọi mỗi frame cũng được: chỉ đổi đích, to nhỏ dần).</summary>
        public static void Loop(LoopId id, bool on, float volume = 1f) => Runner.SetLoop(id, on, volume);

        /// <summary>Nền vườn: gió + lá + chim (tắt khi vào màn đấu).</summary>
        public static void Ambience(bool on) => Runner.Ambience = on;

        private static SfxRunner Runner
        {
            get
            {
                if (_runner != null) return _runner;
                var go = new GameObject("PawFit_Audio");
                UnityEngine.Object.DontDestroyOnLoad(go);
                _runner = go.AddComponent<SfxRunner>();
                return _runner;
            }
        }
    }

    /// <summary>Phần chạy của <see cref="Sfx"/> (1 GameObject ẩn, sống suốt game).</summary>
    public sealed class SfxRunner : MonoBehaviour
    {
        private const int SoundKey = 0x10000, LoopKey = 0x20000, MusicKey = 0x30000;
        private const float MusicFadeSeconds = 1.2f;

        private readonly Dictionary<int, AudioClip> _clips = new Dictionary<int, AudioClip>();
        private readonly ConcurrentQueue<KeyValuePair<int, float[]>> _ready = new ConcurrentQueue<KeyValuePair<int, float[]>>();
        private readonly AudioSource[] _shots = new AudioSource[12];
        private readonly Dictionary<LoopId, AudioSource> _loops = new Dictionary<LoopId, AudioSource>();
        private readonly Dictionary<LoopId, float> _loopTarget = new Dictionary<LoopId, float>();
        private int _next;
        private AudioSource _musicA, _musicB;
        private MusicId _music = MusicId.None, _wanted = MusicId.None;
        private float _musicVolume = 0.32f, _fade = 1f;
        private float _birdTimer = 5f;
        private bool _soundOn = true, _musicOn = true;

        public bool Ambience { get; set; }

        public bool SoundOn
        {
            get => _soundOn;
            set
            {
                _soundOn = value;
                if (!value)
                    foreach (AudioSource s in _shots)
                        if (s != null) s.Stop();
            }
        }

        public bool MusicOn
        {
            get => _musicOn;
            set => _musicOn = value;
        }

        private void Awake()
        {
            for (int i = 0; i < _shots.Length; i++) _shots[i] = NewSource(false);
            _musicA = NewSource(true);
            _musicB = NewSource(true);
            // tạo sẵn ở luồng nền: nền gió + nhạc vườn trước, tiếng ngắn, rồi các tiếng lặp khác, nhạc trận sau cùng
            Task.Run(() =>
            {
                try
                {
                    Enqueue(LoopKey + (int)LoopId.Wind, SoundSynth.Loop(LoopId.Wind));
                    Enqueue(MusicKey + (int)MusicId.Garden, SoundSynth.Music(MusicId.Garden));
                    foreach (SoundId id in (SoundId[])Enum.GetValues(typeof(SoundId))) Enqueue(SoundKey + (int)id, SoundSynth.Sound(id));
                    foreach (LoopId id in (LoopId[])Enum.GetValues(typeof(LoopId)))
                        if (id != LoopId.Wind) Enqueue(LoopKey + (int)id, SoundSynth.Loop(id));
                    Enqueue(MusicKey + (int)MusicId.Battle, SoundSynth.Music(MusicId.Battle));
                }
                catch (Exception)
                {
                    // tạo nền lỗi: tiếng nào cần thì tạo ngay lúc cần (Get)
                }
            });
        }

        private void Enqueue(int key, float[] data) => _ready.Enqueue(new KeyValuePair<int, float[]>(key, data));

        private AudioSource NewSource(bool loop)
        {
            AudioSource s = gameObject.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            s.loop = loop;
            return s;
        }

        public void Play(SoundId id, float volume, float pitch, float pan)
        {
            if (!_soundOn || volume <= 0.001f) return;
            AudioClip clip = Get(SoundKey + (int)id, () => SoundSynth.Sound(id), id.ToString());
            AudioSource s = _shots[_next];
            _next = (_next + 1) % _shots.Length;
            s.Stop();
            s.clip = clip;
            s.volume = Mathf.Clamp01(volume);
            s.pitch = Mathf.Clamp(pitch, 0.5f, 2f);
            s.panStereo = Mathf.Clamp(pan, -1f, 1f);
            s.Play();
        }

        public void SetMusic(MusicId id, float volume)
        {
            _wanted = id;
            _musicVolume = volume;
        }

        public void SetLoop(LoopId id, bool on, float volume)
        {
            float target = on ? Mathf.Clamp01(volume) : 0f;
            _loopTarget[id] = target;
            if (target <= 0f || _loops.ContainsKey(id)) return;
            AudioSource s = NewSource(true);
            s.clip = Get(LoopKey + (int)id, () => SoundSynth.Loop(id), id.ToString());
            s.volume = 0f;
            s.Play();
            _loops[id] = s;
        }

        /// <summary>Clip đã có thì trả ngay; chưa có thì tạo luôn trên luồng chính (tiếng ngắn: dưới 1 ms).</summary>
        private AudioClip Get(int key, Func<float[]> render, string name)
        {
            if (_clips.TryGetValue(key, out AudioClip c)) return c;
            c = ToClip(name, render());
            _clips[key] = c;
            return c;
        }

        private static AudioClip ToClip(string name, float[] data)
        {
            AudioClip clip = AudioClip.Create("pawfit_" + name, data.Length, 1, SoundSynth.Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;   // khựng hình / chậm lại trong trận không làm âm thanh khựng theo
            for (int k = 0; k < 4 && _ready.TryDequeue(out KeyValuePair<int, float[]> item); k++)
                if (!_clips.ContainsKey(item.Key)) _clips[item.Key] = ToClip("bg_" + item.Key.ToString("X"), item.Value);
            UpdateMusic(dt);
            UpdateLoops(dt);
            UpdateAmbience(dt);
        }

        /// <summary>Đổi bài: bài cũ nhỏ dần, bài mới (khi đã tạo xong) to dần. Tắt nhạc thì nhỏ về 0.</summary>
        private void UpdateMusic(float dt)
        {
            if (_wanted != _music)
            {
                bool ready = _wanted == MusicId.None || _clips.ContainsKey(MusicKey + (int)_wanted);
                if (ready)
                {
                    AudioSource old = _musicA;
                    _musicA = _musicB;
                    _musicB = old;   // B = bài cũ đang nhỏ dần
                    _music = _wanted;
                    if (_music != MusicId.None)
                    {
                        _musicA.clip = _clips[MusicKey + (int)_music];
                        _musicA.volume = 0f;
                        _musicA.Play();
                    }
                    _fade = 0f;
                }
            }
            _fade = Mathf.Min(1f, _fade + dt / MusicFadeSeconds);
            float target = _musicOn && _music != MusicId.None ? _musicVolume : 0f;
            _musicA.volume = Mathf.MoveTowards(_musicA.volume, target, dt / MusicFadeSeconds);
            _musicB.volume = Mathf.MoveTowards(_musicB.volume, 0f, dt / MusicFadeSeconds);
            if (_musicB.isPlaying && _musicB.volume <= 0.001f) _musicB.Stop();
        }

        private void UpdateLoops(float dt)
        {
            float ambienceWind = Ambience ? 0.22f : 0f;
            _loopTarget[LoopId.Wind] = ambienceWind;
            if (ambienceWind > 0f && !_loops.ContainsKey(LoopId.Wind) && _clips.ContainsKey(LoopKey + (int)LoopId.Wind))
                SetLoop(LoopId.Wind, true, ambienceWind);
            foreach (KeyValuePair<LoopId, AudioSource> kv in _loops)
            {
                float target = _soundOn && _loopTarget.TryGetValue(kv.Key, out float v) ? v : 0f;
                AudioSource s = kv.Value;
                s.volume = Mathf.MoveTowards(s.volume, target, dt * 1.5f);
                if (target > 0f && !s.isPlaying) s.Play();
                else if (target <= 0f && s.volume <= 0.001f && s.isPlaying) s.Pause();
            }
        }

        /// <summary>Chim hót ngẫu nhiên mỗi 4–12 giây, lệch trái / phải cho có cảm giác không gian.</summary>
        private void UpdateAmbience(float dt)
        {
            if (!Ambience || !_soundOn) return;
            _birdTimer -= dt;
            if (_birdTimer > 0f) return;
            _birdTimer = UnityEngine.Random.Range(4f, 12f);
            SoundId bird = UnityEngine.Random.value < 0.4f ? SoundId.BirdA : UnityEngine.Random.value < 0.6f ? SoundId.BirdB : SoundId.BirdC;
            Play(bird, UnityEngine.Random.Range(0.1f, 0.22f), UnityEngine.Random.Range(0.9f, 1.15f), UnityEngine.Random.Range(-0.7f, 0.7f));
        }
    }
}
