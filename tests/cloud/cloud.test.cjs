// Tests for LangNotebook/wwwroot/js/cloud.js against mocked Dropbox and Google Drive servers, in Chromium.
//
// Run:   node tests/cloud/cloud.test.cjs
// Needs: Playwright (`npm i -D playwright` or a global install exposed through NODE_PATH) and its Chromium.
//
// The app is served from a local http server so the page is a secure context (Web Crypto) and the OAuth pop-up can
// be redirected back to /oauth-callback. Every provider call is intercepted; nothing leaves the machine.
const http = require('http');
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const WWWROOT = path.join(__dirname, '..', '..', 'LangNotebook', 'wwwroot');
const CLIENT_IDS = { dropbox: 'test-dropbox-id', google: 'test-google-id' };
const results = [];

function check(name, condition, detail) {
    results.push({ name, ok: !!condition, detail });
    console.log((condition ? '  ok   ' : '  FAIL ') + name + (condition || !detail ? '' : '  -> ' + detail));
}

// What wwwroot/config/app.config.json (the git-ignored local file) looks like to the page:
//   { json: '...' }  a JSON body        { html: true }  the index page (a host's fallback for a missing file)
//   null             404
const LOCAL_WITH_IDS = { json: JSON.stringify({ cloud: { dropbox: { clientId: CLIENT_IDS.dropbox }, google: { clientId: CLIENT_IDS.google } } }) };

function startServer() {
    const server = http.createServer((req, res) => {
        const url = new URL(req.url, 'http://localhost');
        if (url.pathname === '/test.html') {
            res.setHeader('Content-Type', 'text/html');
            res.end('<!doctype html><html><head><base href="/"></head><body>' +
                '<script src="js/config.js"></script><script src="js/cloud.js"></script></body></html>');
            return;
        }
        // The sign-in window comes back here. A real host answers this extensionless address with index.html, which loads
        // js/oauth-callback.js; the test page does the same.
        if (url.pathname === '/oauth-callback') {
            res.setHeader('Content-Type', 'text/html');
            res.end('<!doctype html><html><head><base href="/"></head><body><div id="app">loading</div><script src="js/oauth-callback.js"></script></body></html>');
            return;
        }
        if (url.pathname === '/config/app.config.json') {
            const local = server.localConfig;
            if (!local) {
                res.statusCode = 404;
                res.end('not found');
            } else if (local.html) {
                res.setHeader('Content-Type', 'text/html');
                res.end('<!doctype html><html><body>index page</body></html>');
            } else {
                res.setHeader('Content-Type', 'application/json');
                res.end(local.json);
            }
            return;
        }
        const file = path.join(WWWROOT, url.pathname);
        if (!file.startsWith(WWWROOT) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) {
            res.statusCode = 404;
            res.end('not found');
            return;
        }
        res.setHeader('Content-Type', file.endsWith('.html') ? 'text/html' : 'text/javascript');
        res.end(fs.readFileSync(file));
    });
    server.localConfig = LOCAL_WITH_IDS;
    return new Promise(resolve => server.listen(0, '127.0.0.1', () => resolve(server)));
}

const CORS = {
    'access-control-allow-origin': '*',
    'access-control-allow-headers': 'authorization, content-type, dropbox-api-arg',
    'access-control-allow-methods': 'GET, POST, DELETE, OPTIONS',
    'access-control-expose-headers': '*'
};

// A fake cloud: files by id, plus a log of what the app sent.
function makeCloud() {
    return { files: [], nextId: 1, log: [], failWith: null, refreshCount: 0 };
}

function json(route, status, body) {
    return route.fulfill({ status, headers: { ...CORS, 'content-type': 'application/json' }, body: JSON.stringify(body) });
}

