using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace 任务管理器x频谱分析器 {
	/// <summary>
	/// 提供跨进程绘制的Graphics对象
	/// </summary>
	unsafe public sealed class WindowGraphics : DisposableObject {
		private readonly IntPtr hdc;
		private readonly IntPtr memDC;
		private IntPtr hBitmap;
		private readonly DrawingVisual drawingVisual = new DrawingVisual();
		private int width, height;
		private int[] buffer;

		public Window Window { get; }

		public WindowGraphics(Window window) {
			Window = window;
			hdc = WinApi.GetDC(window.Handle);
			memDC = WinApi.CreateCompatibleDC(IntPtr.Zero);
		}

		void TryUpdateGraphics() {
			var winRect = Window.Rect;
			if (width != winRect.Width || height != winRect.Height) {
				width = winRect.Width;
				height = winRect.Height;
				buffer = new int[width * height];
				if (hBitmap != IntPtr.Zero) WinApi.DeleteObject(hBitmap);
				hBitmap = WinApi.CreateBitmap(width, height, 1, 32, IntPtr.Zero);
				if (hBitmap == IntPtr.Zero) throw new Exception();
				WinApi.SelectObject(memDC, hBitmap);
			}
		}

		void Flush() {
			var renderTargetBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
			renderTargetBitmap.Render(drawingVisual);
			renderTargetBitmap.Freeze();
			renderTargetBitmap.CopyPixels(buffer, width * 4, 0);
			var bitmapInfo = new WinApi.BitmapInfo {
				Size = sizeof(WinApi.BitmapInfo),
				Width = width,
				Height = height,
				BitCount = 32,
				Planes = 1,
				Compression = 0,
				XPelsPerMeter = 96,
				YPelsPerMeter = 96,
			};
			fixed (int* p = buffer) {
				WinApi.SetDIBits(memDC, hBitmap, 0, height, (IntPtr)p, bitmapInfo, 0);
			}
			WinApi.BitBlt(hdc, 0, 0, width, height, memDC, 0, 0, CopyPixelOperation.SourceCopy);
		}


		protected override void Dispose(bool disposing) {
			WinApi.DeleteObject(hBitmap);
			WinApi.DeleteDC(memDC);
			WinApi.ReleaseDC(Window.Handle, hdc);
		}

		/// <summary>
		/// 开始绘制，必须调用Dispose方法才能将图像绘制至目标窗口
		/// </summary>
		/// <returns></returns>
		public IGraphics OpenDraw() => new DrawHandler(this);

		class DrawHandler : IGraphics {
			private readonly WindowGraphics windowGraphics;

			public DrawHandler(WindowGraphics windowGraphics) {
				this.windowGraphics = windowGraphics;
				windowGraphics.TryUpdateGraphics();
				DrawingContext = windowGraphics.drawingVisual.RenderOpen();
				var transformGroup = new TransformGroup();
				transformGroup.Children.Add(new ScaleTransform(1, -1));
				transformGroup.Children.Add(new TranslateTransform(0, windowGraphics.height));
				transformGroup.Freeze();
				DrawingContext.PushTransform(transformGroup);
			}

			public DrawingContext DrawingContext { get; }
			public Size Size => new Size(windowGraphics.width, windowGraphics.height);

			public void Dispose() {
				DrawingContext.Pop();
				DrawingContext.Close();
				windowGraphics.Flush();
			}
		}
	}
}
