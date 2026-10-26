using UnityEngine;
namespace BallPuzzlePlatformer2D
{

    public class FinishDoor : MonoBehaviour
    {
        public GameObject m_OpenSprite;
        public GameObject m_ClosedSprite;
        public Transform m_DoorCenterPoint;
        public float m_MoveSpeed = 3f;
        public float m_ShrinkSpeed = 2f;
        public float m_FadeSpeed = 2f;

        private bool m_IsOpen = false;
        private bool m_IsBallEntering = false;
        private Transform m_Player;
        private SpriteRenderer[] m_AllRenderers;
        private Transform[] m_AllTransforms;
        private TrailRenderer[] m_AllTrails;

        void Start()
        {
            m_IsOpen = false;
            m_OpenSprite.SetActive(false);
            m_ClosedSprite.SetActive(true);
        }
        void Update()
        {
            if (m_IsBallEntering && m_Player != null)
            {
                m_Player.position = Vector3.MoveTowards(m_Player.position, m_DoorCenterPoint.position, m_MoveSpeed * Time.deltaTime);

                foreach (Transform t in m_AllTransforms)
                {
                    t.localScale = Vector3.Lerp(t.localScale, Vector3.zero, Time.deltaTime * m_ShrinkSpeed);
                }

                foreach (SpriteRenderer sr in m_AllRenderers)
                {
                    if (sr != null)
                    {
                        Color color = sr.color;
                        color.a = Mathf.MoveTowards(color.a, 0f, Time.deltaTime * m_FadeSpeed);
                        sr.color = color;
                    }
                }
            }
        }

        public void OpenDoor()
        {
            m_IsOpen = true;
            m_OpenSprite.SetActive(true);
            m_ClosedSprite.SetActive(false);
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (m_IsOpen && other.CompareTag("Player"))
            {
                CameraFollow.m_Main.StopFollowing();
                m_Player = other.transform;
                m_IsBallEntering = true;

                var m_PlayerScript = m_Player.GetComponent<Player>();
                if (m_PlayerScript != null)
                    m_PlayerScript.enabled = false;

                m_AllRenderers = m_Player.GetComponentsInChildren<SpriteRenderer>();

                m_AllTransforms = m_Player.GetComponentsInChildren<Transform>();

                m_AllTrails = m_Player.GetComponentsInChildren<TrailRenderer>();
                foreach (var trail in m_AllTrails)
                {
                    trail.emitting = false;
                    trail.Clear();
                }

                Invoke(nameof(WinLevel), 0.3f);
            }
        }

        void WinLevel()
        {
            GameControl.m_Current.HandleWin();
        }
        public void ToggleDoor()
        {
            if (m_IsOpen)
                CloseDoor();
            else
                OpenDoor();
        }

        public void CloseDoor()
        {
            Debug.Log("CloseDoor() called");
            m_IsOpen = false;
            m_OpenSprite.SetActive(false);
            m_ClosedSprite.SetActive(true);
        }

    }
}