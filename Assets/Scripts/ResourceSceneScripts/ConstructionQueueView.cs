using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Renders every resource tile currently under construction for the local player's
/// villages. Listens to VillageActionHandler.OnQueueChanged for full rebuilds and
/// refreshes per-row countdowns every frame using NGO ServerTime.
/// </summary>
public class ConstructionQueueView : MonoBehaviour
{
    [Header("UI (optional — auto-built if left empty)")]
    [Tooltip("Container for the queue rows. If null, a full panel + container is created automatically.")]
    public RectTransform rowContainer;
    [Tooltip("Optional header text. If null, a default 'Construction Queue' label is created.")]
    public TextMeshProUGUI headerLabel;

    private struct RowEntry
    {
        public GameObject gameObject;
        public TextMeshProUGUI label;
        public TextMeshProUGUI countdown;
        public string key;
        public int villageId;
        public int resourceIndex;
        public double endServerTime;
    }

    private readonly Dictionary<string, RowEntry> rows = new Dictionary<string, RowEntry>();
    private int lastOwnedVillageCount = -1;
    private bool subscribed;

    private void OnEnable()
    {
        EnsureContainer();
        Subscribe();
        Rebuild();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void OnDestroy()
    {
        Unsubscribe();
    }

    private void Update()
    {
        if (rows.Count == 0) return;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            return;
        }

        // Refresh only the countdown text — row membership is owned by Rebuild(), which runs
        // off OnQueueChanged (fires on accept, on rejection, and at the end of every fresh
        // ReconstructAndCacheDatabase pull from the server).
        double serverNow = NetworkManager.Singleton.ServerTime.Time;
        foreach (var kvp in rows)
        {
            RowEntry row = kvp.Value;
            row.countdown.text = FormatRemaining(row.endServerTime - serverNow);
        }
    }

    // ======================================================================
    //  Event plumbing
    // ======================================================================

    private void Subscribe()
    {
        if (subscribed) return;
        VillageActionHandler.OnQueueChanged += OnQueueChanged;
        VillageActionHandler.OnInventoryChanged += OnQueueChanged; // inventory HUD shares same trigger
        subscribed = true;
    }

    private void Unsubscribe()
    {
        if (!subscribed) return;
        VillageActionHandler.OnQueueChanged -= OnQueueChanged;
        VillageActionHandler.OnInventoryChanged -= OnQueueChanged;
        subscribed = false;
    }

    private void OnQueueChanged()
    {
        Rebuild();
    }

    // ======================================================================
    //  Rebuild
    // ======================================================================

    private void Rebuild()
    {
        EnsureContainer();

        int ownedVillageCount = CountOwnedVillages();
        if (ownedVillageCount != lastOwnedVillageCount)
        {
            if (headerLabel != null)
            {
                headerLabel.text = ownedVillageCount <= 1
                    ? "Construction Queue"
                    : $"Construction Queue ({ownedVillageCount} villages)";
            }
            lastOwnedVillageCount = ownedVillageCount;
        }

        if (ClientVillageDatabase.Instance == null
            || NetworkConnectionManager.Instance == null
            || NetworkManager.Singleton == null
            || !NetworkManager.Singleton.IsListening)
        {
            ClearAllRows();
            return;
        }

        int localPlayerId = NetworkConnectionManager.Instance.LocalPlayerDatabaseId;
        if (localPlayerId == -1)
        {
            ClearAllRows();
            return;
        }

        double serverNow = NetworkManager.Singleton.ServerTime.Time;
        HashSet<string> seen = new HashSet<string>();

        foreach (var village in ClientVillageDatabase.Instance.GetAllVillages())
        {
            if (village == null) continue;
            if (village.owner == null || village.owner.id != localPlayerId) continue;
            if (village.farmlands?.resources == null) continue;

            string villageTag = ownedVillageCount > 1 ? $"[{village.name}] " : "";

            for (int i = 0; i < village.farmlands.resources.Count; i++)
            {
                Resourceblock block = village.farmlands.resources[i];
                if (block == null || !block.isUnderConstruction) continue;

                string key = $"{village.id}:{i}";
                seen.Add(key);

                if (!rows.TryGetValue(key, out RowEntry row))
                {
                    row = CreateRow();
                    row.key = key;
                    row.villageId = village.id;
                    row.resourceIndex = i;
                    rows[key] = row;
                }

                row.endServerTime = block.upgradeEndServerTime;
                if (block.isUpgrading)
                {
                    row.label.text = $"{villageTag}{Capitalize(block.resourceType.ToString())}  Lv{block.level}  →  Lv{block.level + 1}";
                }
                else
                {
                    row.label.text = $"{villageTag}{Capitalize(block.resourceType.ToString())}  Lv{block.level}  ↓";
                }
                row.countdown.text = FormatRemaining(row.endServerTime - serverNow);
            }
        }

        // Drop rows whose block is no longer under construction.
        List<string> stale = null;
        foreach (var kvp in rows)
        {
            if (!seen.Contains(kvp.Key))
            {
                stale ??= new List<string>();
                stale.Add(kvp.Key);
            }
        }
        if (stale != null)
        {
            foreach (var key in stale)
            {
                if (rows.TryGetValue(key, out var row) && row.gameObject != null)
                {
                    Destroy(row.gameObject);
                }
                rows.Remove(key);
            }
        }

        if (rows.Count == 0 && emptyStateLabel != null)
        {
            emptyStateLabel.gameObject.SetActive(true);
        }
        else if (emptyStateLabel != null)
        {
            emptyStateLabel.gameObject.SetActive(false);
        }
    }

    private void ClearAllRows()
    {
        foreach (var kvp in rows)
        {
            if (kvp.Value.gameObject != null) Destroy(kvp.Value.gameObject);
        }
        rows.Clear();
    }

