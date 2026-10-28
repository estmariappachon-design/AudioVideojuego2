using UnityEngine;

/// <summary>
/// EMISOR DE SONIDO PARA OBJETOS DEL MUNDO.
/// Añádalo a enemigos, obstáculos, objetos recogibles, resortes, puertas e interruptores.
/// Suena automáticamente cuando el jugador choca o entra en su trigger, y también expone
/// métodos públicos para llamarlos desde los scripts de la plantilla o desde UnityEvents.
/// </summary>
public class WorldSoundEmitter : MonoBehaviour
{
    public enum Kind { Enemy, Hazard, Pickup, Bounce, Door, Switch, Goal }

    public Kind kind = Kind.Enemy;
    public string playerTag = "Player";
    public bool reactToCollision = true;
    public bool reactToTrigger = true;

    [Header("Enemigos y peligros (sierras): suben la intensidad de la música al acercarse")]
    public bool affectsMusicIntensity = true;
    public float dangerRadius = 5f;

    bool state;                    // abierto/cerrado, encendido/apagado
    Transform player;

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p) player = p.transform;
    }

    void Update()
    {
        // Control dinámico de la música según la proximidad del peligro
        if ((kind == Kind.Enemy || kind == Kind.Hazard) && affectsMusicIntensity && player != null && ProceduralMusic.I != null)
        {
            float d = Vector2.Distance(player.position, transform.position);
            float danger = 1f - Mathf.Clamp01(d / dangerRadius);
            if (danger > 0f) ProceduralMusic.I.PushIntensity(0.5f + 0.5f * danger);
        }
    }

    void OnCollisionEnter2D(Collision2D c)
    {
        if (reactToCollision && c.collider.CompareTag(playerTag)) Fire();
    }

    void OnTriggerEnter2D(Collider2D c)
    {
        if (reactToTrigger && c.CompareTag(playerTag)) Fire();
    }

    public void Fire()
    {
        var am = AudioManager.I; if (am == null) return;
        Vector3 p = transform.position;
        switch (kind)
        {
            case Kind.Enemy:  am.EnemyHit(p); break;
            case Kind.Hazard: am.Hazard(p); break;
            case Kind.Pickup: am.Pickup(p); break;
            case Kind.Bounce: am.Bounce(p); break;
            case Kind.Door:   ToggleDoor(); break;
            case Kind.Switch: ToggleSwitch(); break;
            case Kind.Goal:   am.Victory(); break;
        }
    }

    // Llamables desde UnityEvents o desde los scripts de puertas/interruptores de la plantilla
    public void ToggleDoor()     { state = !state; AudioManager.I?.Door(state, transform.position); }
    public void OpenDoor()       { state = true;  AudioManager.I?.Door(true, transform.position); }
    public void CloseDoor()      { state = false; AudioManager.I?.Door(false, transform.position); }
    public void ToggleSwitch()   { state = !state; AudioManager.I?.Switch(state, transform.position); }
    public void EnemyDefeated()  { AudioManager.I?.EnemyDie(transform.position); }
}
