
using UnityEngine;
namespace BallPuzzlePlatformer2D
{

    public class BladeRotate : MonoBehaviour
    {
        public float rotationSpeed = 200f; // Degrees per second

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

