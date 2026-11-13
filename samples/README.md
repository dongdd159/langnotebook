# Sample data

`sample-data.json` is a backup file with realistic test data for every page:

| What | Amount |
|---|---|
| Notebooks | 4: Japanese N3, English IELTS, Korean Beginner, Spanish travel (archived) |
| Vocabulary | 60 words across all four stages (New, Learning, Young, Mature), about a third due now, some starred, one suspended |
| Grammar | 13 patterns with examples, common mistakes and levels |
| Writing | 6 prompts, 7 articles (finished and drafts, scored 58–84); “Should cities ban cars?” includes a saved AI review with 3 suggested edits |
| Review history | ~1,500 reviews over the last 12 weeks, a 12-day streak and 18 of 30 reviews done today |
| Settings | Daily goal 30, weekly writing goal 1,500 words |

## Load it

1. Run the app and open **Settings › Backup & data**.
2. Tick **Clear existing data before importing**. Without it, the sample notebooks are added next to yours.
3. Choose `samples/sample-data.json`.

## Refresh the dates

Dates are relative to when the file was generated, so cards stop being “due today” after a while.
Generate a fresh copy before testing:

```bash
python3 tools/generate_sample_data.py
```

Use `--seed <n>` for a different review history or `-o <path>` to write somewhere else.
