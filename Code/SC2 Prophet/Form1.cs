using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Drawing.Imaging;
using Tesseract;
using System.Text.RegularExpressions;

namespace SC2_Prophet
{
    public partial class Form1 : Form
    {
        private Label[] commanderLabels;
        private CheckBox[] commanderCheckboxes;

        private const int COMMANDER_COUNT = 18;
        private Color COLOR_LOWEST_COMMANDER = Color.Blue;
        private Color COLOR_CHOSEN_COMMANDER = Color.Red;

        private string LOG_PATH = "log.txt";

        public Form1()
        {
            InitializeComponent();

            // Get common component references in handy array
            commanderCheckboxes = new CheckBox[] { cbCommander1, cbCommander2, cbCommander3, cbCommander4, cbCommander5, cbCommander6, cbCommander7, cbCommander8, cbCommander9, cbCommander10, cbCommander11, cbCommander12, cbCommander13, cbCommander14, cbCommander15, cbCommander16, cbCommander17, cbCommander18 };
            commanderLabels = new Label[] { lCommanderLevel1, lCommanderLevel2, lCommanderLevel3, lCommanderLevel4, lCommanderLevel5, lCommanderLevel6, lCommanderLevel7, lCommanderLevel8, lCommanderLevel9, lCommanderLevel10, lCommanderLevel11, lCommanderLevel12, lCommanderLevel13, lCommanderLevel14, lCommanderLevel15, lCommanderLevel16, lCommanderLevel17, lCommanderLevel18};

            // Fill screen dropdown and select first screen/display
            int displayCount = 0;
            foreach (Screen s in Screen.AllScreens)
            {
                cbDisplay.Items.Add(displayCount.ToString() + " :" + s.Bounds.Width + "x" + s.Bounds.Height);
                displayCount++;
            }
            cbDisplay.SelectedIndex = 0;

            // Other
            cbOCRMode.SelectedIndex = 2;
            if (!Directory.Exists("data"))
            {
                Directory.CreateDirectory("data");
            }

            // Read settings
            ReadSettings();
            ScanOCRModels();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Log("*** START OF RUN ***");

            Log("Parameters:");
            Log("RESX: " + tbResX.Text + " RESY: " + tbResY.Text + 
                " | RX: " + nudResXCoef.Value + " RY: " + nudResYCoef.Value +
                " | DIFFX: " + nudDiffXCoef.Value + " DIFFY: " + nudDiffYCoef.Value +
                " | CROPX: " + nudCropRecXCoef.Value + " CROPY: " + nudCropRecYCoef.Value +
                " | CDEF: " + nudColorToleranceDefault.Value + " CSEL: " + nudColorToleranceSelected.Value);

            // Reset styles
            for (int i = 0; i < COMMANDER_COUNT; i++)
            {
                commanderCheckboxes[i].ForeColor = Color.Black;
                commanderCheckboxes[i].Font = new Font(commanderCheckboxes[i].Font, FontStyle.Regular);

                commanderLabels[i].ForeColor = Color.Black;
                commanderLabels[i].Text = "0";
                commanderLabels[i].Font = new Font(commanderLabels[i].Font, FontStyle.Regular);

            }

            Screen screenToUse = Screen.AllScreens[cbDisplay.SelectedIndex];
            Log("Selected screen bounds: " + screenToUse.Bounds);
            Rectangle bounds = screenToUse.Bounds;


            using (Bitmap bitmap2 = new Bitmap(bounds.Width, bounds.Height))
            {
                using (Graphics g = Graphics.FromImage(bitmap2))
                {
                    g.CopyFromScreen(screenToUse.Bounds.X, screenToUse.Bounds.Y, 0, 0, bounds.Size);
                }
                bitmap2.Save("data/screen.png", System.Drawing.Imaging.ImageFormat.Png);

                Bitmap bitmap = bitmap2;

                int RES_X, RES_Y;

                try
                {
                    RES_X = int.Parse(tbResX.Text);
                    RES_Y = int.Parse(tbResY.Text);
                } catch (Exception)
                {
                    MessageBox.Show("Could not parse resolution!");
                    return;
                }
               
                


                //int X1 = (int)(RES_X * 0.0561848955); // 145; // First row, first commander X - from the left
                //int Y1 = (int)(RES_Y * 0.2827546288);// 409; // First row, first commander Y - from the top

                // Rocno popravljeni koeficienti:
                int X1 = (int)(RES_X * nudResXCoef.Value); // 145; // First row, first commander X - from the left
                int Y1 = (int)(RES_Y * nudResYCoef.Value);// 409; // First row, first commander Y - from the top

                //X1 = 145; Y1 = 410; 

                // 1440p
                // TOCNE KOORDINATE: +5,5: 148, 412
                // VISINA CRKE: 13
                // SIRINA CRKE: 15


                int X_DIFF = (int)(RES_X * nudDiffXCoef.Value); // 96; // Difference to the next commander, moving right
                int Y_DIFF = (int)(RES_Y * nudDiffYCoef.Value); // 137; // Difference to the next commander, moving down (next row)

                //int X_DIFF = 96; // Difference to the next commander, moving right
                //int Y_DIFF = 137; // Difference to the next commander, moving down (next row)

                //const int RECT_SIZE_X = 40; // Size of rectangle to check pixels X
                //const int RECT_SIZE_Y = 23; // Size of rectangle to check pixels Y
                int RECT_SIZE_X = (int) Math.Ceiling(RES_X * nudCropRecXCoef.Value); //45; // Size of rectangle to check pixels X
                int RECT_SIZE_Y = (int) Math.Ceiling(RES_Y * nudCropRecYCoef.Value); //17; // Size of rectangle to check pixels Y

                Log("X1: " + X1 + " Y1: " + Y1 + 
                    " | X_DIFF: " + X_DIFF + " Y_DIFF: " + Y_DIFF + 
                    " | RECT_SIZE_X: " + RECT_SIZE_X + " RECT_SIZE_Y: " + RECT_SIZE_Y);


                int x1 = X1;
                int y1 = Y1;
                int x2 = x1 + RECT_SIZE_X;
                int y2 = y1 + RECT_SIZE_Y;

                int sizeX = x2 - x1;
                int sizeY = y2 - y1;

                // 2nd commander level position: 241 409; 2nd row y: 546
                // Difference:  96, 137

                Bitmap levelsImage = null;
                Bitmap levelsImageOriginal = null;

                int offset1 = (int) nudColorToleranceDefault.Value; //45 dela OK ce je border dobro nastavljen, za 1080p 
                int offset2 = (int) nudColorToleranceSelected.Value;

                for (int commander = 0; commander < COMMANDER_COUNT; commander++)
                {

                    Rectangle numberRec = new Rectangle(new Point(x1, y1), new Size(sizeX, sizeY));
                    Bitmap numberImage = CropImage(bitmap, numberRec);
                    Bitmap numberImageOriginal = (Bitmap)numberImage.Clone();

                    for (int i = 0; i < numberImage.Width; i++)
                    {
                        for (int j = 0; j < numberImage.Height; j++)
                        {
                            
                            //if (numberImage.GetPixel(i,j) == Color.FromArgb(93, 190, 235))
                            Color pColor = numberImage.GetPixel(i, j);
                            if ((pColor.R < 93 + offset1 && pColor.R > 93-offset1 &&
                                pColor.G < 190 + offset1 && pColor.G > 190 - offset1 &&
                                pColor.B < 235 + offset1 && pColor.B > 235 - offset1) ||
                                (pColor.R < 154 + offset2 && pColor.R > 154 - offset2 &&
                                pColor.G < 216 + offset2 && pColor.G > 216 - offset2 &&
                                pColor.B < 255 + offset2 && pColor.B > 255 - offset2))
                            {
                                numberImage.SetPixel(i, j, Color.Black);
                            } else
                            {
                                numberImage.SetPixel(i, j, Color.White);
                            }
                        }
                    }

                    if (levelsImage == null)
                    {
                        levelsImage = numberImage;
                        levelsImageOriginal = numberImageOriginal;
                    }
                    else
                    {
                        levelsImage = AppendBitmapVertical(levelsImage, numberImage, 0);
                        levelsImageOriginal = AppendBitmapVertical(levelsImageOriginal, numberImageOriginal, 0);
                    }

                    if (commander == 9)
                    {
                        // Go to next row
                        x1 = X1;
                        x2 = x1 + RECT_SIZE_X;
                        y1 = Y1 + Y_DIFF;
                        y2 = y1 + RECT_SIZE_Y;

                    }
                    else
                    {
                        // Move right
                        x1 += X_DIFF;
                        x2 += X_DIFF;
                    }


                }  

                bitmap.Dispose();

                levelsImage.Save("data/levelsImage.png", System.Drawing.Imaging.ImageFormat.Png);
                levelsImageOriginal.Save("data/levelsImageOriginal.png", System.Drawing.Imaging.ImageFormat.Png);

                pbProcessedImage.Image = levelsImage;
                pbOriginalImage.Image = levelsImageOriginal;


                string tessPath = Path.Combine("tessdata");
                string result = "";

                int[] commanderLevels = new int[COMMANDER_COUNT];

                using (var engine = new TesseractEngine(tessPath, cbOCRMode.Text, EngineMode.Default))
                {
                    using (var img = Pix.LoadFromFile("data/levelsImage.png"))
                    {
                        engine.DefaultPageSegMode = PageSegMode.SingleBlock;
                        var page = engine.Process(img);
                        result = page.GetText();

                        string[] rows = result.Split('\n');
                        for (int i = 0; i < Math.Min(COMMANDER_COUNT, rows.Length); i++)
                        {
                            string row = Regex.Replace(rows[i], "[^0-9]", "");
                            commanderLabels[i].Text = row;
                            int.TryParse(row, out commanderLevels[i]);
                        }

                        // This below is just for console output
                        string cleanedLevels = Regex.Replace(result, "[^0-9]", "");

                        string formatedLevels = "";
                        foreach (char c in cleanedLevels)
                        {
                            formatedLevels += c + " ";
                        }

                        Log("OCRMode: " + cbOCRMode.Text);
                        Log("OCR result: " + result);
                        Log("Cleaned OCR result: " + cleanedLevels);
                        Log("Formatted OCR result: " + formatedLevels);
                    }
                }

                int lowestLevel = 15;
                // Find lowest level
                for (int i = 0; i < commanderLevels.Length; i++)
                {
                    if (commanderCheckboxes[i].Checked && commanderLevels[i] < lowestLevel) lowestLevel = commanderLevels[i];
                }
                // Find commanders with lowest level
                List<int> lowestCommanders = new List<int>();
                for (int i = 0; i < commanderLevels.Length; i++)
                {
                    if (commanderCheckboxes[i].Checked && commanderLevels[i] == lowestLevel)
                    {
                        lowestCommanders.Add(i);
                        commanderCheckboxes[i].ForeColor = COLOR_LOWEST_COMMANDER;
                        commanderCheckboxes[i].Font = new Font(commanderCheckboxes[i].Font, FontStyle.Bold);
                        commanderLabels[i].ForeColor = COLOR_LOWEST_COMMANDER;
                        commanderLabels[i].Font = new Font(commanderLabels[i].Font, FontStyle.Bold);
                    }
                }
                // Get random lowest level commander
                if (lowestCommanders.Count > 0)
                {
                    Random rand = new Random();
                    int chosenCommander = rand.Next(lowestCommanders.Count);
                    commanderLabels[lowestCommanders[chosenCommander]].Text += "  <---  CHOSEN COMMANDER";
                    commanderLabels[lowestCommanders[chosenCommander]].ForeColor = COLOR_CHOSEN_COMMANDER;
                    commanderCheckboxes[lowestCommanders[chosenCommander]].ForeColor = COLOR_CHOSEN_COMMANDER;
                }

                Log("*** END OF RUN ***");

            }

        }

