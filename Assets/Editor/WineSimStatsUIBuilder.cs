#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class WineSimStatsUIBuilder
{
    [MenuItem("Tools/WineSim/Build Stats UI")]
    public static void Build()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        // --- Canvas + Screens ---
        var canvas = Object.FindFirstObjectByType<Canvas>();
        if (!canvas)
        {
            var go = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            Undo.RegisterCreatedObjectUndo(go, "Create Canvas");
        }

        Transform screens = canvas.transform.Find("Screens");
        if (!screens)
        {
            var go = new GameObject("Screens", typeof(RectTransform));
            go.transform.SetParent(canvas.transform, false);
            Stretch(go.GetComponent<RectTransform>());
            screens = go.transform;
            Undo.RegisterCreatedObjectUndo(go, "Create Screens");
        }

        // --- StatsScreen root ---
        var screenTr = screens.Find("StatsScreen") as RectTransform;
        if (!screenTr)
        {
            var go = new GameObject("StatsScreen", typeof(RectTransform));
            go.transform.SetParent(screens, false);
            screenTr = go.GetComponent<RectTransform>();
            Undo.RegisterCreatedObjectUndo(go, "Create StatsScreen");
        }
        Stretch(screenTr);

        var vlg = screenTr.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(screenTr.gameObject);
        vlg.padding = new RectOffset(16,16,16,16);
        vlg.spacing = 16; vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        // --- TopRow (cash) ---
        var topRow = Ensure(screenTr, "TopRow");
        var leTop = topRow.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(topRow.gameObject);
        leTop.preferredHeight = 100;
        var cash = EnsureTMP(topRow, "CashText", "Cash: $0", 34, FontStyles.Bold);

        // --- Revenue group ---
        var rev = Ensure(screenTr, "Revenue");
        var revTitle = EnsureTMP(rev, "Title", "Revenue (last year)", 30, FontStyles.Bold);
        var revAxis = EnsureTMP(rev, "AxisHint", "months", 20, FontStyles.Italic);
        var revChart = Ensure(rev, "ChartArea");
        GiveChartAreaLook(revChart);

        // --- Bottles group ---
        var bot = Ensure(screenTr, "Bottles");
        var botTitle = EnsureTMP(bot, "Title", "Inventory bottles (last 180d)", 30, FontStyles.Bold);
        var botAxis = EnsureTMP(bot, "AxisHint", "weeks", 20, FontStyles.Italic);
        var botChart = Ensure(bot, "ChartArea");
        GiveChartAreaLook(botChart);

        // --- Add StatsPanel + wire ---
        var panel = screenTr.GetComponent<StatsPanel>() ?? Undo.AddComponent<StatsPanel>(screenTr.gameObject);
        var so = new SerializedObject(panel);
        so.FindProperty("cashText").objectReferenceValue         = cash;
        so.FindProperty("revenueChartArea").objectReferenceValue = revChart;
        so.FindProperty("revenueTitle").objectReferenceValue     = revTitle;
        so.FindProperty("revenueAxisHint").objectReferenceValue  = revAxis;
        so.FindProperty("bottlesChartArea").objectReferenceValue = botChart;
        so.FindProperty("bottlesTitle").objectReferenceValue     = botTitle;
        so.FindProperty("bottlesAxisHint").objectReferenceValue  = botAxis;
        so.ApplyModifiedPropertiesWithoutUndo();

        // --- Ensure a StatsTracker exists under Systems (or root) ---
        var stats = Object.FindFirstObjectByType<StatsTracker>();
        if (!stats)
        {
            Transform systems = canvas.transform.root.Find("Systems");
            GameObject host;
            if (systems) host = systems.gameObject;
            else
            {
                host = new GameObject("Systems", typeof(RectTransform));
                host.transform.SetParent(canvas.transform.root, false);
            }
            var comp = Undo.AddComponent<StatsTracker>(host);
            Undo.RegisterCreatedObjectUndo(host, "Create StatsTracker host");
        }

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Undo.CollapseUndoOperations(group);

        Debug.Log("WineSim: Stats UI built & wired.\n- StatsScreen under Canvas/Screens/\n- Revenue and Bottles bar charts\n- StatsPanel references assigned\n- StatsTracker ensured in scene");
    }

    // --- helpers ---
    private static RectTransform Ensure(Transform parent, string name)
    {
        var t = parent.Find(name) as RectTransform;
        if (!t)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            t = go.GetComponent<RectTransform>();
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        }
        // Give group a vertical layout so Title/Axis/Chart stack nicely
        if (name == "Revenue" || name == "Bottles")
        {
            var vlg = t.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(t.gameObject);
            vlg.spacing = 6; vlg.childControlWidth = true; vlg.childControlHeight = true; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        }
        return t;
    }

    private static TextMeshProUGUI EnsureTMP(Transform parent, string name, string text, int size, FontStyles style)
    {
        var t = parent.Find(name) as RectTransform;
        TextMeshProUGUI tmp;
        if (!t)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            t = go.GetComponent<RectTransform>();
            tmp = go.GetComponent<TextMeshProUGUI>();
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        }
        else tmp = t.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style; tmp.raycastTarget = false;
        return tmp;
    }

    private static void GiveChartAreaLook(RectTransform area)
    {
        // Stretch, add background image and LayoutElement
        area.anchorMin = new Vector2(0, 0); area.anchorMax = new Vector2(1, 1);
        area.offsetMin = new Vector2(0, 0); area.offsetMax = new Vector2(0, 0); area.pivot = new Vector2(0.5f, 0.5f);
        var img = area.GetComponent<Image>() ?? Undo.AddComponent<Image>(area.gameObject);
        img.color = new Color(0.96f, 0.98f, 1f, 1f);
        var le = area.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(area.gameObject);
        le.minHeight = 280;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; rt.pivot = new Vector2(0.5f,0.5f);
    }
}
#endif