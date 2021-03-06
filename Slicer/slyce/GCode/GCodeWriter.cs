﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using ClipperLib;
using Path = System.Collections.Generic.List<ClipperLib.IntPoint>;
using Paths = System.Collections.Generic.List<System.Collections.Generic.List<ClipperLib.IntPoint>>;


using Slicer.slyce.GCode.Commands;
using Slicer.slyce.Constructs;


namespace Slicer.slyce.GCode
{
    public class CommandCollection : List<GCodeBase>
    { }

    class GCodeWriter
    {
        // GCode reference: https://reprap.org/wiki/G-code
    
        private CommandCollection instructions;
        private double layer_height;
        private double nozzle_diam;
        private double filament_diam;
        private bool has_adh;
        private bool first_start;

        private static readonly Comment header_comment   = new Comment() { Text = "Generated with Pancakes - © 2019 Lieven Libberecht, William Thenaers" };
        private static readonly Comment startup_comment  = new Comment() { Text = "\nSetup" };
        private static readonly Comment print_comment    = new Comment() { Text = "\nPrint" };
        private static readonly Comment teardown_comment = new Comment() { Text = "\nTeardown" };

        private static readonly CommandCollection startup_sequence = new CommandCollection()
        {
            startup_comment,
            new SetBedTemperature() { Temperature = 50 },
            new SetExtruderTemperature() { Temperature = 200 },
            new GetExtruderTemperature(),
            new SetBedTemperatureAndWait() { MinTemperature = 50 },
            new SetExtruderTemperatureAndWait() { MinTemperature = 200 },

            new SetExtruderToAbsolute(),
            new SetUnitsToMillimeters(),

            new SetPrintingAcceleration()  { AccX = 500.0m, AccY = 500.0m, AccZ = 100.0m, AccE = 5000.0m,},
            new SetMaximumFeedrate()       { FeedX = 500.0m, FeedY = 500.0m, FeedZ = 10.0m, FeedE = 50.0m,},
            new SetDefaultAcceleration()   { Printing = 500.0m, Retracting = 1000.0m, Travel = 500.0m },
            new SetAdvancedSettings()      { MaxXJerk = 8.0m, MaxYJerk = 8.0m, MaxZJerk = 0.4m, MaxEJerk = 5.0m },
            new SetSpeedFactorOverride()   { Percentage = 100.0m },
            new SetExtrudeFactorOverride() { Percentage = 100.0m },

            new MoveToOrigin(),

            // test_line_sequence
        };

        private static readonly CommandCollection test_line_sequence = new CommandCollection()
        {
            new SetPosition() { Extrude = 0 },
            new LinearMove()  { MoveZ = 2.0m, Feedrate=3000},
            new LinearMove()  { MoveX = 10.1m, MoveY =  20.0m, MoveZ = 0.28m, Feedrate=5000 },
            new LinearMove()  { MoveX = 10.1m, MoveY = 200.0m, MoveZ = 0.28m, Feedrate=1500, Extrude = 15},
            new LinearMove()  { MoveX = 10.4m, MoveY = 200.0m, MoveZ = 0.28m, Feedrate=5000 },
            new LinearMove()  { MoveX = 10.4m, MoveY =  20.0m, MoveZ = 0.28m, Feedrate=1500, Extrude = 30 },

            new SetPosition() { Extrude = 0 },
            new LinearMove()  { MoveZ = 2.0m, Feedrate=3000},

            new SetPosition() { Extrude = 0 },
            new SetPosition() { Extrude = 0 },
            new LinearMove()  { Feedrate=2700, Extrude = -5 },

            new FanOff(),
            new LinearMove()  { Feedrate=1500, Extrude = 0 },
        };

        private static readonly CommandCollection teardown_sequence = new CommandCollection()
        {
            teardown_comment,
            new SetBedTemperature() { Temperature = 0 },
            new FanOff(),
            new SetSpeedFactorOverride() { Percentage = 100 },
            new SetExtrudeFactorOverride() { Percentage = 100 },

            new SetRelativePositioning(),
            new LinearMove() { Feedrate=1800, Extrude = -3 },
            new LinearMove() { MoveZ = 20, Feedrate=3000 },
            
            new SetAbsolutePositioning(),
            new LinearMove() { MoveX = 0, MoveY = 235, Feedrate=1000 },

            new FanOff(),
            new StopIdleHold(),
            new SetExtruderToAbsolute(),
            new SetExtruderTemperature() { Temperature = 0 },
        };

        public GCodeWriter(double layer_height, double nozzle_diam, double filament_diam, bool has_adh = false)
        {
            this.instructions  = new CommandCollection();
            this.layer_height  = layer_height;
            this.nozzle_diam   = nozzle_diam;
            this.filament_diam = filament_diam;
            this.has_adh       = has_adh;
        }

        public void Reset()
        {
            this.instructions.Clear();
        }

        public void Setup()
        {
            this.instructions.AddRange(GCodeWriter.startup_sequence);
        }

        public void Add(GCodeBase cmd)
        {
            this.instructions.Add(cmd);
        }

        private void MoveNozzleUp(double current_z)
        {
            // Move nozzle back up a little
            this.Add(new SetPosition() { Extrude = 0 });
            this.Add(new LinearMove()  { MoveZ = (decimal)(current_z + this.layer_height), Feedrate = 3000 });
            this.Add(new SetPosition() { Extrude = 0 });  // Required to reset extrusion accumulator!
            this.Add(new LinearMove()  { Feedrate = 2400, Extrude = -5 });
        }

