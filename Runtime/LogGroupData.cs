using UnityEngine;
using System.Collections.Generic;
using System;
using System.IO;



#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "LogGroups", menuName = "CustomConsole/Log Groups", order = 1)]
public class LogGroupData : ScriptableObject
{
    [SerializeField, HideInInspector] private List<string> groups = new List<string> { "All", "General" };

    public IReadOnlyList<string> Groups => groups.AsReadOnly();

#if UNITY_EDITOR
    public void GenerateStaticClass()
    {
        string path = "Assets/Scripts/Generated/LogGroups.cs";
        using (StreamWriter writer = new StreamWriter(path))
        {
            writer.WriteLine("// Auto-generated LogGroups class");
            writer.WriteLine("public static class LogGroups");
            writer.WriteLine("{");

            foreach (var group in groups)
            {
                string safeGroupName = group.Replace(" ", "_");
                writer.WriteLine($"    public const string {safeGroupName} = \"{group}\";");
            }

            writer.WriteLine("}");
        }
        UnityEditor.AssetDatabase.Refresh();
    }
#endif

    // Method to add a group
    public void AddGroup(string group)
    {
        if (!groups.Contains(group))
        {
            groups.Add(group);
        }
    }

    // Method to remove a group
    public void RemoveGroup(string group)
    {
        if (groups.Contains(group) && group != "All" && group != "General")
        {
            groups.Remove(group);
        }
    }

    // Method to remove a group by index
    public void RemoveGroupAt(int index)
    {
        if (index >= 0 && index < groups.Count && groups[index] != "All" && groups[index] != "General")
        {
            groups.RemoveAt(index);
        }
    }
}

[Serializable]
public enum LogCategory
{
    General,
    UI,
    Network,
    Database,
    Audio,
    Gameplay
}

#if UNITY_EDITOR
[CustomEditor(typeof(LogGroupData))]
public class LogGroupDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        LogGroupData logGroupData = (LogGroupData)target;

        EditorGUILayout.LabelField("Groups", EditorStyles.boldLabel);

        EditorGUI.BeginDisabledGroup(true);
        foreach (string group in logGroupData.Groups)
        {
            EditorGUILayout.TextField(group);
        }
        EditorGUI.EndDisabledGroup();
    }
}
#endif