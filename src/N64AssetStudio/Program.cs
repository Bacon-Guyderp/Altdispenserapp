using System.Runtime.InteropServices;
using N64AssetStudio.Core;

namespace N64AssetStudio;

static class Program
{
    [DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);

    [STAThread]
    static int Main(string[] args)
    {
        if (args.Length > 0 && args[0] == "--cli")
        {
            // Windowed exe: borrow the console of the shell that launched us.
            AttachConsole(-1);
            var stdout = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
            var stderr = new StreamWriter(Console.OpenStandardError()) { AutoFlush = true };
            try { return Cli.Run(args.Skip(1).ToArray(), stdout, stderr); }
            catch (Exception e) { stderr.WriteLine(e); return 1; }
        }
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        Application.ThreadException += (_, e) => MessageBox.Show(e.Exception.ToString(), "N64 Asset Studio - unexpected error");
        if (args.Length >= 3 && args[0] == "--selftest")
        {
            AttachConsole(-1);
            var form = new MainForm(null);
            int result = 1;
            form.Shown += async (_, _) =>
            {
                try { result = await form.SelfTest(args[1], args[2]); }
                catch (Exception e) { File.WriteAllText(Path.Combine(args[2], "selftest_error.txt"), e.ToString()); result = 1; }
                File.WriteAllText(Path.Combine(args[2], "selftest_result.txt"), result == 0 ? "OK" : $"FAILED ({result} errors)");
                form.Close();
            };
            Application.Run(form);
            return result == 0 ? 0 : 1;
        }
        Application.Run(new MainForm(args.FirstOrDefault()));
        return 0;
    }
}
