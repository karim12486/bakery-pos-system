using Xunit;

namespace Nizam.Api.Tests
{
    [CollectionDefinition("SharedTestCollection")]
    public class SharedTestCollection : ICollectionFixture<CustomWebApplicationFactory<Program>>
    {
        // This class has no code, it's just here to apply the [CollectionDefinition] and define the ICollectionFixture
    }
}