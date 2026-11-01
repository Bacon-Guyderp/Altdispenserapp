package main

import (
	"context"
	"fmt"
	"os"
	"path/filepath"
	"runtime"
	"sort"
	"strings"
	"sync"
	"syscall"
	"time"
	"unicode/utf16"
	"unsafe"
)

const appName = "Blend2Unity"

// Control IDs. Convert uses IDOK (1) so Enter starts a conversion.
const (
	idConvert = 1 + iota
	idCancel2 // unused; IDCANCEL (2) is sent by Esc and must not map to anything
	idBrowseBlender
	idDetect
	idAddFiles
	idAddFolder
	idRemove
	idClear
	idBrowseOutput
	idNextToSource
	idOpenOutput
	idCancel
	idOptions // option checkboxes use idOptions+n
)

const wmRunQueued = wmApp + 1

func init() { runtime.LockOSThread() } // all window calls must stay on one OS thread

type app struct {
	hwnd, hinst      uintptr
	font, monoFont   uintptr
	dpi              int
	ready            bool
	textWidths       map[uintptr]int
	listExtent       int
	grpBlender       uintptr
	edBlender        uintptr
	btnBrowseBlender uintptr
	btnDetect        uintptr
	grpFiles         uintptr
	list             uintptr
	btnAddFiles      uintptr
	btnAddFolder     uintptr
	btnRemove        uintptr
	btnClear         uintptr
	grpOutput        uintptr
	lblSaveTo        uintptr
	edOutput         uintptr
	btnBrowseOutput  uintptr
	chkNextToSource  uintptr
	lblUnityFolder   uintptr
	edUnityFolder    uintptr
	lblHint          uintptr
	grpOptions       uintptr
	chkTextures      uintptr
	chkAnimation     uintptr
	chkModifiers     uintptr
	chkTransform     uintptr
	chkIncludeBlend  uintptr
	grpProgress      uintptr
	lblFile          uintptr
	barFile          uintptr
	lblOverall       uintptr
	barOverall       uintptr
	log              uintptr
	btnOpenOutput    uintptr
	btnCancel        uintptr
	btnConvert       uintptr

	busy          bool
	cancel        context.CancelFunc
	lastOutputDir string
	closeWhenDone bool

	queueMu sync.Mutex
	queue   []func()
}

var ui = &app{textWidths: map[uintptr]int{}}

func main() {
	pCoInitializeEx.Call(0, 2) // apartment-threaded, needed by the folder picker
	if pSetProcessDPIAware.Find() == nil {
		pSetProcessDPIAware.Call()
	}
	icc := [2]uint32{8, 0x20 | 0x4000} // ICC_PROGRESS_CLASS | ICC_STANDARD_CLASSES
	pInitCommonControlsEx.Call(uintptr(unsafe.Pointer(&icc)))
	ui.run(os.Args[1:])
}

func (a *app) s(px int) int { return px * a.dpi / 96 }

