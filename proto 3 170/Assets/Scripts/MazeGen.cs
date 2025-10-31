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

    [Header("Grid Lines")]
    public bool drawGrid = true;
    public Color gridColor = new Color(1f, 1f, 1f, 0.18f);
    public float gridThickness = 0.03f;   // world units
    public float gridY = 0.011f;          // tiny lift over ground to avoid z-fight
    public Material gridMaterial;         // URP/Lit (Transparent). If null we’ll create one at runtime.


    [Header("Exit Trail (debug)")]
    public bool showExitTrail = false;                 // toggle in Inspector
    public Material exitTrailMaterial;                 // URP/Lit Transparent
    public Color exitTrailColor = new Color(1, 0, 0, 0.45f);
    [Tooltip("Inset so the trail is thinner than the cell.")]
    public float exitTrailPadding = 0.35f;

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

    // --------------------- Rooms ---------------------
    [Header("Rooms")]
    [Tooltip("3x3 makes a nice small room for spawn/goal.")]
    public Vector2Int spawnRoomSize = new Vector2Int(3, 3);
    public Vector2Int goalRoomSize = new Vector2Int(3, 3);

    [Tooltip("How many additional rooms to sprinkle in.")]
    public int extraRooms = 2;
    public Vector2Int extraRoomMinSize = new Vector2Int(2, 2);
    public Vector2Int extraRoomMaxSize = new Vector2Int(4, 4);

    [Tooltip("Cells of padding that must remain empty around any room.")]
    public int roomPadding = 1;

    [Tooltip("How many doorways to punch from each room into the corridors.")]
    public Vector2Int doorsPerRoom = new Vector2Int(1, 2);
    // -------------------------------------------------

    Transform wallsRoot;
    Transform groundRoot;
    Transform exitTrailRoot;
    System.Random rng;
    int wallsLayer;
    int groundLayer;

    struct Cell { public bool visited; public bool n, s, e, w; public bool isRoom; } // true = wall present
    Cell[,] _cells; // carved grid for path/queries

    Vector2Int startCell, endCell;

    // Room record (rect in grid)
    struct Room { public int x, y, w, h; }

    List<Room> _rooms = new List<Room>();
    Room _spawnRoom, _goalRoom;

    void Start()
    {
        if (generateOnStart) Generate();
    }

    [ContextMenu("Generate Maze")]
    public void Generate()
    {
        if (wallsRoot != null) DestroyImmediate(wallsRoot.gameObject);
        if (groundRoot != null) DestroyImmediate(groundRoot.gameObject);
        if (exitTrailRoot != null) DestroyImmediate(exitTrailRoot.gameObject);

        wallsRoot = new GameObject("MazeWalls").transform;
        wallsRoot.SetParent(transform, false);

        groundRoot = new GameObject("MazeGround").transform;
        groundRoot.SetParent(transform, false);

        rng = (seed == 0) ? new System.Random() : new System.Random(seed);
        wallsLayer = LayerMask.NameToLayer(wallsLayerName);
        if (wallsLayer < 0) { Debug.LogWarning($"Layer '{wallsLayerName}' not found. Using Default."); wallsLayer = 0; }
        groundLayer = LayerMask.NameToLayer(groundLayerName);
        if (groundLayer < 0) groundLayer = 0;

        // Init full grid with all walls
        var cells = new Cell[width, height];
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                cells[x, y] = new Cell { visited = false, n = true, s = true, e = true, w = true, isRoom = false };

        _rooms.Clear();

        // 1) Place rooms (carve their interiors, keep perimeters closed for door punching later)
        PlaceRooms(cells);

        // 2) Carve the maze around rooms
        CarveMaze(cells);

        _cells = cells; // keep carved maze

        // 3) Choose start/end cells (center of spawn/goal rooms)
        PickEndpoints(out startCell, out endCell);

        // 4) Optional: carve entrances on outside border (unchanged)
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

        // 5) Build geometry
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

        if (showExitTrail) BuildExitTrail();
    }

    // -------------------- ROOM LOGIC --------------------
    void PlaceRooms(Cell[,] cells)
    {
        // Spawn room near a corner; goal room near opposite area; plus extras
        _spawnRoom = TryPlaceSpecificRoom(cells, spawnRoomSize, preferCorner: 0); // 0 = SW corner
        _goalRoom = TryPlaceSpecificRoom(cells, goalRoomSize, preferCorner: 3); // 3 = NE corner

        // Extras
        for (int i = 0; i < extraRooms; i++)
        {
            var size = new Vector2Int(
                Mathf.Clamp(RandomRange(extraRoomMinSize.x, extraRoomMaxSize.x), 1, width),
                Mathf.Clamp(RandomRange(extraRoomMinSize.y, extraRoomMaxSize.y), 1, height)
            );
            TryPlaceRandomRoom(cells, size);
        }

        // Punch doors from each room into the maze (1..2 per room by default)
        foreach (var r in _rooms)
            PunchRoomDoors(cells, r, RandomRange(doorsPerRoom.x, doorsPerRoom.y));
    }

    Room TryPlaceSpecificRoom(Cell[,] cells, Vector2Int size, int preferCorner)
    {
        // preferCorner: 0=SW,1=SE,2=NW,3=NE (just a bias; falls back if overlaps)
        int w = Mathf.Clamp(size.x, 1, Mathf.Max(1, width - 2));
        int h = Mathf.Clamp(size.y, 1, Mathf.Max(1, height - 2));
        Vector2Int posGuess = preferCorner switch
        {
            0 => new Vector2Int(roomPadding, roomPadding),
            1 => new Vector2Int(width - w - roomPadding, roomPadding),
            2 => new Vector2Int(roomPadding, height - h - roomPadding),
            _ => new Vector2Int(width - w - roomPadding, height - h - roomPadding)
        };
        if (!WouldOverlap(posGuess.x, posGuess.y, w, h))
            return CarveRoom(cells, posGuess.x, posGuess.y, w, h);

        // fallback: random tries
        for (int t = 0; t < 80; t++)
        {
            int x = rng.Next(roomPadding, Mathf.Max(roomPadding, width - w - roomPadding + 1));
            int y = rng.Next(roomPadding, Mathf.Max(roomPadding, height - h - roomPadding + 1));
            if (!WouldOverlap(x, y, w, h))
                return CarveRoom(cells, x, y, w, h);
        }
        // if all else fails, place in bounds even if tight
        int fx = Mathf.Clamp(posGuess.x, 0, width - w);
        int fy = Mathf.Clamp(posGuess.y, 0, height - h);
        return CarveRoom(cells, fx, fy, w, h);
    }

    void TryPlaceRandomRoom(Cell[,] cells, Vector2Int size)
    {
        int w = Mathf.Clamp(size.x, 1, width);
        int h = Mathf.Clamp(size.y, 1, height);
        for (int t = 0; t < 60; t++)
        {
            int x = rng.Next(roomPadding, Mathf.Max(roomPadding, width - w - roomPadding + 1));
            int y = rng.Next(roomPadding, Mathf.Max(roomPadding, height - h - roomPadding + 1));
            if (!WouldOverlap(x, y, w, h))
            {
                CarveRoom(cells, x, y, w, h);
                return;
            }
        }
    }

    bool WouldOverlap(int x, int y, int w, int h)
    {
        // prevent overlap incl. padding
        RectInt newRect = new RectInt(x - roomPadding, y - roomPadding, w + roomPadding * 2, h + roomPadding * 2);
        foreach (var r in _rooms)
        {
            RectInt rr = new RectInt(r.x - roomPadding, r.y - roomPadding, r.w + roomPadding * 2, r.h + roomPadding * 2);
            if (newRect.Overlaps(rr)) return true;
        }
        // stay inside bounds
        if (x < 0 || y < 0 || x + w > width || y + h > height) return true;
        return false;
    }

    Room CarveRoom(Cell[,] cells, int x, int y, int w, int h)
    {
        // Mark cells as room + remove interior walls
        for (int ix = x; ix < x + w; ix++)
            for (int iy = y; iy < y + h; iy++)
            {
                cells[ix, iy].isRoom = true;
                cells[ix, iy].visited = true; // so maze backtracker doesn't carve through interior
                                              // remove interior dividing walls (east and north inside the room)
                if (ix < x + w - 1) { cells[ix, iy].e = false; cells[ix + 1, iy].w = false; }
                if (iy < y + h - 1) { cells[ix, iy].n = false; cells[ix, iy + 1].s = false; }
            }

        var room = new Room { x = x, y = y, w = w, h = h };
        _rooms.Add(room);
        return room;
    }

    void PunchRoomDoors(Cell[,] cells, Room r, int doorCount)
    {
        doorCount = Mathf.Max(1, doorCount);
        // Collect all perimeter edges that touch non-room cells
        var candidates = new List<(Vector2Int a, Vector2Int b)>();

        // west & east edges
        for (int iy = r.y; iy < r.y + r.h; iy++)
        {
            int xw = r.x;
            if (xw - 1 >= 0 && !cells[xw - 1, iy].isRoom)
                candidates.Add((new Vector2Int(xw - 1, iy), new Vector2Int(xw, iy))); // open wall between these
            int xe = r.x + r.w - 1;
            if (xe + 1 < width && !cells[xe + 1, iy].isRoom)
                candidates.Add((new Vector2Int(xe, iy), new Vector2Int(xe + 1, iy)));
        }
        // south & north edges
        for (int ix = r.x; ix < r.x + r.w; ix++)
        {
            int ys = r.y;
            if (ys - 1 >= 0 && !cells[ix, ys - 1].isRoom)
                candidates.Add((new Vector2Int(ix, ys - 1), new Vector2Int(ix, ys)));
            int yn = r.y + r.h - 1;
            if (yn + 1 < height && !cells[ix, yn + 1].isRoom)
                candidates.Add((new Vector2Int(ix, yn), new Vector2Int(ix, yn + 1)));
        }

        // Shuffle
        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }

        int made = 0;
        foreach (var edge in candidates)
        {
            var a = edge.a; var b = edge.b;
            Vector2Int d = b - a;
            if (d == Vector2Int.right) { cells[a.x, a.y].e = false; cells[b.x, b.y].w = false; }
            else if (d == Vector2Int.left) { cells[a.x, a.y].w = false; cells[b.x, b.y].e = false; }
            else if (d == Vector2Int.up) { cells[a.x, a.y].n = false; cells[b.x, b.y].s = false; }
            else if (d == Vector2Int.down) { cells[a.x, a.y].s = false; cells[b.x, b.y].n = false; }
            made++;
            if (made >= doorCount) break;
        }
    }
    // ---------------------------------------------------

    void CarveMaze(Cell[,] cells)
    {
        int w = cells.GetLength(0);
        int h = cells.GetLength(1);
        var stack = new Stack<Vector2Int>();

        // Find a random non-room start
        Vector2Int start;
        int tries = 0;
        do { start = new Vector2Int(rng.Next(w), rng.Next(h)); } while (cells[start.x, start.y].isRoom && ++tries < 200);
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

            // collect unvisited neighbors (ignore room interiors—they're already carved)
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

            // knock down wall between cur and next
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
        // If rooms exist, pick centers of spawn & goal rooms
        if (_rooms.Count > 0)
        {
            start = new Vector2Int(_spawnRoom.x + _spawnRoom.w / 2, _spawnRoom.y + _spawnRoom.h / 2);
            end = new Vector2Int(_goalRoom.x + _goalRoom.w / 2, _goalRoom.y + _goalRoom.h / 2);
            start.x = Mathf.Clamp(start.x, 0, width - 1);
            start.y = Mathf.Clamp(start.y, 0, height - 1);
            end.x = Mathf.Clamp(end.x, 0, width - 1);
            end.y = Mathf.Clamp(end.y, 0, height - 1);
            return;
        }

        // Fallback: previous modes
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
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
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

        // --- Ground plane ---
        var groundGO = GameObject.CreatePrimitive(PrimitiveType.Plane);
        groundGO.name = "Ground";
        groundGO.transform.SetParent(groundRoot, false);
        // Unity Plane is 10x10 in local units; scale to our maze size
        groundGO.transform.localScale = new Vector3(totalW / 10f, 1f, totalH / 10f);
        groundGO.transform.position = new Vector3((width - 1) * cellSize * 0.5f, groundY, (height - 1) * cellSize * 0.5f);
        groundGO.layer = groundLayer;

        if (groundMaterial)
            groundGO.GetComponent<MeshRenderer>().sharedMaterial = groundMaterial;

        // --- Grid overlay (thin quads) ---
        if (!drawGrid) return;

        // Parent holder
        var gridRoot = new GameObject("GridOverlay").transform;
        gridRoot.SetParent(groundRoot, false);

        // Make/ensure a transparent unlit material if none was assigned
        Material mat = gridMaterial;
        if (!mat)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            mat = new Material(shader);
            // Transparent rendering
            mat.SetFloat("_Surface", 1f);  // 0=Opaque, 1=Transparent (URP Unlit)
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        // Helper to spawn a single line as a Quad
        GameObject MakeLine(Vector3 centerLocal, float widthLocal, float heightLocal)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(q.GetComponent<Collider>());
            q.transform.SetParent(gridRoot, false);
            q.transform.localPosition = centerLocal;
            q.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // lay flat on XZ
            q.transform.localScale = new Vector3(widthLocal, heightLocal, 1f);

            var mr = q.GetComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", gridColor);
            mr.SetPropertyBlock(mpb);
            return q;
        }

        float half = cellSize * 0.5f;

        // Vertical lines (along Z): x at cell boundaries, z spans full height
        for (int x = 0; x <= width; x++)
        {
            float lineX = (x * cellSize) - half; // -half, +0.5*cs, ..., (w-0.5)*cs
            Vector3 pos = new Vector3(lineX, groundY + gridY, (height - 1) * cellSize * 0.5f);
            MakeLine(pos, gridThickness, totalH);
        }

        // Horizontal lines (along X): z at cell boundaries, x spans full width
        for (int y = 0; y <= height; y++)
        {
            float lineZ = (y * cellSize) - half;
            Vector3 pos = new Vector3((width - 1) * cellSize * 0.5f, groundY + gridY, lineZ);
            MakeLine(pos, totalW, gridThickness);
        }
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

    // ===== Exit Trail =====
    void BuildExitTrail()
    {
        if (exitTrailRoot != null) DestroyImmediate(exitTrailRoot.gameObject);
        exitTrailRoot = new GameObject("ExitTrail").transform;
        exitTrailRoot.SetParent(transform, false);

        var path = FindPath(startCell, endCell);
        if (path == null || path.Count == 0) return;

        foreach (var c in path)
        {
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(q.GetComponent<Collider>());
            q.name = $"Trail_{c.x}_{c.y}";
            q.transform.SetParent(exitTrailRoot, false);

            Vector3 pos = new Vector3(c.x * cellSize, groundY + 0.03f, c.y * cellSize);
            q.transform.position = pos;
            q.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            float side = Mathf.Max(0.01f, cellSize - exitTrailPadding * 2f);
            q.transform.localScale = new Vector3(side, side, 1f);

            var mr = q.GetComponent<MeshRenderer>();
            if (exitTrailMaterial) mr.sharedMaterial = exitTrailMaterial;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;

            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_BaseColor", exitTrailColor);
            mr.SetPropertyBlock(mpb);
        }
    }

    List<Vector2Int> FindPath(Vector2Int start, Vector2Int goal)
    {
        var q = new Queue<Vector2Int>();
        var cameFrom = new Dictionary<Vector2Int, Vector2Int>();
        var seen = new HashSet<Vector2Int>();

        q.Enqueue(start);
        seen.Add(start);

        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            if (cur == goal) break;

            foreach (var n in Neighbors(cur))
            {
                if (seen.Contains(n)) continue;
                seen.Add(n);
                cameFrom[n] = cur;
                q.Enqueue(n);
            }
        }

        if (start != goal && !cameFrom.ContainsKey(goal)) return null;

        var path = new List<Vector2Int>();
        var p = goal;
        path.Add(p);
        while (p != start)
        {
            p = cameFrom[p];
            path.Add(p);
        }
        path.Reverse();
        return path;
    }

    IEnumerable<Vector2Int> Neighbors(Vector2Int c)
    {
        if (!_cells[c.x, c.y].n && c.y + 1 < height) yield return new Vector2Int(c.x, c.y + 1);
        if (!_cells[c.x, c.y].s && c.y - 1 >= 0) yield return new Vector2Int(c.x, c.y - 1);
        if (!_cells[c.x, c.y].e && c.x + 1 < width) yield return new Vector2Int(c.x + 1, c.y);
        if (!_cells[c.x, c.y].w && c.x - 1 >= 0) yield return new Vector2Int(c.x - 1, c.y);
    }

    // ===== Gizmos =====
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

    // helpers
    int RandomRange(int a, int b) => (a <= b) ? rng.Next(a, b + 1) : rng.Next(b, a + 1);
}
