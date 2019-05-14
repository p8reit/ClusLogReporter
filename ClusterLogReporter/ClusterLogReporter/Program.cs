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
using System.Threading;
using System.Web;




namespace LogReporter
{
    static class Program
    {
        //globals
        const string _OperationalLog = "\\operational.txt";
        const string _SystemLog = "\\System.txt";
        const string _ClusterLog = "\\cluster.txt";
        const string _NodesLog = "\\nodes.csv";
        const string _GroupsLog = "\\groups.csv";
        static string _logsPath = null;
        static int _processedCount = 0;

        static Dictionary<string, string> _KnownResources = new Dictionary<string, string>();
        static List<DataTable> _ResourceTables = new List<DataTable>();
        static List<Node> _NodeInfo = new List<Node>();
        static StreamWriter _w = null;
        static StringBuilder _VolumeInfo = new StringBuilder();
        static StringBuilder _GroupInfo = new StringBuilder();
        static StringBuilder _ResourceInfo = new StringBuilder();
        static StringBuilder _RuleInfo = new StringBuilder();
        static StringBuilder _NodeEvents = new StringBuilder();

        static bool _showVMs = true;
        static bool _ignoreOfflineResource = false;
        static bool _ignoreUnknownResource = false;
        static bool _foundGroups = false;
        static bool _foundResources = false;
        static bool _Cluster = false;
        static bool _VSSlogs = false;

        //user prams
        static bool _skipfilecreate = false;
        static bool _skipresources = false;
        static bool _skipgroups = false;

        //Totals
        static int _resourceCount = 0;
        static int _GroupCount = 0;
        static int _VolumeCount = 0;


        //only needed for html formatting 
        //currently not used
        private static string _paraBreak = "\r\n\r\n";
        private static string _link = "<a href=\"{0}\">{1}</a>";
        private static string _linkNoFollow = "<a href=\"{0}\" rel=\"nofollow\">{1}</a>";

        static void Main(string[] args)
        {
            if (processArgs(args))
            {

                if (_Cluster)
                {
                    //collect node info
                    string[] fileEntries = Directory.GetFiles(_logsPath, "*cluster.log");
                    if (fileEntries.Length > 0)
                    {
                        handleClusterLogs(fileEntries);
                    }
                }
                if (_VSSlogs)
                {
                    Console.WriteLine("Coming Soon!");
                }
                
                
                
                
            }
        }

