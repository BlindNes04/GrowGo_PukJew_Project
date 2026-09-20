using UnityEngine;
using UnityEngine.UI;

public class BottomNavigationBar : MonoBehaviour
{
    [Header("Navigation Button Slots")]
    [SerializeField] private Button inventoryButton;
    [SerializeField] private Button plantDexButton;
    [SerializeField] private Button plantStoreButton;

    [Header("Floating Action Buttons")]
    [SerializeField] private Button miniGamesBtn;

    [Header("Drawers & Popups")]
    [SerializeField] private InventoryDrawerController inventoryDrawer;

    private void Awake()
    {
        if (inventoryButton != null) inventoryButton.onClick.AddListener(OnInventoryClicked);
        if (plantDexButton != null) plantDexButton.onClick.AddListener(OnPlantDexClicked);
        if (plantStoreButton != null) plantStoreButton.onClick.AddListener(OnPlantStoreClicked);
        if (miniGamesBtn != null) miniGamesBtn.onClick.AddListener(OnMiniGamesClicked);
    }

    public void OnInventoryClicked()
    {
        if (inventoryDrawer != null)
        {
            inventoryDrawer.ToggleDrawer();
            Debug.Log("Inventory Button Clicked");
        }
    }

    public void OnPlantDexClicked()
    {
        Debug.Log("Plant Dex Button Clicked");
    }

    public void OnPlantStoreClicked()
    {
        Debug.Log("Gacha Shop Button Clicked");
    }
    
    public void OnMiniGamesClicked()
    {
        Debug.Log("Minigames Button Clicked");
    }

    private void OnDestroy()
    {
        //if (homeButton != null) homeButton.onClick.RemoveListener(OnHomeClicked);
        //if (addFriendButton != null) addFriendButton.onClick.RemoveListener(OnAddFriendClicked);
        if (inventoryButton != null) inventoryButton.onClick.RemoveListener(OnInventoryClicked);
        if (plantDexButton != null) plantDexButton.onClick.RemoveListener(OnPlantDexClicked);
        if (plantStoreButton != null) plantStoreButton.onClick.RemoveListener(OnPlantStoreClicked);
        if (miniGamesBtn != null) miniGamesBtn.onClick.RemoveListener(OnMiniGamesClicked);
    }
}