using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Data.OleDb;



namespace ClusterLogReporter
{
    class Program
    {
        //globals
        const string _OperationalLog = "\\operational.txt";
        const string _SystemLog = "\\System.txt";
        const string _ClusterLog = "\\cluster.txt";
        static List<DataTable> _ResourceTables = new List<DataTable>();
        static string _logsPath = null;
        static List<Node> _NodeInfo = new List<Node>();
        static int _processedCount = 0;
        static StreamWriter _w = null;
        static bool _showVMs = false;
        static bool _ignoreOfflineResource = true;
        static bool _ignoreUnknownResource = false;
        static Dictionary<string, string> _KnownResources = new Dictionary<string, string>();

        static void Main(string[] args)
        {
            if (processArgs(args))
            {
                //validate the cluster is a v2 log and process it with trimlogs
                //this leaves you will a folder for each node 
                //with CSV and txt files with data to be poked for info
                populateDataFromClusterLogs(args);

                //take the resources csv file from each node and process it to a merged view
                if (_processedCount > 0)
                {
                    getCSVFilesForNode(); //stub

                    //process recources and find owners and states
                    //start processing any resources in a none healthy state

                    using (_w = File.AppendText(_logsPath + "ClusterReportlog.txt"))
                    {
                       // processResources();
                        createSumary();
                    }
                }
            }
        }

        #region FileProcessing

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

        static DataTable getCSVToTbl(string path)
        {

            string FileName = path;
            OleDbConnection conn = new OleDbConnection
               ("Provider=Microsoft.Jet.OleDb.4.0; Data Source = " +
                 Path.GetDirectoryName(FileName) +
                 "; Extended Properties = \"Text;HDR=YES;FMT=Delimited\"");

            conn.Open();

            OleDbDataAdapter adapter = new OleDbDataAdapter
                   ("SELECT * FROM " + Path.GetFileName(FileName), conn);

            DataSet ds = new DataSet("FileData");
            adapter.Fill(ds);

            conn.Close();

            return ds.Tables[0];

        }

        static DataTable getCSVToTbl(string path, string query)
        {

            string FileName = path;
            OleDbConnection conn = new OleDbConnection
               ("Provider=Microsoft.Jet.OleDb.4.0; Data Source = " +
                 Path.GetDirectoryName(FileName) +
                 "; Extended Properties = \"Text;HDR=YES;FMT=Delimited\"");

            conn.Open();

            OleDbDataAdapter adapter = new OleDbDataAdapter
                   (query, conn);

            DataSet ds = new DataSet("FileData");
            adapter.Fill(ds);

            conn.Close();

            return ds.Tables[0];

        }

        static void processPath(string targetDirectory)
        {
            try
            {
                // Process the list of files found in the directory.
                string[] fileEntries = Directory.GetFiles(targetDirectory, "*cluster.log");
                Console.Write("Found " + fileEntries.Count().ToString() + " cluster logs in root folder. \r");

                foreach (string fileName in fileEntries)
                {

                    Console.Write("Started processing file: " + fileName + " \r");
                    //this tranforms the cluster log with trimlogs
                    runTrimLogs(fileName, targetDirectory);
                   // Console.Write("Completed processing file: " + fileName + "\r");

                }
                //// Recurse into subdirectories of this directory.
                //string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
                //foreach (string subdirectory in subdirectoryEntries)
                //    processPath(subdirectory);
            }
            catch (System.OutOfMemoryException e)
            {
                Console.Write("Failed to read files out of memory :( : " + e.HResult.ToString());
                //throw;
                return;
            }

        }

