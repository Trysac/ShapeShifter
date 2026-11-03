using UnityEngine;

namespace ShapeShifter.Game
{
    /// <summary>Cross-fades the music and plays one-shot effects at the volumes in Settings.</summary>
    public sealed class AudioDirector : MonoBehaviour
    {
        const float MusicLevel = 0.5f;

        AudioSource music;
        AudioSource effects;
        AudioClip wanted;
        float fade = 1f;
        float musicGain = 0.7f;
        float effectsGain = 0.8f;
        bool ducked;

        public AudioClip Playing => music != null ? music.clip : null;

        public void Init()
        {
            music = gameObject.AddComponent<AudioSource>();
            music.loop = true;
            music.playOnAwake = false;
            effects = gameObject.AddComponent<AudioSource>();
            effects.playOnAwake = false;
        }

        public void SetVolumes(float musicVolume, float effectsVolume)
        {
            musicGain = musicVolume;
            effectsGain = effectsVolume;
        }

        public void PlayMusic(AudioClip clip) => wanted = clip;

        public void SetDucked(bool value) => ducked = value;

        public void Play(AudioClip clip, float volume = 1f, float pitchJitter = 0.04f)
        {
            if (clip == null || effects == null || effectsGain <= 0f) return;
            effects.pitch = 1f + Random.Range(-pitchJitter, pitchJitter);
            effects.PlayOneShot(clip, volume * effectsGain);
        }

        void Update()
        {
            if (music == null) return;
            if (music.clip != wanted)
            {
                fade = Mathf.MoveTowards(fade, 0f, Time.unscaledDeltaTime * 2.5f);
                if (fade <= 0f || !music.isPlaying)
                {
                    music.clip = wanted;
                    if (wanted != null) music.Play();
                }
            }
            else
            {
                fade = Mathf.MoveTowards(fade, 1f, Time.unscaledDeltaTime * 1.5f);
            }
            music.volume = MusicLevel * musicGain * fade * (ducked ? 0.35f : 1f);
        }
    }
}
