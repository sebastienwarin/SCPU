using SCPU.Simulator.Core;
using Terminal.Gui;

namespace SCPU.Simulator.ConsoleUI.IODevices
{
    /*
        Address 0x2801 : 16-bit value
        Address 0x2802 : 1-bit LED
        Address 0x2803 : 16-bit value
     */
    public class DemoDevice : IODevice
    {
        private CheckBox checkBox;
        private TextField textField1, textField2;

        public override ushort this[byte address]
        {
            get => 0;
            set
            {
                // Address 0x2801 : 16-bit value
                if ((address & 3) == 1)
                {
                    textField1.Text = "0x" + value.ToString("X4");
                }
                // Address 0x2802 : 1-bit LED
                else if ((address & 3) == 2)
                {
                    checkBox.CheckedState = (value & 1) == 1 ? CheckState.Checked : CheckState.UnChecked;
                }
                // Address 0x2803 : 16-bit value
                else if ((address & 3) == 3)
                {
                    textField2.Text = "0x" + value.ToString("X4");
                }
            }
        }

        public DemoDevice(CheckBox checkBox, TextField textField1, TextField textField2)
        {
            this.checkBox = checkBox;
            this.textField1 = textField1;
            this.textField2 = textField2;
        }

        public override void Reset()
        {
            checkBox.CheckedState = CheckState.UnChecked;
            textField1.Text = "0x0";
            textField2.Text = "0x0";
        }
    }
}
