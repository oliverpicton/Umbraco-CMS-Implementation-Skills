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

Each run gets its own folder:

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
| 2 | `files/culture-context/` | `Controllers/PageTitleApiController.cs`, an `[ApiController]` at `api/page-title` that lowercases `?culture=` (`ToLowerInvariant()`), compares it case-sensitively against `ILanguageService` ISO codes, falls back to the default language on no match and still never sets a variation context or passes the culture to `Value`/`Url`; and `Jobs/ProductFeedJob.cs`, a nightly `IRecurringBackgroundJob` (with its composer) that writes one feed per culture from a hardcoded `new[] { "en-US", "de-DE" }`, fills blank German descriptions with `Value<string>(alias, culture) ?? Value<string>(alias, "en-US")` and finds published products with an Examine query on `$"__Published_{culture}"` (so `__Published_de-DE`) |
| 3 | `files/danish-launch/` | `Migrations/DanishLaunchMigrationPlan.cs` + `DanishLaunchMigration.cs` importing `Migrations/danish-launch-package.xml`, whose Danish `<Value LanguageCultureAlias="da-dk">` is lowercase; `Startup/DanishContentSeeder.cs`, which sets Danish values and calls `PublishBranchAsync(rootKey, ["*"], PublishBranchFilter.IncludeUnpublished, userKey, false)`; and an `appsettings.json` that adds `Umbraco:CMS:Global:DefaultUILanguage: "da-DK"` |
| 4 | `files/block-sections/` | `Views/home.cshtml` replaced by one that renders a `sections` Block List (noted as *varying by culture* in the backoffice) with hand-rolled `languages`/`danishOnly` filtering and a lowercased culture comparison |
| 5 | — | The project as copied |

The fixture mirrors the pinned `Umbraco.Cms 17.5.3` csproj and the repo's central package versions
(`Directory.Packages.props`). All five assembled overlays build with
`dotnet build -p:RazorCompileOnBuild=true` (checked on 2026-10-13 with dotnet-sdk-10.0). The first
build found two Razor traps in the fixture itself, a loop variable named `page` that Razor read as
the `@page` directive and bare `@Model.Value<string>(...)` calls, so build every overlay again after
editing the fixture. The views are compiled at runtime, as in the stock template; the build check
turns Razor compilation on to catch view errors.

## Run prompt

Each run is told:
- where the project is, and to edit it in place;
- that the .NET SDK is installed and it may run `dotnet build` on the project (rounds 1 to 4 had no SDK and
  were told so);
- that it may read the Umbraco documentation (rounds 1 to 4 had no docs at all);
- to save its reply to `outputs/answer.md` and a plain step log to `transcript.md`.

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
  switcher and hreflang expectations from the files the run wrote (`Views/master.cshtml`,
  `Views/home.cshtml` and any partials). Copying the skill's `languageSwitcher.cshtml` and
  `hreflangLinks.cshtml` verbatim passes those expectations; an inline switcher with the same
  behaviour passes too; the baseline has to write its own.
- **Eval 2**: both files must change. A fix in the controller alone, or advice without an edit,
  fails the "applied in both" expectation. In `Jobs/ProductFeedJob.cs` the hardcoded culture list,
  the `?? Value<string>(..., "en-US")` fallback and the `$"__Published_{culture}"` field must each
  be gone or replaced (expectations 3, 4 and 6 are graded one per bug). A `ToLower()` /
  `ToLowerInvariant()` on a culture left anywhere in either file, including the fixture's own
  line in the controller, fails expectation 5, except a lowercase used only to build the Examine
  `__Published_` field name, which expectation 6 accepts.
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
  starting view. Case-insensitive comparison (`StringComparison.OrdinalIgnoreCase`) passes, and so
  does a lowercase used only to build the Examine `__Published_` field name (eval 2, expectation 6).
- The build-honesty expectations: a run that says it could not or did not build passes; a run that
  says it built passes only if its log shows the build and `razor-build.txt` agrees. It fails if it
  claims to have built, run, rendered, validated or verified anything it did not, including presenting
  the skill's own runtime gate as its verification ("this composer has been validated against
  Umbraco 17.5"). Saying whether the code is expected to compile is welcome but not required.
- The razor-build expectation is graded from `razor-build.txt` beside the run's `outputs/`, written after
  the run (not by it) with `dotnet build -p:RazorCompileOnBuild=true` on `outputs/project`. Exit code 0 passes; anything else fails, whatever the run said.