func (a *app) run(args []string) {
	a.hinst, _, _ = pGetModuleHandleW.Call(0)
	a.dpi = 96
	if pGetDpiForSystem.Find() == nil {
		d, _, _ := pGetDpiForSystem.Call()
		a.dpi = int(d)
	} else {
		dc, _, _ := pGetDC.Call(0)
		d, _, _ := pGetDeviceCaps.Call(dc, 90) // LOGPIXELSY
		pReleaseDC.Call(0, dc)
		a.dpi = int(d)
	}
	if a.dpi <= 0 {
		a.dpi = 96
	}
	a.font = a.createFont("Segoe UI", 9)
	a.monoFont = a.createFont("Consolas", 9)

	icon, _, _ := pLoadImageW.Call(a.hinst, ptr(u16("APP")), 1, 0, 0, 0x40|0x8000) // IMAGE_ICON, LR_DEFAULTSIZE|LR_SHARED
	cursor, _, _ := pLoadCursorW.Call(0, 32512)                                    // IDC_ARROW
	className := u16("Blend2UnityWindow")
	wc := wndClassEx{
		lpfnWndProc:   syscall.NewCallback(a.wndProc),
		hInstance:     a.hinst,
		hIcon:         icon,
		hIconSm:       icon,
		hCursor:       cursor,
		hbrBackground: colorBtnFace + 1,
		lpszClassName: className,
	}
	wc.cbSize = uint32(unsafe.Sizeof(wc))
	pRegisterClassExW.Call(uintptr(unsafe.Pointer(&wc)))

	w, h := a.s(780), a.s(800)
	if sh := screenHeight(); sh > 0 && h > sh-a.s(40) {
		h = sh - a.s(40)
	}
	a.hwnd, _, _ = pCreateWindowExW.Call(wsExAcceptFiles, ptr(className),
		ptr(u16(appName+" — .blend to .unitypackage")), wsOverlappedWin,
		cwUseDefault, cwUseDefault, uintptr(w), uintptr(h), 0, 0, a.hinst, 0)
	if a.hwnd == 0 {
		messageBox(0, "Could not create the main window.", appName, mbIconError)
		return
	}
	a.createControls()
	a.loadSettings()
	a.addPaths(args)
	a.ready = true
	a.layout()
	a.updateButtons()
	pShowWindow.Call(a.hwnd, swShowDefault)
	pUpdateWindow.Call(a.hwnd)

	var m msg
	for {
		r, _, _ := pGetMessageW.Call(uintptr(unsafe.Pointer(&m)), 0, 0, 0)
		if int32(r) <= 0 {
			break
		}
		if m.message == wmKeyDown && m.hwnd == a.list && m.wParam == vkDelete {
			a.removeSelected()
			continue
		}
		if r, _, _ := pIsDialogMessageW.Call(a.hwnd, uintptr(unsafe.Pointer(&m))); r != 0 {
			continue
		}
		pTranslateMessage.Call(uintptr(unsafe.Pointer(&m)))
		pDispatchMessageW.Call(uintptr(unsafe.Pointer(&m)))
	}
}

func screenHeight() int {
	p := user32.NewProc("GetSystemMetrics")
	h, _, _ := p.Call(17) // SM_CYFULLSCREEN
	return int(h)
}

func (a *app) createFont(face string, points int) uintptr {
	f, _, _ := pCreateFontW.Call(uintptr(int32(-points*a.dpi/72)), 0, 0, 0, 400, 0, 0, 0, 1, 0, 0, 5, 0, ptr(u16(face)))
	return f
}

// ---------------------------------------------------------------- controls

func (a *app) ctl(class, text string, style, exStyle uint32, id int) uintptr {
	h, _, _ := pCreateWindowExW.Call(uintptr(exStyle), ptr(u16(class)), ptr(u16(text)),
		uintptr(style|wsChild|wsVisible|wsClipSiblings), 0, 0, 0, 0, a.hwnd, uintptr(id), a.hinst, 0)
	sendMessage(h, wmSetFont, a.font, 1)
	a.textWidths[h] = a.measure(text)
	return h
}

func (a *app) group(text string) uintptr {
	return a.ctl("BUTTON", text, bsGroupBox, 0, 0)
}

func (a *app) button(text string, id int) uintptr {
	return a.ctl("BUTTON", text, bsPushButton|wsTabStop, 0, id)
}

func (a *app) check(text string, id int) uintptr {
	return a.ctl("BUTTON", text, bsAutoCheckBox|wsTabStop, 0, id)
}

func (a *app) label(text string, extra uint32) uintptr {
	return a.ctl("STATIC", text, ssNoPrefix|ssCenterImage|extra, 0, 0)
}

func (a *app) edit(extra uint32) uintptr {
	return a.ctl("EDIT", "", esAutoHScroll|wsTabStop|extra, wsExClientEdge, 0)
}

