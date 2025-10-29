using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EchoRevealFade : MonoBehaviour
{
    public float radius = 7f;
    public float fadeIn = 0.08f;
    public float hold = 0.45f;
    public float fadeOut = 0.25f;
    public float cooldown = 0.6f;
    public LayerMask wallMask;

    bool canPing = true;
    readonly Dictionary<Renderer, Coroutine> running = new();

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && canPing)
            StartCoroutine(Ping());
    }

    IEnumerator Ping()
    {
        canPing = false;

        var hits = Physics.OverlapSphere(transform.position, radius, wallMask, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.TryGetComponent<Renderer>(out var r))
            {
                // (Re)start a fade routine for this renderer
                if (running.TryGetValue(r, out var c)) StopCoroutine(c);
                running[r] = StartCoroutine(FadeRoutine(r));
            }
        }

        yield return new WaitForSeconds(cooldown);
        canPing = true;
    }

    IEnumerator FadeRoutine(Renderer r)
    {
        var mpb = new MaterialPropertyBlock();
        r.GetPropertyBlock(mpb);
        Color c = mpb.GetColor("_BaseColor"); // URP Lit uses _BaseColor
        float a0 = c.a;

        // Fade in
        for (float t = 0; t < fadeIn; t += Time.deltaTime)
        {
            float k = t / fadeIn;
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
            float k = t / fadeOut;
            c.a = Mathf.Lerp(1f, 0f, k);
            mpb.SetColor("_BaseColor", c);
            r.SetPropertyBlock(mpb);
            yield return null;
        }
        c.a = 0f; mpb.SetColor("_BaseColor", c); r.SetPropertyBlock(mpb);

        running.Remove(r);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.35f);
        Gizmos.DrawWireSphere(transform.position, radius);
    }
}
