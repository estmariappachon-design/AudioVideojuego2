using UnityEngine;
namespace BallPuzzlePlatformer2D
{

    public class FloorButton : MonoBehaviour
    {
        public enum ButtonAction
        {
            SlidePlatform,
            OpenDoor,
            Both
        }

        public ButtonAction action = ButtonAction.SlidePlatform;

        public PlatformSlider m_PlatformToSlide;
        public FinishDoor m_FinishDoor;

        public float m_PressDepth = 0.1f;
        public float m_PressSpeed = 5f;

        private Vector3 m_OriginalPosition;
        private Vector3 m_PressedPosition;
        private bool m_IsPressed = false;

        public GameObject[] m_Sprites;

        void Start()
        {
            m_OriginalPosition = transform.position;
            m_PressedPosition = m_OriginalPosition - new Vector3(0, m_PressDepth, 0);
        }

        void Update()
        {
            Vector3 targetPos = m_IsPressed ? m_PressedPosition : m_OriginalPosition;
            transform.position = Vector3.MoveTowards(transform.position, targetPos, m_PressSpeed * Time.deltaTime);
            if (m_IsPressed)
            {
                m_Sprites[0].gameObject.SetActive(false);
                m_Sprites[1].gameObject.SetActive(true);
            }
            else
            {
                m_Sprites[0].gameObject.SetActive(true);
                m_Sprites[1].gameObject.SetActive(false);
            }
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Player") && !m_IsPressed)
            {
                m_IsPressed = true;

                switch (action)
                {
                    case ButtonAction.SlidePlatform:
                        m_PlatformToSlide?.StartSlideLoop();
                        break;
                    case ButtonAction.OpenDoor:
                        m_FinishDoor?.ToggleDoor();
                        break;
                    case ButtonAction.Both:
                        m_PlatformToSlide?.StartSlideLoop();
                        m_FinishDoor?.ToggleDoor();
                        break;
                }
            }
        }
        private void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Player"))
            {
                m_IsPressed = false;

                if (action == ButtonAction.SlidePlatform || action == ButtonAction.Both)
                {
                    m_PlatformToSlide?.StopSlide();
                }
            }
        }



    }
}