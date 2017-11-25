using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Text;
using System.Windows.Forms;
using System.Diagnostics;
using System.Reflection;
using System.IO;
using System.CodeDom.Compiler;
using ICSharpCode.TextEditor.Actions;
using ICSharpCode.TextEditor.Document;
using Microsoft.CSharp;
using System.Text.RegularExpressions;
using Microsoft.VisualBasic;

/* 
 * Mini C# Lab 
 * Initiator: Jeffrey Lee, http://blog.darkthread.net
 * Contributor: Yitzchok
 * Ver 1.0 by Jeffrey
 *     Initial version, basic C# code editor and execution console
 * Ver 1.1 by Jeffrey
 *     1) Remove TERRIBLE Ctrl-X shortcut to exit
 *     2) Add //REFDLL syntax to add additional assembly references
 *     3) Add support to .NET 3.5
 * Ver 1.2 by Jeffrey
 *     1) Add suport to VB.NET
 * Ver 1.3 by adminjew (AKA Yitzchok), Jeffrey
 *     1) Replace RichTextBox+CSharpFormat with ICSharpCode.TextEditor
 *     2) Add toolbar and print, undo, redo, split code box, copy as html functions
 *     3) Terminiate execution by force while exiting
 *     4) Prompt to save modified code before exiting
 *     5) Remember saved file name
 * Ver 1.4 by Jeffrey
 *     1) Fix: Thread.Sleep for 0.5" after execution to avoid output loss
 *     2) Add: Add batch mode
 *     3) Add: Add execution duration display
====Batch Mode Syntax====
MiniCSharpLab /batch /cs:x:\temp\test.cx /out:x:\temp\log.txt /overwrite
MiniCSharpLab /batch /cs:""c:\some path\test.cs"" /out:Log{yyyyMMddHHmmss}.txt
MiniCSharpLab /batch /cs:x:\aa.cs /out:x:\aa.txt /timeout:1200
MiniCSharpLab /batch /vb:x:\aa.vb /out:x:\aa.txt /v35";
*/

namespace MiniCSharpLab
{
  public partial class Form1 : Form
  {

    #region Constructors

    public Form1() 
    {
      InitializeComponent();
    }

    bool _isBatchMode = false, _overwrite = false;
    string _csPath = "", _outPath = "";
    public Form1(string csPath, string outPath, bool overwrite) 
    {
        InitializeComponent();
        _isBatchMode = true;
        _csPath = csPath;
        _outPath = outPath;
        _overwrite = overwrite;
    }


    #endregion

    #region Member Variables

    private MemoryStream ms = new MemoryStream();
    private StreamWriter sw = null;
    private CSharpCodeProvider csp = new CSharpCodeProvider();
    private VBCodeProvider vbp = new VBCodeProvider();
    private CodeDomProvider cdp = null;
    private CompilerParameters cp = new CompilerParameters();
    private CompilerResults cr = null;
    private object codeObj = null;
    private System.Threading.Thread runThread = null;
    private TextWriter origConOut = Console.Out;
    private const int SPLIT_WIDTH = 50;

    #endregion

    #region Init Proc
    private string _titlePrefix = "";

    /// <summary>
    /// Handles the Load event of the Form1 control.
    /// <br />2008-08-07 Add batchmode logic
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void Form1_Load(object sender, EventArgs e) {
      //CSharp Formatter Initilaization
      rtbCode.SetHighlighting("C#");
      rtbCode.TabIndex = 4;
      sw = new StreamWriter(ms, Encoding.UTF8);
      string defaultSavePath = Path.Combine(
          Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "MiniC#Lab");
      if (!Directory.Exists(defaultSavePath))
        Directory.CreateDirectory(defaultSavePath);
      openFileDialog1.InitialDirectory = defaultSavePath;
      saveFileDialog1.InitialDirectory = openFileDialog1.InitialDirectory;
      //Set C# as default
      rbCS_CheckedChanged(null, null);
      //Default to load a new template
      mnuNew_Click(this, EventArgs.Empty);

      rtbCode.Document.DocumentChanged += new ICSharpCode.TextEditor.Document.DocumentEventHandler(Document_DocumentChanged);
      btnRedo.Enabled = false;
      btnUndo.Enabled = false;

      //set the title prefix
        Version ver = Assembly.GetExecutingAssembly().GetName().Version;
        _titlePrefix = string.Format("Mini C# Lab Ver {0}.{1} - ",
            ver.Major, ver.Minor);
      //set the form title
        this.Text = _titlePrefix + "New";

      //if batch mode, hide the form, auto load cs file and run it imediately
      if (_isBatchMode)
      {
          this.WindowState = FormWindowState.Minimized;
          this.ShowInTaskbar = false;
          try
          {
              //if overwrite, create a new log file
              if (_overwrite)
              {
                  using (StreamWriter lg = new StreamWriter(_outPath))
                  {
                      lg.Close();
                  }
              }
              output(Color.Black, "{1}{1}{2}{1}[Mini C# Lab Batch Mode] {0:yyyy/MM/dd HH:mm:ss}{1}", DateTime.Now, Environment.NewLine, new string('*', SPLIT_WIDTH));
              rtbCode.Text = File.ReadAllText(_csPath);
              //initial a new log file

          }
          catch (Exception ex)
          {
              try
              {
                  outputError(ex.Message);
              }
              catch
              {
                  MessageBox.Show(ex.Message);
              }
              this.Close();
              return;
          }
          btnRun_Click(btnRun, EventArgs.Empty);
      }
    }

