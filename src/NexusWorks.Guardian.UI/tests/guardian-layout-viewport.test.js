const test = require("node:test");
const assert = require("node:assert/strict");
const path = require("node:path");
const { pathToFileURL } = require("node:url");
const { chromium } = require("playwright");

const fixturePath = path.resolve(__dirname, "fixtures/guardian-layout-smoke.html");
const fixtureUrl = pathToFileURL(fixturePath).href;

async function captureViewportState(page, height) {
    await page.setViewportSize({ width: 1440, height });
    await page.goto(fixtureUrl);

    return page.evaluate(() => ({
        viewportHeight: window.innerHeight,
        secondaryDisplay: getComputedStyle(document.getElementById("shortcut-secondary-1")).display,
        shortcutsGap: getComputedStyle(document.querySelector(".gw-home-shortcuts")).gap,
        workspaceMinHeight: getComputedStyle(document.getElementById("workspace")).minHeight,
        inspectorMinHeight: getComputedStyle(document.getElementById("inspector")).minHeight,
        toolbarPadding: getComputedStyle(document.getElementById("results-toolbar")).padding,
        primaryFontSize: getComputedStyle(document.getElementById("shortcut-primary-1")).fontSize,
        primaryPaddingLeft: getComputedStyle(document.getElementById("shortcut-primary-1")).paddingLeft,
        primaryPaddingTop: getComputedStyle(document.getElementById("shortcut-primary-1")).paddingTop
    }));
}

test("guardian layout adapts across 960px, 820px, and 720px heights", async () => {
    const browser = await chromium.launch();

    try {
        const page = await browser.newPage();

        const desktopState = await captureViewportState(page, 960);
        assert.equal(desktopState.viewportHeight, 960);
        assert.equal(desktopState.secondaryDisplay, "flex");
        assert.equal(desktopState.shortcutsGap, "8px");
        assert.equal(desktopState.workspaceMinHeight, "512px");
        assert.equal(desktopState.inspectorMinHeight, "384px");
        assert.equal(desktopState.toolbarPadding, "16px 20px");
        assert.equal(desktopState.primaryFontSize, "12px");
        assert.equal(desktopState.primaryPaddingLeft, "10px");
        assert.equal(desktopState.primaryPaddingTop, "4px");

        const compactState = await captureViewportState(page, 820);
        assert.equal(compactState.viewportHeight, 820);
        assert.equal(compactState.secondaryDisplay, "none");
        assert.equal(compactState.shortcutsGap, "6px");
        assert.equal(compactState.workspaceMinHeight, "448px");
        assert.equal(compactState.inspectorMinHeight, "336px");
        assert.equal(compactState.toolbarPadding, "16px 20px");
        assert.equal(compactState.primaryFontSize, "12px");
        assert.equal(compactState.primaryPaddingLeft, "10px");
        assert.equal(compactState.primaryPaddingTop, "4px");

        const lowHeightState = await captureViewportState(page, 720);
        assert.equal(lowHeightState.viewportHeight, 720);
        assert.equal(lowHeightState.secondaryDisplay, "none");
        assert.equal(lowHeightState.shortcutsGap, "4px");
        assert.equal(lowHeightState.workspaceMinHeight, "384px");
        assert.equal(lowHeightState.inspectorMinHeight, "288px");
        assert.equal(lowHeightState.toolbarPadding, "14px 16px");
        assert.equal(lowHeightState.primaryFontSize, "11px");
        assert.equal(lowHeightState.primaryPaddingLeft, "8.8px");
        assert.equal(lowHeightState.primaryPaddingTop, "4px");
    }
    finally {
        await browser.close();
    }
});
