using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using System.Threading.Tasks;

namespace 任务管理器x频谱分析器 {
	[SuppressUnmanagedCodeSecurity]
	unsafe public static class DllInjection {
		const int PROCESS_ALL_ACCESS = 0x001fffff;
		const int MEM_COMMIT = 0x1000;
		const int PAGE_EXECUTE_READWRITE = 0x40;
		const int FILE_MAP_READ = 0x4;
		const int MEM_RELEASE = 0x8000;
		const int TOKEN_ALL_ACCESS = 0xf01ff;
		const string SE_DEBUG_NAME = "SeDebugPrivilege";
		const int SE_PRIVILEGE_ENABLED = 2;

		delegate int ThreadProc(void* arg);

		[DllImport("kernel32")]
		static extern IntPtr OpenProcess(int access, bool inheritHandle, int pid);

		[DllImport("kernel32")]
		static extern bool CloseHandle(IntPtr handle);

		[DllImport("kernel32")]
		static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr address, IntPtr buffer, int bufferSize, out int numberOfBytesRead);

		[DllImport("kernel32")]
		static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr address, IntPtr buffer, int bufferSize, out int numberOfBytesWritten);

		[DllImport("kernel32")]
		static extern IntPtr CreateRemoteThread(IntPtr hProcess, void* threadAttributes, IntPtr stackSize, IntPtr threadProc, IntPtr parameter, int creationFlags, int* threadID);

		[DllImport("kernel32")]
		static extern int WaitForSingleObject(IntPtr handle, int milliseconds = -1);

		[DllImport("kernel32", EntryPoint = "GetModuleHandleA")]
		static extern IntPtr GetModuleHandle([MarshalAs(UnmanagedType.LPStr)] string moduleName);

		[DllImport("kernel32")]
		static extern IntPtr GetProcAddress(IntPtr hModule, [MarshalAs(UnmanagedType.LPStr)] string procName);

		[DllImport("kernel32")]
		static extern IntPtr VirtualAllocEx(IntPtr hProcess, void* address, IntPtr size, int allocationType, int protect);

		[DllImport("kernel32")]
		static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr address, IntPtr size, int freeType);

		[DllImport("kernel32", EntryPoint = "OpenFileMappingW")]
		static extern IntPtr OpenFileMapping(int desiredAccess, bool inheritHandle, [MarshalAs(UnmanagedType.LPWStr)] string name);

		[DllImport("kernel32")]
		static extern void* MapViewOfFile(IntPtr memHandle, int desiredAccess, int fileOffsetHigh, int fileOffsetLow, IntPtr numberOfBytesToMap);

		[DllImport("kernel32")]
		static extern bool UnmapViewOfFile(void* buffer);

		[DllImport("kernel32")]
		static extern int GetLastError();

		[DllImport("advapi32")]
		extern static bool OpenProcessToken(IntPtr ProcessHandle, int DesiredAccess, out IntPtr TokenHandle);

		[DllImport("kernel32")]
		extern static IntPtr GetCurrentProcess();

		[DllImport("advapi32", EntryPoint = "LookupPrivilegeValueA")]
		extern static bool LookupPrivilegeValue([MarshalAs(UnmanagedType.LPStr)] string SystemName, [MarshalAs(UnmanagedType.LPStr)] string Name, out long luid);

		[DllImport("advapi32")]
		extern static bool AdjustTokenPrivileges(IntPtr TokenHandle, bool DisableAllPrivileges, void* NewState, int BufferLength, void* PreviousState, int* ReturnLength);

		[StructLayout(LayoutKind.Sequential, Pack = 4)]
		struct LuidAndAttributes {
			public long Luid;
			public int Attributes;
		}

		[StructLayout(LayoutKind.Sequential)]
		struct TokenPrivileges1 {
			public int PrivilegeCount;
			public LuidAndAttributes Privilege;
		}

		const string SharedMemoryName = "Taskmgr_x_Spectrum_Memory";

		public struct Context {
			internal IntPtr hProcess;
			internal IntPtr freeLibraryProcAddress;
		}

		static void SetPrivilege() {
			OpenProcessToken(GetCurrentProcess(), TOKEN_ALL_ACCESS, out var token);
			if (!LookupPrivilegeValue(null, SE_DEBUG_NAME, out var luid)) goto Exit;
			var tp = new TokenPrivileges1 {
				PrivilegeCount = 1,
				Privilege = { Luid = luid, Attributes = SE_PRIVILEGE_ENABLED },
			};
			if (!AdjustTokenPrivileges(token, false, &tp, sizeof(TokenPrivileges1), null, null)) goto Exit;
			Exit:
			CloseHandle(token);
		}

		static DllInjection() {
			SetPrivilege();
		}

		public static Context Injection(string processName, string dllPath) {
			var processes = Process.GetProcessesByName(processName);
			if (processes.Length == 0) throw new InvalidOperationException();
			var process = processes[0];
			var hProcess = OpenProcess(PROCESS_ALL_ACCESS, false, process.Id);
			if (hProcess == IntPtr.Zero) throw new InvalidOperationException();

			var dllPathBufferCount = Encoding.Default.GetByteCount(dllPath);
			var dllPathBuffer = stackalloc byte[dllPathBufferCount + 1];
			fixed (char* s = dllPath) {
				Encoding.Default.GetBytes(s, dllPath.Length, dllPathBuffer, dllPathBufferCount);
			}
			var remoteDllPathBuffer = VirtualAllocEx(hProcess, null, (IntPtr)(dllPathBufferCount + 1), MEM_COMMIT, PAGE_EXECUTE_READWRITE);
			WriteProcessMemory(hProcess, remoteDllPathBuffer, (IntPtr)dllPathBuffer, dllPathBufferCount + 1, out _);

			var hModule = GetModuleHandle("kernel32");
			var loadLibraryAddress = GetProcAddress(hModule, "LoadLibraryA");
			var hThread = CreateRemoteThread(hProcess, null, IntPtr.Zero, loadLibraryAddress, remoteDllPathBuffer, 0, null);
			WaitForSingleObject(hThread);
			CloseHandle(hThread);
			VirtualFreeEx(hProcess, remoteDllPathBuffer, IntPtr.Zero, MEM_RELEASE);

			var mem = OpenFileMapping(FILE_MAP_READ, false, SharedMemoryName);
			var error = GetLastError();
			var buffer = MapViewOfFile(mem, FILE_MAP_READ, 0, 0, IntPtr.Zero);
			var freeLibraryProcAddress = ((IntPtr*)buffer)[0];
			UnmapViewOfFile(buffer);
			CloseHandle(mem);

			return new Context {
				hProcess = hProcess,
				freeLibraryProcAddress = freeLibraryProcAddress,
			};
		}

		public static void FreeLibrary(Context context) {
			var hThread = CreateRemoteThread(context.hProcess, null, IntPtr.Zero, context.freeLibraryProcAddress, IntPtr.Zero, 0, null);
			WaitForSingleObject(hThread);
			CloseHandle(hThread);
			CloseHandle(context.hProcess);
		}
	}
}