        static void createClusterSumary()
        {

            Console.Write("Gathering Summary Information...");
            
            //do the actual log work frist
            //Finds Resources not in the online state-- TODO: Improve this apporach to interesting resources
            if (_skipresources)
            {
                Console.Write("Searching for interesting resources...");
                findInterestingResources();
            }
            if (_skipgroups)
            {
                Console.Write("Searching for interesting groups...");
                //Find Groups
                findInterestingGroups();
            }
            Console.Write("Searching for interesting volumes states...");
            //get volume info 
            gatherVolumeInfo();

            //Console.Write("Processing Rules...");
            ////rules processing
            //processRules();

            Console.Write("Generating output report to :" + _logsPath);

            
            //stupid stuff to get the cluster name 
            foreach (var node in _NodeInfo)
            {

                if (node.Resources == null)
                {
                    continue;
                }
                foreach (DataRow row in node.Resources.Rows)
                {
                    if (row["resourceType"].ToString() == "Network Name" && row["ObjectName"].ToString() != "Cluster Name")
                    {
                        writeToLog("ClusterName: " + row["ObjectName"].ToString(), _w);
                        break;
                    }
                }

                break;
            }

            writeToLog("Logs Processed: " + _NodeInfo.Count.ToString(), _w);
            writeToLog("log Paths: \r", _w);
            foreach (var node in _NodeInfo)
            {
                writeToLog("       " + node.logfilepath, _w);
            }
            writeToLog("Nodes processed Count: " + _NodeInfo.Count.ToString(), _w);
            writeToLog("Nodes found: " , _w);
            foreach (var node in _NodeInfo)
            {
                writeToLog("       " + node.name, _w);
            }

            writeToLog("Summary:", _w);
            writeToLog("        " + _GroupCount + " group(s) in a unhealthy state.", _w);
            writeToLog("        " + _resourceCount + " resource(s) in a unhealthy state.", _w);
            writeToLog("", _w);

            if (_RuleInfo.Length > 0)
            {
                writeToLog("Rules hit:", _w);
                writeToLog(_RuleInfo.ToString(), _w);
                writeToLog("", _w);
            }

            writeToLog(" ", _w);
            writeToLog("Node Timelines: ", _w);
            foreach (var node in _NodeInfo)
            {
                writeToLog("       " + node.name, _w);
                findNodeEvents(node);
                writeToLog(_NodeEvents.ToString(),_w);
                _NodeEvents.Clear();
                writeToLog("", _w);
            }

            
            writeToLog(" ", _w);
            writeToLog("Volume Information:", _w);
            writeToLog(" ", _w);

            writeToLog(_VolumeInfo.ToString(), _w);
            _VolumeInfo = null;


            if (_foundGroups)
            {
                
                writeToLog(" ", _w);
                writeToLog("Interesting Groups:", _w);
                
                writeToLog(_GroupInfo.ToString(), _w);
                _GroupInfo = null;
            }


            if (_foundResources)
            {
                
                writeToLog(" ", _w);
                writeToLog("Resources interesting states by Node:", _w);
                
                writeToLog(_ResourceInfo.ToString(), _w);
                _ResourceInfo = null;
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

            try
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
            catch (Exception)
            {

                return null;
            }
            

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

        static async Task<IEnumerable<bool>> processPath(string targetDirectory)
        {
            try
            {
                // Process the list of files found in the directory.
                string[] fileEntries = Directory.GetFiles(targetDirectory, "*cluster.log");
                Console.Write("Found " + fileEntries.Count().ToString() + " cluster logs in root folder.");
                List<Task<bool>> Tasklist = new List<Task<bool>>();


                foreach (string fileName in fileEntries)
                {

                    //Console.Write("Started processing file: " + fileName + " \r");
                    //this tranforms the cluster log with trimlogs
                    //Tasklist.Add(Task.Run(runTrimLogs(fileName, targetDirectory)));

                    Tasklist.Add(runTrimLogs(fileName, targetDirectory));
                    
                    // Console.Write("Completed processing file: " + fileName + "\r");

                }

                return await Task.WhenAll(Tasklist);
                
                //// Recurse into subdirectories of this directory.
                //string[] subdirectoryEntries = Directory.GetDirectories(targetDirectory);
                //foreach (string subdirectory in subdirectoryEntries)
                //    processPath(subdirectory);
            }
            catch (System.OutOfMemoryException e)
            {
                Console.Write("Failed to read files out of memory :( : " + e.HResult.ToString());
                //throw;
                return null;
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

        static public void handleClusterLogs(string[] fileEntries)
        {
            foreach (var item in fileEntries)
            {
                string CurrentNode = getCurrentNodeFromLog(item);
                if (String.IsNullOrEmpty(CurrentNode))
                {
                    return;
                }

                string newlogspath = _logsPath + CurrentNode;
                Node newnode = new Node();
                newnode.name = CurrentNode;
                newnode.logfilepath = newlogspath;
                _NodeInfo.Add(newnode);
            }



            //validate the cluster is a v2 log and process it with trimlogs
            //this leaves you will a folder for each node 
            //with CSV and txt files with data to be poked for info
            if (!_skipfilecreate)
            {
                populateDataFromClusterLogs(_logsPath);
            }


            //take the resources csv file from each node and process it to a merged view
            if (_processedCount > 0 || _skipfilecreate)
            {
                getCSVFilesForNode(); //stub

                //process recources and find owners and states
                //start processing any resources in a none healthy state
                if (File.Exists(_logsPath + "ClusterReportlog.txt"))
                {
                    File.Delete(_logsPath + "ClusterReportlog.txt");
                }

                using (_w = File.AppendText(_logsPath + "ClusterReportlog.txt"))
                {
                    // processResources();
                    createClusterSumary();
                }
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
                    var output = File.OpenWrite(path + "\\Tool.exe");
                    resourceToSave.CopyTo(output);
                    resourceToSave.Flush();
                    resourceToSave.Close();
                    output.Flush();
                    output.Close();
                }

        }

        static void populateDataFromClusterLogs(string path)
        {

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
                System.Console.WriteLine("Usage: LogReporter.exe <pathtologs> <options>");
                System.Console.WriteLine("LogType:");
                System.Console.WriteLine("Clus - Cluster Logs");
                System.Console.WriteLine("VSS  - VSS Trace - Coming Soon!");
                System.Console.WriteLine("Options: ");
                System.Console.WriteLine("SR - Skips Resources");
                System.Console.WriteLine("SG - Skips Groups");
                System.Console.WriteLine("S  -  Skips File Creation(reprocess logs)");
                System.Console.WriteLine("Example: LogReporter.exe -<type> C:\\LogsFolder -sr -sg -s");
                return isArgs;
            }

            bool first = true;
            foreach (var arg in args)
            {

                if (first && !String.IsNullOrEmpty(arg))
                {
                    first = false;
                    _logsPath = arg;
                    continue;
                }

                string[] arg1 = arg.Split('-');
                switch (arg1[1].ToLower())
                {
                    case "s":
                        {
                            _skipfilecreate = true;
                            break;
                        }
                    case "sg":
                        {
                           _skipgroups = true;
                            break;
                        }
                    case "sr":
                        {
                            _skipresources = true;
                            break;
                        }
                    case "clus":
                        {
                            _Cluster = true;
                            break;
                        }
                    case "vss":
                        {
                            _VSSlogs = true;
                            break;
                        }
                    default:
                        _logsPath = arg;
                        break;

                }
            }

            return true;
        }
            
        static async Task<bool> runTrimLogs(string pathToFile, string logsRoot)
        {
            string CurrentNode = getCurrentNodeFromLog(pathToFile);
            if (String.IsNullOrEmpty(CurrentNode))
            {
                return false;
            }
            
            string newlogspath = (logsRoot + CurrentNode);
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

                //newnode.logfilepath = newlogspath;
                //_NodeInfo.Add(newnode);
                Console.Write("Clusterlog for node " + CurrentNode + " being processed and saved to " + newlogspath);
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
                return true;
            }

            return true;
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

        public static void writeToLog(string logMessage, TextWriter w)
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
                _ResourceInfo.AppendLine("   Node: " + node.name);
                if (node.Resources != null)
                {
                    foreach (DataRow row in node.Resources.Rows)
                    {
                        //check for the resrouce on the list and only reprot the state if it differs from the alraedy exiting record
                        string outval; _KnownResources.TryGetValue(row["ObjectId"].ToString(),out outval);
                        if (_KnownResources.ContainsKey(row["ObjectId"].ToString()))
                        {
                            if (row["_state"].ToString() != outval)
                            {
                                goto Stage2;
                            }
                            else
                            {
                                continue;
                            }
                            
                        }
                        else
                        {
                            _KnownResources.Add(row["ObjectId"].ToString(), row["_state"].ToString());
                        }

                        
      Stage2:                  
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
                            _foundResources = true;
                            _resourceCount++;
                            _ResourceInfo.AppendLine("           Rsource: " + row["ObjectName"].ToString() + " ObjectId: " + row["ObjectId"].ToString() + " ResourceType: " + row["resourceType"].ToString());
                            _ResourceInfo.AppendLine("                   State: " + row["_state"].ToString() + " OldState: " + row["_oldState"].ToString());


                            //catpure inMemoryLastOperationStatusCode if there was one provided
                            if (!row["inMemoryLastOperationStatusCode"].ToString().Contains("0"))
                            {
                                int x = 0; int.TryParse(row["inMemoryLastOperationStatusCode"].ToString(), out x);
                                string hexvalue = x.ToString("X");
                                int hex = int.Parse(hexvalue, System.Globalization.NumberStyles.HexNumber);

                                _ResourceInfo.AppendLine("                   Last InMemory State: " + hex.ToString());
                                _ResourceInfo.AppendLine("\r");

                            }

                            
                            //addd data from cluster log
                            DataTable evts_OpLog = readEventLog(row["ObjectName"].ToString(), node.logfilepath + _OperationalLog);
                            if (evts_OpLog.Rows.Count > 0)
                            {
                                _ResourceInfo.AppendLine("                           Cluster Operational log:  " + row["ObjectName"].ToString());
                                for (int i = 0; i < evts_OpLog.Rows.Count; i++)
                                {
                                    _ResourceInfo.AppendLine("                               " + evts_OpLog.Rows[i][1].ToString());
                                }
                                _ResourceInfo.AppendLine("\r");
                                _ResourceInfo.AppendLine("       ================================================== \r");
                                _ResourceInfo.AppendLine("\r");
                            }
                        }
                    }
                }
                else
                {
                    _ResourceInfo.AppendLine("   This nodes logs contained no unique resource states.");
                    _ResourceInfo.AppendLine(" ");
                    _ResourceInfo.AppendLine(" ");
                }
                

            }
        }

