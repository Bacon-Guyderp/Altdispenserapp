using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using TMPro;

/// <summary>
/// The county sub-menu: pages of counties in alphabetical order, 20 per page.
/// Press a county to switch it on (green) or off (dark). The choice itself lives
/// in GeoGameManager and is shared by everyone; which page you're on is your own.
/// </summary>
[UdonBehaviourSyncMode(BehaviourSyncMode.None)]
public class GeoCountyMenu : UdonSharpBehaviour
{
    public GeoGameManager game;
    public GameObject panel;            // shown / hidden by the Counties button
    public TextMeshPro titleText;
    public TextMeshPro[] rowLabels;     // 20 rows
    public Renderer[] rowVisuals;
    public GameObject[] rowObjects;
    public Material onMaterial, offMaterial;

    private int _page;
    private const int PerPage = 20;

    void Start()
    {
        if (panel != null) panel.SetActive(false);
        Refresh();
    }

    private int Count() { return game == null || game.countyNames == null ? 0 : game.countyNames.Length; }
    private int Pages() { return Mathf.Max(1, (Count() + PerPage - 1) / PerPage); }

    public void ToggleOpen()
    {
        if (panel == null) return;
        panel.SetActive(!panel.activeSelf && game != null && game.CanPickCounty());
        Refresh();
    }

    public void Close() { if (panel != null) panel.SetActive(false); }

    public void NextPage() { _page = (_page + 1) % Pages(); Refresh(); }
    public void PrevPage() { _page = (_page + Pages() - 1) % Pages(); Refresh(); }
    public void All() { if (game != null) game.AllCounties(); }
    public void Clear() { if (game != null) game.ClearCounties(); }

    private void Toggle(int row)
    {
        int c = _page * PerPage + row;
        if (game != null && c < Count()) game.ToggleCounty(c);
    }

    public void Row0() { Toggle(0); }
    public void Row1() { Toggle(1); }
    public void Row2() { Toggle(2); }
    public void Row3() { Toggle(3); }
    public void Row4() { Toggle(4); }
    public void Row5() { Toggle(5); }
    public void Row6() { Toggle(6); }
    public void Row7() { Toggle(7); }
    public void Row8() { Toggle(8); }
    public void Row9() { Toggle(9); }
    public void Row10() { Toggle(10); }
    public void Row11() { Toggle(11); }
    public void Row12() { Toggle(12); }
    public void Row13() { Toggle(13); }
    public void Row14() { Toggle(14); }
    public void Row15() { Toggle(15); }
    public void Row16() { Toggle(16); }
    public void Row17() { Toggle(17); }
    public void Row18() { Toggle(18); }
    public void Row19() { Toggle(19); }

    /// <summary>Redraw the current page. The game calls this whenever the choice changes.</summary>
    public void Refresh()
    {
        if (game == null || rowLabels == null) return;
        if (panel != null && panel.activeSelf && !game.CanPickCounty()) panel.SetActive(false);  // a game started
        int n = Count();
        if (_page >= Pages()) _page = 0;
        int first = _page * PerPage;
        for (int i = 0; i < rowLabels.Length; i++)
        {
            int c = first + i;
            bool exists = c < n;
            if (rowObjects != null && i < rowObjects.Length && rowObjects[i] != null) rowObjects[i].SetActive(exists);
            if (!exists) continue;
            bool on = game.CountyOn(c);
            rowLabels[i].text = game.countyNames[c] + "  <size=70%>" + game.PlacesIn(c) + "</size>";
            if (rowVisuals != null && i < rowVisuals.Length && rowVisuals[i] != null)
                rowVisuals[i].sharedMaterial = on ? onMaterial : offMaterial;
        }
        if (titleText != null && n > 0)
        {
            int last = Mathf.Min(n, first + PerPage) - 1;
            titleText.text = "<b>COUNTIES</b>  " + game.countyNames[first] + " - " + game.countyNames[last] +
                             "   <size=75%>page " + (_page + 1) + "/" + Pages() + "   " + game.SelectedCount() + " of " + n + " in play</size>";
        }
    }
}
