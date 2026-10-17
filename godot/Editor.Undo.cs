using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using Zeus.Sim;

// Undo/redo (Ctrl+Z, Ctrl+Y or Ctrl+Shift+Z) and fine moves with the arrow keys. A step is a full JSON snapshot of the model:
// tactics are small, and the snapshot keeps every field the editor does not know about.
public partial class Editor
{
    const int UndoLimit = 100;
    readonly List<string> _undo = new(), _redo = new();
    string? _lastUndoKey;           // edits with the same key in a row (one drag, one slider) form one undo step

    void PushUndo(string before, string? key)
    {
        if (key is not null && key == _lastUndoKey) return;
        _lastUndoKey = key;
        _undo.Add(before);
        if (_undo.Count > UndoLimit) _undo.RemoveAt(0);
        _redo.Clear();
    }

    void ClearUndo() { _undo.Clear(); _redo.Clear(); _lastUndoKey = null; }

    void Undo() => Step(_undo, _redo, "editor.undone");
    void Redo() => Step(_redo, _undo, "editor.redone");

    void Step(List<string> from, List<string> to, string message)
    {
        if (_model is null) return;
        if (from.Count == 0) { SetStatus(Loc.T("editor.nothing_to_undo"), false); return; }
        try
        {
            var restored = TacticEditorModel.Parse(from[^1]);
            to.Add(_model.ToJson());
            from.RemoveAt(from.Count - 1);
            _model = restored;
            _lastUndoKey = null;
            _dirty = true;
            SetSelection(Members().Where(_model.PlayerIds.Contains));
            RefreshAll();
            SetStatus(Loc.T(message), true);
        }
        catch (Exception e) { SetStatus(Loc.Error(e), false); }
    }

    /// <summary>Keyboard edits on the pitch; returns true when the key was used.</summary>
    bool EditorKey(InputEventKey key)
    {
        if (!key.Pressed) return false;
        bool ctrl = key.CtrlPressed || key.MetaPressed;
        if (ctrl && key.Keycode == Key.Z && !key.Echo) { if (key.ShiftPressed) Redo(); else Undo(); return true; }
        if (ctrl && key.Keycode == Key.Y && !key.Echo) { Redo(); return true; }
        if (!ctrl && key.Keycode == Key.G && !key.Echo) { SetSnap(!_snap); return true; }
        if (ctrl || Members().Count == 0) return false;
        // 1 m per press, 5 m with Shift; the pitch is drawn with attack to the right, so x is lengthways
        double step = key.ShiftPressed ? 5 : 1;
        var (dx, dy) = key.Keycode switch
        {
            Key.Left => (-step / L, 0.0), Key.Right => (step / L, 0.0),
            Key.Up => (0.0, -step / W), Key.Down => (0.0, step / W),
            _ => (0.0, 0.0)
        };
        if (dx == 0 && dy == 0) return false;
        var ids = Members();
        var from = ids.ToDictionary(id => id, id => _model.GetPosition(_phase, id));
        Try(() => MoveGroup(from, dx, dy), refresh: _model.IsAlias(_phase), undoKey: "nudge:" + string.Join(",", ids) + _phase);
        return true;
    }
}
