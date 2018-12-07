// dllmain.cpp : 定义 DLL 应用程序的入口点。
#include "stdafx.h"
#include <vector>
#include <string>
#include <algorithm>
#include <unordered_map>

struct Rect {
	int left;
	int top;
	int right;
	int bottom;

	Rect(const RECT &r) : left(r.left), top(r.top), right(r.right), bottom(r.bottom) {}

	int width() const {
		return right - left;
	}

	int height() const {
		return bottom - top;
	}

	int area() const {
		return width() * height();
	}
};

struct Window {
	HWND hwnd;
	std::vector<Window> children;

	Window(HWND hwnd) : Window(hwnd, true) {}

	Window(HWND hwnd, bool visibleOnly) : hwnd(hwnd) {
		auto child = FindWindowEx(hwnd, nullptr, nullptr, nullptr);
		while (child) {
			if (visibleOnly) {
				RECT rect;
				GetWindowRect(child, &rect);
				if (rect.right <= rect.left || rect.bottom <= rect.top) goto Next;
				int style = GetWindowLong(child, GWL_STYLE);
				if ((style & WS_VISIBLE) == 0)  goto Next;
			}
			children.emplace_back(child);

		Next:
			child = FindWindowEx(hwnd, child, nullptr, nullptr);
		}
	}

	std::wstring ClassName() const {
		WCHAR buf[1000];
		GetClassName(hwnd, buf, 1000);
		return buf;
	}

	Rect Rect() const {
		RECT rect;
		GetWindowRect(hwnd, &rect);
		return rect;
	}
};

std::unordered_map<HWND, LRESULT(CALLBACK *)(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam)> oldWndProc;

LRESULT CALLBACK WndProcHook(HWND hWnd, UINT message, WPARAM wParam, LPARAM lParam) {
	auto oldProc = oldWndProc[hWnd];
	if (message == WM_PAINT) { // 在这里吃掉WM_PAINT消息
		return DefWindowProc(hWnd, message, wParam, lParam);
	}
	return oldProc(hWnd, message, wParam, lParam);
}

const WCHAR *const SharedMemoryName = L"Taskmgr_x_Spectrum_Memory";
HANDLE sharedMemoryHandle = nullptr;

BOOL FreeThisLibrary(void *) {
	auto handle = GetModuleHandle(L"HookWinProc");
	return FreeLibrary(handle);
}

BOOL APIENTRY DllMain(HMODULE hModule, DWORD ul_reason_for_call, LPVOID lpReserved) {
	switch (ul_reason_for_call) {
	case DLL_PROCESS_ATTACH: {
		auto hwnd = FindWindow(L"TaskManagerWindow", L"任务管理器");
		if (!hwnd) return FALSE;
		Window root(hwnd), *proot = &root;
		if (proot->children.size() == 1 && proot->children[0].ClassName() == L"NativeHWNDHost") {
			proot = &proot->children[0];
		} else {
			return FALSE;
		}
		if (proot->children.size() == 1 && proot->children[0].ClassName() == L"DirectUIHWND") {
			proot = &proot->children[0];
		} else {
			return FALSE;
		}

		std::vector<const Window *> subWindows;
		for (const auto &win : proot->children) {
			if (win.ClassName() == L"CtrlNotifySink" && win.children.size() == 1 && win.children[0].ClassName() == L"CvChartWindow") {
				subWindows.push_back(&win.children[0]);
			}
		}
		std::sort(std::begin(subWindows), std::end(subWindows), [](const Window *left, const Window *right) {
			return right->Rect().area() - left->Rect().area();
		});

		const int kernelCount = 4;
		HWND targetWindows[kernelCount];
		for (int i = 0; i < kernelCount; i++) {
			targetWindows[i] = subWindows[i]->hwnd;
			auto oldProc = GetWindowLongPtr(targetWindows[i], GWLP_WNDPROC);
			oldWndProc.insert(std::make_pair(targetWindows[i], (LRESULT(CALLBACK *)(HWND, UINT, WPARAM, LPARAM))oldProc));
			SetWindowLongPtr(targetWindows[i], GWLP_WNDPROC, (LONG_PTR)WndProcHook);
		}

		sharedMemoryHandle = CreateFileMapping(INVALID_HANDLE_VALUE, nullptr, PAGE_READWRITE | SEC_COMMIT, 0, 1024, SharedMemoryName);
		void *buff = MapViewOfFile(sharedMemoryHandle, FILE_MAP_WRITE, 0, 0, 0);
		((void **)buff)[0] = FreeThisLibrary;
		UnmapViewOfFile(buff);
		break;
	}

	case DLL_PROCESS_DETACH: {
		for (const auto &kv : oldWndProc) {
			SetWindowLongPtr(kv.first, GWLP_WNDPROC, (LONG_PTR)kv.second);
		}
		CloseHandle(sharedMemoryHandle);
		break;
	}
	}
	return TRUE;
}