        public static void findInterestingGroups()
        {
            if (!String.IsNullOrEmpty(_NodeInfo[0].logfilepath) || !String.IsNullOrWhiteSpace(_NodeInfo[0].logfilepath))
            {
                DataTable tbl = getCSVToTbl(_NodeInfo[0].logfilepath + _GroupsLog);

                if (tbl.Rows.Count > 0 )
                {

                    _GroupInfo.AppendLine("Groups in Interesting States:");

                    foreach (DataRow row in tbl.Rows)
                    {
                        if (row["_state"].ToString() != "Online")
                        {

                            
                            //add data from cluster log
                            //Get Resource moves for this object
                            if (!string.IsNullOrEmpty(row["ObjectName"].ToString()))
                            {
                                if (!_showVMs && row["ObjectName"].ToString().Contains("SCVMM"))
                                {
                                    continue;
                                }
                                
                                DataTable evts_Cluster = readEventLog("[RCM] move of group " + row["ObjectName"].ToString(), _NodeInfo[0].logfilepath + _ClusterLog);
                                if (evts_Cluster.Rows.Count > 0)
                                {
                                    _foundGroups = true;
                                    _GroupCount++;
                                    _GroupInfo.AppendLine("                           Group Transitions for Group:  " + row["ObjectName"].ToString() + " State: " + row["_state"].ToString());
                                    for (int i = 0; i < evts_Cluster.Rows.Count; i++)
                                    {
                                        _GroupInfo.AppendLine("                               " + evts_Cluster.Rows[i][1].ToString());
                                    }
                                }
                                _GroupInfo.AppendLine("       ================================================== \r");
                                //writeToLog("\r", _w);
                            }
                        }
                    }
                }
            }
                      

        }

