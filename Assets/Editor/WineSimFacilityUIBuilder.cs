#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

public static class WineSimFacilityUIBuilder
{
    private const string PrefabsFolder = "Assets/Prefabs";

    [MenuItem("Tools/WineSim/Build Facility UI")]
    public static void BuildFacilityUI()
    {
        Undo.IncrementCurrentGroup();
        var group = Undo.GetCurrentGroup();

        // Find or create Canvas
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

        // Find/Create Screens parent
        var screens = GameObject.Find("Screens");
        if (!screens)
        {
            screens = new GameObject("Screens", typeof(RectTransform));
            screens.transform.SetParent(canvas.transform, false);
            AnchorStretchFull(screens.GetComponent<RectTransform>());
            Undo.RegisterCreatedObjectUndo(screens, "Create Screens");
        }

        // Find or create FacilityScreen (selected overrides)
        GameObject facilityScreen = Selection.activeGameObject;
        if (facilityScreen == null || facilityScreen.GetComponentInParent<Canvas>() == null)
        {
            facilityScreen = GameObject.Find("FacilityScreen");
            if (!facilityScreen)
            {
                facilityScreen = new GameObject("FacilityScreen", typeof(RectTransform));
                facilityScreen.transform.SetParent(screens.transform, false);
                Undo.RegisterCreatedObjectUndo(facilityScreen, "Create FacilityScreen");
            }
        }
        AnchorStretchFull(facilityScreen.GetComponent<RectTransform>());
        EnsureComponent<VerticalLayoutGroup>(facilityScreen, v =>
        {
            v.padding = new RectOffset(16, 16, 16, 16);
            v.spacing = 12f;
            v.childForceExpandWidth = true;
            v.childForceExpandHeight = false;
        });

        // TOP ROW
        var topRow = EnsureChild(facilityScreen.transform, "TopRow", out RectTransform topRowRT);
        EnsureComponent<HorizontalLayoutGroup>(topRow, h =>
        {
            h.childAlignment = TextAnchor.MiddleLeft;
            h.spacing = 12f;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
        });

        var cashText = EnsureTMP(topRowRT, "CashText", "Cash: $0", 32, FontStyles.Bold);
        var bottlerText = EnsureTMP(topRowRT, "BottlerText", "Bottler: No", 28, FontStyles.Normal);

        var buyRow = EnsureChild(topRowRT, "BuyRow", out RectTransform buyRowRT);
        EnsureComponent<HorizontalLayoutGroup>(buyRow, h =>
        {
            h.spacing = 8f;
            h.childAlignment = TextAnchor.MiddleRight;
            h.childForceExpandWidth = false;
            h.childForceExpandHeight = false;
        });
        var b1 = EnsureButtonTMP(buyRowRT, "+1kL");
        var b2 = EnsureButtonTMP(buyRowRT, "+3kL");
        var b3 = EnsureButtonTMP(buyRowRT, "+6kL");
        var b4 = EnsureButtonTMP(buyRowRT, "+Barrel");
        var b5 = EnsureButtonTMP(buyRowRT, "+Bottler");

        // TANKS SCROLL
        var tanksScroll = EnsureScrollList(facilityScreen.transform, "TanksScroll", out RectTransform tanksContent);
        EnsureComponent<LayoutElement>(tanksScroll, le => { le.flexibleHeight = 1f; });
        // BARRELS SCROLL
        var barrelsScroll = EnsureScrollList(facilityScreen.transform, "BarrelsScroll", out RectTransform barrelsContent);
        EnsureComponent<LayoutElement>(barrelsScroll, le => { le.flexibleHeight = 1f; });

        // PREFABS: TankItemPrefab + BarrelItemPrefab
        EnsureFolder(PrefabsFolder);
        var tankItemPrefab = EnsureTankItemPrefab();
        var barrelItemPrefab = EnsureBarrelItemPrefab();

        // FACILITY PANEL + WIRING
        var panel = facilityScreen.GetComponent<FacilityPanel>() ?? Undo.AddComponent<FacilityPanel>(facilityScreen);
        // Assign serialized fields via SerializedObject to be safe
        var so = new SerializedObject(panel);
        so.FindProperty("cashText").objectReferenceValue = cashText;
        so.FindProperty("bottlerText").objectReferenceValue = bottlerText;
        so.FindProperty("buyTankSmallBtn").objectReferenceValue = b1;
        so.FindProperty("buyTankMedBtn").objectReferenceValue = b2;
        so.FindProperty("buyTankLargeBtn").objectReferenceValue = b3;
        so.FindProperty("buyBarrelBtn").objectReferenceValue = b4;
        so.FindProperty("buyBottlerBtn").objectReferenceValue = b5;
        so.FindProperty("tanksContent").objectReferenceValue = tanksContent;
        so.FindProperty("barrelsContent").objectReferenceValue = barrelsContent;
        so.FindProperty("tankItemPrefab").objectReferenceValue = tankItemPrefab;
        so.FindProperty("barrelItemPrefab").objectReferenceValue = barrelItemPrefab;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(panel);
        EditorSceneManager.MarkSceneDirty(panel.gameObject.scene);

        Debug.Log("WineSim: Facility UI built & wired.\n" +
                  "- TopRow with cash/bottler + buy buttons\n" +
                  "- Tanks & Barrels scroll lists\n" +
                  "- Prefabs created under Assets/Prefabs/\n" +
                  "- FacilityPanel references assigned");
        Undo.CollapseUndoOperations(group);
    }

