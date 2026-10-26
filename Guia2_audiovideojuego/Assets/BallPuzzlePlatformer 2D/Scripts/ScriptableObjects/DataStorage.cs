using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;


namespace BallPuzzlePlatformer2D
{
    [CreateAssetMenu(fileName = "DataStorage", menuName = "CustomObjects/DataStorage", order = 1)]
    public class DataStorage : ScriptableObject
    {
        public int LevelUnlocked;


        public bool MuteMusic;



        public void SaveData()
        {
            PlayerPrefs.SetInt("LevelUnlocked", LevelUnlocked);
            if (MuteMusic)
                PlayerPrefs.SetInt("MuteMusic", 1);
            else
                PlayerPrefs.SetInt("MuteMusic", 0);
            PlayerPrefs.Save();
        }

        public void LoadData()
        {
            LevelUnlocked = PlayerPrefs.GetInt("LevelUnlocked", 0);
            MuteMusic = (PlayerPrefs.GetInt("MuteMusic", 0) == 1);

        }

        public void ResetSaveData()
        {
            SaveData();
        }





        public bool CheckInternet()
        {
            if (Application.internetReachability == NetworkReachability.ReachableViaCarrierDataNetwork || Application.internetReachability == NetworkReachability.ReachableViaLocalAreaNetwork)
                return true;



            return false;
        }

    }
}
