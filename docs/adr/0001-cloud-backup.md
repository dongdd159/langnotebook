# ADR-0001: Back up to the user's own cloud storage

**Status:** Accepted (level 1: backup and manual restore)
**Date:** 2026-11-24
**Deciders:** Project owner

## Context
All data lives in one SQLite file kept in the browser's IndexedDB. Browsers can delete it, it exists on one device
only, and the only safety net is a manual file download. There is no server and no accounts, and we want to keep it
that way for now.

## Decision
Add optional backup to the user's own **Dropbox** or **Google Drive**, using OAuth from the browser and each
provider's private app folder. Upload a compressed JSON export (the existing backup format). Back up automatically
after a quiet period and let the user restore any backup. The app has no sign-in of its own.

## Options considered

### A. Cloud files in the user's account (chosen)
| Dimension | Assessment |
|---|---|
| Complexity | Medium: OAuth pop-up, two provider clients, status and restore UI |
| Cost | None to run; one client ID per provider to register |
| Scalability | Per user; data is small (about 40 KB compressed for 60 words and 1,500 reviews) |
| Team familiarity | Good: plain `fetch` and the existing export code |

**Pros:** no server or accounts; data stays in the user's account; works for several devices used one after another.
**Cons:** conflicts can only be resolved by choosing one side; Google gives browsers a one-hour token; all devices
must use the same provider.

### B. Own server with accounts and record-level sync
**Pros:** real multi-device sync, a base for paid features. **Cons:** servers, accounts, security and privacy
duties, and the data model needs GUIDs and soft deletes first. Too much for the problem at hand.

### C. Only the existing manual download
**Pros:** nothing to build. **Cons:** people forget; it does not survive a browser clearing its data.

## Trade-off analysis
A is the smallest step that removes the real risk (losing everything) and also covers "continue on another device".
It deliberately stops short of merging two devices' changes, which needs the data model work that B needs as well, so
nothing built here is wasted if B is chosen later.

## Consequences
- Easier: recovery after cleared browser data; moving to a new device.
- Harder: every new feature that touches data has to keep the backup format working; strings need translating.
- To revisit: encryption with a passphrase; merging instead of replacing; Dropbox/Google differences if a third
  provider is added; moving restore onto a record-level import once IDs are GUIDs.

## Decisions inside this one
- **Format:** JSON, gzip-compressed in the browser. Not the `.db` file: JSON survives schema changes and is readable.
- **Safety on restore:** validate first, save a copy of the current data, replace in one transaction, offer undo.
- **New devices don't auto-backup** until the user chose between restoring and keeping local data, and retention is
  per device, so an empty device cannot push out good backups.
- **Imports are hardened:** `BackupService.ParseBackup` checks the file, limits colours to `#rrggbb`, and the import runs
  in one transaction with review history re-pointed at the new item IDs.

## Action items
1. [x] Sign-in, upload, list, download, retention (`wwwroot/js/cloud.js`) with tests
2. [x] Top-bar button, Settings panel, restore and first-sync dialogs
3. [x] Modal focus handling (Escape, focus trap, return focus)
4. [ ] Register client IDs and try it against the real providers
5. [ ] Optional passphrase encryption
6. [ ] GUID ids and soft deletes, then merge-based sync
