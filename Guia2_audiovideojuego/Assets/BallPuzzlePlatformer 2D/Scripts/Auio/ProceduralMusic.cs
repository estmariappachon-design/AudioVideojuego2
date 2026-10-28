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
/// ESTILO ESPONJOSO + ALIEN (AudioManager.AlienStyle):
///   · Arpegio: campana FM con PORTAMENTO (las notas se deslizan como un theremín) y ECO
///     espacial sincronizado al tempo (corchea con puntillo).
///   · Bajo "wobble": sierra por un filtro resonante cuyo corte oscila a tempo = bajo gelatinoso.
///   · Bombo de goma (caída de tono más lenta), pad con vibrato lento "flotante".
///   · Burbujas aleatorias a contratiempo cuya probabilidad crece con la intensidad.
/// Póngalo en el mismo GameObject persistente que AudioManager (o en uno propio).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class ProceduralMusic : MonoBehaviour
{
    public static ProceduralMusic I { get; private set; }

    [Range(0, 1)] public float volume = 0.3f;
    [Range(0, 1)] public float intensity = 0.5f;
    [Tooltip("-1 = automático. En esta plantilla los 10 niveles son prefabs dentro de Scene_1, " +
             "por eso el nivel lo informa GameControl.Start() llamando a Configure(nivel). 0 = música del menú.")]
    public int levelOverride = -1;
    [Tooltip("Nombre de la escena del menú principal")]
    public string menuSceneName = "MainMenu";

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

    // voces del estilo esponjoso + alien
    float arpCur = 440f;                         // frecuencia actual con portamento
    float[] echoBuf; int echoW; volatile int echoLen = 1; float echoLp;
    float bassLow, bassBand; double wobPh;      // bajo wobble (filtro de estado variable)
    float bloopEnv, bloopFreq; double bloopPh;  // burbujas a contratiempo
    double padLfoPh;

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
        echoBuf = new float[Mathf.Max(1, sr * 2)];   // hasta 2 s de eco
        var src = GetComponent<AudioSource>();
        src.clip = null; src.loop = true; src.playOnAwake = true; src.spatialBlend = 0f;
        src.Play();
        SceneManager.sceneLoaded += OnSceneLoaded;
        Configure(0);                       // configuración inicial (menú); se corrige al cargar la escena
    }

    void OnDestroy() { if (I == this) SceneManager.sceneLoaded -= OnSceneLoaded; }

    void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        if (levelOverride >= 0) Configure(levelOverride);
        else if (s.name == menuSceneName) Configure(0);   // música tranquila del menú
        else Resume();                                     // en Scene_1, GameControl llamará a Configure(nivel)
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
        CurrentLevel = Mathf.Max(0, level);                 // 0 = menú, 1..10 = niveles
        bool menu = CurrentLevel == 0;
        int L = Mathf.Max(1, CurrentLevel);
        var r = new System.Random(CurrentLevel * 7919 + 101);
        // Los niveles avanzados arrancan con más energía
        baseIntensity = menu ? 0.2f : Mathf.Lerp(0.3f, 0.65f, (L - 1) / 9f);
        var c = new Config
        {
            bpm = menu ? 78 : 92 + (L - 1) * 5 + r.Next(-3, 4),            // el tempo crece con el nivel
            scale = Scales[(L - 1) % Scales.Length],
            root = menu ? 48 : 45 + ((L - 1) * 5) % 12,                      // tónica en ciclo de cuartas
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
        if (echoBuf != null) echoLen = Mathf.Clamp((int)(samplesPerStep * 3), 1, echoBuf.Length - 1);  // corchea con puntillo
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

        // Burbujas alien a contratiempo (solo estilo esponjoso)
        if (AudioManager.AlienStyle && s % 2 == 1 && rand01() < 0.08f + 0.25f * inten)
        {
            int deg = chordDeg + (int)(rand01() * 5f);
            bloopFreq = MidiToHz(c.root + 36 + ScaleNote(c.scale, deg)) * 0.7f;   // empieza abajo y sube
            bloopEnv = 1f; bloopPh = 0;
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

            bool alien = AudioManager.AlienStyle;

            // Bombo: seno con caída rápida de frecuencia 150 Hz -> 45 Hz (en estilo alien, más lenta = "de goma")
            kickPitch *= alien ? 0.9996f : 0.9993f;
            kickPh += (45f + 105f * kickPitch) * pm / sr;
            kickEnv *= 0.99975f;
            float kick = (float)System.Math.Sin(kickPh * TWO_PI) * kickEnv;

            // Hi-hat: ruido menos su versión filtrada = pasa-altos
            float n = rand01() * 2f - 1f;
            hatLp += 0.25f * (n - hatLp);
            hatEnv *= 0.9985f;
            float hat = (n - hatLp) * hatEnv * (alien ? 0.1f : 0.2f);     // hi-hat discreto

            float bass, arp, pad = 0f, bloop = 0f;
            if (!alien)
            {
                // Bajo: triángulo
                bassPh += bassFreq * pm / sr;
                double bx = bassPh - System.Math.Floor(bassPh);
                bassEnv *= 0.99993f;
                bass = (float)(4.0 * System.Math.Abs(bx - 0.5) - 1.0) * bassEnv * 0.55f;

                // Arpegio: cuadrada con ancho de pulso que depende de la intensidad
                arpPh += arpFreq * pm / sr;
                double ax = arpPh - System.Math.Floor(arpPh);
                arpEnv *= 0.99965f;
                arp = (ax < 0.3 + 0.2 * smoothIntensity ? 1f : -1f) * arpEnv * 0.16f;
                arpCur = arpFreq;

                // Pad: tres senos ligeramente desafinados
                for (int k = 0; k < 3; k++)
                {
                    padPh[k] += padFreq[k] * (1.0 + 0.002 * k) * pm / sr;
                    pad += (float)System.Math.Sin(padPh[k] * TWO_PI);
                }
                pad *= 0.06f;
            }
            else
            {
                // BAJO WOBBLE: sierra -> filtro resonante cuyo corte oscila en corcheas (gelatina rítmica)
                bassPh += bassFreq * pm / sr;
                double bx = bassPh - System.Math.Floor(bassPh);
                float saw = (float)(2.0 * bx - 1.0);
                wobPh += pm / (samplesPerStep * 2.0); if (wobPh > 1.0) wobPh -= 1.0;
                float wob = 0.5f - 0.5f * (float)System.Math.Cos(TWO_PI * wobPh);
                float bfc = 110f + (200f + 300f * smoothIntensity) * wob;
                float bk = 2f * Mathf.Sin(Mathf.PI * bfc / sr);
                float bh = saw - bassLow - 0.7f * bassBand; bassBand += bk * bh; bassLow += bk * bassBand;   // poca resonancia
                bassEnv *= 0.99995f;
                bass = bassLow * bassEnv * 0.6f;

                // ARPEGIO: campana FM con PORTAMENTO (la nota se desliza hacia la nueva, ~25 ms)
                arpCur += (arpFreq - arpCur) * 0.001f;
                arpPh += arpCur * pm / sr;
                arpEnv *= 0.99985f;
                float bell = (float)System.Math.Sin(TWO_PI * arpPh + 0.9 * arpEnv * System.Math.Sin(TWO_PI * arpPh * 2.0));
                arp = bell * arpEnv * 0.15f;

                // BURBUJAS: tono que sube, caída rápida
                if (bloopEnv > 0.001f)
                {
                    bloopFreq *= 1.00012f; bloopEnv *= 0.9992f;
                    bloopPh += bloopFreq * pm / sr;
                    bloop = (float)System.Math.Sin(TWO_PI * bloopPh) * bloopEnv * 0.07f;
                }

                // ECO ESPACIAL (corchea con puntillo) con pasa-bajos en la realimentación
                int len = echoBuf.Length;
                float delayed = echoBuf[(echoW - echoLen + len) % len];
                echoLp += 0.3f * (delayed - echoLp);
                echoBuf[echoW] = arp + bloop + echoLp * 0.45f;
                echoW = (echoW + 1) % len;
                arp += delayed * 0.55f;

                // PAD FLOTANTE: tres senos con vibrato lento (0,25 Hz) desfasado entre voces
                padLfoPh += 0.25 / sr; if (padLfoPh > 1.0) padLfoPh -= 1.0;
                for (int k = 0; k < 3; k++)
                {
                    double drift = 1.0 + 0.004 * k + 0.006 * System.Math.Sin(TWO_PI * padLfoPh + k * 2.1);
                    padPh[k] += padFreq[k] * drift * pm / sr;
                    pad += (float)System.Math.Sin(padPh[k] * TWO_PI);
                }
                pad *= 0.075f;
            }

            float mix = kick * 0.6f + hat + bass + arp + pad + bloop;

            // Filtro global: la intensidad abre el brillo de toda la mezcla (más cerrado = más suave)
            float cutoff = 700f + 4500f * smoothIntensity;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff * Mathf.Max(pm, 0.1f) / sr);
            mixLp += a * (mix - mixLp);

            float o = mixLp * volume * master * 0.7f;      // la música queda un escalón debajo de los efectos
            o = o / (1f + Mathf.Abs(o));                  // saturación suave (limitador)
            for (int ch = 0; ch < channels; ch++) data[i + ch] += o;
        }
    }
}
