using UnityEngine;

/// <summary>
/// ZUMBIDO PROCEDURAL PARA PLATAFORMAS MÓVILES, SIERRAS, ASCENSORES Y MECANISMOS.
/// Genera en tiempo real una onda diente de sierra filtrada + ruido. La frecuencia y el volumen
/// dependen de la velocidad real del objeto (calculada por diferencia de posición), y el
/// volumen y paneo dependen de la distancia/posición respecto a la cámara.
/// Así una plataforma que acelera "sube de tono" y una que se detiene se apaga sola.
/// ESTILO ESPONJOSO + ALIEN: en vez de sierras de motor suena un "ovni": seno con algo de FM,
/// vibrato de theremín (5,5 Hz), ring mod con razón inarmónica 1,37 y un trémolo blando que
/// "respira" como una gelatina. Mismo control dinámico (velocidad, distancia, paneo).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MechanismHum : MonoBehaviour
{
    [Range(0, 1)] public float maxVolume = 0.12f;
    public float baseFreq = 55f;          // Hz con el objeto lento
    public float freqPerSpeed = 12f;      // Hz extra por unidad de velocidad
    public float speedForFullVolume = 3f;
    public float hearingDistance = 12f;   // a partir de aquí no se oye
    [Tooltip("Para sierras o rotores que giran sin desplazarse: suena siempre a esta velocidad")]
    public float constantSpeed = 0f;

    Vector3 lastPos;
    Camera cam;
    volatile float tGain, tFreq, tPan;
    float gain, freq = 55f, pan, lp;
    double ph, ph2, vibPh, breathPh;
    int sr;
    uint seed = 98765;

    void Awake()
    {
        sr = AudioSettings.outputSampleRate;
        lastPos = transform.position;
        var src = GetComponent<AudioSource>();
        src.clip = null; src.loop = true; src.playOnAwake = true; src.spatialBlend = 0f;
        if (AudioManager.I != null) src.outputAudioMixerGroup = AudioManager.I.sfxGroup;
        src.Play();
    }

    void Update()
    {
        if (cam == null) cam = Camera.main;
        float dt = Mathf.Max(Time.deltaTime, 0.0001f);
        float speed = constantSpeed > 0 ? constantSpeed : (transform.position - lastPos).magnitude / dt;
        lastPos = transform.position;

        float dist = cam ? Vector2.Distance(cam.transform.position, transform.position) : 0f;
        float att = 1f - Mathf.Clamp01(dist / hearingDistance);        // atenuación por distancia
        tGain = maxVolume * Mathf.Clamp01(speed / speedForFullVolume) * att * att;
        tFreq = baseFreq + speed * freqPerSpeed;
        if (cam) tPan = Mathf.Clamp((transform.position.x - cam.transform.position.x) / (cam.orthographicSize * cam.aspect), -1f, 1f) * 0.7f;
    }

    void OnAudioFilterRead(float[] data, int channels)
    {
        if (sr == 0) return;
        for (int i = 0; i < data.Length; i += channels)
        {
            gain += (tGain - gain) * 0.0004f;
            freq += (tFreq - freq) * 0.0004f;
            pan  += (tPan - pan) * 0.0004f;

            seed ^= seed << 13; seed ^= seed >> 17; seed ^= seed << 5;
            float n = (seed / (float)uint.MaxValue) * 2f - 1f;
            float x;

            if (AudioManager.AlienStyle)
            {
                const double TAU = 2.0 * System.Math.PI;
                vibPh += 5.5 / sr;  if (vibPh > 1.0) vibPh -= 1.0;
                breathPh += 1.3 / sr; if (breathPh > 1.0) breathPh -= 1.0;
                float f = freq * 1.5f * (1f + 0.025f * (float)System.Math.Sin(TAU * vibPh));
                ph  += f / sr;         if (ph  > 1.0) ph  -= 1.0;
                ph2 += f * 1.37 / sr;  if (ph2 > 1.0) ph2 -= 1.0;
                double carrier = System.Math.Sin(TAU * ph);
                double fm = System.Math.Sin(TAU * 2 * ph + 0.6 * carrier);       // un toque leve de FM
                float ring = (float)((carrier + 0.12 * fm) * (0.8 + 0.2 * System.Math.Sin(TAU * ph2)));
                float breath = 0.75f + 0.25f * (float)System.Math.Sin(TAU * breathPh);   // "respira" como gelatina
                x = ring * 0.6f * breath + n * 0.015f;
            }
            else
            {
                ph  += freq / sr;
                ph2 += freq * 1.007 / sr;                     // segunda sierra desafinada = "batido" mecánico
                float saw = (float)((ph - System.Math.Floor(ph)) * 2 - 1 + (ph2 - System.Math.Floor(ph2)) * 2 - 1) * 0.5f;
                x = saw * 0.7f + n * 0.3f;
            }
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * (freq * 3.5f) / sr);     // filtro más cerrado = más suave
            lp += a * (x - lp);

            float s = lp * gain;
            if (channels == 2)
            {
                data[i]     += s * Mathf.Clamp01(1f - pan);
                data[i + 1] += s * Mathf.Clamp01(1f + pan);
            }
            else for (int c = 0; c < channels; c++) data[i + c] += s;
        }
    }
}
