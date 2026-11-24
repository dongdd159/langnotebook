// Cloud backup: sends a compressed JSON copy of the app's data to the user's own Dropbox or Google Drive and
// reads it back. The app has no server: this script signs in with OAuth (a pop-up window), keeps the tokens in
// localStorage and calls the provider's HTTP API directly. Files go to the app's private folder only.
//
// Every public method returns a JSON string: { ok: true, ... } or { ok: false, code, message }, plus the current
// status, so the C# side never has to deal with JavaScript exceptions.
window.langCloud = (function () {
    'use strict';

    var STORAGE_KEY = 'lang-notebook.cloud';
    var SNAPSHOT_KEY = 'lang-notebook.cloud.snapshot';
    var KEEP_PER_DEVICE = 10;
    var CALLBACK_PAGE = 'oauth-callback.html';
    var AUTH_TIMEOUT_MS = 5 * 60 * 1000;
    // langnotebook-20261124T091500Z-a1b2c3d4-Chrome_Windows.json.gz
    var FILE_PATTERN = /^langnotebook-(\d{8}T\d{6}Z)-([0-9a-f]{8})(?:-([A-Za-z0-9_]+))?\.json(?:\.gz)?$/;

    // ------------------------------------------------------------------ errors

    function CloudError(code, message) {
        this.name = 'CloudError';
        this.code = code;
        this.message = message || code;
    }

    CloudError.prototype = Object.create(Error.prototype);

    function HttpError(status, body) {
        this.name = 'HttpError';
        this.status = status;
        this.body = body || '';
        this.message = 'HTTP ' + status;
    }

    HttpError.prototype = Object.create(Error.prototype);

    // ------------------------------------------------------------------ small helpers

    function qs(values) {
        return Object.keys(values).map(function (key) {
            return encodeURIComponent(key) + '=' + encodeURIComponent(values[key]);
        }).join('&');
    }

    function base64Url(bytes) {
        var text = '';
        for (var i = 0; i < bytes.length; i++) text += String.fromCharCode(bytes[i]);
        return btoa(text).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
    }

    function randomBytes(count) {
        var bytes = new Uint8Array(count);
        crypto.getRandomValues(bytes);
        return bytes;
    }

    function randomString(count) {
        return base64Url(randomBytes(count));
    }

    function toHex(bytes) {
        return Array.prototype.map.call(bytes, function (b) {
            return ('0' + b.toString(16)).slice(-2);
        }).join('');
    }

    function sha256(text) {
        return crypto.subtle.digest('SHA-256', new TextEncoder().encode(text)).then(function (buffer) {
            return new Uint8Array(buffer);
        });
    }

    function pkceChallenge(verifier) {
        return sha256(verifier).then(base64Url);
    }

    // The export carries its own time stamp; leave it out so identical data gives the same hash.
    function stableText(json) {
        return json.replace(/"ExportedUtc"\s*:\s*"[^"]*"\s*,?/, '');
    }

    function gzip(text) {
        if (typeof CompressionStream === 'undefined')
            return Promise.resolve({ bytes: new TextEncoder().encode(text), gz: false });
        var stream = new Blob([text]).stream().pipeThrough(new CompressionStream('gzip'));
        return new Response(stream).arrayBuffer().then(function (buffer) {
            return { bytes: new Uint8Array(buffer), gz: true };
        });
    }

    // Files are recognised by their first two bytes, not their name.
    function gunzipIfNeeded(buffer) {
        var bytes = new Uint8Array(buffer);
        if (bytes.length > 2 && bytes[0] === 0x1f && bytes[1] === 0x8b) {
            if (typeof DecompressionStream === 'undefined')
                throw new CloudError('invalid', 'This browser can\'t open compressed backups.');
            var stream = new Blob([bytes]).stream().pipeThrough(new DecompressionStream('gzip'));
            return new Response(stream).text();
        }
        return Promise.resolve(new TextDecoder().decode(bytes));
    }

    function bytesToBase64(bytes) {
        var text = '';
        var chunk = 0x8000;
        for (var i = 0; i < bytes.length; i += chunk)
            text += String.fromCharCode.apply(null, bytes.subarray(i, i + chunk));
        return btoa(text);
    }

    function base64ToBytes(text) {
        var raw = atob(text);
        var bytes = new Uint8Array(raw.length);
        for (var i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
        return bytes;
    }

    function browserName(ua) {
        if (/Edg\//.test(ua)) return 'Edge';
        if (/OPR\//.test(ua)) return 'Opera';
        if (/Firefox\//.test(ua)) return 'Firefox';
        if (/Chrome\//.test(ua)) return 'Chrome';
        if (/Safari\//.test(ua)) return 'Safari';
        return 'Browser';
    }

    function osName(ua) {
        if (/Windows/.test(ua)) return 'Windows';
        if (/Android/.test(ua)) return 'Android';
        if (/iPhone|iPad|iPod/.test(ua)) return 'iOS';
        if (/Mac OS X|Macintosh/.test(ua)) return 'macOS';
        if (/Linux/.test(ua)) return 'Linux';
        return 'Unknown';
    }

    function timestamp(date) {
        return date.toISOString().replace(/[-:]/g, '').replace(/\.\d+Z$/, 'Z');
    }

    function parseTimestamp(text) {
        return text.slice(0, 4) + '-' + text.slice(4, 6) + '-' + text.slice(6, 8) + 'T' +
            text.slice(9, 11) + ':' + text.slice(11, 13) + ':' + text.slice(13, 15) + 'Z';
    }

    function redirectUri() {
        return new URL(CALLBACK_PAGE, document.baseURI).href;
    }

    // ------------------------------------------------------------------ persisted state

    function loadState() {
        try {
            return JSON.parse(localStorage.getItem(STORAGE_KEY)) || {};
        } catch (e) {
            return {};
        }
    }

    var state = loadState();

    function persist() {
        try {
            localStorage.setItem(STORAGE_KEY, JSON.stringify(state));
        } catch (e) { }
    }

    function ensureDevice() {
        if (!state.deviceId) {
            state.deviceId = toHex(randomBytes(4));
            persist();
        }
        return state.deviceId;
    }

    function deviceLabel() {
        return browserName(navigator.userAgent) + '_' + osName(navigator.userAgent);
    }

    function displayLabel(label) {
        var parts = (label || '').split('_');
        return parts.length === 2 ? parts[0] + ' on ' + parts[1] : (label || 'Another device');
    }

    // ------------------------------------------------------------------ HTTP

    function request(url, init) {
        return fetch(url, init).then(function (response) {
            if (response.ok) return response;
            return response.text().then(function (body) {
                throw new HttpError(response.status, body);
            });
        }, function () {
            throw new CloudError(navigator.onLine === false ? 'offline' : 'network',
                'Couldn\'t reach the cloud service.');
        });
    }

    function requestJson(url, init) {
        return request(url, init).then(function (response) {
            return response.text().then(function (text) {
                return text ? JSON.parse(text) : {};
            });
        });
    }

    function providerError(error, providerName) {
        if (error instanceof CloudError) return error;
        if (error instanceof HttpError) {
            var body = error.body || '';
            if (error.status === 401) return new CloudError('auth-expired', 'The sign-in has expired.');
            if (/insufficient_space|storageQuotaExceeded|quotaExceeded/i.test(body))
                return new CloudError('quota', providerName + ' storage is full.');
            if (error.status === 429) return new CloudError('provider', providerName + ' is busy. Try again in a moment.');
            if (error.status >= 500) return new CloudError('provider', providerName + ' is having problems. Try again in a moment.');
            return new CloudError('provider', providerName + ' answered with an error (' + error.status + ').');
        }
        return new CloudError('provider', (error && error.message) || 'Something went wrong.');
    }

    // ------------------------------------------------------------------ providers

    function clientIdFor(id) {
        var config = (window.langCloudConfig || {})[id];
        return config && config.clientId ? config.clientId : '';
    }

    var dropbox = {
        id: 'dropbox',
        name: 'Dropbox',

        authorizeUrl: function (o) {
            return 'https://www.dropbox.com/oauth2/authorize?' + qs({
                client_id: o.clientId, response_type: 'code', code_challenge: o.challenge,
                code_challenge_method: 'S256', token_access_type: 'offline', redirect_uri: o.redirect, state: o.state
            });
        },

        exchange: function (o, params) {
            return requestJson('https://api.dropboxapi.com/oauth2/token', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: qs({
                    code: params.code, grant_type: 'authorization_code', client_id: o.clientId,
                    code_verifier: o.verifier, redirect_uri: o.redirect
                })
            }).then(function (r) {
                return { access: r.access_token, refresh: r.refresh_token || null,
                    expiresAt: Date.now() + (r.expires_in || 14400) * 1000 };
            });
        },

        refresh: function (token) {
            return requestJson('https://api.dropboxapi.com/oauth2/token', {
                method: 'POST',
                headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: qs({ grant_type: 'refresh_token', refresh_token: token.refresh, client_id: clientIdFor('dropbox') })
            }).then(function (r) {
                return { access: r.access_token, refresh: token.refresh,
                    expiresAt: Date.now() + (r.expires_in || 14400) * 1000 };
            });
        },

        rpc: function (token, url, body) {
            var init = { method: 'POST', headers: { Authorization: 'Bearer ' + token } };
            if (body !== undefined) {
                init.headers['Content-Type'] = 'application/json';
                init.body = JSON.stringify(body);
            }
            return requestJson(url, init);
        },

        account: function (token) {
            return this.rpc(token, 'https://api.dropboxapi.com/2/users/get_current_account').then(function (r) {
                return r.email || (r.name && r.name.display_name) || '';
            });
        },

        list: function (token) {
            var self = this;
            var files = [];

            function collect(result) {
                result.entries.forEach(function (entry) {
                    if (entry['.tag'] === 'file')
                        files.push({ id: entry.id, name: entry.name, modifiedUtc: entry.server_modified, size: entry.size });
                });
                if (!result.has_more) return files;
                return self.rpc(token, 'https://api.dropboxapi.com/2/files/list_folder/continue',
                    { cursor: result.cursor }).then(collect);
            }

            return self.rpc(token, 'https://api.dropboxapi.com/2/files/list_folder', { path: '', limit: 200 }).then(collect);
        },

        upload: function (token, name, bytes) {
            return requestJson('https://content.dropboxapi.com/2/files/upload', {
                method: 'POST',
                headers: {
                    Authorization: 'Bearer ' + token,
                    'Content-Type': 'application/octet-stream',
                    'Dropbox-API-Arg': JSON.stringify({ path: '/' + name, mode: 'add', autorename: true, mute: true })
                },
                body: new Blob([bytes])
            }).then(function (r) {
                return { id: r.id, name: r.name, modifiedUtc: r.server_modified, size: r.size };
            });
        },

        download: function (token, id) {
            return request('https://content.dropboxapi.com/2/files/download', {
                method: 'POST',
                headers: { Authorization: 'Bearer ' + token, 'Dropbox-API-Arg': JSON.stringify({ path: id }) }
            }).then(function (response) {
                return response.arrayBuffer();
            });
        },

        remove: function (token, id) {
            return this.rpc(token, 'https://api.dropboxapi.com/2/files/delete_v2', { path: id });
        },

        revoke: function (token) {
            return this.rpc(token, 'https://api.dropboxapi.com/2/auth/token/revoke');
        }
    };

    // Google only hands a browser an access token (about an hour); getting a refresh token needs a client secret,
    // which a web page can't keep. When the token runs out the user signs in again with one click.
    var google = {
        id: 'google',
        name: 'Google Drive',

        authorizeUrl: function (o) {
            return 'https://accounts.google.com/o/oauth2/v2/auth?' + qs({
                client_id: o.clientId, redirect_uri: o.redirect, response_type: 'token', state: o.state,
                scope: 'https://www.googleapis.com/auth/drive.appdata https://www.googleapis.com/auth/userinfo.email',
                include_granted_scopes: 'true'
            });
        },

        exchange: function (o, params) {
            if (!params.access_token) return Promise.reject(new CloudError('provider', 'Google didn\'t return a token.'));
            return Promise.resolve({ access: params.access_token, refresh: null,
                expiresAt: Date.now() + (parseInt(params.expires_in, 10) || 3600) * 1000 });
        },

        refresh: null,

        account: function (token) {
            return requestJson('https://www.googleapis.com/oauth2/v3/userinfo',
                { headers: { Authorization: 'Bearer ' + token } }).then(function (r) {
                return r.email || '';
            });
        },

        list: function (token) {
            var files = [];

            function page(pageToken) {
                var url = 'https://www.googleapis.com/drive/v3/files?' + qs({
                    spaces: 'appDataFolder', pageSize: 100, fields: 'nextPageToken,files(id,name,modifiedTime,size)',
                    orderBy: 'createdTime desc'
                }) + (pageToken ? '&pageToken=' + encodeURIComponent(pageToken) : '');
                return requestJson(url, { headers: { Authorization: 'Bearer ' + token } }).then(function (r) {
                    (r.files || []).forEach(function (f) {
                        files.push({ id: f.id, name: f.name, modifiedUtc: f.modifiedTime, size: parseInt(f.size, 10) || 0 });
                    });
                    return r.nextPageToken ? page(r.nextPageToken) : files;
                });
            }

            return page(null);
        },

        upload: function (token, name, bytes) {
            var boundary = 'langnotebook' + randomString(12);
            var metadata = JSON.stringify({ name: name, parents: ['appDataFolder'], mimeType: 'application/gzip' });
            var body = new Blob([
                '--' + boundary + '\r\nContent-Type: application/json; charset=UTF-8\r\n\r\n' + metadata + '\r\n',
                '--' + boundary + '\r\nContent-Type: application/gzip\r\n\r\n', bytes, '\r\n--' + boundary + '--'
            ]);
            return requestJson('https://www.googleapis.com/upload/drive/v3/files?uploadType=multipart&fields=id,name,modifiedTime,size', {
                method: 'POST',
                headers: { Authorization: 'Bearer ' + token, 'Content-Type': 'multipart/related; boundary=' + boundary },
                body: body
            }).then(function (r) {
                return { id: r.id, name: r.name, modifiedUtc: r.modifiedTime, size: parseInt(r.size, 10) || 0 };
            });
        },

        download: function (token, id) {
            return request('https://www.googleapis.com/drive/v3/files/' + encodeURIComponent(id) + '?alt=media',
                { headers: { Authorization: 'Bearer ' + token } }).then(function (response) {
                return response.arrayBuffer();
            });
        },

        remove: function (token, id) {
            return request('https://www.googleapis.com/drive/v3/files/' + encodeURIComponent(id),
                { method: 'DELETE', headers: { Authorization: 'Bearer ' + token } });
        },

        revoke: function (token) {
            return request('https://oauth2.googleapis.com/revoke', {
                method: 'POST', headers: { 'Content-Type': 'application/x-www-form-urlencoded' },
                body: qs({ token: token })
            });
        }
    };

    var PROVIDERS = { dropbox: dropbox, google: google };

    function currentProvider() {
        var provider = PROVIDERS[state.provider];
        if (!provider || !state.token) throw new CloudError('not-connected', 'Not connected to a cloud service.');
        return provider;
    }

    // ------------------------------------------------------------------ sign-in

    var pendingAuth = null;

    // Opens the provider's sign-in page and resolves with the parameters it redirected back with.
    function openSignIn(url, expectedState) {
        return new Promise(function (resolve, reject) {
            var popup = window.open(url, 'lang-cloud-auth', 'popup=yes,width=520,height=700');
            if (!popup) {
                reject(new CloudError('popup-blocked', 'The sign-in window was blocked.'));
                return;
            }
            var finished = false;
            var channel = null;
            var timer = null;

            function finish(callback, value) {
                if (finished) return;
                finished = true;
                pendingAuth = null;
                window.removeEventListener('message', onWindowMessage);
                if (channel) channel.close();
                clearTimeout(timer);
                try {
                    popup.close();
                } catch (e) { }
                callback(value);
            }

            function accept(data) {
                if (!data || data.type !== 'lang-cloud-oauth') return;
                if (!data.params || data.params.state !== expectedState)
                    finish(reject, new CloudError('provider', 'The sign-in response didn\'t match the request.'));
                else finish(resolve, data.params);
            }

            function onWindowMessage(event) {
                if (event.origin === location.origin) accept(event.data);
            }

            window.addEventListener('message', onWindowMessage);
            try {
                channel = new BroadcastChannel('lang-cloud-oauth');
                channel.onmessage = function (event) { accept(event.data); };
            } catch (e) { }
            timer = setTimeout(function () {
                finish(reject, new CloudError('cancelled', 'Sign-in timed out.'));
            }, AUTH_TIMEOUT_MS);
            pendingAuth = function () {
                finish(reject, new CloudError('cancelled', 'Sign-in cancelled.'));
            };
        });
    }

    async function connectCore(id) {
        var provider = PROVIDERS[id];
        if (!provider) throw new CloudError('invalid', 'Unknown cloud service.');
        var clientId = clientIdFor(id);
        if (!clientId) throw new CloudError('not-configured', provider.name + ' isn\'t set up in this version of the app.');
        if (!window.crypto || !crypto.subtle) throw new CloudError('provider', 'Sign-in needs a secure (https) page.');
        if (pendingAuth) throw new CloudError('busy', 'A sign-in is already in progress.');

        var options = { clientId: clientId, redirect: redirectUri(), state: randomString(16), verifier: randomString(48) };
        options.challenge = await pkceChallenge(options.verifier);
        var params = await openSignIn(provider.authorizeUrl(options), options.state);
        if (params.error) {
            throw new CloudError(params.error === 'access_denied' ? 'cancelled' : 'provider',
                params.error === 'access_denied' ? 'Sign-in cancelled.' : (params.error_description || params.error));
        }
        var token;
        try {
            token = await provider.exchange(options, params);
        } catch (error) {
            throw providerError(error, provider.name);
        }

        var changedProvider = state.provider !== id;
        state.provider = id;
        state.token = token;
        if (changedProvider) {
            state.lastHash = null;
            state.lastBackupUtc = null;
            state.armed = false;
        }
        try {
            state.account = await provider.account(token.access);
        } catch (error) {
            state.account = state.account || '';
        }
        ensureDevice();
        persist();
    }

    // Returns a usable access token, refreshing it when it is about to run out.
    async function freshToken() {
        var provider = currentProvider();
        var token = state.token;
        var expired = token.expiresAt && token.expiresAt - 60000 < Date.now();
        if (!expired) return token.access;
        return refresh(provider);
    }

    async function refresh(provider) {
        if (!provider.refresh || !state.token.refresh)
            throw new CloudError('auth-expired', 'The sign-in has expired.');
        try {
            state.token = await provider.refresh(state.token);
            persist();
            return state.token.access;
        } catch (error) {
            if (error instanceof HttpError && (error.status === 400 || error.status === 401)) {
                state.token.refresh = null;
                persist();
                throw new CloudError('auth-expired', 'The sign-in has expired.');
            }
            throw providerError(error, provider.name);
        }
    }

    // Runs a call with a valid token; if the provider rejects the token, refreshes once and retries.
    async function withToken(call) {
        var provider = currentProvider();
        try {
            return await call(await freshToken(), provider);
        } catch (error) {
            if (error instanceof HttpError && error.status === 401) {
                var token = await refresh(provider);
                try {
                    return await call(token, provider);
                } catch (retry) {
                    throw providerError(retry, provider.name);
                }
            }
            throw providerError(error, provider.name);
        }
    }

    // ------------------------------------------------------------------ files

    function describeFile(entry) {
        var match = FILE_PATTERN.exec(entry.name || '');
        if (!match) return null;
        return {
            id: entry.id,
            name: entry.name,
            createdUtc: parseTimestamp(match[1]),
            modifiedUtc: entry.modifiedUtc || parseTimestamp(match[1]),
            size: entry.size || 0,
            deviceId: match[2],
            deviceLabel: displayLabel(match[3]),
            thisDevice: match[2] === ensureDevice()
        };
    }

    async function listFiles() {
        var entries = await withToken(function (token, provider) {
            return provider.list(token);
        });
        return entries.map(describeFile).filter(Boolean).sort(function (a, b) {
            return a.createdUtc < b.createdUtc ? 1 : a.createdUtc > b.createdUtc ? -1 : 0;
        });
    }

    // Each device keeps its last few backups. Other devices' files are never touched.
    async function prune() {
        var mine = (await listFiles()).filter(function (file) { return file.thisDevice; });
        var extra = mine.slice(KEEP_PER_DEVICE);
        for (var i = 0; i < extra.length; i++) {
            try {
                await withToken(function (token, provider) {
                    return provider.remove(token, extra[i].id);
                });
            } catch (error) { /* leave it for the next backup */ }
        }
    }

    var busy = false;

    async function backupCore(json, force) {
        currentProvider();
        var hash = toHex(await sha256(stableText(json)));
        if (!force && state.lastHash === hash && state.lastBackupUtc)
            return { uploaded: false, skipped: 'unchanged' };

        var packed = await gzip(json);
        var name = 'langnotebook-' + timestamp(new Date()) + '-' + ensureDevice() + '-' + deviceLabel() +
            (packed.gz ? '.json.gz' : '.json');
        var entry = await withToken(function (token, provider) {
            return provider.upload(token, name, packed.bytes);
        });
        state.lastHash = hash;
        state.lastBackupUtc = new Date().toISOString();
        persist();
        try {
            await prune();
        } catch (error) { /* clean-up is best effort */ }
        return { uploaded: true, file: describeFile(entry) };
    }

    // ------------------------------------------------------------------ public API

    function publicStatus() {
        var provider = PROVIDERS[state.provider];
        var connected = !!(provider && state.token);
        var token = state.token || {};
        var expired = connected && token.expiresAt && token.expiresAt < Date.now();
        return {
            connected: connected,
            provider: connected ? state.provider : '',
            providerName: connected ? provider.name : '',
            account: connected ? (state.account || '') : '',
            autoBackup: state.autoBackup !== false,
            armed: connected && !!state.armed,
            lastBackupUtc: connected ? (state.lastBackupUtc || null) : null,
            needsReconnect: !!(expired && !token.refresh),
            deviceId: ensureDevice(),
            deviceLabel: displayLabel(deviceLabel()),
            hasSnapshot: hasSnapshot()
        };
    }

    function succeed(extra) {
        return JSON.stringify(Object.assign({ ok: true, code: '', message: '', status: publicStatus() }, extra || {}));
    }

    function failure(error) {
        var code = error instanceof CloudError ? error.code : 'provider';
        var message = error && error.message ? error.message : 'Something went wrong.';
        return JSON.stringify({ ok: false, code: code, message: message, status: publicStatus() });
    }

    async function run(work, exclusive) {
        if (exclusive) {
            if (busy) return failure(new CloudError('busy', 'Another backup is already running.'));
            busy = true;
        }
        try {
            return succeed(await work());
        } catch (error) {
            return failure(error);
        } finally {
            if (exclusive) busy = false;
        }
    }

    function hasSnapshot() {
        try {
            return !!localStorage.getItem(SNAPSHOT_KEY);
        } catch (e) {
            return false;
        }
    }

    return {
        providers: function () {
            return Promise.resolve(JSON.stringify({
                ok: true, code: '', message: '', status: publicStatus(),
                providers: Object.keys(PROVIDERS).map(function (id) {
                    return { id: id, name: PROVIDERS[id].name, configured: !!clientIdFor(id) };
                })
            }));
        },

        status: function () {
            return Promise.resolve(succeed());
        },

        connect: function (id) {
            return run(function () {
                return connectCore(id).then(function () { return {}; });
            }, false);
        },

        cancelConnect: function () {
            if (pendingAuth) pendingAuth();
            return Promise.resolve(succeed());
        },

        disconnect: function () {
            return run(async function () {
                var provider = PROVIDERS[state.provider];
                if (provider && state.token && state.token.access) {
                    try {
                        await provider.revoke(state.token.access);
                    } catch (error) { /* the token is dropped below either way */ }
                }
                state.provider = null;
                state.token = null;
                state.account = '';
                state.armed = false;
                state.lastHash = null;
                state.lastBackupUtc = null;
                persist();
                return {};
            }, false);
        },

        list: function () {
            return run(async function () {
                return { files: await listFiles() };
            }, false);
        },

        backup: function (json, force) {
            return run(function () {
                return backupCore(json, !!force);
            }, true);
        },

        download: function (id) {
            return run(async function () {
                var buffer = await withToken(function (token, provider) {
                    return provider.download(token, id);
                });
                return { json: await gunzipIfNeeded(buffer) };
            }, true);
        },

        setAutoBackup: function (enabled) {
            state.autoBackup = !!enabled;
            persist();
            return Promise.resolve(succeed());
        },

        // Auto-backup stays off on a device until its user has chosen between restoring and keeping its own data.
        setArmed: function (armed) {
            state.armed = !!armed;
            persist();
            return Promise.resolve(succeed());
        },

        // A copy of the data taken just before a restore, so the restore can be undone.
        saveSnapshot: function (json) {
            return run(async function () {
                var packed = await gzip(json);
                try {
                    localStorage.setItem(SNAPSHOT_KEY, JSON.stringify({
                        savedUtc: new Date().toISOString(), gz: packed.gz, data: bytesToBase64(packed.bytes)
                    }));
                } catch (error) {
                    throw new CloudError('snapshot', 'There isn\'t room to keep a copy of your current data.');
                }
                return {};
            }, false);
        },

        loadSnapshot: function () {
            return run(async function () {
                var stored = null;
                try {
                    stored = JSON.parse(localStorage.getItem(SNAPSHOT_KEY));
                } catch (error) { /* treated as missing */ }
                if (!stored) throw new CloudError('invalid', 'There is nothing to undo.');
                var bytes = base64ToBytes(stored.data);
                return { json: await gunzipIfNeeded(bytes.buffer) };
            }, false);
        },

        clearSnapshot: function () {
            try {
                localStorage.removeItem(SNAPSHOT_KEY);
            } catch (e) { }
            return Promise.resolve(succeed());
        },

        // Exposed for tests.
        _test: { pkceChallenge: pkceChallenge, stableText: stableText, describeFile: describeFile, gzip: gzip,
            gunzipIfNeeded: gunzipIfNeeded, reset: function () { state = {}; persist(); } }
    };
})();