func (a *app) createControls() {
	a.grpBlender = a.group("Blender")
	a.edBlender = a.edit(0)
	a.btnBrowseBlender = a.button("Browse…", idBrowseBlender)
	a.btnDetect = a.button("Detect", idDetect)

	a.grpFiles = a.group(".blend files")
	a.list = a.ctl("LISTBOX", "", lbsNotify|lbsHasStrings|lbsNoIntegral|lbsExtendedSel|wsVScroll|wsHScroll|wsTabStop, wsExClientEdge, 0)
	a.btnAddFiles = a.button("Add files…", idAddFiles)
	a.btnAddFolder = a.button("Add folder…", idAddFolder)
	a.btnRemove = a.button("Remove", idRemove)
	a.btnClear = a.button("Clear", idClear)

	a.grpOutput = a.group("Output")
	a.lblSaveTo = a.label("Save to:", 0)
	a.edOutput = a.edit(0)
	a.btnBrowseOutput = a.button("Browse…", idBrowseOutput)
	a.chkNextToSource = a.check("Save next to each .blend file", idNextToSource)
	a.lblUnityFolder = a.label("Unity folder:", 0)
	a.edUnityFolder = a.edit(0)
	a.lblHint = a.label("{name} = file name", 0)

	a.grpOptions = a.group("Options")
	a.chkTextures = a.check("Include textures", idOptions)
	a.chkAnimation = a.check("Include animation", idOptions+1)
	a.chkModifiers = a.check("Apply modifiers", idOptions+2)
	a.chkTransform = a.check("Apply transform (fixes -90° rotation)", idOptions+3)
	a.chkIncludeBlend = a.check("Also include original .blend", idOptions+4)

	a.grpProgress = a.group("Progress")
	a.lblFile = a.label("Idle", ssEndEllipsis)
	a.barFile = a.ctl("msctls_progress32", "", 0, 0, 0)
	a.lblOverall = a.label("Add .blend files to begin.", ssEndEllipsis)
	a.barOverall = a.ctl("msctls_progress32", "", 0, 0, 0)
	for _, b := range []uintptr{a.barFile, a.barOverall} {
		sendMessage(b, pbmSetRange32, 0, 1001)
	}

	a.log = a.ctl("EDIT", "", esMultiline|esAutoVScroll|esReadOnly|wsVScroll|wsTabStop, wsExClientEdge, 0)
	sendMessage(a.log, wmSetFont, a.monoFont, 1)
	sendMessage(a.log, emSetLimitText, 0, 0)

	a.btnOpenOutput = a.button("Open output folder", idOpenOutput)
	a.btnCancel = a.button("Cancel", idCancel)
	a.btnConvert = a.ctl("BUTTON", "Convert", bsDefPushButton|wsTabStop, 0, idConvert)

	// Group boxes go behind the controls they frame.
	for _, g := range []uintptr{a.grpBlender, a.grpFiles, a.grpOutput, a.grpOptions, a.grpProgress} {
		pSetWindowPos.Call(g, hwndBottom, 0, 0, 0, 0, swpNoMove|swpNoSize|swpNoActivate)
	}
}

func (a *app) measure(text string) int {
	if text == "" {
		return 0
	}
	dc, _, _ := pGetDC.Call(a.hwnd)
	old, _, _ := pSelectObject.Call(dc, a.font)
	s := utf16.Encode([]rune(text))
	var sz size
	pGetTextExtentPoint32W.Call(dc, uintptr(unsafe.Pointer(&s[0])), uintptr(len(s)), uintptr(unsafe.Pointer(&sz)))
	pSelectObject.Call(dc, old)
	pReleaseDC.Call(a.hwnd, dc)
	return int(sz.cx)
}

// ------------------------------------------------------------------ layout

func move(h uintptr, x, y, w, ht int) {
	if w < 0 {
		w = 0
	}
	if ht < 0 {
		ht = 0
	}
	pMoveWindow.Call(h, uintptr(x), uintptr(y), uintptr(w), uintptr(ht), 1)
}

func maxInt(a, b int) int {
	if a > b {
		return a
	}
	return b
}

