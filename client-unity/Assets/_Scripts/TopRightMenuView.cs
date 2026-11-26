using UnityEngine;
using UnityEngine.UI;

public class TopRightMenuView : MonoBehaviour
{
    [Header("More Menu System")]
    [SerializeField] private Button moreButton;          // ปุ่ม 3 จุด
    [SerializeField] private GameObject moreMenuPanel;   // แผงแคปซูลสีขาว

    [Header("Inside More Menu")]
    [SerializeField] private Button friendButton;        // ปุ่มเพื่อน (คนสีฟ้าส้ม)
    [SerializeField] private Button settingButton;       // ปุ่มตั้งค่า (ฟันเฟือง)

    [Header("Other Right Buttons")]
    [SerializeField] private Button eventButton;         // ปุ่ม Event (ม้วนกระดาษ)
    [SerializeField] private Button biomeButton;         // ปุ่ม Biome (แผนที่)
    
    private void Awake()
    {
        // ผูกปุ่ม 3 จุดสำหรับเปิด/ปิดแผง More
        if (moreButton != null) moreButton.onClick.AddListener(ToggleMoreMenu);

        // ผูก Event ปุ่มต่างๆ
        if (friendButton != null) friendButton.onClick.AddListener(OnFriendClicked);
        if (settingButton != null) settingButton.onClick.AddListener(OnSettingClicked);
        if (eventButton != null) eventButton.onClick.AddListener(OnEventClicked);
        if (biomeButton != null) biomeButton.onClick.AddListener(OnBiomeClicked);

        // ซ่อนแผงแคปซูลไว้ตอนเริ่มเกม
        if (moreMenuPanel != null)
        {
            moreMenuPanel.SetActive(false);
        }
    }

    public void ToggleMoreMenu()
    {
        if (moreMenuPanel != null)
        {
            moreMenuPanel.SetActive(!moreMenuPanel.activeSelf);
        }
    }

    // 🔧 กดเพื่อน/ตั้งค่าแล้วปิดแผง More ด้วย (เดิมแผงค้างอยู่หลังป๊อปอัพ)
    public void OnFriendClicked() { Debug.Log("Friend Button Clicked"); CloseMoreMenu(); }
    public void OnSettingClicked() { Debug.Log("Setting Button Clicked"); CloseMoreMenu(); }
    private void CloseMoreMenu() { if (moreMenuPanel != null) moreMenuPanel.SetActive(false); }
    public void OnEventClicked() => Debug.Log("Event Button Clicked");
    public void OnBiomeClicked() => Debug.Log("Biome Button Clicked");

    private void OnDestroy()
    {
        if (moreButton != null) moreButton.onClick.RemoveListener(ToggleMoreMenu);
        if (friendButton != null) friendButton.onClick.RemoveListener(OnFriendClicked);
        if (settingButton != null) settingButton.onClick.RemoveListener(OnSettingClicked);
        if (eventButton != null) eventButton.onClick.RemoveListener(OnEventClicked);
        if (biomeButton != null) biomeButton.onClick.RemoveListener(OnBiomeClicked);
    }
}