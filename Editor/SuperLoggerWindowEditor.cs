using UnityEngine;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Compilation;
using System.Collections.Generic;
using System.Linq;
using PugDev.SuperLogger;
using System;

namespace PugDev.SuperLogger
{
    public class SuperLoggerWindowEditor : EditorWindow
    {
        private Vector2 scrollPosition;
        private Vector2 detailScrollPosition;
        private List<LogEntry> logs = new List<LogEntry>();
        private LogGroupData logGroupData;
        private List<string> selectedGroups = new() { "All" };
        private string searchQuery = "";
        private bool showErrors = true;
        private bool showWarnings = true;
        private bool showDebugs = true;
        private bool autoScroll = true;

        private bool clearOnPlay = true;
        private bool clearOnBuild = true;
        private bool clearOnRecompile = false;

        private Dictionary<string, int> groupCounts = new Dictionary<string, int>();
        private int errorCount = 0;
        private int warningCount = 0;
        private int debugCount = 0;

        private Texture2D errorIcon;
        private Texture2D warningIcon;
        private Texture2D debugIcon;

        private LogEntry selectedLog;
        private float splitHeight = 200f;
        private const string SplitHeightKey = "SuperConsole_SplitHeight";
        private float lastClickTime = 0f;
        private const float doubleClickTime = 0.3f;

        private class LogEntry
        {
            public string message;
            public LogType type;
            public string stackTrace;
            public string group;

            public LogEntry(string msg, LogType type, string stackTrace, string group)
            {
                message = msg;
                this.type = type;
                this.stackTrace = stackTrace;
                this.group = group;
            }
        }

        [MenuItem("Window/Super Console %#x")]
        public static void ShowWindow()
        {
            GetWindow<SuperLoggerWindowEditor>("Super Console");
        }

        private void OnEnable()
        {
            splitHeight = EditorPrefs.GetFloat(SplitHeightKey, 200f);

            Application.logMessageReceived += HandleLog;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
            BuildPlayerWindow.RegisterBuildPlayerHandler(OnBuildStarted);
            CompilationPipeline.compilationStarted += OnCompilationStarted;

            errorIcon = EditorGUIUtility.IconContent("console.erroricon.sml").image as Texture2D;
            warningIcon = EditorGUIUtility.IconContent("console.warnicon.sml").image as Texture2D;
            debugIcon = EditorGUIUtility.IconContent("console.infoicon.sml").image as Texture2D;

            LoadLogGroupData();
        }

        private void OnDisable()
        {
            EditorPrefs.SetFloat(SplitHeightKey, splitHeight);

            Application.logMessageReceived -= HandleLog;
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            BuildPlayerWindow.RegisterBuildPlayerHandler(null);
            CompilationPipeline.compilationStarted -= OnCompilationStarted;
        }

        private void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (clearOnPlay && state == PlayModeStateChange.EnteredPlayMode)
            {
                ClearLogs();
            }
        }

        private void OnBuildStarted(BuildPlayerOptions report)
        {
            if (clearOnBuild)
            {
                ClearLogs();
            }
        }

        private void OnCompilationStarted(object obj)
        {
            if (clearOnRecompile)
            {
                ClearLogs();
            }
        }

        private void ClearLogs()
        {
            logs.Clear();
            groupCounts.Clear();
            errorCount = warningCount = debugCount = 0;
            selectedLog = null;
            Repaint();
        }

        private void LoadLogGroupData()
        {
            logGroupData = AssetDatabase.LoadAssetAtPath<LogGroupData>("Assets/SLoggerGenerated/SLogGroups.asset");

            if (logGroupData == null)
            {
                logGroupData = CreateInstance<LogGroupData>();
                AssetDatabase.CreateAsset(logGroupData, "Assets/SLoggerGenerated/SLogGroups.asset");
                AssetDatabase.SaveAssets();
                Debug.Log("Created new SLogGroups.asset");
            }
        }

        private void SaveLogGroups()
        {
            EditorUtility.SetDirty(logGroupData);
            AssetDatabase.SaveAssets();
        }

        private void HandleLog(string condition, string stackTrace, LogType type)
        {
            string group = "General";

            if (condition.StartsWith("[SLogger]["))
            {
                int start = condition.IndexOf("[SLogger][") + 10;
                int end = condition.IndexOf("]", start);
                if (end > start)
                {
                    group = condition.Substring(start, end - start);
                    condition = condition.Substring(end + 1);
                }
            }

            var entry = logGroupData.GroupColorEntries.FirstOrDefault(e => e.Group == group);
            string color = ColorUtility.ToHtmlStringRGBA(entry?.Color ?? Color.white);
            string time = DateTime.Now.TimeOfDay.ToString(@"hh\:mm\:ss");
            string richTextCondition = $"[{time}] <color=#{color}>[{group}]</color> {condition}";

            logs.Add(new LogEntry(richTextCondition, type, stackTrace, group));

            if (!logGroupData.Groups.Contains(group))
            {
                logGroupData.AddGroup(group, Color.white); // Add default color
                SaveLogGroups();
            }

            switch (type)
            {
                case LogType.Error: errorCount++; break;
                case LogType.Warning: warningCount++; break;
                case LogType.Log: debugCount++; break;
            }

            if (autoScroll)
            {
                scrollPosition.y = float.MaxValue;
            }

            Repaint();
        }

