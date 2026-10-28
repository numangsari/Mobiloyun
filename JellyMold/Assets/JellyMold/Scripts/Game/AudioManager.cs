using System;
using System.Collections.Generic;
using JellyMold.Procedural;
using UnityEngine;

namespace JellyMold.Game
{
    /// <summary>Plays the procedurally generated sound effects through a small pool of sources.</summary>
    public sealed class AudioManager : MonoBehaviour
    {
        private const int SampleRate = 22050;
        private const int Voices = 8;

        private readonly Dictionary<SoundId, AudioClip> clips = new Dictionary<SoundId, AudioClip>();
        private readonly List<AudioSource> sources = new List<AudioSource>();
        private int nextVoice;
        private float lastSquish;

        public bool Muted { get; set; }

        public static AudioManager Create(Transform parent)
        {
            var go = new GameObject("Audio");
            go.transform.SetParent(parent, false);
            var audio = go.AddComponent<AudioManager>();
            foreach (SoundId id in Enum.GetValues(typeof(SoundId)))
            {
                float[] data = SoundSynth.Generate(id, SampleRate);
                AudioClip clip = AudioClip.Create("sfx_" + id, data.Length, 1, SampleRate, false);
                clip.SetData(data, 0);
                audio.clips[id] = clip;
            }
            for (int i = 0; i < Voices; i++)
            {
                AudioSource s = go.AddComponent<AudioSource>();
                s.playOnAwake = false;
                s.spatialBlend = 0f;
                audio.sources.Add(s);
            }
            return audio;
        }

        public void Play(SoundId id, float pitch = 1f, float volume = 1f)
        {
            if (Muted || !clips.TryGetValue(id, out AudioClip clip)) return;
            AudioSource s = sources[nextVoice];
            nextVoice = (nextVoice + 1) % sources.Count;
            s.pitch = pitch;
            s.volume = volume;
            s.clip = clip;
            s.Play();
        }

        /// <summary>Landing squish; rate-limited so a press moving six jellies does not get harsh.</summary>
        public void PlaySquish()
        {
            if (Time.unscaledTime - lastSquish < 0.045f) return;
            lastSquish = Time.unscaledTime;
            Play(SoundId.Squish, UnityEngine.Random.Range(0.92f, 1.12f), 0.8f);
        }

        /// <summary>Each fill within one press climbs the pentatonic scale.</summary>
        public void PlayFill(int chainIndex)
        {
            int step = Mathf.Clamp(chainIndex, 0, 4);
            Play(SoundId.Fill0 + step, 1f, 0.9f);
        }
    }
}
