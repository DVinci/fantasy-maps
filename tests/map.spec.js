// @ts-check
const { test, expect } = require('@playwright/test');

test.describe('Fantasy Map Generator', () => {
  test.beforeEach(async ({ page }) => {
    await page.goto('/');
  });

  test('page loads with correct title', async ({ page }) => {
    await expect(page).toHaveTitle('Generating fantasy maps');
  });

  test('all section headings are present', async ({ page }) => {
    const headings = [
      'Placing random points',
      'Building the mesh',
      'Sculpting the height map',
      'Erosion',
      'Rendering features',
      'Placing cities',
      'Territories & borders',
      'Complete map',
    ];
    for (const text of headings) {
      await expect(page.getByRole('heading', { name: text })).toBeVisible();
    }
  });

  test('Generate random points renders circles into SVG', async ({ page }) => {
    const svg = page.locator('#svg-points');
    await expect(svg).toBeEmpty();

    await page.getByRole('button', { name: 'Generate random points' }).click();

    const circleCount = await svg.locator('circle').count();
    expect(circleCount).toBeGreaterThan(100);
  });

  test('Show Voronoi mesh renders edges', async ({ page }) => {
    await page.getByRole('button', { name: 'Show Voronoi mesh' }).click();

    const svg = page.locator('#svg-voronoi');
    const lineCount = await svg.locator('line').count();
    expect(lineCount).toBeGreaterThan(100);
  });

  test('height map buttons modify the SVG', async ({ page }) => {
    await page.getByRole('button', { name: 'Add random slope' }).click();
    const pathCount = await page.locator('#svg-heightmap path.field').count();
    expect(pathCount).toBeGreaterThan(0);

    await page.getByRole('button', { name: 'Normalize' }).click();
    await page.getByRole('button', { name: 'Set sea level' }).click();
    // coast path should appear after sea level is set
    await expect(page.locator('#svg-heightmap path.coast')).toHaveCount(1);
  });

  test('Generate coastline renders terrain', async ({ page }) => {
    await page.getByRole('button', { name: 'Generate coastline' }).click();

    const fieldPaths = await page.locator('#svg-erosion path.field').count();
    expect(fieldPaths).toBeGreaterThan(1000);

    const coastPaths = await page.locator('#svg-erosion path.coast').count();
    expect(coastPaths).toBeGreaterThanOrEqual(1);
  });

  test('city placement adds circles', async ({ page }) => {
    await page.getByRole('button', { name: 'Add new city' }).click();
    const cities = await page.locator('#svg-cities circle.city').count();
    expect(cities).toBe(1);

    await page.getByRole('button', { name: 'Add new city' }).click();
    expect(await page.locator('#svg-cities circle.city').count()).toBe(2);

    await page.getByRole('button', { name: 'Reset cities' }).click();
    expect(await page.locator('#svg-cities circle.city').count()).toBe(0);
  });

  test('Generate map produces a labelled SVG', async ({ page }) => {
    page.on('dialog', d => d.dismiss());

    await page.getByRole('button', { name: 'Generate map' }).click();

    // Full map generation is expensive — wait up to 60s
    await expect(page.locator('#svg-fullmap text.city').first()).toBeVisible({ timeout: 60000 });

    const cityLabels = await page.locator('#svg-fullmap text.city').count();
    expect(cityLabels).toBeGreaterThan(0);

    const regionLabels = await page.locator('#svg-fullmap text.region').count();
    expect(regionLabels).toBeGreaterThan(0);
  });
});
