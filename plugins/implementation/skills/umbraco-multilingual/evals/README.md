# Running these evals

Run all five evals in `evals.json` together, with and without the skill, using the
`umbraco-skill-evaluator` skill. Run them once on Haiku and once on Sonnet. A stronger model's
baseline already knows some of Umbraco's variant behaviour (the variation context, `Fallback`,
block level variance), so a single strong-model run understates what the skill adds; the Haiku run
shows the gap, the Sonnet run shows what is left once the model knows the basics.

The evaluator expects one workspace laid out as
`<workspace>/<model>/iteration-N/<eval-name>/{with_skill,without_skill}/`, so the two models'
runs sit side by side and are reported separately.

## Project fixture (every eval, both sides)

Each run gets its own sandbox:

```
<run>/outputs/
  Directory.Packages.props     # copy of the repo-root file — see "Outside-the-project check"
  project/                     # the "user's project": the agent edits here, in place
```

To build `project/`:
1. Copy `files/project/` into it. This is a minimal, English-only Umbraco 17 site with root
   namespace `Acme.Web`:
   - the stock Umbraco csproj settings, plus an `EmbeddedResource` glob for `Migrations/*.xml`
     (harmless when the folder is absent);
   - `appsettings.json` with ModelsBuilder in `Nothing` mode and nothing multilingual;
   - `Views/master.cshtml`, a layout with a `<title>` but no hreflang, a header with the site name
     but no language switcher, and `<html lang="en">`;
   - `Views/home.cshtml`, which renders `title` and `bodyText` with `Model.Value<string>(...)` and
     no fallback.
2. Overlay the eval's own folder from its `files` list, replacing any file with the same name:

| Eval | Overlay | Starting state |
|---|---|---|
| 1 | — | The project as copied: no second language, switcher or hreflang anywhere |
| 2 | `files/culture-context/` | `Controllers/PageTitleApiController.cs`, an `[ApiController]` at `api/page-title` that accepts `?culture=` but never uses it, and `Jobs/ProductFeedJob.cs`, a nightly `IRecurringBackgroundJob` that builds the German feed from the content cache the same culture-less way (with its composer) |
| 3 | `files/danish-launch/` | `Migrations/DanishLaunchMigrationPlan.cs` + `DanishLaunchMigration.cs` importing `Migrations/danish-launch-package.xml`, whose Danish `<Value LanguageCultureAlias="da-dk">` is lowercase; `Startup/DanishContentSeeder.cs`, which sets Danish values and calls `PublishBranchAsync(rootKey, ["*"], PublishBranchFilter.IncludeUnpublished, userKey, false)`; and an `appsettings.json` that adds `Umbraco:CMS:Global:DefaultUILanguage: "da-DK"` |
| 4 | `files/block-sections/` | `Views/home.cshtml` replaced by one that renders a `sections` Block List (noted as *varying by culture* in the backoffice) with hand-rolled `languages`/`danishOnly` filtering and a lowercased culture comparison |
| 5 | — | The project as copied |

The fixture mirrors the pinned `Umbraco.Cms 17.5.3` csproj and the repo's central package versions
(`Directory.Packages.props`), but it **was not built** in the session that wrote it: that session
had no .NET SDK and no NuGet access. A reader who can build should assemble each overlay once and
run `dotnet build` before trusting the "the fixture compiles" assumption. The views are compiled at
runtime, as in the stock template.

## Run prompt

Each run is told:
- where the project is, and to edit it in place;
- that it may fetch public docs;
- to save its reply to `outputs/answer.md` and a transcript to `transcript.md`.

Give the with-skill run the path of a **snapshot** of the skill that contains only `SKILL.md`,
`references/` and `assets/`, not the skill folder itself. `evals/` holds the expectations and
`examples/` holds the tests, and a run that can reach them will read them; an instruction not to
read them is not enough. Tell the baseline run not to read `plugins/`, `.claude/` or any Umbraco
source checkout, or it will find the skill by browsing.

