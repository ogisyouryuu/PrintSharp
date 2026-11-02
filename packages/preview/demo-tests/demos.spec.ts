import { test, expect } from '@playwright/test';
import { readFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

for (const [name, port] of [['vue', 5199], ['blazor', 5200]] as const) {
  test(`${name}: real upload, two-page preview, zoom, PDF download, and document replacement`, async ({ page }) => {
    const errors: string[] = [];
    page.on('pageerror', error => errors.push(error.message));
    await page.goto(`http://127.0.0.1:${port}`);
    await page.getByRole('button', { name: '使用内置两页示例' }).click();
    await expect(page.locator('canvas')).toBeVisible({ timeout: 30_000 });
    await expect(page.getByTestId('summary')).toContainText('2 页');
    await expect(page.getByTestId('summary')).toContainText('当前第 1 页');
    await page.getByRole('button', { name: '整页', exact: true }).click();
    await expect.poll(() => page.locator('canvas').evaluate(canvas => parseFloat((canvas as HTMLElement).style.height))).toBeLessThan(600);
    await expect(page.locator('canvas')).toBeVisible();
    await page.screenshot({ path: fileURLToPath(new URL(`../../../artifacts/preview-tests/${name}-demo.png`, import.meta.url)), fullPage: true });
    await page.getByRole('button', { name: '下一页', exact: true }).click();
    await expect(page.locator('canvas')).toHaveAttribute('aria-label', 'Page 2 / 2');
    await expect(page.getByTestId('summary')).toContainText('当前第 2 页');
    const before = await page.locator('canvas').evaluate(canvas => parseFloat((canvas as HTMLElement).style.width));
    await page.getByRole('button', { name: '放大', exact: true }).click();
    await expect.poll(() => page.locator('canvas').evaluate(canvas => parseFloat((canvas as HTMLElement).style.width))).toBeGreaterThan(before);
    await page.getByRole('button', { name: '整页', exact: true }).click();
    await expect(page.locator('canvas')).toBeVisible();
    const pdfLink = page.getByRole('link', { name: '下载同一快照 PDF' });
    const previousId = await pdfLink.getAttribute('href');
    const downloading = page.waitForEvent('download');
    await pdfLink.click();
    const download = await downloading;
    const bytes = readFileSync((await download.path())!);
    expect(bytes.subarray(0, 5).toString()).toBe('%PDF-');
    await download.saveAs(fileURLToPath(new URL(`../../../artifacts/preview-tests/${name}-demo.pdf`, import.meta.url)));
    await page.locator('input[type=file]').setInputFiles(fileURLToPath(new URL('../../../demos/PreviewSamples/template.xlsx', import.meta.url)));
    await page.locator('#data').fill('{"tables":[{"name":"T","columns":[{"name":"Value","type":"string"}],"rows":[{"Value":"Updated / 更新 / العربية"}]}]}');
    await page.locator('#data').blur();
    await page.getByRole('button', { name: '上传并预览', exact: true }).click();
    await expect(pdfLink).not.toHaveAttribute('href', previousId!);
    await expect(page.locator('canvas')).toHaveAttribute('aria-label', 'Page 1 / 2');
    await expect(page.locator('canvas')).toBeVisible();
    await expect(page.getByTestId('summary')).toContainText('绘制完成');
    expect(errors).toEqual([]);
  });
}
