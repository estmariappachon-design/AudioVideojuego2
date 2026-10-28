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

    // ================================================================ ESTILO SONORO
    public enum SoundStyle { Clasico, EsponjosoAlien }

    [Header("Estilo sonoro (F2 lo alterna durante el juego)")]
    [Tooltip("Clasico = síntesis 8-bit original. EsponjosoAlien = burbujas, gelatina, formantes, ring mod y eco.")]
    public SoundStyle style = SoundStyle.EsponjosoAlien;
    public KeyCode toggleStyleKey = KeyCode.F2;

    /// Lo leen los sintetizadores en tiempo real (BallAudio, MechanismHum, ProceduralMusic)
    /// desde el hilo de audio, por eso es una variable estática simple.
    public static volatile bool AlienStyle = true;
    SoundStyle builtStyle;

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
    [Tooltip("Volumen general de TODOS los efectos. 0,7 = mezcla suave")]
    [Range(0, 1)] public float sfxMaster = 0.7f;
    [Range(0, 1)] public float playerVol = 0.8f;
    [Range(0, 1)] public float worldVol = 0.75f;
    [Range(0, 1)] public float eventVol = 0.9f;

    readonly List<AudioSource> pool = new List<AudioSource>();
    readonly Dictionary<string, AudioClip[]> bank = new Dictionary<string, AudioClip[]>();
    readonly Dictionary<string, float> lastPlayTime = new Dictionary<string, float>();
    int poolIndex;
    Camera cam;

    /// Arranque automático: si existe el prefab Assets/Resources/AudioSystem.prefab,
    /// se crea ANTES de cargar cualquier escena. Así el audio funciona aunque le des Play
    /// directamente en Scene_1 sin pasar por el menú.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Bootstrap()
    {
        if (I != null) return;
        var prefab = Resources.Load<GameObject>("AudioSystem");
        if (prefab != null) Instantiate(prefab).name = "AudioSystem";
    }

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

    void Update()
    {
        if (I != this) return;                    // copia duplicada que se está destruyendo
        if (Input.GetKeyDown(toggleStyleKey))
            style = style == SoundStyle.Clasico ? SoundStyle.EsponjosoAlien : SoundStyle.Clasico;
        if (style != builtStyle) BuildBank();     // también reacciona si lo cambias en el Inspector
    }

    /// Cambia el estilo desde otro script o desde un botón de UI.
    public void SetStyle(SoundStyle s) { style = s; if (s != builtStyle) BuildBank(); }

    /// Síntesis de todo el banco de sonidos según el estilo elegido.
    void BuildBank()
    {
        builtStyle = style;
        AlienStyle = style == SoundStyle.EsponjosoAlien;

        // Libera los clips del estilo anterior (AudioClip.Create no se borra solo)
        foreach (var src in pool) src.Stop();
        foreach (var clips in bank.Values) foreach (var c in clips) if (c != null) Destroy(c);
        bank.Clear();

        if (AlienStyle) BuildBankSpongeAlien(); else BuildBankClassic();
        Debug.Log("[Audio] Banco sintetizado con estilo: " + style);
    }

    /// ESTILO ESPONJOSO + ALIEN: mismos eventos, mismos nombres, otro algoritmo de síntesis.
    void BuildBankSpongeAlien()
    {
        bank["jump"]      = new[] { SpongeAlienSFX.Jump(0.92f), SpongeAlienSFX.Jump(1f), SpongeAlienSFX.Jump(1.08f), SpongeAlienSFX.Jump(1.15f) };
        bank["impact_lo"] = new[] { SpongeAlienSFX.Impact(0.15f), SpongeAlienSFX.Impact(0.2f) };
        bank["impact_md"] = new[] { SpongeAlienSFX.Impact(0.45f), SpongeAlienSFX.Impact(0.55f) };
        bank["impact_hi"] = new[] { SpongeAlienSFX.Impact(0.85f), SpongeAlienSFX.Impact(1f) };
        bank["bounce"]    = new[] { SpongeAlienSFX.Bounce() };
        bank["pickup"]    = new[] { SpongeAlienSFX.Pickup(0), SpongeAlienSFX.Pickup(2), SpongeAlienSFX.Pickup(-1) };
        bank["door_open"] = new[] { SpongeAlienSFX.Door(true) };
        bank["door_close"]= new[] { SpongeAlienSFX.Door(false) };
        bank["switch_on"] = new[] { SpongeAlienSFX.Switch(true) };
        bank["switch_off"]= new[] { SpongeAlienSFX.Switch(false) };
        bank["enemy_hit"] = new[] { SpongeAlienSFX.EnemyHit(0.9f), SpongeAlienSFX.EnemyHit(1f), SpongeAlienSFX.EnemyHit(1.15f) };
        bank["enemy_die"] = new[] { SpongeAlienSFX.EnemyDie() };
        bank["hazard"]    = new[] { SpongeAlienSFX.Hazard(), SpongeAlienSFX.Hazard() };
        bank["victory"]   = new[] { SpongeAlienSFX.Victory() };
        bank["defeat"]    = new[] { SpongeAlienSFX.Defeat() };
        bank["restart"]   = new[] { SpongeAlienSFX.Restart() };
        bank["transition"]= new[] { SpongeAlienSFX.Transition() };
        bank["ui"]        = new[] { SpongeAlienSFX.UIClick() };
        bank["climb"]     = new[] { SpongeAlienSFX.ClimbTick(0.9f), SpongeAlienSFX.ClimbTick(1f), SpongeAlienSFX.ClimbTick(1.12f) };
        bank["shatter"]   = new[] { SpongeAlienSFX.Shatter(), SpongeAlienSFX.Shatter() };   // granular: cada versión es distinta
        bank["pu_speed"]  = new[] { SpongeAlienSFX.PowerUp(300f, 1400f) };
        bank["pu_jump"]   = new[] { SpongeAlienSFX.PowerUp(200f, 900f) };
        bank["pu_grow"]   = new[] { SpongeAlienSFX.PowerUp(110f, 330f) };
        bank["pu_shrink"] = new[] { SpongeAlienSFX.PowerUp(1200f, 500f) };
    }

    /// ESTILO CLÁSICO (8-bit), el de la primera versión.
    void BuildBankClassic()
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
        // Añadidos para esta plantilla
        bank["climb"]     = new[] { ProceduralSFX.ClimbTick(0.9f), ProceduralSFX.ClimbTick(1f), ProceduralSFX.ClimbTick(1.12f) };
        bank["shatter"]   = new[] { ProceduralSFX.Shatter(), ProceduralSFX.Shatter() };   // 2 versiones granulares distintas
        bank["pu_speed"]  = new[] { ProceduralSFX.PowerUp(300f, 1400f, ProceduralSFX.Wave.Saw) };
        bank["pu_jump"]   = new[] { ProceduralSFX.PowerUp(200f, 900f, ProceduralSFX.Wave.Square) };
        bank["pu_grow"]   = new[] { ProceduralSFX.PowerUp(110f, 330f, ProceduralSFX.Wave.Square) };
        bank["pu_shrink"] = new[] { ProceduralSFX.PowerUp(1200f, 500f, ProceduralSFX.Wave.Triangle) };
    }

    // ================================================================ API PÚBLICA (llámela desde los scripts del juego)

    // --- Personaje
    public void Jump(Vector3 pos)                 => Play("jump", playerVol, pos);
    public void Bounce(Vector3 pos)               => Play("bounce", worldVol, pos);

    /// Impacto con intensidad según la velocidad relativa del choque (control dinámico).
    public void Impact(float speed, Vector3 pos)
    {
        if (speed < 2.5f) return;                               // roces y micro-rebotes: silencio
        string key = speed < 6f ? "impact_lo" : speed < 11f ? "impact_md" : "impact_hi";
        float vol = Mathf.InverseLerp(2.5f, 16f, speed);        // más rápido = más fuerte
        vol *= vol;                                             // curva cuadrática: los golpes leves son muy suaves
        Play(key, playerVol * Mathf.Lerp(0.15f, 0.75f, vol), pos, 1f + (vol - 0.5f) * -0.1f, 0.05f);
    }

    // --- Mundo
    public void Pickup(Vector3 pos)               => Play("pickup", worldVol, pos);
    public void Door(bool open, Vector3 pos)      => Play(open ? "door_open" : "door_close", worldVol, pos);
    public void Switch(bool on, Vector3 pos)      => Play(on ? "switch_on" : "switch_off", worldVol, pos);
    public void EnemyHit(Vector3 pos)             => Play("enemy_hit", worldVol, pos);
    public void EnemyDie(Vector3 pos)             => Play("enemy_die", worldVol, pos);
    public void Hazard(Vector3 pos)               => Play("hazard", worldVol, pos);

    // --- Añadidos para esta plantilla
    public void Climb(Vector3 pos)                => Play("climb", playerVol * 0.6f, pos);
    public void Shatter(Vector3 pos)              => Play("shatter", playerVol, pos, 1f, 0.05f);

    /// Rebote de JumpPad: el tono y el volumen dependen de la fuerza del trampolín.
    public void Bounce(Vector3 pos, float force)
    {
        float k = Mathf.InverseLerp(5f, 40f, force);
        Play("bounce", worldVol * Mathf.Lerp(0.6f, 1f, k), pos, Mathf.Lerp(1.2f, 0.8f, k), 0.04f);
    }

    /// Power-up. type: 0 Speed, 1 Jump, 2 Shrink, 3 Grow (mismo orden que el enum PowerUpType).
    public void PowerUp(int type, Vector3 pos)
    {
        string key = type == 0 ? "pu_speed" : type == 1 ? "pu_jump" : type == 2 ? "pu_shrink" : "pu_grow";
        Play("pickup", worldVol * 0.7f, pos);
        Play(key, worldVol, pos, 1f, 0.02f);
    }

    /// Choque de objetos físicos (cajas de madera): mismo impacto sintetizado pero más grave
    /// (transformación por pitch) y con volumen según la velocidad.
    public void ObjectImpact(float speed, Vector3 pos, float pitch = 0.7f)
    {
        if (speed < 2.5f) return;
        string key = speed < 6f ? "impact_lo" : "impact_md";
        float vol = Mathf.InverseLerp(2.5f, 14f, speed);
        Play(key, worldVol * Mathf.Lerp(0.15f, 0.6f, vol * vol), pos, pitch, 0.06f);
    }

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
        src.volume = Mathf.Clamp01(volume * sfxMaster * (1f + Random.Range(-volumeVariation, volumeVariation)));
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
