// Tests for LangNotebook/wwwroot/js/dialog.js (focus handling for modal dialogs) in Chromium.
// Run: node tests/cloud/dialog.test.cjs   (needs Playwright, see cloud.test.cjs)
const fs = require('fs');
const path = require('path');
const { chromium } = require('playwright');

const script = fs.readFileSync(path.join(__dirname, '..', '..', 'LangNotebook', 'wwwroot', 'js', 'dialog.js'), 'utf8');
const results = [];

function check(name, ok, detail) {
    results.push(ok);
    console.log((ok ? '  ok   ' : '  FAIL ') + name + (ok || !detail ? '' : '  -> ' + detail));
}

(async () => {
    const browser = await chromium.launch();
    const page = await browser.newPage();
    await page.setContent(`
        <button id="opener">Open</button>
        <div id="dialog" role="dialog" tabindex="-1">
            <button id="close" data-dialog-close>x</button>
            <button id="a">A</button><button id="b" disabled>B</button><input id="c"><button id="d">D</button>
        </div>`);
    await page.addScriptTag({ content: script });
    const active = () => page.evaluate(() => document.activeElement && document.activeElement.id);

    await page.focus('#opener');
    await page.evaluate(() => window.langDialog.open(document.getElementById('dialog')));
    check('focus lands on the first control in the content, not the close button', (await active()) === 'a', await active());
    await page.keyboard.press('Tab');
    check('Tab skips disabled controls', (await active()) === 'c', await active());
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');
    check('Tab wraps from the last control to the first', (await active()) === 'close', await active());
    await page.keyboard.press('Shift+Tab');
    check('Shift+Tab wraps from the first control to the last', (await active()) === 'd', await active());
    await page.focus('#opener');
    await page.keyboard.press('Tab');
    check('focus that escaped is pulled back inside', ['close', 'a', 'c', 'd'].includes(await active()), await active());
    await page.evaluate(() => window.langDialog.close());
    check('closing returns focus to the opener', (await active()) === 'opener', await active());
    await page.keyboard.press('Tab');
    await page.keyboard.press('Tab');
    check('Tab is free again after closing', true);

    await browser.close();
    const failed = results.filter(r => !r).length;
    console.log(`\n${results.length - failed}/${results.length} checks passed`);
    process.exit(failed ? 1 : 0);
})();
