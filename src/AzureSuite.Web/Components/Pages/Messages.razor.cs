using AzureSuite.Application.Messages;
using Microsoft.AspNetCore.Components;
using Microsoft.Identity.Abstractions;
using Microsoft.Identity.Client;

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

    // Set when the cached token for calling the API can no longer be silently refreshed
    // (e.g. the server's in-memory token cache was cleared by an app restart, or the
    // Blazor Server circuit reconnected after being disconnected long enough to lose the
    // token-cache correlation). Blazor Server can't redirect mid-circuit for interactive
    // re-consent, so instead of a raw exception we show a link to sign in again.
    private bool _sessionExpired;

    protected override async Task OnInitializedAsync() => await LoadMessagesAsync();

    private async Task LoadMessagesAsync()
    {
        try
        {
            _errorMessage = null;
            _sessionExpired = false;
            _messages = await DownstreamApi.CallApiForUserAsync<IReadOnlyList<Pacs008MessageSummaryDto>>(
                "MessagesApi",
                options => options.RelativePath = "messages");
        }
        catch (MsalUiRequiredException)
        {
            _sessionExpired = true;
            _messages = [];
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
        catch (MsalUiRequiredException)
        {
            _sessionExpired = true;
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
