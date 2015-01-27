using System;
using System.Windows.Forms;

namespace Halibut.OctopusSample
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(Execute.MainForm = new OctopusForm());
        }
    }
}