        static string getCurrentNodeFromLog(string pathToFile)
        {

            try
            {
                string[] splitlines = new string[10];
                var lines = File.ReadLines(pathToFile).Take(10).ToArray();
                string[] ret = new string[5];

                if (lines == null || lines.Count() == 0)
                {
                    Console.Write("This file is not a V2 Cluster log: " + pathToFile);
                    return null;
                }
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
            catch(IndexOutOfRangeException i)
            {
                Console.Write("Failed to read node name from log: " + pathToFile + " as its 0 bytes " + i.HResult );
                throw;
            }
            catch (Exception e)
            {
                Console.Write("Failed to read node name from log: " + pathToFile + " with error: " + e.HResult.ToString());
                return null;
            }
        }

        static void getCSVFilesForNode()
        {

            //read each nodes resources file
            for (int i = 0; i < _NodeInfo.Count; i++)
            {
                // Process the list of files found in the directory.
                string[] fileEntries = Directory.GetFiles(_NodeInfo[i].logfilepath, "*.csv");
                Console.Write("Found " + fileEntries.Count().ToString() + " cluster logs in node: " + _NodeInfo[i].name + " folder. \r");

                foreach (var file in fileEntries)
                {

                    switch (Path.GetFileName(file))
                    {
                        case "system.csv":
                            _NodeInfo[i].SystemEvt = getCSVToTbl(file);
                            break;
                        case "resources.csv":
                            string query = "SELECT * FROM " + Path.GetFileName(file) + " WHERE _state NOT IN ('Offline', 'Unkown', 'Online')";
                            _NodeInfo[i].Resources = getCSVToTbl(file);
                            _ResourceTables.Add(_NodeInfo[i].Resources);
                            break;
                        case "nodes.csv":
                            _NodeInfo[i].nodes = getCSVToTbl(file);
                            break;
                        case "resource_types.csv":
                            _NodeInfo[i].resource_types = getCSVToTbl(file);
                            break;
                        //not tracking this file name
                        default:
                            break;
                    }
                }

               // string resourcefile = (_NodeInfo[i].logfilepath + "\\resources.csv");
               // _ResourceTables.Add(getCSVToTbl(resourcefile));
            }
        }


        #endregion

        #region Utils

        public static void ExtractSaveResource(string resource, string path)
        {
            Assembly currentAssembly = Assembly.GetExecutingAssembly();
            string[] arrResources = currentAssembly.GetManifestResourceNames();
            foreach (string resourceName in arrResources)
                if (resourceName.ToUpper().EndsWith(resource.ToUpper()))
                {
                    Stream resourceToSave = currentAssembly.GetManifestResourceStream(resourceName);
                    var output = File.OpenWrite(path + "\\Tool.exe");
                    resourceToSave.CopyTo(output);
                    resourceToSave.Flush();
                    resourceToSave.Close();
                    output.Flush();
                    output.Close();
                }

        }

        static void populateDataFromClusterLogs(string[] args)
        {

            foreach (string path in args)
            {
                _logsPath = path;

                if (File.Exists(path) && path.ToString().Contains("cluster.log"))
                {
                    // This path is a file
                    ExtractSaveResource("Tool.exe", path);
                    // _Tables.Add(ReadFiletoTbl(path));
                    File.Delete(path + "\\Tool.exe");
                }
                else if (Directory.Exists(path))
                {
                    // This path is a directory
                    ExtractSaveResource("Tool.exe", path);
                    processPath(path);
                    File.Delete(path + "\\Tool.exe");
                }
                else
                {
                    Console.WriteLine("{0} is not a valid Cluster log file or directory.", path);
                }
            }

            //cleanup
            File.Delete(_logsPath + "\\Tool.exe");
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

        static void runTrimLogs(string pathToFile, string logsRoot)
        {
            string CurrentNode = getCurrentNodeFromLog(pathToFile);
            if (String.IsNullOrEmpty(CurrentNode))
            {
                return;
            }
            
            string newlogspath = (logsRoot + CurrentNode);
            Node newnode = new Node();
            newnode.name = CurrentNode;
            
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
                    //just for testing
                  
                    newlogspath = (newlogspath + "_" + System.DateTime.Now.ToString("yyyy_MM_dd_HHmmssfff"));
                    Directory.CreateDirectory(newlogspath);
                }

                newnode.logfilepath = newlogspath;
                _NodeInfo.Add(newnode);
                Console.Write("Clusterlog for node " + newnode.name + " being processed and saved to " + newnode.logfilepath + "\r");
                File.Copy((logsRoot + "\\Tool.exe"), (newlogspath + "\\Trimlogs.exe"), true);
                ProcessStartInfo proc = new ProcessStartInfo(newlogspath + "\\Trimlogs.exe");
                proc.CreateNoWindow = false;
                proc.UseShellExecute = false;
                proc.WorkingDirectory = newlogspath;
                proc.Arguments = pathToFile;
                Process.Start(proc).WaitForExit();
                File.Delete(newlogspath + "\\Trimlogs.exe");
                _processedCount++;
            }
            catch (Exception e)
            {
                Console.Write("Failed Processing file: " + pathToFile + " with error: " + e.ToString());
                return;
            }
        }

