using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EchoRevealFade : MonoBehaviour
{
    [Header("Echo Reveal")]
    public float radius = 7f;
    public float fadeIn = 0.08f;
    public float hold = 0.45f;
    public float fadeOut = 0.25f;

    [Header("UI Hook (optional)")]
    public MouseReticle reticleUI;

    [Header("Cooldown")]
    public float cooldown = 5f;

    [Header("Masks")]
    public LayerMask wallMask;                  // set to Walls
    public LayerMask groundMask = ~0;           // ONLY the ground layer ideally

    [Header("Camera")]
    public Camera cam;                          // falls back to Camera.main

    [Header("Visited / Marker (optional)")]
    public MazeGenerator generator;
    public VisitedTrailGrid trail;
    public bool dropMarker = true;
    public Material markerMat;                  // URP/Lit Transparent

    [Tooltip("Inset so the marker is smaller than the cell.")]
    public float markerPadding = 0.35f;
    public float markerY = 0.03f;

    [Header("Marker Colors")]
    public Color playerScanColor = new Color(0.5f, 1f, 0.5f, 0.6f); // Space
    public Color clickScanColor = new Color(0.5f, 0.7f, 1f, 0.6f); // Mouse

    bool canPing = true;
    readonly Dictionary<Renderer, Coroutine> running = new();

    Vector3 lastPingOriginWS;

    void Awake()
    {
        if (!cam) cam = Camera.main;
    }

    void Update()
    {
        if (!canPing) return;

        // Priority: mouse click first, then space
        if (Input.GetMouseButtonDown(0))
        {
            if (TryGetClickOrigin(out var origin))
                StartCoroutine(Ping(origin, clickScanColor));
            return;
        }

        if (Input.GetKeyDown(KeyCode.Space))
        {
            var origin = transform.position;
            StartCoroutine(Ping(origin, playerScanColor));
        }
    }

    bool TryGetClickOrigin(out Vector3 originWS)
    {
        if (!cam) cam = Camera.main;
        if (!cam) { originWS = default; return false; }

        Ray r = cam.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(r, out RaycastHit hit, 500f, groundMask, QueryTriggerInteraction.Ignore))
        {
            originWS = hit.point;
            // snap to groundY if you want it flat
            if (generator) originWS.y = generator.groundY;
            return true;
        }
        originWS = default;
        return false;
    }

    IEnumerator Ping(Vector3 originWS, Color markerColor)
    {
        canPing = false;
        if (reticleUI) reticleUI.SetCooldownProgress(0f);
        lastPingOriginWS = originWS;

        // reveal nearby walls by fading alpha up then down
        var hits = Physics.OverlapSphere(originWS, radius, wallMask, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.TryGetComponent<Renderer>(out var r))
            {
                if (running.TryGetValue(r, out var c)) StopCoroutine(c);
                running[r] = StartCoroutine(FadeRoutine(r));
            }
        }

        // stamp the tile + drop a colored marker
        StampVisited(originWS, markerColor);

        // cooldown with UI fill
        float t = 0f;
        while (t < cooldown)
        {
            t += Time.deltaTime;
            if (reticleUI) reticleUI.SetCooldownProgress(t / cooldown);
            yield return null;
        }
        if (reticleUI) reticleUI.SetCooldownProgress(1f);
        canPing = true;
    }

    IEnumerator FadeRoutine(Renderer r)
    {
        var mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);

        Color c = Color.white;
        if (r.sharedMaterial && r.sharedMaterial.HasProperty("_BaseColor"))
            c = r.sharedMaterial.GetColor("_BaseColor");
        float a0 = c.a;

        // Fade in
        for (float t = 0; t < fadeIn; t += Time.deltaTime)
        {
            float k = t / Mathf.Max(0.0001f, fadeIn);
            c.a = Mathf.Lerp(a0, 1f, k);
            mpb.SetColor("_BaseColor", c);
            r.SetPropertyBlock(mpb);
            yield return null;
        }
        c.a = 1f; mpb.SetColor("_BaseColor", c); r.SetPropertyBlock(mpb);

        // Hold
        yield return new WaitForSeconds(hold);

        // Fade out
        for (float t = 0; t < fadeOut; t += Time.deltaTime)
        {
            float k = t / Mathf.Max(0.0001f, fadeOut);
            c.a = Mathf.Lerp(1f, 0f, k);
            mpb.SetColor("_BaseColor", c);
            r.SetPropertyBlock(mpb);
            yield return null;
        }
        c.a = 0f; mpb.SetColor("_BaseColor", c); r.SetPropertyBlock(mpb);

        running.Remove(r);
    }

    void StampVisited(Vector3 worldPos, Color markerColor)
    {
        if (!generator) return;

        // world -> maze cell (maze origin is generator.transform.position)
        Vector3 local = worldPos - generator.transform.position;
        float cs = generator.cellSize;
        float half = cs * 0.5f;

        // +half so [−0.5*cs, +0.5*cs) maps to 0
        int cx = Mathf.FloorToInt((local.x + half) / cs);
        int cy = Mathf.FloorToInt((local.z + half) / cs);

        if (cx < 0 || cy < 0 || cx >= generator.width || cy >= generator.height) return;

        var cell = new Vector2Int(cx, cy);

        // mark overlay cell if present
        if (trail) trail.MarkCellVisited(cell);

        if (!dropMarker) return;

        // quad marker
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        Destroy(go.GetComponent<Collider>());
        go.name = $"ScanMarker_{cx}_{cy}";
        go.transform.position = generator.transform.position +
            new Vector3(cx * cs, generator.groundY + markerY, cy * cs);
        go.transform.rotation = Quaternion.Euler(90f, 0f, 0f);

        float side = Mathf.Max(0.01f, cs - markerPadding * 2f);
        go.transform.localScale = new Vector3(side, side, 1f);

        var r = go.GetComponent<MeshRenderer>();
        if (markerMat) r.sharedMaterial = markerMat;

        var mpb = new MaterialPropertyBlock();
        mpb.SetColor("_BaseColor", markerColor);
        r.SetPropertyBlock(mpb);

        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        r.receiveShadows = false;

        // ensure future raycasts ignore markers
        go.layer = LayerMask.NameToLayer("Ignore Raycast");
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.35f);
        Vector3 center = Application.isPlaying ? lastPingOriginWS : transform.position;
        Gizmos.DrawWireSphere(center, radius);
    }
}
