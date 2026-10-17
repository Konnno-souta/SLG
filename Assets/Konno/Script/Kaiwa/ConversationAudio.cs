using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

namespace Conversation
{
    /// <summary>
    /// BGM・SE の再生管理。ConversationPlayer と同じ GameObject に付ける（無ければ自動で付く）。
    /// 会話以外の場所からも PlayBgm / PlaySe で使える。
    /// </summary>
    public class ConversationAudio : MonoBehaviour
    {
        // =====================================================================
        // 設定
        // =====================================================================

        [Header("データ（空なら ConversationPlayer の Database を使う）")]
        public ConversationDatabase database;

        [Header("出力先（AudioMixer を使う場合）")]
        public AudioMixerGroup bgmMixerGroup;
        public AudioMixerGroup seMixerGroup;

        [Header("音量（オプション画面などから変更する用）")]
        [Range(0f, 1f)] public float masterVolume = 1f;
        [Range(0f, 1f)] public float bgmVolume = 0.8f;
        [Range(0f, 1f)] public float seVolume = 1f;

        [Header("BGM")]
        [Tooltip("フェードイン秒数の初期値（スクリプトで fade= を書かない時）")]
        public float defaultBgmFadeIn = 1f;
        [Tooltip("フェードアウト秒数の初期値")]
        public float defaultBgmFadeOut = 1f;
        [Tooltip("BGM切り替え時に前の曲と重ねる（オフなら前の曲が消えてから次を流す）")]
        public bool crossFade = true;
        [Tooltip("今流れている曲と同じ曲を指定した時、最初から流し直す")]
        public bool restartSameBgm = false;

        [Header("SE")]
        [Tooltip("SEの同時再生数。超えると一番古いSEが止まる")]
        [Range(1, 32)] public int seChannels = 8;
        [Tooltip("SE停止時のフェードアウト秒数の初期値")]
        public float defaultSeFadeOut = 0.1f;

        [Header("SE再生中にBGMを下げる（ダッキング）")]
        public bool duckBgmOnSe = false;
        [Tooltip("下げた時のBGM音量の倍率")]
        [Range(0f, 1f)] public float duckVolume = 0.5f;
        [Tooltip("下げる/戻す時の秒数")]
        public float duckFadeTime = 0.2f;

        [Header("その他")]
        [Tooltip("Time.timeScale の影響を受けない（ポーズ中もフェードが進む）")]
        public bool useUnscaledTime = true;

        // =====================================================================
        // 内部
        // =====================================================================

        class Channel
        {
            public AudioSource src;
            public string name;
            public bool active;
            public float baseVolume = 1f;   // データの音量 × 指定音量
            public float fade = 1f;         // フェード係数
            public float fadeTarget = 1f;
            public float fadeSpeed;         // 1秒あたりの変化量（0以下は即時）
            public bool stopWhenSilent;
            public float delay;             // 再生開始までの待ち
            public int loopStartSample;
            public int loopEndSample;       // 0 ならイントロループ無し
            public float startedAt;
            public bool paused;

            public void Clear()
            {
                active = false;
                paused = false;
                name = null;
                stopWhenSilent = false;
                delay = 0f;
                loopStartSample = loopEndSample = 0;
            }
        }

        Channel bgmA, bgmB, currentBgm;
        readonly List<Channel> seList = new List<Channel>();
        AudioSource uiSource;

        float bgmCommandVolume = 1f, bgmCommandTarget = 1f, bgmCommandSpeed;
        float duckFactor = 1f;
        bool initialized;

        float Dt => useUnscaledTime ? Time.unscaledDeltaTime : Time.deltaTime;

        /// <summary>今流れているBGMの名前（無ければ null）</summary>
        public string CurrentBgmName => currentBgm != null && currentBgm.active ? currentBgm.name : null;

        // =====================================================================
        // 初期化
        // =====================================================================

        void Awake() => Init();

        void Init()
        {
            if (initialized) return;
            initialized = true;

            bgmA = new Channel { src = CreateSource("BGM_A", bgmMixerGroup) };
            bgmB = new Channel { src = CreateSource("BGM_B", bgmMixerGroup) };
            for (int i = 0; i < seChannels; i++)
                seList.Add(new Channel { src = CreateSource("SE_" + i, seMixerGroup) });
            uiSource = CreateSource("UI_SE", seMixerGroup);
        }

        AudioSource CreateSource(string objName, AudioMixerGroup group)
        {
            var go = new GameObject(objName);
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            src.playOnAwake = false;
            src.spatialBlend = 0f;
            src.outputAudioMixerGroup = group;
            return src;
        }

