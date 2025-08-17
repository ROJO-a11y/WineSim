using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;

public static class WineSimVineyardWeatherRowBuilder
{
    [MenuItem("Tools/WineSim UI/Add Weather Row to Vineyard Screen")]
    public static void AddWeatherRow()
    {
        var vs = FindVineyardScreen();
        if (vs == null)
        {
            EditorUtility.DisplayDialog("Weather Row", "Select your VineyardScreen GameObject in the Hierarchy and rerun.", "OK");
            return;
        }

        // Try find existing
        var existing = vs.transform.Find("WeatherRow");
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            Debug.Log("WeatherRow already exists. Selected it for you.");
            return;
        }

        // Create row
        var rowGO = new GameObject("WeatherRow", typeof(RectTransform), typeof(Image), typeof(LayoutElement), typeof(HorizontalLayoutGroup), typeof(WeatherRowUI));
        var rowRT = rowGO.GetComponent<RectTransform>();
        rowRT.SetParent(vs.transform, false);
        rowRT.anchorMin = new Vector2(0, 1);
        rowRT.anchorMax = new Vector2(1, 1);
        rowRT.pivot     = new Vector2(0.5f, 1f);
        rowRT.offsetMin = new Vector2(0, -100); // height 100
        rowRT.offsetMax = new Vector2(0, 0);
        rowRT.anchoredPosition = Vector2.zero;

        var img = rowGO.GetComponent<Image>();
        img.color = new Color(0.95f, 0.97f, 1f, 1f);

        var le = rowGO.GetComponent<LayoutElement>();
        le.preferredHeight = 100;

        var hlg = rowGO.GetComponent<HorizontalLayoutGroup>();
        hlg.padding = new RectOffset(16, 16, 12, 12);
        hlg.spacing = 18;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = false;

        var ui = rowGO.GetComponent<WeatherRowUI>();
        ui.background = img;

        // Helper to spawn a labeled text
        TMP_Text MakeText(string name, Transform parent, float minWidth = 0)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.text = name + ": –";
            t.enableWordWrapping = false;
            var le2 = go.GetComponent<LayoutElement>();
            le2.minHeight = 40;
            if (minWidth > 0) le2.minWidth = minWidth;
            return t;
        }

        ui.dateText   = MakeText("Date",   rowGO.transform, 160);
        ui.seasonText = MakeText("Season", rowGO.transform, 100);
        ui.tempText   = MakeText("Temp",   rowGO.transform, 120);
        ui.rainText   = MakeText("Precip", rowGO.transform, 140);
        ui.windText   = MakeText("Wind",   rowGO.transform, 120);
        ui.humText    = MakeText("Hum",    rowGO.transform, 120);

        // Push the tile grid down by 100 px if we can find it
        var grid = vs.transform.Find("VineyardGrid") ?? vs.transform.Find("Grid") ?? vs.transform.Find("Tiles");
        if (grid != null)
        {
            var grt = grid.GetComponent<RectTransform>();
            if (grt != null)
            {
                // Stretch full screen but leave 100px margin at top (the row height)
                grt.anchorMin = new Vector2(0, 0);
                grt.anchorMax = new Vector2(1, 1);
                grt.offsetMin = new Vector2(0, 0);
                grt.offsetMax = new Vector2(0, -100);
            }
        }

        Selection.activeGameObject = rowGO;
        Debug.Log("WeatherRow created & wired.");
    }

    private static GameObject FindVineyardScreen()
    {
        // Prefer current selection
        if (Selection.activeGameObject != null && Selection.activeGameObject.name.Contains("VineyardScreen"))
            return Selection.activeGameObject;

        // Search common path: UI/Canvas/Screens/VineyardScreen
        var canvas = GameObject.Find("UI/Canvas/Screens/VineyardScreen");
        if (canvas) return canvas;

        // Fallback: any GO named VineyardScreen
        var all = GameObject.FindObjectsOfType<RectTransform>(true);
        foreach (var rt in all)
            if (rt && rt.name == "VineyardScreen") return rt.gameObject;

        return null;
    }
}