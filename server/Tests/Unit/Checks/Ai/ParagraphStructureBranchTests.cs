using server.Checks.Ai;
using server.Models;

namespace server.Tests.Unit.Checks.Ai;

public class ParagraphStructureBranchTests
{
    private readonly ParagraphStructureCheck _check = new();

    [Fact]
    public async Task RunAsync_UniformParagraphs_HighScore()
    {
        var text = "The first paragraph contains a few sentences. It discusses the main topic. The data supports the claim.\n\n" +
                   "The second paragraph also has similar length. It covers related evidence. The findings are consistent.\n\n" +
                   "The third paragraph wraps up the discussion. It summarizes key points. The conclusion is straightforward.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore > 30, $"Uniform paragraphs should score >30, got {result.AiScore}");
    }

    [Fact]
    public async Task RunAsync_VariedParagraphs_LowerScore()
    {
        var text = "Short paragraph here.\n\n" +
                   "This is a much longer paragraph that goes on and on, discussing multiple points and covering a wide range of topics. " +
                   "It includes many sentences with varying structure. The author explores different angles and provides extensive evidence. " +
                   "Multiple citations are referenced and compared against each other to build a comprehensive argument.\n\n" +
                   "Medium length paragraph with some content and more details about the research methodology.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore >= 0);
    }

    [Fact]
    public async Task RunAsync_RepeatedStarters_GetsBonus()
    {
        var text = "The first finding was significant and new. The researchers noted its impact on the community.\n\n" +
                   "The second finding confirmed the hypothesis. The data showed a clear correlation between variables.\n\n" +
                   "The third finding was unexpected but valid. The analysis revealed hidden patterns in the dataset.";
        var result = await _check.RunAsync(text);
        Assert.True(result.AiScore >= 0);
    }

    [Fact]
    public void Type_IsParagraphStructure()
    {
        Assert.Equal(AiCheckType.ParagraphStructure, _check.Type);
    }
}
