#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class PixelPaintPngConverterWindow : EditorWindow
{
    private Texture2D sourceTexture;
    private string sourcePngPath = "";
    private int maxPaletteColors = 8;
    private bool includeAlphaAsEmpty = true;
    private float alphaThreshold = 0.2f;
    private bool useCompactRowFormat = true;
    private bool flipY = true;
    private Vector2 scroll;
    private string outputRows = "";
    private string outputPalette = "";

    [MenuItem("Tools/Pixel Paint/PNG Converter")]
    private static void Open()
    {
        var win = GetWindow<PixelPaintPngConverterWindow>("PixelPaint PNG");
        win.minSize = new Vector2(520f, 520f);
    }

    private void OnGUI()
    {
        EditorGUILayout.LabelField("PNG -> PixelPaint Rows", EditorStyles.boldLabel);
        EditorGUILayout.Space(4);

        sourceTexture = (Texture2D)EditorGUILayout.ObjectField("Source PNG", sourceTexture, typeof(Texture2D), false);
        EditorGUILayout.BeginHorizontal();
        sourcePngPath = EditorGUILayout.TextField("PNG Path", sourcePngPath);
        if (GUILayout.Button("Browse", GUILayout.Width(72f)))
        {
            string picked = EditorUtility.OpenFilePanel("Select PNG", Application.dataPath, "png");
            if (!string.IsNullOrEmpty(picked))
                sourcePngPath = picked;
        }
        EditorGUILayout.EndHorizontal();

        maxPaletteColors = EditorGUILayout.IntSlider("Max Palette", maxPaletteColors, 2, 16);
        includeAlphaAsEmpty = EditorGUILayout.Toggle("Alpha => 0", includeAlphaAsEmpty);
        if (includeAlphaAsEmpty)
            alphaThreshold = EditorGUILayout.Slider("Alpha Threshold", alphaThreshold, 0f, 1f);
        useCompactRowFormat = EditorGUILayout.Toggle("Compact Rows", useCompactRowFormat);
        flipY = EditorGUILayout.Toggle("Top Row First", flipY);

        EditorGUILayout.Space(8);
        using (new EditorGUI.DisabledScope(sourceTexture == null && string.IsNullOrWhiteSpace(sourcePngPath)))
        {
            if (GUILayout.Button("Convert", GUILayout.Height(34f)))
                ConvertNow();
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Rows (paste into puzzle.rows)", EditorStyles.boldLabel);
        scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.Height(220f));
        EditorGUILayout.TextArea(outputRows, GUILayout.ExpandHeight(true));
        EditorGUILayout.EndScrollView();
        if (!string.IsNullOrEmpty(outputRows))
        {
            if (GUILayout.Button("Copy Rows"))
                EditorGUIUtility.systemCopyBuffer = outputRows;
        }

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("Palette Preview (index -> color)", EditorStyles.boldLabel);
        EditorGUILayout.TextArea(outputPalette, GUILayout.Height(120f));
    }

    private void ConvertNow()
    {
        outputRows = "";
        outputPalette = "";

        if (!TryGetReadableTexture(out Texture2D tex, out bool destroyAfterUse, out string error))
        {
            EditorUtility.DisplayDialog("Convert Failed", error, "OK");
            return;
        }

        Color32[] pixels = tex.GetPixels32();
        int width = tex.width;
        int height = tex.height;
        if (pixels == null || pixels.Length != width * height)
        {
            if (destroyAfterUse) DestroyImmediate(tex);
            return;
        }

        List<Color32> palette = BuildPalette(pixels, maxPaletteColors, includeAlphaAsEmpty, alphaThreshold);
        if (palette.Count == 0)
            palette.Add(new Color32(255, 255, 255, 255));

        StringBuilder rowsBuilder = new StringBuilder(4096);

        for (int row = 0; row < height; row++)
        {
            int y = flipY ? (height - 1 - row) : row;
            List<int> rowValues = new List<int>(width);

            for (int x = 0; x < width; x++)
            {
                Color32 c = pixels[y * width + x];
                int index = ToColorIndex(c, palette, includeAlphaAsEmpty, alphaThreshold);
                rowValues.Add(index);
            }

            if (useCompactRowFormat)
            {
                for (int i = 0; i < rowValues.Count; i++)
                    rowsBuilder.Append(ToCellChar(rowValues[i]));
            }
            else
            {
                for (int i = 0; i < rowValues.Count; i++)
                {
                    if (i > 0) rowsBuilder.Append(' ');
                    rowsBuilder.Append(rowValues[i]);
                }
            }

            if (row < height - 1)
                rowsBuilder.AppendLine();
        }

        StringBuilder paletteBuilder = new StringBuilder(512);
        for (int i = 0; i < palette.Count; i++)
        {
            Color32 p = palette[i];
            paletteBuilder.AppendLine($"{i + 1}: #{p.r:X2}{p.g:X2}{p.b:X2}");
        }

        outputRows = rowsBuilder.ToString();
        outputPalette = paletteBuilder.ToString();

        if (destroyAfterUse) DestroyImmediate(tex);
    }

    private bool TryGetReadableTexture(out Texture2D tex, out bool destroyAfterUse, out string error)
    {
        tex = null;
        destroyAfterUse = false;
        error = "";

        // 1) If object field is assigned, try imported texture first.
        if (sourceTexture != null)
        {
            string assetPath = AssetDatabase.GetAssetPath(sourceTexture);
            if (!string.IsNullOrEmpty(assetPath))
            {
                var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
                if (importer != null && importer.isReadable)
                {
                    tex = sourceTexture;
                    return true;
                }

                // fallback: load PNG bytes from asset path, so Read/Write is not required.
                string projectRoot = Directory.GetParent(Application.dataPath).FullName;
                string absoluteAssetPath = Path.GetFullPath(Path.Combine(projectRoot, assetPath));
                if (TryLoadTextureFromFile(absoluteAssetPath, out tex))
                {
                    destroyAfterUse = true;
                    return true;
                }
            }
        }

        // 2) If path is entered, load directly.
        if (!string.IsNullOrWhiteSpace(sourcePngPath))
        {
            string resolved = ResolvePngPath(sourcePngPath.Trim());
            if (!string.IsNullOrEmpty(resolved) && TryLoadTextureFromFile(resolved, out tex))
            {
                destroyAfterUse = true;
                return true;
            }
        }

        error = "PNG를 읽을 수 없습니다. Texture 할당 또는 PNG 경로를 확인해 주세요.";
        return false;
    }

    private static string ResolvePngPath(string inputPath)
    {
        if (File.Exists(inputPath))
            return inputPath;

        // Unity-relative path like "Assets/Art/foo.png"
        string projectRoot = Directory.GetParent(Application.dataPath).FullName;
        string combined = Path.GetFullPath(Path.Combine(projectRoot, inputPath));
        if (File.Exists(combined))
            return combined;

        // folder path: pick first png in folder
        string folder = inputPath;
        if (!Directory.Exists(folder))
        {
            string combinedFolder = Path.GetFullPath(Path.Combine(projectRoot, inputPath));
            if (Directory.Exists(combinedFolder))
                folder = combinedFolder;
            else
                return null;
        }

        string[] pngs = Directory.GetFiles(folder, "*.png", SearchOption.TopDirectoryOnly);
        if (pngs.Length == 0) return null;
        return pngs[0];
    }

    private static bool TryLoadTextureFromFile(string path, out Texture2D tex)
    {
        tex = null;
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
            return false;

        byte[] bytes = File.ReadAllBytes(path);
        if (bytes == null || bytes.Length == 0)
            return false;

        tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        return tex.LoadImage(bytes, false);
    }

    private static List<Color32> BuildPalette(Color32[] pixels, int maxCount, bool alphaAsEmpty, float alphaThreshold)
    {
        Dictionary<int, int> countByColor = new Dictionary<int, int>();

        for (int i = 0; i < pixels.Length; i++)
        {
            Color32 c = pixels[i];
            if (alphaAsEmpty && (c.a / 255f) < alphaThreshold)
                continue;

            int key = (c.r << 16) | (c.g << 8) | c.b;
            if (countByColor.TryGetValue(key, out int count))
                countByColor[key] = count + 1;
            else
                countByColor[key] = 1;
        }

        List<KeyValuePair<int, int>> sorted = new List<KeyValuePair<int, int>>(countByColor);
        sorted.Sort((a, b) => b.Value.CompareTo(a.Value));

        List<Color32> palette = new List<Color32>();
        int take = Mathf.Min(maxCount, sorted.Count);
        for (int i = 0; i < take; i++)
        {
            int key = sorted[i].Key;
            byte r = (byte)((key >> 16) & 0xFF);
            byte g = (byte)((key >> 8) & 0xFF);
            byte b = (byte)(key & 0xFF);
            palette.Add(new Color32(r, g, b, 255));
        }

        return palette;
    }

    private static int ToColorIndex(Color32 c, List<Color32> palette, bool alphaAsEmpty, float alphaThreshold)
    {
        if (alphaAsEmpty && (c.a / 255f) < alphaThreshold)
            return 0;

        int best = 0;
        int bestDist = int.MaxValue;
        for (int i = 0; i < palette.Count; i++)
        {
            Color32 p = palette[i];
            int dr = c.r - p.r;
            int dg = c.g - p.g;
            int db = c.b - p.b;
            int dist = (dr * dr) + (dg * dg) + (db * db);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = i;
            }
        }

        return best + 1;
    }

    private static char ToCellChar(int value)
    {
        if (value <= 0) return '0';
        if (value < 10) return (char)('0' + value);
        value -= 10;
        if (value < 26) return (char)('A' + value);
        return 'Z';
    }
}
#endif
