using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// GENERADOR DE EFECTOS SONOROS POR SÍNTESIS.
/// Ningún sonido de esta clase es pregrabado: cada AudioClip se calcula muestra por muestra
/// con osciladores (seno, cuadrada, sierra, triángulo), ruido blanco, envolventes,
/// barridos de frecuencia, modulación FM y filtros digitales.
/// </summary>
public static class ProceduralSFX
{
    public const int SR = 44100;                       // frecuencia de muestreo
    static readonly System.Random rng = new System.Random();

    public enum Wave { Sine, Square, Saw, Triangle, Noise }

    // ---------------------------------------------------------------- utilidades DSP

    static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);

    /// Oscilador. 'phase' se mide en ciclos (0..1 = un periodo).
    static float Osc(Wave w, double phase)
    {
        double x = phase - Math.Floor(phase);
        switch (w)
        {
            case Wave.Sine:     return (float)Math.Sin(2.0 * Math.PI * x);
            case Wave.Square:   return x < 0.5 ? 1f : -1f;
            case Wave.Saw:      return (float)(2.0 * x - 1.0);
            case Wave.Triangle: return (float)(4.0 * Math.Abs(x - 0.5) - 1.0);
            default:            return Noise();
        }
    }

    /// Envolvente ataque lineal + caída exponencial. p = progreso 0..1.
    static float Env(float p, float attack, float curve = 2f)
    {
        if (p < attack) return p / attack;
        return Mathf.Pow(1f - (p - attack) / (1f - attack), curve);
    }

    /// Genera un tono con frecuencia variable en el tiempo (barrido / glissando).
    static float[] Tone(float dur, Func<float, float> freq, Wave w, float attack, float amp, float curve = 2f)
    {
        int n = Mathf.CeilToInt(dur * SR);
        var d = new float[n];
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            float p = (float)i / n;
            ph += freq(p) / SR;
            d[i] = Osc(w, ph) * Env(p, attack, curve) * amp;
        }
        return d;
    }

    /// Síntesis FM: portadora modulada por otra onda (timbres metálicos / gruñidos).
    static float[] FM(float dur, float carrier, float ratio, float index, float attack, float amp, float pitchDrop = 0f)
    {
        int n = Mathf.CeilToInt(dur * SR);
        var d = new float[n];
        double pc = 0, pm = 0;
        for (int i = 0; i < n; i++)
        {
            float p = (float)i / n;
            float fc = carrier * (1f - pitchDrop * p);
            pm += fc * ratio / SR;
            float mod = (float)Math.Sin(2 * Math.PI * pm) * index * (1f - p);   // el índice decae = el timbre se "suaviza"
            pc += fc / SR;
            d[i] = (float)Math.Sin(2 * Math.PI * pc + mod) * Env(p, attack) * amp;
        }
        return d;
    }

    /// Filtro pasa-bajos de un polo con frecuencia de corte variable en el tiempo.
    static void LowPass(float[] d, Func<float, float> cutoff)
    {
        float y = 0;
        for (int i = 0; i < d.Length; i++)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * cutoff((float)i / d.Length) / SR);
            y += a * (d[i] - y);
            d[i] = y;
        }
    }

    /// Suma varias capas (con longitudes distintas).
    static float[] Mix(params float[][] layers)
    {
        int n = 0;
        foreach (var l in layers) n = Mathf.Max(n, l.Length);
        var o = new float[n];
        foreach (var l in layers) for (int i = 0; i < l.Length; i++) o[i] += l[i];
        return o;
    }

    /// Concatena fragmentos (útil para arpegios y melodías).
    static float[] Concat(params float[][] parts)
    {
        var list = new List<float>();
        foreach (var p in parts) list.AddRange(p);
        return list.ToArray();
    }

    static float Midi(float m) => 440f * Mathf.Pow(2f, (m - 69f) / 12f);

    /// Normaliza, aplica micro-fundidos anti-clic y empaqueta en AudioClip.
    static AudioClip ToClip(string name, float[] d, float peak = 0.9f)
    {
        float max = 0.0001f;
        foreach (var s in d) max = Mathf.Max(max, Mathf.Abs(s));
        int fade = Mathf.Min(64, d.Length / 4);
        for (int i = 0; i < d.Length; i++)
        {
            d[i] = d[i] / max * peak;
            if (i < fade) d[i] *= (float)i / fade;
            if (i > d.Length - fade) d[i] *= (float)(d.Length - i) / fade;
        }
        var clip = AudioClip.Create(name, d.Length, 1, SR, false);
        clip.SetData(d, 0);
        return clip;
    }

    // ---------------------------------------------------------------- PERSONAJE

    /// Salto: barrido ascendente (glissando) de onda cuadrada + seno. 'variation' cambia el registro.
    public static AudioClip Jump(float variation = 1f)
    {
        float f0 = 260f * variation, f1 = 760f * variation;
        var body = Tone(0.20f, p => Mathf.Lerp(f0, f1, Mathf.Sqrt(p)), Wave.Square, 0.02f, 0.35f);
        var sub  = Tone(0.20f, p => Mathf.Lerp(f0, f1, Mathf.Sqrt(p)) * 0.5f, Wave.Sine, 0.02f, 0.5f);
        var mix = Mix(body, sub);
        LowPass(mix, p => 3000f);
        return ToClip("sfx_jump", mix, 0.7f);
    }

    /// Impacto/aterrizaje: golpe grave con caída de tono + ruido filtrado.
    /// 'intensity' (0..1) controla duración, brillo del ruido y profundidad.
    public static AudioClip Impact(float intensity)
    {
        intensity = Mathf.Clamp01(intensity);
        float dur = 0.08f + 0.25f * intensity;
        var thump = Tone(dur, p => Mathf.Lerp(140f, 45f, p), Wave.Sine, 0.005f, 1f, 3f);
        var noise = Tone(dur * 0.7f, p => 0, Wave.Noise, 0.002f, 0.6f, 4f);
        float bright = 600f + 5000f * intensity;
        LowPass(noise, p => Mathf.Lerp(bright, 200f, p));
        return ToClip("sfx_impact_" + intensity.ToString("0.0"), Mix(thump, noise), 0.6f + 0.3f * intensity);
    }

    /// Rebote elástico (resortes, plataformas saltarinas): "boing" con vibrato.
    public static AudioClip Bounce()
    {
        var d = Tone(0.35f, p => 180f + 220f * p + 25f * Mathf.Sin(p * 60f), Wave.Triangle, 0.01f, 0.8f);
        return ToClip("sfx_bounce", d);
    }

    // ---------------------------------------------------------------- OBJETOS

    /// Recoger objeto: arpegio rápido de dos notas (onda cuadrada).
    public static AudioClip Pickup(float semitoneShift = 0f)
    {
        var a = Tone(0.06f, p => Midi(88 + semitoneShift), Wave.Square, 0.01f, 0.4f);
        var b = Tone(0.18f, p => Midi(95 + semitoneShift), Wave.Square, 0.01f, 0.4f);
        var d = Concat(a, b);
        LowPass(d, p => 5000f);
        return ToClip("sfx_pickup", d, 0.6f);
    }

    // ---------------------------------------------------------------- PUERTAS / INTERRUPTORES

    /// Puerta: motor grave (sierra que sube/baja) + ruido de fricción + golpe final.
    public static AudioClip Door(bool open)
    {
        float dur = 0.9f;
        Func<float, float> f = open ? (Func<float, float>)(p => Mathf.Lerp(55f, 95f, p))
                                    : (p => Mathf.Lerp(95f, 55f, p));
        var motor = Tone(dur, f, Wave.Saw, 0.15f, 0.5f, 1f);
        LowPass(motor, p => 400f + 300f * Mathf.Sin(p * Mathf.PI));
        var scrape = Tone(dur, p => 0, Wave.Noise, 0.2f, 0.25f, 1f);
        LowPass(scrape, p => 900f);
        var slam = new float[Mathf.CeilToInt(dur * SR) + SR / 4];
        var hit = Impact(0.7f); var hd = new float[hit.samples]; hit.GetData(hd, 0);
        int off = Mathf.CeilToInt(dur * 0.85f * SR);
        for (int i = 0; i < hd.Length && off + i < slam.Length; i++) slam[off + i] = hd[i] * 0.8f;
        return ToClip(open ? "sfx_door_open" : "sfx_door_close", Mix(motor, scrape, slam));
    }

    /// Interruptor: clic (ráfaga de ruido de 4 ms) + tono corto (agudo = ON, grave = OFF).
    public static AudioClip Switch(bool on)
    {
        var click = Tone(0.004f, p => 0, Wave.Noise, 0.001f, 1f, 1f);
        var tone = Tone(0.08f, p => on ? 1400f : 900f, Wave.Sine, 0.002f, 0.5f, 3f);
        return ToClip(on ? "sfx_switch_on" : "sfx_switch_off", Mix(click, tone), 0.7f);
    }

    // ---------------------------------------------------------------- ENEMIGOS / OBSTÁCULOS

    /// Enemigo (golpe/ataque): gruñido FM grave con caída de tono.
    public static AudioClip EnemyHit(float variation = 1f)
        => ToClip("sfx_enemy_hit", FM(0.35f, 110f * variation, 0.5f, 6f, 0.01f, 0.9f, 0.4f));

    /// Enemigo derrotado: FM que cae + ruido.
    public static AudioClip EnemyDie()
    {
        var fm = FM(0.6f, 300f, 1.5f, 8f, 0.005f, 0.8f, 0.85f);
        var n = Tone(0.3f, p => 0, Wave.Noise, 0.002f, 0.4f, 3f);
        LowPass(n, p => 2500f);
        return ToClip("sfx_enemy_die", Mix(fm, n));
    }

    /// Obstáculo peligroso (pinchos, sierras): FM con razón inarmónica (1.41) = timbre metálico.
    public static AudioClip Hazard()
        => ToClip("sfx_hazard", FM(0.4f, 520f, 1.41f, 9f, 0.002f, 0.8f, 0.2f));

    // ---------------------------------------------------------------- EVENTOS DE JUEGO

    /// Victoria: arpegio mayor ascendente (Do-Mi-Sol-Do) con acorde final.
    public static AudioClip Victory()
    {
        int[] notes = { 72, 76, 79, 84 };
        var parts = new List<float[]>();
        foreach (var m in notes) parts.Add(Tone(0.12f, p => Midi(m), Wave.Square, 0.01f, 0.35f));
        var chord = Mix(Tone(0.9f, p => Midi(84), Wave.Triangle, 0.02f, 0.4f, 1.5f),
                        Tone(0.9f, p => Midi(88), Wave.Triangle, 0.02f, 0.3f, 1.5f),
                        Tone(0.9f, p => Midi(91), Wave.Triangle, 0.02f, 0.3f, 1.5f));
        parts.Add(chord);
        var d = Concat(parts.ToArray());
        LowPass(d, p => 6000f);
        return ToClip("sfx_victory", d);
    }

    /// Derrota: melodía cromática descendente en sierra con vibrato creciente y filtro que se cierra.
    public static AudioClip Defeat()
    {
        int[] notes = { 67, 66, 65, 64 };
        var parts = new List<float[]>();
        for (int k = 0; k < notes.Length; k++)
        {
            int m = notes[k]; bool last = k == notes.Length - 1;
            parts.Add(Tone(last ? 0.8f : 0.22f, p => Midi(m) * (1f + (last ? 0.03f : 0.01f) * Mathf.Sin(p * 40f)),
                           Wave.Saw, 0.01f, 0.5f, last ? 1.2f : 1f));
        }
        var d = Concat(parts.ToArray());
        LowPass(d, p => Mathf.Lerp(3000f, 400f, p));
        return ToClip("sfx_defeat", d);
    }

    /// Reinicio: barrido "rebobinado" descendente y luego ascendente.
    public static AudioClip Restart()
    {
        var d = Tone(0.4f, p => p < 0.5f ? Mathf.Lerp(900f, 200f, p * 2f) : Mathf.Lerp(200f, 1100f, (p - 0.5f) * 2f),
                     Wave.Triangle, 0.02f, 0.6f, 1f);
        return ToClip("sfx_restart", d, 0.6f);
    }

    /// Transición entre niveles: "whoosh" de ruido con filtro que se abre y se cierra.
    public static AudioClip Transition()
    {
        var d = Tone(1.0f, p => 0, Wave.Noise, 0.5f, 0.8f, 1.5f);
        LowPass(d, p => 150f + 4500f * Mathf.Sin(p * Mathf.PI));
        return ToClip("sfx_transition", d, 0.6f);
    }

    /// Clic de interfaz.
    public static AudioClip UIClick()
        => ToClip("sfx_ui", Tone(0.05f, p => 1800f, Wave.Sine, 0.002f, 0.5f, 3f), 0.5f);

    // ---------------------------------------------------------------- AÑADIDOS PARA ESTA PLANTILLA

    /// Paso en escalera: golpecito de madera (ruido filtrado + tono corto).
    public static AudioClip ClimbTick(float pitch = 1f)
    {
        var n = Tone(0.05f, p => 0, Wave.Noise, 0.002f, 0.6f, 4f);
        LowPass(n, p => 1800f * pitch);
        var t = Tone(0.06f, p => 320f * pitch, Wave.Triangle, 0.002f, 0.5f, 4f);
        return ToClip("sfx_climb", Mix(n, t), 0.5f);
    }

    /// Rotura de la bola (Smash): SÍNTESIS GRANULAR.
    /// Se dispersan ~60 "granos" (micro-sonidos de 5-30 ms) en posiciones aleatorias,
    /// con tono y brillo aleatorios y densidad que decae: suena a vidrio/cerámica quebrándose.
    public static AudioClip Shatter()
    {
        float dur = 0.9f;
        int n = Mathf.CeilToInt(dur * SR);
        var d = new float[n];
        for (int g = 0; g < 60; g++)
        {
            float startP = Mathf.Pow((float)rng.NextDouble(), 2.2f);           // más granos al principio
            int start = (int)(startP * n * 0.8f);
            int len = (int)(SR * (0.005f + 0.025f * (float)rng.NextDouble()));
            float f = 1500f + 6000f * (float)rng.NextDouble();
            float amp = (1f - startP) * (0.4f + 0.6f * (float)rng.NextDouble());
            double ph = 0;
            for (int i = 0; i < len && start + i < n; i++)
            {
                float p = (float)i / len;
                ph += f / SR;
                float grain = (Osc(Wave.Sine, ph) * 0.6f + Noise() * 0.4f) * Mathf.Sin(p * Mathf.PI); // ventana de Hann aprox.
                d[start + i] += grain * amp;
            }
        }
        var thump = Tone(0.25f, p => Mathf.Lerp(120f, 40f, p), Wave.Sine, 0.003f, 1.2f, 3f);
        return ToClip("sfx_shatter", Mix(d, thump));
    }

    /// Power-up: barrido de frecuencia con vibrato + una quinta justa encima.
    /// Si toHz > fromHz el sonido sube (velocidad, salto, crecer); si es menor, baja (encoger).
    public static AudioClip PowerUp(float fromHz, float toHz, Wave w)
    {
        var a = Tone(0.45f, p => Mathf.Lerp(fromHz, toHz, p) * (1f + 0.02f * Mathf.Sin(p * 90f)), w, 0.02f, 0.4f, 1.5f);
        var b = Tone(0.45f, p => Mathf.Lerp(fromHz, toHz, p) * 1.5f, Wave.Sine, 0.05f, 0.25f, 1.5f);   // quinta justa
        var d = Mix(a, b);
        LowPass(d, p => 4000f);
        return ToClip("sfx_powerup", d, 0.7f);
    }
}
