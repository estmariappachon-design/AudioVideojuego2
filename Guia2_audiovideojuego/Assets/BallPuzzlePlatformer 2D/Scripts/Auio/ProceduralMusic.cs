using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// MÚSICA GENERATIVA EN TIEMPO REAL (secuenciador + sintetizador).
/// Cada nivel recibe una "semilla" distinta: a partir de ella el algoritmo elige
/// tempo, escala, tónica, progresión de acordes y patrón de arpegio. Así los diez niveles
/// tienen música diferente sin un solo archivo de audio.
/// Instrumentos sintetizados: bombo (seno con caída de tono), hi-hat (ruido pasa-altos),
/// bajo (triángulo), arpegio (cuadrada) y pad (3 senos).
/// Parámetro dinámico "intensity" (0..1): densidad rítmica, notas y brillo del filtro.
/// Derrota = efecto "tape stop" (el tono cae a cero). Victoria = fundido de salida.
/// Póngalo en el mismo GameObject persistente que AudioManager (o en uno propio).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ProceduralMusic : MonoBehaviour
{
    public static ProceduralMusic I { get; private set; }

    [Range(0, 1)] public float volume = 0.3f;
    [Range(0, 1)] public float intensity = 0.5f;
    [Tooltip("Desplazamiento entre buildIndex de la escena y número de nivel (p. ej. 1 si la escena 0 es el menú)")]
    public int buildIndexOffset = 1;
    [Tooltip("-1 = automático por escena")]
    public int levelOverride = -1;

    // ---------------------------------------------------------------- configuración por nivel
    static readonly int[][] Scales =
    {
        new[] { 0, 2, 4, 7, 9 },          // pentatónica mayor (alegre)
        new[] { 0, 2, 4, 5, 7, 9, 11 },   // jónico / mayor
        new[] { 0, 2, 3, 5, 7, 9, 10 },   // dórico (aventurero)
        new[] { 0, 3, 5, 7, 10 },         // pentatónica menor
        new[] { 0, 2, 4, 6, 7, 9, 11 },   // lidio (mágico)
        new[] { 0, 2, 3, 5, 7, 8, 10 },   // eólico / menor (tenso)
        new[] { 0, 1, 4, 5, 7, 8, 10 },   // frigio dominante (exótico/peligro)
    };

    class Config
    {
        public double bpm;
        public int[] scale;
        public int root;                 // nota MIDI de la tónica
        public int[] progression;        // grados de la escala (4 compases)
        public int[] arp;                // 16 pasos: -1 silencio, n = grado relativo al acorde
        public int[] bassSteps;          // pasos donde suena el bajo
    }

    volatile Config cfg;
    public int CurrentLevel { get; private set; }

    // ---------------------------------------------------------------- estado del sintetizador (hilo de audio)
    int sr;
    double samplesPerStep, nextStep, sampleClock;
    int step;
    uint seed = 1234567;
    float rand01() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return seed / (float)uint.MaxValue; }

    // voces
    double kickPh; float kickEnv, kickPitch;
    float hatEnv, hatLp;
    double bassPh; float bassFreq, bassEnv;
    double arpPh; float arpFreq, arpEnv;
    readonly double[] padPh = new double[3]; readonly float[] padFreq = new float[3];
    float mixLp;

    // control global
    volatile float targetMaster = 1f;
    volatile float targetPitch = 1f;
    float master = 0f, pitchMul = 1f, smoothIntensity = 0.5f;
    volatile float intensityShared = 0.5f;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        if (transform.parent == null) DontDestroyOnLoad(gameObject);
        sr = AudioSettings.outputSampleRate;
        var src = GetComponent<AudioSource>();
        src.clip = null; src.loop = true; src.playOnAwake = true; src.spatialBlend = 0f;
        src.Play();
        SceneManager.sceneLoaded += OnSceneLoaded;
        Configure(levelOverride >= 0 ? levelOverride : SceneManager.GetActiveScene().buildIndex - buildIndexOffset + 1);
    }

    void OnDestroy() { if (I == this) SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        Configure(levelOverride >= 0 ? levelOverride : s.buildIndex - buildIndexOffset + 1);
    }

    [Tooltip("Intensidad de reposo; la música vuelve a ella cuando no hay peligro cerca")]
    [Range(0, 1)] public float baseIntensity = 0.4f;

    void Update()
    {
        // La intensidad vuelve poco a poco a su valor base
        intensity = Mathf.MoveTowards(intensity, baseIntensity, 0.15f * Time.deltaTime);
        intensityShared = intensity;
    }

    // ================================================================ API
    public void SetIntensity(float v) => intensity = Mathf.Clamp01(v);
    /// Sube la intensidad (solo si el nuevo valor es mayor). Úsela desde enemigos/zonas de peligro.
    public void PushIntensity(float v) => intensity = Mathf.Max(intensity, Mathf.Clamp01(v));
    public void OnDefeat()  { targetPitch = 0.05f; targetMaster = 0f; }    // "tape stop"
    public void OnVictory() { targetMaster = 0f; }                          // fundido de salida
    public void Resume()    { targetPitch = 1f; targetMaster = 1f; }

    /// Genera la configuración musical del nivel a partir de una semilla (determinista).
    public void Configure(int level)
    {
        CurrentLevel = Mathf.Max(1, level);
        var r = new System.Random(CurrentLevel * 7919 + 101);
        var c = new Config
        {
            bpm = 92 + (CurrentLevel - 1) * 5 + r.Next(-3, 4),             // el tempo crece con el nivel
            scale = Scales[(CurrentLevel - 1) % Scales.Length],
            root = 45 + ((CurrentLevel - 1) * 5) % 12,                       // tónica en ciclo de cuartas
            progression = new int[4],
            arp = new int[16],
            bassSteps = r.NextDouble() < 0.5 ? new[] { 0, 6, 8, 14 } : new[] { 0, 3, 8, 11 }
        };
        int[] degreePool = { 0, 3, 4, 5, 2, 1 };
        c.progression[0] = 0;
        for (int i = 1; i < 4; i++) c.progression[i] = degreePool[r.Next(1, degreePool.Length)];
        for (int i = 0; i < 16; i++) c.arp[i] = r.NextDouble() < 0.65 ? r.Next(0, 5) : -1;
        cfg = c;
        samplesPerStep = sr * 60.0 / c.bpm / 4.0;                             // semicorcheas
        targetPitch = 1f; targetMaster = 1f; step = 0; nextStep = sampleClock;
    }

    // ================================================================ utilidades
    static float MidiToHz(float m) => 440f * Mathf.Pow(2f, (m - 69f) / 12f);
    static int ScaleNote(int[] scale, int degree)
    {
        int len = scale.Length;
        int oct = Mathf.FloorToInt((float)degree / len);
        int idx = ((degree % len) + len) % len;
        return scale[idx] + 12 * oct;
    }

    // ================================================================ SECUENCIADOR
    void TriggerStep(Config c, float inten)
    {
        int s = step % 16;
        int bar = (step / 16) % 4;
        int chordDeg = c.progression[bar];

        // Bombo
        if (s % 4 == 0 || (inten > 0.65f && s == 10)) { kickEnv = 1f; kickPitch = 1f; kickPh = 0; }

        // Hi-hat: más densidad con más intensidad
        bool hat = inten > 0.7f ? true : inten > 0.25f ? s % 2 == 0 : s % 4 == 2;
        if (hat) hatEnv = (s % 4 == 2) ? 0.7f : 0.4f;

        // Bajo
        foreach (int b in c.bassSteps)
            if (b == s && (b == 0 || b == 8 || inten > 0.4f))
            { bassFreq = MidiToHz(c.root + ScaleNote(c.scale, chordDeg)); bassEnv = 1f; }

        // Arpegio (probabilístico: cada repetición varía un poco)
        if (c.arp[s] >= 0 && rand01() < 0.35f + 0.65f * inten)
        {
            int deg = chordDeg + c.arp[s] * 2;                                // saltos de tercera = notas del acorde
            arpFreq = MidiToHz(c.root + 24 + ScaleNote(c.scale, deg));
            arpEnv = 1f;
        }

        // Pad: acorde nuevo cada compás
        if (s == 0)
            for (int k = 0; k < 3; k++)
                padFreq[k] = MidiToHz(c.root + 12 + ScaleNote(c.scale, chordDeg + k * 2));

        step++;
    }

    // ================================================================ SÍNTESIS (hilo de audio)
    void OnAudioFilterRead(float[] data, int channels)
    {
        var c = cfg;
        if (c == null || sr == 0) return;
        float inten = intensityShared;
        double TWO_PI = 2.0 * System.Math.PI;

        for (int i = 0; i < data.Length; i += channels)
        {
            master += (targetMaster - master) * 0.00005f;
            pitchMul += (targetPitch - pitchMul) * 0.00003f;
            smoothIntensity += (inten - smoothIntensity) * 0.00002f;

            // El reloj avanza más lento si pitchMul baja (efecto "tape stop" afecta también el tempo)
            sampleClock += pitchMul;
            if (sampleClock >= nextStep) { TriggerStep(c, smoothIntensity); nextStep += samplesPerStep; }

            float pm = pitchMul;

            // Bombo: seno con caída rápida de frecuencia 150 Hz -> 45 Hz
            kickPitch *= 0.9993f;
            kickPh += (45f + 105f * kickPitch) * pm / sr;
            kickEnv *= 0.99975f;
            float kick = (float)System.Math.Sin(kickPh * TWO_PI) * kickEnv;

            // Hi-hat: ruido menos su versión filtrada = pasa-altos
            float n = rand01() * 2f - 1f;
            hatLp += 0.25f * (n - hatLp);
            hatEnv *= 0.9985f;
            float hat = (n - hatLp) * hatEnv * 0.35f;

            // Bajo: triángulo
            bassPh += bassFreq * pm / sr;
            double bx = bassPh - System.Math.Floor(bassPh);
            bassEnv *= 0.99993f;
            float bass = (float)(4.0 * System.Math.Abs(bx - 0.5) - 1.0) * bassEnv * 0.55f;

            // Arpegio: cuadrada con ancho de pulso que depende de la intensidad
            arpPh += arpFreq * pm / sr;
            double ax = arpPh - System.Math.Floor(arpPh);
            arpEnv *= 0.99965f;
            float arp = (ax < 0.3 + 0.2 * smoothIntensity ? 1f : -1f) * arpEnv * 0.16f;

            // Pad: tres senos ligeramente desafinados
            float pad = 0;
            for (int k = 0; k < 3; k++)
            {
                padPh[k] += padFreq[k] * (1.0 + 0.002 * k) * pm / sr;
                pad += (float)System.Math.Sin(padPh[k] * TWO_PI);
            }
            pad *= 0.06f;

            float mix = kick * 0.8f + hat + bass + arp + pad;

            // Filtro global: la intensidad abre el brillo de toda la mezcla
            float cutoff = 800f + 7000f * smoothIntensity;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff * Mathf.Max(pm, 0.1f) / sr);
            mixLp += a * (mix - mixLp);

            float o = mixLp * volume * master;
            o = o / (1f + Mathf.Abs(o));                  // saturación suave (limitador)
            for (int ch = 0; ch < channels; ch++) data[i + ch] += o;
        }
    }
}
