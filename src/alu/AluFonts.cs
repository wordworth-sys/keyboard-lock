using System;
using System.Drawing;

namespace KeyboardLock.Alu
{
    /// <summary>
    /// 面板字体集：把 CSS 里的像素字号换算成 GDI+ 字号。
    ///
    /// 字号用 GraphicsUnit.Pixel 而不是 Point：Point 会被系统 DPI 再缩放一次，
    /// 与布局缩放叠加后文字会偏大。像素单位则完全由布局缩放决定，
    /// 界面在任何 DPI 下都与设计稿等比。
    /// </summary>
    internal sealed class AluFonts : IDisposable
    {
        private const string UiFamily = "Microsoft YaHei UI";
        private const string MonoFamily = "Consolas";

        public readonly Font Title;        // 13px 粗体
        public readonly Font ModelTag;     // 10px 粗体等宽
        public readonly Font StatusCn;     // 15px 粗体
        public readonly Font StatusEn;     // 9.5px 粗体等宽
        public readonly Font RailLabel;    // 9px 粗体等宽
        public readonly Font ThumbLabel;   // 11px 粗体等宽
        public readonly Font MainLabel;    // 16px 粗体
        public readonly Font KeyLabel;     // 11px 粗体等宽
        public readonly Font NoteLabel;    // 11px 粗体

        /// <summary>按当前缩放比例创建字体集。</summary>
        public AluFonts(float scale)
        {
            if (scale <= 0f)
                scale = 1f;

            Title = Make(UiFamily, 13f, FontStyle.Bold, scale);
            ModelTag = Make(MonoFamily, 10f, FontStyle.Bold, scale);
            StatusCn = Make(UiFamily, 15f, FontStyle.Bold, scale);
            StatusEn = Make(MonoFamily, 9.5f, FontStyle.Bold, scale);
            RailLabel = Make(MonoFamily, 9f, FontStyle.Bold, scale);
            ThumbLabel = Make(MonoFamily, 11f, FontStyle.Bold, scale);
            MainLabel = Make(UiFamily, 16f, FontStyle.Bold, scale);
            KeyLabel = Make(MonoFamily, 11f, FontStyle.Bold, scale);
            NoteLabel = Make(UiFamily, 11f, FontStyle.Bold, scale);
        }

        private static Font Make(string family, float cssPixels, FontStyle style, float scale)
        {
            return new Font(family, cssPixels * scale, style, GraphicsUnit.Pixel);
        }

        public void Dispose()
        {
            DisposeFont(Title);
            DisposeFont(ModelTag);
            DisposeFont(StatusCn);
            DisposeFont(StatusEn);
            DisposeFont(RailLabel);
            DisposeFont(ThumbLabel);
            DisposeFont(MainLabel);
            DisposeFont(KeyLabel);
            DisposeFont(NoteLabel);
        }

        private static void DisposeFont(Font font)
        {
            if (font != null)
                font.Dispose();
        }
    }
}
