using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using System.IO;
using System.Configuration;
using System.Diagnostics;
using System.Net;
using MySql.Data.MySqlClient;
using System.Data;
using System.Threading;
using IWshRuntimeLibrary;
using System.Web.Security;
using System.Web;

namespace DeployReport
{
    class Program
    {
        static string _batchSeqNum;
     
        static string _exceptionDataDetailsServiceURL = ConfigurationManager.AppSettings["exceptionDataDetailsServiceURL"];
        static string _jmeterOutputFolder = ConfigurationManager.AppSettings["JmeterOutputFolder"];
     
        string _jMeterInputFileFolder = ConfigurationManager.AppSettings["JMeterInputFileFolder"];
   
        string _appName = ConfigurationManager.AppSettings["AppName"];
        static  string _batFile = ConfigurationManager.AppSettings["JMeterCmdFile"];
        string _jmeterOutputFile;
        private static object _lock = new object();
        string _mySQLConnectionString = ConfigurationManager.AppSettings["mySQLConnectionString"];
        string _emailServiceURL =ConfigurationManager.AppSettings["emailServiceURL"];// "http://shyw.ecc.icson.com/api/alarm_api.php";
        static string _userName=ConfigurationManager.AppSettings["userName"];
        static string _password = ConfigurationManager.AppSettings["password"];
        static void WriteToTraceAndConsole(string log)
        {
           //Trace.WriteLine(log + " at "+ DateTime.Now);
            Console.WriteLine(log + " at "+ DateTime.Now);
        }
      
