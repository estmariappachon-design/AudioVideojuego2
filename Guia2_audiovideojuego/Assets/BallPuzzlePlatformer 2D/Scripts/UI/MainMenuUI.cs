using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
namespace BallPuzzlePlatformer2D
{
    public class MainMenuUI : MonoBehaviour
    {
        [SerializeField, Space]
        private DataStorage m_DataStorage;

        [SerializeField, Space]
        private GameplayData m_GameplayData;


        public void BtnExit()
        {
            Application.Quit();
        }

        public void BtnLevel(int num)
        {
            m_GameplayData.LevelNumber = num;
            SceneManager.LoadScene("Scene_1");

        }
    }
}