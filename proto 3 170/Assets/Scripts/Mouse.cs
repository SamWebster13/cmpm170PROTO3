using UnityEngine;
using UnityEngine.UI;

public class MouseReticle : MonoBehaviour
{
    public RectTransform reticle;

    [Header("Behavior")]
    public float baseSize = 12f;
    public float clickSize = 24f;
    public float followSmooth = 0f;
    public bool followMouseWhenUnlocked = false;

    // --- NEW: center-out cooldown fill ---
    [Header("Cooldown Fill (center→edge)")]
    public RectTransform cooldownFill;       // drag the CooldownFill child here
    public Color fillReadyGrey = new Color(0.75f, 0.75f, 0.75f, 1f);
    public Color fillEmptyWhite = Color.white;
    [Range(0f, 1f)] public float cooldownProgress = 1f; // 1 = ready

    Vector3 vel;
    float currentSize;

    void Awake()
    {
        currentSize = baseSize;
        ApplyCooldownVisual();
    }

    void Update()
    {
        bool isDown =
#if ENABLE_INPUT_SYSTEM
            UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.leftButton.isPressed :
#endif
            Input.GetMouseButton(0);

        float targetSize = isDown ? clickSize : baseSize;
        currentSize = Mathf.Lerp(currentSize, targetSize, 20f * Time.deltaTime);

        if (!reticle) return;

        Vector2 targetPos;
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        if (!locked && followMouseWhenUnlocked)
        {
#if ENABLE_INPUT_SYSTEM
            targetPos =
                UnityEngine.InputSystem.Mouse.current != null ? UnityEngine.InputSystem.Mouse.current.position.ReadValue() :
                (Vector2)Input.mousePosition;
#else
            targetPos = Input.mousePosition;
#endif
        }
        else
        {
            targetPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        }

        if (followSmooth <= 0f)
            reticle.position = targetPos;
        else
            reticle.position = Vector3.SmoothDamp(reticle.position, targetPos, ref vel, 1f / followSmooth);

        reticle.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, currentSize);
        reticle.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, currentSize);
    }

    // Call this from EchoRevealFade
    public void SetCooldownProgress(float normalized)
    {
        cooldownProgress = Mathf.Clamp01(normalized);
        ApplyCooldownVisual();
    }

    void ApplyCooldownVisual()
    {
        if (!cooldownFill) return;

        // Scale inner circle from center (0 → 1)
        float s = Mathf.Lerp(0.0f, 1.0f, cooldownProgress);
        cooldownFill.localScale = new Vector3(s, s, 1f);

        // Tint from white (just scanned) → grey (ready)
        var img = cooldownFill.GetComponent<Image>();
        if (img) img.color = Color.Lerp(fillEmptyWhite, fillReadyGrey, cooldownProgress);
    }
}
