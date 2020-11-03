using System;
#if NETFRAMEWORK
using System.Windows.Forms;
#endif

namespace Halibut.OctopusSample
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
#if NETFRAMEWORK
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(Execute.MainForm = new OctopusForm());
#else 
            Console.WriteLine("This sample application is only supported on environments that have the .NET framework installed");
#endif
        }
    }
}
