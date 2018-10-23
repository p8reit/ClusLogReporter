﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;


namespace ClusterLogReporter
{
    class Program
    {

        static List<DataTable> _Tables = new List<DataTable>();


        static void Main(string[] args)
        {

            if (processArgs(args))
            {
                
                seekLogsInFolder(args);

            }

        }

        static public DataTable ReadFiletoTbl(string FilePath)
        {

            //List<field> fields = new List<field>();
            //string[] filter = File.ReadAllLines(Current_Filter);

            //int y = 0;//set int to 0 for tracking string length
            //if (filter.Length > 0)//make sure the filter is not empty
            //{
            //    for (int i = 0; i < filter.Length; i++)
            //    {

            //        y = filter[i].Length;//set length for substring
            //        field f = new field();//add new field named f
            //        f.Field = "Trace Data"; // bogus header name - note this is not really being used yet
            //        string[] a = filter[i].ToString().Split(',');
            //        // f.Value = a[1];//sets the switch value for color 
            //        f.Value1 = a[0];//sets the contians value
            //        fields.Add(f);
            //    }
            //}


            DataTable table = new DataTable();
            table.Columns.Add("LineNumber", typeof(string));
            table.Columns.Add("Trace Data", typeof(string));
            try
            {
                if (String.IsNullOrEmpty(FilePath))
                {
                    System.Console.WriteLine("File Path is Null or empty");
                }
                else
                {
                    //string[] lines = File.ReadAllLines(FilePath);
                    using (FileStream fs = File.Open(FilePath, FileMode.Open))
                    using (BufferedStream bs = new BufferedStream(fs))
                    using (StreamReader sr = new StreamReader(bs))
                    {
                        string s;
                        int i = 0;
                        while ((s = sr.ReadLine()) != null)
                        {
                            DataRow dr = table.NewRow();
                            table.Rows.Add(i++, s);
                        }

                        sr.Dispose();
                        bs.Dispose();
                        fs.Dispose();
                        sr.Close();
                        bs.Close();
                        fs.Close();

                    }

                }

                return table;
            }
            catch (Exception)
            {

                throw;
            }


        }

        static void runTrimLogs(string pathToFile, string logsRoot)
        {
            string rootPath;
            string newlogspath = (logsRoot + "\\"  + getCurrentNodeFromLog(pathToFile));
            //mk dir
            //move copy of our tool there
            //process our log in that dir
            //remove tool 
            // getCurrentNodeFromLog(pathToFile);
            try
            {
                if (!Directory.Exists(newlogspath))
                {
                    Directory.CreateDirectory(newlogspath);
                }
                else
                {
                    Directory.CreateDirectory(newlogspath + Random.);
                }
               
                
                File.Copy((logsRoot + "\\Trimlogs.exe"), (newlogspath + "\\Trimlogs.exe"),true);
                ProcessStartInfo proc = new ProcessStartInfo(newlogspath + "\\Trimlogs.exe");
                proc.CreateNoWindow = false;
                proc.UseShellExecute = false;
                proc.WorkingDirectory = newlogspath;

                proc.Arguments = pathToFile;
                Process.Start(proc).WaitForExit();
                
                File.Delete(newlogspath + "\\Trimlogs.exe");
            }
            catch (Exception)
            {

                throw;
            }

            

            

        }

        public static void ExtractSaveResource(string resource, string path)
        {
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            string[] arrResources = currentAssembly.GetManifestResourceNames();
            foreach (string resourceName in arrResources)
                if (resourceName.ToUpper().EndsWith(resource.ToUpper()))
                {
                    Stream resourceToSave = currentAssembly.GetManifestResourceStream(resourceName);
                    var output = File.OpenWrite(path + "\\Trimlogs.exe");
                    resourceToSave.CopyTo(output);
                    resourceToSave.Flush();
                    resourceToSave.Close();
                    output.Flush();
                    output.Close();
                }

        }


        static void seekLogsInFolder(string[] args)
        {

            foreach (string path in args)
            {
                if (File.Exists(path) && path.ToString().Contains("cluster.log"))
                {
                    // This path is a file
                    ExtractSaveResource("Trimlogs.exe", path);
                    _Tables.Add(ReadFiletoTbl(path));
                }
                else if (Directory.Exists(path))
                {
                    // This path is a directory
                    ExtractSaveResource("Trimlogs.exe", path);
                    processPath(path);
                }
                else
                {
                    Console.WriteLine("{0} is not a valid Cluster log file or directory.", path);
                }
            }
        }

        static void processPath(string targetDirectory)
        {

            try
            {
                // Process the list of files found in the directory.
                string[] fileEntries = Directory.GetFiles(targetDirectory, "*cluster.log");
                foreach (string fileName in fileEntries)
                {
                   // _Tables.Add(ReadFiletoTbl(fileName));
                    runTrimLogs(fileName, targetDirectory);
                }
                   

                // Recurse into subdirectories of this directory.
                string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
                foreach (string subdirectory in subdirectoryEntries)
                    processPath(subdirectory);
            }
            catch (System.OutOfMemoryException e)
            {
                Console.Write("Failed to read files out of memory :( : " + e.HResult.ToString());
                //throw;
                return;
            }

        }

        static bool processArgs(string[] args)
        {

            bool isArgs = false;

            // Test if input arguments were supplied:
            if (args.Length == 0)
            {
                System.Console.WriteLine("Please enter a path.");
                System.Console.WriteLine("Usage: Cluslg.exe <pathtologs>");
                System.Console.WriteLine("Example: Cluslg.exe C:\\LogsFolder");
                return isArgs;
            }

            bool test = String.IsNullOrEmpty(args[0]);
            if (test != false)
            {
                System.Console.WriteLine("Please enter a valid path.");
                System.Console.WriteLine("Usage: Cluslg.exe <pathtolog>");
                System.Console.WriteLine("Example: Cluslg.exe C:\\LogsFolder");
                return isArgs;
            }
            else
            {

                return true;

            }

        }

        static string getCurrentNodeFromLog(string pathToFile)
        {
            string[] splitlines = new string[10];
            var lines = File.ReadLines(pathToFile).Take(10).ToArray();
            string[] ret = new string[5];
            foreach (var item in lines)
            {
                if (item.Contains("Current node"))
                {
                    splitlines = new string[item.Count()];
                    splitlines = item.Split('(');
                    ret = splitlines[1].ToString().Split(')');
                    break;

                }
            } 
            
            return ret[0];
        }



    }
}