func (a *app) layout() {
	if !a.ready {
		return
	}
	var rc rect
	pGetClientRect.Call(a.hwnd, uintptr(unsafe.Pointer(&rc)))
	W, H := int(rc.right), int(rc.bottom)
	s := a.s
	m, gap, gt, gp := s(10), s(8), s(20), s(10)
	bh, eh, ch, lh, pbh := s(26), s(23), s(22), s(18), s(18)
	bw := maxInt(s(96), a.textWidths[a.btnAddFolder]+s(24))
	x0, gw := m, W-2*m
	ix, iw := x0+gp, gw-2*gp

	// Option checkboxes flow onto as many rows as needed.
	checks := []uintptr{a.chkTextures, a.chkAnimation, a.chkModifiers, a.chkTransform, a.chkIncludeBlend}
	type pos struct{ x, row, w int }
	var places []pos
	cx, row := ix, 0
	for _, c := range checks {
		w := a.textWidths[c] + s(28)
		if cx > ix && cx+w > ix+iw {
			cx, row = ix, row+1
		}
		places = append(places, pos{cx, row, w})
		cx += w + s(8)
	}
	optRows := row + 1

	hBlender := gt + bh + gp
	hOutput := gt + bh + s(4) + ch + s(4) + bh + gp
	hOptions := gt + optRows*ch + (optRows-1)*s(4) + gp
	hProgress := gt + lh + s(3) + pbh + s(8) + lh + s(3) + pbh + gp
	flex := H - 2*m - hBlender - hOutput - hOptions - hProgress - bh - 6*gap
	hFiles := maxInt(gt+2*bh+s(6)+gp, flex*55/100)
	hLog := maxInt(s(60), flex-hFiles)

	y := m
	// Blender
	move(a.grpBlender, x0, y, gw, hBlender)
	iy := y + gt
	move(a.btnDetect, ix+iw-bw, iy, bw, bh)
	move(a.btnBrowseBlender, ix+iw-2*bw-gap, iy, bw, bh)
	move(a.edBlender, ix, iy+(bh-eh)/2, iw-2*bw-2*gap, eh)
	y += hBlender + gap

	// Files
	move(a.grpFiles, x0, y, gw, hFiles)
	iy = y + gt
	bx := ix + iw - 2*bw - s(6)
	move(a.list, ix, iy, bx-gap-ix, hFiles-gt-gp)
	move(a.btnAddFiles, bx, iy, bw, bh)
	move(a.btnAddFolder, bx+bw+s(6), iy, bw, bh)
	move(a.btnRemove, bx, iy+bh+s(6), bw, bh)
	move(a.btnClear, bx+bw+s(6), iy+bh+s(6), bw, bh)
	y += hFiles + gap

	// Output
	move(a.grpOutput, x0, y, gw, hOutput)
	iy = y + gt
	lw := maxInt(a.textWidths[a.lblSaveTo], a.textWidths[a.lblUnityFolder]) + s(10)
	hw := a.textWidths[a.lblHint] + s(4)
	move(a.lblSaveTo, ix, iy+(bh-eh)/2, lw, eh)
	move(a.edOutput, ix+lw, iy+(bh-eh)/2, iw-lw-bw-gap, eh)
	move(a.btnBrowseOutput, ix+iw-bw, iy, bw, bh)
	iy += bh + s(4)
	move(a.chkNextToSource, ix+lw, iy, a.textWidths[a.chkNextToSource]+s(28), ch)
	iy += ch + s(4)
	move(a.lblUnityFolder, ix, iy+(bh-eh)/2, lw, eh)
	move(a.edUnityFolder, ix+lw, iy+(bh-eh)/2, iw-lw-hw-gap, eh)
	move(a.lblHint, ix+iw-hw, iy+(bh-eh)/2, hw, eh)
	y += hOutput + gap

	// Options
	move(a.grpOptions, x0, y, gw, hOptions)
	for i, c := range checks {
		p := places[i]
		move(c, p.x, y+gt+p.row*(ch+s(4)), p.w, ch)
	}
	y += hOptions + gap

	// Progress
	move(a.grpProgress, x0, y, gw, hProgress)
	iy = y + gt
	move(a.lblFile, ix, iy, iw, lh)
	move(a.barFile, ix, iy+lh+s(3), iw, pbh)
	iy += lh + s(3) + pbh + s(8)
	move(a.lblOverall, ix, iy, iw, lh)
	move(a.barOverall, ix, iy+lh+s(3), iw, pbh)
	y += hProgress + gap

	// Log
	move(a.log, x0, y, gw, hLog)
	y += hLog + gap

	// Actions
	ow := maxInt(bw, a.textWidths[a.btnOpenOutput]+s(24))
	move(a.btnOpenOutput, x0, y, ow, bh)
	move(a.btnConvert, x0+gw-bw, y, bw, bh)
	move(a.btnCancel, x0+gw-2*bw-gap, y, bw, bh)
}

// ---------------------------------------------------------- window messages