        public static void findInterestingVolumes()
        {



        }

        public static void gatherVolumeInfo()
        {

            //provide the user with volume arival information. 

            foreach (var node in _NodeInfo)
            {
                DataTable evts_Cluster = readEventLog("SetDownLevelNFilter", node.logfilepath + _ClusterLog);
                if (evts_Cluster.Rows.Count > 0)
                {
                    _VolumeInfo.AppendLine("    Volume Arrival events for " + ": " + node.name + "\r");
                    
                    for (int i = 0; i < evts_Cluster.Rows.Count; i++)
                    {
                    _VolumeInfo.AppendLine("            " + evts_Cluster.Rows[i][1].ToString());
                    }
                }

                _VolumeInfo.AppendLine("");
            }
            _VolumeInfo.AppendLine("================================================== \r");


        }

        public static void inspectNodeState()
        {

            if (_NodesLog != null)
            {
                foreach (var node in _NodeInfo)
                {
                    DataTable tbl = getCSVToTbl(node.logfilepath + _NodesLog);


                }
            }
            

            
        }

        public static string ConvertDataTableToHTML(DataTable dt)
        {
            string html = "<table>";
            //add header row
            html += "<tr>";
            for (int i = 0; i < dt.Columns.Count; i++)
                html += "<td>" + dt.Columns[i].ColumnName + "</td>";
            html += "</tr>";
            //add rows
            for (int i = 0; i < dt.Rows.Count; i++)
            {
                html += "<tr>";
                for (int j = 0; j < dt.Columns.Count; j++)
                    html += "<td>" + dt.Rows[i][j].ToString() + "</td>";
                html += "</tr>";
            }
            html += "</table>";
            return html;
        }

