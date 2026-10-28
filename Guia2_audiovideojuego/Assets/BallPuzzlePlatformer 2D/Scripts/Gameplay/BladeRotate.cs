
using UnityEngine;
namespace BallPuzzlePlatformer2D
{

    public class BladeRotate : MonoBehaviour
    {
        public float rotationSpeed = 200f; // Degrees per second

        void Start()
        {
            // [AUDIO PROCEDURAL] zumbido de sierra generado en tiempo real (MechanismHum)
            // La velocidad de giro real viene del script Spin del hijo (z = 300 grados/s).
            Spin spin = GetComponentInChildren<Spin>();
            float spinSpeed = spin != null ? Mathf.Abs(spin.m_SpinDirection.z) : rotationSpeed;
            if (GetComponent<MechanismHum>() == null)
            {
                var hum = gameObject.AddComponent<MechanismHum>();
                hum.constantSpeed = 3f;
                hum.baseFreq = 70f + spinSpeed * 0.15f;     // gira más rápido = más agudo
                hum.maxVolume = 0.06f;                       // muy tenue: solo da presencia
                hum.hearingDistance = 6f;                    // se oye solo si estás cerca
            }

            // Golpe metálico (FM) al tocarla + la música sube de intensidad al acercarse
            if (GetComponent<WorldSoundEmitter>() == null)
            {
                var em = gameObject.AddComponent<WorldSoundEmitter>();
                em.kind = WorldSoundEmitter.Kind.Hazard;
                em.reactToTrigger = false;
                em.dangerRadius = 4f;
            }
        }

        void Update()
        {
            //transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);
        }
        void OnCollisionEnter2D(Collision2D col)
        {
            if (col.gameObject.CompareTag("Player"))
            {
                Player obj = col.gameObject.GetComponent<Player>();
                if (obj != null)
                {
                    obj.Smash();
                }
            }

        }
    }
}