async function installProviders(context, cloud) {
    await context.route(/^https:\/\/(www\.dropbox\.com|api\.dropboxapi\.com|content\.dropboxapi\.com|accounts\.google\.com|www\.googleapis\.com|oauth2\.googleapis\.com)\//, async route => {
        const request = route.request();
        const url = new URL(request.url());
        const method = request.method();
        if (method === 'OPTIONS') return route.fulfill({ status: 204, headers: CORS });
        cloud.log.push(method + ' ' + url.host + url.pathname);

        if (cloud.abort) return route.abort('internetdisconnected');
        if (cloud.failWith && !url.pathname.includes('oauth2')) {
            const f = cloud.failWith;
            return route.fulfill({ status: f.status, headers: { ...CORS, 'content-type': 'application/json' }, body: f.body || '{}' });
        }

        // ---- sign-in pages: bounce straight back to the app's callback page
        if (url.host === 'www.dropbox.com') {
            const back = new URL(url.searchParams.get('redirect_uri'));
            if (cloud.denyAuth) back.search = '?error=access_denied&state=' + url.searchParams.get('state');
            else back.search = '?code=CODE123&state=' + (cloud.badState ? 'WRONG' : url.searchParams.get('state'));
            return route.fulfill({ status: 302, headers: { location: back.href } });
        }
        if (url.host === 'accounts.google.com') {
            const back = new URL(url.searchParams.get('redirect_uri'));
            back.hash = '#access_token=gtoken&token_type=Bearer&expires_in=3599&state=' + url.searchParams.get('state');
            return route.fulfill({ status: 302, headers: { location: back.href } });
        }

        // ---- Dropbox
        if (url.host === 'api.dropboxapi.com' && url.pathname === '/oauth2/token') {
            const form = new URLSearchParams(request.postData());
            if (form.get('grant_type') === 'refresh_token') {
                cloud.refreshCount++;
                return json(route, 200, { access_token: 'dbx-refreshed-' + cloud.refreshCount, expires_in: 14400 });
            }
            cloud.lastToken = Object.fromEntries(form);
            return json(route, 200, { access_token: 'dbx-access', refresh_token: 'dbx-refresh', expires_in: 14400 });
        }
        if (url.host === 'api.dropboxapi.com' && url.pathname === '/2/users/get_current_account')
            return json(route, 200, { email: 'me@example.com', name: { display_name: 'Me' } });
        if (url.host === 'api.dropboxapi.com' && url.pathname === '/2/files/list_folder')
            return json(route, 200, { has_more: false, cursor: 'c', entries: cloud.files.map(f => ({ '.tag': 'file', id: f.id, name: f.name, server_modified: f.modified, size: f.bytes.length })) });
        if (url.host === 'content.dropboxapi.com' && url.pathname === '/2/files/upload') {
            const arg = JSON.parse(request.headers()['dropbox-api-arg']);
            const file = { id: 'id:' + cloud.nextId++, name: arg.path.slice(1), modified: new Date().toISOString(), bytes: request.postDataBuffer() };
            cloud.files.push(file);
            cloud.lastUploadArg = arg;
            cloud.lastAuth = request.headers().authorization;
            return json(route, 200, { id: file.id, name: file.name, server_modified: file.modified, size: file.bytes.length });
        }
        if (url.host === 'content.dropboxapi.com' && url.pathname === '/2/files/download') {
            const arg = JSON.parse(request.headers()['dropbox-api-arg']);
            const file = cloud.files.find(f => f.id === arg.path);
            if (!file) return json(route, 409, { error_summary: 'path/not_found/' });
            return route.fulfill({ status: 200, headers: { ...CORS, 'content-type': 'application/octet-stream' }, body: file.bytes });
        }
        if (url.host === 'api.dropboxapi.com' && url.pathname === '/2/files/delete_v2') {
            const id = JSON.parse(request.postData()).path;
            cloud.files = cloud.files.filter(f => f.id !== id);
            return json(route, 200, { metadata: {} });
        }
        if (url.host === 'api.dropboxapi.com' && url.pathname === '/2/auth/token/revoke')
            return json(route, 200, {});

        // ---- Google Drive
        if (url.pathname === '/oauth2/v3/userinfo') return json(route, 200, { email: 'me@gmail.com' });
        if (url.host === 'oauth2.googleapis.com') return json(route, 200, {});
        if (url.pathname === '/drive/v3/files' && method === 'GET') {
            if (url.searchParams.get('spaces') !== 'appDataFolder') return json(route, 400, { error: 'wrong space' });
            return json(route, 200, { files: cloud.files.map(f => ({ id: f.id, name: f.name, modifiedTime: f.modified, size: String(f.bytes.length) })) });
        }
        if (url.pathname === '/upload/drive/v3/files') {
            const body = request.postDataBuffer();
            const text = body.toString('latin1');
            const meta = JSON.parse(text.slice(text.indexOf('{'), text.indexOf('}') + 1));
            const start = text.indexOf('\r\n\r\n', text.indexOf('application/gzip')) + 4;
            const end = text.lastIndexOf('\r\n--');
            const file = { id: 'g' + cloud.nextId++, name: meta.name, modified: new Date().toISOString(), bytes: Buffer.from(text.slice(start, end), 'latin1'), parents: meta.parents };
            cloud.files.push(file);
            return json(route, 200, { id: file.id, name: file.name, modifiedTime: file.modified, size: String(file.bytes.length) });
        }
        const driveFile = /^\/drive\/v3\/files\/(.+)$/.exec(url.pathname);
        if (driveFile && method === 'GET') {
            const file = cloud.files.find(f => f.id === decodeURIComponent(driveFile[1]));
            return file ? route.fulfill({ status: 200, headers: CORS, body: file.bytes }) : json(route, 404, {});
        }
        if (driveFile && method === 'DELETE') {
            cloud.files = cloud.files.filter(f => f.id !== decodeURIComponent(driveFile[1]));
            return route.fulfill({ status: 204, headers: CORS });
        }
        return json(route, 500, { error: 'unmocked ' + method + ' ' + url.href });
    });
}

