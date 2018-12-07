using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace 任务管理器x频谱分析器 {
	[SuppressUnmanagedCodeSecurity]
	unsafe public static class WinApi {
		[DllImport("user32")]
		public static extern int GetSystemMetrics(int index);

		[DllImport("user32")]
		public static extern IntPtr FindWindow(string className, string windowName);

		[DllImport("user32")]
		public static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string className, string windowName);

		[DllImport("user32")]
		public static extern int GetWindowText(IntPtr hwnd, StringBuilder buffer, int bufferSize);

		[DllImport("user32")]
		public static extern int GetClassName(IntPtr hwnd, StringBuilder buffer, int bufferSize);

		[DllImport("user32")]
		extern static bool GetWindowRect(IntPtr hwnd, out WinApiRect rect);

		[DllImport("user32")]
		public extern static bool SetWindowPos(IntPtr hwnd, IntPtr hwndInsertAfter, int x, int y, int w, int h, int flags);

		[DllImport("user32")]
		public extern static bool ShowWindow(IntPtr hwnd, int cmd);

		[DllImport("user32")]
		public extern static bool EnableWindow(IntPtr hwnd, bool enable);

		[DllImport("user32")]
		public extern static IntPtr GetDC(IntPtr hwnd);

		[DllImport("user32")]
		public extern static int ReleaseDC(IntPtr hwnd, IntPtr hdc);

		[DllImport("gdi32")]
		public extern static IntPtr CreateCompatibleDC(IntPtr hdc);

		[DllImport("gdi32")]
		public extern static IntPtr CreateCompatibleBitmap(IntPtr hdc, int width, int height);

		[DllImport("gdi32")]
		public extern static IntPtr CreateBitmap(int width, int height, int planes, int bitsPeral, IntPtr pixels);

		[DllImport("gdi32")]
		public extern static IntPtr SelectObject(IntPtr hdc, IntPtr obj);

		[DllImport("gdi32")]
		public extern static int GetObject(IntPtr hObject, int cb, void* refObject);

		[DllImport("gdi32")]
		public extern static bool DeleteObject(IntPtr hObject);

		[DllImport("gdi32")]
		public extern static bool DeleteDC(IntPtr hdc);

		[DllImport("gdi32")]
		public extern static bool BitBlt(IntPtr hdcDst, int xDst, int yDst, int wDst, int hDst, IntPtr hdcSrc, int xSrc, int ySrc, CopyPixelOperation op);

		[DllImport("gdi32")]
		public extern static int SetDIBits(IntPtr hdc, IntPtr hBitmap, int startScan, int scanLines, IntPtr buffer, in BitmapInfo bitmapInfo, int colorUse);

		[DllImport("user32", EntryPoint = "GetWindowLong")]
		public extern static int GetWindowLongWinApi(IntPtr hwnd, int index);

		[DllImport("user32")]
		public extern static long GetWindowLongPtr(IntPtr hwnd, int index);

		[DllImport("user32", EntryPoint = "SetWindowLong")]
		public extern static int SetWindowLongWinApi(IntPtr hwnd, int index, int value);

		[DllImport("user32")]
		public extern static long SetWindowLongPtr(IntPtr hwnd, int index, long value);

		[StructLayout(LayoutKind.Sequential)]
		public struct WinApiRect {
			public int Left;
			public int Top;
			public int Right;
			public int Bottom;
		}

		[StructLayout(LayoutKind.Sequential)]
		public struct BitmapInfo {
			public int Size;
			public int Width;
			public int Height;
			public short Planes;
			public short BitCount;
			public int Compression;
			public int SizeImage;
			public int XPelsPerMeter;
			public int YPelsPerMeter;
			public int ClrUsed;
			public int ClrImportant;
			public fixed uint Colors[1];
		}

		const int SM_CXSCREEN = 0;
		const int SM_CYSCREEN = 0;

		public static int GetScreenWidth() => GetSystemMetrics(SM_CXSCREEN);

		public static int GetScreenHeight() => GetSystemMetrics(SM_CYSCREEN);

		public static string GetClassName(IntPtr hwnd) {
			var buffer = new StringBuilder(255);
			GetClassName(hwnd, buffer, 255);
			return buffer.ToString();
		}

		public static string GetWindowText(IntPtr hwnd) {
			var buffer = new StringBuilder(255);
			GetWindowText(hwnd, buffer, 255);
			return buffer.ToString();
		}

		public static Int32Rect GetWindowRectangle(IntPtr hwnd) {
			GetWindowRect(hwnd, out var rect);
			return new Int32Rect(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top);
		}

		public const int GWL_EXSTYLE = -20;
		public const int GWL_STYLE = -16;

		public const int WS_VISIBLE = 0x10000000;

		public static long GetWindowLong(IntPtr hwnd, int index) {
			if (IntPtr.Size == 4) {
				return GetWindowLongWinApi(hwnd, index);
			} else {
				return GetWindowLongPtr(hwnd, index);
			}
		}

		public static void SetWindowLong(IntPtr hwnd, int index, long exStyle) {
			if (IntPtr.Size == 4) {
				SetWindowLongWinApi(hwnd, index, (int)exStyle);
			} else {
				SetWindowLongPtr(hwnd, index, exStyle);
			}
		}

		public static long GetWindowExStyle(IntPtr hwnd)
			=> GetWindowLong(hwnd, GWL_EXSTYLE);

		public static void SetWindowExStyle(IntPtr hwnd, long exStyle)
			=> SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);

		public static long GetWindowStyle(IntPtr hwnd)
			=> GetWindowLong(hwnd, GWL_STYLE);

		public static void SetWindowStyle(IntPtr hwnd, long exStyle)
			=> SetWindowLong(hwnd, GWL_STYLE, exStyle);
	}
}
