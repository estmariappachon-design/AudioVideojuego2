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
            AudioManager.I?.Restart();      // [AUDIO] barrido "rebobinar"
            SceneManager.LoadScene(SceneManager.GetActiveScene().name);
        }
        public void BtnExit()
        {
            AudioManager.I?.UIClick();

            SceneManager.LoadScene("MainMenu");
        }

    }

}