using System;
#if NETFRAMEWORK
using System.Windows.Forms;
#endif

namespace Halibut.OctopusSample
{
    static class Program
    {
#if NETFRAMEWORK
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(Execute.MainForm = new OctopusForm());
        }
#endif
#if NETCOREAPP2_2
		static void Main()
		{
			Console.WriteLine("This sample application is only supported on environments that have the .NET framework installed");
		}
#endif
    }
}
