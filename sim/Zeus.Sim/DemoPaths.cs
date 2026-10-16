namespace Zeus.Sim;

/// <summary>Filesystem layout independent of Godot and the working directory.</summary>
public static class DemoPaths
{
    public static string DataRoot(bool editor, string resourceRoot, string executablePath) => Path.GetFullPath(editor
        ? Path.Combine(resourceRoot, "..") : Path.Combine(Path.GetDirectoryName(executablePath)!, "data"));
}