        void Update()
        {
            float dt = Dt;

            // ダッキング
            bool seSounding = false;
            if (duckBgmOnSe)
                foreach (var ch in seList)
                    if (ch.active && ch.delay <= 0f && !ch.src.loop) { seSounding = true; break; }
            float duckTarget = seSounding ? duckVolume : 1f;
            duckFactor = duckFadeTime > 0f
                ? Mathf.MoveTowards(duckFactor, duckTarget, dt / duckFadeTime)
                : duckTarget;

            // @bgm volume による音量変更
            bgmCommandVolume = bgmCommandSpeed > 0f
                ? Mathf.MoveTowards(bgmCommandVolume, bgmCommandTarget, bgmCommandSpeed * dt)
                : bgmCommandTarget;

            float bgmMul = masterVolume * bgmVolume * duckFactor * bgmCommandVolume;
            Tick(bgmA, dt, bgmMul);
            Tick(bgmB, dt, bgmMul);

            float seMul = masterVolume * seVolume;
            foreach (var ch in seList) Tick(ch, dt, seMul);
        }

        void Tick(Channel ch, float dt, float mul)
        {
            if (!ch.active) return;

            // 遅延再生
            if (ch.delay > 0f)
            {
                ch.delay -= dt;
                if (ch.delay > 0f) return;
                ch.src.Play();
            }

            // フェード
            ch.fade = ch.fadeSpeed > 0f
                ? Mathf.MoveTowards(ch.fade, ch.fadeTarget, ch.fadeSpeed * dt)
                : ch.fadeTarget;

            if (ch.stopWhenSilent && ch.fade <= 0f)
            {
                ch.src.Stop();
                ch.Clear();
                return;
            }

            // イントロ付きループ（loopEnd まで来たら loopStart に戻す）
            if (ch.loopEndSample > 0 && ch.src.timeSamples >= ch.loopEndSample)
                ch.src.timeSamples -= ch.loopEndSample - ch.loopStartSample;

            // 鳴り終わった（ループしない音）
            if (!ch.paused && !ch.src.isPlaying && !ch.src.loop && ch.delay <= 0f && Time.unscaledTime - ch.startedAt > 0.05f)
            {
                ch.Clear();
                return;
            }

            ch.src.volume = ch.baseVolume * ch.fade * mul;
        }

        // =====================================================================
        // BGM
        // =====================================================================

        /// <summary>Database に登録した名前でBGMを流す</summary>
        /// <param name="fade">フェードイン秒数（負なら初期値）</param>
        /// <param name="volume">音量倍率</param>
        /// <param name="startTime">再生開始位置（秒。負ならデータの設定）</param>
        /// <param name="pitch">ピッチ倍率</param>
        public void PlayBgm(string bgmName, float fade = -1f, float volume = 1f, float startTime = -1f, float pitch = 1f)
        {
            var entry = database ? database.FindBgm(bgmName) : null;
            if (entry == null || entry.clip == null)
            {
                Debug.LogWarning($"[ConversationAudio] BGM '{bgmName}' が見つかりません");
                return;
            }
            PlayBgmInternal(bgmName, entry.clip, entry.volume * volume, entry.pitch * pitch, entry.loop,
                startTime >= 0f ? startTime : entry.startTime, entry.loopStart, entry.loopEnd, fade);
        }

        /// <summary>AudioClip を直接指定してBGMを流す</summary>
        public void PlayBgm(AudioClip clip, float fade = -1f, float volume = 1f, bool loop = true)
        {
            if (clip == null) return;
            PlayBgmInternal(clip.name, clip, volume, 1f, loop, 0f, 0f, 0f, fade);
        }

        void PlayBgmInternal(string bgmName, AudioClip clip, float volume, float pitch, bool loop,
            float startTime, float loopStart, float loopEnd, float fade)
        {
            Init();
            if (fade < 0f) fade = defaultBgmFadeIn;

            // 同じ曲が流れている
            if (currentBgm != null && currentBgm.active && currentBgm.name == bgmName && !restartSameBgm)
            {
                currentBgm.baseVolume = volume;
                currentBgm.src.pitch = pitch;
                currentBgm.stopWhenSilent = false;
                SetFade(currentBgm, 1f, fade);
                return;
            }

            float waitForOld = 0f;
            if (currentBgm != null && currentBgm.active)
            {
                FadeOutAndStop(currentBgm, fade);
                if (!crossFade) waitForOld = fade;
            }

            var next = currentBgm == bgmA ? bgmB : bgmA;
            if (next.active) { next.src.Stop(); next.Clear(); }

            var src = next.src;
            src.clip = clip;
            src.loop = loop;
            src.pitch = pitch;
            src.time = Mathf.Clamp(startTime, 0f, Mathf.Max(0f, clip.length - 0.01f));

            next.name = bgmName;
            next.active = true;
            next.baseVolume = volume;
            next.startedAt = Time.unscaledTime;
            next.stopWhenSilent = false;
            next.fade = fade > 0f ? 0f : 1f;
            SetFade(next, 1f, fade);

            // イントロ付きループ
            if (loop && loopEnd > loopStart && loopEnd > 0f)
            {
                next.loopStartSample = (int)(loopStart * clip.frequency);
                next.loopEndSample = Mathf.Min((int)(loopEnd * clip.frequency), clip.samples);
            }

            src.volume = 0f;
            next.delay = waitForOld;
            if (waitForOld <= 0f) src.Play();

            currentBgm = next;
        }