func (a *app) wndProc(hwnd, message, wParam, lParam uintptr) uintptr {
	switch message {
	case wmSize:
		a.layout()
		return 0
	case wmGetMinMaxInfo:
		mmi := *(**minMaxInfo)(unsafe.Pointer(&lParam)) // lParam points to a MINMAXINFO
		mmi.minTrackSize = point{int32(a.s(640)), int32(a.s(600))}
		return 0
	case wmCtlColorStatic:
		hdc := wParam
		switch lParam {
		case a.lblHint:
			c, _, _ := pGetSysColor.Call(colorGrayText)
			pSetTextColor.Call(hdc, c)
			pSetBkMode.Call(hdc, 1) // TRANSPARENT
			b, _, _ := pGetSysColorBrush.Call(colorBtnFace)
			return b
		case a.log:
			c, _, _ := pGetSysColor.Call(colorWindow)
			pSetBkColor.Call(hdc, c)
			b, _, _ := pGetSysColorBrush.Call(colorWindow)
			return b
		}
	case wmCommand:
		if wParam>>16 == 0 { // BN_CLICKED
			a.onCommand(int(wParam & 0xFFFF))
		}
		return 0
	case wmDropFiles:
		a.onDrop(wParam)
		return 0
	case wmRunQueued:
		a.queueMu.Lock()
		q := a.queue
		a.queue = nil
		a.queueMu.Unlock()
		for _, f := range q {
			f()
		}
		return 0
	case wmClose:
		a.onClose()
		return 0
	case wmDestroy:
		pPostQuitMessage.Call(0)
		return 0
	}
	r, _, _ := pDefWindowProcW.Call(hwnd, message, wParam, lParam)
	return r
}

// post runs f on the UI thread.
func (a *app) post(f func()) {
	a.queueMu.Lock()
	a.queue = append(a.queue, f)
	first := len(a.queue) == 1
	a.queueMu.Unlock()
	if first {
		pPostMessageW.Call(a.hwnd, wmRunQueued, 0, 0)
	}
}

func (a *app) onCommand(id int) {
	switch id {
	case idConvert:
		a.start()
	case idBrowseBlender:
		if p := a.openFiles("Select blender.exe", "blender.exe\x00blender.exe\x00Programs (*.exe)\x00*.exe\x00All files\x00*.*\x00", false); len(p) > 0 {
			setText(a.edBlender, p[0])
		}
	case idDetect:
		if p := FindBlender(); p != "" {
			setText(a.edBlender, p)
			a.logLine("Found Blender: " + p)
		} else {
			messageBox(a.hwnd, "Blender could not be found automatically.\nInstall Blender from blender.org, or use Browse… to locate blender.exe.", appName, mbIconWarning)
		}
	case idAddFiles:
		a.addPaths(a.openFiles("Select .blend files", "Blender files (*.blend)\x00*.blend\x00All files\x00*.*\x00", true))
	case idAddFolder:
		if d := a.browseFolder("Add every .blend file in this folder (and its subfolders):", ""); d != "" {
			a.addPaths([]string{d})
		}
	case idRemove:
		a.removeSelected()
	case idClear:
		if !a.busy {
			sendMessage(a.list, lbResetContent, 0, 0)
			a.listExtent = 0
			sendMessage(a.list, lbSetHorizExtent, 0, 0)
			a.updateButtons()
		}
	case idBrowseOutput:
		if d := a.browseFolder("Choose where to save .unitypackage files:", getText(a.edOutput)); d != "" {
			setText(a.edOutput, d)
		}
	case idNextToSource:
		a.updateButtons()
	case idOpenOutput:
		if a.lastOutputDir != "" {
			pShellExecuteW.Call(a.hwnd, ptr(u16("open")), ptr(u16(a.lastOutputDir)), 0, 0, swShowNormal)
		}
	case idCancel:
		if a.busy && a.cancel != nil {
			a.cancel()
			setText(a.lblOverall, "Cancelling…")
		}
	}
}

func (a *app) onDrop(hdrop uintptr) {
	defer pDragFinish.Call(hdrop)
	if a.busy {
		return
	}
	n, _, _ := pDragQueryFileW.Call(hdrop, 0xFFFFFFFF, 0, 0)
	var paths []string
	for i := uintptr(0); i < n; i++ {
		l, _, _ := pDragQueryFileW.Call(hdrop, i, 0, 0)
		buf := make([]uint16, l+1)
		pDragQueryFileW.Call(hdrop, i, uintptr(unsafe.Pointer(&buf[0])), l+1)
		paths = append(paths, syscall.UTF16ToString(buf))
	}
	a.addPaths(paths)
}

func (a *app) onClose() {
	if a.busy {
		if messageBox(a.hwnd, "A conversion is running. Cancel it and quit?", appName, mbYesNo|mbIconQuestion) == idYes {
			a.closeWhenDone = true
			a.cancel()
		}
		return
	}
	a.saveSettings()
	pDestroyWindow.Call(a.hwnd)
}

// ----------------------------------------------------------------- dialogs

func encodeFilter(s string) []uint16 { return utf16.Encode([]rune(s + "\x00")) }

