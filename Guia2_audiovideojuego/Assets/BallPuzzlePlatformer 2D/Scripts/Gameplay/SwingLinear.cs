using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace BallPuzzlePlatformer2D
{

    public class SwingLinear : MonoBehaviour
    {
        public float m_Speed = 1;
        public float m_Radius = 1;
        public Vector3 m_Axis = Vector3.up;

        [HideInInspector]
        public Vector3 m_InitPosition;
        // Start is called before the first frame update
        void Start()
        {
            m_InitPosition = transform.localPosition;
        }

        // Update is called once per frame
        void Update()
        {
            transform.localPosition = m_InitPosition + m_Radius * Mathf.Sin(m_Speed * Time.time) * m_Axis;
        }
    }
}