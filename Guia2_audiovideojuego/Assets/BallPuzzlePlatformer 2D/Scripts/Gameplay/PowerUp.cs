using UnityEngine;
namespace BallPuzzlePlatformer2D
{

    public enum PowerUpType
    {
        Speed,
        Jump,
        Shrink,
        Grow

    }

    public class PowerUp : MonoBehaviour
    {
        public PowerUpType powerUpType;
        public float amount = 3f;
        public float duration = 5f;

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                Player player = other.GetComponent<Player>();
                if (player != null)
                {
                    switch (powerUpType)
                    {
                        case PowerUpType.Speed:
                            player.ApplySpeedBoost(amount, duration);
                            break;

                        case PowerUpType.Jump:
                            player.ApplyJumpBoost(amount, duration);
                            break;
                        case PowerUpType.Shrink:
                            player.ApplySizeChange(0.5f, duration);
                            break;
                        case PowerUpType.Grow:
                            player.ApplySizeChange(1.5f, duration);
                            break;


                    }

                    Destroy(gameObject);
                }
            }
        }
    }
}