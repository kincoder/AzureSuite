using System.ComponentModel.DataAnnotations;
using System.Reflection;
using AzureSuite.Application.Messages;
using FluentAssertions;

namespace AzureSuite.Application.Tests.Messages;

/// <summary>
/// Verifies validation attributes on CreatePacs008MessageRequest's primary constructor
/// parameters directly via reflection, rather than via
/// System.ComponentModel.DataAnnotations.Validator.TryValidateObject.
///
/// Why: ASP.NET Core's MVC model binder reads validation metadata from a record's
/// constructor PARAMETERS (required for [ApiController] auto-validation to work at all -
/// see the comment on CreatePacs008MessageRequest). Validator.TryValidateObject instead
/// reads metadata from the record's generated PROPERTIES via TypeDescriptor, so it can't
/// see attributes that only exist on the parameters - it would silently report zero
/// errors regardless of input. Testing via the actual attribute placement (the parameter)
/// is what proves the API's validation will actually fire.
/// </summary>
public class CreatePacs008MessageRequestTests
{
    private static ParameterInfo GetParameter(string name) =>
        typeof(CreatePacs008MessageRequest)
            .GetConstructors().Single()
            .GetParameters().Single(p => p.Name == name);

    [Theory]
    [InlineData(nameof(CreatePacs008MessageRequest.MessageId), 35)]
    [InlineData(nameof(CreatePacs008MessageRequest.EndToEndId), 35)]
    [InlineData(nameof(CreatePacs008MessageRequest.DebtorName), 140)]
    [InlineData(nameof(CreatePacs008MessageRequest.DebtorIban), 34)]
    [InlineData(nameof(CreatePacs008MessageRequest.DebtorBic), 11)]
    [InlineData(nameof(CreatePacs008MessageRequest.CreditorName), 140)]
    [InlineData(nameof(CreatePacs008MessageRequest.CreditorIban), 34)]
    [InlineData(nameof(CreatePacs008MessageRequest.CreditorBic), 11)]
    public void Field_HasExpectedMaxLength(string parameterName, int expectedMaxLength)
    {
        var attribute = GetParameter(parameterName).GetCustomAttribute<StringLengthAttribute>();

        attribute.Should().NotBeNull();
        attribute!.MaximumLength.Should().Be(expectedMaxLength);
    }

    [Theory]
    [InlineData(nameof(CreatePacs008MessageRequest.MessageId))]
    [InlineData(nameof(CreatePacs008MessageRequest.EndToEndId))]
    [InlineData(nameof(CreatePacs008MessageRequest.Currency))]
    [InlineData(nameof(CreatePacs008MessageRequest.DebtorName))]
    [InlineData(nameof(CreatePacs008MessageRequest.DebtorIban))]
    [InlineData(nameof(CreatePacs008MessageRequest.DebtorBic))]
    [InlineData(nameof(CreatePacs008MessageRequest.CreditorName))]
    [InlineData(nameof(CreatePacs008MessageRequest.CreditorIban))]
    [InlineData(nameof(CreatePacs008MessageRequest.CreditorBic))]
    public void Field_IsRequired(string parameterName)
    {
        GetParameter(parameterName).GetCustomAttribute<RequiredAttribute>().Should().NotBeNull();
    }

    [Fact]
    public void RemittanceInformation_IsNotRequired()
    {
        GetParameter(nameof(CreatePacs008MessageRequest.RemittanceInformation))
            .GetCustomAttribute<RequiredAttribute>()
            .Should().BeNull();
    }

    [Fact]
    public void Currency_MustBeExactly3Characters()
    {
        var attribute = GetParameter(nameof(CreatePacs008MessageRequest.Currency)).GetCustomAttribute<StringLengthAttribute>();

        attribute.Should().NotBeNull();
        attribute!.MaximumLength.Should().Be(3);
        attribute.MinimumLength.Should().Be(3);
    }

    [Fact]
    public void Amount_MustBePositive()
    {
        var attribute = GetParameter(nameof(CreatePacs008MessageRequest.Amount)).GetCustomAttribute<RangeAttribute>();

        attribute.Should().NotBeNull();
        Convert.ToDecimal(attribute!.Minimum).Should().Be(0.01m);
    }
}
