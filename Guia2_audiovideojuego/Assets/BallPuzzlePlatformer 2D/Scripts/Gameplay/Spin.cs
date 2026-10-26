using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace BallPuzzlePlatformer2D
{

    public class Spin : MonoBehaviour
    {
        public Vector3 m_SpinDirection;
        // Start is called before the first frame update
        void Start()
        {

        }

        // Update is called once per frame
        void Update()
        {
            transform.Rotate(Time.deltaTime * m_SpinDirection, Space.Self);
        }
    }
}
