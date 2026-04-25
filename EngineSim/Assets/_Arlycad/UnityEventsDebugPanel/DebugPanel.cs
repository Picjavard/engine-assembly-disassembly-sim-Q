using UnityEngine;
using UnityEngine.Events;
using System;

/// <summary>
/// Компонент панели отладки.
/// Хранит список кнопок и связанных с ними событий.
/// Вешается на любой объект в сцене.
/// </summary>
public class DebugPanel : MonoBehaviour
{
    [Serializable]
    public struct DebugButtonData
    {
        [Tooltip("Название кнопки, отображаемое в инспекторе")]
        public string buttonName;
        
        [Tooltip("Событие, вызываемое при нажатии")]
        public UnityEvent onClick;
    }

    [Header("Настройки отладки")]
    [Tooltip("Список кнопок. Нажмите + чтобы добавить новую.")]
    public DebugButtonData[] buttons = new DebugButtonData[0];

    // Метод для вызова события по индексу (используется редактором)
    public void TriggerEvent(int index)
    {
        if (index >= 0 && index < buttons.Length)
        {
            buttons[index].onClick?.Invoke();
        }
    }
}