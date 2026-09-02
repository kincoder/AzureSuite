using AzureSuite.Application.Messages;
using Microsoft.AspNetCore.Components;
using Microsoft.Identity.Abstractions;

namespace AzureSuite.Web.Components.Pages;

public partial class Messages : ComponentBase
{
    [Inject]
    private IDownstreamApi DownstreamApi { get; set; } = null!;

    private IReadOnlyList<Pacs008MessageSummaryDto>? _messages;

    // Bound directly by the EditForm below - same type the API accepts, see the
    // comment on CreatePacs008MessageRequest for why it isn't a record.
    private CreatePacs008MessageRequest _newMessage = new() { Currency = "EUR" };

    private bool _isSubmitting;
    private string? _errorMessage;

    protected override async Task OnInitializedAsync() => await LoadMessagesAsync();

    private async Task LoadMessagesAsync()
    {
        try
        {
            _errorMessage = null;
            _messages = await DownstreamApi.CallApiForUserAsync<IReadOnlyList<Pacs008MessageSummaryDto>>(
                "MessagesApi",
                options => options.RelativePath = "messages");
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to load messages: {ex.Message}";
            _messages = [];
        }
    }

    private async Task SubmitMessageAsync()
    {
        _isSubmitting = true;
        try
        {
            await DownstreamApi.CallApiForUserAsync<CreatePacs008MessageRequest, Pacs008MessageSummaryDto>(
                "MessagesApi",
                _newMessage,
                options =>
                {
                    options.RelativePath = "messages";
                    options.HttpMethod = HttpMethod.Post.Method;
                });

            _newMessage = new CreatePacs008MessageRequest { Currency = "EUR" };
            await LoadMessagesAsync();
        }
        catch (Exception ex)
        {
            _errorMessage = $"Failed to submit message: {ex.Message}";
        }
        finally
        {
            _isSubmitting = false;
        }
    }
}