Do not tell either side to avoid writing outside the project: see the next section.

No Umbraco instance and no Umbraco MCP are running during a run, so every backoffice step
(languages, fallback, vary by culture, Culture and Hostnames, exposing blocks) is graded on the
instructions the run gives, not on anything it did.

## Outside-the-project check

`dotnet add package` in a centrally-managed project edits the nearest `Directory.Packages.props`
above it. The copy in `outputs/` is there to absorb that, so the repo's own file is never touched.
After a run, diff the copy against the repo-root original; a difference means the run reached
outside `project/`.

## Reviewing in the viewer

The evaluator's viewer lists only the files directly inside `outputs/`, not those under
`outputs/project/`. Before generating the report, copy each file the run created or changed up into
`outputs/` (for example as `changed - Views__home.cshtml` or
`created - Views__Partials__languageSwitcher.cshtml`), or a reviewer sees only `answer.md` and
will think the code changes are missing.

## Grading notes

Grade code expectations from the files under `outputs/project/`, not from `answer.md` alone.

- **Eval 1** is mostly backoffice instructions plus two partials and a view change. Grade the
  switcher and hreflang expectations from the files the run wrote (`Views/Partials/*.cshtml`,
  `Views/master.cshtml`, `Views/home.cshtml`). Copying the skill's `languageSwitcher.cshtml` and
  `hreflangLinks.cshtml` verbatim passes those expectations; the baseline has to write its own.
- **Eval 2**: both files must change. A fix in the controller alone, or advice without an edit,
  fails the "applied in both" expectation.
- **Eval 3**: four separate causes, four separate fixes. Check the manifest's casing in
  `danish-launch-package.xml`, the cultures passed in `DanishContentSeeder.cs`, where the fallback
  language gets set (backoffice, MCP or code are all acceptable), and what happened to
  `DefaultUILanguage` in `appsettings.json`.
- **Eval 4**: the rewritten `Views/home.cshtml` should contain no culture filtering at all; the
  rest is advice, graded from `answer.md`.