        /// <summary>BGMを止める</summary>
        public void StopBgm(float fade = -1f)
        {
            Init();
            if (fade < 0f) fade = defaultBgmFadeOut;
            if (currentBgm != null && currentBgm.active) FadeOutAndStop(currentBgm, fade);
        }

        /// <summary>BGMの音量を一時的に変える（1で元に戻る）</summary>
        public void SetBgmVolume(float volume, float time = 0f)
        {
            bgmCommandTarget = Mathf.Clamp01(volume);
            bgmCommandSpeed = time > 0f ? Mathf.Abs(bgmCommandTarget - bgmCommandVolume) / time : 0f;
        }

        public void PauseBgm()
        {
            if (currentBgm != null && currentBgm.active) { currentBgm.src.Pause(); currentBgm.paused = true; }
        }

        public void ResumeBgm()
        {
            if (currentBgm != null && currentBgm.active) { currentBgm.src.UnPause(); currentBgm.paused = false; }
        }

        // =====================================================================
        // SE
        // =====================================================================

        /// <summary>Database に登録した名前でSEを鳴らす</summary>
        public void PlaySe(string seName, float volume = 1f, float pitch = 1f, float delay = 0f,
            bool? loop = null, float fadeIn = 0f)
        {
            var entry = database ? database.FindSe(seName) : null;
            if (entry == null || entry.clip == null)
            {
                Debug.LogWarning($"[ConversationAudio] SE '{seName}' が見つかりません");
                return;
            }

            float p = entry.pitch * pitch;
            if (entry.pitchRandom > 0f) p *= 1f + Random.Range(-entry.pitchRandom, entry.pitchRandom);
            PlaySeInternal(seName, entry.clip, entry.volume * volume, p, delay, loop ?? entry.loop, fadeIn);
        }

        /// <summary>AudioClip を直接指定してSEを鳴らす</summary>
        public void PlaySe(AudioClip clip, float volume = 1f, float pitch = 1f, float delay = 0f, bool loop = false)
        {
            if (clip == null) return;
            PlaySeInternal(clip.name, clip, volume, pitch, delay, loop, 0f);
        }

        void PlaySeInternal(string seName, AudioClip clip, float volume, float pitch, float delay, bool loop, float fadeIn)
        {
            Init();
            var ch = GetFreeSeChannel();
            var src = ch.src;
            src.Stop();
            src.clip = clip;
            src.loop = loop;
            src.pitch = pitch;
            src.time = 0f;

            ch.Clear();
            ch.name = seName;
            ch.active = true;
            ch.baseVolume = volume;
            ch.startedAt = Time.unscaledTime + Mathf.Max(0f, delay);
            ch.fade = fadeIn > 0f ? 0f : 1f;
            SetFade(ch, 1f, fadeIn);
            ch.delay = delay;
            src.volume = 0f;
            if (delay <= 0f)
            {
                src.volume = volume * ch.fade * masterVolume * seVolume;
                src.Play();
            }
        }

        Channel GetFreeSeChannel()
        {
            Channel oldest = seList[0];
            foreach (var ch in seList)
            {
                if (!ch.active) return ch;
                if (ch.startedAt < oldest.startedAt) oldest = ch;
            }
            return oldest; // 空きが無ければ一番古いSEを止めて使う
        }

        /// <summary>SEを止める（名前を省略すると全部）</summary>
        public void StopSe(string seName = null, float fade = -1f)
        {
            if (fade < 0f) fade = defaultSeFadeOut;
            foreach (var ch in seList)
                if (ch.active && (string.IsNullOrEmpty(seName) || ch.name == seName))
                    FadeOutAndStop(ch, fade);
        }

        public void StopAllSe(float fade = -1f) => StopSe(null, fade);

        /// <summary>指定SEが鳴っているか（名前を省略すると何か1つでも）</summary>
        public bool IsSePlaying(string seName = null)
        {
            foreach (var ch in seList)
                if (ch.active && (string.IsNullOrEmpty(seName) || ch.name == seName))
                    return true;
            return false;
        }

        /// <summary>UI用の効果音（決定音など）。同時に何個鳴っても止まらない</summary>
        public void PlayUi(AudioClip clip, float volume = 1f)
        {
            if (clip == null) return;
            Init();
            uiSource.PlayOneShot(clip, volume * masterVolume * seVolume);
        }

        /// <summary>BGM・SEをすべて止める</summary>
        public void StopAll(float fade = 0f)
        {
            StopBgm(fade);
            StopAllSe(fade);
        }

        // =====================================================================
        // 共通
        // =====================================================================

        static void SetFade(Channel ch, float target, float time)
        {
            ch.fadeTarget = target;
            ch.fadeSpeed = time > 0f ? 1f / time : 0f;
        }

        static void FadeOutAndStop(Channel ch, float time)
        {
            if (time <= 0f)
            {
                ch.src.Stop();
                ch.Clear();
                return;
            }
            ch.stopWhenSilent = true;
            SetFade(ch, 0f, time);
        }
    }
}