        private void OnGUI()
        {
            DrawToolbar();

            Rect logListRect = new Rect(0, 25, position.width, splitHeight);
            GUILayout.BeginArea(logListRect);
            DrawLogList();
            GUILayout.EndArea();

            Rect splitterRect = new Rect(0, splitHeight + 25, position.width, 1);
            EditorGUI.DrawRect(splitterRect, Color.black);
            HandleResize(splitterRect);

            Rect logDetailsRect = new Rect(0, splitHeight + 30, position.width, position.height - splitHeight - 30);
            GUILayout.BeginArea(logDetailsRect);
            DrawLogDetails();
            GUILayout.EndArea();
        }

        private void HandleResize(Rect splitterRect)
        {
            EditorGUIUtility.AddCursorRect(splitterRect, MouseCursor.ResizeVertical);
            if (Event.current.type == EventType.MouseDown && splitterRect.Contains(Event.current.mousePosition))
            {
                GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
            }
            if (GUIUtility.hotControl != 0 && Event.current.type == EventType.MouseDrag)
            {
                splitHeight = Mathf.Clamp(Event.current.mousePosition.y - 25, 50, position.height - 50);
                Repaint();
            }
            if (Event.current.type == EventType.MouseUp)
            {
                GUIUtility.hotControl = 0;
                EditorPrefs.SetFloat(SplitHeightKey, splitHeight);
            }
        }