    void Document_DocumentChanged(object sender, DocumentEventArgs e) {
      if (e.Document.UndoStack.CanRedo) {
        btnRedo.Enabled = true;
      } else {
        btnRedo.Enabled = false;
      }

      if (e.Document.UndoStack.CanUndo) {
        btnUndo.Enabled = true;
      } else {
        btnUndo.Enabled = false;
      }
    }

    #endregion

    #region Dynamic compiler
    string splitLine = new string('=', SPLIT_WIDTH);

    /// <summary>
    /// Compiles and Runs the specified code.
    /// </summary>
    /// <param name="code">The code.</param>
    public void Run(string code) {
      timer1.Enabled = false;

      rtbOutput.Clear();
      rtbOutput.SelectionColor = Color.Brown;

      cp.ReferencedAssemblies.Clear();
      //2008-05-29 by Jeffrey
      //Support additional reference
      //REFDLL System.Web;System.Data.OracleClient

      cp.ReferencedAssemblies.Add("System.dll");
      //Add ref to Linq by default when use .NET 3.5
      if (rbNET35.Checked) {
        //cp.ReferencedAssemblies.Add("System.Linq.dll");
        cp.ReferencedAssemblies.Add("System.Data.Linq.dll");
        cp.ReferencedAssemblies.Add("System.Xml.Linq.dll");
        cp.ReferencedAssemblies.Add("System.Core.dll");
      }

      Match m = null;
      if ((m = Regex.Match(code, "(?ims)^[/']{2}REFDLL (?<ref>.+?)$")).Success) {
        foreach (string refDll in m.Groups["ref"].Value.Split(new char[] { ';', ',' })) {
          //2008-06-18 by Jeffrey, remove redundant \r
          string mdfyRefDll = refDll.Replace("\r", "").Replace("\n", "");
          //trim the ending .dll if exists
          if (mdfyRefDll.ToLower().EndsWith(".dll"))
              mdfyRefDll = mdfyRefDll.Substring(0, mdfyRefDll.Length - 4);
          string lcRefDll = mdfyRefDll.ToLower();
          if (lcRefDll == "system.data.linq" || lcRefDll == "system"
              || lcRefDll == "system.xml.linq" || lcRefDll == "system.core")
            continue;
          cp.ReferencedAssemblies.Add(mdfyRefDll + ".dll");
        }
      }

      cp.GenerateInMemory = true;

      Stopwatch timer = new Stopwatch();
      timer.Start();
      //use CodeDomProvider, support both C# and VB.NET
      cdp = rbVB.Checked ? (CodeDomProvider)vbp : csp;
      cr = cdp.CompileAssemblyFromSource(cp, code);
      timer.Stop();

      Console.SetOut(sw);

      if (cr.Errors.HasErrors) {
        output(Color.Red, String.Format("Compilation Errors:{0}", Environment.NewLine));
        for (int x = 0; x < cr.Errors.Count; x++)
          output(Color.Red, String.Format("Line: {0} Column {2} - {1}{3}", cr.Errors[x].Line, cr.Errors[x].ErrorText, cr.Errors[x].Column, Environment.NewLine));
        Console.SetOut(origConOut);
        //2008-09-01 by Jeffrey
        //close the application if failed to compile in batch mode
        if (_isBatchMode) this.Close();
        return;
      }

      output(Color.Green, String.Format("Built successfully in {{0:N0}}ms!{0}", Environment.NewLine), timer.ElapsedMilliseconds);
      output(Color.Black, String.Format("Prepare to run...{0}{1}{0}", Environment.NewLine, splitLine));

      rtbOutput.SelectionColor = Color.Blue;

      try {
        Assembly asm = cr.CompiledAssembly;
        codeObj = asm.CreateInstance("CSharpLab");
        if (codeObj == null) {
          outputError("Class 'CSharpLab' not found!");
          Console.SetOut(origConOut);
          return;
        }
      } catch (Exception ex) {
        outputError(String.Format("Runtime error!{0}", Environment.NewLine) + ex.Message);
      }

      runThread = new System.Threading.Thread(new System.Threading.ThreadStart(threadRun));
      timer1.Enabled = true;
      runThread.Start();
    }

