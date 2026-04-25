using UnityEngine;
using DG.Tweening;

/// <summary>
/// Контроллер орбитальной камеры.
/// Позволяет вращать камеру вокруг цели и приближать/отдалять её.
/// </summary>
public class OrbitCameraController : MonoBehaviour
{
    [Header("Настройки цели")]
    [Tooltip("Точка, вокруг которой вращается камера (Pivot)")]
    public Vector3 targetPoint = Vector3.zero;

    [Header("Настройки вращения")]
    [Tooltip("Скорость вращения мышью")]
    public float rotateSpeed = 5f;
    
    [Tooltip("Минимальный угол по вертикали (чтобы не уйти под землю)")]
    public float minVerticalAngle = -80f;
    
    [Tooltip("Максимальный угол по вертикали")]
    public float maxVerticalAngle = 80f;

    [Header("Настройки зума")]
    [Tooltip("Текущая дистанция до цели")]
    public float currentDistance = 10f;
    
    [Tooltip("Минимальная дистанция зума")]
    public float minDistance = 2f;
    
    [Tooltip("Максимальная дистанция зума")]
    public float maxDistance = 20f;
    
    [Tooltip("Скорость зума колесиком")]
    public float zoomSpeed = 2f;

    [Header("Настройки анимации фокуса")]
    [Tooltip("Длительность плавного перелета к новой цели")]
    public float focusDuration = 1.0f;
    
    [Tooltip("Тип плавности анимации фокуса")]
    public Ease focusEase = Ease.InOutQuad;

    // Внутренние переменные для углов
    private float _horizontalAngle = 0f;
    private float _verticalAngle = 10f;
    
    // Ссылка на трансформ для кэширования
    private Transform _camTransform;

    private void Awake()
    {
        _camTransform = transform;
        // Инициализируем углы текущим положением, если нужно, 
        // но проще начать с дефолтных значений или вычислить из текущей позиции
        UpdateCameraPosition();
    }

    private void LateUpdate()
    {
        // Обработка вращения мышью
        if (Input.GetMouseButton(0)) // ЛКМ для вращения (можно изменить на правую кнопку если нужно)
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            _horizontalAngle += mouseX * rotateSpeed;
            _verticalAngle -= mouseY * rotateSpeed;

            // Ограничение вертикального угла
            _verticalAngle = Mathf.Clamp(_verticalAngle, minVerticalAngle, maxVerticalAngle);
        }

        // Обработка зума колесиком
        float scrollWheel = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scrollWheel) > 0.01f)
        {
            currentDistance -= scrollWheel * zoomSpeed * 10f;
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);
        }

        UpdateCameraPosition();
    }

    /// <summary>
    /// Обновляет позицию камеры на основе углов и дистанции
    /// </summary>
    private void UpdateCameraPosition()
    {
        // Вычисляем позицию камеры в сферических координатах
        float radH = _horizontalAngle * Mathf.Deg2Rad;
        float radV = _verticalAngle * Mathf.Deg2Rad;

        float sinV = Mathf.Sin(radV);
        float cosV = Mathf.Cos(radV);
        float sinH = Mathf.Sin(radH);
        float cosH = Mathf.Cos(radH);

        Vector3 offset = new Vector3(
            cosV * sinH * currentDistance,
            sinV * currentDistance,
            cosV * cosH * currentDistance
        );

        Vector3 newPosition = targetPoint + offset;
        
        _camTransform.position = newPosition;
        _camTransform.LookAt(targetPoint);
    }

    // ==========================================================
    // НОВЫЕ МЕТОДЫ ДЛЯ ПРОГРАММНОГО УПРАВЛЕНИЯ (API)
    // ==========================================================

    /// <summary>
    /// Плавно перемещает точку фокуса (pivot) камеры в новую позицию.
    /// Используется при клике на деталь в иерархии.
    /// </summary>
    /// <param name="newTarget">Новая целевая точка (позиция объекта)</param>
    public void SetFocusPoint(Vector3 newTarget)
    {
        // Прерываем предыдущую анимацию фокуса, если она была
        DOTween.Kill("FocusTween");

        // Анимируем изменение поля targetPoint
        Vector3 startTarget = targetPoint;
        
        DOTween.To(
            () => targetPoint, 
            x => targetPoint = x, 
            newTarget, 
            focusDuration
        )
        .SetId("FocusTween")
        .SetEase(focusEase)
        .OnUpdate(() => UpdateCameraPosition()); // Обновляем позицию камеры каждый кадр анимации
    }

    /// <summary>
    /// Мгновенно устанавливает точку фокуса (без анимации).
    /// Полезно для сброса или начальной настройки.
    /// </summary>
    public void SetFocusPointInstant(Vector3 newTarget)
    {
        DOTween.Kill("FocusTween");
        targetPoint = newTarget;
        UpdateCameraPosition();
    }

    /// <summary>
    /// Плавно изменяет дистанцию камеры (Зум).
    /// </summary>
    /// <param name="newDistance">Новая дистанция</param>
    public void SetTargetDistance(float newDistance)
    {
        newDistance = Mathf.Clamp(newDistance, minDistance, maxDistance);

        DOTween.Kill("ZoomTween");

        DOTween.To(
            () => currentDistance,
            x => currentDistance = x,
            newDistance,
            focusDuration
        )
        .SetId("ZoomTween")
        .SetEase(focusEase);
    }

    /// <summary>
    /// Сброс камеры в исходное состояние (центр сцены, стандартный зум).
    /// </summary>
    public void ResetCamera()
    {
        SetFocusPoint(Vector3.zero);
        SetTargetDistance(10f); // Или любое значение по умолчанию
        _horizontalAngle = 0f;
        _verticalAngle = 10f;
    }
}