using System.Windows.Forms;

namespace MyMovieDB.Tray;

internal static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        TrayLog.Configure(args);
        TrayLog.Write("Program start");

        Application.ThreadException += (_, e) => TrayLog.Write("UI exception: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => TrayLog.Write("Unhandled exception: " + e.ExceptionObject);

        try
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new TrayAppContext(args));
            TrayLog.Write("Program exit");
        }
        catch (Exception ex)
        {
            TrayLog.Write("Fatal: " + ex);
            MessageBox.Show(ex.ToString(), "MyMovieDB Tray", MessageBoxButtons.OK, MessageBoxIcon.Error);
            throw;
        }
    }
}
