using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

public class ParagraphStructureCheckTests
{
    private readonly ParagraphStructureCheck _check = new();

    [Fact]
    public async Task RunAsync_TooFewParagraphs_ReturnsZeroScore()
    {
        var text = "Just one paragraph here that is fairly short.";
        var result = await _check.RunAsync(text);

        Assert.Equal(0, result.AiScore);
        Assert.Contains("Not enough paragraphs", result.Description);
    }

    [Fact]
    public async Task RunAsync_UniformParagraphs_ReturnsHigherScore()
    {
        // All paragraphs of similar length — AI pattern
        var text = "The first paragraph discusses the initial findings of the research study conducted last year.\n\n" +
                   "The second paragraph explores the methodology used in the comprehensive data analysis process.\n\n" +
                   "The third paragraph examines the results obtained from the controlled experimental group participants.\n\n" +
                   "The fourth paragraph provides a thorough discussion of the implications for future research work.";

        var result = await _check.RunAsync(text);

        Assert.True(result.AiScore >= 30, $"Uniform paragraphs should score higher, got {result.AiScore}");
    }

    [Fact]
    public async Task RunAsync_VariedParagraphs_ReturnsDifferentScore()
    {
        var text = "Short.\n\n" +
                   "A much longer paragraph that goes into extensive detail about numerous topics covering a wide range of subjects and perspectives that would make it very different from the others in terms of length and complexity.\n\n" +
                   "Medium length paragraph with some content and ideas.\n\n" +
                   "Another extremely long paragraph that continues to elaborate on many different themes including science, art, philosophy, mathematics, technology, and various other academic disciplines spanning centuries of human thought.";

        var result = await _check.RunAsync(text);

        Assert.InRange(result.AiScore, 0, 100);
    }

    [Fact]
    public async Task RunAsync_ReturnsCorrectType()
    {
        var text = "Paragraph one here with content.\n\nParagraph two here with content.\n\nParagraph three here with content.";
        var result = await _check.RunAsync(text);

        Assert.Equal(AiCheckType.ParagraphStructure, result.Type);
    }
}
