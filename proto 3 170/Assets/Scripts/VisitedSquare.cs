using System.Collections;
using UnityEngine;

public class VisitedTrailGrid : MonoBehaviour
{
    [Header("Refs")]
    public MazeGenerator generator;              // drag your MazeGenerator (or it will try GetComponent)
    public Material tileMatTransparent;          // URP/Lit Transparent, BaseColor alpha = 1

    [Header("Look")]
    public Color visitedColor = new Color(0.9f, 1f, 0.9f, 0.35f);
    public float padding = 0.15f;                // inset so chips are smaller than a cell
    public float yOffset = 0.02f;                // float above ground a hair
    public float fadeInTime = 0.12f;

    Renderer[,] tiles;
    bool[,] visited;
    Transform root;

    void Start()
    {
        if (!generator) generator = GetComponent<MazeGenerator>();
        BuildOverlay();
    }

    [ContextMenu("Rebuild Overlay")]
    public void RebuildOverlay()
    {
        if (root) DestroyImmediate(root.gameObject);
        BuildOverlay();
    }

    void BuildOverlay()
    {
        if (!generator) { Debug.LogWarning("[VisitedTrailGrid] No MazeGenerator set."); return; }

        int w = Mathf.Max(1, generator.width);
        int h = Mathf.Max(1, generator.height);
        tiles = new Renderer[w, h];
        visited = new bool[w, h];

        root = new GameObject("VisitedOverlay").transform;
        root.SetParent(generator.transform, false);

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
                DestroyImmediate(go.GetComponent<Collider>());
                go.name = $"Tile_{x}_{y}";
                go.transform.SetParent(root, false);

                // position relative to the maze's origin
                Vector3 pos = new Vector3(x * generator.cellSize,
                                          generator.groundY + yOffset,
                                          y * generator.cellSize);
                go.transform.localPosition = pos;
                go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                float side = Mathf.Max(0.01f, generator.cellSize - padding * 2f);
                go.transform.localScale = new Vector3(side, side, 1f);

                var r = go.GetComponent<MeshRenderer>();
                if (tileMatTransparent) r.sharedMaterial = tileMatTransparent;

                // start fully transparent
                var mpb = new MaterialPropertyBlock();
                var c = visitedColor; c.a = 0f;
                mpb.SetColor("_BaseColor", c);
                r.SetPropertyBlock(mpb);

                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;

                tiles[x, y] = r;
            }
    }

    // === Public API ===
    public void MarkCellVisited(Vector2Int c)
    {
        if (tiles == null) return;
        if (c.x < 0 || c.y < 0 || c.x >= tiles.GetLength(0) || c.y >= tiles.GetLength(1)) return;
        if (visited[c.x, c.y]) return;

        visited[c.x, c.y] = true;
        StartCoroutine(FadeInTile(tiles[c.x, c.y]));
    }

    public bool MarkWorldVisited(Vector3 world)
    {
        if (!generator) return false;

        // convert world → local maze space
        Vector3 local = world - generator.transform.position;
        int cx = Mathf.FloorToInt(local.x / generator.cellSize);
        int cy = Mathf.FloorToInt(local.z / generator.cellSize);
        if (cx < 0 || cy < 0 || cx >= generator.width || cy >= generator.height) return false;

        MarkCellVisited(new Vector2Int(cx, cy));
        return true;
    }

    IEnumerator FadeInTile(Renderer r)
    {
        var mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        Color c = visitedColor; c.a = 0f;

        for (float t = 0; t < fadeInTime; t += Time.deltaTime)
        {
            float k = t / Mathf.Max(0.0001f, fadeInTime);
            var cc = Color.Lerp(new Color(visitedColor.r, visitedColor.g, visitedColor.b, 0f), visitedColor, k);
            mpb.SetColor("_BaseColor", cc);
            r.SetPropertyBlock(mpb);
            yield return null;
        }
        mpb.SetColor("_BaseColor", visitedColor);
        r.SetPropertyBlock(mpb);
    }
}