        static void Main(string[] args)
        {
        
            {
 

      
                {


                    for (int i = 0; i < args.Length; i++)
                    {
                        if (args[i].ToLower() == "-j")
                        {
                            if (i < args.Length - 1)
                            {
                                _batFile = args[i + 1];
                            }
                        }

                    }
                    Program p = new Program();
                    p.Run();
                
                }
              
            }
        }
        private void Run() {
         
            WriteToTraceAndConsole("DeployReport Start Running...");
            try
            {

                _batchSeqNum = Guid.NewGuid().ToString();
                 DirectoryInfo dir = new DirectoryInfo(_jMeterInputFileFolder); 
                //shortcut mode
                 //var jFiles = dir.GetFiles("*.lnk").ToList();
                //file mode
                 var jFiles = dir.GetFiles("*.jmx").ToList();
                for (int i = 0; i < jFiles.Count(); i++)
                {
                    try
                    {
                        var f = jFiles[i];
                        // shortcut mode

                        //WshShell shell = new WshShell();

                        //IWshShortcut link = (IWshShortcut)shell.CreateShortcut(f.FullName);
                        // Creates a copy of that shorcut

                        //string targetpath = link.TargetPath; //using the properties of the created shortcut we can get the TargetPath
                        //file mode
                        string targetpath = f.FullName;
                        var doc = XDocument.Load(targetpath);
                        if (doc.Descendants("ResultCollector").Count() >= 1)
                        {
                            DirectoryInfo outputDir = new DirectoryInfo(_jmeterOutputFolder);
                        
                            _jmeterOutputFile = outputDir.FullName + @"\" + Guid.NewGuid().ToString() + ".csv";
                            if (System.IO.File.Exists(_jmeterOutputFile))
                            {
                                System.IO.File.Delete(_jmeterOutputFile);
                            }
                            //var outputLogElement = doc.Descendants("ResultCollector").Elements("stringProp").First();
                            //var outputLogFile = outputLogElement.Value;

                            //_jmeterOutputFile = outputLogFile;

                            var functionName = f.Name.Split('.')[0];

                            StartProcess(targetpath);

                        }
                    }
                    catch (Exception ex)
                    {
                        // var errorInfo = string.Format("Error:{0}, stacktrace:{1}", ex.Message, ex.StackTrace);
                        // TODO: Log Exception stack trace, message to db
                        WriteToTraceAndConsole(ex.ToString());
                    }
                }
                
 
               
            }
            catch (Exception ex)
            {
               // var errorInfo = string.Format("Error:{0}, stacktrace:{1}", ex.Message, ex.StackTrace);
                // TODO: Log Exception stack trace, message to db
                WriteToTraceAndConsole(ex.ToString());
            }
        }
        /// <summary>
        /// Returns the batch sequence number
        /// </summary>
        /// <returns></returns>
        private string StartProcess(string jMeterInputFileUrl)
        {
           
            string result = string.Empty;
            lock (_lock)
            {
               // var baseDir = AppDomain.CurrentDomain.BaseDirectory;
                // Run batch file that uses JMeter to run test cases and create output file
                WriteToTraceAndConsole(string.Format("Start running batch file", DateTime.Now));
                RunBatch(_batFile, jMeterInputFileUrl,_jmeterOutputFile);
                WriteToTraceAndConsole(string.Format("End running batch file", DateTime.Now));
                // Save output
                WriteToTraceAndConsole(string.Format("Start saving output data to DB", DateTime.Now));
                try
                {
                    result = SaveJMeterOutputDataToDb(jMeterInputFileUrl);
                    WriteToTraceAndConsole(string.Format("End saving output data to DB", DateTime.Now));
                }
                // Delete output file
                finally
                {
                    System.IO.File.Delete(_jmeterOutputFile);
                }
                WriteToTraceAndConsole(string.Format("Output data on disk is of no use now. Output file deleted", DateTime.Now));

            }
            return result;
        }
        private void RunBatch(string batPath, string jMeterInputFileUrl, string outputfile)
        {
            Process p = Process.Start(new ProcessStartInfo(batPath,
@"-JJmeterAuto_LogFile=" + outputfile + " -n -t " + "\"" + jMeterInputFileUrl + "\"")
            {
                WorkingDirectory = batPath.Replace("jmeter.bat",""),
                UseShellExecute = false, RedirectStandardOutput = false               
            });
            p.WaitForExit();
        }
        private string SaveJMeterOutputDataToDb(string jMeterInputFileUrl)
        {
          
            // Save JMeter output data to db
            using (rockEntities entities = new rockEntities())
            {
               
                JMeterOutputFileParser parser = new JMeterOutputFileParser(_jmeterOutputFile);

                // Get all the httpSample data from the JMeter output file, save them to database
              int numInSameSeq = 0;
                foreach (var httpSample in parser.GetHttpSamples())
                {
                    // Jemter has a bug that sometimes will store empty responseData


                    numInSameSeq++;
                    var newRecord = new rock_jmeter();
                    if (httpSample.AssertionResult != null)
                    {
                        newRecord.AssertionResult_Error = httpSample.AssertionResult.Error;
                        newRecord.AssertionResult_Failure = httpSample.AssertionResult.Failure;
                        newRecord.AssertionResult_Name = httpSample.AssertionResult.Name;
                    }
                    FileInfo fi = new FileInfo(jMeterInputFileUrl);
                    newRecord.numInSameSeq = numInSameSeq;
                    newRecord.JmxFileName = fi.Name;
                    newRecord.exeUser = _userName;
                    newRecord.CGIName = httpSample.CGIName;
                    newRecord.Cookies = httpSample.Cookies;
                    newRecord.Method = httpSample.Method;
                    newRecord.QueryString = httpSample.QueryString;
                    newRecord.RequestHeader = httpSample.RequestHeader;
                    newRecord.ResponseData = httpSample.ResponseData;
                    newRecord.ResponseHeader = httpSample.ResponseHeader;
                    newRecord.ResponseTime = int.Parse(httpSample.ResponseTime);
                    int returnCode = -1;
                    int.TryParse(httpSample.ReturnCode, out returnCode);
                    newRecord.ReturnCode = returnCode;
                    newRecord.Url = httpSample.Url;
                    newRecord.RecordTimeStamp = new DateTimeOffset(DateTime.Now);
                    newRecord.ThreadName = httpSample.ThreadName;
                    newRecord.ParentCGIName = httpSample.ParentCGIName;
                    newRecord.AppName = this._appName;
                    newRecord.SeqNum = _batchSeqNum;
                    StringBuilder sbForIp = new StringBuilder();
                    IPAddress[] localIPs = Dns.GetHostAddresses(Dns.GetHostName());

                    foreach (IPAddress addr in localIPs)
                    {
                        sbForIp.Append(addr);
                        sbForIp.AppendLine();
                    }
                    newRecord.TestMachineIP = sbForIp.ToString();

                    
                        entities.AddTorock_jmeter(newRecord);
                 

                }
                // start saving
              
                    entities.SaveChanges();
                
                // save done
            }
            return _batchSeqNum;
        }
    }
    #region JMeter Parser Classes
    public class JMeterOutputFileParser
    {
        private string _filePath = string.Empty;
        private List<HttpSample> _httpSamples = new List<HttpSample>();
        private XDocument _doc;
        public JMeterOutputFileParser() { }
        public JMeterOutputFileParser(string filePath)
        {

            if (System.IO.File.Exists(filePath))
            {
                this._filePath = filePath;
                _doc = XDocument.Parse(this.PreProcessXml(System.IO.File.ReadAllText(filePath)));
                var httpSampleNodes = _doc.Descendants().ToList();
                foreach (var node in httpSampleNodes)
                {
                    if (node.Name == "httpSample" || node.Name == "sample")
                    {
                        var responseHeader = node.Element("responseHeader").Value;
                        var requestHeader = node.Element("requestHeader").Value;
                        var responseData = node.Element("responseData").Value;
                        var queryString = node.Element("queryString") == null ? string.Empty : node.Element("queryString").Value;
                        // Does 't' mean the response time?
                        var responseTime = node.Attribute("t").Value;
                        var cgiName = node.Attribute("lb").Value;
                        var returnCode = node.Attribute("rc").Value;
                        var cookies = node.Element("cookies") == null ? string.Empty : node.Element("cookies").Value;
                        var url = node.Element("java.net.URL") == null ? string.Empty : node.Element("java.net.URL").Value;
                        var method = node.Element("method") == null ? string.Empty : node.Element("method").Value;
                        var threadName = node.Attribute("tn").Value;
                        AssertionResult assertionResult = null;
                        var assertionNode = node.Element("assertionResult");
                        if (assertionNode != null)
                        {
                            assertionResult = new AssertionResult();
                            assertionResult.Error = assertionNode.Element("error").Value;
                            assertionResult.Failure = assertionNode.Element("failure").Value.ToLower();
                            if (assertionNode.Element("failureMessage") != null && assertionNode.Element("failure").Value.ToLower() == "true")
                            {
                                var failsureMsg = assertionNode.Element("failureMessage").Value;
                                //if (failsureMsg.ToLower() == "false" || string.IsNullOrEmpty(failsureMsg))
                                {
                                    assertionResult.FailureMsg = failsureMsg;
                                }
                            }
                            assertionResult.Name = assertionNode.Element("name").Value;
                        }
                        var httpSample = new HttpSample();
                        httpSample.AssertionResult = assertionResult;
                        httpSample.CGIName = cgiName;
                        httpSample.Cookies = cookies;
                        httpSample.Method = method;
                        httpSample.QueryString = queryString;
                        httpSample.RequestHeader = requestHeader;
                        httpSample.ResponseData = responseData;
                        httpSample.ResponseHeader = responseHeader;
                        httpSample.ResponseTime = responseTime;
                        httpSample.ReturnCode = returnCode;
                        httpSample.ThreadName = threadName;
                        httpSample.Url = url;
                        // Find parent httpsample, if it has parent httpsample node, set parent's lb attribute to it's parent CGI Name
                        if (node.Parent.Name == "sample")
                        {
                            httpSample.ParentCGIName = node.Parent.Attribute("lb").Value;
                        }
                        else if (node.Parent.Name == "httpSample")
                        {
                            httpSample.ParentCGIName = node.Parent.Attribute("lb").Value;
                        }
                        if (httpSample.CGIName != "忽略")
                        {
                            _httpSamples.Add(httpSample);
                        }
                    }
                }

            }
        }
        public List<HttpSample> GetHttpSamples()
        {
            return _httpSamples;
        }
        public enum Status { Normal, ProbeUnicode, ProbeHex }
        public string PreProcessXml(string xml)
        {
            char[] hexChar = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F', 'a', 'b', 'c', 'd', 'e', 'f' };
            // Find unicode
            StringBuilder sb = new StringBuilder();
            Status status = Status.Normal;
            char probingChar = '#';
            string temp = string.Empty;
            char triggerChar = '&';
            for (int i = 0; i < xml.Length; i++)
            {
                var currentChar = xml[i];
                if (status == Status.ProbeUnicode)
                {
                    if (probingChar == currentChar)
                    {


                        if (probingChar == 'x')
                        {
                            probingChar = '#';
                            status = Status.ProbeHex;
                        }
                        if (probingChar == '#') probingChar = 'x';
                        continue;
                    }
                    else
                    {
                        if (probingChar == '#')
                        {
                            sb.Append('&');
                        }
                        else if (probingChar == 'x')
                        {
                            sb.Append("&#");
                        }
                        sb.Append(currentChar);
                        status = Status.Normal; triggerChar = '&';
                        continue;
                    }

                }
                else if (status == Status.ProbeHex)
                {
                    if (!hexChar.Contains(currentChar) && currentChar != ';')
                    {
                        sb.Append(currentChar);
                        status = Status.Normal; triggerChar = '&';
                        temp = string.Empty;

                    }
                    else if (hexChar.Contains(currentChar))
                    {
                        temp += currentChar;
                    }
                    else if (currentChar == ';')
                    {

                        // Probing end. Decide whether to append the unicode
                        if (temp != string.Empty)
                        {
                            int value = Convert.ToInt32(temp, 16);
                            if (this.IsLegalXmlChar(value))
                            {
                                sb.Append("&#x");
                                sb.Append(temp);
                                sb.Append(';');
                            }
                        }
                        status = Status.Normal;
                        triggerChar = '&';
                        temp = string.Empty;

                    }
                    continue;
                }
                else if (status == Status.Normal)
                {

                    if (currentChar == triggerChar)
                    {
                        status = Status.ProbeUnicode;
                        probingChar = '#';
                        continue;
                    }
                    sb.Append(currentChar);
                }
            }


            return sb.ToString();
        }

        private bool IsLegalXmlChar(int character)
        {
            return
            (
                 character == 0x9 /* == '/t' == 9   */        ||
                 character == 0xA /* == '/n' == 10  */        ||
                 character == 0xD /* == '/r' == 13  */        ||
                (character >= 0x20 && character <= 0xD7FF) ||
                (character >= 0xE000 && character <= 0xFFFD) ||
                (character >= 0x10000 && character <= 0x10FFFF)
            );
        }
    }
    #region Model Class
    public class AssertionResult
    {
        public string Name { get; set; }
        public string Failure { get; set; }
        public string FailureMsg { get; set; }
        public string Error { get; set; }
    }
    public class HttpSample
    {
        public AssertionResult AssertionResult { get; set; }
        public string ResponseHeader { get; set; }
        public string RequestHeader { get; set; }
        public string ResponseData { get; set; }
        public string QueryString { get; set; }
        public string Url { get; set; }
        public string Method { get; set; }
        public string Cookies { get; set; }
        public string CGIName { get; set; }
        public string ReturnCode { get; set; }
        public string ResponseTime { get; set; }
        public string ThreadName { get; set; }
        public string ParentCGIName { get; set; }

    }

    #endregion
    #endregion
}
