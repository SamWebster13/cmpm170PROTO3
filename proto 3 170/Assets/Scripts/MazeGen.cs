using System.Collections.Generic;
using UnityEngine;

public class MazeGenerator : MonoBehaviour
{
    [Header("Grid (cells)")]
    public int width = 12;
    public int height = 12;
    public float cellSize = 2f;

    [Header("Walls")]
    public float wallThickness = 0.25f;
    public float wallHeight = 2f;
    public Material wallMaterialTransparent; // URP/Lit Transparent
    public string wallsLayerName = "Walls";
    public bool visibleOnStart = false;     // start invisible for echo reveal

    [Header("Ground")]
    public bool createGround = true;
    public Material groundMaterial;          // optional
    public string groundLayerName = "Default";
    public float groundY = 0f;               // Y position for ground

    // NOTE: Attributes can't go on a type. Keep enum clean:
    public enum EndpointMode { Corners, RandomOpposite, RandomAny }

    [Header("Start / End")]
    public EndpointMode endpointMode = EndpointMode.Corners;
    public bool carveEntranceExit = true;    // knocks out bordering walls at start/end
    public Transform playerToPlace;          // optional: move player to Start
    public GameObject goalPrefab;            // optional: spawn a goal at End (must have trigger)

    [Header("Build")]
    public bool generateOnStart = true;
    public int seed = 0;                     // 0 = random

    Transform wallsRoot;
    Transform groundRoot;
    System.Random rng;
    int wallsLayer;
    int groundLayer;

    struct Cell { public bool visited; public bool n, s, e, w; } // true = wall present

    Vector2Int startCell, endCell;

    void Start()
    {
        if (generateOnStart) Generate();
    }

    [ContextMenu("Generate Maze")]
    public void Generate()
    {
        if (wallsRoot != null) DestroyImmediate(wallsRoot.gameObject);
        if (groundRoot != null) DestroyImmediate(groundRoot.gameObject);

        wallsRoot = new GameObject("MazeWalls").transform;
        wallsRoot.SetParent(transform, false);

        groundRoot = new GameObject("MazeGround").transform;
        groundRoot.SetParent(transform, false);

        rng = (seed == 0) ? new System.Random() : new System.Random(seed);
        wallsLayer = LayerMask.NameToLayer(wallsLayerName);
        if (wallsLayer < 0) { Debug.LogWarning($"Layer '{wallsLayerName}' not found. Using Default."); wallsLayer = 0; }
        groundLayer = LayerMask.NameToLayer(groundLayerName);
        if (groundLayer < 0) groundLayer = 0;

        var cells = new Cell[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                cells[x, y] = new Cell { visited = false, n = true, s = true, e = true, w = true };

        CarveMaze(cells);
        PickEndpoints(out startCell, out endCell);

        if (carveEntranceExit)
        {
            if (startCell.x == 0) cells[startCell.x, startCell.y].w = false;
            else if (startCell.x == width - 1) cells[startCell.x, startCell.y].e = false;
            else if (startCell.y == 0) cells[startCell.x, startCell.y].s = false;
            else if (startCell.y == height - 1) cells[startCell.x, startCell.y].n = false;

            if (endCell.x == 0) cells[endCell.x, endCell.y].w = false;
            else if (endCell.x == width - 1) cells[endCell.x, endCell.y].e = false;
            else if (endCell.y == 0) cells[endCell.x, endCell.y].s = false;
            else if (endCell.y == height - 1) cells[endCell.x, endCell.y].n = false;
        }

        BuildWalls(cells);

        if (createGround) BuildGround();

        if (!visibleOnStart) SetAllWallsAlpha(0f);
        else SetAllWallsAlpha(1f);

        if (playerToPlace)
        {
            Vector3 startPos = CellCenterWorld(startCell) + new Vector3(0f, 0.05f, 0f);
            playerToPlace.position = startPos;
        }

        if (goalPrefab)
        {
            Vector3 endPos = CellCenterWorld(endCell);
            var goal = Instantiate(goalPrefab, endPos, Quaternion.identity, transform);
            goal.transform.position = new Vector3(endPos.x, groundY, endPos.z);
        }
    }

    void CarveMaze(Cell[,] cells)
    {
        int w = cells.GetLength(0);
        int h = cells.GetLength(1);
        var stack = new Stack<Vector2Int>();
        var start = new Vector2Int(rng.Next(w), rng.Next(h));
        cells[start.x, start.y].visited = true;
        stack.Push(start);

        Vector2Int[] dirs = {
        new Vector2Int(0, 1),   // N
        new Vector2Int(0,-1),   // S
        new Vector2Int(1, 0),   // E
        new Vector2Int(-1,0)    // W
    };

        while (stack.Count > 0)
        {
            var cur = stack.Peek();

            // collect unvisited neighbors
            var neighbors = new List<Vector2Int>();
            foreach (var d in dirs)
            {
                int nx = cur.x + d.x, ny = cur.y + d.y;
                if (nx >= 0 && ny >= 0 && nx < w && ny < h && !cells[nx, ny].visited)
                    neighbors.Add(new Vector2Int(nx, ny));
            }

            if (neighbors.Count == 0)
            {
                stack.Pop();
                continue;
            }

            var next = neighbors[rng.Next(neighbors.Count)];

            // knock down the wall between cur and next
            if (next.x > cur.x) { cells[cur.x, cur.y].e = false; cells[next.x, next.y].w = false; }  
            else if (next.x < cur.x) { cells[cur.x, cur.y].w = false; cells[next.x, next.y].e = false; } 
            else if (next.y > cur.y) { cells[cur.x, cur.y].n = false; cells[next.x, next.y].s = false; } 
            else if (next.y < cur.y) { cells[cur.x, cur.y].s = false; cells[next.x, next.y].n = false; } 

            cells[next.x, next.y].visited = true;
            stack.Push(next);
        }
    }


    void PickEndpoints(out Vector2Int start, out Vector2Int end)
    {
        switch (endpointMode)
        {
            case EndpointMode.Corners:
                start = new Vector2Int(0, 0);
                end = new Vector2Int(width - 1, height - 1);
                break;
            case EndpointMode.RandomOpposite:
                Vector2Int[] corners = {
                    new Vector2Int(0,0),
                    new Vector2Int(width-1,0),
                    new Vector2Int(0,height-1),
                    new Vector2Int(width-1,height-1)
                };
                start = corners[rng.Next(corners.Length)];
                end = new Vector2Int(width - 1 - start.x, height - 1 - start.y);
                break;
            default: // RandomAny
                start = new Vector2Int(rng.Next(width), rng.Next(height));
                do { end = new Vector2Int(rng.Next(width), rng.Next(height)); } while (end == start);
                break;
        }
    }

    void BuildWalls(Cell[,] cells)
    {
        int w = cells.GetLength(0);
        int h = cells.GetLength(1);
        float cs = cellSize;
        float half = cs * 0.5f;
        float t = wallThickness;

        GameObject MakeWall(Vector3 center, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = "Wall";
            go.transform.SetParent(wallsRoot, false);
            go.transform.localPosition = center;
            go.transform.localScale = scale;

            var autoCol = go.GetComponent<Collider>(); if (autoCol) DestroyImmediate(autoCol);
            var mr = go.GetComponent<MeshRenderer>();
            if (wallMaterialTransparent) mr.sharedMaterial = wallMaterialTransparent;
            go.layer = wallsLayer;

            var bc = go.AddComponent<BoxCollider>();
            bc.size = Vector3.one;

            return go;
        }

        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                Vector3 cellBase = new Vector3(x * cs, groundY + wallHeight * 0.5f, y * cs);
                if (cells[x, y].e)
                {
                    Vector3 center = cellBase + new Vector3(half, 0f, 0f);
                    Vector3 scale = new Vector3(t, wallHeight, cs + t);
                    MakeWall(center, scale);
                }
                if (cells[x, y].n)
                {
                    Vector3 center = cellBase + new Vector3(0f, 0f, half);
                    Vector3 scale = new Vector3(cs + t, wallHeight, t);
                    MakeWall(center, scale);
                }
            }

        // Borders
        for (int y = 0; y < h; y++) { MakeWall(new Vector3(-half, groundY + wallHeight * 0.5f, y * cs), new Vector3(t, wallHeight, cs + t)); }
        for (int x = 0; x < w; x++) { MakeWall(new Vector3(x * cs, groundY + wallHeight * 0.5f, -half), new Vector3(cs + t, wallHeight, t)); }
        for (int y = 0; y < h; y++) { MakeWall(new Vector3(w * cs - half, groundY + wallHeight * 0.5f, y * cs), new Vector3(t, wallHeight, cs + t)); }
        for (int x = 0; x < w; x++) { MakeWall(new Vector3(x * cs, groundY + wallHeight * 0.5f, h * cs - half), new Vector3(cs + t, wallHeight, t)); }
    }

