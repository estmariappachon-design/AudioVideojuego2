using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// CENTRO DE CONTROL DE AUDIO.
/// - Al iniciar, SINTETIZA todos los efectos (ProceduralSFX) y genera varias variaciones de cada uno.
/// - Reproduce con un "pool" de AudioSources (evita cortar sonidos).
/// - Control dinámico: pitch y volumen aleatorios, volumen/intensidad según la velocidad del impacto,
///   paneo estéreo calculado según la posición en pantalla, y "ducking" de la música en eventos importantes.
/// Colóquelo en un GameObject vacío "AudioManager" en la PRIMERA escena (persiste entre niveles).
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager I { get; private set; }

    [Header("Mixer (opcional pero recomendado)")]
    public AudioMixer mixer;                 // parámetro expuesto: "MusicVol"
    public AudioMixerGroup sfxGroup;
    public AudioMixerGroup uiGroup;

    [Header("Variación aleatoria")]
    [Range(0f, 0.3f)] public float pitchVariation = 0.08f;
    [Range(0f, 0.3f)] public float volumeVariation = 0.1f;

    [Header("Pool")]
    public int poolSize = 16;

    [Header("Volúmenes por categoría (mezcla)")]
    [Range(0, 1)] public float playerVol = 0.8f;
    [Range(0, 1)] public float worldVol = 0.75f;
    [Range(0, 1)] public float eventVol = 0.9f;

    readonly List<AudioSource> pool = new List<AudioSource>();
    readonly Dictionary<string, AudioClip[]> bank = new Dictionary<string, AudioClip[]>();
    readonly Dictionary<string, float> lastPlayTime = new Dictionary<string, float>();
    int poolIndex;
    Camera cam;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);

        for (int i = 0; i < poolSize; i++)
        {
            var src = new GameObject("SFX_" + i).AddComponent<AudioSource>();
            src.transform.SetParent(transform);
            src.playOnAwake = false;
            src.spatialBlend = 0f;                // 2D: el paneo lo calculamos nosotros
            src.outputAudioMixerGroup = sfxGroup;
            pool.Add(src);
        }
        BuildBank();
    }

    /// Síntesis de todo el banco de sonidos (tarda unos milisegundos al arrancar).
    void BuildBank()
    {
        bank["jump"]      = new[] { ProceduralSFX.Jump(0.92f), ProceduralSFX.Jump(1f), ProceduralSFX.Jump(1.08f), ProceduralSFX.Jump(1.15f) };
        bank["impact_lo"] = new[] { ProceduralSFX.Impact(0.15f), ProceduralSFX.Impact(0.2f) };
        bank["impact_md"] = new[] { ProceduralSFX.Impact(0.45f), ProceduralSFX.Impact(0.55f) };
        bank["impact_hi"] = new[] { ProceduralSFX.Impact(0.85f), ProceduralSFX.Impact(1f) };
        bank["bounce"]    = new[] { ProceduralSFX.Bounce() };
        bank["pickup"]    = new[] { ProceduralSFX.Pickup(0), ProceduralSFX.Pickup(2), ProceduralSFX.Pickup(-1) };
        bank["door_open"] = new[] { ProceduralSFX.Door(true) };
        bank["door_close"]= new[] { ProceduralSFX.Door(false) };
        bank["switch_on"] = new[] { ProceduralSFX.Switch(true) };
        bank["switch_off"]= new[] { ProceduralSFX.Switch(false) };
        bank["enemy_hit"] = new[] { ProceduralSFX.EnemyHit(0.9f), ProceduralSFX.EnemyHit(1f), ProceduralSFX.EnemyHit(1.15f) };
        bank["enemy_die"] = new[] { ProceduralSFX.EnemyDie() };
        bank["hazard"]    = new[] { ProceduralSFX.Hazard() };
        bank["victory"]   = new[] { ProceduralSFX.Victory() };
        bank["defeat"]    = new[] { ProceduralSFX.Defeat() };
        bank["restart"]   = new[] { ProceduralSFX.Restart() };
        bank["transition"]= new[] { ProceduralSFX.Transition() };
        bank["ui"]        = new[] { ProceduralSFX.UIClick() };
    }

    // ================================================================ API PÚBLICA (llámela desde los scripts del juego)

    // --- Personaje
    public void Jump(Vector3 pos)                 => Play("jump", playerVol, pos);
    public void Bounce(Vector3 pos)               => Play("bounce", worldVol, pos);

    /// Impacto con intensidad según la velocidad relativa del choque (control dinámico).
    public void Impact(float speed, Vector3 pos)
    {
        if (speed < 1.5f) return;                               // roces muy suaves: silencio
        string key = speed < 5f ? "impact_lo" : speed < 10f ? "impact_md" : "impact_hi";
        float vol = Mathf.InverseLerp(1.5f, 14f, speed);        // más rápido = más fuerte
        Play(key, playerVol * Mathf.Lerp(0.3f, 1f, vol), pos, 1f + (vol - 0.5f) * -0.1f, 0.05f);
    }

    // --- Mundo
    public void Pickup(Vector3 pos)               => Play("pickup", worldVol, pos);
    public void Door(bool open, Vector3 pos)      => Play(open ? "door_open" : "door_close", worldVol, pos);
    public void Switch(bool on, Vector3 pos)      => Play(on ? "switch_on" : "switch_off", worldVol, pos);
    public void EnemyHit(Vector3 pos)             => Play("enemy_hit", worldVol, pos);
    public void EnemyDie(Vector3 pos)             => Play("enemy_die", worldVol, pos);
    public void Hazard(Vector3 pos)               => Play("hazard", worldVol, pos);

    // --- Eventos de juego (con ducking de la música y aviso al generador musical)
    public void Victory()
    {
        Play("victory", eventVol, null, 1f, 0f);
        ProceduralMusic.I?.OnVictory();
    }
    public void Defeat()
    {
        Play("defeat", eventVol, null, 1f, 0f);
        ProceduralMusic.I?.OnDefeat();
    }
    public void Restart()
    {
        Play("restart", eventVol, null, 1f, 0f);
        Duck(-10f, 0.4f);
    }
    public void Transition()
    {
        Play("transition", eventVol, null, 1f, 0f);
        Duck(-15f, 1.0f);
    }
    public void UIClick() => Play("ui", 0.6f, null, 1f, 0.03f, uiGroup);

    // ================================================================ MOTOR DE REPRODUCCIÓN

    /// <param name="pitchVar">-1 = usar la variación por defecto</param>
    public void Play(string key, float volume, Vector3? worldPos, float pitch = 1f, float pitchVar = -1f, AudioMixerGroup group = null)
    {
        if (!bank.TryGetValue(key, out var clips)) return;

        // Evita "ametralladora": el mismo sonido no se repite en menos de 40 ms
        if (lastPlayTime.TryGetValue(key, out var t) && Time.unscaledTime - t < 0.04f) return;
        lastPlayTime[key] = Time.unscaledTime;

        var src = NextSource();
        src.clip = clips[Random.Range(0, clips.Length)];
        float pv = pitchVar < 0 ? pitchVariation : pitchVar;
        src.pitch = pitch * (1f + Random.Range(-pv, pv));
        src.volume = Mathf.Clamp01(volume * (1f + Random.Range(-volumeVariation, volumeVariation)));
        src.panStereo = worldPos.HasValue ? ComputePan(worldPos.Value) : 0f;
        src.outputAudioMixerGroup = group != null ? group : sfxGroup;
        src.Play();
    }

    AudioSource NextSource()
    {
        for (int i = 0; i < pool.Count; i++)
        {
            var s = pool[(poolIndex + i) % pool.Count];
            if (!s.isPlaying) { poolIndex = (poolIndex + i + 1) % pool.Count; return s; }
        }
        var steal = pool[poolIndex];                // todos ocupados: reutiliza el más antiguo
        poolIndex = (poolIndex + 1) % pool.Count;
        return steal;
    }

    /// Paneo estéreo según la posición horizontal del objeto respecto a la cámara.
    float ComputePan(Vector3 pos)
    {
        if (cam == null) cam = Camera.main;
        if (cam == null) return 0f;
        float halfWidth = cam.orthographicSize * cam.aspect;
        return Mathf.Clamp((pos.x - cam.transform.position.x) / halfWidth, -1f, 1f) * 0.7f;
    }

    // ================================================================ DUCKING (baja la música temporalmente)

    Coroutine duckRoutine;
    public void Duck(float dB, float holdSeconds)
    {
        if (mixer == null) return;
        if (duckRoutine != null) StopCoroutine(duckRoutine);
        duckRoutine = StartCoroutine(DuckRoutine(dB, holdSeconds));
    }

    IEnumerator DuckRoutine(float dB, float hold)
    {
        yield return Fade("MusicVol", 0f, dB, 0.1f);
        yield return new WaitForSecondsRealtime(hold);
        yield return Fade("MusicVol", dB, 0f, 0.6f);
    }

    IEnumerator Fade(string param, float from, float to, float time)
    {
        for (float t = 0; t < time; t += Time.unscaledDeltaTime)
        {
            mixer.SetFloat(param, Mathf.Lerp(from, to, t / time));
            yield return null;
        }
        mixer.SetFloat(param, to);
    }
}
