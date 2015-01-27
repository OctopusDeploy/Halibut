using System;
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
}