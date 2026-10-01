using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems; // Required for UI event interfaces
using UnityEngine.UI;

// Adding these interfaces allows the script to automatically listen to UI/EventSystem interactions
public class ObjectHandler : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    public UnityEvent OnLeftClicked;
    public UnityEvent OnRightClicked;
    public UnityEvent OnHovered;
    public UnityEvent OnUnhovered;
    
    [SerializeField] Color outlineColor = Color.white;
    [SerializeField] Color defaultColor = Color.black;
    [SerializeField] Material material;
    public bool isUIElement = true;

    bool initialized;

    // Initialize in Awake (not Start) so the material clone + listeners are set up
    // exactly once, deterministically, BEFORE any OnEnable/SetOutline runs. Start is
    // unreliable for pooled objects (it can be deferred past the first activation),
    // which caused the outline to fail on some respawned pieces.
    void Awake()
    {
        Initialize();
    }

    void OnEnable()
    {
        // Safety net in case this is the first activation of an object that was
        // instantiated inactive (Awake still runs first, so this is guarded).
        Initialize();

        // Reset to hidden state every time the object is (re)activated from a pool.
        HideOutline();
    }

    void Initialize()
    {
        if (initialized) return;
        initialized = true;

        InitializeMaterial();

        OnHovered.AddListener(ShowOutline);
        OnUnhovered.AddListener(HideOutline);
    }

    void InitializeMaterial()
    {
        Material sourceMaterial = material;

        if (isUIElement)
        {
            Image image = GetComponentInChildren<Image>();
            if (sourceMaterial == null && image != null)
                sourceMaterial = image.material;

            if (sourceMaterial == null)
                return;

            material = new Material(sourceMaterial);
            if (image != null)
                image.material = material;
        }
        else
        {
            Renderer renderer = GetComponentInChildren<Renderer>();
            if (sourceMaterial == null && renderer != null)
                sourceMaterial = renderer.material;

            if (sourceMaterial == null)
                return;

            material = new Material(sourceMaterial);
            if (renderer != null)
                renderer.material = renderer.sharedMaterial = material;
        }
    }

    // ==========================================
    // METHOD A: FOR 2D/3D WORLD OBJECTS (Physics)
    // ==========================================
    private void OnMouseEnter()
    {
        if (!isUIElement) OnHovered?.Invoke();
    }

    private void OnMouseExit()
    {
        if (!isUIElement) OnUnhovered?.Invoke();
    }

    private void OnMouseDown()
    {
        if (!isUIElement)
        {
            if (Input.GetMouseButtonDown(0)) OnLeftClicked?.Invoke();
            else if (Input.GetMouseButtonDown(1)) OnRightClicked?.Invoke();
        }
    }

    // ==========================================
    // METHOD B: FOR UI ELEMENTS (Canvas)
    // ==========================================
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (isUIElement) OnHovered?.Invoke();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (isUIElement) OnUnhovered?.Invoke();
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (isUIElement)
        {
            if (eventData.button == PointerEventData.InputButton.Left) OnLeftClicked?.Invoke();
            else if (eventData.button == PointerEventData.InputButton.Right) OnRightClicked?.Invoke();
        }
    }

    // ==========================================
    // OUTLINE LOGIC
    // ==========================================
    public void ShowOutline()
    {
        if (material != null)
        {
            material.SetFloat("_EnableOutline", 1);
            material.SetColor("_Color", outlineColor);
        }
    }

    public void HideOutline()
    {
        if (material != null)
        {
            material.SetFloat("_EnableOutline", 0);
            material.SetColor("_Color", defaultColor);
        }
    }
}