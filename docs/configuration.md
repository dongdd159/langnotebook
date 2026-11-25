# Configuration

Every value that differs between deployments lives in **one JSON file** that both the C# code and the JavaScript read.

| File | In git? | Purpose |
|---|---|---|
| `LangNotebook/wwwroot/config/app.config.example.json` | yes | Every default: the AI providers and endpoints, the Anthropic API headers, empty client IDs |
| `LangNotebook/wwwroot/config/app.config.json` | **no** (git-ignored) | This deployment's own values. Optional |

A top-level section in `app.config.json` (`cloud`, `ai`) **replaces** the same section of the example. Sections it leaves
out come from the example. A missing, unreadable or non-JSON `app.config.json` is skipped, so the app still starts
(without client IDs, the cloud buttons show "Not set up").

## What is in it

| Section | Keys | Used by |
|---|---|---|
| `cloud.dropbox`, `cloud.google` | `clientId` | `wwwroot/js/cloud.js` (cloud backup sign-in) |
| `ai.anthropic` | `apiVersion`, `betaHeader` | `Service/WritingAiService.cs` (Anthropic request headers) |
| `ai.providers[]` | `id`, `name`, `protocol` (`anthropic` or `openai`), `endpoint`, `defaultModel`, `modelHint`, `keyRequired` | `Service/AiProviders.cs` and the AI settings in Settings |

The `anthropic` and `custom` entries must stay in the providers list: the code refers to them by id. Every endpoint has
to answer a browser CORS preflight, because the app has no server to relay requests.

Not in this file, on purpose: the spaced-repetition maths (FSRS weights, stage thresholds), scoring rules, UI limits and
timings. They are part of how the app behaves, not of where it is deployed.

## Setting it up

Local development:

```bash
cp LangNotebook/wwwroot/config/app.config.example.json LangNotebook/wwwroot/config/app.config.json
# then edit cloud.dropbox.clientId / cloud.google.clientId
```

CI or a deployment script, without keeping the file in the repository:

```bash
LANGNOTEBOOK_DROPBOX_CLIENT_ID=... LANGNOTEBOOK_GOOGLE_CLIENT_ID=... python3 tools/write_app_config.py
dotnet publish -c Release
```

`tools/write_app_config.py` copies the example and fills in the values; `--set path=value` changes any other text value
(for example `--set ai.anthropic.apiVersion=...`). Run it before `dotnet publish`, because the file is published with
the app. Store the values in your CI's secret store, and keep them out of logs.

## What "secure" means here

LangNotebook runs entirely in the browser, so **everything in these files is downloaded by every visitor** and can be read
at `/config/app.config.json`. Keeping the file out of git stops the values appearing in the repository and its history;
it cannot hide them from someone who opens the deployed site.

That is fine for what the file holds: a client ID is public by design (it names the app to Dropbox or Google), and
the AI endpoints are public URLs. **Never put an API key, a client secret or a password in it.** Users' own AI keys are
typed into Settings and stay in their browser's `localStorage`; they never touch this file.
