using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
namespace BallPuzzlePlatformer2D
{
    public class InGameUI : MonoBehaviour
    {
        // [SerializeField, Space]
        // private Player m_Player;
        [SerializeField, Space]
        private GameplayData m_GameplayData;
        [SerializeField, Space]
        private Contents m_Contents;
        [SerializeField, Space]
        private DataStorage m_DataStorage;



        [SerializeField]
        private Text Text_LevelNum;



        [SerializeField, Space]
        public RectTransform MainCanvas;

        public static InGameUI Current;

        public Image[] m_SelectionFrames;


        [HideInInspector]
        public int m_SelectedItem = -1;


        void Awake()
        {
            Current = this;

        }

        void Start()
        {
            //Cursor.visible = false;
        }

        void Update()
        {


            Text_LevelNum.text = "Level " + (m_GameplayData.LevelNumber + 1).ToString();
            // Vector3 v;
            // v = Input.mousePosition;
            // v.x = v.x / Screen.width;
            // v.y = v.y / Screen.height;

            // v.x = MainCanvas.sizeDelta.x * v.x - 0.5f * MainCanvas.sizeDelta.x;
            // v.y = MainCanvas.sizeDelta.y * v.y - 0.5f * MainCanvas.sizeDelta.y;

            // m_ReticleBase.rectTransform.localPosition = v;

            // Text_TargetCounter.text = "Targets : " + GameControl.Current.m_TargetDestroyedCount + " / " + GameControl.Current.m_MaxTargetCount;
            // Text_AmmoCounter.text = GameControl.Current.m_AxeCount.ToString();
            // Text_LevelNum.text = "Level " + (m_GameplayData.LevelNumber + 1).ToString();
        }

        public void BtnExit()
        {
            SceneManager.LoadScene("MainMenu");
        }


    }
}
