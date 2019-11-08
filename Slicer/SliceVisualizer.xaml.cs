﻿using Slicer.slyce.Constructs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Slicer
{
    /// <summary>
    /// Interaction logic for SliceVisualizer.xaml
    /// </summary>
    public partial class SliceVisualizer : Window
    {
        public static SliceVisualizer sliceVisualizer;
        private double scale;
        private double min;
        private double max;
        public SliceVisualizer(Vertex[] vertices, double stroke, double min, double max)
        {
            InitializeComponent();
            Update(vertices, stroke, min, max);
        }

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            ClearValue(SizeToContentProperty);
            LayoutRoot.ClearValue(WidthProperty);
            LayoutRoot.ClearValue(HeightProperty);
        }

        public void Update(Vertex[] vertices, double stroke, double min, double max)
        {
            var width = canvasGrid.Width;
            var height = canvasGrid.Height;
            var comp = width;
            if(height < comp)
            {
                comp = height;
            }
            this.min = min;
            this.max = max;
            scale = comp / (max - min);
            canvasGrid.Children.Clear();
            for (int i = 0; i < vertices.Count() - 1; i+=2)
            {
                var line = new Line();
                line.Stroke = System.Windows.Media.Brushes.Black;
                line.X1 = (vertices[i].Pos.X - min) * scale;
                line.X2 = (vertices[i + 1].Pos.X - min) * scale;
                line.Y1 = (vertices[i].Pos.Y - min) * scale;
                line.Y2 = (vertices[i + 1].Pos.Y - min) * scale;
                line.HorizontalAlignment = HorizontalAlignment.Left;
                line.VerticalAlignment = VerticalAlignment.Top;
                line.StrokeThickness = stroke * scale;
                canvasGrid.Children.Add(line);
            }
        }
    }
}