    private int CountOwnedVillages()
    {
        if (ClientVillageDatabase.Instance == null
            || NetworkConnectionManager.Instance == null) return 0;

        int localPlayerId = NetworkConnectionManager.Instance.LocalPlayerDatabaseId;
        if (localPlayerId == -1) return 0;

        int n = 0;
        foreach (var v in ClientVillageDatabase.Instance.GetAllVillages())
        {
            if (v?.owner != null && v.owner.id == localPlayerId) n++;
        }
        return n;
    }

    // ======================================================================
    //  UI construction
    // ======================================================================

    private TextMeshProUGUI emptyStateLabel;
    private bool autoBuilt;

    private void EnsureContainer()
    {
        if (rowContainer != null) return;
        if (autoBuilt) return;

        // Build a minimal panel: Canvas (if missing) → background image → vertical layout
        // → header → row container. The row container is what we'll parent rows into.
        Canvas canvas = FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGo = new GameObject("ConstructionQueueCanvas",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
        }

        GameObject panel = new GameObject("ConstructionQueuePanel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
        panel.transform.SetParent(canvas.transform, false);

        RectTransform panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1, 1);
        panelRt.anchorMax = new Vector2(1, 1);
        panelRt.pivot = new Vector2(1, 1);
        panelRt.anchoredPosition = new Vector2(-20, -20);
        panelRt.sizeDelta = new Vector2(360, 0);

        Image bg = panel.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);

        VerticalLayoutGroup vlg = panel.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(12, 12, 12, 12);
        vlg.spacing = 6;
        vlg.childAlignment = TextAnchor.UpperRight;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;

        // Header
        GameObject headerGo = new GameObject("Header", typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
        headerGo.transform.SetParent(panel.transform, false);
        headerLabel = headerGo.GetComponent<TextMeshProUGUI>();
        headerLabel.text = "Construction Queue";
        headerLabel.fontSize = 20;
        headerLabel.alignment = TextAlignmentOptions.Center;
        headerLabel.color = Color.white;
        headerGo.GetComponent<LayoutElement>().preferredHeight = 28;

        // Row container
        GameObject rowContainerGo = new GameObject("Rows", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
        rowContainerGo.transform.SetParent(panel.transform, false);
        rowContainer = rowContainerGo.GetComponent<RectTransform>();

        VerticalLayoutGroup rowsVlg = rowContainerGo.GetComponent<VerticalLayoutGroup>();
        rowsVlg.spacing = 4;
        rowsVlg.childForceExpandHeight = false;
        rowsVlg.childForceExpandWidth = true;
        rowsVlg.childControlHeight = true;
        rowsVlg.childControlWidth = true;

        ContentSizeFitter fitter = rowContainerGo.GetComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        // Empty-state placeholder
        GameObject emptyGo = new GameObject("EmptyState", typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
        emptyGo.transform.SetParent(panel.transform, false);
        emptyStateLabel = emptyGo.GetComponent<TextMeshProUGUI>();
        emptyStateLabel.text = "(no active construction)";
        emptyStateLabel.fontSize = 14;
        emptyStateLabel.alignment = TextAlignmentOptions.Center;
        emptyStateLabel.color = new Color(1f, 1f, 1f, 0.6f);
        emptyGo.GetComponent<LayoutElement>().preferredHeight = 22;

        autoBuilt = true;
    }

    private RowEntry CreateRow()
    {
        // Row = horizontal layout with [Label ........... Countdown]
        GameObject rowGo = new GameObject("Row",
            typeof(RectTransform), typeof(LayoutElement), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(rowContainer, false);
        rowGo.GetComponent<LayoutElement>().preferredHeight = 26;

        HorizontalLayoutGroup hlg = rowGo.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 8;
        hlg.childAlignment = TextAnchor.MiddleLeft;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childControlHeight = true;
        hlg.childControlWidth = true;

        // Label
        GameObject labelGo = new GameObject("Label", typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
        labelGo.transform.SetParent(rowGo.transform, false);
        LayoutElement labelLe = labelGo.GetComponent<LayoutElement>();
        labelLe.flexibleWidth = 1;
        TextMeshProUGUI label = labelGo.GetComponent<TextMeshProUGUI>();
        label.fontSize = 16;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = Color.white;
        label.text = "—";

        // Countdown
        GameObject cdGo = new GameObject("Countdown", typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
        cdGo.transform.SetParent(rowGo.transform, false);
        LayoutElement cdLe = cdGo.GetComponent<LayoutElement>();
        cdLe.minWidth = 90;
        cdLe.preferredWidth = 90;
        TextMeshProUGUI cd = cdGo.GetComponent<TextMeshProUGUI>();
        cd.fontSize = 16;
        cd.alignment = TextAlignmentOptions.MidlineRight;
        cd.color = new Color(1f, 0.85f, 0.4f, 1f);
        cd.text = "00:00";

        return new RowEntry
        {
            gameObject = rowGo,
            label = label,
            countdown = cd,
        };
    }

    // ======================================================================
    //  Formatting
    // ======================================================================

    private static string FormatRemaining(double secondsRemaining)
    {
        if (secondsRemaining < 0) secondsRemaining = 0;

        int total = Mathf.FloorToInt((float)secondsRemaining);
        int h = total / 3600;
        int m = (total % 3600) / 60;
        int s = total % 60;

        if (h > 0)
        {
            return $"{h:D2}:{m:D2}:{s:D2}";
        }
        return $"{m:D2}:{s:D2}";
    }

    private static string Capitalize(string s)
    {
        if (string.IsNullOrEmpty(s)) return s;
        return char.ToUpperInvariant(s[0]) + s.Substring(1);
    }
}
