namespace PrintSharp.WinForms.Demo;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if (args.Contains("--smoke-test"))
        {
            SmokeTest.Run(args.Length > 1 ? args[1] : Path.Combine(AppContext.BaseDirectory, "SmokeOutput"));
            return;
        }
        Application.Run(new MainForm());
    }
}
