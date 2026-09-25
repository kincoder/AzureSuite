using AzureSuite.Catalog.Domain.Entities;
using AzureSuite.Catalog.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Catalog.Infrastructure.Tests.Persistence
{
    public class CatalogDbContextTests
    {
        private static IReadOnlyList<Microsoft.EntityFrameworkCore.Metadata.IForeignKey> RouteForeignKeys()
        {
            var options = new DbContextOptionsBuilder<CatalogDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            using var context = new CatalogDbContext(options);
            return context.Model.FindEntityType(typeof(Route))!.GetForeignKeys().ToList();
        }

        [Fact]
        public void Route_HasForeignKeyToClient_CascadingOnDelete()
        {
            var foreignKey = RouteForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(Client));

            foreignKey.Properties.Select(p => p.Name).Should().Equal(nameof(Route.ClientId));
            foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        }

        [Fact]
        public void Route_HasForeignKeyToMessageTypeNameAndVersion_RestrictingDelete()
        {
            var foreignKey = RouteForeignKeys().Single(fk => fk.PrincipalEntityType.ClrType == typeof(MessageType));

            foreignKey.Properties.Select(p => p.Name).Should().Equal(nameof(Route.MessageTypeName), nameof(Route.MessageTypeVersion));
            foreignKey.PrincipalKey.Properties.Select(p => p.Name).Should().Equal(nameof(MessageType.Name), nameof(MessageType.Version));
            foreignKey.DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        }
    }
}
