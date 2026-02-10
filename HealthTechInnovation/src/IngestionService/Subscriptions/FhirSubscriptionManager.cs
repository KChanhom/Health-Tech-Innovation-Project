using Hl7.Fhir.Model;
using Hl7.Fhir.Rest;
using Microsoft.Extensions.Logging;
using Shared.Fhir;
using Task = System.Threading.Tasks.Task;

namespace IngestionService.Subscriptions;

/// <summary>
/// Manages FHIR Subscriptions for real-time data notification.
/// Can create, list, and delete Subscriptions with rest-hook or websocket channels.
/// </summary>
public class FhirSubscriptionManager
{
    private readonly IFhirClientFactory _clientFactory;
    private readonly ILogger<FhirSubscriptionManager> _logger;

    public FhirSubscriptionManager(IFhirClientFactory clientFactory, ILogger<FhirSubscriptionManager> logger)
    {
        _clientFactory = clientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Creates a FHIR Subscription for a given resource type with a rest-hook channel.
    /// </summary>
    /// <param name="criteria">FHIR search criteria (e.g., "Observation?category=vital-signs")</param>
    /// <param name="webhookUrl">URL of the webhook endpoint to receive notifications</param>
    /// <param name="reason">Human-readable reason for this subscription</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created Subscription resource</returns>
    public async Task<Subscription> CreateSubscriptionAsync(
        string criteria,
        string webhookUrl,
        string reason = "Health Tech data ingestion",
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Creating FHIR Subscription for criteria: {Criteria}", criteria);

        var subscription = new Subscription
        {
            Status = Subscription.SubscriptionStatus.Requested,
            Reason = reason,
            Criteria = criteria,
            Channel = new Subscription.ChannelComponent
            {
                Type = Subscription.SubscriptionChannelType.RestHook,
                Endpoint = webhookUrl,
                Payload = "application/fhir+json",
                Header = new List<string> { "Authorization: Bearer <token>" }
            },
            End = DateTimeOffset.UtcNow.AddDays(30)
        };

        var client = _clientFactory.CreateClient();
        var created = await client.CreateAsync(subscription);

        _logger.LogInformation("Subscription created with ID: {Id}, Status: {Status}",
            created.Id, created.Status);

        return created;
    }

    /// <summary>
    /// Lists all active subscriptions from the FHIR server.
    /// </summary>
    public async Task<IEnumerable<Subscription>> ListSubscriptionsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Listing active FHIR Subscriptions...");

        var client = _clientFactory.CreateClient();
        var bundle = await client.SearchAsync<Subscription>(new[] { "status=active" });

        var subscriptions = new List<Subscription>();
        if (bundle.Entry != null)
        {
            foreach (var entry in bundle.Entry)
            {
                if (entry.Resource is Subscription sub)
                {
                    subscriptions.Add(sub);
                }
            }
        }

        _logger.LogInformation("Found {Count} active subscriptions", subscriptions.Count);
        return subscriptions;
    }

    /// <summary>
    /// Deletes a subscription by its ID.
    /// </summary>
    public async Task DeleteSubscriptionAsync(string subscriptionId, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting Subscription with ID: {Id}", subscriptionId);

        var client = _clientFactory.CreateClient();
        await client.DeleteAsync($"Subscription/{subscriptionId}");

        _logger.LogInformation("Subscription {Id} deleted successfully", subscriptionId);
    }
}
