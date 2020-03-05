using System;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using Image = System.Windows.Controls.Image;
using System.IO;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Text;

namespace ImageSlicer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        class Label
        {
            public int Status { get; set; }
            public int LabelNum { get; set; }
            public Pic Value { get; set; }
            public Label(int num, int status, Pic value)
            {
                LabelNum = num;
                Status = status;
                Value = value;
            }
        }

        class Pic
        {
            public int lowX { get; set; }
            public int highX { get; set; }
            public int lowY { get; set; }
            public int highY { get; set; }
            public Pic(int lX, int hX, int lY, int hY)
            {
                lowX = lX;
                highX = hX;
                lowY = lY;
                highY = hY;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
        }

        private void ImageDrop(object sender, System.Windows.DragEventArgs e)
        {
            string fileName = ((System.Windows.DataObject)e.Data).GetFileDropList().Cast<string>().First();
            Image showUpImage = sender as Image;
            if (showUpImage != null)
            {
                showUpImage.Source = new BitmapImage(new Uri(fileName, UriKind.Absolute));
                Text.Visibility = Visibility.Hidden;
                SliceButton.IsEnabled = true;
            }
        }


        private void SliceButton_Click(object sender, RoutedEventArgs e)
        {
            Bitmap bitmap = new Bitmap(new Uri(Image.Source.ToString()).LocalPath);
            List<HashSet<int>> lookUp = new List<HashSet<int>>();
            List<Label> results = new List<Label>();
            Label[] current = new Label[bitmap.Width];
            Label[] last = new Label[bitmap.Width];

            //merge a label with a value
            Label Merge(Label label, Pic value)
            {
                label.Value = new Pic
                    (Math.Min(label.Value.lowX, value.lowX),
                    Math.Max(label.Value.highX, value.highX),
                    Math.Min(label.Value.lowY, value.lowY),
                    Math.Max(label.Value.highY, value.highY));
                return label;
            }

            //create link between two label
            Label Merge2Label(Label label1, Label label2)
            {
                if (label1 != label2)
                {
                    lookUp[label1.LabelNum].Add(label2.LabelNum);
                    lookUp[label2.LabelNum].Add(label1.LabelNum);
                }
                return label1;
            }

            //merge a label with its relative labels
            Label SuperMerge(Label label)
            {
                label.Status = 0;
                if (lookUp[label.LabelNum].Count == 0)
                    return label;

                foreach (var num in lookUp[label.LabelNum])
                {
                    if (results[num].Status == 0)
                        continue;
                    Merge(label, SuperMerge(results[num]).Value);
                }
                return label;
            }

            //check target pixel
            bool isTargetPixel(Color pixel)
            {
                // target condition
                if (pixel.A != 0)
                    return true;
                return false;
            }

            using (var dialog = new FolderBrowserDialog())
            {
                DialogResult result = dialog.ShowDialog();
                if (result == System.Windows.Forms.DialogResult.OK)
                {
                    // -- Check first row of pixels

                    if (isTargetPixel(bitmap.GetPixel(0, 0))) //check first pixel of row
                    {
                        lookUp.Add(new HashSet<int>());
                        results.Add(new Label(results.Count, 1, new Pic(0, 0, 0, 0)));
                        last[0] = results.Last();
                    }

                    for (int j = 0; j < bitmap.Width - 1; j++) //check the rest pixels of row
                    {
                        if (!isTargetPixel(bitmap.GetPixel(j, 0)) && isTargetPixel(bitmap.GetPixel(j + 1, 0)))
                        {
                            lookUp.Add(new HashSet<int>());
                            results.Add(new Label(results.Count, 1, new Pic(j + 1, j + 1, 0, 0)));
                            last[j + 1] = results.Last();
                        }
                        else if (isTargetPixel(bitmap.GetPixel(j, 0)) && isTargetPixel(bitmap.GetPixel(j + 1, 0)))
                            last[j + 1] = Merge(last[j], new Pic(j + 1, j + 1, 0, 0));
                    }

                    // -- Check rows in middle

                    for (int i = 1; i < bitmap.Height; i++)
                    {
                        if (isTargetPixel(bitmap.GetPixel(0, i))) //check first pixel of row
                        {
                            if (isTargetPixel(bitmap.GetPixel(0, i - 1))) //check pixel above
                                current[0] = Merge(last[0], new Pic(0, 0, i, i));
                            else if (isTargetPixel(bitmap.GetPixel(1, i - 1))) //check pixel above on right
                                current[0] = Merge(last[1], new Pic(0, 0, i, i));
                            else // if arent any pixel above
                            {
                                lookUp.Add(new HashSet<int>());
                                results.Add(new Label(results.Count, 1, new Pic(0, 0, i, i)));
                                current[0] = results.Last();
                            }
                        }
                        for (int j = 0; j < bitmap.Width - 2; j++) //check the rest pixels of row
                        {
                            //check firest appearance of non-transparent pixel on row
                            if (!isTargetPixel(bitmap.GetPixel(j, i)) && isTargetPixel(bitmap.GetPixel(j + 1, i)))
                            {
                                if (isTargetPixel(bitmap.GetPixel(j + 1, i - 1))) //check pixel above
                                    current[j + 1] = Merge(last[j + 1], new Pic(j + 1, j + 1, i, i));
                                else
                                {
                                    bool check = true;
                                    if (isTargetPixel(bitmap.GetPixel(j, i - 1))) //check pixel above on left
                                    {
                                        current[j + 1] = Merge(last[j], new Pic(j + 1, j + 1, i, i));
                                        check = false;
                                        if (isTargetPixel(bitmap.GetPixel(j + 2, i - 1))) //check pixel above on right
                                            current[j + 1] = Merge2Label(current[j + 1], last[j + 2]);
                                    }
                                    else if (isTargetPixel(bitmap.GetPixel(j + 2, i - 1))) //check pixel above on right
                                    {
                                        current[j + 1] = Merge(last[j + 2], new Pic(j + 1, j + 1, i, i));
                                        check = false;
                                    }
                                    if (check) // if arent any pixel above
                                    {
                                        lookUp.Add(new HashSet<int>());
                                        results.Add(new Label(results.Count, 1, new Pic(j + 1, j + 1, i, i)));
                                        current[j + 1] = results.Last();
                                    }
                                }
                            }
                            // check continuity of non-transparent pixel
                            else if (isTargetPixel(bitmap.GetPixel(j, i)) && isTargetPixel(bitmap.GetPixel(j + 1, i)))
                            {
                                if (isTargetPixel(bitmap.GetPixel(j + 1, i - 1))) //check pixel above
                                    current[j + 1] = Merge2Label(Merge(current[j], new Pic(j + 1, j + 1, i, i)), last[j + 1]);
                                else
                                {
                                    bool check = true;
                                    if (isTargetPixel(bitmap.GetPixel(j + 2, i - 1))) //check pixel above on right
                                    {
                                        current[j + 1] = Merge2Label(Merge(current[j], new Pic(j + 1, j + 1, i, i)), last[j + 2]);
                                        check = false;
                                    }
                                    if (isTargetPixel(bitmap.GetPixel(j, i - 1))) //check pixel above on left
                                    {
                                        current[j + 1] = Merge2Label(Merge(last[j], new Pic(j + 1, j + 1, i, i)), current[j]);
                                        check = false;
                                    }
                                    if (check) // check if arent any pixel above
                                        current[j + 1] = Merge(current[j], new Pic(j + 1, j + 1, i, i));
                                }
                            }
                        }
                        // -- Check last row
                        int lastRow = bitmap.Width - 2;
                        if (!isTargetPixel(bitmap.GetPixel(lastRow, i)) && isTargetPixel(bitmap.GetPixel(lastRow + 1, i)))
                        {
                            if (isTargetPixel(bitmap.GetPixel(lastRow + 1, i - 1)))
                                current[lastRow + 1] = Merge(last[lastRow + 1], new Pic(lastRow + 1, lastRow + 1, i, i));
                            else if (isTargetPixel(bitmap.GetPixel(lastRow, i - 1)))
                                current[lastRow + 1] = Merge(last[lastRow], new Pic(lastRow + 1, lastRow + 1, i, i));
                            else
                            {
                                lookUp.Add(new HashSet<int>());
                                results.Add(new Label(results.Count, 1, new Pic(lastRow + 1, lastRow + 1, i, i)));
                                current[lastRow + 1] = results.Last();
                            }
                        }
                        else if (isTargetPixel(bitmap.GetPixel(lastRow, i)) && isTargetPixel(bitmap.GetPixel(lastRow + 1, i)))
                        {
                            if (isTargetPixel(bitmap.GetPixel(lastRow + 1, i - 1)))
                                current[lastRow + 1] = Merge2Label(Merge(current[lastRow], new Pic(lastRow + 1, lastRow + 1, i, i)), last[lastRow + 1]);
                            else if (isTargetPixel(bitmap.GetPixel(lastRow, i - 1)))
                                current[lastRow + 1] = Merge2Label(Merge(current[lastRow], new Pic(lastRow + 1, lastRow + 1, i, i)), last[lastRow]);
                            else
                                current[lastRow + 1] = Merge(current[lastRow], new Pic(lastRow + 1, lastRow + 1, i, i));
                        }

                        //assign current to last and clear current
                        for (int j = 0; j < current.Length; j++)
                            if (current[j] != null)
                                last[j] = current[j];
                        current = new Label[bitmap.Width];
                    }

                    //link all relative label
                    List<Label> finalResult = new List<Label>();
                    for (int i = 0; i < results.Count; i++)
                    {
                        if (results[i].Status == 1)
                            finalResult.Add(SuperMerge(results[i]));
                    }

                    //export image
                    int k = 0;
                    foreach (var img in finalResult)
                        using (Bitmap expImg = bitmap.Clone(new Rectangle(img.Value.lowX,
                                                    img.Value.lowY,
                                                    img.Value.highX - img.Value.lowX + 1,
                                                    img.Value.highY - img.Value.lowY + 1),
                                     bitmap.PixelFormat))
                        {
                            // Image size condition
                            //if (expImg.Width < 19 || expImg.Width > 70 || expImg.Height < 19 || expImg.Height > 70)
                            //    continue;
                            expImg.Save($"{dialog.SelectedPath}\\{k}.png");
                            k++;
                        }

                    System.Windows.MessageBox.Show("Slice successfully!");
                }
            }
        }
    }
}
