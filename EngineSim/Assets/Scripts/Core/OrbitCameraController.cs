using UnityEngine;
using UnityEngine.InputSystem;
using DG.Tweening;

/// <summary>
/// Контроллер орбитальной камеры с поддержкой плавного фокуса и зума.
/// </summary>
public class OrbitCameraController : MonoBehaviour
{
    [Header("Настройки вращения")]
    public float rotateSpeed = 100f;

    [Header("Настройки зума")]
    public float minDistance = 2f;
    public float maxDistance = 20f;
    public float zoomSpeed = 5f;

    [Header("Текущее состояние")]
    public Vector3 focusPoint = Vector3.zero;
    public float currentDistance = 10f;

    // Внутренние переменные для углов
    private float _horizontalAngle = 0f;
    private float _verticalAngle = 20f;

    // Ссылка на камеру
    private Camera _cam;

    // Ссылки на действия New Input System
    private Mouse _mouse;

    private void Start()
    {
        _cam = GetComponent<Camera>();
        if (_cam == null)
        {
            Debug.LogError("OrbitCameraController: Камера не найдена!");
            enabled = false;
        }

        // Инициализация New Input System
        _mouse = Mouse.current;

        UpdateCameraPosition();
    }

    private void LateUpdate()
    {
        // Проверка на случай, если устройство мыши не подключено
        if (_mouse == null)
            return;

        // Вращение (ЛКМ + Drag или колесико как кнопка)
        // GetMouseButton(0) -> leftButton.isPressed
        if (_mouse.leftButton.isPressed)
        {
            //.GetAxis("Mouse X") -> delta.x
            float h = _mouse.delta.x.ReadValue();
            //.GetAxis("Mouse Y") -> delta.y
            float v = _mouse.delta.y.ReadValue();

            _horizontalAngle += h * rotateSpeed * Time.deltaTime;
            _verticalAngle -= v * rotateSpeed * Time.deltaTime;
            _verticalAngle = Mathf.Clamp(_verticalAngle, -80f, 80f);

            UpdateCameraPosition();
        }

        // Зум (Колесико мыши)
        //.GetAxis("Mouse ScrollWheel") -> scroll.ReadValue()
        float scroll = _mouse.scroll.ReadValue().y;
        if (scroll != 0f)
        {
            currentDistance -= scroll * zoomSpeed * 0.01f; // Усиливаем чувствительность
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
            UpdateCameraPosition();
        }
    }

    /// <summary>
    /// Обновляет позицию камеры относительно точки фокуса
    /// </summary>
    private void UpdateCameraPosition()
    {
        Quaternion rotation = Quaternion.Euler(_verticalAngle, _horizontalAngle, 0);
        Vector3 offset = rotation * new Vector3(0, 0, currentDistance);

        transform.position = focusPoint - offset;
        transform.LookAt(focusPoint);
    }

    #region Public Methods for External Control

    /// <summary>
    /// Мгновенно установить точку фокуса
    /// </summary>
    public void SetFocusPoint(Vector3 point)
    {
        focusPoint = point;
        UpdateCameraPosition();
    }

    /// <summary>
    /// Мгновенно установить дистанцию
    /// </summary>
    public void SetTargetDistance(float distance)
    {
        currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);
        UpdateCameraPosition();
    }

    /// <summary>
    /// Плавная фокусировка на конкретном объекте.
    /// Вызывается из скриптов (например, HierarchyView).
    /// </summary>
    public void FocusOnObject(GameObject target, float duration = 1.5f, float offsetDistance = 3f)
    {
        if (target == null) return;

        Vector3 targetPos = target.transform.position;

        // 1. Анимация смены точки фокуса (Pivot)
        DOTween.To(() => focusPoint, x => focusPoint = x, targetPos, duration)
                .SetEase(Ease.OutCubic)
                .OnUpdate(UpdateCameraPosition);

        // 2. Анимация зума (немного отъезжаем, чтобы видеть объект целиком)
        float targetDist = Mathf.Clamp(offsetDistance, minDistance, maxDistance);

        DOTween.To(() => currentDistance, x => currentDistance = x, targetDist, duration)
               .SetEase(Ease.OutCubic)
               .OnUpdate(UpdateCameraPosition);
    }

    /// <summary>
    /// Плавный сброс камеры в исходное состояние (Центр + Стандартный зум)
    /// </summary>
    public void ResetCameraSmooth(float duration = 1.0f)
    {
        // Целевые значения
        Vector3 targetFocus = Vector3.zero;
        float targetDist = 10f; // Стандартный зум

        // Анимация фокуса
        DOTween.To(() => focusPoint, x => focusPoint = x, targetFocus, duration)
                .SetEase(Ease.InOutQuad)
                .OnUpdate(UpdateCameraPosition);

        // Анимация зума
        DOTween.To(() => currentDistance, x => currentDistance = x, targetDist, duration)
               .SetEase(Ease.InOutQuad)
               .OnUpdate(UpdateCameraPosition);
    }

    #endregion
}