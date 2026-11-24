# Cloud backup

LangNotebook can keep a copy of its data in the user's own **Dropbox** or **Google Drive**. There is no LangNotebook
server: the browser signs in with OAuth and calls the provider directly. The app can only see its own private folder.

Users see a cloud button in the top bar and a **Cloud backup** section in Settings.

## Turning it on for your deployment

A provider's connect button is shown as "Not set up" until you give it a **client ID**. A client ID is public (it
names the app, it is not a secret), so it lives in a plain file: `LangNotebook/wwwroot/js/cloud.config.js`.

```js
window.langCloudConfig = {
    dropbox: { clientId: 'YOUR_DROPBOX_APP_KEY' },
    google:  { clientId: 'YOUR_GOOGLE_CLIENT_ID.apps.googleusercontent.com' }
};
```

You can set up one provider or both. Every provider needs the **redirect URI** of your site:

| Where the app runs | Redirect URI to register |
|---|---|
| Local development | `http://localhost:5096/oauth-callback.html` (and the https port from `launchSettings.json`) |
| Production | `https://YOUR-HOST/oauth-callback.html`. If the app is hosted under a sub-path, put the path before the file name |

The sign-in page must be served over **https** (or `localhost`), because the browser only offers Web Crypto there.

### Dropbox

1. Open the [Dropbox App Console](https://www.dropbox.com/developers/apps) and create an app with **Scoped access**
   and the **App folder** access type.
2. Permissions: `files.metadata.read`, `files.content.read`, `files.content.write`, `account_info.read`.
3. Settings: add the redirect URI(s) above and allow public clients (PKCE).
4. Copy the **App key** into `clientId`.

Dropbox returns a long-lived refresh token, so users sign in once and stay connected.

### Google Drive

1. In the [Google Cloud Console](https://console.cloud.google.com/) create a project and enable the **Google Drive API**.
2. Configure the OAuth consent screen. Scopes: `.../auth/drive.appdata` and `.../auth/userinfo.email`. Neither is a
   sensitive scope, so Google verification is normally not needed.
3. **Publish the app ("In production").** In "Testing" mode only listed test users can sign in and tokens last 7 days.
4. Create an **OAuth client ID** of type *Web application*. Add your origin as an authorised JavaScript origin and
   the redirect URI above.
5. Copy the client ID into `clientId`.

A web page can't keep a client secret, so Google gives the browser only an access token that lasts about an hour. When
it runs out the top-bar button turns into a warning and one click signs in again. Dropbox is smoother for daily use.

Provider consoles change over time; check their current documentation if a step doesn't match.

## What is stored where

| What | Where |
|---|---|
| Backups | The provider's private app folder, one compressed JSON file per backup (`langnotebook-<UTC time>-<device id>-<browser>_<os>.json.gz`) |
| Sign-in tokens, device id, settings | This browser's `localStorage` (`lang-notebook.cloud`). Never part of a backup |
| Copy of the data taken before a restore | This browser's `localStorage` (`lang-notebook.cloud.snapshot`), so a restore can be undone |

AI provider keys are not included in backups (they were never stored in the database).

Backups are **not encrypted**: they are readable by anyone with access to the user's Dropbox or Google account.

## How it behaves

- **Connect.** If the cloud holds no backups, or holds backups from this same device, auto-backup starts at once. If it
  holds backups from other devices only, the user chooses first: *Restore this backup* or *Keep this device's data*.
  This stops an empty new device from pushing out the backups that are there.
- **Auto-backup.** After a change, once the data has been quiet for 90 seconds, and at most once every 10 minutes.
  Unchanged data is not uploaded again (the export's own time stamp is ignored when comparing).
- **Retention.** Each device keeps its last 10 backups. Files from other devices are never deleted.
- **Restore.** The backup is read and checked, a copy of the current data is saved, and only then is the data replaced,
  in a single database transaction. *Undo restore* in Settings puts the previous data back.
- **Offline / errors.** Failures are shown in the top-bar icon and as a message that says what happened and what to do.
  Network problems are retried every 5 minutes; problems that need the user (expired sign-in, full storage) are not.

## Tests

```bash
node tests/cloud/cloud.test.cjs    # sign-in, upload, list, download, retention, errors (mocked providers)
node tests/cloud/dialog.test.cjs   # focus handling for modal dialogs
```

Both need Playwright and its Chromium. The provider calls are intercepted, so nothing leaves the machine.
