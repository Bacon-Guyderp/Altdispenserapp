"""``python -m n64studio [rom]`` launches the GUI; ``--cli`` runs a headless export."""
import os
import sys


def _attach_console() -> None:
    """A windowed (no-console) .exe has no stdout; borrow the parent console."""
    if sys.stdout is not None and sys.stderr is not None:
        return
    stream = None
    if os.name == "nt":
        try:
            import ctypes
            if ctypes.windll.kernel32.AttachConsole(-1):
                stream = open("CONOUT$", "w", encoding="utf-8", errors="replace")
        except (AttributeError, OSError):
            stream = None
    if stream is None:
        stream = open(os.devnull, "w", encoding="utf-8")
    sys.stdout = sys.stdout or stream
    sys.stderr = sys.stderr or stream


def main() -> None:
    if len(sys.argv) > 1 and sys.argv[1] == "--cli":
        _attach_console()
        from .cli import main as cli_main
        cli_main(sys.argv[2:])
    else:
        from .gui.app import main as gui_main
        gui_main(sys.argv[1:])


if __name__ == "__main__":
    main()