    // ---------- helpers ----------

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = current + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private static GameObject EnsureChild(Transform parent, string name, out RectTransform rt)
    {
        var t = parent.Find(name);
        GameObject go;
        if (!t)
        {
            go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        }
        else go = t.gameObject;
        rt = go.GetComponent<RectTransform>();
        return go;
    }

    private static T EnsureComponent<T>(GameObject go, System.Action<T> init = null) where T : Component
    {
        var c = go.GetComponent<T>();
        if (!c) { c = Undo.AddComponent<T>(go); init?.Invoke(c); }
        return c;
    }
    private static T EnsureComponent<T>(RectTransform rt, System.Action<T> init = null) where T : Component
        => EnsureComponent<T>(rt.gameObject, init);

    private static void AnchorStretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        rt.pivot = new Vector2(0.5f, 0.5f);
    }

    private static TMP_Text EnsureTMP(Transform parent, string name, string text, int size, FontStyles style)
    {
        var go = parent.Find(name)?.gameObject;
        if (!go)
        {
            go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        }
        var tmp = go.GetComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.fontStyle = style;
        tmp.enableWordWrapping = true;
        tmp.raycastTarget = false;
        return tmp;
    }

    private static Button EnsureButtonTMP(Transform parent, string label)
    {
        // Root
        var root = new GameObject(label + "Btn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        root.transform.SetParent(parent, false);
        Undo.RegisterCreatedObjectUndo(root, "Create " + root.name);
        var img = root.GetComponent<Image>();
        img.color = new Color(0.9f, 0.93f, 0.98f, 1f);
        var le = root.AddComponent<LayoutElement>();
        le.minWidth = 180; le.minHeight = 96;

        // Label
        var txtGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(root.transform, false);
        var txt = txtGO.GetComponent<TextMeshProUGUI>();
        txt.text = label;
        txt.fontSize = 32;
        txt.alignment = TextAlignmentOptions.Center;
        txt.raycastTarget = false;

        // Stretch label
        var r = txtGO.GetComponent<RectTransform>();
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;

        return root.GetComponent<Button>();
    }

    private static GameObject EnsureScrollList(Transform parent, string name, out RectTransform content)
    {
        var root = parent.Find(name)?.gameObject;
        if (!root)
        {
            root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            root.transform.SetParent(parent, false);
            Undo.RegisterCreatedObjectUndo(root, "Create " + name);
        }
        var rootRT = root.GetComponent<RectTransform>();
        AnchorStretchFull(rootRT);
        var bg = root.GetComponent<Image>(); bg.color = new Color(0.96f, 0.96f, 0.96f, 1f);

        var scroll = root.GetComponent<ScrollRect>() ?? Undo.AddComponent<ScrollRect>(root);
        scroll.horizontal = false;
        scroll.vertical = true;

        // Viewport
        var viewport = root.transform.Find("Viewport")?.gameObject;
        if (!viewport)
        {
            viewport = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Mask));
            viewport.transform.SetParent(root.transform, false);
            Undo.RegisterCreatedObjectUndo(viewport, "Create Viewport");
        }
        var vpRT = viewport.GetComponent<RectTransform>();
        AnchorStretchFull(vpRT);
        viewport.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
        viewport.GetComponent<Mask>().showMaskGraphic = false;

        // Content
        var contentGO = viewport.transform.Find("Content")?.gameObject;
        if (!contentGO)
        {
            contentGO = new GameObject("Content", typeof(RectTransform));
            contentGO.transform.SetParent(viewport.transform, false);
            Undo.RegisterCreatedObjectUndo(contentGO, "Create Content");
        }
        content = contentGO.GetComponent<RectTransform>();
        AnchorStretchFull(content);
        var vlg = contentGO.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(contentGO);
        vlg.spacing = 8f; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var fitter = contentGO.GetComponent<ContentSizeFitter>() ?? Undo.AddComponent<ContentSizeFitter>(contentGO);
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = vpRT;
        scroll.content = content;

        return root;
    }

    private static TankItemUI EnsureTankItemPrefab()
    {
        string path = PrefabsFolder + "/TankItemPrefab.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<TankItemUI>(path);
        if (existing) return existing;

        var root = new GameObject("TankItemPrefab", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var img = root.GetComponent<Image>(); img.color = new Color(0.93f, 0.97f, 1f, 1f);
        var vlg = root.AddComponent<VerticalLayoutGroup>(); vlg.padding = new RectOffset(8, 8, 8, 8); vlg.spacing = 4;
        var le = root.AddComponent<LayoutElement>(); le.minHeight = 140;

        var title = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        title.transform.SetParent(root.transform, false);
        title.text = "Tank • Title";
        title.fontSize = 32; title.fontStyle = FontStyles.Bold;

        var info = new GameObject("Info", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        info.transform.SetParent(root.transform, false);
        info.text = "Info"; info.fontSize = 26; info.enableWordWrapping = true;

        var rackBtn = EnsureButtonTMP(root.transform, "Rack to barrel");

        var comp = root.AddComponent<TankItemUI>();
        comp.GetType().GetField("title", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            ?.SetValue(comp, title);
        comp.GetType().GetField("info", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            ?.SetValue(comp, info);
        comp.GetType().GetField("rackBtn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            ?.SetValue(comp, rackBtn);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<TankItemUI>();
    }

    private static BarrelItemUI EnsureBarrelItemPrefab()
    {
        string path = PrefabsFolder + "/BarrelItemPrefab.prefab";
        var existing = AssetDatabase.LoadAssetAtPath<BarrelItemUI>(path);
        if (existing) return existing;

        var root = new GameObject("BarrelItemPrefab", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        var img = root.GetComponent<Image>(); img.color = new Color(0.98f, 0.96f, 0.92f, 1f);
        var vlg = root.AddComponent<VerticalLayoutGroup>(); vlg.padding = new RectOffset(8, 8, 8, 8); vlg.spacing = 4;
        var le = root.AddComponent<LayoutElement>(); le.minHeight = 140;

        var title = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        title.transform.SetParent(root.transform, false);
        title.text = "Barrel • Title";
        title.fontSize = 32; title.fontStyle = FontStyles.Bold;

        var info = new GameObject("Info", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
        info.transform.SetParent(root.transform, false);
        info.text = "Info"; info.fontSize = 26; info.enableWordWrapping = true;

        var bottleBtn = EnsureButtonTMP(root.transform, "Bottle");

        var comp = root.AddComponent<BarrelItemUI>();
        comp.GetType().GetField("title", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            ?.SetValue(comp, title);
        comp.GetType().GetField("info", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            ?.SetValue(comp, info);
        comp.GetType().GetField("bottleBtn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            ?.SetValue(comp, bottleBtn);

        var prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab.GetComponent<BarrelItemUI>();
    }
}
#endif