        public Bitmap CropImage(Bitmap source, Rectangle section)
        {
            var bitmap = new Bitmap(section.Width, section.Height);
            using (var g = Graphics.FromImage(bitmap))
            {
                g.DrawImage(source, 0, 0, section, GraphicsUnit.Pixel);
                return bitmap;
            }
        }

        public Bitmap AppendBitmap(Bitmap source, Bitmap target, int spacing)
        {
            int w = source.Width + target.Width;
            int h = source.Height;
            Bitmap bmp = new Bitmap(w, h);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.DrawImage(source, 0, 0);
                g.DrawImage(target, source.Width, 0);
            }

            return bmp;
        }

        public Bitmap AppendBitmapVertical(Bitmap source, Bitmap target, int spacing)
        {
            int w = source.Width;
            int h = source.Height + target.Height;
            Bitmap bmp = new Bitmap(w, h);

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.DrawImage(source, 0, 0);
                g.DrawImage(target, 0, source.Height);
            }

            return bmp;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveSettings();
        }

        private void SaveSettings()
        {
            // Save settings
            try
            {
                StreamWriter sw = new StreamWriter("data/settings.txt");
                foreach (CheckBox cb in commanderCheckboxes)
                {
                    sw.WriteLine(cb.Checked ? "1" : "0");
                }
                sw.WriteLine("resx " + tbResX.Text);
                sw.WriteLine("resy " + tbResY.Text);
                sw.WriteLine("display " + cbDisplay.SelectedIndex);
                sw.Close();
            }
            catch (Exception e)
            {
                Log("Error writing settings.txt file: " + e.Message);
            }

            
        }

        private void ReadSettings()
        {
            // Read settings
            try
            {
                StreamReader sr = new StreamReader("data/settings.txt");
                string line = sr.ReadToEnd();
                string[] lines = line.Replace("\r", "").Split('\n');
                int i = 0;
                for (i = 0; i < COMMANDER_COUNT; i++)
                {
                    lines[i] = Regex.Replace(lines[i], "[^01]", "");
                    commanderCheckboxes[i].Checked = lines[i] == "1" ? true : false;
                }
                while (i < lines.Length) {
                    if (lines[i].StartsWith("resx"))
                    {
                        tbResX.Text = lines[i].Split(' ')[1];
                    }
                    if (lines[i].StartsWith("resy"))
                    {
                        tbResY.Text = lines[i].Split(' ')[1];
                    }
                    if (lines[i].StartsWith("display"))
                    {
                        int index = 0;
                        int.TryParse(lines[i].Split(' ')[1], out index);
                        if (index <= cbDisplay.Items.Count)
                        {
                            cbDisplay.SelectedIndex = index;
                        }
                    }
                    i++;
                }
                sr.Close();
                
            } catch (Exception e)
            {
                Log("Error reading settings.txt file: " + e.Message);
            }
            
        }

        private void ScanOCRModels()
        {
            try
            {
                cbOCRMode.Items.Clear();
                string[] fileEntries = Directory.GetFiles("tessdata");
                foreach (string fileName in fileEntries)
                {
                    string[] split = fileName.Split('.');
                    if (split.Length == 2)
                    {
                        if (split[1] == "traineddata")
                        {
                            split[0] = split[0].Replace("tessdata\\", "");
                            cbOCRMode.Items.Add(split[0]);
                            if (split[0].Contains("SC2Nums_legit"))
                            {
                                cbOCRMode.SelectedIndex = cbOCRMode.Items.Count - 1;
                            }
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Log("Error reading OCR models: " + e.Message);
            }
            
                
        }

        private void Log(string message)
        {
            if (!cbLog.Checked) return;

            StreamWriter sw = null;
            if (!File.Exists(LOG_PATH))
            {
                sw = File.CreateText(LOG_PATH);
            } 
            else
            {
                sw = File.AppendText(LOG_PATH);
            }

            if (sw != null)
            {
                DateTime now = DateTime.Now;
                sw.WriteLine("[" + now.ToShortDateString() + " " + now.ToLongTimeString() + "]\t" + message);
                sw.Close();
            }

        }
    }
}
