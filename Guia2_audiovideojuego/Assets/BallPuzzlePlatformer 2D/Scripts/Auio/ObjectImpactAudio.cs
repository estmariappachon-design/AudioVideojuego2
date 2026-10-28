using UnityEngine;

/// <summary>
/// IMPACTOS DE OBJETOS FÍSICOS (cajas de madera de la plantilla: wood-1, wood-2, wood-3).
/// Reutiliza el impacto sintetizado, pero lo transforma: tono más grave (pitch) según el tamaño
/// del objeto y volumen según la velocidad del choque. PhysicsObject.Start() lo añade solo.
/// </summary>
public class ObjectImpactAudio : MonoBehaviour
{
    [Tooltip("Tono base. Se ajusta solo según el tamaño: objetos grandes suenan más graves")]
    public float basePitch = 0.75f;
    float cooldown;

    void OnCollisionEnter2D(Collision2D c)
    {
        if (Time.time < cooldown || AudioManager.I == null) return;
        cooldown = Time.time + 0.08f;                     // evita ráfagas cuando el objeto vibra
        float size = Mathf.Max(0.3f, transform.lossyScale.magnitude);
        float pitch = basePitch / Mathf.Sqrt(size);
        float normalSpeed = Mathf.Abs(Vector2.Dot(c.relativeVelocity, c.GetContact(0).normal));
        AudioManager.I.ObjectImpact(normalSpeed, transform.position, Mathf.Clamp(pitch, 0.4f, 1.3f));
    }
}
