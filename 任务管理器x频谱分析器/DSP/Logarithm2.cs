using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace 任务管理器x频谱分析器.DSP {
	public sealed class Logarithm2 : ILogarithm {
		public double ILog(double y) {
			return FastMath.Pow(2, y) - 1;
		}

		public double Log(double x) {
			return FastMath.Log2(x + 1);
		}
	}
}
