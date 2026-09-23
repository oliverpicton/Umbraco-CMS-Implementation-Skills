# Adding cultures missing from the Languages dropdown

Use this **only** when a culture the site needs isn't offered in Settings → Languages → Create. Adding
an ordinary language needs no code; see [languages-and-variants.md](languages-and-variants.md).

## Why a culture goes missing

On .NET with app-local ICU, some valid BCP 47 locales (the docs' example is `zh-HK`) are reported as
`CultureTypes.UserCustomCulture`, or aren't returned by `CultureInfo.GetCultures`. Umbraco's
`IIsoCodeValidator` rejects custom cultures, and `ICultureService` builds the dropdown from the
validated list, so the culture never appears.

## Fix

Fetch [Adding Additional Languages](https://docs.umbraco.com/umbraco-cms/extend-your-project/server-side-extensions/language-files/adding-additional-languages.md)
first, then copy [`assets/AllowAdditionalCulturesComposer.cs`](../assets/AllowAdditionalCulturesComposer.cs).
It is the docs' composer. Substitute `<Namespace>` and `<AdditionalIsoCode>`, the missing culture's
ISO code, e.g. `zh-HK`; add more array entries if several are missing. The composer replaces both
services with wrappers that accept the listed codes as well as everything the defaults accept.

1. Discover the project's namespace and composer folder convention.
2. Write the file with `<Namespace>` and `<AdditionalIsoCode>` substituted.
3. Build, restart, and check the culture now appears under Settings → Languages → Create.

## Caveats (tell the user)

- The validator is used for **all** culture validation, including backoffice user cultures, not just
  this dropdown. Only list codes the site genuinely needs.
- Only list real BCP 47 locales that `CultureInfo.GetCultureInfo` can resolve. The culture service
  silently drops a code it can't resolve, so the language still won't appear.
- This is **not** a workaround for inventing locales. If the business wants, say, "English for market
  X", prefer an existing real locale for that market, or a per-market root (see the note in
  [languages-and-variants.md](languages-and-variants.md)).
- This does not change the backoffice UI language (`DefaultUILanguage`) or add backoffice
  translations.

## Done

Tell the user which codes were added, where the composer lives, and that a restart is needed before
the culture shows in the dropdown.
