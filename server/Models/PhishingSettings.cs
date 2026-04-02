namespace server.Models;

public class PhishingSettings
{
    public string[] TargetedBrands { get; set; } = Array.Empty<string>();
}