        static void processResources()
        {

            // Get the DataTable of a DataSet.

            foreach (var node in _NodeInfo)
            {

                DataRow[] rows = node.Resources.Select("_state NOT IN ('Online', 'Unknown', 'Offline')");

                
                
                //// Print the value one column of each DataRow.
                //log("============Resources of Interest by Node============", _w);
                //for (int i = 0; i < rows.Length; i++)
                //{
                    
                //    log(node.name + " | " + rows[i][1].ToString() + " | " + rows[i][2].ToString(), _w);

                //    // File verbose logs for each node for this resource
                //    using (StreamReader r = new StreamReader((node.logfilepath + _OperationalLog)))
                //    {

                //        log("============Resource Verbose Output============", _w);
                //        while (!r.EndOfStream)
                //        {

                //            try
                //            {
                //                if (!r.ReadLine().Contains("SCVMM") && r.ReadLine().Contains(rows[i][1].ToString()))
                //                {
                //                    log(r.ReadLine().ToString(), _w);
                //                }
                //            }
                //            catch (NullReferenceException e)
                //            {

                //                continue;
                //            }
                            
                //        }
                //    }
                    
                //}

                
            }
        }

        static void createSumary()
        {

            log("Logs Processed: " + _NodeInfo.Count.ToString(),_w);
            log("log Paths: \r",_w);
            foreach (var node in _NodeInfo)
            {
                log("       " + node.logfilepath, _w);
            }
            log(" ", _w);
            log("Nodes Found: ", _w);
            foreach (var node in _NodeInfo)
            {
                log("       " + node.name, _w);
            }
            log(" ", _w);
            log("Resources States by Node:", _w);

            //Finds Resources not in the online state  -- TODO: Improve this apporach to interesting resources
            findInterestingResources();

                    

        }

        public static void log(string logMessage, TextWriter w)
        {
            //w.Write("\r\nLog Entry : ");
           // w.WriteLine("{0} {1}", DateTime.Now.ToLongTimeString(),
           //     DateTime.Now.ToLongDateString());
           // w.WriteLine("  :");
            w.WriteLine("{0}", logMessage);
           // w.WriteLine("-------------------------------");
        }

