using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;
namespace BallPuzzlePlatformer2D
{
    public class Joystick : MonoBehaviour
    {

        [HideInInspector]
        public Vector2 StickOffset;

        public Joystick_Stick LeftStick;
        public Joystick_Stick RightStick;
        public JoystickButton ButtonA;

        public GraphicRaycaster Canvas;
        public static Joystick GeneralJoystick;

        [HideInInspector]
        public List<RaycastResult> RayCastResults;
        [HideInInspector]
        public List<GameObject> RayCastObjects;

        public Text[] JoyButtonTexts;
        public string[] JoyButtonStrings;
        void Awake()
        {
            GeneralJoystick = this;
        }

        void Start()
        {

        }

        void Update()
        {

        }

    }
}