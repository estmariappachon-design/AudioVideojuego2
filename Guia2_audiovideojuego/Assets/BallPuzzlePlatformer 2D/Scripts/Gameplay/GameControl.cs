using System.Collections;
using UnityEngine;

namespace BallPuzzlePlatformer2D
{

    public class GameControl : MonoBehaviour
    {

        public static GameControl m_Current;

        [SerializeField] private GameplayData m_GameplayData;
        [SerializeField] private Contents m_Contents;

        public Camera m_MainCamera;

        private int m_GameState = 0;

        void Awake()
        {
            m_Current = this;
        }

        void Start()
        {

            int levelNum = m_GameplayData.LevelNumber;
            Instantiate(m_Contents.m_Levels[levelNum].m_LevelPrefab);
        }

        public void HandleWin()
        {
            if (m_GameState != 0) return;
            StartCoroutine(Co_HandleWin());
        }

        private IEnumerator Co_HandleWin()
        {
            m_GameState = 1;
            Cursor.visible = true;
            yield return new WaitForSeconds(1);
            UISystem.RemoveUI("game-ui");
            yield return new WaitForSeconds(2);
            UISystem.ShowUI("win-ui");
        }
        public void HandleLose()
        {
            if (m_GameState != 0) return;
            StartCoroutine(Co_HandleLose());
        }

        private IEnumerator Co_HandleLose()
        {
            m_GameState = 2;
            Cursor.visible = true;
            yield return new WaitForSeconds(1);
            UISystem.RemoveUI("game-ui");
            yield return new WaitForSeconds(2);
            UISystem.ShowUI("lose-ui");
        }
    }
}
