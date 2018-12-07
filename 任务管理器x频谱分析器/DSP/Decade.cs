using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace 任务管理器x频谱分析器.DSP {
	public sealed class Decade : ILogarithm {
		public double ILog(double y) {
			return FastMath.Pow(10, y);
		}

		public double Log(double x) {
			return FastMath.Log10(x);
		}
	}
}
