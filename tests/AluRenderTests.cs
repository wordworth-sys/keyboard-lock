// AluPanelRenderer 离屏绘制与几何回归测试（纯内存 GDI+，不建窗、不装钩子）。运行：.\tests\run-tests.ps1
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using KeyboardLock.Alu;

internal static class AluRenderTests
{
    private static int failures;

    private static void Main()
    {
        var tests = new List<KeyValuePair<string, Action>>
        {
            Case("LED 灯珠严格限制在 12×12 内，居中露出 3px 金属灯座圈与底沿高光", TestLedBezelAndLensGeometry),
            Case("AluText.Measure 正确计入空格宽度（标题栏与英文标签不粘连）", TestTextSpaceMeasurement),
            Case("局部重绘（窗口控制键悬停 / 主操作键按下）与全量重绘逐像素零差异", TestPartialRepaintConsistency),
            Case("150% 高 DPI 缩放绘制与命中测试在三态下均正常", TestHighDpiAndHitTesting),
        };

        foreach (var t in tests)
        {
            int before = failures;
            t.Value();
            Console.WriteLine((failures == before ? "  ok   " : "  FAIL ") + t.Key);
        }

        Console.WriteLine(failures == 0
            ? "\nAll " + tests.Count + " render tests passed."
            : "\n" + failures + " render assertion(s) failed.");
        Environment.Exit(failures == 0 ? 0 : 1);
    }

    private static void TestLedBezelAndLensGeometry()
    {
        Size size = new Size(400, 264);
        using (Bitmap unlocked = RenderFull(size, AluPanelState.Unlocked, false, false, false))
        using (Bitmap locked = RenderFull(size, AluPanelState.Locked, false, false, false))
        using (Bitmap unavail = RenderFull(size, AluPanelState.Unavailable, false, false, false))
        {
            // 圆心 (226, 94) 应呈现各状态对应的鲜明灯珠主色
            Color cGreen = unlocked.GetPixel(226, 94);
            Color cRed = locked.GetPixel(226, 94);
            Color cAmber = unavail.GetPixel(226, 94);
            Check(cGreen.G > cGreen.R + 80 && cGreen.G > cGreen.B + 30, "Unlocked LED center should be green, got " + Fmt(cGreen));
            Check(cRed.R > cRed.G + 80 && cRed.R > cRed.B + 80, "Locked LED center should be red/orange, got " + Fmt(cRed));
            Check(cAmber.R > cAmber.B + 80 && cAmber.G > cAmber.B + 40, "Unavailable LED center should be amber, got " + Fmt(cAmber));

            // 18×18 灯座外侧 (x=213, 239) 必须是深色状态窗玻璃底色（修复前 24×24 灯珠会直接溢出到这里）
            Color outsideL = unlocked.GetPixel(213, 94);
            Color outsideR = unlocked.GetPixel(239, 94);
            Check(outsideL.G < 60 && outsideR.G < 60, "Pixels outside 18x18 bezel must not be covered by LED lens: L=" + Fmt(outsideL) + " R=" + Fmt(outsideR));

            // 3px 金属灯座环左右对称点 (218, 94) 与 (233, 94)：不应被不透明灯珠覆盖，且亮度左右对称
            Color bezelL = unlocked.GetPixel(218, 94);
            Color bezelR = unlocked.GetPixel(233, 94);
            Check(bezelL.G < 110 && bezelR.G < 110, "3px metallic bezel ring should remain visible (not overwritten by opaque lens): L=" + Fmt(bezelL) + " R=" + Fmt(bezelR));
            Check(Math.Abs(bezelL.G - bezelR.G) <= 8 && Math.Abs(bezelL.R - bezelR.R) <= 8,
                "Centered LedLens (220,88) must produce symmetric left/right bezel ring: L=" + Fmt(bezelL) + " R=" + Fmt(bezelR));

            // 灯座下沿 +1px 的白色边缘高光 (226, 103) 应明显亮于下方状态窗背景 (226, 107)
            Color bottomRim = unlocked.GetPixel(226, 103);
            Color statusBg = unlocked.GetPixel(226, 107);
            Check(bottomRim.R > statusBg.R + 18, "LED bezel bottom rim highlight at (226,103) should be visible: rim=" + Fmt(bottomRim) + " bg=" + Fmt(statusBg));
        }
    }

