using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace BallPuzzlePlatformer2D
{

public class Background : MonoBehaviour
{
    public GameObject m_StickTarget;


    // Update is called once per frame
    void FixedUpdate()
    {
        Vector3 pos = m_StickTarget.transform.position + new Vector3(-.05f * m_StickTarget.transform.position.x, 0, 0);
        pos.z = 300;
        transform.position = pos;
    }
}
}