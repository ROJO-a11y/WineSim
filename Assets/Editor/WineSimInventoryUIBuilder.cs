#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class WineSimInventoryUIBuilder
{
    private const string PrefabsFolder = "Assets/Prefabs";

    [MenuItem("Tools/WineSim/Build Inventory UI")]
    public static void BuildInventoryUI()
    {
        Undo.IncrementCurrentGroup();
        int group = Undo.GetCurrentGroup();

        // Canvas
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

        // Screens parent
        var screens = GameObject.Find("Screens");
        if (!screens)
        {
            screens = new GameObject("Screens", typeof(RectTransform));
            screens.transform.SetParent(canvas.transform, false);
            StretchFull(screens.GetComponent<RectTransform>());
            Undo.RegisterCreatedObjectUndo(screens, "Create Screens");
        }

        // InventoryScreen
        var inv = GameObject.Find("InventoryScreen");
        if (!inv)
        {
            inv = new GameObject("InventoryScreen", typeof(RectTransform));
            inv.transform.SetParent(screens.transform, false);
            Undo.RegisterCreatedObjectUndo(inv, "Create InventoryScreen");
        }
        StretchFull(inv.GetComponent<RectTransform>());
        var bg = inv.GetComponent<Image>() ?? Undo.AddComponent<Image>(inv);
        bg.color = new Color(1f,1f,1f,0f);

        var vlg = inv.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(inv);
        vlg.padding = new RectOffset(16,16,16,16); vlg.spacing = 12; vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;
        var fitter = inv.GetComponent<ContentSizeFitter>() ?? inv.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.Unconstrained;

        // TopRow
        var topRow = Ensure(inv.transform, "TopRow", out RectTransform _);
        var hlg = topRow.GetComponent<HorizontalLayoutGroup>() ?? Undo.AddComponent<HorizontalLayoutGroup>(topRow);
        hlg.childAlignment = TextAnchor.MiddleLeft; hlg.spacing = 12;
        var leTop = topRow.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(topRow);
        leTop.preferredHeight = 120;

        var cash = EnsureTMP(topRow.transform, "CashText", "Cash: $0", 32, FontStyles.Bold);

        // StockScroll
        var stockScroll = Ensure(inv.transform, "StockScroll", out RectTransform stockRT);
        var leStock = stockScroll.GetComponent<LayoutElement>() ?? Undo.AddComponent<LayoutElement>(stockScroll);
        leStock.flexibleHeight = 1f;
        var sr = stockScroll.GetComponent<ScrollRect>() ?? Undo.AddComponent<ScrollRect>(stockScroll);
        sr.horizontal = false; sr.vertical = true;
        var vp = stockScroll.transform.Find("Viewport") as RectTransform;
        if (!vp)
        {
            var go = new GameObject("Viewport", typeof(RectTransform), typeof(CanvasRenderer), typeof(RectMask2D));
            vp = go.GetComponent<RectTransform>();
            vp.SetParent(stockScroll.transform, false);
        }
        vp.anchorMin = Vector2.zero; vp.anchorMax = Vector2.one; vp.offsetMin = Vector2.zero; vp.offsetMax = Vector2.zero;
        sr.viewport = vp;
        var content = vp.Find("Content") as RectTransform;
        if (!content)
        {
            var go = new GameObject("Content", typeof(RectTransform));
            content = go.GetComponent<RectTransform>();
            content.SetParent(vp, false);
        }
        content.anchorMin = Vector2.zero; content.anchorMax = Vector2.one; content.offsetMin = Vector2.zero; content.offsetMax = Vector2.zero;
        var vlgC = content.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(content.gameObject);
        vlgC.spacing = 8; vlgC.childForceExpandWidth = true; vlgC.childForceExpandHeight = false;
        var fitC = content.GetComponent<ContentSizeFitter>() ?? Undo.AddComponent<ContentSizeFitter>(content.gameObject);
        fitC.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        sr.content = content;

        // Sell Modal
        var modal = Ensure(inv.transform, "SellModal", out RectTransform modalRT);
        modal.SetActive(false);
        AnchorBottom(modalRT, 0.45f);
        var modalBG = modal.GetComponent<Image>() ?? Undo.AddComponent<Image>(modal);
        modalBG.color = new Color(0,0,0,0.35f);

        // Panel inside modal
        var panel = Ensure(modal.transform, "Panel", out RectTransform panelRT);
        panelRT.anchorMin = new Vector2(0.1f, 0.1f);
        panelRT.anchorMax = new Vector2(0.9f, 0.9f);
        panelRT.offsetMin = Vector2.zero; panelRT.offsetMax = Vector2.zero;
        var panelImg = panel.GetComponent<Image>() ?? Undo.AddComponent<Image>(panel);
        panelImg.color = new Color(0.97f, 0.98f, 1f, 1f);
        var pvlg = panel.GetComponent<VerticalLayoutGroup>() ?? Undo.AddComponent<VerticalLayoutGroup>(panel);
        pvlg.padding = new RectOffset(16,16,16,16); pvlg.spacing = 10;

        var title = EnsureTMP(panel.transform, "Title", "Sell", 34, FontStyles.Bold);
        var maxText = EnsureTMP(panel.transform, "MaxText", "In stock: 0", 26, FontStyles.Normal);

        // Qty row
        var qtyRow = Ensure(panel.transform, "QtyRow", out RectTransform _qtyRT);
        var hlgQty = qtyRow.GetComponent<HorizontalLayoutGroup>() ?? Undo.AddComponent<HorizontalLayoutGroup>(qtyRow);
        hlgQty.spacing = 8; hlgQty.childAlignment = TextAnchor.MiddleLeft;
        var qtyLabel = EnsureTMP(qtyRow.transform, "QtyLabel", "Qty:", 28, FontStyles.Normal);
        var qtyGO = new GameObject("QtyInput", typeof(RectTransform), typeof(CanvasRenderer), typeof(TMP_InputField), typeof(Image));
        qtyGO.transform.SetParent(qtyRow.transform, false);
        var qtyRT = (RectTransform)qtyGO.transform;
        qtyRT.sizeDelta = new Vector2(220, 64);
        var qtyImg = qtyGO.GetComponent<Image>(); qtyImg.color = new Color(1,1,1,1);
        var qtyTextGO = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        qtyTextGO.transform.SetParent(qtyGO.transform, false);
        var qtyTextRT = (RectTransform)qtyTextGO.transform;
        qtyTextRT.anchorMin = Vector2.zero; qtyTextRT.anchorMax = Vector2.one; qtyTextRT.offsetMin = new Vector2(10, 10); qtyTextRT.offsetMax = new Vector2(-10, -10);
        var qtyText = qtyTextGO.GetComponent<TextMeshProUGUI>(); qtyText.fontSize = 28; qtyText.text = "0";
        var input = qtyGO.GetComponent<TMP_InputField>();
        input.textViewport = qtyTextRT;
        input.textComponent = qtyText;
        input.contentType = TMP_InputField.ContentType.IntegerNumber;

        var estText = EnsureTMP(panel.transform, "EstText", "Est. revenue: $0", 26, FontStyles.Italic);

        var btnRow = Ensure(panel.transform, "BtnRow", out RectTransform _btnRT);
        var hlgBtn = btnRow.GetComponent<HorizontalLayoutGroup>() ?? Undo.AddComponent<HorizontalLayoutGroup>(btnRow);
        hlgBtn.spacing = 12; hlgBtn.childAlignment = TextAnchor.MiddleRight;
        var confirm = CreateButton(btnRow.transform, "ConfirmBtn", "Sell");
        var cancel  = CreateButton(btnRow.transform, "CancelBtn", "Cancel");

        // Create prefab: InventoryItemPrefab
        EnsureFolder(PrefabsFolder);
        var prefab = AssetDatabase.LoadAssetAtPath<InventoryItemUI>(PrefabsFolder + "/InventoryItemPrefab.prefab");
        if (!prefab)
        {
            var root = new GameObject("InventoryItemPrefab", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            var rootImg = root.GetComponent<Image>(); rootImg.color = new Color(0.95f, 0.97f, 1f, 1f);
            var vlgI = root.AddComponent<VerticalLayoutGroup>(); vlgI.padding = new RectOffset(12,12,12,12); vlgI.spacing = 6;
            var leI = root.AddComponent<LayoutElement>(); leI.minHeight = 160;

            var titleI = new GameObject("Title", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            titleI.transform.SetParent(root.transform, false); titleI.fontSize = 32; titleI.fontStyle = FontStyles.Bold; titleI.text = "Wine • 2025";
            var infoI = new GameObject("Info", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            infoI.transform.SetParent(root.transform, false); infoI.fontSize = 24; infoI.text = "Bottles: 0\nQuality: 0.0";
            var sell = CreateButton(root.transform, "SellBtn", "Sell");

            var comp = root.AddComponent<InventoryItemUI>();
            comp.__EditorAssign(titleI, infoI, sell);

            var saved = PrefabUtility.SaveAsPrefabAsset(root, PrefabsFolder + "/InventoryItemPrefab.prefab");
            Object.DestroyImmediate(root);
            prefab = saved.GetComponent<InventoryItemUI>();
        }

        // Add InventoryPanel + wire
        var panelComp = inv.GetComponent<InventoryPanel>() ?? Undo.AddComponent<InventoryPanel>(inv);
        var so = new SerializedObject(panelComp);
        so.FindProperty("cashText").objectReferenceValue = cash;
        so.FindProperty("stockContent").objectReferenceValue = content;
        so.FindProperty("stockItemPrefab").objectReferenceValue = prefab;
        so.FindProperty("sellModal").objectReferenceValue = modal;
        so.FindProperty("sellTitle").objectReferenceValue = title;
        so.FindProperty("qtyInput").objectReferenceValue = input;
        so.FindProperty("maxText").objectReferenceValue = maxText;
        so.FindProperty("estText").objectReferenceValue = estText;
        so.FindProperty("confirmSellBtn").objectReferenceValue = confirm;
        so.FindProperty("cancelSellBtn").objectReferenceValue = cancel;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
        Undo.CollapseUndoOperations(group);

        Debug.Log("WineSim: Inventory UI built & wired.\n- InventoryScreen under Canvas/Screens/\n- StockScroll list with item prefab\n- SellModal with qty + confirm\n- InventoryPanel references assigned");
    }

    // -------- helpers --------
    private static GameObject Ensure(Transform parent, string name, out RectTransform rt)
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

    private static TextMeshProUGUI EnsureTMP(Transform parent, string name, string text, int size, FontStyles style)
    {
        var t = parent.Find(name);
        TextMeshProUGUI tmp;
        if (!t)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            tmp = go.GetComponent<TextMeshProUGUI>();
            Undo.RegisterCreatedObjectUndo(go, "Create " + name);
        }
        else tmp = t.GetComponent<TextMeshProUGUI>();
        tmp.text = text; tmp.fontSize = size; tmp.fontStyle = style; tmp.raycastTarget = false;
        return tmp;
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        var root = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        var img = root.GetComponent<Image>(); img.color = new Color(0.90f, 0.93f, 0.98f, 1f);
        var le = root.GetComponent<LayoutElement>(); le.minHeight = 80; le.minWidth = 180;

        var txtGO = new GameObject("Label", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
        txtGO.transform.SetParent(root.transform, false);
        var txt = txtGO.GetComponent<TextMeshProUGUI>(); txt.text = label; txt.fontSize = 30; txt.alignment = TextAlignmentOptions.Center; txt.raycastTarget = false;
        var r = txtGO.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;

        Undo.RegisterCreatedObjectUndo(root, "Create " + name);
        return root.GetComponent<Button>();
    }

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; rt.pivot = new Vector2(0.5f,0.5f);
    }

    private static void AnchorBottom(RectTransform rt, float heightFrac)
    {
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, heightFrac);
        rt.offsetMin = Vector2.zero; rt.offsetMax = Vector2.zero; rt.pivot = new Vector2(0.5f, 0);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        string cur = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = cur + "/" + parts[i];
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(cur, parts[i]);
            cur = next;
        }
    }
}
#endif