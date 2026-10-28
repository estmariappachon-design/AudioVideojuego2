using UnityEngine;

/// <summary>
/// AUDIO DEL PERSONAJE (la bola) — SÍNTESIS EN TIEMPO REAL (OnAudioFilterRead).
/// 1) Rodado: el volumen, el brillo y el tono dependen de la velocidad real de la bola.
///    · ZONA MUERTA: por debajo de 'minSpeed' hay silencio total. La bola de la plantilla no tiene
///      fricción de aire (Linear Drag = 0) y aunque parezca quieta se desliza un poco; sin esta
///      zona muerta se oía un zumbido con la bola "quieta".
///    · Entrada suave (curva smoothstep): el sonido aparece poco a poco, sin golpe.
/// 2) Impactos: la fuerza del choque decide qué tan fuerte suena (lo sintetiza AudioManager).
/// 3) Salto: Player.cs llama a OnJump() en el mismo frame del impulso.
///
/// ESTILO ESPONJOSO + ALIEN (versión suave):
///    · "Soplo" de rodado: ruido filtrado con pasa-bajos de 2 polos (250-1100 Hz según velocidad):
///      suena como algo blandito rodando sobre una alfombra.
///    · Cuerpo redondo: seno grave que se "aplasta" levemente (±4 % de tono, ±25 % de volumen)
///      dos veces por vuelta, según la velocidad angular: el carácter esponjoso, pero sutil.
///    · Burbujitas ocasionales (pocas y suaves) cuando la bola va rápido.
///    · Silbido de theremín muy tenue en el aire (sube al subir, baja al caer).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BallAudio : MonoBehaviour
{
    [Header("Rodado")]
    [Range(0, 1)] public float rollVolume = 0.15f;
    [Tooltip("Por debajo de esta velocidad la bola no suena (zona muerta)")]
    public float minSpeed = 0.8f;
    public float maxSpeed = 12f;              // velocidad a la que el rodado suena al máximo

    [Header("Estilo esponjoso + alien")]
    [Range(0, 0.1f)] public float airWhistleVolume = 0.015f;  // silbido en el aire (0 = apagado)

    [Header("Salto")]
    public bool autoDetectJump = true;        // desactívelo si llama OnJump() manualmente
    public float jumpVelocityThreshold = 4f;

    Rigidbody2D rb;
    int contacts;                             // nº de superficies que toca
    float prevVy;

    // Valores compartidos con el hilo de audio (se escriben en Update, se leen en OnAudioFilterRead)
    volatile float targetGain, targetCutoff, targetRumble;
    float gain, cutoff = 200f, rumbleFreq = 40f, lp1, lp2;
    double rumblePhase;
    int sampleRate;
    uint seed = 22222;
    float originalScale = 1f;

    // --- estado del modo esponjoso/alien
    volatile float targetSquish, targetBubbleRate, targetAir, targetAirFreq;
    float squishFreq, bubbleRate, airGain, airFreq = 300f;
    double squishPh, bodyPh, airPh, airVibPh;
    float bubbleEnv, bubbleFreq;
    double bubblePh;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        originalScale = Mathf.Max(0.01f, Mathf.Abs(transform.localScale.x));
        sampleRate = AudioSettings.outputSampleRate;
        var src = GetComponent<AudioSource>();
        src.clip = null; src.loop = true; src.playOnAwake = true; src.spatialBlend = 0f;
        if (AudioManager.I != null) src.outputAudioMixerGroup = AudioManager.I.sfxGroup;
        src.Play();                            // sin clip: el sonido lo crea OnAudioFilterRead
    }

    Vector2 Velocity
    {
#if UNITY_6000_0_OR_NEWER
        get { return rb.linearVelocity; }
#else
        get { return rb.velocity; }
#endif
    }

    void Update()
    {
        if (rb == null) return;
        Vector2 v = Velocity;
        bool grounded = contacts > 0;

        // Zona muerta + curva suave: 0 por debajo de minSpeed, sube con forma de "S" hasta maxSpeed
        float speed01 = Mathf.Clamp01((Mathf.Abs(v.x) - minSpeed) / (maxSpeed - minSpeed));
        float fade = speed01 * speed01 * (3f - 2f * speed01);          // smoothstep

        // Mapeo de parámetros físicos a parámetros de síntesis
        targetGain   = grounded ? fade * rollVolume : 0f;
        targetCutoff = Mathf.Lerp(250f, 1100f, speed01);              // más rápido = un poco más brillante
        targetRumble = Mathf.Lerp(45f, 85f, speed01);                 // más rápido = cuerpo más agudo

        // Power-ups Grow/Shrink: una bola más grande suena más grave, una pequeña más aguda
        float sizeMul = originalScale / Mathf.Max(0.01f, Mathf.Abs(transform.localScale.x));
        targetRumble *= sizeMul;
        targetCutoff *= Mathf.Sqrt(sizeMul);

        // Esponjoso: 2 "aplastones" por vuelta => frecuencia = |ω| / 180 (ω en grados/s)
        targetSquish = Mathf.Clamp(Mathf.Abs(rb.angularVelocity) / 180f, 0f, 14f);
        targetBubbleRate = grounded ? fade * 2.5f : 0f;             // pocas burbujas por segundo
        // Alien: silbido en el aire; solo con movimiento vertical claro
        targetAir = (!grounded && Mathf.Abs(v.y) > 4f) ? airWhistleVolume : 0f;
        targetAirFreq = Mathf.Clamp(450f + v.y * 25f, 200f, 900f) * Mathf.Sqrt(sizeMul);

        // Detección automática de salto: estaba en el suelo y la velocidad vertical sube de golpe
        // (prevVy > -1 evita confundir un rebote al caer con un salto)
        if (autoDetectJump && prevVy > -1f && v.y - prevVy > jumpVelocityThreshold && v.y > 0.5f)
            OnJump();
        prevVy = v.y;
    }

    public void OnJump() => AudioManager.I?.Jump(transform.position);

    void OnCollisionEnter2D(Collision2D c)
    {
        contacts++;
        // Usamos la componente normal de la velocidad relativa: un roce lateral no "golpea"
        float normalSpeed = Mathf.Abs(Vector2.Dot(c.relativeVelocity, c.GetContact(0).normal));
        AudioManager.I?.Impact(normalSpeed, transform.position);
    }

    void OnCollisionExit2D(Collision2D c) => contacts = Mathf.Max(0, contacts - 1);

    float Rand01() { seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5; return seed / (float)uint.MaxValue; }

    // ---------------------------------------------------------------- HILO DE AUDIO
    // ¡Aquí no se puede usar la API de Unity! Solo matemáticas con valores precalculados.
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (sampleRate == 0) return;

        // Compuerta de silencio: si no hay nada que sonar, no se calcula nada (bola quieta = 0 absoluto)
        if (targetGain <= 0f && gain < 0.0001f && targetAir <= 0f && airGain < 0.0001f && bubbleEnv < 0.001f)
        {
            gain = 0f; airGain = 0f;
            return;
        }

        if (AudioManager.AlienStyle) { SpongeAlien(data, channels); return; }

        for (int i = 0; i < data.Length; i += channels)
        {
            // Suavizado de parámetros (evita saltos bruscos / clics)
            gain       += (targetGain   - gain)       * 0.0005f;
            cutoff     += (targetCutoff - cutoff)     * 0.0005f;
            rumbleFreq += (targetRumble - rumbleFreq) * 0.0005f;

            // Ruido blanco (xorshift) -> pasa-bajos de 2 polos
            float noise = Rand01() * 2f - 1f;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / sampleRate);
            lp1 += a * (noise - lp1);
            lp2 += a * (lp1 - lp2);

            // Zumbido grave con ligera modulación (textura de "rodar")
            rumblePhase += rumbleFreq / sampleRate;
            float rumble = Mathf.Sin((float)(rumblePhase * 2 * System.Math.PI)) * (0.7f + 0.3f * lp1);

            float s = (lp2 * 1.5f + rumble * 0.4f) * gain;
            for (int c = 0; c < channels; c++) data[i + c] += s;
        }
    }

    /// SÍNTESIS "ESPONJOSA + ALIEN" SUAVE EN TIEMPO REAL
    void SpongeAlien(float[] data, int channels)
    {
        const double TAU = 2.0 * System.Math.PI;
        float sr = sampleRate;
        for (int i = 0; i < data.Length; i += channels)
        {
            gain       += (targetGain       - gain)       * 0.0004f;
            cutoff     += (targetCutoff     - cutoff)     * 0.0004f;
            rumbleFreq += (targetRumble     - rumbleFreq) * 0.0004f;
            squishFreq += (targetSquish     - squishFreq) * 0.0004f;
            bubbleRate += (targetBubbleRate - bubbleRate) * 0.0004f;
            airGain    += (targetAir        - airGain)    * 0.0003f;
            airFreq    += (targetAirFreq    - airFreq)    * 0.0008f;   // portamento de theremín

            // 1) Aplastamiento suave 0..1 (seno elevado), dos veces por vuelta
            squishPh += squishFreq / sr; if (squishPh > 1.0) squishPh -= 1.0;
            float sq = 0.5f - 0.5f * (float)System.Math.Cos(TAU * squishPh);

            // 2) Soplo de rodado: ruido por pasa-bajos de 2 polos (sin resonancias = sin aspereza)
            float noise = Rand01() * 2f - 1f;
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / sr);
            lp1 += a * (noise - lp1);
            lp2 += a * (lp1 - lp2);

            // 3) Cuerpo redondo: seno + un poco de 2º armónico (se oye también en parlantes pequeños),
            //    con el tono doblándose apenas en cada aplastón
            bodyPh += rumbleFreq * 2f * (1f + 0.04f * sq) / sr; if (bodyPh > 1.0) bodyPh -= 1.0;
            float body = (float)(System.Math.Sin(TAU * bodyPh) + 0.2 * System.Math.Sin(TAU * 2.0 * bodyPh));

            // 4) Burbujitas ocasionales (proceso de Poisson)
            if (bubbleEnv < 0.001f && Rand01() < bubbleRate / sr)
            { bubbleEnv = 1f; bubbleFreq = 450f + 400f * Rand01(); bubblePh = 0; }
            float bubble = 0f;
            if (bubbleEnv > 0.001f)
            {
                bubbleFreq *= 1.00008f; bubbleEnv *= 0.9988f;
                bubblePh += bubbleFreq / sr;
                bubble = (float)System.Math.Sin(TAU * bubblePh) * bubbleEnv;
            }

            // 5) Silbido de theremín en el aire (vibrato de 5 Hz)
            airVibPh += 5.0 / sr; if (airVibPh > 1.0) airVibPh -= 1.0;
            airPh += airFreq * (1f + 0.015f * (float)System.Math.Sin(TAU * airVibPh)) / sr; if (airPh > 1.0) airPh -= 1.0;
            float air = (float)System.Math.Sin(TAU * airPh) * airGain;

            float squish = 0.75f + 0.25f * sq;                        // "respiración" de la esponja
            float s = (lp2 * 2.2f + body * 0.35f) * squish * gain + bubble * 0.08f * Mathf.Min(1f, gain * 6f) + air;
            for (int c = 0; c < channels; c++) data[i + c] += s;
        }
    }
}
