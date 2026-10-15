---
name: umbraco-nullable-reference-types
description: >
  Write and fix Umbraco 17+ C# and Razor code under C# nullable reference types (NRT), which
  Umbraco has enabled since v10. Covers which Umbraco APIs return null (Value<T>() returning T?,
  Parent, GetById, Models Builder properties, media and picker values), the TryGetUmbracoContext /
  TryGetUmbracoHelper accessor pattern with [NotNullWhen] flow analysis, Razor null handling, and
  turning on <Nullable>enable</Nullable> with WarningsAsErrors=nullable without scattering the
  null-forgiving operator. Use this when the user mentions "nullable reference types", "NRT",
  CS8602 / CS8600 / CS8603 / CS8618 warnings in an Umbraco project, "possible null reference",
  "dereference of a possibly null reference", "null-forgiving operator", or asks to make Umbraco
  code null-safe. SKIP: non-Umbraco projects, Umbraco versions before 10, and general C# NRT
  questions with no Umbraco API involved.
---

# Nullable Reference Types in Umbraco

TODO(lead): body written after references/ and assets/ land.
