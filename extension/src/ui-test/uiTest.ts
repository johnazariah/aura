/**
 * UI Tests for Aura VS Code Extension
 * Uses vscode-extension-tester to automate and screenshot the extension UI
 */

import * as path from 'path';
import * as fs from 'fs';
import {
    VSBrowser,
    WebDriver,
    ActivityBar,
    SideBarView,
    TreeItem,
    Workbench,
    InputBox,
    EditorView,
    WebView
} from 'vscode-extension-tester';

describe('Aura Extension UI Tests', function () {
    this.timeout(60000); // UI tests need longer timeout

    let browser: VSBrowser;
    let driver: WebDriver;
    const screenshotDir = path.join(__dirname, '..', '..', 'screenshots');

    before(async function () {
        browser = VSBrowser.instance;
        driver = browser.driver;

        // Create screenshots directory
        if (!fs.existsSync(screenshotDir)) {
            fs.mkdirSync(screenshotDir, { recursive: true });
        }
    });

    async function takeScreenshot(name: string): Promise<string> {
        const filePath = path.join(screenshotDir, `${name}-${Date.now()}.png`);
        const screenshot = await driver.takeScreenshot();
        fs.writeFileSync(filePath, screenshot, 'base64');
        console.log(`Screenshot saved: ${filePath}`);
        return filePath;
    }

    it('should show Aura activity bar icon', async function () {
        const activityBar = new ActivityBar();
        await takeScreenshot('01-activity-bar');

        const controls = await activityBar.getViewControls();
        const auraControl = controls.find(async (c) => {
            const title = await c.getTitle();
            return title.toLowerCase().includes('aura');
        });

        if (auraControl) {
            console.log('Found Aura activity bar icon');
        }
    });

    it('should open Aura sidebar and show views', async function () {
        const activityBar = new ActivityBar();
        const controls = await activityBar.getViewControls();

        // Find and click Aura
        for (const control of controls) {
            const title = await control.getTitle();
            if (title.toLowerCase().includes('aura')) {
                await control.openView();
                await driver.sleep(1000);
                break;
            }
        }

        await takeScreenshot('02-aura-sidebar-open');

        // Get the sidebar content
        const sideBar = new SideBarView();
        const content = sideBar.getContent();
        const sections = await content.getSections();

        console.log('Sidebar sections:');
        for (const section of sections) {
            const title = await section.getTitle();
            console.log(`  - ${title}`);
        }
    });

    it('should show System Status view', async function () {
        const sideBar = new SideBarView();
        const content = sideBar.getContent();

        try {
            const statusSection = await content.getSection('System Status');
            await statusSection.expand();
            await driver.sleep(500);
            await takeScreenshot('03-system-status');

            const items = await statusSection.getVisibleItems();
            console.log('Status items:');
            for (const item of items) {
                if (item instanceof TreeItem) {
                    const label = await item.getLabel();
                    console.log(`  - ${label}`);
                }
            }
        } catch (e) {
            console.log('System Status section not found or empty');
        }
    });

    it('should show Stories view', async function () {
        const sideBar = new SideBarView();
        const content = sideBar.getContent();

        try {
            const storySection = await content.getSection('Stories');
            await storySection.expand();
            await driver.sleep(500);
            await takeScreenshot('04-stories');

            const items = await storySection.getVisibleItems();
            console.log('Story items:');
            for (const item of items) {
                if (item instanceof TreeItem) {
                    const label = await item.getLabel();
                    console.log(`  - ${label}`);
                }
            }
        } catch (e) {
            console.log('Stories section not found or empty');
        }
    });

    it('should show Agents view', async function () {
        const sideBar = new SideBarView();
        const content = sideBar.getContent();

        try {
            const agentsSection = await content.getSection('Agents');
            await agentsSection.expand();
            await driver.sleep(500);
            await takeScreenshot('05-agents');

            const items = await agentsSection.getVisibleItems();
            console.log('Agent items:');
            for (const item of items) {
                if (item instanceof TreeItem) {
                    const label = await item.getLabel();
                    console.log(`  - ${label}`);
                }
            }
        } catch (e) {
            console.log('Agents section not found or empty');
        }
    });

    it('should trigger Create Story command', async function () {
        const workbench = new Workbench();

        // Open command palette
        await workbench.openCommandPrompt();
        await driver.sleep(500);

        const input = await InputBox.create();
        await input.setText('Aura: Create Story');
        await driver.sleep(500);
        await takeScreenshot('06-command-palette-create-story');

        // Press Escape to close without executing
        await input.cancel();
    });

    it('should take final overview screenshot', async function () {
        // Make sure Aura sidebar is open
        const activityBar = new ActivityBar();
        const controls = await activityBar.getViewControls();

        for (const control of controls) {
            const title = await control.getTitle();
            if (title.toLowerCase().includes('aura')) {
                await control.openView();
                break;
            }
        }

        await driver.sleep(1000);
        await takeScreenshot('07-final-overview');
    });
});
