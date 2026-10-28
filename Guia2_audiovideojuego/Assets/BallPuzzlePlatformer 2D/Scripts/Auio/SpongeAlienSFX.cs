using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ESTILO SONORO "ESPONJOSO + ALIEN" — 100 % SÍNTESIS, SIN ARCHIVOS DE AUDIO.
/// Versión SUAVE: ataques de al menos 6 ms, pasa-bajos global a 5 kHz, ring mod y FM moderados,
/// osciladores triangulares en lugar de sierra y picos más bajos (SOFTNESS).
///
/// ESPONJOSO (blando, gelatinoso, elástico):
///   · Bloop: seno cuyo tono SUBE rápido, como una burbuja que revienta.
///   · Gelatina: vibrato y trémolo que se amortiguan (el sonido "tiembla" y se calma, como un flan).
///   · Squash: el tono baja un instante antes de subir (aplastar y estirar).
///   · Ataques suaves y filtros pasa-bajos: nada suena duro ni metálico.
/// ALIEN (extraño, de otro planeta):
///   · Modulación en anillo (ring mod): multiplica por un seno inarmónico, timbre "de ovni".
///   · Filtro de FORMANTES: dos pasa-banda resonantes que imitan vocales (u-a-i), la "voz" del goo.
///   · Theremín: glissandos continuos con vibrato.
///   · Intervalos raros (7,5 semitonos = cuartos de tono) y FM con razones inarmónicas.
///   · Eco espacial con realimentación filtrada.
/// </summary>
public static class SpongeAlienSFX
{
    const int SR = 44100;
    const double TAU = 2.0 * Math.PI;
    static readonly System.Random rng = new System.Random();

    static float Rnd() => (float)rng.NextDouble();
    static float Noise() => Rnd() * 2f - 1f;
    static int N(float dur) => Mathf.CeilToInt(dur * SR);
    static float Midi(float m) => 440f * Mathf.Pow(2f, (m - 69f) / 12f);

    enum W { Sine, Tri, Saw, Noise }

    // ================================================================ BLOQUES DSP

