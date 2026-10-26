using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace BallPuzzlePlatformer2D
{
    public class WinUI : MonoBehaviour
    {
        [SerializeField, Space]
        private GameplayData m_GameplayData;
        [SerializeField, Space]
        private Contents m_Contents;
        void Start()
        {

        }

        void Update()
        {
        }

        public void Continue()
        {
            m_GameplayData.LevelNumber++;
            if (m_GameplayData.LevelNumber > m_Contents.m_Levels.Length - 1)
            {
                m_GameplayData.LevelNumber = 0;
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
    }

}