using FluentAssertions;
using NexusWorks.Guardian;

namespace NexusWorks.Guardian.Tests;

[Trait("Category", "Infrastructure")]
public class GuardianAssemblyMarkerTests
{
    [Fact]
    public void Marker_should_expose_guardian_assembly_identity()
    {
        var assemblyName = typeof(GuardianAssemblyMarker).Assembly.GetName().Name;

        assemblyName.Should().Be("NexusWorks.Guardian");
    }
}
