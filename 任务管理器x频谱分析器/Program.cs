using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;

namespace 任务管理器x频谱分析器 {
	unsafe class Program {
		static readonly object fftLock = new object();
		const int FFTSize = 4096;
		static readonly int FFTComplexCount = FFTTools.GetComplexCount(FFTSize);
		const double MinimumFrequency = 50;
		const double MaximumFrequency = 18000;
		const double MaxDB = 75;

		static readonly Brush externalBorderBrush, internalBorderBrush;
		static readonly Pen externalBorderPen, internalBorderPen;

		static Program() {
			externalBorderBrush = new SolidColorBrush(Color.FromRgb(17, 125, 187));
			internalBorderBrush = new SolidColorBrush(Color.FromRgb(217, 234, 244));
			externalBorderPen = new Pen(externalBorderBrush, 1);
			internalBorderPen = new Pen(internalBorderBrush, 1);
			externalBorderBrush.Freeze();
			internalBorderBrush.Freeze();
			externalBorderPen.Freeze();
			internalBorderPen.Freeze();
		}

		/// <summary>
		/// 绘制方格
		/// </summary>
		/// <param name="drawCtx"></param>
		static void DrawBorder(IGraphics drawCtx) {
			double w = drawCtx.Size.Width, h = drawCtx.Size.Height;
			int subW = 35;
			double subH = h / 10;

			for (int x = subW; x < w; x += subW) {
				var drawX = Math.Round((double)x) + 0.5;
				drawCtx.DrawingContext.DrawLine(internalBorderPen, new Point(drawX, 0), new Point(drawX, h));
			}

			for (int i = 1; i < 10; i++) {
				double y = Math.Round(i * subH) + 0.5;
				drawCtx.DrawingContext.DrawLine(internalBorderPen, new Point(0, y), new Point(w, y));
			}

			drawCtx.DrawingContext.DrawRectangle(null, externalBorderPen, new Rect(0.5, 0.5, w - 1, h - 1));
		}

		/// <summary>
		/// 绘制波形
		/// </summary>
		/// <param name="win"></param>
		/// <param name="waveQueueData"></param>
		/// <param name="polylineStyle"></param>
		/// <param name="id">区分线程，调试用</param>
		static void DrawWave(Window win, FixedQueueArray<float> waveQueueData, bool polylineStyle, int id = 0) {
			var lineColor = Color.FromRgb(17, 125, 187);
			var lineBrush = new SolidColorBrush(lineColor);
			lineBrush.Freeze();
			var pen = new Pen(lineBrush, 1);
			pen.Freeze();

			// WindowGraphics 这是一个可以跨进程绘制窗体的类
			// 使用 WPF 中的 DrawingContext 进行绘制
			var wg = new WindowGraphics(win);
			while (true) {
				Task.Delay(20).Wait();

				using (var drawCtx = wg.OpenDraw()) {
					Span<float> waveData = new float[(int)drawCtx.Size.Width];
					waveQueueData.Read(waveData);

					drawCtx.DrawingContext.DrawRectangle(Brushes.White, null, new Rect(drawCtx.Size));
					DrawBorder(drawCtx);

					var h2 = drawCtx.Size.Height / 2;
					Point[] points;
					if (polylineStyle) {
						const int step = 10;
						points = new Point[waveData.Length / step + 1];
						for (int i = 0; i < points.Length; i++) {
							double sum = 0;
							int count = 0;
							for (int j = 0; j < step; j++) {
								if (i * step + j >= waveData.Length) break;
								sum += waveData[i * step + j];
								count++;
							}
							var avg = count > 0 ? sum / count : 0;
							points[i] = new Point(i * step, h2 + avg * h2);

						}
						points[points.Length - 1].X = drawCtx.Size.Width;
					} else {
						points = new Point[waveData.Length];
						for (int i = 0; i < waveData.Length; i++) {
							double y = waveData[i] * h2;
							points[i] = new Point(i, h2 + y);
						}
					}

					var geometry = new StreamGeometry();
					using (var geometryCtx = geometry.Open()) {
						geometryCtx.BeginFigure(points[0], false, false);
						geometryCtx.PolyLineTo(points, true, true);
					}
					geometry.Freeze();
					drawCtx.DrawingContext.DrawGeometry(null, pen, geometry);
				}
			}
		}

