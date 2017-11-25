using System;
using System.Collections.Generic;
using System.Windows.Forms;

namespace MiniCSharpLab
{
    static class Program
    {
        static bool bBatchMode = false;
        static string csPath = "";
        static string outPath = "";
        static bool bOverwrite = false;
        static int timeOutSecs = 0;

        static string analyzeArgs(string[] args) 
        {
            try
            {
                foreach (string arg in args)
                {
                    string lcArg = arg.ToLower();
                    if (lcArg.StartsWith("/batch"))
                        bBatchMode = true;
                    else if (lcArg.StartsWith("/cs:"))
                        csPath = arg.Substring(4);
                    else if (lcArg.StartsWith("/out:"))
                        outPath = arg.Substring(5);
                    else if (lcArg.StartsWith("/overwrite"))
                        bOverwrite = true;
                    else if (lcArg.StartsWith("/timeout:"))
                        timeOutSecs = int.Parse(lcArg.Substring(9));
                    //outPath support {yyyyMMddHHmmss} datetime presentation
                    if (outPath.IndexOf("{") > 0)
                        outPath = string.Format(outPath.Replace("{", "{0:"), DateTime.Now);
                }
                if (bBatchMode && (string.IsNullOrEmpty(csPath) || string.IsNullOrEmpty(outPath)))
                    throw new ApplicationException("Missing cs or out argument!");
                return "";
            }
            catch
            {
                return @"
Batch Mode Syntax:
MiniCSharpLab /batch /cs:x:\temp\test.cx /out:x:\temp\log.txt /overwrite
MiniCSharpLab /batch /cs:""c:\some path\test.cs"" /out:Log{yyyyMMddHHmmss}.txt
MiniCSharpLab /batch /cs:x:\aa.cs /out:x:\aa.txt /timeout:1200";
            }
        }


        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string msg = analyzeArgs(args);
            if (msg.Length > 0)
            {
                MessageBox.Show(msg);
                return;
            }
            Form1 frm = 
                (bBatchMode) ?
                new Form1(csPath, outPath, bOverwrite) :
                new Form1();
            if (bBatchMode && timeOutSecs > 0) //set timeout
                frm.SetExecutionTimeout(timeOutSecs);
            Application.Run(frm);
        }
    }
}
