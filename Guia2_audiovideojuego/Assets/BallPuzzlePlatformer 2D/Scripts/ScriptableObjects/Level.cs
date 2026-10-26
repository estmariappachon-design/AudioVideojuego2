using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BallPuzzlePlatformer2D
{
    [CreateAssetMenu(fileName = "Level", menuName = "CustomObjects/Level", order = 1)]
    public class Level : ScriptableObject
    {
        [Header("Data")]
        public GameObject m_LevelPrefab;

    }
}
