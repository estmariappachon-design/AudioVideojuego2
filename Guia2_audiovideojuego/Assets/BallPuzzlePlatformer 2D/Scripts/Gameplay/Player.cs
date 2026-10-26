using UnityEngine;
namespace BallPuzzlePlatformer2D
{

    public class Player : MonoBehaviour
    {
        public GameObject m_SmashPrefab;
        private float m_OriginalSpeed;
        private bool m_IsBoosted = false;
        public float m_MoveSpeed = 50f;
        public float m_JumpForce = 10f;

        private Rigidbody2D rb;
        private bool m_CanJump = true;
        private bool m_IsClimbing = false;
        public float m_ClimbSpeed = 4f;
        private float m_OriginalGravity;
        private Vector3 m_OriginalScale;

        public Transform m_Shadow;

        //mobile joystick
        private Joystick m_Joystick;
        private JoystickButton m_JumpButton;
        public static Player m_Main;

        private void Awake()
        {
            m_Main = this;
        }

        void Start()
        {
            rb = GetComponent<Rigidbody2D>();
            m_OriginalGravity = rb.gravityScale;
            m_OriginalSpeed = m_MoveSpeed;
            m_OriginalScale = transform.localScale;

            //mobile joystick
            if (InputControl.m_Main.m_MobileControl)
            {
                m_Joystick = Joystick.GeneralJoystick;
                m_JumpButton = m_Joystick.ButtonA;
            }
        }

        void Update()
        {

            if ((Input.GetKeyDown(KeyCode.Space) || Input.GetKey(KeyCode.Space) || (InputControl.m_Main.m_MobileControl && m_JumpButton.Pressed)) && m_CanJump && !m_IsClimbing)
            {
                rb.velocity = new Vector2(rb.velocity.x, 0f); // Optional: reset Y velocity
                rb.AddForce(Vector2.up * m_JumpForce, ForceMode2D.Impulse);
                m_CanJump = false;
            }
            // if (InputControl.m_Main.m_MobileControl)
            // {
            //     if (m_JumpButton.Pressed)
            //     {
            //         rb.velocity = new Vector2(rb.velocity.x, 0f); // Optional: reset Y velocity
            //         rb.AddForce(Vector2.up * m_JumpForce, ForceMode2D.Impulse);
            //         m_CanJump = false;
            //     }

            // }

            Vector3 pos = transform.position;
            bool hasHit = false;
            RaycastHit2D[] hits = Physics2D.RaycastAll(transform.position, Vector2.down, 100);
            foreach (RaycastHit2D hit in hits)
            {
                if (hit.collider.gameObject == gameObject)
                    continue;

                hasHit = true;
                pos = hit.point;
            }

            if (hasHit)
            {
                m_Shadow.gameObject.SetActive(true);
                m_Shadow.position = pos;
                m_Shadow.rotation = Quaternion.identity;
            }
            else
            {
                m_Shadow.gameObject.SetActive(false);
            }
        }

        void FixedUpdate()
        {
            float moveX = Input.GetAxisRaw("Horizontal");
            float moveY = Input.GetAxisRaw("Vertical");

            if (m_Joystick != null && m_Joystick.LeftStick != null)
            {
                if (Mathf.Abs(m_Joystick.LeftStick.StickOffset.x) > 0.1f)
                    moveX = m_Joystick.LeftStick.StickOffset.x;
            }

            if (m_IsClimbing)
            {
                rb.velocity = new Vector2(0f, moveY * m_ClimbSpeed);
                rb.gravityScale = 0f;
            }
            else
            {
                rb.AddForce(new Vector2(moveX * m_MoveSpeed, 0));
                //rb.velocity = new Vector2(moveX * m_MoveSpeed, rb.velocity.y);
                rb.gravityScale = m_OriginalGravity;
            }
        }


        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (collision.collider.CompareTag("Ground"))
            {
                m_CanJump = true;
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.CompareTag("Ladder"))
            {
                m_IsClimbing = true;
            }
            else if (collision.CompareTag("FallDetector"))
            {
                Smash();
            }
        }

        private void OnTriggerExit2D(Collider2D collision)
        {
            if (collision.CompareTag("Ladder"))
            {
                m_IsClimbing = false;
            }
        }
        public void ApplySpeedBoost(float boostAmount, float duration)
        {
            if (!m_IsBoosted)
            {
                StartCoroutine(SpeedBoostRoutine(boostAmount, duration));
            }
        }

        private System.Collections.IEnumerator SpeedBoostRoutine(float boostAmount, float duration)
        {
            m_IsBoosted = true;
            m_MoveSpeed += boostAmount;

            yield return new WaitForSeconds(duration);

            m_MoveSpeed = m_OriginalSpeed;
            m_IsBoosted = false;
        }
        public void ApplyJumpBoost(float boostAmount, float duration)
        {
            StartCoroutine(JumpBoostRoutine(boostAmount, duration));
        }

        private System.Collections.IEnumerator JumpBoostRoutine(float boostAmount, float duration)
        {
            m_JumpForce += boostAmount;
            yield return new WaitForSecondsRealtime(duration);
            m_JumpForce -= boostAmount;
        }
        public void ApplySizeChange(float scaleMultiplier, float duration)
        {
            StartCoroutine(SizeChangeRoutine(scaleMultiplier, duration));
        }

        private System.Collections.IEnumerator SizeChangeRoutine(float scaleMultiplier, float duration)
        {
            transform.localScale = m_OriginalScale * scaleMultiplier;
            yield return new WaitForSeconds(duration);
            transform.localScale = m_OriginalScale;
        }
        public void Smash()
        {
            GameObject obj = Instantiate(m_SmashPrefab);
            obj.transform.position = transform.position + new Vector3(0, 0, -.1f);
            obj.transform.rotation = transform.rotation;
            Destroy(obj, 5);

            Destroy(gameObject);
            GameControl.m_Current.HandleLose();
        }
    }
}