using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
namespace BallPuzzlePlatformer2D
{
    public class Joystick_Stick : MonoBehaviour
    {
        public Image Back;
        public Image Stick;

        [HideInInspector]
        public bool Hold;
        [HideInInspector]
        public Vector3 HitPosition;
        [HideInInspector]
        public Vector3 StickDirection;

        [HideInInspector]
        public Vector3 m_OriginPosition;
        [HideInInspector]
        public bool m_PrevTouch = false;

        public RectTransform m_MainRect;

        public Vector2 m_InitPostition;
        public bool m_IsLeft = true;

        // 👇 NEW: Public property to read stick movement
        public Vector2 StickOffset
        {
            get { return new Vector2(StickDirection.x, StickDirection.y); }
        }

        void Start()
        {
            Hold = false;
            m_InitPostition = Back.rectTransform.anchoredPosition;
        }

        void Update()
        {
            Hold = false;
            Vector3[] PointerPos = new Vector3[2];

            if (Application.platform == RuntimePlatform.Android)
            {
                PointerPos = new Vector3[Input.touchCount];
                for (int i = 0; i < Input.touchCount; i++)
                {
                    PointerPos[i] = Input.touches[i].position;
                }
            }
            else
            {
                if (Input.GetMouseButton(0))
                {
                    PointerPos = new Vector3[1];
                    PointerPos[0] = Input.mousePosition;
                }
                else
                {
                    PointerPos = new Vector3[0];
                }
            }

            HitPosition = Vector3.zero;
            bool found = false;
            Vector3 foundPos = Vector3.zero;
            for (int i = 0; i < PointerPos.Length; i++)
            {
                Vector3 pos = PointerPos[i];
                pos.z = 0;
                pos.x = pos.x / Screen.width;
                pos.y = pos.y / Screen.height;

                Vector2 p2 = Vector2.zero;
                p2.x = pos.x * m_MainRect.sizeDelta.x;
                p2.y = pos.y * m_MainRect.sizeDelta.y;

                float screenPos = PointerPos[i].x / (float)Screen.width;
                if (Vector2.Distance(p2, m_InitPostition) < 300f)
                {
                    foundPos = p2;
                    found = true;
                    if (!m_PrevTouch)
                    {
                        m_PrevTouch = true;
                        m_OriginPosition = m_InitPostition;
                    }
                    break;
                }
            }

            if (!found)
            {
                m_PrevTouch = false;
                StickDirection = Vector3.zero;

                Back.rectTransform.anchoredPosition = m_InitPostition;
                Stick.rectTransform.anchoredPosition = m_InitPostition;
            }
            else
            {
                Stick.enabled = true;
                Stick.rectTransform.anchoredPosition = foundPos;

                float MaxDistance = 200;

                StickDirection = foundPos - m_OriginPosition;
                StickDirection = StickDirection / MaxDistance;
                StickDirection = Vector3.ClampMagnitude(StickDirection, 1);

                Vector3 dir = foundPos - m_OriginPosition;
                if (dir.magnitude > MaxDistance)
                {
                    m_OriginPosition = Vector3.Lerp(m_OriginPosition, foundPos, 5 * Time.deltaTime);
                }
            }
        }
    }
}