func (a *app) openFiles(title, filter string, multi bool) []string {
	buf := make([]uint16, 65536)
	f := encodeFilter(filter)
	ofn := openFileName{
		hwndOwner:   a.hwnd,
		lpstrFilter: &f[0],
		lpstrFile:   &buf[0],
		nMaxFile:    uint32(len(buf)),
		lpstrTitle:  u16(title),
		flags:       0x80000 | 0x1000 | 0x800 | 0x4 | 0x8, // EXPLORER|FILEMUSTEXIST|PATHMUSTEXIST|HIDEREADONLY|NOCHANGEDIR
	}
	if multi {
		ofn.flags |= 0x200 // ALLOWMULTISELECT
	}
	ofn.lStructSize = uint32(unsafe.Sizeof(ofn))
	if r, _, _ := pGetOpenFileNameW.Call(uintptr(unsafe.Pointer(&ofn))); r == 0 {
		return nil
	}
	// Result: "C:\full\path\0" or "C:\dir\0name1\0name2\0\0".
	var parts []string
	start := 0
	for i, c := range buf {
		if c == 0 {
			if i == start {
				break
			}
			parts = append(parts, string(utf16.Decode(buf[start:i])))
			start = i + 1
		}
	}
	if len(parts) <= 1 {
		return parts
	}
	var files []string
	for _, n := range parts[1:] {
		files = append(files, filepath.Join(parts[0], n))
	}
	return files
}

var browseCallback = syscall.NewCallback(func(hwnd, m, lParam, data uintptr) uintptr {
	if m == 1 && data != 0 { // BFFM_INITIALIZED: preselect the current folder
		sendMessage(hwnd, wmUser+103, 1, data) // BFFM_SETSELECTIONW
	}
	return 0
})

func (a *app) browseFolder(title, initial string) string {
	display := make([]uint16, 260)
	bi := browseInfo{
		hwndOwner:      a.hwnd,
		pszDisplayName: &display[0],
		lpszTitle:      u16(title),
		ulFlags:        0x1 | 0x40, // RETURNONLYFSDIRS | NEWDIALOGSTYLE
		lpfn:           browseCallback,
	}
	var init *uint16
	if initial != "" {
		if st, err := os.Stat(initial); err == nil && st.IsDir() {
			init = u16(initial)
			bi.lParam = uintptr(unsafe.Pointer(init))
		}
	}
	pidl, _, _ := pSHBrowseForFolderW.Call(uintptr(unsafe.Pointer(&bi)))
	runtime.KeepAlive(init)
	if pidl == 0 {
		return ""
	}
	defer pCoTaskMemFree.Call(pidl)
	path := make([]uint16, 32768)
	if r, _, _ := pSHGetPathFromIDListW.Call(pidl, uintptr(unsafe.Pointer(&path[0]))); r == 0 {
		return ""
	}
	return syscall.UTF16ToString(path)
}

// ----------------------------------------------------------------- helpers

func (a *app) files() []string {
	n := int(sendMessage(a.list, lbGetCount, 0, 0))
	out := make([]string, 0, n)
	for i := 0; i < n; i++ {
		l := sendMessage(a.list, lbGetTextLen, uintptr(i), 0)
		buf := make([]uint16, l+1)
		sendMessage(a.list, lbGetText, uintptr(i), uintptr(unsafe.Pointer(&buf[0])))
		out = append(out, syscall.UTF16ToString(buf))
	}
	return out
}

func (a *app) addPaths(paths []string) {
	existing := map[string]bool{}
	for _, f := range a.files() {
		existing[strings.ToLower(f)] = true
	}
	for _, p := range paths {
		var found []string
		if st, err := os.Stat(p); err == nil && st.IsDir() {
			filepath.Walk(p, func(f string, info os.FileInfo, err error) error {
				if err == nil && !info.IsDir() && strings.EqualFold(filepath.Ext(f), ".blend") {
					found = append(found, f)
				}
				return nil
			})
			sort.Strings(found)
		} else if err == nil && strings.EqualFold(filepath.Ext(p), ".blend") {
			found = []string{p}
		}
		for _, f := range found {
			full, err := filepath.Abs(f)
			if err != nil || existing[strings.ToLower(full)] {
				continue
			}
			existing[strings.ToLower(full)] = true
			sendMessage(a.list, lbAddString, 0, ptr(u16(full)))
			if w := a.measure(full) + a.s(8); w > a.listExtent {
				a.listExtent = w
				sendMessage(a.list, lbSetHorizExtent, uintptr(w), 0)
			}
		}
	}
	a.updateButtons()
}

