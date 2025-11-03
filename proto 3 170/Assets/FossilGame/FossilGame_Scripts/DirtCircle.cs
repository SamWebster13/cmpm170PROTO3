using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class DirtCircle : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
{
    [Header("Animation Settings")]
    [SerializeField] private float flyOffSpeed = 1000f;
    [SerializeField] private float flyOffDuration = 0.5f;
    [SerializeField] private float rotationSpeed = 720f;
    [SerializeField] private float scaleSpeed = 2f;
    
    private FossilCleaningGame gameManager;
    private RectTransform rectTransform;
    private Canvas canvas;
    private bool isDragging = false;
    private bool isFlying = false;
    private Vector2 flyDirection;
    private float flyTimer = 0f;
    private Vector3 originalScale;
    
    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvas = GetComponentInParent<Canvas>();
        originalScale = transform.localScale;
    }
    
    public void Initialize(FossilCleaningGame manager)
    {
        gameManager = manager;
    }
    
    public void OnPointerDown(PointerEventData eventData)
    {
        if (isFlying) return;
        
        isDragging = true;
        
        // Add a slight scale up effect on click
        transform.localScale = originalScale * 1.1f;
    }
    
    public void OnDrag(PointerEventData eventData)
    {
        if (isFlying) return;
        
        isDragging = true;
        
        // Move with mouse/touch
        if (rectTransform != null && canvas != null)
        {
            Vector2 localPoint;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                eventData.position,
                canvas.worldCamera,
                out localPoint
            );
            
            rectTransform.localPosition = localPoint;
        }
    }
    
    public void OnPointerUp(PointerEventData eventData)
    {
        if (isFlying) return;
        
        if (isDragging)
        {
            // Start the fly-off animation
            StartFlyOff();
        }
        
        isDragging = false;
    }
    
    void StartFlyOff()
    {
        isFlying = true;
        
        // Random direction for flying off
        float angle = Random.Range(0f, 360f);
        flyDirection = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
        
        // Play a satisfying sound if you have one
        // AudioSource.PlayClipAtPoint(flyOffSound, Vector3.zero);
    }
    
    void Update()
    {
        if (isFlying)
        {
            AnimateFlyOff();
        }
    }
    
    void AnimateFlyOff()
    {
        flyTimer += Time.deltaTime;
        float progress = flyTimer / flyOffDuration;
        
        // Move in the fly direction
        rectTransform.anchoredPosition += flyDirection * flyOffSpeed * Time.deltaTime;
        
        // Rotate
        rectTransform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
        
        // Scale down
        float scale = Mathf.Lerp(1f, 0f, progress * scaleSpeed);
        transform.localScale = originalScale * scale;
        
        // Fade out
        Image image = GetComponent<Image>();
        if (image != null)
        {
            Color color = image.color;
            color.a = Mathf.Lerp(1f, 0f, progress);
            image.color = color;
        }
        
        // Check if animation is complete
        if (progress >= 1f)
        {
            // Notify game manager
            if (gameManager != null)
            {
                gameManager.OnDirtRemoved(this);
            }
            
            // Destroy this object
            Destroy(gameObject);
        }
    }
}

