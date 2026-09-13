using System.Collections.Generic;
using UnityEngine;

/// General-purpose replacement for GridLayoutGroup + ContentSizeFitter, with game-specific shapes
/// (row/column/grid/circle/auto-polygon). Scans active direct children in sibling order, positions
/// them centered on (0,0), then resizes itself and/or a target RectTransform to fit + padding.
/// Call Relayout() whenever the active child set changes (pooling, popup open, etc.) - this does NOT
/// hook into Unity's automatic layout rebuild system, so timing is fully caller-controlled.
[AddComponentMenu("GRYNSOFT/UI/Simple Grid")]
[ExecuteAlways]
public class SimpleGrid : MonoBehaviour
{
    public enum Shape { Auto, Grid, Row, Column, Circle }

    public enum GridAnchor
    {
        TopLeft, TopCenter, TopRight,
        MiddleLeft, MiddleCenter, MiddleRight,
        BottomLeft, BottomCenter, BottomRight
    }

    [Header("Shape")]
    public Shape ArrangementShape = Shape.Auto;
    [Tooltip("Grid mode only. 0 = auto (as square as possible).")]
    public int Columns = 0;
    [Tooltip("Which point of the arranged block coincides with this RectTransform's own pivot - i.e. which corner/edge the block grows away from. MiddleCenter grows symmetrically (default).")]
    public GridAnchor Anchor = GridAnchor.MiddleCenter;

    [Header("Sizing")]
    [Tooltip("Target distance between adjacent item centers - used by Auto/Circle/Row/Column.")]
    public float ItemDistance = 140f;
    [Tooltip("Row/column gap - used by Grid mode.")]
    public Vector2 Spacing = new Vector2(140f, 140f);
    [Tooltip("Extra margin added around the computed bounding box when resizing.")]
    public Vector2 Padding = new Vector2(40f, 40f);
    [Tooltip("If true, item size is read from a direct child named 'MainObjectName' instead of the item's own RectTransform. Use when the root is inflated by nested content such as a SimpleMenu.")]
    public bool UseMainObjectSize = false;
    [Tooltip("Name of the direct child to use for sizing when UseMainObjectSize is enabled.")]
    public string MainObjectName = "Main";

    [Header("Resize Target")]
    public bool ResizeSelf = true;
    public RectTransform ResizeTarget;

    [HideInInspector] public Vector2 LastComputedSize;

    private RectTransform _rect;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
    }

    void OnEnable()
    {
        Relayout();
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        //DEFER - avoid calling into RectTransform layout during the inspector's own OnValidate pass
        UnityEditor.EditorApplication.delayCall += () =>
        {
            if (this != null) Relayout();
        };
    }
