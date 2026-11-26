# Configuration

Every value that differs between deployments lives in **one JSON file**: `LangNotebook/wwwroot/config/app.config.json`.
It is **git-ignored** and **optional**. The template is [`docs/app.config.example.json`](app.config.example.json).

| What | Where |
|---|---|
| The file you edit | `LangNotebook/wwwroot/config/app.config.json` (not in git) |
| Template to copy | `docs/app.config.example.json` (in git; never loaded by the app) |
| Built-in defaults | `Service/AppConfig.cs` (`AiConfig.CreateDefaults`) |

A section in the file (`cloud`, `ai`) **replaces** the same section of the defaults. A missing, unreadable or invalid file
is skipped, so the app always starts: without client IDs the cloud buttons show "Not set up", and the AI providers are the
built-in ones. A broken `ai` section is reported in the browser console and the built-in providers are used instead.

## What is in it

| Section | Keys | Default | Used by |
|---|---|---|---|
| `cloud.dropbox`, `cloud.google` | `clientId` | empty (not set up) | `wwwroot/js/cloud.js` |
| `ai.anthropic` | `apiVersion`, `betaHeader` | built in | `Service/WritingAiService.cs` |
| `ai.providers[]` | `id`, `name`, `protocol` (`anthropic` or `openai`), `endpoint`, `defaultModel`, `modelHint`, `keyRequired` | the ten providers listed in `CreateDefaults` | `Service/AiProviders.cs`, AI settings |

If you give an `ai.providers` list it replaces the whole built-in list, and it must keep the `anthropic` and `custom`
entries (the code refers to them by id). Every endpoint has to answer a browser CORS preflight, because the app has no
server to relay requests.

Not configurable on purpose: the spaced-repetition maths (FSRS weights, stage thresholds), scoring rules, UI limits and
timings. They are part of how the app behaves, not of where it is deployed.

## Setting it up

Local development: copy the template and edit it.

```bash
cp docs/app.config.example.json LangNotebook/wwwroot/config/app.config.json
# then fill in cloud.dropbox.clientId / cloud.google.clientId
```

The file only needs the parts you want to change, for example just the client IDs:

```json
{ "cloud": { "dropbox": { "clientId": "YOUR_DROPBOX_APP_KEY" } } }
```

CI or a deployment script, without keeping the file in the repository:

```bash
LANGNOTEBOOK_DROPBOX_CLIENT_ID=... LANGNOTEBOOK_GOOGLE_CLIENT_ID=... python3 tools/write_app_config.py
dotnet publish -c Release
```

`tools/write_app_config.py` copies the template and fills in the values; `--set path=value` changes any other text value
(for example `--set ai.anthropic.apiVersion=...`). Run it before `dotnet publish`, because the file is published with
the app. Keep the values in your CI's secret store and out of logs. A bare clone has no configuration file, which is fine:
the app runs with the defaults.

## What "secure" means here

LangNotebook runs entirely in the browser, so **everything in this file is downloaded by every visitor** and can be read
at `/config/app.config.json`. Keeping the file out of git stops the values appearing in the repository and its history;
it cannot hide them from someone who opens the deployed site.

That is fine for what the file holds: a client ID is public by design (it names the app to Dropbox or Google), and the
AI endpoints are public URLs. **Never put an API key, a client secret or a password in it.** Users' own AI keys are typed
into Settings and stay in their browser's `localStorage`; they never touch this file.
