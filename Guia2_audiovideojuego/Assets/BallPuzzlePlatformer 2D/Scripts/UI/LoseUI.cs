using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
namespace BallPuzzlePlatformer2D
{
    public class LoseUI : MonoBehaviour
    {

        public void BtnRestart()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        public void BtnExit()
        {

            SceneManager.LoadScene("MainMenu");
        }

    }

}