        private void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            try
            {
                if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(50)))
                {
                    ClearLogs();
                }

                // Multi-select dropdown for clear options
                if (GUILayout.Button("", EditorStyles.toolbarPopup, GUILayout.Width(20)))
                {
                    GenericMenu menu = new GenericMenu();
                    menu.AddItem(new GUIContent("Clear on Play"), clearOnPlay, () => ToggleClearOption(ref clearOnPlay));
                    menu.AddItem(new GUIContent("Clear on Build"), clearOnBuild, () => ToggleClearOption(ref clearOnBuild));
                    menu.AddItem(new GUIContent("Clear on Recompile"), clearOnRecompile, () => ToggleClearOption(ref clearOnRecompile));
                    menu.DropDown(new Rect(0, 20, 0, 0));
                }

                DrawSearchBar();

                GUILayout.FlexibleSpace();
                showErrors = GUILayout.Toggle(showErrors, new GUIContent($"{errorCount}", errorIcon), EditorStyles.toolbarButton);
                showWarnings = GUILayout.Toggle(showWarnings, new GUIContent($"{warningCount}", warningIcon), EditorStyles.toolbarButton);
                showDebugs = GUILayout.Toggle(showDebugs, new GUIContent($"{debugCount}", debugIcon), EditorStyles.toolbarButton);

                // Add logGroupData multi-selection dropdown menu
                if (GUILayout.Button("Groups", EditorStyles.toolbarPopup, GUILayout.Width(80)))
                {
                    GUI.FocusControl("Groups");
                    Rect buttonRect = GUILayoutUtility.GetLastRect();
                    MultiSelectDropdown.ShowDropdown(buttonRect, logGroupData.Groups.ToList(), selectedGroups, (selected) =>
                    {
                        selectedGroups = selected;
                        Repaint();
                    });
                }

                autoScroll = GUILayout.Toggle(autoScroll, "Auto Scroll", EditorStyles.toolbarButton);

                if (GUILayout.Button("Manage Groups", EditorStyles.toolbarButton))
                {
                    GroupManagerWindow.ShowWindow(logGroupData);
                }
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }
        }

        private void ToggleClearOption(ref bool option)
        {
            option = !option;
        }

        private void DrawSearchBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            try
            {
                GUIStyle searchBarStyle = new GUIStyle("ToolbarSearchTextField");
                GUIStyle searchCancelStyle = new GUIStyle("ToolbarSearchCancelButton");

                Rect searchRect = GUILayoutUtility.GetRect(200, 20, searchBarStyle);
                searchRect.width = Mathf.Max(searchRect.width, 200);

                GUIContent searchIconContent = EditorGUIUtility.IconContent("Search Icon");
                Rect iconRect = new Rect(searchRect.x + 5, searchRect.y + 2, 16, 16);
                GUI.Label(iconRect, searchIconContent);

                searchBarStyle.padding = new RectOffset(20, 0, 0, 0);

                searchQuery = GUI.TextField(searchRect, searchQuery, searchBarStyle);

                EditorGUIUtility.AddCursorRect(searchRect, MouseCursor.Text);

                if (!string.IsNullOrEmpty(searchQuery))
                {
                    Rect cancelRect = new Rect(searchRect.x + searchRect.width - 18, searchRect.y + 2, 16, 16);
                    if (GUI.Button(cancelRect, GUIContent.none, searchCancelStyle))
                    {
                        searchQuery = "";
                        GUI.FocusControl(null);
                    }
                }
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }
        }


        private void DrawLogList()
        {
            scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition);
            try
            {
                for (int i = 0; i < logs.Count; i++)
                {
                    var log = logs[i];
                    if ((log.type == LogType.Error && showErrors) ||
                        (log.type == LogType.Warning && showWarnings) ||
                        (log.type == LogType.Log && showDebugs))
                    {
                        if (!selectedGroups.Contains("All") && !selectedGroups.Contains(log.group))
                            continue;

                        if (!string.IsNullOrEmpty(searchQuery) && !log.message.ToLower().Contains(searchQuery.ToLower()))
                            continue;

                        GUIStyle style = new GUIStyle(EditorStyles.label) { richText = true, alignment = TextAnchor.MiddleLeft };
                        Texture2D icon = debugIcon;

                        if (log.type == LogType.Error)
                        {
                            style = new GUIStyle(EditorStyles.boldLabel) { richText = true, alignment = TextAnchor.MiddleLeft };
                            icon = errorIcon;
                        }
                        else if (log.type == LogType.Warning)
                        {
                            icon = warningIcon;
                        }

                        GUIStyle boxStyle = new GUIStyle("box") { contentOffset = Vector2.up / 2, richText = true };

                        GUILayout.BeginVertical(boxStyle);
                        EditorGUILayout.BeginHorizontal();
                        try
                        {
                            GUILayout.Box(icon, GUILayout.Width(25), GUILayout.Height(25));
                            if (GUILayout.Button(log.message, boxStyle))
                            {
                                HandleLogClick(log);
                            }
                        }
                        finally
                        {
                            EditorGUILayout.EndHorizontal();
                            GUILayout.EndVertical();
                        }
                    }
                }
            }
            finally
            {
                EditorGUILayout.EndScrollView();
            }
        }

        private void HandleLogClick(LogEntry log)
        {
            float clickTime = Time.realtimeSinceStartup;

            if (selectedLog == log && (clickTime - lastClickTime) < doubleClickTime)
            {
                OpenScriptFromStackTrace(log.stackTrace);
            }
            else
            {
                selectedLog = log;
            }

            lastClickTime = clickTime;
        }

        private void OpenScriptFromStackTrace(string stackTrace)
        {
            if (string.IsNullOrEmpty(stackTrace)) return;

            string[] lines = stackTrace.Split('\n');

            if (lines.Length < 2) return;

            string targetLine = lines[2];
            string path = ExtractPathFromStackTrace(targetLine, out int lineNumber);

            if (!string.IsNullOrEmpty(path))
            {
                UnityEditorInternal.InternalEditorUtility.OpenFileAtLineExternal(path, lineNumber);
            }
        }

        private string ExtractPathFromStackTrace(string stackTraceLine, out int lineNumber)
        {
            lineNumber = 0;

            int startIndex = stackTraceLine.IndexOf(" (at ");
            if (startIndex == -1) return null;

            startIndex += 5;
            int endIndex = stackTraceLine.IndexOf(")", startIndex);
            if (endIndex == -1) return null;

            string filePathWithLine = stackTraceLine.Substring(startIndex, endIndex - startIndex);
            string[] parts = filePathWithLine.Split(':');

            if (parts.Length == 2 && int.TryParse(parts[1], out lineNumber))
            {
                return parts[0];
            }

            return null;
        }


        private void DrawLogDetails()
        {
            if (selectedLog != null)
            {
                detailScrollPosition = EditorGUILayout.BeginScrollView(detailScrollPosition, GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true));
                try
                {
                    GUIStyle style = new GUIStyle(EditorStyles.label) { richText = true };
                    EditorGUILayout.LabelField(selectedLog.message, style, GUILayout.ExpandWidth(true));
                    EditorGUILayout.Space(5);
                    DrawStackTrace(selectedLog.stackTrace);
                }
                finally
                {
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawStackTrace(string stackTrace)
        {
            string[] stackTraceLines = stackTrace.Split('\n');
            foreach (var line in stackTraceLines)
            {
                if (string.IsNullOrEmpty(line)) continue;

                var match = System.Text.RegularExpressions.Regex.Match(line, @"\(at (.+):(\d+)\)");
                if (match.Success)
                {
                    string filePath = match.Groups[1].Value;
                    int lineNumber = int.Parse(match.Groups[2].Value);

                    Rect rect = new Rect(0, 0, 0, 0);
                    EditorGUIUtility.AddCursorRect(rect, MouseCursor.Link);
                    if (GUILayout.Button(line, EditorStyles.linkLabel))
                    {
                        OpenStackTraceLine(filePath, lineNumber);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(line);
                }
            }
        }

        private void OpenStackTraceLine(string filePath, int lineNumber)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(filePath);
            if (asset != null)
            {
                AssetDatabase.OpenAsset(asset, lineNumber);
            }
            else
            {
                Debug.LogWarning($"Unable to load asset at path: {filePath}");
            }
        }
    }

    public class GroupManagerWindow : EditorWindow
    {
        private string newGroupName = "";
        private Color newGroupColor = Color.white;
        private LogGroupData logGroupData;

        public static void ShowWindow(LogGroupData logGroupData)
        {
            GroupManagerWindow window = GetWindow<GroupManagerWindow>("Group Manager");
            window.logGroupData = logGroupData;
        }

        private void OnGUI()
        {
            EditorGUILayout.BeginHorizontal();
            try
            {
                newGroupName = EditorGUILayout.TextField("New Group:", newGroupName);
                newGroupColor = EditorGUILayout.ColorField("Group Color:", newGroupColor);
                if (GUILayout.Button("Add Group"))
                {
                    if (!string.IsNullOrEmpty(newGroupName) && !logGroupData.Groups.Contains(newGroupName))
                    {
                        logGroupData.AddGroup(newGroupName, newGroupColor);
                        SaveLogGroups();
                        logGroupData.GenerateStaticClass();
                        newGroupName = "";
                        newGroupColor = Color.white;
                        Repaint();
                    }
                }
            }
            finally
            {
                EditorGUILayout.EndHorizontal();
            }

            // Dropdown for groups with remove button
            for (int i = 0; i < logGroupData.Groups.Count; i++)
            {
                if (logGroupData.Groups[i] != "All" && logGroupData.Groups[i] != "General")
                {
                    EditorGUILayout.BeginHorizontal();
                    try
                    {
                        GUILayout.Label(logGroupData.Groups[i], GUILayout.Width(100));
                        if (GUILayout.Button("x", GUILayout.Width(20)))
                        {
                            logGroupData.RemoveGroupAt(i);
                            SaveLogGroups();
                            logGroupData.GenerateStaticClass();
                            Repaint();
                            break;
                        }
                    }
                    finally
                    {
                        EditorGUILayout.EndHorizontal();
                    }
                }
            }
        }

        private void SaveLogGroups()
        {
            EditorUtility.SetDirty(logGroupData);
            AssetDatabase.SaveAssets();
        }
    }

    public class MultiSelectDropdown : EditorWindow
    {
        private static MultiSelectDropdown window;
        private static List<string> items;
        private static List<string> selectedItems;
        private static System.Action<List<string>> onSelectionChanged;

        public static void ShowDropdown(Rect buttonRect, List<string> items, List<string> selectedItems, System.Action<List<string>> onSelectionChanged)
        {
            if (window == null)
            {
                window = CreateInstance<MultiSelectDropdown>();
            }

            MultiSelectDropdown.items = items;
            MultiSelectDropdown.selectedItems = selectedItems;
            MultiSelectDropdown.onSelectionChanged = onSelectionChanged;

            Vector2 screenPosition = GUIUtility.GUIToScreenPoint(new Vector2(buttonRect.x, buttonRect.y + buttonRect.height));
            window.ShowAsDropDown(new Rect(screenPosition, Vector2.zero), new Vector2(200, items.Count * 20 + 10));
        }

        private void OnGUI()
        {
            Vector2 scrollPosition = Vector2.zero;

            if (items.Count > 10)
            {
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
            }

            for (int i = 0; i < items.Count; i++)
            {
                bool isSelected = selectedItems.Contains(items[i]);
                bool newIsSelected = EditorGUILayout.ToggleLeft(items[i], isSelected);

                if (newIsSelected != isSelected)
                {
                    if (newIsSelected)
                    {
                        if (items[i] == "All")
                        {
                            selectedItems.Clear();
                            selectedItems.Add("All");
                        }
                        else
                        {
                            selectedItems.Remove("All");
                            selectedItems.Add(items[i]);
                        }
                    }
                    else
                    {
                        selectedItems.Remove(items[i]);
                    }

                    onSelectionChanged?.Invoke(selectedItems);
                }
            }

            if (items.Count > 10)
            {
                EditorGUILayout.EndScrollView();
            }

            // Ensure "All" is selected if no other items are selected
            if (selectedItems.Count == 0)
            {
                selectedItems.Add("All");
                onSelectionChanged?.Invoke(selectedItems);
            }
        }
    }
}