const SAMPLE = JSON.stringify({ Format: 'lang-notebook-backup', Version: 1, ExportedUtc: '2026-11-24T10:00:00Z', Notebooks: [{ Id: 1, Name: 'Japanese' }], VocabEntries: [] });
const SAMPLE_LATER = SAMPLE.replace('2026-11-24T10:00:00Z', '2026-11-24T11:30:00Z');

async function call(page, fn, ...args) {
    const text = await page.evaluate(([f, a]) => window.langCloud[f](...a), [fn, args]);
    return JSON.parse(text);
}

async function freshPage(context, server) {
    const page = await context.newPage();
    await page.goto(`http://127.0.0.1:${server.address().port}/test.html`);
    return page;
}

(async () => {
    const server = await startServer();
    const browser = await chromium.launch();
    try {
        // ---------------------------------------------------------------- helpers
        console.log('helpers');
        {
            const context = await browser.newContext();
            const page = await freshPage(context, server);
            const challenge = await page.evaluate(() => window.langCloud._test.pkceChallenge('dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk'));
            check('PKCE challenge matches the RFC 7636 example', challenge === 'E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM', challenge);
            const stable = await page.evaluate(([a, b]) => [window.langCloud._test.stableText(a), window.langCloud._test.stableText(b)], [SAMPLE, SAMPLE_LATER]);
            check('time stamp is ignored when comparing exports', stable[0] === stable[1] && !stable[0].includes('ExportedUtc'));
            const round = await page.evaluate(async (text) => {
                const packed = await window.langCloud._test.gzip(text);
                return { gz: packed.gz, magic: [packed.bytes[0], packed.bytes[1]], back: await window.langCloud._test.gunzipIfNeeded(packed.bytes.buffer) };
            }, SAMPLE);
            check('gzip round trip', round.gz && round.magic[0] === 0x1f && round.magic[1] === 0x8b && round.back === SAMPLE);
            const parsed = await page.evaluate(() => [
                window.langCloud._test.describeFile({ id: 'x', name: 'langnotebook-20261124T091500Z-a1b2c3d4-Chrome_Windows.json.gz' }),
                window.langCloud._test.describeFile({ id: 'y', name: 'holiday-photo.jpg' })]);
            check('file names are parsed', parsed[0] && parsed[0].deviceId === 'a1b2c3d4' && parsed[0].deviceLabel === 'Chrome on Windows' && parsed[0].createdUtc === '2026-11-24T09:15:00Z');
            check('unrelated files are ignored', parsed[1] === null);
            await context.close();
        }

        // ---------------------------------------------------------------- Dropbox
        console.log('dropbox');
        {
            const context = await browser.newContext();
            const cloud = makeCloud();
            await installProviders(context, cloud);
            const page = await freshPage(context, server);

            const providers = await call(page, 'providers');
            check('both providers are listed as set up', providers.providers.length === 2 && providers.providers.every(p => p.configured));

            const connected = await call(page, 'connect', 'dropbox');
            check('connect succeeds', connected.ok, JSON.stringify(connected));
            check('status says connected with the account', connected.status.connected && connected.status.account === 'me@example.com' && connected.status.provider === 'dropbox');
            check('the redirect address has no file extension', cloud.lastToken && cloud.lastToken.redirect_uri === `http://127.0.0.1:${server.address().port}/oauth-callback`, cloud.lastToken && cloud.lastToken.redirect_uri);
            check('token exchange sent a PKCE verifier and no secret', cloud.lastToken && cloud.lastToken.code_verifier && cloud.lastToken.code_verifier.length >= 43 && !cloud.lastToken.client_secret, JSON.stringify(cloud.lastToken));
            check('new device is not armed for auto-backup yet', connected.status.armed === false);

            await call(page, 'setArmed', true);
            const first = await call(page, 'backup', SAMPLE, false);
            check('first backup uploads', first.ok && first.uploaded && first.file, JSON.stringify(first));
            check('upload name follows the pattern', /^langnotebook-\d{8}T\d{6}Z-[0-9a-f]{8}-[A-Za-z]+_[A-Za-z]+\.json\.gz$/.test(cloud.files[0].name), cloud.files[0].name);
            check('upload is gzip and bearer-authenticated', cloud.files[0].bytes[0] === 0x1f && cloud.lastAuth === 'Bearer dbx-access');

            const same = await call(page, 'backup', SAMPLE_LATER, false);
            check('same data with a new time stamp is skipped', same.ok && same.uploaded === false && same.skipped === 'unchanged', JSON.stringify(same));
            check('skipped backup uploaded nothing', cloud.files.length === 1);
            const forced = await call(page, 'backup', SAMPLE_LATER, true);
            check('force uploads anyway', forced.ok && forced.uploaded && cloud.files.length === 2);

            const changed = await call(page, 'backup', SAMPLE.replace('Japanese', 'Korean'), false);
            check('changed data uploads', changed.ok && changed.uploaded && cloud.files.length === 3);

            const list = await call(page, 'list');
            check('list returns this device\'s backups, newest first', list.ok && list.files.length === 3 && list.files.every(f => f.thisDevice) && list.files[0].createdUtc >= list.files[2].createdUtc);

            // Time stamps have one-second resolution, so ask for the Korean backup by its id, not by position.
            const download = await call(page, 'download', changed.file.id);
            check('download returns the original JSON', download.ok && download.json.includes('Korean'), JSON.stringify(download).slice(0, 200));

            // retention: other devices are never touched, this device keeps 10
            const deviceId = list.files[0].deviceId;
            for (let i = 0; i < 12; i++)
                cloud.files.push({ id: 'old' + i, name: `langnotebook-2026010${(i % 9) + 1}T0${i % 10}0000Z-${deviceId}-Chrome_Windows.json.gz`.replace(/T0(\d)0000Z/, 'T0$10000Z'), modified: new Date().toISOString(), bytes: Buffer.from([0x1f, 0x8b, 1]) });
            cloud.files.push({ id: 'other', name: 'langnotebook-20250101T000000Z-deadbeef-Safari_iOS.json.gz', modified: new Date().toISOString(), bytes: Buffer.from([0x1f, 0x8b, 1]) });
            cloud.files.push({ id: 'foreign', name: 'notes.txt', modified: new Date().toISOString(), bytes: Buffer.from('x') });
            const thai = await call(page, 'backup', SAMPLE.replace('Japanese', 'Thai'), false);
            const after = await call(page, 'list');
            const mine = after.files.filter(f => f.thisDevice);
            check('this device keeps its last 10 backups', mine.length === 10, String(mine.length));
            check('another device\'s backup is left alone', cloud.files.some(f => f.id === 'other'));
            check('files that are not backups are left alone', cloud.files.some(f => f.id === 'foreign'));
            check('the backup just made survived pruning', thai.ok && mine.some(f => f.id === thai.file.id));

            const snapshot = await call(page, 'saveSnapshot', SAMPLE);
            check('snapshot saved and reported', snapshot.ok && snapshot.status.hasSnapshot);
            const loaded = await call(page, 'loadSnapshot');
            check('snapshot loads back unchanged', loaded.ok && loaded.json === SAMPLE);
            const cleared = await call(page, 'clearSnapshot');
            check('snapshot can be cleared', cleared.ok && !cleared.status.hasSnapshot);

            const off = await call(page, 'setAutoBackup', false);
            check('auto-backup can be turned off', off.status.autoBackup === false);

            const gone = await call(page, 'disconnect');
            check('disconnect clears the connection', gone.ok && !gone.status.connected && cloud.log.includes('POST api.dropboxapi.com/2/auth/token/revoke'));
            const after2 = await call(page, 'backup', SAMPLE, false);
            check('backup after disconnect says not connected', !after2.ok && after2.code === 'not-connected', JSON.stringify(after2));
            await context.close();
        }

        // ---------------------------------------------------------------- Dropbox: refresh and errors
        console.log('dropbox errors');
        {
            const context = await browser.newContext();
            const cloud = makeCloud();
            await installProviders(context, cloud);
            const page = await freshPage(context, server);
            await call(page, 'connect', 'dropbox');
            await call(page, 'setArmed', true);

            // expire the access token: the refresh token should be used silently
            await page.evaluate(() => {
                const key = 'lang-notebook.cloud';
                const state = JSON.parse(localStorage.getItem(key));
                state.token.expiresAt = Date.now() - 1000;
                localStorage.setItem(key, JSON.stringify(state));
            });
            await page.reload();
            const status = await call(page, 'status');
            check('an expired token with a refresh token does not need reconnecting', status.status.connected && !status.status.needsReconnect);
            const refreshed = await call(page, 'backup', SAMPLE, true);
            check('expired token is refreshed and the backup goes through', refreshed.ok && cloud.refreshCount === 1 && cloud.lastAuth === 'Bearer dbx-refreshed-1', JSON.stringify(refreshed));

            cloud.failWith = { status: 409, body: JSON.stringify({ error_summary: 'path/insufficient_space/..' }) };
            const full = await call(page, 'backup', SAMPLE_LATER.replace('Japanese', 'Quota'), true);
            check('full storage is reported as quota', !full.ok && full.code === 'quota', JSON.stringify(full));

            cloud.failWith = { status: 500 };
            const down = await call(page, 'backup', SAMPLE_LATER.replace('Japanese', 'Down'), true);
            check('a provider outage is reported as provider', !down.ok && down.code === 'provider', JSON.stringify(down));

            cloud.failWith = { status: 429 };
            const busy = await call(page, 'list');
            check('rate limiting is reported as provider', !busy.ok && busy.code === 'provider');
            cloud.failWith = null;

            // A request that can't get through while the browser still thinks it is online: a network problem.
            cloud.abort = true;
            const network = await call(page, 'backup', SAMPLE_LATER.replace('Japanese', 'Network'), true);
            check('an unreachable service is reported as network', !network.ok && network.code === 'network', JSON.stringify(network));
            // The browser knows it is offline: offline.
            await context.setOffline(true);
            const offline = await call(page, 'backup', SAMPLE_LATER.replace('Japanese', 'Offline'), true);
            check('no connection is reported as offline', !offline.ok && offline.code === 'offline', JSON.stringify(offline));
            await context.setOffline(false);
            cloud.abort = false;
            const recovered = await call(page, 'backup', SAMPLE_LATER.replace('Japanese', 'Recovered'), true);
            check('backups work again once the connection is back', recovered.ok && recovered.uploaded, JSON.stringify(recovered).slice(0, 200));
            await context.close();
        }

        // ---------------------------------------------------------------- sign-in problems
        console.log('sign-in');
        {
            const context = await browser.newContext();
            const cloud = makeCloud();
            await installProviders(context, cloud);
            const page = await freshPage(context, server);

            cloud.denyAuth = true;
            const denied = await call(page, 'connect', 'dropbox');
            check('denying access is reported as cancelled', !denied.ok && denied.code === 'cancelled', JSON.stringify(denied));
            cloud.denyAuth = false;

            cloud.badState = true;
            const forged = await call(page, 'connect', 'dropbox');
            check('a response with the wrong state is rejected', !forged.ok && forged.code === 'provider', JSON.stringify(forged));
            check('nothing is connected after a forged response', !forged.status.connected);
            cloud.badState = false;

            await page.evaluate(() => { window.open = () => null; });
            const blocked = await call(page, 'connect', 'dropbox');
            check('a blocked pop-up is reported', !blocked.ok && blocked.code === 'popup-blocked', JSON.stringify(blocked));
            await context.close();

            const bare = await browser.newContext();
            server.localConfig = null;
            const barePage = await freshPage(bare, server);
            const missing = await call(barePage, 'connect', 'dropbox');
            check('a provider without a client ID is reported as not configured', !missing.ok && missing.code === 'not-configured', JSON.stringify(missing));
            await bare.close();
            server.localConfig = LOCAL_WITH_IDS;
        }

        // ---------------------------------------------------------------- configuration file
        console.log('configuration');
        {
            // wwwroot/config/app.config.json is the one configuration file and is optional: whatever is wrong with it,
            // the page must still load and the cloud buttons must just read "not set up".
            const scenarios = [
                ['no file', null, false],
                ['a file the host answers with the index page', { html: true }, false],
                ['a file that is not valid JSON', { json: '{ nope' }, false],
                ['a file that is not an object', { json: '[1,2]' }, false],
                ['a file without a cloud section', { json: JSON.stringify({ ai: { providers: [] } }) }, false],
                ['a file with an empty client ID', { json: JSON.stringify({ cloud: { dropbox: { clientId: '  ' }, google: { clientId: '' } } }) }, false],
                ['a file with client IDs', LOCAL_WITH_IDS, true]
            ];
            for (const [label, local, configured] of scenarios) {
                server.localConfig = local;
                const context = await browser.newContext();
                const page = await freshPage(context, server);
                const providers = await call(page, 'providers');
                const ok = providers.ok && providers.providers.length === 2 && providers.providers.every(p => p.configured === configured);
                check(`${label}: providers are ${configured ? '' : 'not '}set up`, ok, JSON.stringify(providers.providers));
                await context.close();
            }

            // only one provider configured
            server.localConfig = { json: JSON.stringify({ cloud: { dropbox: { clientId: CLIENT_IDS.dropbox } } }) };
            const context = await browser.newContext();
            const page = await freshPage(context, server);
            const providers = await call(page, 'providers');
            const byId = Object.fromEntries(providers.providers.map(p => [p.id, p.configured]));
            check('a provider can be set up on its own', byId.dropbox === true && byId.google === false, JSON.stringify(byId));
            await context.close();
            server.localConfig = LOCAL_WITH_IDS;
        }

        // ---------------------------------------------------------------- cancel while waiting
        console.log('cancel');
        {
            const context = await browser.newContext();
            await context.route(/^https:\/\/www\.dropbox\.com\//, route => route.fulfill({ status: 200, headers: { 'content-type': 'text/html' }, body: '<p>waiting</p>' }));
            const page = await freshPage(context, server);
            const pending = page.evaluate(() => window.langCloud.connect('dropbox'));
            await page.waitForTimeout(600);
            await call(page, 'cancelConnect');
            const result = JSON.parse(await pending);
            check('cancelling while waiting ends the sign-in', !result.ok && result.code === 'cancelled', JSON.stringify(result));
            await context.close();
        }

        // ---------------------------------------------------------------- Google Drive
        console.log('google drive');
        {
            const context = await browser.newContext();
            const cloud = makeCloud();
            await installProviders(context, cloud);
            const page = await freshPage(context, server);
            const connected = await call(page, 'connect', 'google');
            check('connect succeeds with the implicit flow', connected.ok && connected.status.provider === 'google' && connected.status.account === 'me@gmail.com', JSON.stringify(connected));
            await call(page, 'setArmed', true);
            const backed = await call(page, 'backup', SAMPLE, false);
            check('backup goes to the app data folder', backed.ok && backed.uploaded && cloud.files.length === 1 && JSON.stringify(cloud.files[0].parents) === '["appDataFolder"]', JSON.stringify(backed));
            check('uploaded bytes are intact gzip', cloud.files[0].bytes[0] === 0x1f && cloud.files[0].bytes[1] === 0x8b);
            const list = await call(page, 'list');
            const download = await call(page, 'download', list.files[0].id);
            check('download returns the original JSON', download.ok && download.json === SAMPLE, JSON.stringify(download).slice(0, 160));

            await page.evaluate(() => {
                const key = 'lang-notebook.cloud';
                const state = JSON.parse(localStorage.getItem(key));
                state.token.expiresAt = Date.now() - 1000;
                localStorage.setItem(key, JSON.stringify(state));
            });
            await page.reload();
            const status = await call(page, 'status');
            check('an expired Google token needs reconnecting', status.status.connected && status.status.needsReconnect === true);
            const expired = await call(page, 'backup', SAMPLE_LATER.replace('Japanese', 'Late'), true);
            check('backup with an expired Google token asks to sign in again', !expired.ok && expired.code === 'auth-expired', JSON.stringify(expired));
            const again = await call(page, 'connect', 'google');
            check('signing in again restores the connection', again.ok && !again.status.needsReconnect);
            check('re-connecting the same provider keeps the device chosen state', again.status.armed === true);
            await context.close();
        }
    } finally {
        await browser.close();
        server.close();
    }

    const failed = results.filter(r => !r.ok);
    console.log(`\n${results.length - failed.length}/${results.length} checks passed`);
    process.exit(failed.length ? 1 : 0);
})();
