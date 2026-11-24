using System.Collections.Generic;
using UnityEngine;

namespace AbyssalWarden
{
    // =========================================================
    // WARDEN MUSIC
    // =========================================================
    // Boss music director. While any reason is active ("boss",
    // "ending", "prologue") the boss track plays and the normal
    // background music (the BGM object, or any long looping 2D
    // track) is faded out and paused; when the last reason ends the
    // boss track fades out and the BGM resumes where it stopped.
    // =========================================================
    public class WardenMusic : MonoBehaviour
    {
        const float FadeSeconds = 1.6f;

        static WardenMusic instance;
        static readonly HashSet<string> reasons = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() { reasons.Clear(); instance = null; } // play mode without domain reload

        AudioSource boss;
        float bossVolume = .8f;
        readonly Dictionary<AudioSource, float> muted = new(); // BGM source -> its original volume

        /// <summary> Turns one reason for the boss music on or off. The clip is remembered from the first call that passes one. </summary>
        public static void Set(string reason, bool on, AudioClip clip = null, float volume = .8f)
        {
            bool changed = on ? reasons.Add(reason) : reasons.Remove(reason);
            if (!changed && (clip == null || instance != null)) return;
            Instance.Configure(clip, volume);
        }

        public static bool Playing => reasons.Count > 0;

        static WardenMusic Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("Boss Music");
                    instance = go.AddComponent<WardenMusic>();
                    instance.boss = go.AddComponent<AudioSource>();
                    instance.boss.loop = true;
                    instance.boss.playOnAwake = false;
                    instance.boss.spatialBlend = 0f;
                    instance.boss.volume = 0f;
                    instance.boss.priority = 0;
                }
                return instance;
            }
        }

        void Configure(AudioClip clip, float volume)
        {
            if (clip != null && boss.clip != clip) boss.clip = clip;
            bossVolume = volume;
        }

        void Update()
        {
            bool on = reasons.Count > 0 && boss.clip != null;
            float step = Time.unscaledDeltaTime / FadeSeconds;

            if (on)
            {
                if (!boss.isPlaying) { boss.volume = 0f; boss.Play(); }
                boss.volume = Mathf.MoveTowards(boss.volume, bossVolume, step * bossVolume);
                MuteBackgroundMusic(step);
            }
            else
            {
                if (boss.isPlaying)
                {
                    boss.volume = Mathf.MoveTowards(boss.volume, 0f, step * bossVolume);
                    if (boss.volume <= 0f) boss.Stop();
                }
                RestoreBackgroundMusic(step);
            }
        }

        void MuteBackgroundMusic(float step)
        {
            foreach (AudioSource source in FindObjectsByType<AudioSource>(FindObjectsSortMode.None))
            {
                if (source == boss || muted.ContainsKey(source) || !source.isPlaying || !IsBackgroundMusic(source)) continue;
                muted[source] = source.volume;
            }
            foreach (var pair in muted)
            {
                AudioSource source = pair.Key;
                if (source == null || !source.isPlaying) continue;
                source.volume = Mathf.MoveTowards(source.volume, 0f, step * Mathf.Max(.01f, pair.Value));
                if (source.volume <= 0f) source.Pause();
            }
        }

        void RestoreBackgroundMusic(float step)
        {
            if (muted.Count == 0) return;
            var done = new List<AudioSource>();
            foreach (var pair in muted)
            {
                AudioSource source = pair.Key;
                if (source == null) { done.Add(source); continue; }
                if (!source.isPlaying) source.UnPause();
                source.volume = Mathf.MoveTowards(source.volume, pair.Value, step * Mathf.Max(.01f, pair.Value));
                if (Mathf.Approximately(source.volume, pair.Value)) done.Add(source);
            }
            foreach (var source in done) muted.Remove(source);
        }

        // 2D looping music: the scene's "BGM" object, or any long looping 2D track (ambience is 3D and left alone).
        static bool IsBackgroundMusic(AudioSource source)
        {
            if (!source.loop || source.spatialBlend > .5f) return false;
            string name = source.gameObject.name;
            if (name.IndexOf("BGM", System.StringComparison.OrdinalIgnoreCase) >= 0 || name.IndexOf("Music", System.StringComparison.OrdinalIgnoreCase) >= 0) return true;
            return source.clip != null && source.clip.length > 45f;
        }
    }
}
