using UnityEngine;
#if UNITY_IOS && !UNITY_EDITOR
using System.Runtime.InteropServices;
#endif

namespace Game.Runtime
{
    public enum SfxId
    {
        Hit,
        Skill,
        Victory,
        Defeat,
        MenuTap,
    }

    /// Narration, music and sfx sources on the bootstrap object (ARCHITECTURE §4, §14.6); only sfx plays in M1.
    /// On iOS the native plugin puts AVAudioSession in the playback category so the game is heard with the silent
    /// switch on (§13.9). It is re-applied on focus and on resume because Unity re-applies its own category after
    /// interruptions. No mixer, no pitch variation, no serialized fields.
    public sealed class AudioService : MonoBehaviour
    {
        const string Tag = "[AudioService] ";

        AudioSource narration = null!;
        AudioSource music = null!;
        AudioSource sfx = null!;
        AudioClip[] clips = null!;
        bool narrationMuted;
        bool firstPlayLogged;

#if UNITY_IOS && !UNITY_EDITOR
        [DllImport("__Internal")] static extern void SQ_AudioSessionPlayback();
#endif

        public static AudioService Create(GameObject host) => host.AddComponent<AudioService>();

        /// M3's settings toggle; mutes the narration source so lines queued later stay silent.
        public bool NarrationMuted
        {
            get => narrationMuted;
            set
            {
                narrationMuted = value;
                narration.mute = value;
            }
        }

        public void Play(SfxId id)
        {
            if (!firstPlayLogged)
            {
                firstPlayLogged = true;
                Debug.Log(Tag + "first Play: " + id);
            }
            sfx.PlayOneShot(clips[(int)id]);
        }

        void Awake()
        {
            narration = AddSource(false);
            music = AddSource(true);
            sfx = AddSource(false);
            clips = PlaceholderSfx.Build();
            ApplyPlaybackCategory();
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus)
                ApplyPlaybackCategory();
        }

        void OnApplicationPause(bool paused)
        {
            if (!paused)
                ApplyPlaybackCategory();
        }

        AudioSource AddSource(bool loop)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            return source;
        }

        static void ApplyPlaybackCategory()
        {
#if UNITY_IOS && !UNITY_EDITOR
            SQ_AudioSessionPlayback();
            Debug.Log(Tag + "AVAudioSession playback category applied");
#endif
        }
    }
}
