using FactoryMind.Api.Routing;

namespace FactoryMind.Tests;

public sealed class ApiRoutesTests {
    [Fact]
    public void Business_route_groups_keep_the_existing_API_prefix() {
        Assert.Equal("/api", ApiRoutes.Base);
        Assert.Equal("/api/auth", ApiRoutes.Auth.Group);
        Assert.Equal("/api/conversations", ApiRoutes.Conversations.Group);
        Assert.Equal("/api/documents", ApiRoutes.Documents.Group);
    }

    [Fact]
    public void Child_routes_are_defined_once_and_compose_with_their_group() {
        Assert.Equal("/api/auth/login", ApiRoutes.Auth.Group + ApiRoutes.Auth.Login);
        Assert.Equal(
            "/api/conversations/{conversationId:guid}/messages/stream",
            ApiRoutes.Conversations.Group + ApiRoutes.Conversations.StreamMessage);
        Assert.Equal(
            "/api/documents/{documentId:guid}/process",
            ApiRoutes.Documents.Group + ApiRoutes.Documents.Process);
    }
}
