using System;
using System.IO;
using Godot;
using Zeus.Sim;

public partial class Editor
{
    const int OpenImportId = 100002, OpenExportId = 100003, OpenCodeId = 100004;
    FileDialog? _importDialog, _exportDialog;

    // Share code: the same JSON, deflated and base64url-packed with a version and checksum, so it survives chat and mail as one line.
    void CopyShareCode()
    {
        if (_model is null) return;
        try
        {
            DisplayServer.ClipboardSet(TacticExchange.ToCode(_model));
            SetStatus(Loc.T("editor.code_copied"), true);
        }
        catch (Exception e) { SetStatus(Loc.T("editor.copy_failed") + Loc.Error(e), false); }
    }

    // Shared with the tactic library overlay: the parent only hosts the dialog.
    public static FileDialog TacticDialog(Node parent, FileDialog.FileModeEnum mode, string title)
    {
        FilePickerLanguage.Apply();
        var dialog = new FileDialog
        {
            Access = FileDialog.AccessEnum.Filesystem,
            FileMode = mode,
            Title = title,
            Filters = new[] { "*.json ; " + Loc.T("editor.tactic_file_filter") },
            Theme = UiTheme.Create(),
            MinSize = new Vector2I(640, 360)
        };
        dialog.SetTranslationDomain(FilePickerLanguage.Domain);
        dialog.GetCancelButton().Text = Loc.T("common.cancel");
        var documents = OS.GetSystemDir(OS.SystemDir.Documents);
        dialog.CurrentDir = Directory.Exists(documents) ? documents : OS.GetUserDataDir();
        parent.AddChild(dialog);
        return dialog;
    }

    void ShowTacticImport()
    {
        if (_importDialog is null)
        {
            _importDialog = TacticDialog(this, FileDialog.FileModeEnum.OpenFile, Loc.T("editor.import_file"));
            _importDialog.GetOkButton().Text = Loc.T("editor.import_file");
            _importDialog.FileSelected += ImportTacticFile;
        }
        _importDialog.PopupCenteredRatio(0.75f);
    }

    void ImportTacticFile(string path)
    {
        try
        {
            // Validate before asking to discard edits. A bad file leaves the current tactic untouched.
            var model = TacticExchange.Import(path);
            var stem = TacticExchange.AvailableName(model.Name, GameSession.WritableTacticsDir());
            Guard(() =>
            {
                _sourcePath = null;
                SetModel(model, stem);
                _dirty = true;
                SetStatus(Loc.T("editor.imported_file", Path.GetFileName(path)), true);
            });
        }
        catch (Exception e) { SetStatus(Loc.T("editor.import_failed") + Loc.Error(e), false); }
    }

    void ShowTacticExport()
    {
        if (_model is null) return;
        if (_exportDialog is null)
        {
            _exportDialog = TacticDialog(this, FileDialog.FileModeEnum.SaveFile, Loc.T("editor.export_file"));
            _exportDialog.GetOkButton().Text = Loc.T("editor.export_file");
            _exportDialog.FileSelected += ExportTacticFile;
        }
        var name = TacticFileNames.Sanitize(_name.Text);
        _exportDialog.CurrentFile = (name.Length > 0 ? name : "tactic") + ".json";
        _exportDialog.PopupCenteredRatio(0.75f);
    }

    void ExportTacticFile(string path)
    {
        try
        {
            TacticExchange.Export(_model, path);
            // Export is a copy: it must not mark unsaved editor changes as saved.
            SetStatus(Loc.T("editor.exported_file", Path.GetFileName(path)), true);
        }
        catch (Exception e) { SetStatus(Loc.T("editor.export_failed") + Loc.Error(e), false); }
    }
}
