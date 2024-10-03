using System;
using System.Drawing;
using System.Runtime.InteropServices;

namespace ClassIsland
{
    public static class WindowCaptureHelper
    {
        public const uint SRCCOPY = 0x00CC0020;
        public const int PW_CLIENTONLY = 1; // 仅捕获客户区

        [DllImport("user32.dll")]
        private static extern IntPtr GetWindowRect(IntPtr hWnd, ref RECT rect);

        [DllImport("user32.dll", EntryPoint = "GetWindowDC")]
        public static extern IntPtr GetWindowDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        public static extern bool PrintWindow(IntPtr hwnd, IntPtr hdcBlt, int nFlags);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

        [DllImport("gdi32.dll")]
        private static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

        [DllImport("gdi32.dll")]
        private static extern int DeleteDC(IntPtr hdc);

        [DllImport("user32.dll")]
        public static extern IntPtr ReleaseDC(IntPtr hwnd, IntPtr hdc);

        [DllImport("gdi32.dll")]
        public static extern bool DeleteObject(IntPtr hObject);

        [StructLayout(LayoutKind.Sequential)]
        public struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        public static Bitmap CaptureWindow(IntPtr hWnd)
        {
            RECT windowRect = new RECT();
            GetWindowRect(hWnd, ref windowRect);

            int width = windowRect.Right - windowRect.Left;
            int height = windowRect.Bottom - windowRect.Top;

            IntPtr hDCMem = CreateCompatibleDC(IntPtr.Zero);
            IntPtr hBitmap = CreateCompatibleBitmap(GetWindowDC(hWnd), width, height);
            IntPtr hOldBitmap = SelectObject(hDCMem, hBitmap);

            // 使用PrintWindow捕获窗口的客户区
            PrintWindow(hWnd, hDCMem, PW_CLIENTONLY);

            Bitmap bitmap = Image.FromHbitmap(hBitmap);
            SelectObject(hDCMem, hOldBitmap);
            DeleteObject(hBitmap);
            DeleteDC(hDCMem);
            ReleaseDC(hWnd, GetWindowDC(hWnd));

            return bitmap;
        }
    }
}