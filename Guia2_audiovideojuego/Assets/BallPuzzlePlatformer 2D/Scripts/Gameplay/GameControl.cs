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

            // [AUDIO PROCEDURAL] Los 10 niveles son prefabs dentro de esta misma escena,
            // así que aquí le decimos al generador musical qué nivel es (1..10).
            // Cada nivel = semilla distinta = tempo, escala, tónica y patrones distintos.
            ProceduralMusic.I?.Configure(levelNum + 1);
        }

        public void HandleWin()
        {
            if (m_GameState != 0) return;
            AudioManager.I?.Victory();      // [AUDIO] fanfarria sintetizada + fundido de la música
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
            AudioManager.I?.Defeat();       // [AUDIO] melodía descendente + "tape stop" de la música
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
