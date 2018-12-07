using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace 任务管理器x频谱分析器 {
	/// <summary>
	/// 表示一个可释放对象，派生类无需定义析构函数
	/// </summary>
	public abstract class DisposableObject : IDisposable {
		~DisposableObject() {
			Dispose(false);
		}

		protected abstract void Dispose(bool disposing);

		public void Dispose() {
			Dispose(true);
			GC.SuppressFinalize(this);
		}
	}
}
