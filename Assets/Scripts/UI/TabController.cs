using UnityEngine;

public class TabController : MonoBehaviour
{
    [Header("Screens")]
    [SerializeField] private GameObject vineyardScreen;
    [SerializeField] private GameObject facilityScreen;
    [SerializeField] private GameObject inventoryScreen;
    [SerializeField] private GameObject statsScreen;
    [SerializeField] private GameObject menuScreen;

    // Button-callable wrappers (public, void, no params)
    public void OnVineyard()  => Show(0);
    public void OnFacility()  => Show(1);
    public void OnInventory() => Show(2);
    public void OnStats()     => Show(3);
    public void OnMenu()      => Show(4);

    private void Show(int idx)
    {
        if (!vineyardScreen || !facilityScreen || !inventoryScreen || !statsScreen || !menuScreen) return;
        vineyardScreen.SetActive(idx == 0);
        facilityScreen.SetActive(idx == 1);
        inventoryScreen.SetActive(idx == 2);
        statsScreen.SetActive(idx == 3);
        menuScreen.SetActive(idx == 4);
    }
}