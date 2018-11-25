using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using AForge.Video.DirectShow;
using openalprnet;
using AForge.Video;
/*using Ozeki.Media;
using Ozeki.Camera;*/

namespace PlakaTanima
{
    public partial class Form1 : Form
    {

        private FilterInfoCollection webcam;
        private AForge.Video.DirectShow.VideoCaptureDevice cam;

/*
        private IIPCamera _camera;
        private DrawingImageProvider _imageProvider = new DrawingImageProvider();
        private MediaConnector _connector = new MediaConnector();
        private VideoViewerWF _videoViewerWF1;*/



        public Form1()
        {
            InitializeComponent();
            try
            {
                //donanımsal kamera için
                webcam = new FilterInfoCollection(FilterCategory.VideoInputDevice);//webcam dizisine mevcut kameraları dolduruyoruz.
                foreach (FilterInfo videocapturedevice in webcam)
                {
                    comboBox1.Items.Add(videocapturedevice.Name);//kameraları combobox a dolduruyoruz.
                }
                comboBox1.SelectedIndex = 0;


                //rtsp için
               /* _videoViewerWF1 = new VideoViewerWF();
                _videoViewerWF1.Name = "videoViewerWF1";
                _videoViewerWF1.Size = pictureBox2.Size;
                pictureBox2.Controls.Add(_videoViewerWF1);

                // Bind the camera image to the UI control
                _videoViewerWF1.SetImageProvider(_imageProvider);*/

            }
            catch (Exception e)
            {

            }
            //cam.DesiredFrameSize = new Size(pictureBox2.Width, pictureBox2.Height);
        }

        public static string AssemblyDirectory
        {
            get
            {
                var codeBase = Assembly.GetExecutingAssembly().CodeBase;
                var uri = new UriBuilder(codeBase);
                var path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        public System.Drawing.Rectangle boundingRectangle(List<Point> points)
        {

            var minX = points.Min(p => p.X);
            var minY = points.Min(p => p.Y);
            var maxX = points.Max(p => p.X);
            var maxY = points.Max(p => p.Y);

            return new System.Drawing.Rectangle(new Point(minX, minY), new Size(maxX - minX, maxY - minY));
        }

        private static Image cropImage(Image img, System.Drawing.Rectangle cropArea)
        {
            var bmpImage = new Bitmap(img);
            return bmpImage.Clone(cropArea, bmpImage.PixelFormat);
        }

        public static Bitmap combineImages(List<Image> images)
        {
            //read all images into memory
            Bitmap finalImage = null;

            try
            {
                var width = 0;
                var height = 0;

                foreach (var bmp in images)
                {
                    width += bmp.Width;
                    height = bmp.Height > height ? bmp.Height : height;
                }

                finalImage = new Bitmap(width, height);

                using (var g = Graphics.FromImage(finalImage))
                {
                    g.Clear(Color.Black);


                    var offset = 0;
                    foreach (Bitmap image in images)
                    {
                        g.DrawImage(image,
                                    new System.Drawing.Rectangle(offset, 0, image.Width, image.Height));
                        offset += image.Width;
                    }
                }

                return finalImage;
            }
            catch (Exception ex)
            {
                if (finalImage != null)
                    finalImage.Dispose();

                throw ex;
            }
            finally
            {
                foreach (var image in images)
                {
                    image.Dispose();
                }
            }
        }

        private void processImageFile(string fileName)
        {
            resetControls();
            var region = rbUSA.Checked ? "us" : "eu";
            String config_file = Path.Combine(AssemblyDirectory, "openalpr.conf");
            String runtime_data_dir = Path.Combine(AssemblyDirectory, "runtime_data");
            using (var alpr = new AlprNet(region, config_file, runtime_data_dir))
            {
                if (!alpr.IsLoaded())
                {
                    lbxPlates.Items.Add("Error initializing OpenALPR");
                    return;
                }
                picOriginal.ImageLocation = fileName;
                picOriginal.Load();

                var results = alpr.Recognize(fileName);

                var images = new List<Image>(results.Plates.Count());
                var i = 1;
                foreach (var result in results.Plates)
                {
                    var rect = boundingRectangle(result.PlatePoints);
                    var img = Image.FromFile(fileName);
                    var cropped = cropImage(img, rect);
                    images.Add(cropped);

                    lbxPlates.Items.Add("\t\t-- Plaka #" + i++ + " --");
                    List<string> plateOran = new List<string>();
                    foreach (var plate in result.TopNPlates)
                    {
                        
                        plateOran.Add(string.Format(@"{0} {1}% {2}",
                                                          plate.Characters.PadRight(12),
                                                          plate.OverallConfidence.ToString("N1").PadLeft(8),
                                                          plate.MatchesTemplate.ToString().PadLeft(8)));
                        
                    }
                    lbxPlates.Items.Add(plateOran[0].ToString());

                }

                if (images.Any())
                {
                    picLicensePlate.Image = combineImages(images);
                }
            }
        }

        private void resetControls()
        {
            picOriginal.Image = null;
            picLicensePlate.Image = null;
            lbxPlates.Items.Clear();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            resetControls();
            
        }

        private void btnDetect_Click(object sender, EventArgs e)
        {
            String date = DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss");
            picOriginal.Image = pictureBox2.Image;
            String fileName = "images/" + date + ".jpg";
            pictureBox2.Image.Save(fileName);

            if (openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                processImageFile(openFileDialog.FileName);
            }
            //processImageFile();
        }

        private void rbUSA_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog.ShowDialog(this) == DialogResult.OK)
            {
                processImageFile(openFileDialog.FileName);
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            cam = new AForge.Video.DirectShow.VideoCaptureDevice(webcam[comboBox1.SelectedIndex].MonikerString);
            cam.NewFrame += new AForge.Video.NewFrameEventHandler(cam_NewFrame);
            cam.Start();
        }

        void cam_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            Bitmap bit = (Bitmap)eventArgs.Frame.Clone();//kısaca bu eventta kameradan alınan görüntüyü picturebox a atıyoruz.
            pictureBox2.Image = bit;
        }

        private void button3_Click(object sender, EventArgs e)
        {
            string ip = textBox1.Text.ToString();
            MJPEGStream videoSource = new MJPEGStream(ip);
            videoSource.NewFrame += new NewFrameEventHandler(cam_NewFrame);
            videoSource.Start();
          
        }

        private void button4_Click(object sender, EventArgs e)
        {
            //rtsp kamera ip ve çalıştırma
            /*_camera = IPCameraFactory.GetCamera("rtsp://192.168.1.7:5540/ch0", "root", "pass");
            _connector.Connect(_camera.VideoChannel, _imageProvider);
            _camera.Start();
            _videoViewerWF1.Start();*/
        }
    }
}