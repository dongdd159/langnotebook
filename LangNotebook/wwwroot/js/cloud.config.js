// Client IDs for the cloud providers. A client ID is public (it identifies the app, it is not a secret), so it is
// safe to ship in this file. Leave a provider's value empty to hide its connect button as "not set up".
// How to get one, and which redirect URI to register: see docs/cloud-backup.md.
window.langCloudConfig = {
    dropbox: { clientId: '' },
    google: { clientId: '' }
};