#endif

    void OnTransformChildrenChanged()
    {
        Relayout();
    }

    /// Re-scans active direct children, repositions them per the current Shape, and resizes.
    public void Relayout()
    {
        if (_rect == null) _rect = GetComponent<RectTransform>();

        var items = new List<RectTransform>();
        for (int i = 0; i < transform.childCount; i++)
        {
            var child = transform.GetChild(i);
            if (!child.gameObject.activeSelf) continue;
            if (child is RectTransform rt) items.Add(rt);
        }

        //NORMALIZE - anchoredPosition (0,0) is only Content's true center if the item itself is
        //center-anchored/pivoted. Force that so positioning math below is correct regardless of
        //whatever anchor/pivot the prefab happens to be set up with.
        var center = new Vector2(0.5f, 0.5f);
        foreach (var rt in items)
        {
            if (rt.anchorMin == center && rt.anchorMax == center && rt.pivot == center) continue;
            var itemSize = rt.rect.size;
            rt.anchorMin = center;
            rt.anchorMax = center;
            rt.pivot = center;
            rt.sizeDelta = itemSize;
        }

        int n = items.Count;
        if (n == 0)
        {
            LastComputedSize = Vector2.zero;
            return;
        }

        var sizes = new List<Vector2>(n);
        for (int i = 0; i < n; i++)
            sizes.Add(GetItemSize(items[i]));

        List<Vector2> positions;
        switch (ArrangementShape)
        {
            case Shape.Grid:
                positions = ComputeGridPositions(n, Columns, Spacing, sizes);
                break;
            case Shape.Row:
                positions = ComputeLinePositions(n, horizontal: true, ItemDistance);
                break;
            case Shape.Column:
                positions = ComputeLinePositions(n, horizontal: false, ItemDistance);
                break;
            case Shape.Circle:
                positions = ComputePolygonPositions(n, ItemDistance);
                break;
            default:
                positions = ComputeAutoPositions(n, ItemDistance);
                break;
        }

        ComputeBounds(items, sizes, positions, out var min, out var max);
        var paddedMin = min - Padding;
        var paddedMax = max + Padding;
        var anchorPoint = GetAnchorPoint(paddedMin, paddedMax, Anchor);

        //SHIFT - so the chosen anchor point of the block lands on this RectTransform's own pivot (local origin)
        for (int i = 0; i < n; i++)
        {
            var basePos = positions[i] - anchorPoint;
            var selectable = items[i].GetComponent<CursorSelectable>();
            if (selectable != null)
            {
                selectable.SavedAnchoredPosition = basePos;
                items[i].anchoredPosition = basePos + selectable.PositionOffset;
            }
            else
            {
                items[i].anchoredPosition = basePos;
            }
        }

        var size = paddedMax - paddedMin;
        LastComputedSize = size;
        if (ResizeSelf && _rect != null) _rect.sizeDelta = size;
        if (ResizeTarget != null) ResizeTarget.sizeDelta = size;
    }

    private static Vector2 GetAnchorPoint(Vector2 min, Vector2 max, GridAnchor anchor)
    {
        float x = anchor switch
        {
            GridAnchor.TopLeft or GridAnchor.MiddleLeft or GridAnchor.BottomLeft => min.x,
            GridAnchor.TopRight or GridAnchor.MiddleRight or GridAnchor.BottomRight => max.x,
            _ => (min.x + max.x) * 0.5f,
        };
        float y = anchor switch
        {
            GridAnchor.TopLeft or GridAnchor.TopCenter or GridAnchor.TopRight => max.y,
            GridAnchor.BottomLeft or GridAnchor.BottomCenter or GridAnchor.BottomRight => min.y,
            _ => (min.y + max.y) * 0.5f,
        };
        return new Vector2(x, y);
    }

    private Vector2 GetItemSize(RectTransform item)
    {
        if (!UseMainObjectSize) return item.rect.size;
        var main = item.Find(MainObjectName);
        if (main != null && main is RectTransform mainRt) return mainRt.rect.size;
        return item.rect.size;
    }

    private static void ComputeBounds(List<RectTransform> items, List<Vector2> sizes, List<Vector2> positions, out Vector2 min, out Vector2 max)
    {
        min = new Vector2(float.MaxValue, float.MaxValue);
        max = new Vector2(float.MinValue, float.MinValue);
        for (int i = 0; i < items.Count; i++)
        {
            var half = sizes[i] * 0.5f;
            min = Vector2.Min(min, positions[i] - half);
            max = Vector2.Max(max, positions[i] + half);
        }
    }

    private static List<Vector2> ComputeAutoPositions(int n, float spacing)
    {
        var result = new List<Vector2>(n);
        switch (n)
        {
            case 1:
                result.Add(Vector2.zero);
                break;
            case 2:
                result.Add(new Vector2(-spacing * 0.5f, 0f));
                result.Add(new Vector2(spacing * 0.5f, 0f));
                break;
            case 3:
                {
                    float h = spacing * Mathf.Sqrt(3f) * 0.5f;
                    result.Add(new Vector2(0f, h * 2f / 3f));
                    result.Add(new Vector2(-spacing * 0.5f, -h / 3f));
                    result.Add(new Vector2(spacing * 0.5f, -h / 3f));
                    break;
                }
            case 4:
                result.Add(new Vector2(-spacing * 0.5f, spacing * 0.5f));
                result.Add(new Vector2(spacing * 0.5f, spacing * 0.5f));
                result.Add(new Vector2(-spacing * 0.5f, -spacing * 0.5f));
                result.Add(new Vector2(spacing * 0.5f, -spacing * 0.5f));
                break;
            default:
                result.AddRange(ComputePolygonPositions(n, spacing));
                break;
        }
        return result;
    }

    private static List<Vector2> ComputePolygonPositions(int n, float spacing)
    {
        var result = new List<Vector2>(n);
        if (n <= 1)
        {
            result.Add(Vector2.zero);
            return result;
        }
        float radius = spacing / (2f * Mathf.Sin(Mathf.PI / n));
        const float startAngleDeg = 90f;
        for (int i = 0; i < n; i++)
        {
            float angleDeg = startAngleDeg - i * (360f / n);
            float angleRad = angleDeg * Mathf.Deg2Rad;
            result.Add(new Vector2(radius * Mathf.Cos(angleRad), radius * Mathf.Sin(angleRad)));
        }
        return result;
    }

    private static List<Vector2> ComputeLinePositions(int n, bool horizontal, float spacing)
    {
        var result = new List<Vector2>(n);
        float total = (n - 1) * spacing;
        for (int i = 0; i < n; i++)
        {
            float t = -total * 0.5f + i * spacing;
            result.Add(horizontal ? new Vector2(t, 0f) : new Vector2(0f, -t));
        }
        return result;
    }

    private static List<Vector2> ComputeGridPositions(int n, int columns, Vector2 spacing, List<Vector2> sizes)
    {
        int cols = columns > 0 ? columns : Mathf.Max(1, Mathf.CeilToInt(Mathf.Sqrt(n)));
        int rows = Mathf.CeilToInt((float)n / cols);

        var cell = Vector2.zero;
        foreach (var s in sizes) cell = Vector2.Max(cell, s);
        var step = cell + spacing;

        var result = new List<Vector2>(n);
        float totalHeight = (rows - 1) * step.y;
        for (int i = 0; i < n; i++)
        {
            int row = i / cols;
            int col = i % cols;
            int itemsInRow = Mathf.Min(cols, n - row * cols);
            float rowWidth = (itemsInRow - 1) * step.x;
            float x = -rowWidth * 0.5f + col * step.x;
            float y = totalHeight * 0.5f - row * step.y;
            result.Add(new Vector2(x, y));
        }
        return result;
    }
}
