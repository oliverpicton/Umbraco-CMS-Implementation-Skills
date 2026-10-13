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

Run on 2026-10-13 with the method above: all five evals, with and without the skill, as Claude Code
subagents on `claude-haiku-5-5` and `claude-sonnet-5-5`, one run per cell, graded by Haiku 5.5.
Four rounds were run; the skill and two eval details were fixed between rounds, and the table is the
final round. Conditions that applied to both sides equally: no .NET SDK (nothing was built), no
Umbraco instance or MCP, and `docs.umbraco.com` blocked by the sandbox's egress proxy, so every run
worked from the skill text (with-skill side) or its own knowledge (baseline). None of the 80 runs
modified the `Directory.Packages.props` copy. Read single runs as a range, not a point: baseline
scores on the same prompt moved by one or two expectations between rounds.

### Final round

| Eval | Haiku with skill | Haiku baseline | Sonnet with skill | Sonnet baseline |
|---|---|---|---|---|
| 1. Danish launch setup (languages, fallback, domains, switcher, hreflang) | 9/9 | 8/9 | 9/9 | 8/9 |
| 2. Culture-less contexts (API controller, background job) | 6/6 | 6/6 | 6/6 | 6/6 |
| 3. Scripted launch diagnosis (manifest casing, `"*"` publish, fallback, `DefaultUILanguage`) | 6/6 | 5/6 | 6/6 | 5/6 |
| 4. Block level variance (shared blocks, unexposed blocks, migration) | 6/6 | 3/6 | 6/6 | 5/6 |
| 5. Missing culture `zh-HK` (composer) | 7/7 | 4/7 | 7/7 | 5/7 |
| **Mean pass rate** | **100%** (34/34) | **76%** (26/34) | **100%** (34/34) | **85%** (29/34) |

**Haiku 5.5**

- The skill reached 100% against 76% without it, at about the same cost: 83k tokens and 68 s per
  run with the skill, 80k tokens and 92 s without. The with-skill run is faster because it copies
  the partials and the composer instead of working them out.
- Baseline misses: the switcher written inline in `master.cshtml` instead of a partial; the `"*"`
  branch publish never explained (the run blamed idempotency and logging); block level variance
  described without the Document Type varying, no mention of unexposed blocks, and a view that still
  filters by a lowercased culture; and for `zh-HK` a diagnosis of invariant globalization with no
  composer and no word about `IIsoCodeValidator`.
- Haiku is the model the skill helps most, and the one that needed the SKILL.md wording fixes
  below: it works from SKILL.md and reads a reference only when the router points at it.

**Sonnet 5.5**

- The skill reached 100% against 85% without it. It costs about 4k more tokens per run (76k against
  72k) and runs faster (69 s against 101 s).
- Baseline misses: the switcher inline in `master.cshtml`; the `"*"` publish behaviour again
  unexplained; block level variance without the Document Type clause; and no composer for `zh-HK`
  (the baseline switched `InvariantGlobalization` off in the csproj and stopped).
- The gap is smaller than Haiku's and sits in what has to be known rather than reasoned out: the
  `"*"` publish rule, unexposed blocks, and that the dropdown is `ICultureService` filtered by
  `IIsoCodeValidator`.

**Across both models**

- Every with-skill run in the final round got full marks. Rounds 1 to 3 each had one to three
  with-skill misses; each was traced and fixed (below) before the next round.
- Evals 1, 3, 4 and 5 separate the two sides on both models. Eval 2 (the variation context in a
  controller and a job) scored 6/6 in all sixteen cells across the four rounds: both models know it
  without help, so it did not discriminate in any round.
- Baselines varied between rounds on the same prompt: Sonnet's eval 3 baseline scored 5/6, 5/6, 6/6
  and 5/6; Haiku's eval 5 baseline 4/7, 6/7, 5/7 and 4/7. Single-run figures are a range.
- No with-skill run lowercased a culture in any round. In the final round two baselines did: Haiku's
  eval 4 view kept the fixture's `.ToLower()` on the request culture, and Sonnet's eval 4 migration
  lowercased the block-settings culture list.

### Earlier rounds and what changed between them

| Round | Haiku with / baseline | Sonnet with / baseline | With-skill misses and the fix that followed |
|---|---|---|---|
| 1 | 32/34 / 24/34 | 33/34 / 28/34 | Eval 1 (both models): the package-manifest expectation was unconditional, but neither run used the package; it is now conditional on using or recommending it. Eval 3 (Haiku): never questioned making Danish the default; SKILL.md now says changing the default is a separate decision. |
| 2 | 33/34 / 28/34 | 32/34 / 28/34 | All three misses on the build-honesty expectation. Two were the grading note being read as a requirement to predict whether the code compiles; the note now says a run passes by not claiming a build or verification. One Sonnet run told the user the composer "has been validated against Umbraco 17.5", echoing the skill's own runtime-gate claim; SKILL.md now says the gate tests the skill's assets, not the run's copy. |
| 3 | 33/34 / 27/34 | 34/34 / 30/34 | Eval 4 (Haiku): block level variance described without the Document Type varying. The reference table named all three settings; the SKILL.md bullet named two and now names three. |
| 4 (final) | 34/34 / 26/34 | 34/34 / 29/34 | None. |

A single-cell re-run of Haiku eval 3 between rounds 1 and 2, against the round-1 SKILL.md fix,
scored 6/6; the full round 2 confirmed it.

### Harness notes for the next person

- Sonnet 5.5's safeguards rejected the executor prompt wholesale when its transcript line asked
  for "each step ... and your reasoning", and rejected three runs mid-task even after that was
  reworded. From round 2 both models were asked for "a plain log of the steps you took"; a rejected
  run was reset to a fresh sandbox and re-run, never patched up.
- Some runs write `transcript.md` inside `outputs/` rather than beside it. Move it before grading.
- One Haiku run in round 3 reported a 20-minute wall time although its files were written in two
  minutes; the harness stalled after the work. Treat a lone outlier like that as harness time.