    /// <summary>
    /// Threads the run.
    /// </summary>
    private void threadRun() {
      sw.Flush();
      ms.SetLength(0);
      try {
          Stopwatch dura = new Stopwatch();
          dura.Start();
        object[] para = new object[] { };
        codeObj.GetType().InvokeMember("Test", BindingFlags.InvokeMethod, null, codeObj, para);
        dura.Stop();
        //2008-07-21 by Jeffrey
        //Sleep for 0.5 ms to wait for Console.Output flush
        System.Threading.Thread.Sleep(500);
        output(Color.Green, string.Format("{0}{1}{0}", Environment.NewLine, splitLine));
        output(Color.Green, string.Format("Execution time: {0:N0} ms", dura.ElapsedMilliseconds));
      } catch (System.MissingMethodException mme) {
        outputError("Method CSharpLab.Test() is missing!");
      } catch (System.Threading.ThreadAbortException tae) {
        //Do nothing
      } catch (Exception ex) {
        outputError(String.Format("Runtime error!{0}{1}", Environment.NewLine, ex.InnerException.Message));
        //outputError(String.Format("Runtime error!{0}", Environment.NewLine) + ex.Message);
      }
    }

    /// <summary>
    /// Outputs the error.
    /// </summary>
    /// <param name="errMsg">The err MSG.</param>
    private void outputError(string errMsg) {
      output(Color.Red, "ERROR: " + errMsg);
    }

    private delegate void SetOutputCallback(Color color, string text);

    /// <summary>
    /// Outputs the specified text with the specified color.
    /// <br />2008-08-07 output to log file in batch mode
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="text">The text.</param>
    private void output(Color color, string text) {
        //write the output text to log file in batch mode
        if (_isBatchMode)
        {
            lock (this)
            {
                using (StreamWriter sw = new StreamWriter(_outPath, true))
                {
                    sw.Write(text);
                    sw.Close();
                }
                return;
            }
        }

      if (this.InvokeRequired) {
        SetOutputCallback deleg = new SetOutputCallback(output);
        this.Invoke(deleg, new object[] { color, text });
      } else {
        rtbOutput.SelectionStart = rtbOutput.Text.Length;
        rtbOutput.SelectionColor = color;
        rtbOutput.AppendText(text);
      }
    }

    /// <summary>
    /// Outputs the specified color.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="textToFormat">The text to format.</param>
    /// <param name="args">The args.</param>
    private void output(Color color, string textToFormat, params object[] args) {
      output(color, string.Format(textToFormat, args));
    }

    /// <summary>
    /// Outputs the specified color.
    /// </summary>
    /// <param name="color">The color.</param>
    /// <param name="stream">The stream.</param>
    private void output(Color color, MemoryStream stream) {
      output(color, System.Text.Encoding.UTF8.GetString(stream.ToArray()));
      stream.SetLength(0);
    }

    /// <summary>
    /// Stops the execution.
    /// </summary>
    private void StopExecution() {
      if (runThread != null && runThread.IsAlive) {
        runThread.Abort();
        output(Color.Red, String.Format("{0}Execution is cancelled by user!", Environment.NewLine));
      }
    }

    /// <summary>
    /// Setups the .NET Version for the compiler.
    /// </summary>
    /// <param name="compilerVersion">The compiler version.</param>
    public void SetupDotNetVersion(string compilerVersion) {
      Dictionary<string, string> initP = new Dictionary<string, string>();
      initP.Add("CompilerVersion", compilerVersion);
      csp = new CSharpCodeProvider(initP);
      vbp = new VBCodeProvider(initP);
    }

    #endregion

    #region Code Modification

    //For code modification judgement
    private int _origCodeLength = 0;//This should be a lot faster so check this first
    private int _origHashOfCode = 0;

    /// <summary>
    /// Prompts the save.
    /// </summary>
    private void PromptSave() {
      if (CodeHasChanged) {
        if (MessageBox.Show("Do you want to save the current code?", "Code Modified", MessageBoxButtons.YesNo)
            == DialogResult.Yes)
          mnuSave_Click(null, null);
      }
    }

