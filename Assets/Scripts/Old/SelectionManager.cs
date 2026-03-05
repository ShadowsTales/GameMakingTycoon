using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;

public class SelectionManager
{
    private VisualElement _selectionBox;
    private Vector2 _startPos;
    private VisualElement _board;
    private List<VisualElement> _selectedNodes = new List<VisualElement>();

    public SelectionManager(VisualElement board)
    {
        _board = board;
    _selectionBox = new VisualElement();
    
    _selectionBox.style.position = Position.Absolute;
    _selectionBox.style.backgroundColor = new Color(0, 0.5f, 1, 0.1f);
    
    // Statt .borderColor nutzen wir die Einzelzuweisung
    Color borderCol = new Color(0, 0.8f, 1, 0.5f);
    _selectionBox.style.borderTopColor = borderCol;
    _selectionBox.style.borderBottomColor = borderCol;
    _selectionBox.style.borderLeftColor = borderCol;
    _selectionBox.style.borderRightColor = borderCol;

    // Statt .borderWidth nutzen wir die Einzelzuweisung
    _selectionBox.style.borderTopWidth = 1;
    _selectionBox.style.borderBottomWidth = 1;
    _selectionBox.style.borderLeftWidth = 1;
    _selectionBox.style.borderRightWidth = 1;

    // WICHTIG: Das Board muss Klicks erlauben
    _board.pickingMode = PickingMode.Position;
    _selectionBox.pickingMode = PickingMode.Ignore;
    _selectionBox.style.visibility = Visibility.Hidden;
    _board.Add(_selectionBox);

        _board.RegisterCallback<PointerDownEvent>(OnDown);
        _board.RegisterCallback<PointerMoveEvent>(OnMove);
        _board.RegisterCallback<PointerUpEvent>(OnUp);
    }

    private void OnDown(PointerDownEvent evt)
    {
        if (evt.button != 0) return; // Nur Linksklick
        _startPos = evt.localPosition;
        _selectionBox.style.visibility = Visibility.Visible;
        _selectionBox.style.left = _startPos.x;
        _selectionBox.style.top = _startPos.y;
        _selectionBox.style.width = 0;
        _selectionBox.style.height = 0;
        _board.CapturePointer(evt.pointerId);
    }

    private void OnMove(PointerMoveEvent evt)
    {
        if (!_board.HasPointerCapture(evt.pointerId)) return;

        Vector2 currentPos = evt.localPosition;
        float width = Mathf.Abs(currentPos.x - _startPos.x);
        float height = Mathf.Abs(currentPos.y - _startPos.y);

        _selectionBox.style.left = Mathf.Min(currentPos.x, _startPos.x);
        _selectionBox.style.top = Mathf.Min(currentPos.y, _startPos.y);
        _selectionBox.style.width = width;
        _selectionBox.style.height = height;
    }

    private void OnUp(PointerUpEvent evt)
    {
        _selectionBox.style.visibility = Visibility.Hidden;
        _board.ReleasePointer(evt.pointerId);
        // Hier käme die Logik: Welche Nodes liegen im Rect?
    }
}