using UnityEngine;

/// <summary>
/// AUDIO DEL PERSONAJE (la bola) — SÍNTESIS EN TIEMPO REAL.
/// 1) Sonido de rodado: ruido blanco + filtro pasa-bajos + zumbido grave, generados en
///    OnAudioFilterRead. Volumen, brillo y tono dependen de la velocidad de la bola.
/// 2) Impactos: al chocar, la fuerza del choque decide qué tan fuerte y brillante suena.
/// 3) Salto: detección automática (o llame a OnJump() desde el script de salto de la plantilla).
/// Añádalo al GameObject del jugador (el que tiene Rigidbody2D).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class BallAudio : MonoBehaviour
{
    [Header("Rodado")]
    [Range(0, 1)] public float rollVolume = 0.35f;
    public float maxSpeed = 12f;              // velocidad a la que el rodado suena al máximo

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

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
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
        float speed01 = Mathf.Clamp01(Mathf.Abs(v.x) / maxSpeed);

        // Mapeo de parámetros físicos a parámetros de síntesis
        targetGain   = grounded ? speed01 * rollVolume : 0f;
        targetCutoff = Mathf.Lerp(150f, 2500f, speed01);              // más rápido = más brillante
        targetRumble = Mathf.Lerp(35f, 90f, speed01);                 // más rápido = zumbido más agudo

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

    // ---------------------------------------------------------------- HILO DE AUDIO
    // ¡Aquí no se puede usar la API de Unity! Solo matemáticas con valores precalculados.
    void OnAudioFilterRead(float[] data, int channels)
    {
        if (sampleRate == 0) return;
        for (int i = 0; i < data.Length; i += channels)
        {
            // Suavizado de parámetros (evita saltos bruscos / clics)
            gain       += (targetGain   - gain)       * 0.0005f;
            cutoff     += (targetCutoff - cutoff)     * 0.0005f;
            rumbleFreq += (targetRumble - rumbleFreq) * 0.0005f;

            // Ruido blanco (xorshift, rápido y seguro en el hilo de audio)
            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            float noise = (seed / (float)uint.MaxValue) * 2f - 1f;

            // Filtro pasa-bajos de 2 polos
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff / sampleRate);
            lp1 += a * (noise - lp1);
            lp2 += a * (lp1 - lp2);

            // Zumbido grave con ligera modulación (textura de "rodar")
            rumblePhase += rumbleFreq / sampleRate;
            float rumble = Mathf.Sin((float)(rumblePhase * 2 * System.Math.PI)) * (0.6f + 0.4f * lp1);

            float s = (lp2 * 1.5f + rumble * 0.5f) * gain;
            for (int c = 0; c < channels; c++) data[i + c] += s;
        }
    }
}