    /// <summary>
    /// Gets a value indicating whether [code has changed].
    /// </summary>
    /// <value><c>true</c> if [code has changed]; otherwise, <c>false</c>.</value>
    public bool CodeHasChanged {
      get {
        int rtbCodeTextLength = rtbCode.Text.Length;
        return (_origCodeLength != rtbCodeTextLength || (rtbCodeTextLength > 0 && rtbCode.Text.GetHashCode() != _origHashOfCode));
      }
    }

    /// <summary>
    /// Remembers the original code.
    /// </summary>
    private void RememberCurrentCode() {
      //remember the original code
      _origCodeLength = rtbCode.Text.Length;
      _origHashOfCode = rtbCode.Text.GetHashCode();
    }

    #endregion

    #region Event Handlers

    #region Menu

    private void mnuNew_Click(object sender, EventArgs e) {
      PromptSave();
      string fileName = String.Format("MiniCSharpLab.CodeTemplate.{0}", rbCS.Checked ? "cs" : "vb");

      using (StreamReader sr = new StreamReader(Assembly.GetExecutingAssembly().GetManifestResourceStream(fileName))) {
        rtbCode.LoadFile(fileName, sr.BaseStream, true, true);
      }
      RememberCurrentCode();
      SaveName = "New";
    }

    private void mnuOpen_Click(object sender, EventArgs e) {
      PromptSave();
      openFileDialog1.FileName = "";
      if (openFileDialog1.ShowDialog() == DialogResult.OK) {
        using (Stream s = openFileDialog1.OpenFile()) {
          rtbCode.LoadFile(openFileDialog1.FileName, openFileDialog1.OpenFile(), true, true);
        }
        RememberCurrentCode();
        SaveName = openFileDialog1.FileName;
      }
    }

    private string _saveName = null;

    public string SaveName
    {
        get { return _saveName; }
        set 
        { 
            _saveName = value;
            this.Text = _titlePrefix + Path.GetFileName(_saveName);
        }
    }

    private void mnuSave_Click(object sender, EventArgs e) {
        if (_saveName==null || _saveName == "New")
            _saveName = string.Format("Lab{0:yyyyMMdd}.cs", DateTime.Now);
      saveFileDialog1.FileName = _saveName;
      if (saveFileDialog1.ShowDialog() == DialogResult.OK) {
        string file = saveFileDialog1.FileName;
        File.WriteAllText(file, rtbCode.Text);
        SaveName = file;
        
      }
      RememberCurrentCode();
    }

    private void mnuExit_Click(object sender, EventArgs e) {
      btnStop_Click(null, null);
      this.Close();
    }

    private void aboutToolStripMenuItem_Click(object sender, EventArgs e) {
      Process.Start("http://www.darkthread.net/minicsharplab/");
    }

    #endregion

    #region Button actions

    private void btnRun_Click(object sender, EventArgs e) {
      ResetReadyMode(true);
      Run(rtbCode.Text);
      if (!timer1.Enabled) //if code is not running
        ResetReadyMode(false);
    }

    private void btnStop_Click(object sender, EventArgs e) {
      StopExecution();
      ResetReadyMode(false);
    }

    /// <summary>
    /// Resets the ready mode.
    /// </summary>
    /// <param name="run"></param>
    private void ResetReadyMode(bool run) {
      if (run) {
        btnRun.Enabled = false;
        btnStop.Enabled = true;
        tsbtnStop.Enabled = true;
        tsbtnRun.Enabled = false;
      } else {
        timer1.Enabled = false;
        btnRun.Enabled = true;
        btnStop.Enabled = false;
        tsbtnStop.Enabled = false;
        tsbtnRun.Enabled = true;
        Console.SetOut(origConOut);
      }
    }

    #endregion

    #region Toolbar Events

    private void btnUndo_ButtonClick(object sender, EventArgs e) {
      rtbCode.Undo();
    }

    private void btnRedo_ButtonClick(object sender, EventArgs e) {
      rtbCode.Redo();
    }

    private void cutToolStripButton_Click(object sender, EventArgs e) {
      ExecuteIEditAction(new Cut());
    }

    private void copyToolStripButton_Click(object sender, EventArgs e) {
      ExecuteIEditAction(new Copy());
    }

    private void pasteToolStripButton_Click(object sender, EventArgs e) {
      ExecuteIEditAction(new Paste());
    }

    private void btnSplitCodeBox_Click(object sender, EventArgs e) {
      rtbCode.Split();
    }