func (a *app) removeSelected() {
	if a.busy {
		return
	}
	n := sendMessage(a.list, lbGetSelCount, 0, 0)
	if int32(n) <= 0 {
		return
	}
	sel := make([]int32, n)
	sendMessage(a.list, lbGetSelItems, n, uintptr(unsafe.Pointer(&sel[0])))
	sort.Slice(sel, func(i, j int) bool { return sel[i] > sel[j] })
	for _, i := range sel {
		sendMessage(a.list, lbDeleteString, uintptr(i), 0)
	}
	a.updateButtons()
}

func (a *app) logLine(text string) {
	text = strings.ReplaceAll(strings.ReplaceAll(text, "\r\n", "\n"), "\n", "\r\n") + "\r\n"
	n, _, _ := pGetWindowTextLengthW.Call(a.log)
	sendMessage(a.log, emSetSel, n, n)
	sendMessage(a.log, emReplaceSel, 0, ptr(u16(text)))
}

func setBar(bar uintptr, f float64) {
	if f < 0 {
		f = 0
	} else if f > 1 {
		f = 1
	}
	v := uintptr(f * 1000)
	// Setting one step past the value first skips Windows' slow fill animation.
	sendMessage(bar, pbmSetPos, v+1, 0)
	sendMessage(bar, pbmSetPos, v, 0)
}

func (a *app) updateButtons() {
	idle := !a.busy
	for _, h := range []uintptr{a.btnAddFiles, a.btnAddFolder, a.btnRemove, a.btnClear, a.edBlender,
		a.btnBrowseBlender, a.btnDetect, a.edUnityFolder, a.chkNextToSource, a.chkTextures,
		a.chkAnimation, a.chkModifiers, a.chkTransform, a.chkIncludeBlend} {
		enable(h, idle)
	}
	hasFiles := sendMessage(a.list, lbGetCount, 0, 0) > 0
	enable(a.btnConvert, idle && hasFiles)
	enable(a.btnCancel, a.busy)
	outOn := idle && !isChecked(a.chkNextToSource)
	enable(a.edOutput, outOn)
	enable(a.btnBrowseOutput, outOn)
	enable(a.btnOpenOutput, a.lastOutputDir != "")
}

func (a *app) options() Options {
	o := DefaultOptions()
	if f := strings.TrimSpace(getText(a.edUnityFolder)); f != "" {
		o.UnityFolder = f
	}
	o.IncludeTextures = isChecked(a.chkTextures)
	o.IncludeAnimation = isChecked(a.chkAnimation)
	o.ApplyModifiers = isChecked(a.chkModifiers)
	o.ApplyTransform = isChecked(a.chkTransform)
	o.IncludeBlend = isChecked(a.chkIncludeBlend)
	return o
}

// --------------------------------------------------------------- conversion

func (a *app) start() {
	if a.busy {
		return
	}
	files := a.files()
	if len(files) == 0 {
		return
	}
	blender := strings.Trim(strings.TrimSpace(getText(a.edBlender)), `"`)
	if st, err := os.Stat(blender); blender == "" || err != nil || st.IsDir() {
		messageBox(a.hwnd, "Please set the location of blender.exe (use Detect or Browse…).", appName, mbIconError)
		return
	}
	outputDir := ""
	if !isChecked(a.chkNextToSource) {
		outputDir = strings.Trim(strings.TrimSpace(getText(a.edOutput)), `"`)
		if outputDir == "" {
			messageBox(a.hwnd, "Please choose an output folder.", appName, mbIconError)
			return
		}
		if err := os.MkdirAll(outputDir, 0o755); err != nil {
			messageBox(a.hwnd, "Cannot create the output folder:\n"+err.Error(), appName, mbIconError)
			return
		}
	}
	a.saveSettings()
	opts := a.options()
	ctx, cancel := context.WithCancel(context.Background())
	a.cancel = cancel
	a.busy = true
	setBar(a.barFile, 0)
	setBar(a.barOverall, 0)
	a.updateButtons()
	go a.work(ctx, files, outputDir, blender, opts)
}

