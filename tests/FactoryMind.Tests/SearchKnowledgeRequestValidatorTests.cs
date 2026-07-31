using FactoryMind.Api.Endpoints;
using FactoryMind.Application.Features.Knowledge;

namespace FactoryMind.Tests;

public sealed class SearchKnowledgeRequestValidatorTests {
    private readonly SearchKnowledgeRequestValidator _validator = new();

    [Fact]
    public async Task Valid_request_uses_the_default_limit() {
        var request = new SearchKnowledgeRequest { Query = "machine safety" };

        var result = await _validator.ValidateAsync(request);

        Assert.True(result.IsValid);
        Assert.Equal(KnowledgeSearchConstraints.DefaultLimit, request.Limit);
    }

    [Theory]
    [InlineData("", 5)]
    [InlineData("question", 0)]
    [InlineData("question", 21)]
    public async Task Invalid_request_is_rejected(string query, int limit) {
        var request = new SearchKnowledgeRequest { Query = query, Limit = limit };

        var result = await _validator.ValidateAsync(request);

        Assert.False(result.IsValid);
    }
}
