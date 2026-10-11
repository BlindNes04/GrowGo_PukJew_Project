using UnityEngine;
using UnityEditor;

public class ClearSaveData
{
    [MenuItem("Tools/Clear All Save Data")]
    public static void ClearAllData()
    {
        PlayerPrefs.DeleteAll();
        PlayerPrefs.Save();
        Debug.Log("ล้างข้อมูล PlayerPrefs ในเครื่องเรียบร้อยแล้ว!");
    }
}