func (a *app) work(ctx context.Context, files []string, outputDir, blender string, o Options) {
	total := len(files)
	ok, failed := 0, 0
	version := BlenderVersion(blender)
	if version == "" {
		version = "Blender"
	}
	a.post(func() { a.logLine(fmt.Sprintf("Using %s (%s)", version, blender)) })

	var lastUI time.Time
	for i, f := range files {
		if ctx.Err() != nil {
			break
		}
		i, name := i, filepath.Base(f)
		a.post(func() { a.logLine(fmt.Sprintf("[%d/%d] Converting %s", i+1, total, name)) })
		progress := func(frac float64, msg string) {
			// Throttle the many small updates while packaging big files.
			if frac < 1 && frac > BlenderShare && time.Since(lastUI) < 40*time.Millisecond {
				return
			}
			lastUI = time.Now()
			a.post(func() {
				setBar(a.barFile, frac)
				setBar(a.barOverall, (float64(i)+frac)/float64(total))
				setText(a.lblFile, name+": "+msg)
				setText(a.lblOverall, fmt.Sprintf("File %d of %d", i+1, total))
			})
		}
		out, err := Convert(ctx, f, outputDir, blender, o, progress, func(m string) { a.post(func() { a.logLine(m) }) })
		switch {
		case err == ErrCancelled:
			a.post(func() { a.logLine("Cancelled.") })
		case err != nil:
			failed++
			a.post(func() { a.logLine(fmt.Sprintf("FAILED %s: %v", name, err)) })
		default:
			ok++
			dir := filepath.Dir(out)
			a.post(func() { a.lastOutputDir = dir })
		}
	}
	cancelled := ctx.Err() != nil
	a.post(func() { a.finished(ok, failed, total, cancelled) })
}

func (a *app) finished(ok, failed, total int, cancelled bool) {
	summary := fmt.Sprintf("%d of %d converted", ok, total)
	if failed > 0 {
		summary += fmt.Sprintf(", %d failed", failed)
	}
	if cancelled {
		summary += " (cancelled)"
		setText(a.lblFile, "Cancelled")
	} else {
		setText(a.lblFile, "Finished")
		setBar(a.barOverall, 1)
	}
	setText(a.lblOverall, summary)
	a.logLine(summary)
	a.busy = false
	a.cancel = nil
	a.updateButtons()
	if a.closeWhenDone {
		a.saveSettings()
		pDestroyWindow.Call(a.hwnd)
		return
	}
	if failed > 0 && !cancelled {
		messageBox(a.hwnd, summary+".\nSee the log for details.", appName, mbIconWarning)
	} else if !cancelled {
		pMessageBeep.Call(mbIconInfo)
	}
}

// ---------------------------------------------------------------- settings

func (a *app) loadSettings() {
	s := LoadSettings()
	blender := s.Get("blender", "")
	if st, err := os.Stat(blender); blender == "" || err != nil || st.IsDir() {
		if found := FindBlender(); found != "" {
			blender = found
		}
	}
	setText(a.edBlender, blender)
	home, _ := os.UserHomeDir()
	setText(a.edOutput, s.Get("output", filepath.Join(home, "Documents", "UnityPackages")))
	setChecked(a.chkNextToSource, s.GetBool("next_to_source", false))
	setText(a.edUnityFolder, s.Get("unity_folder", "Assets/{name}"))
	setChecked(a.chkTextures, s.GetBool("include_textures", true))
	setChecked(a.chkAnimation, s.GetBool("include_animation", true))
	setChecked(a.chkModifiers, s.GetBool("apply_modifiers", true))
	setChecked(a.chkTransform, s.GetBool("apply_transform", true))
	setChecked(a.chkIncludeBlend, s.GetBool("include_blend", false))
}

func (a *app) saveSettings() {
	s := Settings{}
	s.Set("blender", strings.TrimSpace(getText(a.edBlender)))
	s.Set("output", strings.TrimSpace(getText(a.edOutput)))
	s.SetBool("next_to_source", isChecked(a.chkNextToSource))
	s.Set("unity_folder", strings.TrimSpace(getText(a.edUnityFolder)))
	s.SetBool("include_textures", isChecked(a.chkTextures))
	s.SetBool("include_animation", isChecked(a.chkAnimation))
	s.SetBool("apply_modifiers", isChecked(a.chkModifiers))
	s.SetBool("apply_transform", isChecked(a.chkTransform))
	s.SetBool("include_blend", isChecked(a.chkIncludeBlend))
	s.Save()
}
