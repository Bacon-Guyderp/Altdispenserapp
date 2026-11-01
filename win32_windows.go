package main

// Thin bindings to the parts of the Windows API the GUI uses. Everything is
// loaded from DLLs that ship with Windows, so the program needs no runtime.

import (
	"syscall"
	"unsafe"
)

var (
	user32   = syscall.NewLazyDLL("user32.dll")
	kernel32 = syscall.NewLazyDLL("kernel32.dll")
	gdi32    = syscall.NewLazyDLL("gdi32.dll")
	comctl32 = syscall.NewLazyDLL("comctl32.dll")
	comdlg32 = syscall.NewLazyDLL("comdlg32.dll")
	shell32  = syscall.NewLazyDLL("shell32.dll")
	ole32    = syscall.NewLazyDLL("ole32.dll")

	pRegisterClassExW      = user32.NewProc("RegisterClassExW")
	pCreateWindowExW       = user32.NewProc("CreateWindowExW")
	pDefWindowProcW        = user32.NewProc("DefWindowProcW")
	pGetMessageW           = user32.NewProc("GetMessageW")
	pTranslateMessage      = user32.NewProc("TranslateMessage")
	pDispatchMessageW      = user32.NewProc("DispatchMessageW")
	pIsDialogMessageW      = user32.NewProc("IsDialogMessageW")
	pPostQuitMessage       = user32.NewProc("PostQuitMessage")
	pSendMessageW          = user32.NewProc("SendMessageW")
	pPostMessageW          = user32.NewProc("PostMessageW")
	pMoveWindow            = user32.NewProc("MoveWindow")
	pSetWindowPos          = user32.NewProc("SetWindowPos")
	pGetClientRect         = user32.NewProc("GetClientRect")
	pShowWindow            = user32.NewProc("ShowWindow")
	pUpdateWindow          = user32.NewProc("UpdateWindow")
	pEnableWindow          = user32.NewProc("EnableWindow")
	pSetWindowTextW        = user32.NewProc("SetWindowTextW")
	pGetWindowTextW        = user32.NewProc("GetWindowTextW")
	pGetWindowTextLengthW  = user32.NewProc("GetWindowTextLengthW")
	pMessageBoxW           = user32.NewProc("MessageBoxW")
	pMessageBeep           = user32.NewProc("MessageBeep")
	pLoadCursorW           = user32.NewProc("LoadCursorW")
	pLoadImageW            = user32.NewProc("LoadImageW")
	pDestroyWindow         = user32.NewProc("DestroyWindow")
	pGetDC                 = user32.NewProc("GetDC")
	pReleaseDC             = user32.NewProc("ReleaseDC")
	pGetSysColor           = user32.NewProc("GetSysColor")
	pGetSysColorBrush      = user32.NewProc("GetSysColorBrush")
	pGetDpiForSystem       = user32.NewProc("GetDpiForSystem")
	pSetProcessDPIAware    = user32.NewProc("SetProcessDPIAware")
	pRedrawWindow          = user32.NewProc("RedrawWindow")
	pGetModuleHandleW      = kernel32.NewProc("GetModuleHandleW")
	pCreateFontW           = gdi32.NewProc("CreateFontW")
	pGetDeviceCaps         = gdi32.NewProc("GetDeviceCaps")
	pSelectObject          = gdi32.NewProc("SelectObject")
	pGetTextExtentPoint32W = gdi32.NewProc("GetTextExtentPoint32W")
	pSetTextColor          = gdi32.NewProc("SetTextColor")
	pSetBkColor            = gdi32.NewProc("SetBkColor")
	pSetBkMode             = gdi32.NewProc("SetBkMode")
	pInitCommonControlsEx  = comctl32.NewProc("InitCommonControlsEx")
	pGetOpenFileNameW      = comdlg32.NewProc("GetOpenFileNameW")
	pSHBrowseForFolderW    = shell32.NewProc("SHBrowseForFolderW")
	pSHGetPathFromIDListW  = shell32.NewProc("SHGetPathFromIDListW")
	pDragAcceptFiles       = shell32.NewProc("DragAcceptFiles")
	pDragQueryFileW        = shell32.NewProc("DragQueryFileW")
	pDragFinish            = shell32.NewProc("DragFinish")
	pShellExecuteW         = shell32.NewProc("ShellExecuteW")
	pCoInitializeEx        = ole32.NewProc("CoInitializeEx")
	pCoTaskMemFree         = ole32.NewProc("CoTaskMemFree")
)

