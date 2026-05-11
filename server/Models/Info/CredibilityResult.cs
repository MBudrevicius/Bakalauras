namespace server.Models;

public class CredibilityResult
{
    public int Score { get; set; }
    public string Verdict { get; set; } = string.Empty;
    public List<string> Claims { get; set; } = [];
}