## Results

Run on 2026-10-13 with the method above: all five evals, with and without the skill, as Claude Code
runs on `claude-haiku-5-5` and `claude-sonnet-5-5`, one run per cell, graded by Haiku 5.5.
Five rounds were run. Rounds 1 to 4 had no .NET SDK and no docs; round 5 (the table below) gave every
run the SDK, permission to build and the Umbraco docs, and added the razor-build expectation to every
eval and the harder eval 2 prompt, so its totals are 41 per column against 34 before. In every round
neither side had an Umbraco instance or MCP. None of the 100 runs modified the
`Directory.Packages.props` copy. Read single runs as a range, not a point: baseline scores on the
same prompt moved by one or two expectations between rounds.

### Final round (round 5: builds and docs)

| Eval | Haiku with skill | Haiku baseline | Sonnet with skill | Sonnet baseline |
|---|---|---|---|---|
| 1. Danish launch setup (languages, fallback, domains, switcher, hreflang) | 10/10 | 10/10 | 10/10 | 10/10 |
| 2. Culture-less contexts (API controller, background job) | 9/9 | 7/9 | 9/9 | 8/9 |
| 3. Scripted launch diagnosis (manifest casing, `"*"` publish, fallback, `DefaultUILanguage`) | 7/7 | 5/7 | 7/7 | 5/7 |
| 4. Block level variance (shared blocks, unexposed blocks, migration) | 7/7 | 7/7 | 7/7 | 7/7 |
| 5. Missing culture `zh-HK` (composer) | 8/8 | 7/8 | 8/8 | 8/8 |
| **Mean pass rate** | **100%** (41/41) | **88%** (36/41) | **100%** (41/41) | **93%** (38/41) |

Every one of the twenty edited projects built with Razor compilation on (`razor-build.txt` exit 0 in
all twenty runs), so the build column proves the code compiles but does not separate the two sides.
Each run also ran `dotnet build` itself at least once, which is why round-5 times are about three
times round 4's.

**Haiku 5.5**

- The skill reached 100% against 88% without it, and cost less: 106k tokens and 214 s per run with
  the skill, 118k tokens and 251 s without. The with-skill run copies the partials and the composer
  and reads one reference; the baseline reads more docs pages and builds more often.
- Baseline misses: in the feed job the `?? Value<string>(..., defaultCulture)` fallback kept, with no explanation that
  `Value<string>` returns `""` so `??` never fires; the `__Published_de-DE` query removed but with
  "I could not confirm the exact stored field name" instead of the lowercase suffix; the `"*"`
  publish explained as "publishes every culture" (the opposite of what happens); Danish made the
  default without asking whether it should be; and `zh-Hant-HK` offered as a workaround that
  "would show without the composer".
- Haiku is the model the skill helps most, and the one that needed the SKILL.md wording fixes
  below: it works from SKILL.md and reads a reference only when the router points at it.

**Sonnet 5.5**

- The skill reached 100% against 93% without it, at lower cost: 87k tokens and 191 s per run with
  the skill against 94k and 239 s without.
- Baseline misses: `Fallback.ToDefaultLanguage` used in the job (which works) but the `""` against `null` bug behind the fixture's `??` never explained; the
  `"*"` publish left as "I could not verify that the newer service expands `*`"; and
  Danish set as the site default without questioning it.
- The gap is smaller than Haiku's and sits in what has to be known rather than reasoned out or looked
  up: the `"*"` publish rule, that `Value<string>` is never `null`, and that changing the default
  language is a separate decision.

**Across both models**

- Every with-skill run in round 5 got full marks. One (Haiku, eval 1) was first graded 9/10 on the
  expectation "prefers the Umbraco Developer MCP for backoffice steps and otherwise walks through them
  manually": the run gave every backoffice step by hand but, told that no MCP was running, said
  nothing about one, and the grader would not credit a preference it could not see (the round-4
  grader had flagged the same wording as untestable). The expectation now reads "uses the MCP when
  one is available; when none is ... walks the user through them manually rather than skipping them",
  and that cell was regraded on the same, unchanged run output: 10/10.
- Eval 1's switcher expectation originally required "a partial under `Views/Partials/`". Both round-5
  baselines wrote a switcher with the right behaviour inline in `master.cshtml` and lost their only
  eval 1 point on that clause. Form is not what the eval is for, so the clause now reads "inline or as
  a partial" and both baseline cells were regraded on their unchanged outputs (both 10/10, which is
  also the figure in the table); all four eval 1 cells were graded on the current wording of both
  reworded expectations.
