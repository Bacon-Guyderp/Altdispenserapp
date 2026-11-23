using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using TMPro;

/// <summary>
/// The Categories menu: switch kinds of places on or off (interstates & highways,
/// towns & cities, country roads, middle of nowhere). The choice is shared and
/// lives in GeoGameManager; it combines with the county and photo-type choices.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GeoCategoryMenu : UdonSharpBehaviour
{
    public GeoGameManager game;
    public GameObject panel;
    public GeoCountyMenu countyMenu;     // closed when this opens (they share a spot)
    public TextMeshPro titleText;
    public TextMeshPro[] rowLabels;      // 4 rows
    public Renderer[] rowVisuals;
    public Material onMaterial, offMaterial;

    void Start()
    {
        if (panel != null) panel.SetActive(false);
    }

    public void ToggleOpen()
    {
        if (panel == null || game == null) return;
        string why = game.CountyMenuBlocker();
        if (why != "") { panel.SetActive(false); game.ShowNotice(why); return; }
        bool open = !panel.activeSelf;
        if (open && countyMenu != null) countyMenu.Close();
        panel.SetActive(open);
        game.ShowNotice(open ? "Pick categories on the menu above the map. Press <b>Close</b> when done." : "");
        Refresh();
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
        if (game != null) game.ShowNotice("");
    }

    public void All() { if (game != null) game.AllCategories(); }
    public void Row0() { if (game != null) game.ToggleCategory(0); }
    public void Row1() { if (game != null) game.ToggleCategory(1); }
    public void Row2() { if (game != null) game.ToggleCategory(2); }
    public void Row3() { if (game != null) game.ToggleCategory(3); }

    public void Refresh()
    {
        if (game == null || rowLabels == null || !game.ready) return;
        if (panel != null && panel.activeSelf && !game.CanPickCounty()) Close();
        for (int k = 0; k < rowLabels.Length && k < GeoGameManager.CATEGORIES; k++)
        {
            rowLabels[k].text = game.categoryNames[k] + "  <size=70%>" + game.PlacesInCategory(k).ToString("N0") + "</size>";
            if (rowVisuals != null && k < rowVisuals.Length && rowVisuals[k] != null)
                rowVisuals[k].sharedMaterial = game.CategoryOn(k) ? onMaterial : offMaterial;
        }
        if (titleText != null) titleText.text = "<b>CATEGORIES</b>   <size=75%>green = in play</size>";
    }
}