    void BuildGround()
    {
        float totalW = width * cellSize;
        float totalH = height * cellSize;

        var groundGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
        groundGO.name = "Ground";
        groundGO.transform.SetParent(groundRoot, false);
        groundGO.transform.localScale = new Vector3(totalW / 10f, 1f, totalH / 10f); // Unity Plane = 10x10
        groundGO.transform.position = new Vector3((width - 1) * cellSize * 0.5f, groundY, (height - 1) * cellSize * 0.5f);
        groundGO.layer = groundLayer;

        if (groundMaterial)
            groundGO.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;
    }

    Vector3 CellCenterWorld(Vector2Int c) => new Vector3(c.x * cellSize, groundY, c.y * cellSize);

    void SetAllWallsAlpha(float a)
    {
        var rList = wallsRoot.GetComponentsInChildren<MeshRenderer>();
        var mpb = new MaterialPropertyBlock();
        foreach (var r in rList)
        {
            r.GetPropertyBlock(mpb);
            Color c = Color.white;
            if (r.sharedMaterial && r.sharedMaterial.HasProperty("_BaseColor"))
                c = r.sharedMaterial.GetColor("_BaseColor");
            c.a = Mathf.Clamp01(a);
            mpb.SetColor("_BaseColor", c);
            r.SetPropertyBlock(mpb);
            r.enabled = true;
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.15f);
        Gizmos.DrawWireCube(
            transform.position + new Vector3((width - 1) * cellSize * 0.5f, groundY, (height - 1) * cellSize * 0.5f),
            new Vector3(width * cellSize, 0.1f, height * cellSize)
        );

        Gizmos.color = new Color(0.4f, 1f, 0.4f, 0.9f);
        Gizmos.DrawSphere(transform.position + CellCenterWorld(new Vector2Int(Mathf.Clamp(startCell.x, 0, width - 1), Mathf.Clamp(startCell.y, 0, height - 1))), 0.2f);
        Gizmos.color = new Color(1f, 0.4f, 0.4f, 0.9f);
        Gizmos.DrawSphere(transform.position + CellCenterWorld(new Vector2Int(Mathf.Clamp(endCell.x, 0, width - 1), Mathf.Clamp(endCell.y, 0, height - 1))), 0.2f);
    }
}
