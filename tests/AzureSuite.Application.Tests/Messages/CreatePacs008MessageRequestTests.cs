using System.ComponentModel.DataAnnotations;
using AzureSuite.Application.Messages;
using FluentAssertions;

namespace AzureSuite.Application.Tests.Messages;

public class CreatePacs008MessageRequestTests
{
    private static CreatePacs008MessageRequest CreateValidRequest() => new()
    {
        MessageId = "MSG-0001",
        EndToEndId = "E2E-0001",
        Amount = 100.50m,
        Currency = "EUR",
        DebtorName = "Alice",
        DebtorIban = "DE89370400440532013000",
        DebtorBic = "COBADEFFXXX",
        CreditorName = "Bob",
        CreditorIban = "FR1420041010050500013M02606",
        CreditorBic = "PSSTFRPPXXX"
    };

    private static IList<ValidationResult> Validate(CreatePacs008MessageRequest request)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void ValidRequest_HasNoValidationErrors()
    {
        var request = CreateValidRequest();

        Validate(request).Should().BeEmpty();
    }

    [Fact]
    public void EndToEndId_LongerThan35Chars_FailsValidation()
    {
        var request = CreateValidRequest();
        request.EndToEndId = new string('a', 36);

        Validate(request).Should().Contain(r => r.MemberNames.Contains(nameof(CreatePacs008MessageRequest.EndToEndId)));
    }

    [Fact]
    public void Currency_NotExactly3Chars_FailsValidation()
    {
        var request = CreateValidRequest();
        request.Currency = "EURO";

        Validate(request).Should().Contain(r => r.MemberNames.Contains(nameof(CreatePacs008MessageRequest.Currency)));
    }

    [Fact]
    public void Amount_Zero_FailsValidation()
    {
        var request = CreateValidRequest();
        request.Amount = 0;

        Validate(request).Should().Contain(r => r.MemberNames.Contains(nameof(CreatePacs008MessageRequest.Amount)));
    }

    [Fact]
    public void MessageId_Empty_FailsValidation()
    {
        var request = CreateValidRequest();
        request.MessageId = "";

        Validate(request).Should().Contain(r => r.MemberNames.Contains(nameof(CreatePacs008MessageRequest.MessageId)));
    }

    [Fact]
    public void RemittanceInformation_NotRequired()
    {
        var request = CreateValidRequest();
        request.RemittanceInformation = null;

        Validate(request).Should().BeEmpty();
    }
}
