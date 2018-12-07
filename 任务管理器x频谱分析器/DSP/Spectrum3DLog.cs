using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace 任务管理器x频谱分析器.DSP {
	public class Spectrum3DLog : ILogarithm {
		const double CutOffFrequency = 440;

		public double ILog(double y) {
			return CutOffFrequency * (Math.Exp(y / 1127) - 1);
		}

		public double Log(double x) {
			return 1127 * FastMath.Log(1 + x / CutOffFrequency);
		}
	}
}
