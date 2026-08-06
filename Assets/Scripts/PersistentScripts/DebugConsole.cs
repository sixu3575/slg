using UnityEngine;
using System.Collections.Generic;

public class DebugConsole : MonoBehaviour
{
    public static DebugConsole Instance { get; private set; }

    private List<string> logs = new List<string>();
    private Vector2 scrollPosition;
    private bool showConsole = true;
    private bool scrollToBottom = false; // <-- Track if we need to snap to the bottom

    [SerializeField] private int maxMessages = 50;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        // Hides the built-in development console/log overlay on the screen
        Debug.developerConsoleVisible = false;

        Application.logMessageReceived += HandleLog;
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= HandleLog;
    }

    private void HandleLog(string logString, string stackTrace, LogType type)
    {
        string message;
        string lineLocation = "";

        if (type == LogType.Error || type == LogType.Exception || type == LogType.Warning)
        {
            if (!string.IsNullOrEmpty(stackTrace))
            {
                string[] lines = stackTrace.Split('\n');

                foreach (string traceLine in lines)
                {
                    if (traceLine.Contains("Assets/") || traceLine.Contains(".cs:"))
                    {
                        int assetsIndex = traceLine.IndexOf("Assets/");
                        lineLocation = "\n👉 " + (assetsIndex != -1 ? traceLine.Substring(assetsIndex) : traceLine).Trim();
                        break;
                    }
                }

                // Clean fallback if Unity hides the line numbers with a GUID hash
                if (string.IsNullOrEmpty(lineLocation) && lines.Length > 0)
                {
                    string firstTrace = lines[0].Trim();

                    // If it contains the ugly assembly hash text, strip it out cleanly
                    if (firstTrace.Contains("(at <"))
                    {
                        int atIndex = firstTrace.IndexOf("(at <");
                        if (atIndex != -1) firstTrace = firstTrace.Substring(0, atIndex).Trim();
                    }

                    lineLocation = "\n👉 " + firstTrace + " (Line numbers hidden by build settings)";
                }
            }

            message = $"[{type}] {logString}{lineLocation}";
        }
        else
        {
            message = $"[{type}] {logString}";
        }

        logs.Add(message);

        if (logs.Count > maxMessages)
            logs.RemoveAt(0);

        // Flag that a new log arrived so we snap the scroll view down
        scrollToBottom = true;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
            showConsole = !showConsole;
    }

    void OnGUI()
    {
        if (!showConsole) return;

        GUILayout.BeginArea(new Rect(10, 10, Screen.width - 20, 300), "Console", GUI.skin.window);

        // If a new message came in, force the Y position to the absolute bottom
        if (scrollToBottom)
        {
            scrollPosition.y = float.MaxValue;
            scrollToBottom = false;
        }

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        foreach (var log in logs)
        {
            if (log.StartsWith("[Error]") || log.StartsWith("[Exception]"))
                GUI.contentColor = Color.red;
            else if (log.StartsWith("[Warning]"))
                GUI.contentColor = Color.yellow;
            else
                GUI.contentColor = Color.white;

            GUILayout.Label(log);
        }

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    // Optional helper for custom logs
    public void Log(string message)
    {
        HandleLog(message, "", LogType.Log);
    }
}