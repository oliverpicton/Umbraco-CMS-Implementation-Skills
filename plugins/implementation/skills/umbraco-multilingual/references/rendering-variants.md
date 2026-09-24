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
culture from the request (route, domain, query string), never from a hardcoded list.

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

## Blocks

With block level variance (an invariant Block List/Grid property whose element types vary; see
[languages-and-variants.md](languages-and-variants.md#blocks-same-blocks-translated-content)),
rendering needs no filtering or culture code:

```cshtml
@using Umbraco.Cms.Core.Models.Blocks
@foreach (var block in Model.Value<BlockListModel>("blocks") ?? BlockListModel.Empty)
{
    <h2>@block.Content.Value("title")</h2>  @* already the request's culture *@
}
```

- Blocks **not exposed** in the request's culture are left out.
- `block.Content.Value(...)` returns the request's culture, and shared (invariant) block properties
  return their single value.
- **Language fallback for blocks (Umbraco 17.4+):** pass a fallback to show a block that isn't exposed
  in this language, rendered in the fallback language, in its usual position:
  `Model.Value<BlockListModel>("blocks", fallback: Fallback.ToLanguage)`. Without the fallback
  argument it stays hidden. Use it only when "show the English block until it's translated" is what
  the site wants.
- In culture-less contexts (controllers, jobs), set the variation context first, as above. Blocks
  follow it.

## Done

Tell the user which properties use fallback and to which language, where dictionary items were added,
and, for any controller or job, how it sets the culture.