    /// Oscilador con frecuencia variable en el tiempo. f(t) recibe segundos.
    static float[] Osc(float dur, Func<float, float> f, W w)
    {
        int n = N(dur);
        var d = new float[n];
        double ph = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            ph += f(t) / SR;
            double x = ph - Math.Floor(ph);
            switch (w)
            {
                case W.Sine: d[i] = (float)Math.Sin(TAU * ph); break;
                case W.Tri:  d[i] = (float)(4.0 * Math.Abs(x - 0.5) - 1.0); break;
                case W.Saw:  d[i] = (float)(2.0 * x - 1.0); break;
                default:     d[i] = Noise(); break;
            }
        }
        return d;
    }

    /// Envolvente blanda: ataque en curva seno² (sin clic) + caída polinómica hasta 0 en 'dur'.
    static float Env(float t, float dur, float attack, float curve)
    {
        attack = Mathf.Max(attack, 0.006f);              // SUAVE: ningún ataque más corto que 6 ms (sin "clics")
        if (t >= dur) return 0f;
        if (t < attack) { float a = Mathf.Sin(0.5f * Mathf.PI * t / attack); return a * a; }
        return Mathf.Pow(1f - (t - attack) / (dur - attack), curve);
    }

    /// "Gelatina": oscilación amortiguada. Devuelve 1 ± depth, y el temblor se apaga con 'damp'.
    static float Jelly(float t, float rate, float depth, float damp)
        => 1f + depth * Mathf.Exp(-damp * t) * Mathf.Sin((float)(TAU * rate * t));

    static void Apply(float[] d, Func<float, float> g)
    {
        for (int i = 0; i < d.Length; i++) d[i] *= g((float)i / SR);
    }

    /// Modulación en anillo: mezcla la señal con su producto por un seno inarmónico.
    static void RingMod(float[] d, float freq, float mix)
    {
        for (int i = 0; i < d.Length; i++)
        {
            float m = (float)Math.Sin(TAU * freq * i / SR);
            d[i] *= (1f - mix) + mix * m;
        }
    }

    /// Filtro pasa-bajos de un polo con corte variable.
    static void LowPass(float[] d, Func<float, float> fc)
    {
        float y = 0;
        for (int i = 0; i < d.Length; i++)
        {
            float a = 1f - Mathf.Exp(-2f * Mathf.PI * fc((float)i / SR) / SR);
            y += a * (d[i] - y);
            d[i] = y;
        }
    }

    // Formantes aproximados de vocales (F1, F2) en Hz
    static readonly float[,] Vowels = { { 300, 870 },   // 0 U
                                        { 500, 1000 },  // 1 O
                                        { 730, 1090 },  // 2 A
                                        { 530, 1840 },  // 3 E
                                        { 270, 2290 } };// 4 I
    public const int U = 0, O = 1, A = 2, E = 3, I = 4;

    /// FILTRO DE FORMANTES: dos pasa-banda resonantes (filtro de estado variable de Chamberlin)
    /// cuyos centros se mueven de la vocal 'from' a la vocal 'to' según morph(t) ∈ [0,1].
    /// Resultado: el sonido parece "decir" u→a, a→u, etc. — una voz gelatinosa alienígena.
    static float[] Vowel(float[] x, int from, int to, Func<float, float> morph, float q)
    {
        var y = new float[x.Length];
        float l1 = 0, b1 = 0, l2 = 0, b2 = 0;
        for (int i = 0; i < x.Length; i++)
        {
            float m = Mathf.Clamp01(morph((float)i / SR));
            float f1 = Mathf.Lerp(Vowels[from, 0], Vowels[to, 0], m);
            float f2 = Mathf.Lerp(Vowels[from, 1], Vowels[to, 1], m);
            float k1 = 2f * Mathf.Sin(Mathf.PI * f1 / SR);
            float k2 = 2f * Mathf.Sin(Mathf.PI * f2 / SR);
            float h1 = x[i] - l1 - q * b1; b1 += k1 * h1; l1 += k1 * b1;
            float h2 = x[i] - l2 - q * b2; b2 += k2 * h2; l2 += k2 * b2;
            y[i] = b1 + 0.6f * b2;
        }
        return y;
    }

    /// ECO ESPACIAL: línea de retardo con realimentación y pasa-bajos dentro del lazo
    /// (cada repetición suena más oscura, como si se alejara). Alarga el buffer para la cola.
    static float[] Echo(float[] x, float delaySec, float feedback, float wet = 0.5f)
    {
        int D = Mathf.Max(1, (int)(delaySec * SR));
        int tail = (int)(D * Mathf.Log(0.01f) / Mathf.Log(Mathf.Max(0.05f, feedback)));
        var y = new float[x.Length + Mathf.Clamp(tail, 0, SR * 2)];
        var line = new float[D];
        int w = 0; float lp = 0;
        for (int i = 0; i < y.Length; i++)
        {
            float dry = i < x.Length ? x[i] : 0f;
            float delayed = line[w];
            lp += 0.35f * (delayed - lp);
            line[w] = dry + lp * feedback;
            w = (w + 1) % D;
            y[i] = dry + delayed * wet;
        }
        return y;
    }

    static float[] Mix(params float[][] layers)
    {
        int n = 0;
        foreach (var l in layers) n = Mathf.Max(n, l.Length);
        var o = new float[n];
        foreach (var l in layers) for (int i = 0; i < l.Length; i++) o[i] += l[i];
        return o;
    }

    static float[] Gain(float[] d, float g) { for (int i = 0; i < d.Length; i++) d[i] *= g; return d; }

    /// Inserta 'src' dentro de 'dst' a partir del segundo 'at' (lo agranda si hace falta).
    static float[] Place(float[] dst, float[] src, float at, float gain)
    {
        int off = (int)(at * SR);
        if (off + src.Length > dst.Length) Array.Resize(ref dst, off + src.Length);
        for (int i = 0; i < src.Length; i++) dst[off + i] += src[i] * gain;
        return dst;
    }

    /// BURBUJA ("bloop"): seno cuyo tono sube de f0 a f1 en forma exponencial.
    /// Es el ladrillo básico del estilo esponjoso.
    static float[] Bloop(float dur, float f0, float f1, float attack = 0.004f)
    {
        var d = Osc(dur, t => f0 * Mathf.Pow(f1 / f0, Mathf.Min(1f, t / (dur * 0.7f))), W.Sine);
        Apply(d, t => Env(t, dur, attack, 2f));
        return d;
    }

    /// Lluvia de burbujas aleatorias (posición, tono y tamaño al azar).
    static float[] Bubbles(float span, int count, float fMin, float fMax, float gain)
    {
        var d = new float[N(span)];
        for (int k = 0; k < count; k++)
        {
            float f0 = Mathf.Lerp(fMin, fMax, Rnd());
            var b = Bloop(0.03f + 0.05f * Rnd(), f0, f0 * (1.6f + Rnd()));
            d = Place(d, b, Rnd() * span * 0.85f, gain * (0.5f + 0.5f * Rnd()));
        }
        return d;
    }

    /// Normaliza al pico pedido, suaviza bordes y empaqueta como AudioClip.
    /// Suavidad global: todos los efectos salen a este porcentaje del pico pedido (≈ -2,5 dB).
    const float SOFTNESS = 0.75f;

    static AudioClip ToClip(string name, float[] d, float peak)
    {
        // "Calidez": pasa-bajos suave a 5 kHz sobre TODO efecto, quita el brillo áspero
        LowPass(d, t => 5000f);
        peak *= SOFTNESS;
        float max = 0.0001f;
        foreach (var s in d) max = Mathf.Max(max, Mathf.Abs(s));
        int fade = Mathf.Min(256, d.Length / 4);
        for (int i = 0; i < d.Length; i++)
        {
            d[i] = d[i] / max * peak;
            if (i < 32) d[i] *= i / 32f;
            if (i > d.Length - fade) d[i] *= (float)(d.Length - i) / fade;
        }
        var clip = AudioClip.Create("alien_" + name, d.Length, 1, SR, false);
        clip.SetData(d, 0);
        return clip;
    }

    // ================================================================ PERSONAJE

    /// SALTO: squash (el tono baja 20 ms) → estirón ascendente con temblor de gelatina,
    /// y una "voz" que dice "u→i" (bwiii) gracias al filtro de formantes. Ring mod suave.
    public static AudioClip Jump(float v = 1f)
    {
        float dur = 0.34f;
        Func<float, float> f = t =>
        {
            float b = t < 0.02f ? Mathf.Lerp(240f, 195f, t / 0.02f)
                                : Mathf.Lerp(195f, 640f, Mathf.Sqrt((t - 0.02f) / (dur - 0.02f)));
            return b * v * Jelly(t, 14f, 0.06f, 7f);
        };
        var body = Osc(dur, f, W.Sine);
        var voice = Vowel(Osc(dur, f, W.Tri), U, I, t => t / dur, 0.35f);   // triángulo: más dulce que sierra
        var d = Mix(Gain(body, 0.9f), Gain(voice, 0.5f));
        Apply(d, t => Env(t, dur, 0.008f, 2f));
        RingMod(d, 87f, 0.08f);
        return ToClip("jump", Echo(d, 0.09f, 0.2f, 0.25f), 0.6f);
    }

    /// IMPACTO = "SPLAT/SQUISH": golpe grave que se hunde con temblor de gelatina,
    /// chapoteo de ruido por formantes a→u, y burbujas si el golpe es fuerte.
    /// 'k' (0..1) = fuerza del choque: más largo, más tembloroso y con más burbujas.
    public static AudioClip Impact(float k)
    {
        k = Mathf.Clamp01(k);
        float dur = 0.14f + 0.35f * k;
        float jRate = Mathf.Lerp(20f, 12f, k);
        var thud = Osc(dur, t => Mathf.Lerp(150f, 55f, Mathf.Min(1f, t / (0.06f + 0.1f * k)))
                                 * Jelly(t, jRate, 0.25f * k, 10f), W.Sine);
        Apply(thud, t => Env(t, dur, 0.004f, 2.5f) * (1f - 0.5f * Mathf.Exp(-8f * t) * (0.5f + 0.5f * Mathf.Sin((float)(TAU * jRate * t)))));
        var squelch = Vowel(Osc(dur, t => 0, W.Noise), A, U, t => t / dur, 0.25f);
        Apply(squelch, t => Env(t, dur * 0.8f, 0.002f, 3f));
        var d = Mix(thud, Gain(squelch, 0.12f + 0.25f * k));      // chapoteo apenas insinuado
        if (k > 0.4f) d = Place(d, Bubbles(0.25f, 2 + (int)(3 * k), 400f, 900f, 0.25f), 0.03f, 1f);
        LowPass(d, t => 1100f + 1400f * k);              // blando: casi sin agudos
        RingMod(d, 61f, 0.05f);
        return ToClip("impact_" + k.ToString("0.0"), d, 0.4f + 0.3f * k);
    }

    /// TRAMPOLÍN: "boiiing" gelatinoso — tono que sube con vibrato amortiguado fuerte,
    /// trémolo elástico, vocal u→o y eco.
    public static AudioClip Bounce()
    {
        float dur = 0.6f;
        Func<float, float> f = t => Mathf.Lerp(160f, 420f, 1f - Mathf.Exp(-6f * t)) * Jelly(t, 11f, 0.12f, 4f);
        var d = Mix(Osc(dur, f, W.Sine), Gain(Vowel(Osc(dur, f, W.Tri), U, O, t => t / dur, 0.3f), 1.5f));
        Apply(d, t => Env(t, dur, 0.005f, 1.5f) * Jelly(t, 11f, 0.4f, 5f));
        return ToClip("bounce", Echo(d, 0.12f, 0.25f, 0.3f), 0.7f);
    }

    /// PASO EN ESCALERA: "squeak" blandito (burbuja corta + ruido con vocal o).
    public static AudioClip ClimbTick(float p = 1f)
    {
        var b = Bloop(0.07f, 420f * p, 620f * p, 0.003f);
        var n = Vowel(Osc(0.05f, t => 0, W.Noise), O, O, t => 0, 0.3f);
        Apply(n, t => Env(t, 0.05f, 0.002f, 3f));
        var d = Mix(b, Gain(n, 0.4f));
        LowPass(d, t => 3000f);
        return ToClip("climb", d, 0.4f);
    }

    /// LA BOLA SE ROMPE = "SPLAT EN GOO": SÍNTESIS GRANULAR con granos que son burbujas
    /// (45 bloops de 15-60 ms dispersos al azar, más densos al principio) + chapoteo + golpe.
    public static AudioClip Shatter()
    {
        var d = new float[N(1.0f)];
        for (int g = 0; g < 45; g++)
        {
            float at = Mathf.Pow(Rnd(), 2f) * 0.85f;
            float f0 = 300f + 1200f * Rnd();
            d = Place(d, Bloop(0.015f + 0.045f * Rnd(), f0, f0 * (1.6f + 0.8f * Rnd()), 0.002f), at, (1f - at) * (0.3f + 0.4f * Rnd()));
        }
        var splat = Vowel(Osc(0.4f, t => 0, W.Noise), A, U, t => t / 0.4f, 0.2f);
        Apply(splat, t => Env(t, 0.4f, 0.002f, 2.5f));
        var thud = Osc(0.35f, t => Mathf.Lerp(110f, 40f, t / 0.35f) * Jelly(t, 20f, 0.15f, 8f), W.Sine);
        Apply(thud, t => Env(t, 0.35f, 0.003f, 2.5f));
        LowPass(splat, t => 1800f);
        d = Mix(d, Gain(splat, 0.5f), Gain(thud, 1.1f));
        return ToClip("shatter", Echo(d, 0.13f, 0.25f, 0.3f), 0.75f);
    }

    // ================================================================ OBJETOS

    /// RECOGER: dos "blips" burbujeantes separados 7,5 semitonos (un cuarto de tono = intervalo alien)
    /// con destello FM de razón inarmónica 2,71 y eco.
    public static AudioClip Pickup(float shift = 0f)
    {
        var a = Bloop(0.07f, Midi(84 + shift), Midi(84 + shift) * 1.25f, 0.003f);
        float fb = Midi(91.5f + shift);
        int n = N(0.18f);
        var b = new float[n];
        double ph = 0, pm = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            pm += fb * 2.71 / SR; ph += fb / SR;
            b[i] = (float)Math.Sin(TAU * ph + 1.4 * Math.Exp(-12 * t) * Math.Sin(TAU * pm)) * Env(t, 0.18f, 0.003f, 2f);
        }
        var d = Place(a, b, 0.06f, 1f);
        return ToClip("pickup", Echo(d, 0.11f, 0.3f, 0.35f), 0.55f);
    }

    /// POWER-UP: glissando de theremín (from → to) con vibrato, ring mod y burbujas al final.
    public static AudioClip PowerUp(float from, float to)
    {
        float dur = 0.5f;
        Func<float, float> f = t => from * Mathf.Pow(to / from, Mathf.Min(1f, t / (dur * 0.8f))) * (1f + 0.03f * Mathf.Sin((float)(TAU * 7f * t)));
        var d = Mix(Osc(dur, f, W.Sine), Gain(Osc(dur, t => f(t) * 1.5f, W.Tri), 0.25f));
        Apply(d, t => Env(t, dur, 0.02f, 1.5f));
        RingMod(d, 120f, 0.12f);
        d = Place(d, Bubbles(0.2f, 4, Mathf.Max(from, to), Mathf.Max(from, to) * 2f, 0.3f), 0.3f, 1f);
        return ToClip("powerup", Echo(d, 0.1f, 0.3f, 0.3f), 0.6f);
    }

    // ================================================================ PUERTAS / INTERRUPTORES

    /// PUERTA = "ESCOTILLA DE MEMBRANA": respiración de ruido por formantes (u→a al abrir, a→u al cerrar),
    /// theremín grave que sube/baja, burbujas y un "plop" blando al final (nada metálico).
    public static AudioClip Door(bool open)
    {
        float dur = 1.0f;
        var breath = Vowel(Osc(dur, t => 0, W.Noise), open ? U : A, open ? A : U, t => t / dur, 0.12f);
        Apply(breath, t => Env(t, dur, 0.25f, 1.2f));
        var hum = Osc(dur, t => (open ? Mathf.Lerp(70f, 140f, t / dur) : Mathf.Lerp(140f, 70f, t / dur)) * (1f + 0.03f * Mathf.Sin((float)(TAU * 5.5f * t))), W.Sine);
        Apply(hum, t => Env(t, dur, 0.15f, 1f));
        var d = Mix(Gain(breath, 0.3f), Gain(hum, 0.55f), Gain(Bubbles(dur, 6, 250f, 700f, 1f), 0.2f));
        var plop = Osc(0.2f, t => Mathf.Lerp(open ? 160f : 120f, 50f, t / 0.2f) * Jelly(t, 16f, 0.15f, 9f), W.Sine);
        Apply(plop, t => Env(t, 0.2f, 0.003f, 2.5f));
        d = Place(d, plop, dur * 0.85f, 0.9f);
        LowPass(d, t => 1600f);                           // membrana blanda: sin siseo
        RingMod(d, 43f, 0.08f);
        return ToClip(open ? "door_open" : "door_close", d, 0.65f);
    }

    /// INTERRUPTOR: "pop" de burbuja (sube al encender, baja al apagar) + ring mod + eco corto.
    public static AudioClip Switch(bool on)
    {
        var d = on ? Bloop(0.09f, 500f, 1300f, 0.002f) : Bloop(0.09f, 900f, 350f, 0.002f);
        var tick = Osc(0.004f, t => 0, W.Noise);
        LowPass(tick, t => 3000f);
        d = Mix(d, Gain(tick, 0.5f));
        RingMod(d, 210f, 0.1f);
        return ToClip(on ? "switch_on" : "switch_off", Echo(d, 0.06f, 0.15f, 0.2f), 0.55f);
    }

    // ================================================================ ENEMIGOS / OBSTÁCULOS

    /// ENEMIGO: "zap-gloop": FM inarmónica (1,37) que cae, gruñido por ring mod a 33 Hz y vocal o→u.
    public static AudioClip EnemyHit(float v = 1f)
    {
        float dur = 0.35f;
        int n = N(dur);
        var d = new float[n];
        double pc = 0, pm = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR, p = t / dur;
            float fc = 180f * v * (1f - 0.5f * p);
            pm += fc * 1.37 / SR; pc += fc / SR;
            d[i] = (float)Math.Sin(TAU * pc + 4f * (1f - p) * Math.Sin(TAU * pm));
        }
        d = Mix(Gain(d, 0.6f), Vowel(d, O, U, t => t / dur, 0.3f));
        Apply(d, t => Env(t, dur, 0.01f, 2f));
        LowPass(d, t => 2200f);
        RingMod(d, 33f, 0.25f);
        return ToClip("enemy_hit", d, 0.65f);
    }

    /// ENEMIGO DERROTADO: "globo que se desinfla": sierra que cae con temblor, vocal a→u y burbujas.
    public static AudioClip EnemyDie()
    {
        float dur = 0.8f;
        var d = Vowel(Osc(dur, t => 400f * Mathf.Pow(60f / 400f, t / dur) * Jelly(t, 9f, 0.08f, 1f), W.Saw), A, U, t => t / dur, 0.2f);
        Apply(d, t => Env(t, dur, 0.01f, 1.5f));
        d = Place(d, Bubbles(0.4f, 4, 300f, 600f, 0.4f), 0.3f, 1f);
        LowPass(d, t => 2000f);
        return ToClip("enemy_die", d, 0.65f);
    }

    /// PELIGRO (sierras): metal "de otro planeta": FM 1,41 + ring mod 97 Hz + eco corto.
    public static AudioClip Hazard()
    {
        float dur = 0.4f;
        int n = N(dur);
        var d = new float[n];
        double pc = 0, pm = 0;
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR, p = t / dur;
            float fc = 520f * (1f - 0.2f * p);
            pm += fc * 1.41 / SR; pc += fc / SR;
            d[i] = (float)Math.Sin(TAU * pc + 5f * (1f - p) * Math.Sin(TAU * pm)) * Env(t, dur, 0.002f, 2f);
        }
        LowPass(d, t => 2500f);
        RingMod(d, 97f, 0.15f);
        return ToClip("hazard", Echo(d, 0.07f, 0.25f, 0.25f), 0.55f);
    }

    // ================================================================ EVENTOS DE JUEGO

    /// VICTORIA: melodía de theremín en modo LIDIO (Do-Mi-Fa#-Si-Do… Mi) con PORTAMENTO
    /// (el tono se desliza de una nota a la otra), nota final con vibrato creciente,
    /// destellos de burbujas agudas y eco espacial.
    public static AudioClip Victory()
    {
        int[] notes = { 72, 76, 78, 83, 84, 88 };
        float step = 0.13f, hold = 0.9f, dur = step * (notes.Length - 1) + hold;
        int n = N(dur);
        var d = new float[n];
        double ph = 0; float cur = Midi(notes[0]);
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / SR;
            int idx = Mathf.Min(notes.Length - 1, (int)(t / step));
            cur += (Midi(notes[idx]) - cur) * 0.0025f;                       // portamento (~10 ms)
            float tHold = Mathf.Max(0f, t - step * (notes.Length - 1));
            float vib = 1f + 0.012f * Mathf.Min(1f, tHold * 2f) * Mathf.Sin((float)(TAU * 6f * t));
            ph += cur * vib / SR;
            float x = (float)(Math.Sin(TAU * ph) + 0.3 * Math.Sin(TAU * 2 * ph));
            d[i] = x * Env(t, dur, 0.01f, 1.2f);
        }
        d = Place(d, Bubbles(hold, 10, 1500f, 4000f, 0.15f), step * (notes.Length - 1), 1f);
        return ToClip("victory", Echo(d, 0.18f, 0.3f, 0.35f), 0.7f);
    }

    /// DERROTA: "suspiro que se desinfla": voz de sierra que cae de 330 a 110 Hz por formantes a→u,
    /// vibrato que crece (temblor triste), sub-octava, filtro que se cierra y burbujas que se apagan.
    public static AudioClip Defeat()
    {
        float dur = 1.3f;
        Func<float, float> f = t => 330f * Mathf.Pow(110f / 330f, t / dur) * (1f + 0.05f * (t / dur) * Mathf.Sin((float)(TAU * 7f * t)));
        var voice = Vowel(Osc(dur, f, W.Tri), A, U, t => t / dur, 0.3f);    // triángulo: suspiro más suave
        var sub = Osc(dur, t => f(t) * 0.5f, W.Sine);
        var d = Mix(voice, Gain(sub, 0.5f));
        Apply(d, t => Env(t, dur, 0.02f, 1.1f));
        LowPass(d, t => Mathf.Lerp(3000f, 400f, t / dur));
        d = Place(d, Bubbles(0.4f, 3, 150f, 300f, 0.5f), dur - 0.45f, 1f);
        RingMod(d, 23f, 0.05f);
        return ToClip("defeat", d, 0.7f);
    }

    /// REINICIO: "rebobinado de burbujas": 8 bloops que suben de 300 a 1200 Hz + theremín baja-sube.
    public static AudioClip Restart()
    {
        float dur = 0.45f;
        var sweep = Osc(dur, t => t < dur / 2 ? Mathf.Lerp(900f, 220f, t / (dur / 2)) : Mathf.Lerp(220f, 1100f, (t - dur / 2) / (dur / 2)), W.Sine);
        Apply(sweep, t => Env(t, dur, 0.02f, 1f));
        var d = Gain(sweep, 0.5f);
        for (int k = 0; k < 8; k++)
        {
            float f0 = 300f * Mathf.Pow(4f, k / 7f);
            d = Place(d, Bloop(0.05f, f0, f0 * 1.8f, 0.002f), k * 0.045f, 0.5f);
        }
        return ToClip("restart", Echo(d, 0.08f, 0.2f, 0.25f), 0.55f);
    }

    /// TRANSICIÓN = "WARP": ruido por formantes u→i→u, theremín que sube y baja, ring mod y eco.
    public static AudioClip Transition()
    {
        float dur = 1.1f;
        var air = Vowel(Osc(dur, t => 0, W.Noise), U, I, t => Mathf.Sin(Mathf.PI * t / dur), 0.1f);
        Apply(air, t => Env(t, dur, 0.45f, 1.5f));
        var th = Osc(dur, t => (200f + 1000f * Mathf.Sin(Mathf.PI * t / dur)) * (1f + 0.02f * Mathf.Sin((float)(TAU * 6f * t))), W.Sine);
        Apply(th, t => Env(t, dur, 0.3f, 1.5f));
        LowPass(air, t => 2500f);
        var d = Mix(Gain(air, 0.5f), Gain(th, 0.35f));
        RingMod(d, 140f, 0.1f);
        return ToClip("transition", Echo(d, 0.15f, 0.3f, 0.3f), 0.5f);
    }

    /// CLIC DE INTERFAZ: burbujita.
    public static AudioClip UIClick()
        => ToClip("ui", Echo(Bloop(0.05f, 700f, 1500f, 0.002f), 0.05f, 0.15f, 0.2f), 0.4f);
}
