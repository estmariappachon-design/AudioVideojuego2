using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BallPuzzlePlatformer2D
{

    [CreateAssetMenu(fileName = "Contents", menuName = "CustomObjects/Contents", order = 1)]
    public class Contents : ScriptableObject
    {
        public Level[] m_Levels;

    }
}
