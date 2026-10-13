using System.Text;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.PublishedCache;
using Umbraco.Cms.Core.Sync;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.BackgroundJobs;
using Umbraco.Cms.Infrastructure.DependencyInjection;
using Umbraco.Extensions;

namespace Acme.Web.Jobs;

/// <summary>
/// Nightly job that writes the German product feed (feeds/products-de.txt) for our partner network.
/// It was ported from Hangfire to Umbraco's own scheduler, so the team still calls it "the Hangfire
/// job". It runs once every 24 hours on the scheduling server.
///
/// Known problem: the feed comes out with the English product names and descriptions, even though
/// the same products render in German at /de/ on the site.
/// </summary>
public class ProductFeedJob : IRecurringBackgroundJob
{
    // The feed is always the German one; the partner network only takes de-DE.
    private const string FeedCulture = "de-DE";

    private readonly IUmbracoContextFactory _umbracoContextFactory;
    private readonly IPublishedContentCache _contentCache;
    private readonly IWebHostEnvironment _hostEnvironment;
    private readonly ILogger<ProductFeedJob> _logger;

    public ProductFeedJob(
        IUmbracoContextFactory umbracoContextFactory,
        IPublishedContentCache contentCache,
        IWebHostEnvironment hostEnvironment,
        ILogger<ProductFeedJob> logger)
    {
        _umbracoContextFactory = umbracoContextFactory;
        _contentCache = contentCache;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    // Nightly.
    public TimeSpan Period => TimeSpan.FromHours(24);

    public TimeSpan Delay => TimeSpan.FromMinutes(5);

    public ServerRole[] ServerRoles => [ServerRole.Single, ServerRole.SchedulingPublisher];

    // The period never changes at runtime.
    public event EventHandler PeriodChanged { add { } remove { } }

    public async Task RunJobAsync()
    {
        // Running outside a request, so make sure an UmbracoContext exists before touching the cache.
        using UmbracoContextReference _ = _umbracoContextFactory.EnsureUmbracoContext();

        IPublishedContent? productsRoot = _contentCache.GetAtRoot()
            .SelectMany(root => root.Children())
            .FirstOrDefault(node => node.ContentType.Alias == "productList");

        if (productsRoot is null)
        {
            _logger.LogWarning("No productList node found; the {Culture} product feed was not written.", FeedCulture);
            return;
        }

        var feed = new StringBuilder();
        foreach (IPublishedContent product in productsRoot.Children())
        {
            // sku is shared across cultures; productName and shortDescription are translated per
            // language in the backoffice, but the feed always gets the English text.
            feed.Append(product.Value<string>("sku")).Append('\t')
                .Append(product.Value<string>("productName")).Append('\t')
                .Append(product.Value<string>("shortDescription")).Append('\t')
                .Append(product.Url(mode: UrlMode.Absolute))
                .AppendLine();
        }

        string feedsDirectory = Path.Combine(_hostEnvironment.ContentRootPath, "feeds");
        Directory.CreateDirectory(feedsDirectory);
        await File.WriteAllTextAsync(Path.Combine(feedsDirectory, "products-de.txt"), feed.ToString());

        _logger.LogInformation("Wrote the {Culture} product feed.", FeedCulture);
    }
}

public class ProductFeedJobComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
        => builder.Services.AddRecurringBackgroundJob<ProductFeedJob>();
}
