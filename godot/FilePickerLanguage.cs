using Godot;

/// <summary>Supplies runtime translations for the engine's built-in file picker labels.</summary>
public static class FilePickerLanguage
{
    public const string Domain = "gaffers.file_picker";
    static Translation? _translation;

    public static void Apply()
    {
        var domain = TranslationServer.GetOrAddDomain(Domain);
        if (_translation is not null) domain.RemoveTranslation(_translation);
        _translation = new Translation { Locale = Loc.Language };
        _translation.AddMessage("Path:", Loc.T("file.path"));
        _translation.AddMessage("Favorites:", Loc.T("file.favorites"));
        _translation.AddMessage("Recent:", Loc.T("file.recent"));
        _translation.AddMessage("Directories & Files:", Loc.T("file.contents"));
        _translation.AddMessage("File:", Loc.T("file.filename"));
        _translation.AddMessage("Cancel", Loc.T("common.cancel"));
        _translation.AddMessage("OK", Loc.T("common.ok"));
        _translation.AddMessage("Please Confirm...", Loc.T("file.confirm_title"));
        _translation.AddMessage("Alert!", Loc.T("file.alert_title"));
        _translation.AddMessage("Warning!", Loc.T("file.warning_title"));
        _translation.AddMessage("Error!", Loc.T("file.error_title"));
        _translation.AddMessage("Cut", Loc.T("file.cut"));
        _translation.AddMessage("Copy", Loc.T("file.copy"));
        _translation.AddMessage("Paste", Loc.T("file.paste"));
        _translation.AddMessage("Select All", Loc.T("file.select_all"));
        _translation.AddMessage("Undo", Loc.T("file.undo"));
        _translation.AddMessage("Redo", Loc.T("file.redo"));
        _translation.AddMessage("Clear", Loc.T("file.clear"));
        _translation.AddMessage("Open", Loc.T("editor.open"));
        _translation.AddMessage("Save", Loc.T("editor.save"));
        _translation.AddMessage("Go to previous folder.", Loc.T("file.previous"));
        _translation.AddMessage("Go to next folder.", Loc.T("file.next"));
        _translation.AddMessage("Go to parent folder.", Loc.T("file.parent"));
        _translation.AddMessage("Refresh files.", Loc.T("file.refresh"));
        _translation.AddMessage("(Un)favorite current folder.", Loc.T("file.favorite_toggle"));
        _translation.AddMessage("Create a new folder.", Loc.T("file.create_help"));
        _translation.AddMessage("Create Folder", Loc.T("file.create"));
        _translation.AddMessage("Name:", Loc.T("file.name"));
        _translation.AddMessage("Filter:", Loc.T("file.filter"));
        _translation.AddMessage("Filename Filter:", Loc.T("file.filename_filter"));
        _translation.AddMessage("Drive", Loc.T("file.drive"));
        _translation.AddMessage("Toggle the visibility of hidden files.", Loc.T("file.hidden"));
        _translation.AddMessage("View items as a grid of thumbnails.", Loc.T("file.grid"));
        _translation.AddMessage("View items as a list.", Loc.T("file.list"));
        _translation.AddMessage("Toggle the visibility of the filter for file names.", Loc.T("file.filter_toggle"));
        _translation.AddMessage("Sort files", Loc.T("file.sort"));
        _translation.AddMessage("Sort by Name (Ascending)", Loc.T("file.sort_name_asc"));
        _translation.AddMessage("Sort by Name (Descending)", Loc.T("file.sort_name_desc"));
        _translation.AddMessage("Sort by Type (Ascending)", Loc.T("file.sort_type_asc"));
        _translation.AddMessage("Sort by Type (Descending)", Loc.T("file.sort_type_desc"));
        _translation.AddMessage("Sort by Modified Time (Newest First)", Loc.T("file.sort_newest"));
        _translation.AddMessage("Sort by Modified Time (Oldest First)", Loc.T("file.sort_oldest"));
        _translation.AddMessage("Could not create folder.", Loc.T("file.create_failed"));
        _translation.AddMessage("Invalid extension, or empty filename.", Loc.T("file.invalid_name"));
        _translation.AddMessage("File \"%s\" already exists.\nDo you want to overwrite it?", Loc.T("file.overwrite"));
        _translation.AddMessage("Delete the selected file?\nDepending on your filesystem configuration, the files will either be moved to the system trash or deleted permanently.", Loc.T("file.delete_confirm"));
        _translation.AddMessage("Delete", Loc.T("file.delete"));
        _translation.AddMessage("Refresh", Loc.T("file.refresh_short"));
        _translation.AddMessage("New Folder...", Loc.T("file.new_folder"));
        _translation.AddMessage("Show Hidden Files", Loc.T("file.show_hidden"));
        _translation.AddMessage("Open in File Manager", Loc.T("file.file_manager"));
        _translation.AddMessage("Copy Path", Loc.T("file.copy_path"));
        _translation.AddMessage("All Files (*)", Loc.T("file.all_files"));
        _translation.AddMessage("All Recognized", Loc.T("file.all_recognized"));
        domain.AddTranslation(_translation);
        TranslationServer.SetLocale(Loc.Language);
    }
}
