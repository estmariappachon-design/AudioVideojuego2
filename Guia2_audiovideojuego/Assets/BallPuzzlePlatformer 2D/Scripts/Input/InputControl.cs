using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace BallPuzzlePlatformer2D
{
    public class InputControl : MonoBehaviour
    {

        //--inputs
        [HideInInspector]
        public Vector3 m_Movement;
        [HideInInspector]



        public static InputControl m_Main;

        public bool m_MobileControl = false;

        void Awake()
        {
            m_Main = this;
        }
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            m_Movement = Vector3.zero;



            if (!m_MobileControl)
            {
                m_Movement.x = Input.GetAxis("Horizontal");
                m_Movement.y = Input.GetAxis("Vertical");



            }
            else
            {
                if (Joystick.GeneralJoystick != null)
                {
                    m_Movement.x = Joystick.GeneralJoystick.LeftStick.StickDirection.x;
                    m_Movement.y = Joystick.GeneralJoystick.LeftStick.StickDirection.y;



                }
            }


            m_Movement = Vector3.ClampMagnitude(m_Movement, 1.0f);
        }
    }
}