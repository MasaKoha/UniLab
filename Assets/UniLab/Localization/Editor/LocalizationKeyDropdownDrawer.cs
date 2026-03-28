using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using System.Linq;
using System.Collections.Generic;

namespace UniLab.Localization.Editor
{
    public class LocalizationKeyDropdownAttribute : PropertyAttribute
    {
    }

#if UNITY_EDITOR
    [CustomPropertyDrawer(typeof(LocalizationKeyDropdownAttribute))]
    public class LocalizationKeyDropdownDrawer : PropertyDrawer
    {
        private const string EmptyEntry = "--";

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var container = new VisualElement();

            var data = Resources.Load<LocalizationData>("LocalizationData");
            var allKeys = data != null
                ? data.Entries.Select(e => e.Key).ToList()
                : new List<string>();

            if (!allKeys.Contains(EmptyEntry))
            {
                allKeys.Insert(0, EmptyEntry);
            }

            var searchField = new TextField { label = "Search" };

            string currentValue = property.stringValue;

            if (string.IsNullOrEmpty(currentValue) || !allKeys.Contains(currentValue))
            {
                currentValue = EmptyEntry;
                property.stringValue = "";
                property.serializedObject.ApplyModifiedProperties();
            }

            var dropdown = new PopupField<string>("Key", allKeys, currentValue);

            dropdown.RegisterValueChangedCallback(e =>
            {
                property.stringValue = e.newValue == EmptyEntry ? "" : e.newValue;
                property.serializedObject.ApplyModifiedProperties();
            });

            searchField.RegisterValueChangedCallback(evt =>
            {
                var keyword = evt.newValue.ToLower();
                var filtered = allKeys.Where(k => k.ToLower().Contains(keyword)).ToList();

                if (!filtered.Contains(EmptyEntry))
                {
                    filtered.Insert(0, EmptyEntry);
                }

                if (!filtered.Contains(dropdown.value))
                {
                    dropdown.value = EmptyEntry;
                }

                dropdown.choices = filtered;
            });

            container.Add(searchField);
            container.Add(dropdown);
            return container;
        }
    }
#endif
}
