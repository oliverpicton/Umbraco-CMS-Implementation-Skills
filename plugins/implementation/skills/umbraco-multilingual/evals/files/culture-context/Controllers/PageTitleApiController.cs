using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Models.PublishedContent;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Acme.Web.Controllers;

/// <summary>
/// Returns a page's title as JSON for the front-end widgets.
/// Called from the German site as GET /api/page-title?key={guid}&amp;culture=de-DE, but the title that
/// comes back is always the English one.
/// </summary>
[ApiController]
[Route("api/page-title")]
public class PageTitleApiController : ControllerBase
{
    private readonly IPublishedContentQuery _publishedContentQuery;
    private readonly IUmbracoContextAccessor _umbracoContextAccessor;

    public PageTitleApiController(
        IPublishedContentQuery publishedContentQuery,
        IUmbracoContextAccessor umbracoContextAccessor)
    {
        _publishedContentQuery = publishedContentQuery;
        _umbracoContextAccessor = umbracoContextAccessor;
    }

    [HttpGet]
    public IActionResult Get([FromQuery] Guid key, [FromQuery] string? culture)
    {
        if (!_umbracoContextAccessor.TryGetUmbracoContext(out _))
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, "Umbraco is not ready.");
        }

        IPublishedContent? content = _publishedContentQuery.Content(key);
        if (content is null)
        {
            return NotFound();
        }

        // TODO: 'culture' is accepted so the German site can pass ?culture=de-DE, but the values
        // below come back in English no matter what is passed.
        return Ok(new PageTitleResponse(
            content.Key,
            content.Name,
            content.Value<string>("title"),
            content.Url()));
    }

    public record PageTitleResponse(Guid Key, string? Name, string? Title, string Url);
}
