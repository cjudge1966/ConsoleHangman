using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CJUtil
{
    /// <summary>
    /// A general purpose logfile/file writer.
    /// </summary>
    public class LogFile : IDisposable {
        protected bool _open = false;
        public DateTime _today = DateTime.Today;

        protected string _currentFilename;
        public string CurrentFilename
        {
            get
            {
                return _currentFilename;
            }
            set
            {
                _currentFilename = value;
                _currentFullname = CurrentPath + CurrentFilename;
            }
        }

        protected string _currentPath;
        public string CurrentPath
        {
            get
            {
                return _currentPath;
            }
            set
            {
                _currentPath = value;
                _currentFullname = CurrentPath + CurrentFilename;
            }
        }

        protected string _currentFullname;
        public string CurrentFullname
        {
            get
            {
                return _currentFullname;
            }
        }

        public bool EchoToConsole; 
        protected string _rawFilename = "";
        protected string _rawPath = "";
        protected int _totalIndent = 0;
        protected bool _includeTimestamp = false;
        protected bool _includeHeader = false;
        protected bool _managed = false;
        protected int _maxAge = 0;
        protected bool _append = false;
        protected System.Windows.Forms.TextBox _textbox = null;
        protected AutoResetEvent _available = new AutoResetEvent(true);


        /* Creates (or appends to an existing) logfile and initializes private data so that the object instance can
         * be used to add output to the logfile.  Writes are "atomic" - the output file is closed and flushed after each write.
         * This may reduce performance - though OS buffering will minimize that - but increases the chances that output
         * will be written as requested.
         * 
         * Inputs:
         *  filename        the fully qualified path and filename of the logfile
         *  maxAge          if logfile is managed, this is the number of days to age a managed file before 
         *                  automatically deleting it. Use -1 for no automatic deletion of aged files.
         *  includeHeader   if true, include a short initialization header.
         *  IncludeTimeStamp if true, timestamp each line of data written to the log.
         *  AppendToExisting if true, append output to existing file.  if false, force creation of new file.
         *  echoToConsole   if true, echo all output to console (stdout)
         *  TextBox         A Windows Forms Textbox to echo the output.
         */

        public LogFile (
          string filename)
            : this(filename, -1, false, false, false, false, null) { }

        public LogFile (
          string filename,
          int maxAge)
            : this(filename, maxAge, false, false, false, false, null) { }

        public LogFile (
          string filename,
          int maxAge,
          bool includeHeader)
            : this(filename, maxAge, includeHeader, false, false, false, null) { }

        public LogFile (
          string filename,
          int maxAge,
          bool includeHeader,
          bool includeTimeStamp)
            : this(filename, maxAge, includeHeader, includeTimeStamp, false, false, null) { }

        public LogFile (
          string filename,
          int maxAge,
          bool includeHeader,
          bool includeTimeStamp,
          bool appendToExisting)
            : this(filename, maxAge, includeHeader, includeTimeStamp, appendToExisting, false, null) { }

        public LogFile(
          string filename,
          int maxAge,
          bool includeHeader,
          bool includeTimeStamp,
          bool appendToExisting,
          bool echoToConsole)
            : this(filename, maxAge, includeHeader, includeTimeStamp, appendToExisting, echoToConsole, null) { }

        public LogFile(
          string namePattern,
          int maxAge,
          bool includeHeader,
          bool includeTimeStamp,
          bool appendToExisting,
          bool echoToConsole,
          System.Windows.Forms.TextBox textbox) {
            _rawFilename = namePattern.Substring(namePattern.LastIndexOf("\\") + 1);
            _rawPath = namePattern.Substring(0, namePattern.LastIndexOf("\\") + 1);

            if (_rawPath == "") {
                _rawPath = ".\\";
            }

            _includeTimestamp = includeTimeStamp;
            _includeHeader = includeHeader;
            _append = appendToExisting;
            EchoToConsole = echoToConsole;

            if (_rawFilename.Contains("%date%")
                || _rawFilename.Contains("%year%")
                || _rawFilename.Contains("%month%")
                || _rawFilename.Contains("%day%")
                || _rawFilename.Contains("%time%")) {
                _maxAge = maxAge;
                _managed = true;
            }
            else {
                _maxAge = -1;
            }
    
            if (textbox != null) {
                _textbox = textbox;
            }

            _open = true;
            _today = System.DateTime.Today;

            createFile();

            if (_includeHeader) {
                StartBlock();

                WriteLine("Logfile = " + CurrentFilename);
                WriteLine("Time = " + DateTime.Now.ToString());
                WriteLine("Machine name = " + System.Environment.MachineName);
                WriteLine("OS = " + System.Environment.OSVersion.ToString());
                WriteLine("User = " + System.Environment.UserName);

                EndBlock();
            }
        }

        protected virtual string NEWLINE {
            get {
                return "\r\n";
            }
        }

        protected virtual string SPACE {
            get {
                return " ";
            }
        }
        private string ResolveTokens(string input)
        {
            return input.
                Replace("%year%", DateTime.Now.ToString("yyyy")).
                Replace("%month%", DateTime.Now.ToString("MM")).
                Replace("%day%", DateTime.Now.ToString("dd")).
                Replace("%date%", DateTime.Now.ToString("yyyy-MM-dd")).
                Replace("%time%", DateTime.Now.ToString("HHmmsszzz"));
        }

        private string SearchTokens(string input)
        {
            return input.
                Replace("%date%", "????-??-??").
                Replace("%year%", "????").
                Replace("%month%", "??").
                Replace("%day%", "??").
                Replace("%time%", "?????????");
        }

        protected void createFile () { 
            // update filename components
            CurrentFilename = ResolveTokens(_rawFilename);
            CurrentPath = ResolveTokens(_rawPath);

            // make sure directory exists
            Directory.CreateDirectory(CurrentPath);

            // manage old files
            if (_maxAge > -1) {
                string fileSearch = SearchTokens(_rawFilename);
                foreach (var f in Directory.GetFiles(CurrentPath, fileSearch).
                    OrderByDescending(x => File.GetCreationTime(x))
                    .Take(_maxAge))
                {

                    File.Delete(f);
                }
            }

            FileMode mode;
            if (_append) {
                mode = System.IO.FileMode.Append;
            }
            else {
                mode = System.IO.FileMode.Create;
            }

            FileStream Stream;
            try {
                Stream = File.Open(_currentFilename, mode);
            }
            catch {
                throw;
            }

            Stream.Close();
            Stream = null;
        }

        /// <summary>
        /// Copy the logfile to another location.  After the copy this object still points to original location.
        /// </summary>
        /// <param name="destPath">The destination path.</param>
        public void CopyTo (string destPath) {
            if (destPath.Substring(destPath.Length - 1) != "\\") {
                destPath += "\\";
            }
            
            try {
                File.Copy(_currentFullname, destPath + CurrentFilename, true);
            }
            catch {
                throw;
            }
        }


        /// <summary>
        /// Move the logfile to another location.  After move, this object references the new location and the original file has been deleted.
        /// </summary>
        /// <param name="destPath">The destination path.</param>
        public void MoveTo (string destPath) {
            if (destPath.Substring(destPath.Length - 1) != "\\") {
                destPath += "\\";
            }
            
            try {
                File.Move(_currentFullname, destPath + CurrentFilename);
                CurrentPath = destPath;
            }
            catch {
                throw;
            }
        }


        /// <summary>
        /// Copy the logfile to another location.  After the copy the original location still exists, but this object now references the new location.
        /// </summary>
        /// <param name="destPath">The destination path.</param>
        public void CopyToAndMove (string destPath) {
            if (destPath.Substring(destPath.Length - 1) != "\\") {
                destPath += "\\";
            }
            
            try {
                File.Copy(_currentFullname, destPath + CurrentFilename, true);
                CurrentPath = destPath;
            }
            catch {
                throw;
            }
        }


        protected string callerName (int offset) {
            try
            {
                var t = new System.Diagnostics.StackTrace();
                var f = t.GetFrame(offset);
                var m = f.GetMethod();
                return m.DeclaringType.Name + "." + m.Name;
            }
            catch
            {
                return "Unknown.Unknown";
            }
        }

        /// <summary>
        /// Begin an indented block that identifies the caller.
        /// </summary>
        /// <param name="stackOffset">Offset in the call stack to identify the callee.</param>
        public void StartBlock (int stackOffset) {
            WriteLine(2, "START: " + callerName(stackOffset));
        }

        /// <summary>
        /// Begin an indented block that identifies the caller.
        /// </summary>
        public void StartBlock () {
            StartBlock(2);
        }

        /// <summary>
        /// Begin an indented block that identifies the caller and includes an extra message.
        /// </summary>
        /// <param name="message">The extra message to display.</param>
        /// <param name="stackOffset">Offset in the call stack to identify the callee.</param>
        public void StartBlock (string message, int stackOffset) {
            WriteLine(2, "START: " + callerName(stackOffset) + ": " + message);
        }

        /// <summary>
        /// Begin an indented block that identifies the caller and includes an extra message.
        /// </summary>
        /// <param name="message">The extra message to display.</param>
        public void StartBlock (string message) {
            StartBlock(message, 2);
        }

        /// <summary>
        /// Close an indented block.
        /// </summary>
        /// <param name="stackOffset">Offset in the call stack to identify the caller.</param>
        public void EndBlock (int stackOffset) {
            WriteLine(-2, "END: " + callerName(stackOffset));
        }

        /// <summary>
        /// Close an indented block.
        /// </summary>
        public void EndBlock () {
            EndBlock(2);
        }

        /// <summary>
        /// Close an indented block with an extra message.
        /// </summary>
        /// <param name="message">The extra message.</param>
        /// <param name="stackOffset">Offset in the call stack to identify the callee.</param>
        public void EndBlock (string message, int stackOffset) {
            WriteLine(-2, "END: " + callerName(stackOffset) + ": " + message);
        }

        /// <summary>
        /// Close an indented block with an extra message.
        /// </summary>
        /// <param name="message">The extra message.</param>
        public void EndBlock (string message) {
            EndBlock(message, 2);
        }

        /// <summary>
        /// Write to the log file without a terminating newline.
        /// </summary>
        /// <param name="Data">The data to write.</param>
        public void Write (string Data) {
            Write(0, Data);
        }

        public void Write(string format, params object[] args)
        {
            Write(0, string.Format(format, args));
        }

        /// <summary>
        /// Write to the log file and terminate with a newline.
        /// </summary>
        /// <param name="Data">The data to write.</param>
        public void WriteLine (string Data) {
            Write(0, Data + NEWLINE);
        }

        public void WriteLine(string format, params object[] args)
        {
            Write(0, string.Format(format, args) + NEWLINE);
        }

        /// <summary>
        /// Write to the log file and terminate with a newline.
        /// </summary>
        /// <param name="Data">The data to write.</param>
        /// <param name="Indent">The amount to indent this line.</param>
        public void WriteLine (int Indent, string Data) {
            Write(Indent, Data + NEWLINE);
        }

        /// <summary>
        /// Write to the log file.
        /// </summary>
        /// <param name="Data">The data to write.</param>
        /// <param name="Indent">Indent/outdent value.</param>
        public void Write (int Indent, string Data) {
            if (_open) {
                _available.WaitOne();

                if (_managed && DateTime.Today != _today) {
                    _today = DateTime.Today;
                    WriteLine("");
                    WriteLine("END OF DAY - SWITCHING TO NEW MANAGED LOG FILE");

                    createFile();

                    WriteLine("BEGIN DAY - CONTINUED FROM PREVIOUS MANAGED LOG FILE");
                    WriteLine("");
                }

                outdent(Indent);

                using (var Stream = new StreamWriter(_currentFullname, true)) {
                    var Output = new StringBuilder();

                    if (_includeTimestamp) {
                        Output.Append(DateTime.Now.ToString("HH:mm:ss "));
                    }

                    for (int i = 0; i < _totalIndent; i++) {
                        Output.Append(SPACE);
                    }
                    Output.Append(Data);

                    if (_textbox != null) {
                        AppendText(Output.ToString());
                    }

                    if (EchoToConsole)
                    {
                        Console.Write(Output.ToString());
                    }

                    Stream.Write(Output.ToString());
                    Stream.Flush();
                }

                indent(Indent);

                _available.Set();
            }
        }

        /// <summary>
        /// Bypass additional formatting and dump message directly to output stream (and Console,
        /// if echoToConsole is set).
        /// </summary>
        /// <param name="message"></param>
        private void Debug(string message)
        {
            using (var Stream = new StreamWriter(_currentFullname, true))
            {
                var Output = new StringBuilder();

                if (_includeTimestamp)
                {
                    Output.Append(DateTime.Now.ToString("HH:mm:ss "));
                }

                Output.Append("DEBUG: " + message + NEWLINE);

                if (EchoToConsole)
                {
                    Console.Write(Output.ToString());
                }

                Stream.Write(Output.ToString());
                Stream.Flush();
            }
        }

        /// <summary>
        /// Write an exception block to the log file.
        /// </summary>
        /// <param name="e"></param>
        public void Exception (Exception e) {
            StartBlock("EXCEPTION");
            WriteLine("Message: " + e.Message);
            WriteLine("Source: " + e.Source);

            var inner = e.InnerException;
            while (inner != null)
            {
                StartBlock("Inner Exception");
                WriteLine("Message: " + inner.Message);
                WriteLine("Source: " + inner.Source);
                EndBlock("Inner Exception");

                inner = inner.InnerException;
            }

            WriteLine("StackTrace: " + e.StackTrace);
            EndBlock("EXCEPTION");
        }

        /// <summary>
        /// Write an error block to the log file.
        /// </summary>
        /// <param name="e"></param>
        public void Error (string message) {
            StartBlock("ERROR");
            WriteLine("Message: " + message);
            EndBlock("ERROR");
        }

        /// <summary>
        /// Write an error block to the log file.
        /// </summary>
        /// <param name="e"></param>
        public void Warning (string message) {
            StartBlock("WARNING");
            WriteLine("Message: " + message);
            EndBlock("WARNING");
        }

        /// <summary>
        /// Increase total indent by val, cap at 20.
        /// </summary>
        /// <param name="val"></param>
        protected void indent (int val) {
            if (val > 0) {
                _totalIndent += val;
                if (_totalIndent > 20) {
                    _totalIndent = 20;
                }
            }
        }

        /// <summary>
        /// Decrease total indent by val, cap at zero.
        /// </summary>
        /// <param name="val"></param>
        protected void outdent (int val) {
            if (val < 0) {
                _totalIndent += val;
                if (_totalIndent < 0) {
                    _totalIndent = 0;
                }
            }
        }

        /// <summary>
        /// Left-pad text with spaces to be centered on width characters.
        /// </summary>
        /// <param name="text"></param>
        /// <param name="width"></param>
        /// <returns></returns>
        protected string CenterText (string text, int width) {
            if (text.Length >= width) {
                return text;
            }
            else {
                width = width / 2;
                return new string(' ', width - (text.Length / 2)) + text;
            }
        }

        /// <summary>
        /// The WinForms textbox control to which the log file will echo its output.
        /// </summary>
        public System.Windows.Forms.TextBox Textbox {
            get {
                return _textbox;
            }
            set {
                _textbox = value;
            }
        }

        
        /// <summary>
        /// Display the logfile in the system registered viewer.
        /// </summary>
        [EnvironmentPermissionAttribute(SecurityAction.LinkDemand, Unrestricted = true)]
        public void View () {
            System.Diagnostics.Process.Start(_currentFullname);
        }

        public delegate void delegateAppendText (string text);

        /// <summary>
        /// Append text to the end of the textbox.
        /// </summary>
        /// <param name="text"></param>
        private void AppendText (string text) {
            if (_textbox.InvokeRequired) {
                _textbox.Invoke(new delegateAppendText(this.AppendText), new Object[] { text });
            }
            else {
                if (_textbox.Text.Length > 20000) {
                    _textbox.Text = _textbox.Text.Substring(20000);
                }
                _textbox.AppendText(text);
            }
        }

        /// <summary>
        /// Close the resources used by the log file.
        /// </summary>
        public void Close () {
            _open = false;
            if (null != _available) {
                _available.Close();
                _available = null;
            }

            _textbox = null;
        }

        private bool disposed = false;

        public void Dispose () {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose (bool disposing) {
            if (!this.disposed) {
                if (disposing) {
                    Close();
                }

                disposed = true;
            }
        }

        ~LogFile () {
            Dispose(false);
        }
    }
}
