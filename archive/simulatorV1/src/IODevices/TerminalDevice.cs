using SCPU.Simulator.Core;
using System.Text;
using Terminal.Gui;

namespace SCPU.Simulator.ConsoleUI.IODevices
{
    /*
        Address 0x2901 : 7-bit ASCII value of the next character to be entered into the terminal
        Address 0x2902 : 7-bit ASCII code for the leftmost character in the buffer
        Address 0x2903 : 1-bit when the keyboard buffer contains at least one character
     */
    public class TerminalDevice : IODevice
    {
        private TextView console;
        private Queue<char> buffer = new Queue<char>();

        public override ushort this[byte address]
        {
            get
            {
                // Address 0x2902 : 7-bit ASCII code for the leftmost character in the buffer
                if ((address & 3) == 2)
                {
                    return (ushort)(buffer.Count == 0 ? 0 : buffer.Dequeue());
                }
                // Address 0x2903 : 1-bit when the keyboard buffer contains at least one character
                else if ((address & 3) == 3)
                {
                    return (ushort)(buffer.Count == 0 ? 0 : 1);
                }
                else
                {
                    return 0;
                }
            }
            set
            {
                // Address 0x2901 : 7-bit ASCII value of the next character to be entered into the terminal
                if ((address & 3) == 1)
                {
                    console.Text += Encoding.ASCII.GetString(new byte[1] { (byte)value });
                    console.CursorPosition = new System.Drawing.Point(0, console.Lines + 2);
                }
            }
        }

        public TerminalDevice(TextView console)
        {
            this.console = console;
            Application.KeyDown += Application_KeyDown;
        }

        private void Application_KeyDown(object? sender, Key e)
        {
            if (e.IsValid)
            {
                if (e.IsKeyCodeAtoZ)
                {
                    buffer.Enqueue(e.ToString()[0]);
                }
                else if (e.KeyCode == KeyCode.Enter)
                {
                    buffer.Enqueue((char)10);
                }
                else if (e.KeyCode == KeyCode.Space)
                {
                    buffer.Enqueue((char)e.KeyCode);
                }
            }
        }

        public override void Reset()
        {
            buffer.Clear();
            console.SelectAll();
            console.DeleteAll();
        }
    }
}
