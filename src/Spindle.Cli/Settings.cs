namespace Spindle;

public class GeneratorSettings
{
    public string PlatformName { get; set; } = null!;

    public string DatabaseType { get; set; } = null!;

    public string DatabaseConnection { get; set; } = null!;

    public string TemplateFolder { get; set; } = null!;

    public string OutputFolder { get; set; } = null!;

    public bool OutputEntityFramework6 { get; set; }
}