        private void MoveToNextStart(Point start_point, double start_z)
        {
            if (this.first_start)
            {
                first_start = false;

                this.Add(new LinearMove()
                {
                    Extrude = 0,
                    Feedrate = 2700,
                });

                // Move to XYZ
                this.Add(new RapidLinearMove()
                {
                    MoveX = (decimal)start_point.X,
                    MoveY = (decimal)start_point.Y,
                    MoveZ = (decimal)(start_z - 0.02),  // TODO ?? go a bit lower or not?
                    Feedrate = 6000
                });

                // Set feed
                this.Add(new LinearMove()
                {
                    Feedrate = 1200,    // TODO At wich rate?  changed from 1200
                });
            }
            else
            {
                //this.Add(new LinearMove()
                //{
                //    Extrude = 0,
                //    Feedrate = 2700,
                //});

                // Move to XYZ
                this.Add(new RapidLinearMove()
                {
                    MoveX = (decimal)start_point.X,
                    MoveY = (decimal)start_point.Y,
                    Feedrate = 6000
                });

                // Set feed
                this.Add(new LinearMove()
                {
                    Feedrate = 1200,    // TODO At wich rate?  changed from 1200
                });
            }
        }

        private decimal AddPolys(List<Polygon2D> polygons, double zheight, decimal accumulated_extrusion = 0.0m)
        {
            foreach (var p in polygons)
            {
                if (p.Lines.First == null)
                    continue;

                // Move to position of first point
                this.MoveToNextStart(p.FirstPoint(), zheight);

                // TODO possibly optimize for circles
                // NOTE Arc support for printer MUST be enabled first...
                //// Test if arc
                //var maybe_circle = p.ResemblesCircle();
                //if (maybe_circle.Item1)
                //{
                //    var rad = maybe_circle.Item2;
                //    var circumference = Math.PI * 2.0 * rad;

                //    accumulated_extrusion += (decimal)((this.layer_height * this.nozzle_diam * circumference)
                //                                        / this.filament_diam);

                //    var relative_x = p.Center.X - p.FirstPoint().X;
                //    var relative_y = p.Center.Y - p.FirstPoint().Y;

                //    // Move and extrude
                //    this.Add(new ControlledArcMoveClockwise()
                //    {
                //        MoveX = (decimal)p.FirstPoint().X,
                //        MoveY = (decimal)p.FirstPoint().Y,
                //        CenterX = (decimal)relative_x,
                //        CenterY = (decimal)relative_y,
                //        Extrude = accumulated_extrusion
                //    });
                //}
                //else
                {
                    // Extrude from first and follow next points
                    for (LinkedListNode<Line> it = p.Lines.First; it != null; it = it.Next)
                    {
                        var next_point = it.Value.EndPoint;

                        // Get extrusion for prev line segment (next_point == EndPoint of prev)
                        accumulated_extrusion += (decimal)((this.layer_height * this.nozzle_diam * it.Value.GetLength())
                                                            / this.filament_diam);

                        // Move and extrude
                        this.Add(new LinearMove()
                        {
                            MoveX = (decimal)next_point.X,
                            MoveY = (decimal)next_point.Y,
                            Extrude = accumulated_extrusion
                        });
                    }
                }
            }

            return accumulated_extrusion;
        }

        private void AddSlice(Slice s)
        {
            this.first_start = true;
            decimal accumulated_extrusion = 0.0m;

            accumulated_extrusion = this.AddPolys(s.Polygons, s.ZHeight, accumulated_extrusion);
            accumulated_extrusion = this.AddPolys(s.FillPolygons, s.ZHeight, accumulated_extrusion);
            
            // Move nozzle back up a little to clear current layer and reset extrusion
            this.MoveNozzleUp(s.ZHeight);
        }

        public void AddAllSlices(List<Slice> slices)
        {
            string layer_fmt = "\nLAYER {0} of " + slices.Count;
            var layer = 0;
            var it = slices.GetEnumerator();

            // Go to first
            if (it.MoveNext())
            {
                // If we would support a brim, it should be added here, or within the first layer.

                // Start layer 0
                this.Add(new Comment() { Text = String.Format(layer_fmt, layer++) });

                // Fan on low for first layer or off?
                this.Add(new FanOff());  
                //this.Add(new FanOn() { FanSpeed = 85 });

                this.AddSlice(it.Current);
                this.Add(new FanOn() { FanSpeed = 255 });

                // Start next layers
                while (it.MoveNext())
                {
                    this.Add(new Comment() { Text = String.Format(layer_fmt, layer++) });
                    this.AddSlice(it.Current);
                }
            }
        }

        public void ExportToFile(Uri path, bool insert_setup = true, bool append_teardown = true)
        {
            this.ExportToFile(path.ToString(), insert_setup, append_teardown);
        }

        public void ExportToFile(string path, bool insert_setup=true, bool append_teardown=true)
        {
            var fout = new StreamWriter(path);

            fout.WriteLine(GCodeWriter.header_comment.ToString());
            
            if (insert_setup)
            {
                GCodeWriter.startup_sequence.ForEach(x => {
                    fout.WriteLine(x.ToString());
                });

                if (!this.has_adh)
                {
                    GCodeWriter.test_line_sequence.ForEach(x => {
                        fout.WriteLine(x.ToString());
                    });
                }
            }

            fout.WriteLine(GCodeWriter.print_comment.ToString());
            this.instructions.ForEach(x => {
                fout.WriteLine(x.ToString());
            });

            if (append_teardown)
            {
                GCodeWriter.teardown_sequence.ForEach(x => {
                    fout.WriteLine(x.ToString());
                });
            }

            fout.Flush();
            fout.Close();
        }
    }
}