		/// <summary>
		/// 绘制频谱（FFT算法用的是FFTW库）
		/// </summary>
		/// <param name="win"></param>
		/// <param name="nextData"></param>
		/// <param name="sampleRate"></param>
		/// <param name="polylineStyle"></param>
		/// <param name="id">区分线程，调试用</param>
		static void DrawSpectrum(Window win, float[] nextData, int sampleRate, bool polylineStyle, int id = 0) {
			double* inBuff = (double*)fftw.alloc_complex((IntPtr)FFTSize);
			double* outBuff = (double*)fftw.alloc_complex((IntPtr)FFTSize);
			IntPtr fft = fftw.dft_1d(FFTSize, (IntPtr)inBuff, (IntPtr)outBuff, fftw_direction.Forward, fftw_flags.Estimate);
			Span<double> tempBuff = stackalloc double[FFTSize];
			var cutLength = FFTTools.CutFrequencyLength(FFTSize, MinimumFrequency, MaximumFrequency, sampleRate, FFTComplexCount);
			Span<double> cutData = tempBuff.Slice(0, cutLength);
			DSP.Window fftWindow = new DSP.BlackmanNuttallWindow(FFTSize);
			ILogarithm log = new DSP.Decade();
			var lineBrush = new SolidColorBrush(Color.FromRgb(17, 125, 187));
			var pen = new Pen(lineBrush, 1);
			var backBrush = new SolidColorBrush(Color.FromRgb(241, 246, 250));
			lineBrush.Freeze();
			pen.Freeze();
			backBrush.Freeze();
			Point[] drawPoints = null;

			Span<double> logData = null;
			var wg = new WindowGraphics(win);
			while (true) {
				Task.Delay(20).Wait();

				using (var drawCtx = wg.OpenDraw()) {
					lock (fftLock) {
						for (int i = 0; i < nextData.Length; i++) {
							tempBuff[i] = nextData[i];
						}
					}
					// 加窗
					fftWindow?.Apply(tempBuff, tempBuff);
					for (int i = 0; i < tempBuff.Length; i++) {
						inBuff[2 * i + 0] = tempBuff[i];
						inBuff[2 * i + 1] = 0;
					}
					// 执行FFT
					fftw.execute(fft);

					if (logData == null || logData.Length != (int)drawCtx.Size.Width) {
						logData = new double[(int)drawCtx.Size.Width];
					}
					double h = drawCtx.Size.Height;
					// FFT复数结果取模
					FFTTools.Abs(FFTSize, new ReadOnlySpan<Complex>(outBuff, FFTSize), tempBuff, true, fftWindow);
					// 保留感兴趣的频率区域
					FFTTools.CutFrequency(FFTSize, MinimumFrequency, MaximumFrequency, sampleRate, tempBuff, cutData);
					// 频率以对数方式显示
					FFTTools.Logarithm(cutData, MinimumFrequency, MaximumFrequency, logData, log);
					// 振幅以分贝方式显示
					FFTTools.ToDB(logData, MaxDB);
					// 因为上面的结果在区间[0,1]，所以乘以窗体高度h，才能画满整个窗体
					FFTTools.Scale(logData, h);

					if (polylineStyle) {
						const int step = 10;
						var points = new Point[logData.Length / step + 1];
						for (int i = 0; i < points.Length; i++) {
							double max = 0;
							for (int j = 0; j < step; j++) {
								if (i * step + j >= logData.Length) break;
								max = Math.Max(logData[i * step + j], max);
							}
							points[i] = new Point(i * step, h - max);
						}
						points[points.Length - 1].X = drawCtx.Size.Width;

						drawPoints = new Point[points.Length + 2];
						points.AsSpan().CopyTo(drawPoints);
					} else {
						drawPoints = new Point[logData.Length + 2];
						for (int i = 0; i < logData.Length; i++) {
							drawPoints[i] = new Point(i, h - logData[i]);
						}
					}

					drawPoints[drawPoints.Length - 2] = new Point(drawCtx.Size.Width, drawCtx.Size.Height);
					drawPoints[drawPoints.Length - 1] = new Point(0, drawCtx.Size.Height);

					var geometry = new StreamGeometry();
					using (var geometryCtx = geometry.Open()) {
						geometryCtx.BeginFigure(drawPoints[0], true, true);
						geometryCtx.PolyLineTo(drawPoints, true, true);
					}
					geometry.Freeze();

					drawCtx.DrawingContext.DrawRectangle(Brushes.White, null, new Rect(drawCtx.Size));
					drawCtx.DrawingContext.DrawGeometry(backBrush, null, geometry);
					DrawBorder(drawCtx);
					drawCtx.DrawingContext.DrawGeometry(null, pen, geometry);
				}
			}
		}

		delegate bool ControlCtrlDelegate(int CtrlType);

		[DllImport("kernel32")]
		static extern bool SetConsoleCtrlHandler(ControlCtrlDelegate HandlerRoutine, bool Add);

		static ControlCtrlDelegate controlCtrlDelegate;

