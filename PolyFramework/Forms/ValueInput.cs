using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PolyFramework
{
    public partial class ValueInput : Form
    {
        public double resValue { get; set; }

        public ValueInput()
        {
            InitializeComponent();
        }

        private void ValueInput_Load(object sender, EventArgs e)
        {
            textBox1.Text = resValue.ToString();
        }

        private void textBox1_TextChanged(object sender, EventArgs e)
        {
            if (System.Text.RegularExpressions.Regex.IsMatch(textBox1.Text, "  ^ [0-9]"))
            {
                textBox1.Text = "";
            }
        }

        private void textBox1_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && (e.KeyChar != '.'))
            {
                e.Handled = true;
            }


        }

        private void textBox1_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter || e.KeyCode == Keys.Space)
            {
                if (double.TryParse(textBox1.Text, out double result))
                {
                    this.resValue = result;
                    this.DialogResult = DialogResult.OK;
                    this.Hide();
                }
                else
                {
                    textBox1.Text = "";
                }
                
                
                
            }
            if (e.KeyCode == Keys.Escape)
            {
                this.DialogResult = DialogResult.Abort;
                this.Hide();
            }
        }
    }
}
