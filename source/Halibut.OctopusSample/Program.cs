using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace Halibut.OctopusSample
{
    public static class Execute
    {
        public static Form MainForm;

        public static void Background(Action callback)
        {
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    callback();
                }
                catch (Exception ex)
                {
                    Foreground(delegate
                    {
                        throw new Exception("Error on background: " + ex.Message, ex);
                    });
                }
            });
        }

        public static void Foreground(Action callback)
        {
            if (MainForm.InvokeRequired)
            {
                MainForm.Invoke(callback);
            }
            else
            {
                callback();
            }
        }
    }

    public interface IHealthCheckService
    {
        bool IsOnline();
    }

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