        public static void processRules()
        {

            //STATUS_CONNECTION_DISCONNECTED(c000020c)
            foreach (var node in _NodeInfo)
            {
                DataTable evts_Cluster = readEventLog("STATUS_IO_TIMEOUT(c00000b5)", node.logfilepath + _SystemLog);
                if (evts_Cluster.Rows.Count > 0)
                {
                    _RuleInfo.AppendLine("Rule Triggered for for node " + node.name + ": 'Event 5120' with STATUS_IO_TIMEOUT c00000b5 or STATUS_CONNECTION_DISCONNECTED(c000020c) after an S2D node restart on Windows Server 2016 May 2018 update or later \r");
                    _RuleInfo.AppendLine("A likely known issue was detected. To fix this issue, ensure the October 18, 2018, cumulative update for Windows Server 2016 (KB4462928) or a later version is installed. \r");
                    _RuleInfo.AppendLine("KB link: https://support.microsoft.com/en-in/help/4462487/ \r");

                    for (int i = 0; i < evts_Cluster.Rows.Count; i++)
                    {
                        _RuleInfo.AppendLine("      " + evts_Cluster.Rows[i][1].ToString());
                    }
                }

                _RuleInfo.AppendLine("");
                
                DataTable evts_Cluster1 = readEventLog("STATUS_CONNECTION_DISCONNECTED(c000020c)", node.logfilepath + _SystemLog);
                if (evts_Cluster1.Rows.Count > 0)
                {
                    _RuleInfo.AppendLine("Rule Triggered for for node " + node.name + ": 'Event 5120' with STATUS_IO_TIMEOUT c00000b5 or STATUS_CONNECTION_DISCONNECTED(c000020c) after an S2D node restart on Windows Server 2016 May 2018 update or later \r");
                    _RuleInfo.AppendLine("A likely known issue was detected. To fix this issue, ensure the October 18, 2018, cumulative update for Windows Server 2016 (KB4462928) or a later version is installed. \r");
                    _RuleInfo.AppendLine("KB link: https://support.microsoft.com/en-in/help/4462487/ \r");

                    for (int i = 0; i < evts_Cluster1.Rows.Count; i++)
                    {
                        _RuleInfo.AppendLine("      " + evts_Cluster1.Rows[i][1].ToString());
                    }
                }

                _RuleInfo.AppendLine("");
            }
            //_RuleInfo.AppendLine("================================================== \r");


        }

        public static void findNodeEvents(Node Node)
        {

            //Find service start events
            DataTable evts_Cluster2 = readEventLog("Starting clussvc", Node.logfilepath + _ClusterLog);
            if (evts_Cluster2.Rows.Count > 0)
            {

                _NodeEvents.AppendLine("            Starting Clussvc Events:  ");
                for (int i = 0; i < evts_Cluster2.Rows.Count; i++)
                {
                    _NodeEvents.AppendLine("                    " + evts_Cluster2.Rows[i][1].ToString());
                }
                _NodeEvents.AppendLine("            =============================================");
            }

            //Find new view events
            DataTable evts_Cluster3 = readEventLog("New View is", Node.logfilepath + _ClusterLog);
            if (evts_Cluster3.Rows.Count > 0)
            {

                _NodeEvents.AppendLine("            Node Membership View:  ");
                for (int i = 0; i < evts_Cluster3.Rows.Count; i++)
                {
                    _NodeEvents.AppendLine("                    " + evts_Cluster3.Rows[i][1].ToString());
                }
                _NodeEvents.AppendLine("            =============================================");
            }

            //Find Join events
            DataTable evts_Cluster = readEventLog("joined the failover cluster", Node.logfilepath + _OperationalLog);
            if (evts_Cluster.Rows.Count > 0)
            {
                
                _NodeEvents.AppendLine("            Join Events:  ");
                for (int i = 0; i < evts_Cluster.Rows.Count; i++)
                {
                _NodeEvents.AppendLine("                    " + evts_Cluster.Rows[i][1].ToString());
                }
                _NodeEvents.AppendLine("            =============================================");
            }
            
            //Find Join events
            DataTable evts_Cluster1 = readEventLog("isolated state", Node.logfilepath + _SystemLog);
            if (evts_Cluster1.Rows.Count > 0)
            {

                _NodeEvents.AppendLine("            Isolation Events:  ");
                for (int i = 0; i < evts_Cluster1.Rows.Count; i++)
                {
                    _NodeEvents.AppendLine("                    " + evts_Cluster1.Rows[i][1].ToString());
                }
                _NodeEvents.AppendLine("            =============================================");
            }

            //consecutive heartbeats
            //Find Join events
            DataTable evts_Cluster4 = readEventLog("consecutive heartbeats", Node.logfilepath + _ClusterLog);
            if (evts_Cluster4.Rows.Count > 0)
            {

                _NodeEvents.AppendLine("            Missed Heartbeats Events:  ");
                for (int i = 0; i < evts_Cluster4.Rows.Count; i++)
                {
                    _NodeEvents.AppendLine("                    " + evts_Cluster4.Rows[i][1].ToString());
                }
                _NodeEvents.AppendLine("            =============================================");
            }

        }

