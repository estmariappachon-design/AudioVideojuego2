using UnityEngine;
namespace BallPuzzlePlatformer2D
{

    public class JumpPad : MonoBehaviour
    {
        public float m_BounceForce = 20f;

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.collider.CompareTag("Player"))
            {
                Rigidbody2D rb = collision.collider.GetComponent<Rigidbody2D>();
                if (rb != null)
                {
                    // Reset vertical speed before bounce
                    rb.velocity = new Vector2(rb.velocity.x, 0f);
                    rb.AddForce(Vector2.up * m_BounceForce, ForceMode2D.Impulse);
                }
            }
        }
    }
}
