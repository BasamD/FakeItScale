namespace ScaleSim.Desktop;
internal static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        bool smoke = args.Contains("--smoke-test", StringComparer.OrdinalIgnoreCase);
        try
        {
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.ThrowException);
            ApplicationConfiguration.Initialize();
            using var form = new MainForm();
            bool shown = false;
            using var timer = new System.Windows.Forms.Timer { Interval = 250 };
            if(smoke)
            {
                form.Shown += (_,_) => { shown = form.Visible && form.IsHandleCreated; timer.Start(); };
                timer.Tick += (_,_) => { timer.Stop(); form.Close(); };
            }
            Application.Run(form);
            if(smoke && !shown) throw new InvalidOperationException("The main window did not become visible.");
            return 0;
        }
        catch(Exception ex)
        {
            string location;
            try
            {
                var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ScaleSim");
                Directory.CreateDirectory(folder);
                location = Path.Combine(folder, "startup-error.log");
                File.AppendAllText(location, $"{DateTimeOffset.Now:O}\n{ex}\n\n");
            }
            catch { location = "The error log could not be written."; }
            if(!smoke)
                MessageBox.Show($"ScaleSim could not start.\n\n{ex.Message}\n\nError details: {location}",
                    "ScaleSim startup error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