        #endregion

        #region HTML

        public static string ToHtml(this string s)
        {
            return ToHtml(s, false);
        }

        /// <summary>
        /// Returns a copy of this string converted to HTML markup.
        /// </summary>
        /// <param name="nofollow">If true, links are given "nofollow"
        /// attribute</param>
        public static string ToHtml(this string s, bool nofollow)
        {


            StringBuilder sb = new StringBuilder();

            int pos = 0;
            while (pos < s.Length)
            {
                // Extract next paragraph
                int start = pos;
                pos = s.IndexOf(_paraBreak, start);
                if (pos < 0)
                    pos = s.Length;
                string para = s.Substring(start, pos - start).Trim();

                // Encode non-empty paragraph
                if (para.Length > 0)
                    EncodeParagraph(para, sb, nofollow);

                // Skip over paragraph break
                pos += _paraBreak.Length;
            }
            // Return result
            return sb.ToString();
        }

        /// <summary>
        /// Encodes a single paragraph to HTML.
        /// </summary>
        /// <param name="s">Text to encode</param>
        /// <param name="sb">StringBuilder to write results</param>
        /// <param name="nofollow">If true, links are given "nofollow"
        /// attribute</param>
        private static void EncodeParagraph(string s, StringBuilder sb, bool nofollow)
        {
            // Start new paragraph
            sb.AppendLine("<p>");

            // HTML encode text
            s = HttpUtility.HtmlEncode(s);

            // Convert single newlines to <br>
            s = s.Replace(Environment.NewLine, "<br />\r\n");

            // Encode any hyperlinks
            EncodeLinks(s, sb, nofollow);

            // Close paragraph
            sb.AppendLine("\r\n</p>");
        }

        /// <summary>
        /// Encodes [[URL]] and [[Text][URL]] links to HTML.
        /// </summary>
        /// <param name="text">Text to encode</param>
        /// <param name="sb">StringBuilder to write results</param>
        /// <param name="nofollow">If true, links are given "nofollow"
        /// attribute</param>
        private static void EncodeLinks(string s, StringBuilder sb, bool nofollow)
        {
            // Parse and encode any hyperlinks
            int pos = 0;
            while (pos < s.Length)
            {
                // Look for next link
                int start = pos;
                pos = s.IndexOf("[[", pos);
                if (pos < 0)
                    pos = s.Length;
                // Copy text before link
                sb.Append(s.Substring(start, pos - start));
                if (pos < s.Length)
                {
                    string label, link;

                    start = pos + 2;
                    pos = s.IndexOf("]]", start);
                    if (pos < 0)
                        pos = s.Length;
                    label = s.Substring(start, pos - start);
                    int i = label.IndexOf("][");
                    if (i >= 0)
                    {
                        link = label.Substring(i + 2);
                        label = label.Substring(0, i);
                    }
                    else
                    {
                        link = label;
                    }
                    // Append link
                    sb.Append(String.Format(nofollow ? _linkNoFollow : _link, link, label));

                    // Skip over closing "]]"
                    pos += 2;
                }
            }
        }


        #endregion


    }

    class Node
    {
       public string name;
       public string logfilepath;
       public DataTable SystemEvt;
       public DataTable Resources;
       public DataTable resource_types;
       public DataTable nodes;
       public string NodeState;

    }

    class Resource
    {

        public string Name;
        public string State;
        public string Type;

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
