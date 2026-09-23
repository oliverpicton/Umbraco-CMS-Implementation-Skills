# Rendering variant content (step 4)

Fetch [Language Variation](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/templating-and-rendering/language-variation.md)
and the fallback section of
[Rendering Content](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/templating-and-rendering/design/rendering-content.md)
before writing rendering code.

## In a template: the request's culture is already set

When a request matches a domain, Umbraco sets the **variation context** to that domain's culture.
`Model.Value("title")` and `Model.Name` then return that culture's values, and `Model.Url()` returns
that culture's URL. Pass a culture explicitly only when rendering a *different* one, for example in a
language switcher:

```cshtml
@using Umbraco.Cms.Core.Models.PublishedContent
@Model.Value("title", "da-DK")
```

## Fallback

A blank value in a non-default culture renders as empty unless you ask for fallback:

```cshtml
@Model.Value("title", fallback: Fallback.ToLanguage)
```

| Fallback | Reads |
|---|---|
| `Fallback.ToLanguage` | The language's configured *Fall back to language*, chained (set it in step 1) |
| `Fallback.ToDefaultLanguage` | The default language |
| `Fallback.ToAncestors` | The same property on ancestors (recursive) |
| `Fallback.ToDefaultValue` | The `defaultValue:` argument |

`Fallback.To(Fallback.Language, Fallback.Ancestors)` combines them in order.

**`Value<string>` returns `""` for a blank value, not `null`**, so `Model.Value<string>("title") ?? "…"`
never falls back. Use `Fallback` or `string.IsNullOrWhiteSpace`. The same applies to pickers, which
return an empty collection rather than `null`.

## Culture-less contexts: controllers, jobs, notification handlers

Outside a routed front-end request (Surface/API controllers, background jobs, notification handlers),
nothing sets the variation context, so `IPublishedContent` resolves to the **default** language. Set
it explicitly:

```csharp
using Umbraco.Cms.Core.Models.PublishedContent;

public class MyService(IVariationContextAccessor variationContextAccessor)
{
    public void RenderFor(string culture)
    {
        variationContextAccessor.VariationContext = new VariationContext(culture);
        // IPublishedContent reads below now resolve in `culture`.
    }
}
```

Or pass the culture to each call: `content.Value("title", culture)`, `content.Url(culture)`. Take the
culture from the request (route, domain, `Accept-Language`), never from a hardcoded list.

## Dictionary items: fixed UI strings

Labels that aren't content ("Read more", form labels, footer text) belong in **Translation →
Dictionary**, one item per string, with a value per language. In Razor:

```cshtml
@Umbraco.GetDictionaryValue("ReadMore")
@Umbraco.GetDictionaryValueOrDefault("ReadMore", "Read more")
```

Both read the current request's culture. Prefer `GetDictionaryValueOrDefault` so a missing key or
translation renders readable text rather than an empty string. For dropdown/checkbox property editors,
use dictionary keys as the option values and translate them at render time (the tutorial shows the
loop). The package manifest in [`assets/`](../assets/multilingual-package.xml) shows the dictionary
XML shape.

## Headless: the Delivery API

The Delivery API picks the variant from the `Accept-Language` request header (e.g.
`Accept-Language: da-DK`) when querying by id. When querying by path, the domain in the path already
implies the culture, but a present `Accept-Language` header still takes precedence. A headless front
end:
- keeps the language in its own routing (for example `/[lang]/…`);
- sends that culture on every Delivery API call;
- builds its switcher and hreflang tags from the languages the item is published in.

Fetch the [Content Delivery API docs](https://docs.umbraco.com/umbraco-cms/develop-with-umbraco/headless-and-apis/content-delivery-api.md)
for the exact endpoints before writing the client. Don't ship this skill's Razor partials to a
headless site.

## Done

Tell the user which properties use fallback and to which language, where dictionary items were added,
and, for any controller or job, how it sets the culture.
