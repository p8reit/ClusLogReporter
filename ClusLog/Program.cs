using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Data;
using System.IO;

namespace ClusLog
{
    class Program
    {
        static void Main(string[] args)
        {
            if (processargs(args))
            {
                DataTable table = ReadFiletoTbl(args[0]);
                Array Tables;
                
                
                foreach (DataRow row in table.Rows)
                {
                    //Console.WriteLine("--- Row ---");
                    foreach (var item in row.ItemArray)
                    {

                        if (item.ToString().Contains("=== "))
                        {
                            Console.Write("Section" + item.ToString());
                        }
                        //Console.Write("Item: "); // Print label.
                        //Console.WriteLine(item);
                    }
                }
            } 

        }


        public static DataTable ReadFiletoTbl(string FilePath)
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
                    string[] lines = File.ReadAllLines(FilePath);


                    int i = 0;

                    foreach (string line in lines)
                    {
                        bool chk = false;

                        if (chk != true)
                        {

                            chk = true;
                            DataRow dr = table.NewRow();
                            table.Rows.Add(i++, line);
                        }
                    }
                }

                return table;
            }
            catch (Exception)
            {

                throw;
            }
            

        }

        static bool processargs(string[] args)
        {

            bool isArgs = false;

            // Test if input arguments were supplied:
            if (args.Length == 0)
            {
                System.Console.WriteLine("Please enter a path.");
                System.Console.WriteLine("Usage: Cluslg.exe <pathtolog>");
                System.Console.WriteLine("Example: Cluslg.exe C:\\Cluster.log");
                return isArgs;
            }

            bool test = String.IsNullOrEmpty(args[0]);
            if (test != false)
            {
                System.Console.WriteLine("Please enter a valid path.");
                System.Console.WriteLine("Usage: Cluslg.exe <pathtolog>");
                System.Console.WriteLine("Example: Cluslg.exe C:\\Cluster.log");
                return isArgs;
            }
            else
            {

                return true;

            }
            
        }
    

    }
}
