using UnityEngine;

/// <summary>
/// Отвечает за подсветку деталей при наведении курсора.
/// Версия 2.0: Прямая установка цвета эмиссии без зависимости от базового цвета материала.
/// </summary>
[RequireComponent(typeof(Camera))]
public class RaycastHighlighter : MonoBehaviour
{
    [Header("Настройки слоя")]
    [Tooltip("Слой, на котором находятся детали модели (например, 'Parts')")]
    public LayerMask partsLayer;

    [Header("Настройки визуализации")]
    [Tooltip("Цвет подсветки (например, золотистый)")]
    public Color highlightColor = new Color(1f, 0.8f, 0f, 1f);
    
    [Tooltip("Имя свойства цвета в шейдере (обычно _EmissionColor)")]
    public string colorPropertyName = "_EmissionColor";
    
    [Range(0f, 1f)]
    [Tooltip("Яркость свечения (0 - выкл, 1 - полная яркость цвета highlightColor)")]
    public float emissionIntensity = 0.5f;

    [Header("Отладка")]
    public bool showDebugRay = false;

    private Camera _cam;
    private GameObject _currentHoveredObject;
    private MaterialPropertyBlock _propertyBlock;
    private Renderer _currentRenderer;
    private int _colorPropertyId;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null)
        {
            Debug.LogError("RaycastHighlighter: Требуется компонент Camera на этом объекте!");
            enabled = false;
            return;
        }

        _propertyBlock = new MaterialPropertyBlock();
        _colorPropertyId = Shader.PropertyToID(colorPropertyName);
    }

    private void Update()
    {
        HandleRaycast();
    }

    private void HandleRaycast()
    {
        if (_cam == null) return;

        Ray ray = _cam.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Пускаем луч только по слою деталей
        if (Physics.Raycast(ray, out hit, Mathf.Infinity, partsLayer))
        {
            GameObject hitObject = hit.collider.gameObject;

            if (_currentHoveredObject != hitObject)
            {
                ResetHighlight();
                ApplyHighlight(hitObject);
                _currentHoveredObject = hitObject;
            }
        }
        else
        {
            if (_currentHoveredObject != null)
            {
                ResetHighlight();
                _currentHoveredObject = null;
            }
        }

        if (showDebugRay)
        {
            Debug.DrawRay(ray.origin, ray.direction * 100, Color.red);
        }
    }

    private void ApplyHighlight(GameObject target)
    {
        Renderer renderer = target.GetComponent<Renderer>();
        if (renderer == null) return;

        _currentRenderer = renderer;
        
        renderer.GetPropertyBlock(_propertyBlock);

        // === НОВАЯ ЛОГИКА ===
        // Мы полностью игнорируем текущий цвет материала.
        // Просто берем наш золотистый цвет и умножаем его на интенсивность (0..1).
        // Пример: Золотой (1, 0.8, 0) * 0.5 = (0.5, 0.4, 0) -> мягкое свечение.
        
        Color finalEmission = highlightColor * emissionIntensity;

        _propertyBlock.SetColor(_colorPropertyId, finalEmission);

        renderer.SetPropertyBlock(_propertyBlock);
    }

    private void ResetHighlight()
    {
        if (_currentRenderer != null)
        {
            // Сбрасываем блок, возвращая материал в исходное состояние
            _currentRenderer.SetPropertyBlock(null);
            _currentRenderer = null;
        }
    }
}