using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using Manoli.Utils.CSharpFormat;

namespace MiniCSharpLab
{
  public partial class FormatCodeHtml : Form
  {

    /// <summary>
    /// Initializes a new instance of the <see cref="FormatCodeHtml"/> class.
    /// </summary>
    /// <param name="codeToFormat">The code to format.</param>
    /// <param name="defaultLanguage">The default language.</param>
    public FormatCodeHtml(string codeToFormat, Language defaultLanguage) {
      this.CodeToFormat = codeToFormat;
      InitializeComponent(); 

      switch (defaultLanguage) {
        case Language.CSharp:
          rbCSharp.Checked = true;
          break;
        case Language.VBNET:
          rbVB.Checked = true;
          break;
        case Language.Xml:
          rbHtml.Checked = true;
          break;
        case Language.JavaScript:
          rbJS.Checked = true;
          break;
        case Language.Msh:
          rbMsh.Checked = true;
          break;
        case Language.TSql:
          rbtsql.Checked = true;
          break;
        case Language.C:
          rbCSharp.Checked = true;
          break;
        default:
          rbCSharp.Checked = true;
          break;
      }
    }

    /// <summary>
    /// Handles the Click event of the btnCopy control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void btnCopy_Click(object sender, EventArgs e) {
      SourceFormat sf = null;

      if (rbCSharp.Checked) {
        sf = new CSharpFormat();
      } else if (rbVB.Checked) {
        sf = new VisualBasicFormat();
      } else if (rbtsql.Checked) {
        sf = new TsqlFormat();
      } else if (rbHtml.Checked) {
        sf = new HtmlFormat();
      } else if (rbJS.Checked) {
        sf = new JavaScriptFormat();
      } else if (rbMsh.Checked) {
        sf = new MshFormat();
      } else { return; }

      sf.TabSpaces = 4;
      sf.LineNumbers = cbLineNumbers.Checked;
      sf.EmbedStyleSheet = cbEmbedCss.Checked;
      sf.Alternate = cbAlternate.Checked;
      string formatedCode = sf.FormatCode(CodeToFormat);
      //Clipboard.SetText(formatedCode, TextDataFormat.Html);
      Clipboard.SetText(formatedCode);
    }

    /// <summary>
    /// Handles the Click event of the btnClose control.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="System.EventArgs"/> instance containing the event data.</param>
    private void btnClose_Click(object sender, EventArgs e) {
      btnCopy_Click(sender, e);
      this.Close();
    }

    /// <summary>
    /// Gets or sets the code to format.
    /// </summary>
    /// <value>The code to format.</value>
    public string CodeToFormat { get; set; }

  }
}
