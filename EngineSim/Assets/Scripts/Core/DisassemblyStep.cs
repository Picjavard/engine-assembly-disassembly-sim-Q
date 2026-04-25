using UnityEngine;

/// <summary>
/// Данные одного шага разборки/сборки (ТОЛЬКО ИНСТРУКЦИИ).
/// Не содержит ссылок на объекты сцены, чтобы быть универсальным ассетом.
/// Создается через Editor: Right Click -> Create -> Disassembly Step.
/// </summary>
[CreateAssetMenu(fileName = "NewDisassemblyStep", menuName = "Disassembly/Step", order = 0)]
public class DisassemblyStep : ScriptableObject
{
    [Header("Параметры трансформации")]
    [Tooltip("Конечная позиция (Local Position относительно родителя или World, если нет родителя).")]
    public Vector3 targetPosition;

    [Tooltip("Конечный поворот (Local Rotation).")]
    public Vector3 targetRotation;

    [Header("Настройки анимации")]
    [Tooltip("Длительность анимации в секундах")]
    public float duration = 1.0f;

    [Tooltip("Задержка перед началом шага")]
    public float delay = 0.0f;

    [Header("Тип шага")]
    [Tooltip("Если true, деталь считается извлеченной (можно отключить физику/коллайдер после анимации)")]
    public bool isRemovalStep = false;
    
    [Tooltip("Описание для UI")]
    [TextArea(2, 4)]
    public string description = "";
}