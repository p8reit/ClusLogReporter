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
using System.Diagnostics;
using System.Net;

namespace ClusterlogRepoter
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private static string sddcPath = "SDDCDataPath";
        
        private void button1_Click(object sender, EventArgs e)
        {
            folderBrowserDialog1.ShowDialog();

            sddcPath = folderBrowserDialog1.SelectedPath;
            textBox1.Text = sddcPath;

            try

            { 

                var filePaths = Directory.EnumerateFiles(sddcPath,
                                                     "*cluster.log",
                                                     SearchOption.AllDirectories);


                    richTextBox1.Text = "Cluster.log(s) found at following locations:\n";
                    foreach (string filePath in filePaths)


                    {
                        // Display file path.
                        richTextBox1.AppendText("\n" + filePath);

                        /*
                        * Removing this code which i wrote for copying all the cluster logs found at different location to one location before we start processing.  
                        The expectation was to have all cluster logs in same folder and in SDDC Diag, we see them all in the root of the HealthTest Folder.
                        Now we will create another folder with name "Processed Data" and write all the information inside that to make sure the info in not clubbed with the data already present in that folder.
                        */

                    //Moving file to the root path selected as SDDC Path.


                    /*

                    string fileName = fileName = Path.GetFileName(filePath);

                        string sourcePath = filePath;
                        string destPath = Path.Combine(sddcPath, fileName);

                        richTextBox3.AppendText("Copying file "  + fileName + " to SDDC path " + sddcPath + " for further processing.\n");

                        CopyFileWithProgress(sourcePath, destPath);
                        */

                }

                
               // richTextBox3.AppendText("\n\nFinished copying Cluster.log files to selected SDDC Path.");

            
                richTextBox3.AppendText("******Click Start Processing to process the Cluster.log file(s)******");
                richTextBox3.AppendText("\n\n\"Start Processing\" processes the log(s) with -SR -SG switches\n" +
                                         "SR - Skip Resources\n" +
                                         "SG - Skip Groups\n");

                richTextBox3.AppendText("\n\"Start Processing Verbose\" processes the log(s) without -SR -SG switches");

            }

            catch (IndexOutOfRangeException i)
            {
                richTextBox1.Text = "Path not seleted" + i.HResult;

            }

            catch (ArgumentException a)
            {
                richTextBox1.Text = "Invalid path. Please select a valid HealthTest folder path. " + a.Message;
               
            }


            catch (UnauthorizedAccessException UAEx)
            {
                richTextBox1.AppendText(UAEx.Message);
            }
            catch (PathTooLongException PathEx)
            {
                richTextBox1.AppendText(PathEx.Message);
            }

           

            // Show the dialog and get result.
            //DialogResult result = folderBrowserDialog1.ShowDialog();

        }

        private void textBox1_TextChanged(object sender, 
                                            EventArgs e)
        {

        }

        private void richTextBox1_TextChanged(object sender, 
                                                EventArgs e)
        {

        }
        
        private void button2_Click(object sender, 
                                     EventArgs e)
        {


            string clusterLogPath = sddcPath;

            if (clusterLogPath == "SDDCDataPath")

            {

                richTextBox3.Text = "Please select SDDC HealthTest Folder before clicking Start Processing.";

            }

            else if (clusterLogPath == null)

                    {
                        richTextBox3.Text = "Please select SDDC HealthTest Folder before clicking Start Processing.";
                    }

            else
            {
                try
                {


                    richTextBox2.Text = "Processing all Cluster.log file(s) at location " + clusterLogPath + ".\n\n";
                    richTextBox2.Text += "Processing the log(s) with -SR -SG switches\n" +
                                            "SR - Skip Resources\n" +
                                            "SG - Skip Groups\n\n";
                    Process process = new Process();

                    process.StartInfo.WorkingDirectory = "\\";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.FileName = "ClusLogReporter.exe";
                    process.StartInfo.Arguments = clusterLogPath + " -sg -sr";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardInput = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    richTextBox2.AppendText(output);




                }

                catch (FileNotFoundException g)

                {

                    richTextBox2.Text = g.Message;

                }

            }

        }

        private void button2_Click_1(object sender, EventArgs e)
        {
            string clusterLogPath = sddcPath;

            if (clusterLogPath == "SDDCDataPath")

            {

                richTextBox3.Text = "Please select SDDC HealthTest Folder before clicking Start Processing Verbose.";

            }

            else
            {
                try
                {

                    richTextBox2.AppendText("Processing all Cluster.log file(s) at location " + clusterLogPath + ".\n\n");
                    Process process = new Process();
                    process.StartInfo.WorkingDirectory = "\\";
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.FileName = "ClusLogReporter.exe";
                    richTextBox2.AppendText("Processing the log(s) in verbose mode");
                    process.StartInfo.Arguments = clusterLogPath;
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.RedirectStandardInput = true;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.StartInfo.RedirectStandardError = true;
                    process.Start();
                    string output = process.StandardOutput.ReadToEnd();
                    process.WaitForExit();
                    richTextBox2.AppendText(output);

                }

                catch (FileNotFoundException g)

                {

                    richTextBox2.Text = g.Message;

                }

            }

        }


        private void richTextBox2_TextChanged(object sender, EventArgs e)
        {

        }


        //We dont need this code as it was written to copy all the cluster logs to one location befor we start processing.

            /*

        public delegate void IntDelegate(int Int);

        public static event IntDelegate FileCopyProgress;
        public static void CopyFileWithProgress(string source, string destination)
        {
            var webClient = new System.Net.WebClient();
            webClient.DownloadProgressChanged += DownloadProgress;
            webClient.DownloadFileAsync(new Uri(source), destination);
        }

        private static void DownloadProgress(object sender, DownloadProgressChangedEventArgs e)
        {
            if (FileCopyProgress != null)
                FileCopyProgress(e.ProgressPercentage);
        }

    */

        private void richTextBox3_TextChanged(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void Form1_Load(object sender, EventArgs e)
        {

        }

        private void textBox2_TextChanged(object sender, EventArgs e)
        {

        }
    }
}
