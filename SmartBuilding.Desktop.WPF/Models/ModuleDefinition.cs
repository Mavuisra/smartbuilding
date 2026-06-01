namespace SmartBuilding.Desktop.WPF.Models;

public record ModuleDefinition(
    string Id,
    string Title,
    string Subtitle,
    string IconKind,
    string Section,
    string? PermissionCode = null);

public record ModuleDataResult(
    IReadOnlyList<string> Headers,
    IReadOnlyList<ModuleListRow> Rows,
    int TotalCount);

public abstract class ShellNavEntry;

public sealed class ShellNavSectionHeader(string label) : ShellNavEntry
{
    public string Label { get; } = label;
}

public sealed class ShellNavModuleItem(ModuleDefinition Module, string? displayTitle = null) : ShellNavEntry
{
    public ModuleDefinition Module { get; } = Module;
    public string Id => Module.Id;
    public string Title => displayTitle ?? Module.Title;
    public string IconKind => Module.IconKind;
}

public sealed class ShellNavChildItem(string id, string title)
{
    public string Id { get; } = id;
    public string Title { get; } = title;
}

public sealed class ShellNavExpandableModuleItem(ModuleDefinition module, IReadOnlyList<ShellNavChildItem> children) : ShellNavEntry
{
    public ModuleDefinition Module { get; } = module;
    public string Id => Module.Id;
    public string Title => Module.Title;
    public string IconKind => Module.IconKind;
    public IReadOnlyList<ShellNavChildItem> Children { get; } = children;
    public bool IsExpanded { get; set; } = true;
}