    /// <summary>
    /// Handles the Click event of the printToolStripButton control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void printToolStripButton_Click(object sender, EventArgs e) {
      using (PrintDocument document = rtbCode.PrintDocument) {
        if (document != null) {
          using (PrintDialog dialog = new PrintDialog()) {
            dialog.AllowSomePages = true;
            dialog.Document = document;
            if (dialog.ShowDialog(this) == DialogResult.OK) {
              document.Print();
            }
          }
        }
      }
    }

    private void btnCopyAsHtml_Click(object sender, EventArgs e) {
      CopyTextAsHtml(rtbCode.Text);
    }

    #endregion

    private void copyAsToolStripMenuItem_Click(object sender, EventArgs e)
    {
      if (rtbCode.ActiveTextAreaControl.SelectionManager != null)
        if (rtbCode.ActiveTextAreaControl.SelectionManager.SelectedText != null)
          CopyTextAsHtml(rtbCode.ActiveTextAreaControl.SelectionManager.SelectedText);
    }

    private void rbNET20_CheckedChanged(object sender, EventArgs e) {
      if (rbNET20.Checked) {
        SetupDotNetVersion("v2.0");
      }
    }

    private void rbNET35_CheckedChanged(object sender, EventArgs e) {
      if (rbNET35.Checked) {
        SetupDotNetVersion("v3.5");
      }
    }

    private void timer1_Tick(object sender, EventArgs e) {
      sw.Flush();
      if (ms.Length > 0)
        output(Color.Blue, ms);
      if (!runThread.IsAlive)
      {
          ResetReadyMode(false);
          //if in batch mode, when thread stop, close this form
          if (_isBatchMode) this.Close();
      }
    }

    private void rbCS_CheckedChanged(object sender, EventArgs e) {
      if (rbCS.Checked) {
        //cdp = csp;
        gbCode.Text = "C# Code";
        mnuNew_Click(null, null);
        openFileDialog1.Filter = "C# (*.cs)|*.cs|All Files (*.*)|*.*";
        saveFileDialog1.Filter = openFileDialog1.Filter;
        ChangeHighlightingStrategy("C#");
      }
    }

    private void rbVB_CheckedChanged(object sender, EventArgs e) {
      if (rbVB.Checked) {
        //cdp = vbp;
        gbCode.Text = "VB.NET Code";
        mnuNew_Click(null, null);
        openFileDialog1.Filter = "VB.NET (*.vb)|*.vb|All Files (*.*)|*.*";
        saveFileDialog1.Filter = openFileDialog1.Filter;
        ChangeHighlightingStrategy("VBNET");
      }
    }

      /// <summary>
      /// Set to use VB.NET
      /// </summary>
    public void SetVbNet()
    {
        rbVB.Checked = true;
    }

    private void Form1_FormClosing(object sender, FormClosingEventArgs e) {
        //Make sure to stop the execution before we exit the application.
        StopExecution();
        //2008-06-16 by Jeffrey
        //if code is modified and not saved, prompt
        if (!_isBatchMode) PromptSave();
    }
    
    #endregion

    #region Code Formatting

    /// <summary>
    /// Changes the highlighting strategy.
    /// </summary>
    /// <param name="language">The language.</param>
    private void ChangeHighlightingStrategy(string language) {
      rtbCode.Document.HighlightingStrategy = HighlightingStrategyFactory.CreateHighlightingStrategy(language);
    }

    #endregion

    #region Helpers

    /// <summary>
    /// Executes the edit action.
    /// </summary>
    /// <param name="command">The command.</param>
    private void ExecuteIEditAction(IEditAction command) {
      command.Execute(rtbCode.ActiveTextAreaControl.TextArea);
    }

    /// <summary>
    /// Copies the text as HTML.
    /// </summary>
    /// <param name="code">The code.</param>
    private void CopyTextAsHtml(string code) {
      Language currentLang = Language.CSharp;
      if (rbVB.Checked)
        currentLang = Language.VBNET;

      FormatCodeHtml fcHtml = new FormatCodeHtml(code, currentLang);
      fcHtml.ShowDialog(this);
    }

    #endregion


    #region execution timeout
    private int _timeoutCountDown = -1;
    private int _timeout = 0;
    public void SetExecutionTimeout(int secs)
    {
        _timeoutCountDown = _timeout = secs;
        tmrExecTimeout.Enabled = true;
    }

    /// <summary>
    /// Use this timer to set execution timeout
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private void tmrExecTimeout_Tick(object sender, EventArgs e)
    {
        _timeoutCountDown--;
        if (_timeoutCountDown == 0)
        {
            outputError(
                string.Format("{1}{2}{1}Execution timeout -- {0:N0} secs!", _timeout, Environment.NewLine, splitLine)
                );
            StopExecution();
        }
    }
    #endregion
  }
}
