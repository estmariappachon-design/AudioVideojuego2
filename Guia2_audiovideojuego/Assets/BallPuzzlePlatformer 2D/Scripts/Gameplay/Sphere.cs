using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace BallPuzzlePlatformer2D
{

    public class Sphere : MonoBehaviour
    {
        public GameObject m_ExplosionPrefab;

        // Start is called before the first frame update
        void Start()
        {
            Rigidbody2D rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.AddTorque(Random.Range(-250f, 250f), ForceMode2D.Impulse);
                rb.AddForce(Vector2.down * 50f);
            }
        }

        // Update is called once per frame
        void Update()
        {

        }
        public void Explosion()
        {
            GameObject obj = Instantiate(m_ExplosionPrefab);
            obj.transform.position = transform.position + new Vector3(0, 0, -.1f);
            obj.transform.rotation = transform.rotation;
            Destroy(obj, 5);

            Destroy(gameObject);
            GameControl.m_Current.HandleLose();
        }
        void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.gameObject.CompareTag("Ground"))
            {
                Explosion();
                GameControl.m_Current.HandleLose();
            }
        }
    }
}
