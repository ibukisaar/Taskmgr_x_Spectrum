using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace 任务管理器x频谱分析器 {

	public sealed class Window {
		public IntPtr Handle { get; }
		public IReadOnlyList<Window> Childs { get; }
		public string Title => WinApi.GetWindowText(Handle);
		public string ClassName => WinApi.GetClassName(Handle);
		public Int32Rect Rect => WinApi.GetWindowRectangle(Handle);
		public bool Visible => (WinApi.GetWindowStyle(Handle) & WinApi.WS_VISIBLE) != 0;

		public Window(IntPtr hwnd, bool visibleOnly = true) {
			Handle = hwnd;
			var childs = new List<Window>();
			IntPtr child = WinApi.FindWindowEx(hwnd, IntPtr.Zero, null, null);
			while (child != IntPtr.Zero) {
				if (visibleOnly) {
					var childRect = WinApi.GetWindowRectangle(child);
					if (childRect.Width <= 0 || childRect.Height <= 0) goto Next;
					
					var style = WinApi.GetWindowStyle(hwnd);
					if ((style & WinApi.WS_VISIBLE) == 0) goto Next;
				}
				childs.Add(new Window(child, visibleOnly));

				Next:
				child = WinApi.FindWindowEx(hwnd, child, null, null);
			}
			Childs = childs.ToArray();
		}

		public override string ToString() => $"{ClassName}: {Title}";
	}
}