    private static void TestTextSpaceMeasurement()
    {
        using (Bitmap bmp = new Bitmap(64, 32, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        using (AluFonts fonts = new AluFonts(1f))
        {
            float compact = AluText.MeasureWidth(g, "HOOKFAILED", fonts.StatusEn, 1.33f);
            float spaced = AluText.MeasureWidth(g, "HOOK FAILED", fonts.StatusEn, 1.33f);
            float singleSpaceGlyph = AluText.MeasureWidth(g, " ", fonts.StatusEn, 1.33f);
            float singleSpaceWhole = AluText.MeasureWidth(g, " ", fonts.Title, 0f);
            Check(spaced > compact + 1.33f + 1.5f, "Space inside 'HOOK FAILED' (letter-spaced path) must contribute positive glyph width");
            Check(singleSpaceGlyph > 1.5f, "Single space glyph measurement must be non-zero, got " + singleSpaceGlyph);
            Check(singleSpaceWhole > 1.5f, "Single space whole-string measurement must be non-zero, got " + singleSpaceWhole);
        }
    }

    private static void TestPartialRepaintConsistency()
    {
        Size size = new Size(400, 264);

        // 1) 窗口控制键悬停局部重绘 vs 全量重绘
        using (AluPanelRenderer r = new AluPanelRenderer())
        using (Bitmap partial = RenderWithRenderer(r, size))
        {
            r.HoverClose = true;
            Rectangle dirty = r.WindowControlsBounds;
            RedrawClip(r, partial, size, dirty);

            using (Bitmap full = RenderFull(size, AluPanelState.Unlocked, false, true, false))
            {
                int diff = CountDiffPixels(partial, full);
                Check(diff == 0, "WindowControlsBounds partial repaint differed from full repaint by " + diff + " pixel(s)");
            }
        }

        // 2) 主操作键按下局部重绘 vs 全量重绘（锁定态）
        using (AluPanelRenderer r = new AluPanelRenderer())
        {
            r.State = AluPanelState.Locked;
            using (Bitmap partial = RenderWithRenderer(r, size))
            {
                r.PressMainButton = true;
                Rectangle dirty = r.MainButtonBounds;
                RedrawClip(r, partial, size, dirty);

                using (Bitmap full = RenderFull(size, AluPanelState.Locked, false, false, true))
                {
                    int diff = CountDiffPixels(partial, full);
                    Check(diff == 0, "MainButtonBounds partial repaint differed from full repaint by " + diff + " pixel(s)");
                }
            }
        }
    }

    private static void TestHighDpiAndHitTesting()
    {
        Size hiDpi = new Size(600, 396);
        using (AluPanelRenderer r = new AluPanelRenderer())
        using (Bitmap bmp = new Bitmap(hiDpi.Width, hiDpi.Height, PixelFormat.Format32bppArgb))
        using (Graphics g = Graphics.FromImage(bmp))
        {
            Rectangle all = new Rectangle(Point.Empty, hiDpi);
            r.State = AluPanelState.Unlocked;
            r.Draw(g, hiDpi, all);
            Check(Math.Abs(r.Metrics.Scale - 1.5f) < 0.001f, "Expected 1.5x scale at 600x396, got " + r.Metrics.Scale);
            Check(r.HitTest(new Point(300, 270)) == AluHitTarget.MainButton, "Expected MainButton hit at 150% DPI");
            Check(r.IsInteractive(AluHitTarget.MainButton), "MainButton must be interactive when Unlocked");

            r.State = AluPanelState.Locked;
            r.Draw(g, hiDpi, all);

            r.State = AluPanelState.Unavailable;
            r.Draw(g, hiDpi, all);
            Check(!r.IsInteractive(AluHitTarget.MainButton), "MainButton must be disabled when Unavailable");
            Check(!r.IsInteractive(AluHitTarget.ToggleModule), "ToggleModule must be disabled when Unavailable");
            Check(r.IsInteractive(AluHitTarget.CloseToTray), "CloseToTray must stay interactive when Unavailable");
        }
    }

    private static Bitmap RenderFull(Size size, AluPanelState state, bool hoverMin, bool hoverClose, bool pressMain)
    {
        using (AluPanelRenderer r = new AluPanelRenderer())
        {
            r.State = state;
            r.HoverMinimize = hoverMin;
            r.HoverClose = hoverClose;
            r.PressMainButton = pressMain;
            return RenderWithRenderer(r, size);
        }
    }

    private static Bitmap RenderWithRenderer(AluPanelRenderer r, Size size)
    {
        Bitmap bmp = new Bitmap(size.Width, size.Height, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.Clear(AluPalette.ChassisMid);
            r.Draw(g, size, new Rectangle(Point.Empty, size));
        }
        return bmp;
    }

    private static void RedrawClip(AluPanelRenderer r, Bitmap target, Size size, Rectangle clip)
    {
        using (Graphics g = Graphics.FromImage(target))
        {
            g.SetClip(clip);
            using (SolidBrush bg = new SolidBrush(AluPalette.ChassisMid))
                g.FillRectangle(bg, clip);
            r.Draw(g, size, clip);
        }
    }

    private static int CountDiffPixels(Bitmap a, Bitmap b)
    {
        int diff = 0;
        for (int y = 0; y < a.Height; y++)
        {
            for (int x = 0; x < a.Width; x++)
            {
                if (a.GetPixel(x, y).ToArgb() != b.GetPixel(x, y).ToArgb())
                    diff++;
            }
        }
        return diff;
    }

    private static void Check(bool ok, string msg)
    {
        if (!ok)
        {
            failures++;
            Console.WriteLine("  FAIL: " + msg);
        }
    }

    private static string Fmt(Color c)
    {
        return "rgba(" + c.R + "," + c.G + "," + c.B + "," + c.A + ")";
    }

    private static KeyValuePair<string, Action> Case(string name, Action body)
    {
        return new KeyValuePair<string, Action>(name, body);
    }
}
