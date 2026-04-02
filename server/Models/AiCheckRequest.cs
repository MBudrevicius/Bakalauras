namespace server.Models;

public class AiCheckRequest
{
    public string Text { get; set; } = string.Empty;

    /// <summary>
    /// Optional: the URL to extract page text from (used for full-page checks).
    /// If Text is provided, it takes priority.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// Optional: if true, include Claude AI model check (costs 1 credit per check).
    /// Default is false - only local checks are free.
    /// </summary>
    public bool UseClaudeAi { get; set; } = false;
}