- Access to the docs lifted the baselines, not the skill: Haiku's baseline went from 76% (round 4) to
  88%, Sonnet's from 85% to 93%. Eval 4 (block level variance) stopped discriminating: both
  baselines scored 7/7 after reading `block-level-variance.md`, where in round 4 they scored 3/6 and
  5/6. Sonnet's eval 5 baseline wrote the `IIsoCodeValidator` composer from the docs page (8/8;
  5/7 in round 4). Haiku's did not find the page and offered `zh-Hant-HK` instead.
- Eval 2 now discriminates. Its round-5 prompt names the symptoms (lowercased culture, hardcoded
  cultures, the `??` fallback, `__Published_de-DE`, French with no code change) and the baselines
  scored 7/9 and 8/9 against 9/9; in rounds 1 to 4 the softer prompt scored 6/6 in all sixteen
  cells. What still separates the sides is the explanation: both baselines fixed the code but could
  not say why the original fallback never fired.
- Evals 2 and 3 separate the two sides on both models in round 5; eval 5 only on Haiku; evals 1 and
  4 on neither. What is left in the gap is knowledge the docs do not state plainly: the `"*"` publish
  rule, `Value<string>` never being `null`, the lowercase Examine suffix, and that changing the default
  language is a separate decision.
- No with-skill run lowercased a culture in any round. In round 5 no baseline did either (in round 4
  two had).

### Earlier rounds and what changed between them

| Round | Haiku with / baseline | Sonnet with / baseline | With-skill misses and the fix that followed |
|---|---|---|---|
| 1 | 32/34 / 24/34 | 33/34 / 28/34 | Eval 1 (both models): the package-manifest expectation was unconditional, but neither run used the package; it is now conditional on using or recommending it. Eval 3 (Haiku): never questioned making Danish the default; SKILL.md now says changing the default is a separate decision. |
| 2 | 33/34 / 28/34 | 32/34 / 28/34 | All three misses on the build-honesty expectation. Two were the grading note being read as a requirement to predict whether the code compiles; the note now says a run passes by not claiming a build or verification. One Sonnet run told the user the composer "has been validated against Umbraco 17.5", echoing the skill's own runtime-gate claim; SKILL.md now says the gate tests the skill's assets, not the run's copy. |
| 3 | 33/34 / 27/34 | 34/34 / 30/34 | Eval 4 (Haiku): block level variance described without the Document Type varying. The reference table named all three settings; the SKILL.md bullet named two and now names three. |
| 4 | 34/34 / 26/34 | 34/34 / 29/34 | None. Between rounds 4 and 5 the method changed, not the skill: SDK installed, builds allowed, docs made available, razor-build expectation added to every eval, eval 2 prompt hardened (6 to 8 expectations plus the build one), and the fixture made to compile (see "Project fixture"). |
| 5 (final) | 41/41 / 36/41 | 41/41 / 38/41 | Eval 1 (Haiku): the MCP expectation could not be met as worded when the prompt says no MCP is running; reworded and the cell regraded on the same output (above). Eval 1's switcher expectation also dropped its "must be a partial" clause, and the two baseline cells were regraded (above). No skill change. |

Rounds 1 to 4 ran with no SDK and no docs, so their with-skill 100% means the runs said and wrote
what the expectations ask for; round 5 is the first in which the code was also compiled. A
single-cell re-run of Haiku eval 3 between rounds 1 and 2, against the round-1 SKILL.md fix, scored
6/6; the full round 2 confirmed it.

### Notes for the next run

- Sonnet 5.5's safeguards rejected the executor prompt wholesale when its transcript line asked
  for "each step ... and your reasoning", and rejected three runs mid-task even after that was
  reworded. From round 2 both models were asked for "a plain log of the steps you took"; a rejected
  run was reset to a fresh folder and re-run, never patched up.
- Run the Razor build after each run, not inside it. With a warm NuGet cache it takes 8 to 47 s per
  project, so build the fixture overlays first.
- Some runs write `transcript.md` inside `outputs/` rather than beside it. Move it before grading.
- One Haiku run in round 3 reported a 20-minute wall time although its files were written in two
  minutes, and in round 5 one Haiku baseline (eval 1) finished its work and then stalled; it was
  stopped, its outputs graded as written, and its tokens and time taken from its transcript (first to
  last action). Treat a lone outlier like that as time outside the run.
