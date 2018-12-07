using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace 任务管理器x频谱分析器 {
	public class FixedQueueArray<T> where T : unmanaged {
		private readonly T[] buffer;

		public int FixedLength => buffer.Length;

		public FixedQueueArray(int fixedLength) {
			buffer = new T[fixedLength];
		}

		public void Write(ReadOnlySpan<T> inBuffer) {
			lock (buffer) {
				if (inBuffer.Length >= buffer.Length) {
					inBuffer.Slice(inBuffer.Length - buffer.Length).CopyTo(buffer);
				} else {
					buffer.AsSpan(inBuffer.Length).CopyTo(buffer);
					inBuffer.CopyTo(buffer.AsSpan(buffer.Length - inBuffer.Length));
				}
			}
		}

		public void Read(Span<T> outBuffer) {
			lock (buffer) {
				if (outBuffer.Length <= buffer.Length) {
					buffer.AsSpan(buffer.Length - outBuffer.Length).CopyTo(outBuffer);
				} else {
					buffer.CopyTo(outBuffer);
				}
			}
		}
	}
}