		static void Main(string[] args) {
			// 先注入一个dll，Hook要绘制的窗体的WndProc函数，吃掉WM_PAINT消息，不然任务管理器会闪烁
			var dllInjectionContext = DllInjection.Injection("Taskmgr", Path.Combine(Environment.CurrentDirectory, "HookWinProc.dll"));

			controlCtrlDelegate = type => {
				switch (type) {
					case 0: // CTRL_C_EVENT
					case 2: // CTRL_CLOSE_EVENT
					case 5: // CTRL_LOGOFF_EVENT
					case 6: // CTRL_SHUTDOWN_EVENT
						// 控制台关闭后清理掉注入的dll
						DllInjection.FreeLibrary(dllInjectionContext);
						break;
				}
				return true;
			};
			SetConsoleCtrlHandler(controlCtrlDelegate, true);


			// 获得CPU核心数，其实我之后的代码已经写死默认是4个核心的情况
			var kernelCount = Environment.ProcessorCount;

			var hwnd = WinApi.FindWindow("TaskManagerWindow", "任务管理器");
			var root = new Window(hwnd);
			if (root.Childs.Count == 1 && root.Childs[0].ClassName == "NativeHWNDHost") {
				root = root.Childs[0];
			} else {
				throw new Exception("未找到窗体");
			}
			if (root.Childs.Count == 1 && root.Childs[0].ClassName == "DirectUIHWND") {
				root = root.Childs[0];
			} else {
				throw new Exception("未找到窗体");
			}

			// 拿到4个子窗体，但是顺序还不确定
			var drawWindows = root.Childs
				.Where(w => w.ClassName == "CtrlNotifySink" && w.Childs.Count == 1 && w.Childs[0].ClassName == "CvChartWindow")
				.Select(w => w.Childs[0])
				.OrderByDescending(w => w.Rect.Width * w.Rect.Height)
				.Take(kernelCount)
				.ToArray();

			// 以下是捕获声音有关的代码，使用的是WASAPI Loopback的方式
			var screenWidth = WinApi.GetScreenWidth();
			FixedQueueArray<float> leftWaveQueue = new FixedQueueArray<float>(screenWidth), rightWaveQueue = new FixedQueueArray<float>(screenWidth);
			IWaveIn loopbackCapture = new WasapiLoopbackCapture();
			var observer = new StreamObserver<float>(FFTSize, FFTSize / 2, 2);
			float[] leftFixedWaveData = new float[FFTSize], rightFixedWaveData = new float[FFTSize];

			// 捕获声音
			// leftFixedWaveData 和 rightFixedWaveData 之后会交给绘制频谱的线程处理
			observer.Completed += newData => {
				lock (fftLock) {
					for (int i = 0; i < leftFixedWaveData.Length; i++) {
						leftFixedWaveData[i] = newData[2 * i + 0];
						rightFixedWaveData[i] = newData[2 * i + 1];
					}
				}
			};

			// 捕获声音
			// leftWaveQueue 和 rightWaveQueue 之后会交给绘制声波的线程处理
			loopbackCapture.DataAvailable += (_, e) => {
				var waveData = MemoryMarshal.Cast<byte, float>(new ReadOnlySpan<byte>(e.Buffer, 0, e.BytesRecorded));
				int copyLength = Math.Min(waveData.Length / 2, screenWidth);
				if (copyLength == 0) return;
				Span<float> leftNextData = stackalloc float[copyLength], rightNextData = stackalloc float[copyLength];
				for (int i = 0; i < copyLength; i++) {
					leftNextData[i] = waveData[2 * i + 0];
					rightNextData[i] = waveData[2 * i + 1];
				}
				leftWaveQueue.Write(leftNextData);
				rightWaveQueue.Write(rightNextData);
				observer.Write(waveData);
			};
			loopbackCapture.StartRecording();

			var sampleRate = loopbackCapture.WaveFormat.SampleRate;
			bool polylineStyle = false;

			// 找出对应位置的子窗体
			var leftTopWin = drawWindows.OrderBy(w => w.Rect.X + w.Rect.Y).First();
			var rightTopWin = drawWindows.OrderBy(w => -w.Rect.X + w.Rect.Y).First();
			var leftBottomWin = drawWindows.OrderBy(w => w.Rect.X - w.Rect.Y).First();
			var rightBottomWin = drawWindows.OrderBy(w => -w.Rect.X - w.Rect.Y).First();

			void StartThread(Action action) {
				new Thread(new ThreadStart(action)) {
					Priority = ThreadPriority.Highest
				}.Start();
			}

			// 启动线程开始捕获声音并绘制，每个窗体一个线程
			StartThread(() => DrawWave(leftTopWin, leftWaveQueue, polylineStyle, 1));
			StartThread(() => DrawWave(rightTopWin, rightWaveQueue, polylineStyle, 2));
			StartThread(() => DrawSpectrum(leftBottomWin, leftFixedWaveData, sampleRate, polylineStyle, 3));
			StartThread(() => DrawSpectrum(rightBottomWin, rightFixedWaveData, sampleRate, polylineStyle, 4));

			Thread.Sleep(Timeout.Infinite);
		}
	}
}
