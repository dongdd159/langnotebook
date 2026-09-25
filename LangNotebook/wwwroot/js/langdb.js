window.langDb = (function () {
    const DB_NAME = 'lang-notebook';
    const STORE_NAME = 'files';
    const KEY = 'app.db';
    const DB_VERSION = 1;
    let dbPromise = null;

    function openDatabase() {
        if (dbPromise)
            return dbPromise;
        dbPromise = new Promise(function (resolve, reject) {
            const request = indexedDB.open(DB_NAME, DB_VERSION);
            request.onupgradeneeded = function () {
                const db = request.result;
                if (!db.objectStoreNames.contains(STORE_NAME))
                    db.createObjectStore(STORE_NAME);
            };
            request.onsuccess = function () {
                resolve(request.result);
            };
            request.onerror = function () {
                reject(request.error);
            };
        });
        return dbPromise;
    }

    function withStore(mode, action) {
        return openDatabase().then(function (db) {
            return new Promise(function (resolve, reject) {
                const tx = db.transaction(STORE_NAME, mode);
                const store = tx.objectStore(STORE_NAME);
                const request = action(store);
                tx.oncomplete = function () {
                    resolve(request ? request.result : undefined);
                };
                tx.onerror = function () {
                    reject(tx.error);
                };
                tx.onabort = function () {
                    reject(tx.error);
                };
            });
        });
    }

    return {
        load: function () {
            return withStore('readonly', function (store) {
                return store.get(KEY);
            }).then(function (value) {
                return value || null;
            }).catch(function (error) {
                console.warn('[langDb] Error reading IndexedDB:', error);
                return null;
            });
        },

        save: function (base64) {
            return withStore('readwrite', function (store) {
                return store.put(base64, KEY);
            }).catch(function (error) {
                console.error('[langDb] Error writing IndexedDB:', error);
            });
        },

        clear: function () {
            return withStore('readwrite', function (store) {
                return store.delete(KEY);
            }).catch(function (error) {
                console.error('[langDb] Error deleting IndexedDB:', error);
            });
        },

        download: function (fileName, contentType, bytes) {
            try {
                const blob = new Blob([bytes], {type: contentType || 'application/octet-stream'});
                const url = URL.createObjectURL(blob);
                const link = document.createElement('a');
                link.href = url;
                link.download = fileName;
                document.body.appendChild(link);
                link.click();
                document.body.removeChild(link);
                setTimeout(function () {
                    URL.revokeObjectURL(url);
                }, 2000);
            } catch (error) {
                console.error('[langDb] Khong tao duoc file tai ve:', error);
            }
        }
    };
})();
