---
name: umbraco-multilingual
description: >
  Use for any Umbraco 17+ work where content exists in more than one language (culture). Covers
  setup and debugging: adding a language or market (languages, fallback, vary-by-culture Document
  Types, Culture and Hostnames such as /de or a domain per country) without breaking existing
  URLs; translating Block List, Block Grid or rich text blocks per language while keeping the same
  blocks and order (block level variance); blank translated fields that should fall back to
  another language; a controller, SurfaceController or background job returning the wrong culture;
  translating hardcoded view text (button labels, UI strings) with dictionary items; a language
  switcher and hreflang tags; migrations or packages whose imported languages, dictionary items or
  cultures don't go live; a culture such as zh-HK missing from the Languages dropdown, especially
  on Linux or Azure. SKIP: non-Umbraco apps, Umbraco 13 and earlier, translating the backoffice UI
  or extension labels, and headless / Content Delivery API front ends.
---

# Multilingual site

One content tree, **culture variants** on the nodes, and a **domain per culture** on the site root.
That is Umbraco's documented model: one node holds every language version, and the domain the request
matches decides which one renders. Build it in this order, since each step depends on the one before.

| # | Step | Reference | Ships |
|---|---|---|---|
| 1 | Languages: default, mandatory, fallback | [languages-and-variants.md](references/languages-and-variants.md) | [`assets/multilingual-package.xml`](assets/multilingual-package.xml) |
| 2 | Vary Document Types, properties and blocks by culture | [languages-and-variants.md](references/languages-and-variants.md) | — |
| 3 | Culture and Hostnames: a domain per culture | [languages-and-variants.md](references/languages-and-variants.md) | — |
| 4 | Render variant values, fallback, dictionary, culture outside a request | [rendering-variants.md](references/rendering-variants.md) | — |
| 5 | Language switcher and hreflang | [language-switcher-and-seo.md](references/language-switcher-and-seo.md) | [`languageSwitcher.cshtml`](assets/languageSwitcher.cshtml), [`hreflangLinks.cshtml`](assets/hreflangLinks.cshtml) |
| — | Only if a culture is missing from the Languages dropdown | [additional-cultures.md](references/additional-cultures.md) | [`AllowAdditionalCulturesComposer.cs`](assets/AllowAdditionalCulturesComposer.cs) |

### How to decide

- Default to one tree with culture variants. Choose a **root per market** only when markets differ
  in *structure*, not just in wording; see the note in
  [languages-and-variants.md](references/languages-and-variants.md).
- For backoffice steps (1–3), prefer the Umbraco Developer MCP. If it's unavailable, walk the user
  through the backoffice one step at a time; the order is in
  [languages-and-variants.md](references/languages-and-variants.md). Don't skip a step because the
  MCP is missing.
- **Headless / Content Delivery API front ends are out of scope for now.** This skill covers Razor
  sites.

## Version compatibility

Targets **Umbraco 17+**. Every API used here (`ILanguageService`, `IDomainService.UpdateDomainsAsync`,
`IIsoCodeValidator`, `ICultureService`) exists on 17. Umbraco 13 and earlier have different
language/domain APIs; don't apply this skill there.

## Best practices

- **Never `ToLower()` a culture.** Keep ISO casing (`da-DK`) in code and config, and compare with
  `StringComparison.OrdinalIgnoreCase`. Casing differs between sources (Examine stores culture field
  suffixes lowercase, e.g. `__Published_da-dk`), so a case-sensitive comparison fails on some of them.
- **Don't hardcode culture lists.** Read them from `ILanguageService` or `IPublishedContent.Cultures`,
  so adding a language needs no code change.
- **Set a fallback per language and decide mandatory deliberately.** A mandatory language blocks
  publishing every other culture of a node until it has content.
- **Translate blocks with block level variance, not a varying block property.** Keep the Block
  List/Grid property invariant and make its element types vary, so every language shares the same
  blocks and order. A varying block property gives each language its own list, and the lists drift.
- **Vary only what differs.** Shared values (SKUs, dates, images with no text) belong on invariant
  properties, or editors enter them once per language and they drift apart.
- **`Value<string>` returns `""`, not `null`, for a blank value.** A `?? fallback` never fires; use
  `Fallback.ToLanguage` or `string.IsNullOrWhiteSpace`.
- **`Umbraco:CMS:Global:DefaultUILanguage` is the *backoffice* UI language.** The site's default
  content language is set on the language itself (Settings → Languages → Default language).
- **Every published culture needs a reachable URL.** If a culture has no domain, its pages can't be
  routed in that culture, and the switcher and hreflang tags have nothing to link to.

## Validation

Assertions live in [`evals/evals.json`](evals/evals.json); run them with `umbraco-skill-evaluator`.
The runtime gate covers both gated parts:
- `examples/culture-variants/` on the blank host (languages, domains, fallback, dictionary, switcher,
  hreflang, block level variance);
- `examples/additional-cultures/` on the Clean host (the composer).
