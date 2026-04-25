using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DebugPanel))]
public class DebugPanelEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DebugPanel myTarget = (DebugPanel)target;

        // Обновляем данные перед отрисовкой
        serializedObject.Update();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("🛠 Debug Panel", EditorStyles.boldLabel);

        // Предупреждение, если не в режиме Play
        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Кнопки активны только в режиме Play (Play Mode).", MessageType.Info);
        }

        // Находим свойство массива кнопок
        SerializedProperty buttonsProp = serializedObject.FindProperty("buttons");

        if (buttonsProp != null)
        {
            // Рисуем стандартный массив (где можно добавлять/удалять элементы и настраивать события)
            // Но мы кастомизируем заголовок массива
            EditorGUILayout.PropertyField(buttonsProp, new GUIContent("Конфигурация кнопок"), true);

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Управление", EditorStyles.boldLabel);

            // Блокируем кнопки, если игра не запущена
            EditorGUI.BeginDisabledGroup(!Application.isPlaying);

            for (int i = 0; i < buttonsProp.arraySize; i++)
            {
                SerializedProperty buttonData = buttonsProp.GetArrayElementAtIndex(i);
                SerializedProperty nameProp = buttonData.FindPropertyRelative("buttonName");
                
                // Получаем имя кнопки. Если пустое, пишем "Button #i"
                string btnName = string.IsNullOrEmpty(nameProp.stringValue) 
                    ? $"Button {i + 1}" 
                    : nameProp.stringValue;

                // Рисуем кнопку вызова
                GUI.backgroundColor = new Color(0.6f, 0.9f, 1f, 1f); // Голубой цвет
                
                if (GUILayout.Button($"▶ EXECUTE: {btnName}", GUILayout.Height(25)))
                {
                    // Вызываем метод на целевом объекте
                    myTarget.TriggerEvent(i);
                    Debug.Log($"[DebugPanel] Executed: {btnName}");
                }
                
                GUI.backgroundColor = Color.white;
                EditorGUILayout.Space(2);
            }

            EditorGUI.EndDisabledGroup();
        }

        // Применяем изменения (если пользователь добавил новый элемент в массив)
        serializedObject.ApplyModifiedProperties();
    }
}