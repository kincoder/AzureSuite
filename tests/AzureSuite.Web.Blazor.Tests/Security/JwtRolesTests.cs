using System.Text;
using AzureSuite.Web.Blazor.Security;
using FluentAssertions;

namespace AzureSuite.Web.Blazor.Tests.Security
{
    public class JwtRolesTests
    {
        internal static string TokenWithPayload(string json)
        {
            static string Encode(string s) => Convert.ToBase64String(Encoding.UTF8.GetBytes(s))
                .TrimEnd('=').Replace('+', '-').Replace('/', '_');
            return $"{Encode("{\"alg\":\"none\"}")}.{Encode(json)}.signature";
        }

        [Fact]
        public void Read_ReturnsEveryRoleInThePayload()
        {
            var token = TokenWithPayload("{\"roles\":[\"Catalog.Reader\",\"Messages.Submit\"]}");

            JwtRoles.Read(token).Should().Equal("Catalog.Reader", "Messages.Submit");
        }

        [Fact]
        public void Read_WithNoRolesClaim_ReturnsEmpty()
        {
            JwtRoles.Read(TokenWithPayload("{\"name\":\"Oscar\"}")).Should().BeEmpty();
        }

        [Fact]
        public void Read_HandlesPayloadsThatNeedBase64Padding()
        {
            // 1-char and 2-char remainders exercise both padding branches.
            JwtRoles.Read(TokenWithPayload("{\"roles\":[\"A\"]}")).Should().Equal("A");
            JwtRoles.Read(TokenWithPayload("{\"roles\":[\"AB\"]}")).Should().Equal("AB");
        }

        [Theory]
        [InlineData("")]
        [InlineData("not-a-jwt")]
        [InlineData("a.%%%.c")]
        public void Read_WithAMalformedToken_ReturnsEmpty(string token)
        {
            JwtRoles.Read(token).Should().BeEmpty();
        }
    }
}
