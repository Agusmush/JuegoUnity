using UnityEngine;
using UnityEngine.Audio;
using System;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance;

    [System.Serializable]
    public class Sound
    {
        public string name;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 0.7f;
        [Range(0.1f, 3f)] public float pitch = 1f;

        // 0 = 2D (Música/Ambiente), 1 = 3D (Explosiones/Pasos)
        [Range(0f, 1f)] public float spatialBlend = 0f; // <--- NUEVO

        public bool loop;

        [HideInInspector] public AudioSource source;
    }

    public Sound[] sounds;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Para que la música no se corte al reiniciar
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Inicializar fuentes de audio
        foreach (Sound s in sounds)
        {
            s.source = gameObject.AddComponent<AudioSource>();
            s.source.clip = s.clip;
            s.source.volume = s.volume;
            s.source.pitch = s.pitch;
            s.source.loop = s.loop;

            s.source.spatialBlend = s.spatialBlend; // <--- ASIGNAR AQUÍ
        }
    }

    public void Play(string name)
    {
        Sound s = Array.Find(sounds, sound => sound.name == name);
        if (s == null)
        {
            Debug.LogWarning("Sonido: " + name + " no encontrado!");
            return;
        }
        s.source.Play();
    }

    public void Stop(string name)
    {
        Sound s = Array.Find(sounds, sound => sound.name == name);
        if (s != null) s.source.Stop();
    }

    // Para cambiar el pitch dinámicamente (Efecto de tensión)
    public void SetPitch(string name, float newPitch)
    {
        Sound s = Array.Find(sounds, sound => sound.name == name);
        if (s != null) s.source.pitch = newPitch;
    }
}