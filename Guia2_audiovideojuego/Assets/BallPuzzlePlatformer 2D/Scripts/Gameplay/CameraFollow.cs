using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace BallPuzzlePlatformer2D
{

    public class CameraFollow : MonoBehaviour
    {
        private Vector3 m_Offset = new Vector3(0, 0, -10);
        public float m_SmoothTime;
        private Vector3 m_Velocity = Vector3.zero;

        public Vector3 m_RoomSize = new Vector3(1400, 700, 1);
        private bool m_CanFollow = true;

        public static CameraFollow m_Main;

        private void Awake()
        {
            m_Main = this;
        }
        private void Start()
        {
            if (Player.m_Main != null)
                transform.position = Player.m_Main.transform.position + m_Offset;
        }
        void LateUpdate()
        {
            if (!m_CanFollow || Player.m_Main == null)
                return;

            Vector3 targetPosition = Player.m_Main.transform.position + m_Offset;
            transform.position = Vector3.SmoothDamp(transform.position, targetPosition, ref m_Velocity, m_SmoothTime);

            float ySize = GetComponent<Camera>().orthographicSize;
            float ratio = (float)Screen.width / (float)Screen.height;
            float xSize = ratio * ySize;

            Vector2 min = new Vector2(-m_RoomSize.x / 2f, -m_RoomSize.y / 2f);
            Vector2 max = new Vector2(m_RoomSize.x / 2f, m_RoomSize.y / 2f);

            Vector3 pos = transform.position;
            pos.x = Mathf.Clamp(pos.x, min.x + xSize, max.x - xSize);
            pos.y = Mathf.Clamp(pos.y, min.y + ySize, max.y - ySize);
            transform.position = pos;
        }



        private void OnDrawGizmos()
        {
            Gizmos.color = Color.green;
            Gizmos.DrawWireCube(Vector3.zero, m_RoomSize);
        }
        public void StopFollowing()
        {
            m_CanFollow = false;
            //m_Target = null; // This stops camera updates entirely
        }


    }
}