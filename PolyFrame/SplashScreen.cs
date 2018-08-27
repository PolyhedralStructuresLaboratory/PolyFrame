using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Reflection;

namespace PolyFrame
{
    public partial class SplashScreen : Form
    {

        Version version = Assembly.GetExecutingAssembly().GetName().Version;
        public SplashScreen()
        {
            InitializeComponent();
            this.VersionText.Text = "Ver. " + version.ToString();
            this.VersionText.BackColor = Color.Transparent;
        }

        public void SplashScreen_Load(object sender, EventArgs e)
        {
            this.timer1.Start();
        }

        public void SplashScreen_Hover(object sender, EventArgs e)
        {
            this.timer1.Stop();
            this.timer2.Stop();
            this.Opacity = 1.0;
        }

        public void SplashScreen_Leave(object sender, EventArgs e)
        {
            this.timer1.Start();
        }

        public void timer1_Tick(object sender, EventArgs e)
        {
            this.timer2.Start();
            this.timer1.Stop();
            
        }

        private void timer2_Tick(object sender, EventArgs e)
        {
            this.Opacity -=.01;
            if (Opacity == .01)
            {
                this.Close();
                Dispose();
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