        public static DataTable readEventLog(string searchterm, string filepath)
        {

            //Reads the logfile requested and prpvides a table of lines with searched
            DataTable table = new DataTable();
            table.Columns.Add("LineNumber", typeof(string));
            table.Columns.Add("Trace Data", typeof(string));
            try
            {
                if (String.IsNullOrEmpty(filepath))
                {
                    System.Console.WriteLine("File Path is Null or empty");
                }
                else
                {
                    //string[] lines = File.ReadAllLines(FilePath);
                    using (FileStream fs = File.Open(filepath, FileMode.Open))
                    using (BufferedStream bs = new BufferedStream(fs))
                    using (StreamReader sr = new StreamReader(bs))
                    {
                        string s;
                        int i = 0;
                        while ((s = sr.ReadLine()) != null)
                        {
                            if (s.Contains(searchterm))
                            {
                                DataRow dr = table.NewRow();
                                table.Rows.Add(i++, s);
                            }

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

        public static void findInterestingResources()
        {

            foreach (var node in _NodeInfo)
            {

                log("   Node: " + node.name, _w);
                foreach (DataRow row in node.Resources.Rows)
                {


                    if (_KnownResources.ContainsKey(row["ObjectId"].ToString()))
                    {
                        continue;
                    }

                    _KnownResources.Add(row["ObjectId"].ToString(), row["ObjectName"].ToString());

                    //Find Failed Resources
                    //filter out VM and VM Configs for testing too much noise, TODO: Make this an option 
                    if (!row["_state"].ToString().Contains("Online"))
                    {

                        //filtering rules for type and state
                        //
                        if (row["resourceType"].ToString().Contains("Virtual Machine") || row["resourceType"].ToString().Contains("Configuration") && !_showVMs)
                        {
                            continue;
                        }
                        if (row["_state"].ToString().Contains("Offline") && _ignoreOfflineResource)
                        {
                            continue;
                        }
                        if (row["_state"].ToString().Contains("Unknown") && _ignoreUnknownResource)
                        {
                            continue;
                        }


                        //start entery in log for this resource

                        log("           Rsource: " + row["ObjectName"].ToString() + " ObjectId: " + row["ObjectId"].ToString() + " ResourceType: " + row["resourceType"].ToString(), _w);
                        log("                   State: " + row["_state"].ToString() + " OldState: " + row["_oldState"].ToString(), _w);
                       
                        
                        //catpure inMemoryLastOperationStatusCode if there was one provided
                        if (!row["inMemoryLastOperationStatusCode"].ToString().Contains("0"))
                        {
                            int x = 0; int.TryParse(row["inMemoryLastOperationStatusCode"].ToString(),out x);
                            string hexvalue = x.ToString("X");
                            int hex = int.Parse(hexvalue, System.Globalization.NumberStyles.HexNumber);
                            
                        log("                   Last InMemory State: " + hex.ToString(), _w);
                        log("\r", _w);

                        }
                        //addd data from cluster log
                        DataTable evts_OpLog = readEventLog(row["ObjectName"].ToString(), node.logfilepath + _OperationalLog);
                        if (evts_OpLog.Rows.Count > 0)
                        {
                            log("                           Cluster Operational log:  " + row["ObjectName"].ToString(), _w);
                            for (int i = 0; i < evts_OpLog.Rows.Count; i++)
                            {
                                log("                               " + evts_OpLog.Rows[i][1].ToString(), _w);
                            }
                            log("\r", _w);
                            log("       ================================================== \r", _w);
                            log("\r", _w);
                        }
                       
                        //addd data from cluster log
                        DataTable evts_Cluster = readEventLog(row["ObjectName"].ToString(), node.logfilepath + _ClusterLog);
                        if (evts_Cluster.Rows.Count > 0)
                        {
                            log("                           Cluster verbose log:  " + row["ObjectName"].ToString(), _w);
                            for (int i = 0; i < evts_Cluster.Rows.Count; i++)
                            {
                                log("                               " + evts_Cluster.Rows[i][1].ToString(), _w);
                            }
                        }
                        

                        log("\r", _w);
                        log("\r", _w);
                        log("       ================================================== \r", _w);
                        log("\r", _w);
                    }
                }

            }
        }

            #endregion



    }

    class Node
    {
       public string name;
       public  string logfilepath;
       public DataTable SystemEvt;
       public DataTable Resources;
       public DataTable resource_types;
       public DataTable nodes;

    }

    
       public class outputLog
        {
            public static void Log(string logMessage, TextWriter w)
            {
                w.Write("\r\nLog Entry : ");
                w.WriteLine("{0} {1}", DateTime.Now.ToLongTimeString(),
                    DateTime.Now.ToLongDateString());
                w.WriteLine("  :");
                w.WriteLine("  :{0}", logMessage);
                w.WriteLine("-------------------------------");
            }

            public static void Openlog()
            {

            }
        }

}
