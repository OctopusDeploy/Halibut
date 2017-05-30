using System;
using System.Windows.Forms;

namespace Halibut.OctopusSample
{
    public class Logger
    {
        readonly TextBox text;

        public Logger(TextBox text)
        {
            this.text = text;
        }

        public void WriteLine(string message)
        {
            text.AppendText(message);
            text.AppendText(Environment.NewLine);
        }
    }
}