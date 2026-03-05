using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Horizontales Panning + Scrollbar-Leiste. Kein Zoom.
/// Pan: Linksklick-Drag auf dem Hintergrund, Mausrad scrollt horizontal.
/// </summary>
public class NodeBoardView
{
    private readonly VisualElement _viewport;
    private readonly VisualElement _content;
    private readonly VisualElement _thumb;
    private readonly VisualElement _track;

    private bool  _isPanning;
    private float _panStartMouseX;
    private float _panStartContentX;

    private bool  _isThumbDragging;
    private float _thumbDragStartMouseX;
    private float _thumbDragStartContentX;

    public float TotalContentWidth { get; set; } = 4000f;

    public NodeBoardView(VisualElement viewport, VisualElement thumb, VisualElement track)
    {
        _viewport = viewport;
        _content  = viewport.Q("GridContent");
        _thumb    = thumb;
        _track    = track;

        _viewport.RegisterCallback<PointerDownEvent>(OnViewportDown, TrickleDown.TrickleDown);
        _viewport.RegisterCallback<PointerMoveEvent>(OnViewportMove, TrickleDown.TrickleDown);
        _viewport.RegisterCallback<PointerUpEvent>  (OnViewportUp,   TrickleDown.TrickleDown);
        _viewport.RegisterCallback<WheelEvent>      (OnWheel,        TrickleDown.TrickleDown);

        _thumb.RegisterCallback<PointerDownEvent>(OnThumbDown);
        _thumb.RegisterCallback<PointerMoveEvent>(OnThumbMove);
        _thumb.RegisterCallback<PointerUpEvent>  (OnThumbUp);
        _track.RegisterCallback<PointerDownEvent>(OnTrackClick);
    }

    // ── Viewport-Pan ─────────────────────────────────────────────────
    private void OnViewportDown(PointerDownEvent evt)
    {
        if (evt.button != 0 && evt.button != 1 && evt.button != 2) return;
        _isPanning          = true;
        _panStartMouseX     = evt.position.x;
        _panStartContentX   = _content.resolvedStyle.left;
        _viewport.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnViewportMove(PointerMoveEvent evt)
    {
        if (!_isPanning || !_viewport.HasPointerCapture(evt.pointerId)) return;
        float newX = ClampX(_panStartContentX + evt.position.x - _panStartMouseX);
        _content.style.left = newX;
        UpdateScrollbar();
    }

    private void OnViewportUp(PointerUpEvent evt)
    {
        if (!_isPanning) return;
        _isPanning = false;
        if (_viewport.HasPointerCapture(evt.pointerId))
            _viewport.ReleasePointer(evt.pointerId);
    }

    private void OnWheel(WheelEvent evt)
    {
        float newX = ClampX(_content.resolvedStyle.left - evt.delta.y * 1.4f);
        _content.style.left = newX;
        UpdateScrollbar();
        evt.StopPropagation();
    }

    // ── Scrollbar-Thumb-Drag ─────────────────────────────────────────
    private void OnThumbDown(PointerDownEvent evt)
    {
        _isThumbDragging        = true;
        _thumbDragStartMouseX   = evt.position.x;
        _thumbDragStartContentX = _content.resolvedStyle.left;
        _thumb.CapturePointer(evt.pointerId);
        evt.StopPropagation();
    }

    private void OnThumbMove(PointerMoveEvent evt)
    {
        if (!_isThumbDragging || !_thumb.HasPointerCapture(evt.pointerId)) return;

        float trackW   = _track.resolvedStyle.width;
        float thumbW   = _thumb.resolvedStyle.width;
        float maxThumb = trackW - thumbW;
        if (maxThumb <= 0f) return;

        float dx    = evt.position.x - _thumbDragStartMouseX;
        float frac  = dx / maxThumb;
        float maxS  = TotalContentWidth - _viewport.resolvedStyle.width;
        float newX  = ClampX(_thumbDragStartContentX - frac * maxS);

        _content.style.left = newX;
        UpdateScrollbar();
    }

    private void OnThumbUp(PointerUpEvent evt)
    {
        if (!_isThumbDragging) return;
        _isThumbDragging = false;
        if (_thumb.HasPointerCapture(evt.pointerId))
            _thumb.ReleasePointer(evt.pointerId);
    }

    // ── Track-Klick ──────────────────────────────────────────────────
    private void OnTrackClick(PointerDownEvent evt)
    {
        if (evt.target == _thumb) return;
        float trackW = _track.resolvedStyle.width;
        float thumbW = _thumb.resolvedStyle.width;
        float clickX = _track.WorldToLocal(evt.position).x;
        float frac   = Mathf.Clamp01((clickX - thumbW * 0.5f) / (trackW - thumbW));
        float maxS   = TotalContentWidth - _viewport.resolvedStyle.width;
        _content.style.left = ClampX(-(frac * maxS));
        UpdateScrollbar();
        evt.StopPropagation();
    }

    // ── Scrollbar aktualisieren ──────────────────────────────────────
    public void UpdateScrollbar()
    {
        float viewW  = _viewport.resolvedStyle.width;
        float trackW = _track.resolvedStyle.width;
        float total  = TotalContentWidth;
        if (viewW <= 0f || trackW <= 0f || total <= 0f) return;

        float ratio  = Mathf.Clamp01(viewW / total);
        float thumbW = Mathf.Max(36f, trackW * ratio);
        float maxS   = total - viewW;
        float scrolled = Mathf.Max(0f, -_content.resolvedStyle.left);
        float thumbX = maxS > 0f ? (scrolled / maxS) * (trackW - thumbW) : 0f;

        _thumb.style.width = thumbW;
        _thumb.style.left  = thumbX;
    }

    private float ClampX(float x)
    {
        float minX = -(TotalContentWidth - _viewport.resolvedStyle.width);
        return Mathf.Clamp(x, Mathf.Min(0f, minX), 0f);
    }

    public VisualElement GetSpawnArea() => _content;
}