﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using cs_ahill_app.Properties;

namespace cs_ahill_app
{
    public partial class Form1 : Form
    {
        Bitmap ahillBmp = Resource1.Ahill;
        Bitmap formBmp;
        Graphics formGraphics;
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

            if (flag == false)
            {
                flag = true;

                this.formBmp = new Bitmap(width, height);
                this.formGraphics = Graphics.FromImage(this.formBmp);

                this.formGraphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.NearestNeighbor;

                this.pictureBox1.Location = new Point(0, 0);
                this.pictureBox1.Size = new Size(width, height);
                this.pictureBox1.Image = this.formBmp;
            }

            int size_x = rand.Next(300) + 100;

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

            formGraphics.DrawImage(ahillBmp, x, y, width * neg, height);
            this.pictureBox1.Refresh();

            this.pictureBox1.Location = new Point(-100 + rand.Next(30), -100 + rand.Next(30));
        }
    }
}