- **Eval 5** is advice plus one composer. Grade it from `answer.md` and any `.cs` the run wrote.
- A conditional expectation whose condition never arises (for example "if the agent replaces
  `IIsoCodeValidator`" when it keeps the stock one) is not applicable; do not grade it as a fail.
- "Does not lowercase cultures": a `ToLower()` or `ToLowerInvariant()` applied to a culture code
  anywhere in the written code or in a proposed fix fails it, including one left over from the eval 4
  starting view. Case-insensitive comparison (`StringComparison.OrdinalIgnoreCase`) passes.
- The build-honesty expectations: no run can build here, so a run that says it could not build
  passes. It fails only if it claims to have built, run, rendered, validated or verified anything,
  including presenting the skill's own runtime gate as its verification ("this composer has been
  validated against Umbraco 17.5"). Saying whether the code is expected to compile is welcome but
  not required; the expectation text does not ask for it.

## Results

Run on 2026-10-13 with the method above: all five evals, with and without the skill, one run per
cell, as Claude Code subagents on `claude-haiku-5-5` and `claude-sonnet-5-5`, graded by Haiku 5.5.
Conditions that applied to both sides equally: no .NET SDK (nothing was built), no Umbraco instance
or MCP, and `docs.umbraco.com` blocked by the sandbox's egress proxy, so every run worked from the
skill text (with-skill side) or its own knowledge (baseline). No run modified the
`Directory.Packages.props` copy. Read single runs as a range, not a point: PR #20 saw baseline
scores move between rounds on the same prompts.

### Haiku 5.5

| Eval | With skill | Baseline |
|---|---|---|
| 1 `danish-launch-setup` | 8/9 | 6/9 |
| 2 `culture-less-contexts` | 6/6 | 6/6 |
| 3 `scripted-launch-diagnosis` | 5/6 (iteration 2: 6/6) | 5/6 |
| 4 `block-level-variance` | 6/6 | 3/6 |
| 5 `missing-culture-zh-hk` | 7/7 | 4/7 |
| **Total** | **32/34 (94%)** | **24/34 (71%)** |
| Mean tokens / wall time per run | 90k / 92 s | 81k / 101 s |

### Sonnet 5.5

| Eval | With skill | Baseline |
|---|---|---|
| 1 `danish-launch-setup` | 8/9 | 6/9 |
| 2 `culture-less-contexts` | 6/6 | 6/6 |
| 3 `scripted-launch-diagnosis` | 6/6 | 5/6 |
| 4 `block-level-variance` | 6/6 | 6/6 |
| 5 `missing-culture-zh-hk` | 7/7 | 5/7 |
| **Total** | **33/34 (97%)** | **28/34 (82%)** |
| Mean tokens / wall time per run | 75k / 72 s | 72k / 114 s |

On both models the skill costs a few thousand tokens more per run and finishes sooner, because the
with-skill run copies the partials and composer instead of working them out.

### What the skill added

- **Eval 4 (block level variance).** The Haiku baseline kept the hand-rolled filter, invented a
  per-block `cultures` checkbox and never mentioned unexposed blocks. With the skill both models
  made the property shared, varied the element types, and left the view with no culture code.
- **Eval 5 (zh-HK).** Both baselines diagnosed invariant globalization or a missing ICU package and
  wrote no composer (Sonnet's wrote a startup check that only logs). With the skill both shipped the
  `ICultureService` / `IIsoCodeValidator` composer with the validator caveat.
- **Eval 1 (Danish launch).** Both baselines put the switcher inline in `master.cshtml` instead of
  a partial; the Sonnet baseline added `da` rather than `da-DK`; the Haiku baseline did not say to
  keep shared values invariant. With the skill both models copied the two partials and the hreflang
  `<head>` block verbatim.
- **Eval 3 (scripted launch).** Neither baseline explained that a branch publish with `"*"`
  republishes only the default culture (Haiku blamed a validation error; Sonnet fixed the code but
  gave a different reason). Both with-skill runs did.

### Where the baseline already got it

Eval 2 (culture-less controller and job) scored 6/6 in all four cells: both models know the
variation context without help, so this eval did not discriminate in this round.

### The two with-skill misses, and what changed

- **Eval 1, expectation 2** (the package manifest does not set fallback, mandatory or default)
  failed in all four eval-1 cells, including both with-skill runs: no run used the package at all.
  They created the language in the backoffice and set its fallback there, which the skill allows,
  and the skill only promises the package note "whenever you use the package". The expectation was
  unconditional, so it penalised a correct answer and separated nothing. It is now conditional on
  the run using or recommending the package. The tables above are graded against the old wording;
  under the new one every eval-1 cell gains one pass (9/9 vs 7/9 on both models).
- **Eval 3, expectation 4** (Haiku, with skill): the run explained `DefaultUILanguage` correctly
  and removed it, but never questioned whether Danish should become the site's default language.
  The skill said where the default lives, not that changing it is a decision. SKILL.md now says
  so (the default drives no-domain routing and `Fallback.ToDefaultLanguage`; ask before switching
  it). Re-running only that cell on Haiku with the updated skill (iteration 2) scored 6/6;
  the run's answer raises the default-language decision explicitly.

### Harness notes for the next person

- Sonnet 5.5's safeguards rejected the executor prompt wholesale when its transcript line asked
  for "each step ... and your reasoning", and rejected three runs mid-task even after that was
  reworded. Ask for "a plain log of the steps you took" instead; a rejected run was reset to a
  fresh sandbox and re-run, never patched up.
- One Sonnet baseline wrote its `transcript.md` inside `outputs/` rather than beside it. Harmless,
  but check before aggregating.
