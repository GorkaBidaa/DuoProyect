using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public static class CursedForestHudBuilder
{
    [MenuItem("Tools/Cursed Forest/Create HUD")]
    public static void Create()
    {
        string folder = null;
        foreach (string guid in AssetDatabase.FindAssets("CursedForestHud t:MonoScript"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            const string suffix = "/Scripts/CursedForestHud.cs";
            if (path.EndsWith(suffix, StringComparison.Ordinal)) { folder = path.Substring(0, path.Length - suffix.Length); break; }
        }
        if (folder == null) throw new InvalidOperationException("Keep the kit folders together under Assets.");

        string[] layers = { "backplate", "health-track", "stamina-track", "health-fill", "stamina-fill", "item-slot", "count-badge", "health-icon", "stamina-icon" };
        foreach (string layer in layers)
        {
            string path = folder + "/Sprites/" + layer + ".png";
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (!importer) throw new InvalidOperationException("Missing sprite: " + path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
        }

        Canvas canvas = Selection.activeGameObject ? Selection.activeGameObject.GetComponentInParent<Canvas>() : null;
        if (!canvas) canvas = UnityEngine.Object.FindObjectOfType<Canvas>();
        if (!canvas)
        {
            var go = new GameObject("HUD Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            Undo.RegisterCreatedObjectUndo(go, "Create HUD Canvas");
            canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
        }
        var root = new GameObject("Cursed Forest HUD", typeof(RectTransform), typeof(CursedForestHud));
        Undo.RegisterCreatedObjectUndo(root, "Create Cursed Forest HUD");
        var rect = root.GetComponent<RectTransform>();
        rect.SetParent(canvas.transform, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.zero;
        rect.sizeDelta = new Vector2(256, 256);
        rect.anchoredPosition = new Vector2(24, 24);
        var hud = root.GetComponent<CursedForestHud>();
        foreach (string layer in layers)
        {
            Image image = NewImage(layer, rect);
            image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(folder + "/Sprites/" + layer + ".png");
            if (layer.EndsWith("-fill", StringComparison.Ordinal))
            {
                image.type = Image.Type.Filled;
                image.fillMethod = Image.FillMethod.Radial360;
                image.fillOrigin = (int)Image.Origin360.Bottom;
                image.fillClockwise = layer == "health-fill";
                image.fillAmount = 0;
                image.enabled = false;
                if (layer == "health-fill") hud.healthFill = image; else hud.staminaFill = image;
            }
        }
        hud.itemIcon = NewImage("Current Item (empty)", rect);
        SetBox(hud.itemIcon.rectTransform, new Vector2(0, 2), new Vector2(110, 110));
        hud.itemIcon.preserveAspect = true;
        hud.itemIcon.enabled = false;
        var count = new GameObject(
            "Item Count (empty)",
            typeof(RectTransform),
            typeof(TextMeshProUGUI)
);

        count.transform.SetParent(rect, false);
        hud.itemCount = count.GetComponent<TextMeshProUGUI>();
        hud.itemCount.fontSize = 18;
        hud.itemCount.alignment = TextAlignmentOptions.Center;
        hud.itemCount.color = Color.white;
        hud.itemCount.raycastTarget = false;
        hud.itemCount.text = string.Empty;
        SetBox(hud.itemCount.rectTransform, new Vector2(46.625f, -42.625f), new Vector2(30, 24));
        Selection.activeGameObject = root;
    }

    private static Image NewImage(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        var image = go.GetComponent<Image>();
        var rect = image.rectTransform;
        rect.SetParent(parent, false);
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = rect.offsetMax = Vector2.zero;
        image.raycastTarget = false;
        return image;
    }

    private static void SetBox(RectTransform rect, Vector2 position, Vector2 size)
    {
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = position;
        rect.sizeDelta = size;
    }
}
