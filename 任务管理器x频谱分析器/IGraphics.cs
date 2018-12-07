using System.Windows;
using System.Windows.Media;

namespace 任务管理器x频谱分析器 {
	public interface IGraphics : System.IDisposable {
		DrawingContext DrawingContext { get; }
		Size Size { get; }
	}
}