const (
	wsChild          = 0x40000000
	wsVisible        = 0x10000000
	wsTabStop        = 0x00010000
	wsGroup          = 0x00020000
	wsVScroll        = 0x00200000
	wsHScroll        = 0x00100000
	wsClipSiblings   = 0x04000000
	wsClipChildren   = 0x02000000
	wsOverlappedWin  = 0x00CF0000
	wsExClientEdge   = 0x00000200
	wsExAcceptFiles  = 0x00000010
	esMultiline      = 0x0004
	esAutoVScroll    = 0x0040
	esAutoHScroll    = 0x0080
	esReadOnly       = 0x0800
	bsPushButton     = 0x0
	bsDefPushButton  = 0x1
	bsAutoCheckBox   = 0x3
	bsGroupBox       = 0x7
	ssNoPrefix       = 0x80
	ssCenterImage    = 0x200
	ssEndEllipsis    = 0x4000
	lbsNotify        = 0x0001
	lbsHasStrings    = 0x0040
	lbsNoIntegral    = 0x0100
	lbsExtendedSel   = 0x0800
	wmDestroy        = 0x0002
	wmSize           = 0x0005
	wmClose          = 0x0010
	wmSetFont        = 0x0030
	wmGetMinMaxInfo  = 0x0024
	wmKeyDown        = 0x0100
	wmCommand        = 0x0111
	wmCtlColorStatic = 0x0138
	wmDropFiles      = 0x0233
	wmUser           = 0x0400
	wmApp            = 0x8000
	bmGetCheck       = 0x00F0
	bmSetCheck       = 0x00F1
	lbAddString      = 0x0180
	lbDeleteString   = 0x0182
	lbResetContent   = 0x0184
	lbGetText        = 0x0189
	lbGetTextLen     = 0x018A
	lbGetCount       = 0x018B
	lbGetSelCount    = 0x0190
	lbGetSelItems    = 0x0191
	lbSetHorizExtent = 0x0194
	emSetSel         = 0x00B1
	emReplaceSel     = 0x00C2
	emSetLimitText   = 0x00C5
	pbmSetPos        = 0x0402
	pbmSetRange32    = 0x0406
	swShowDefault    = 10
	swShowNormal     = 1
	cwUseDefault     = 0x80000000
	colorWindow      = 5
	colorBtnFace     = 15
	colorGrayText    = 17
	mbOK             = 0x0
	mbYesNo          = 0x4
	mbIconError      = 0x10
	mbIconQuestion   = 0x20
	mbIconWarning    = 0x30
	mbIconInfo       = 0x40
	idYes            = 6
	vkDelete         = 0x2E
	swpNoSize        = 0x1
	swpNoMove        = 0x2
	swpNoActivate    = 0x10
	hwndBottom       = 1
)

type wndClassEx struct {
	cbSize        uint32
	style         uint32
	lpfnWndProc   uintptr
	cbClsExtra    int32
	cbWndExtra    int32
	hInstance     uintptr
	hIcon         uintptr
	hCursor       uintptr
	hbrBackground uintptr
	lpszMenuName  *uint16
	lpszClassName *uint16
	hIconSm       uintptr
}

type point struct{ x, y int32 }

type msg struct {
	hwnd    uintptr
	message uint32
	wParam  uintptr
	lParam  uintptr
	time    uint32
	pt      point
	private uint32
}

type rect struct{ left, top, right, bottom int32 }

type minMaxInfo struct {
	reserved, maxSize, maxPosition, minTrackSize, maxTrackSize point
}

type openFileName struct {
	lStructSize       uint32
	hwndOwner         uintptr
	hInstance         uintptr
	lpstrFilter       *uint16
	lpstrCustomFilter *uint16
	nMaxCustFilter    uint32
	nFilterIndex      uint32
	lpstrFile         *uint16
	nMaxFile          uint32
	lpstrFileTitle    *uint16
	nMaxFileTitle     uint32
	lpstrInitialDir   *uint16
	lpstrTitle        *uint16
	flags             uint32
	nFileOffset       uint16
	nFileExtension    uint16
	lpstrDefExt       *uint16
	lCustData         uintptr
	lpfnHook          uintptr
	lpTemplateName    *uint16
	pvReserved        uintptr
	dwReserved        uint32
	flagsEx           uint32
}

type browseInfo struct {
	hwndOwner      uintptr
	pidlRoot       uintptr
	pszDisplayName *uint16
	lpszTitle      *uint16
	ulFlags        uint32
	lpfn           uintptr
	lParam         uintptr
	iImage         int32
}

type size struct{ cx, cy int32 }

func u16(s string) *uint16 {
	p, _ := syscall.UTF16PtrFromString(s)
	return p
}

func ptr(p *uint16) uintptr { return uintptr(unsafe.Pointer(p)) }

func sendMessage(hwnd uintptr, m uint32, w, l uintptr) uintptr {
	r, _, _ := pSendMessageW.Call(hwnd, uintptr(m), w, l)
	return r
}

func getText(hwnd uintptr) string {
	n, _, _ := pGetWindowTextLengthW.Call(hwnd)
	buf := make([]uint16, n+1)
	pGetWindowTextW.Call(hwnd, uintptr(unsafe.Pointer(&buf[0])), n+1)
	return syscall.UTF16ToString(buf)
}

func setText(hwnd uintptr, s string) {
	pSetWindowTextW.Call(hwnd, ptr(u16(s)))
}

func enable(hwnd uintptr, on bool) {
	v := uintptr(0)
	if on {
		v = 1
	}
	pEnableWindow.Call(hwnd, v)
}

func isChecked(hwnd uintptr) bool { return sendMessage(hwnd, bmGetCheck, 0, 0) == 1 }

func setChecked(hwnd uintptr, on bool) {
	v := uintptr(0)
	if on {
		v = 1
	}
	sendMessage(hwnd, bmSetCheck, v, 0)
}

func messageBox(owner uintptr, text, title string, flags uint32) int {
	r, _, _ := pMessageBoxW.Call(owner, ptr(u16(text)), ptr(u16(title)), uintptr(flags))
	return int(r)
}
