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
            using var form = new MainForm(persistPreferences: !smoke);
            bool shown = false;
            Exception? smokeError = null;
            using var timer = new System.Windows.Forms.Timer { Interval = 250 };
            if(smoke)
            {
                form.Shown += (_,_) => { shown = form.Visible && form.IsHandleCreated; timer.Start(); };
                timer.Tick += async (_,_) => {
                    timer.Stop();
                    try {
                        await form.RunUiChecks();
                        int screenshotArg=Array.IndexOf(args,"--screenshot");
                        if(screenshotArg>=0 && screenshotArg+1<args.Length) {
                            using var bitmap=new Bitmap(form.Width,form.Height);
                            form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));
                            bitmap.Save(args[screenshotArg+1],System.Drawing.Imaging.ImageFormat.Png);
                        }
                    } catch(Exception ex) { smokeError=ex; }
                    form.Close();
                };
            }
            Application.Run(form);
            if(smokeError != null) throw new InvalidOperationException("Windows UI smoke checks failed.",smokeError);
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
