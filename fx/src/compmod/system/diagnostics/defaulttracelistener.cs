//------------------------------------------------------------------------------
// <copyright file="DefaultTraceListener.cs" company="Microsoft">
//     
//      Copyright (c) 2006 Microsoft Corporation.  All rights reserved.
//     
//      The use and distribution terms for this software are contained in the file
//      named license.txt, which can be found in the root of this distribution.
//      By using this software in any fashion, you are agreeing to be bound by the
//      terms of this license.
//     
//      You must not remove this notice, or any other, from this software.
//     
// </copyright>                                                                
//------------------------------------------------------------------------------
#define DEBUG
#define TRACE
namespace System.Diagnostics {
    using System;
    using System.IO;
    using System.Text;
    using System.Collections;
    using System.Reflection;
    using System.Runtime.InteropServices;
    using System.Security.Permissions;
    using System.Security;
    using Microsoft.Win32;       
    using System.Globalization;
    using System.Runtime.Versioning;

    /// <devdoc>
    ///    <para>Provides
    ///       the default output methods and behavior for tracing.</para>
    /// </devdoc>
    [HostProtection(Synchronization=true)]
    public class DefaultTraceListener : TraceListener {    

        bool assertUIEnabled;        
        string logFileName;
        bool settingsInitialized;
        const int internalWriteSize = 16384;


        /// <devdoc>
        /// <para>Initializes a new instance of the <see cref='System.Diagnostics.DefaultTraceListener'/> class with 
        ///    Default as its <see cref='System.Diagnostics.TraceListener.Name'/>.</para>
        /// </devdoc>
        public DefaultTraceListener()
            : base("Default") {
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public bool AssertUiEnabled {
            get { 
                if (!settingsInitialized) InitializeSettings();
                return assertUIEnabled; 
            }
            set { 
                if (!settingsInitialized) InitializeSettings();
                assertUIEnabled = value; 
            }
        }

        /// <devdoc>
        ///    <para>[To be supplied.]</para>
        /// </devdoc>
        public string LogFileName {
            [ResourceExposure(ResourceScope.Machine)]
            [ResourceConsumption(ResourceScope.Machine)]
            get { 
                if (!settingsInitialized) InitializeSettings();
                return logFileName; 
            }
            [ResourceExposure(ResourceScope.Machine)]
            [ResourceConsumption(ResourceScope.Machine)]
            set { 
                if (!settingsInitialized) InitializeSettings();
                logFileName = value; 
            }
        }
        
        /// <devdoc>
        ///    <para>
        ///       Emits or displays a message
        ///       and a stack trace for an assertion that
        ///       always fails.
        ///    </para>
        /// </devdoc>
        public override void Fail(string message) {
            Fail(message, null);
        }

        /// <devdoc>
        ///    <para>
        ///       Emits or displays messages and a stack trace for an assertion that
        ///       always fails.
        ///    </para>
        /// </devdoc>
        public override void Fail(string message, string detailMessage) {            
            StackTrace stack = new StackTrace(true);
            int userStackFrameIndex = 0;
            string stackTrace;
            bool uiPermission = UiPermission;

            try {
                stackTrace = StackTraceToString(stack, userStackFrameIndex, stack.FrameCount - 1);
            }
            catch {
                stackTrace = "";
            }
            
            WriteAssert(stackTrace, message, detailMessage);
            if (AssertUiEnabled && uiPermission) {
                AssertWrapper.ShowAssert(stackTrace, stack.GetFrame(userStackFrameIndex), message, detailMessage);
            }
        }    

        [ResourceExposure(ResourceScope.Machine)]
        [ResourceConsumption(ResourceScope.Machine)]
        private void InitializeSettings() {
            // don't use the property setters here to avoid infinite recursion.
            assertUIEnabled = DiagnosticsConfiguration.AssertUIEnabled;
            logFileName = DiagnosticsConfiguration.LogFileName;
            settingsInitialized = true;
        }

        private void WriteAssert(string stackTrace, string message, string detailMessage) {
            string assertMessage = SR.GetString(SR.DebugAssertBanner) + "\r\n"
                                            + SR.GetString(SR.DebugAssertShortMessage) + "\r\n"
                                            + message + "\r\n"
                                            + SR.GetString(SR.DebugAssertLongMessage) + "\r\n" + 
                                            detailMessage + "\r\n" 
                                            + stackTrace;
            WriteLine(assertMessage);
        }

        [ResourceExposure(ResourceScope.None)]
        [ResourceConsumption(ResourceScope.Machine, ResourceScope.Machine)]
        private void WriteToLogFile(string message, bool useWriteLine) {
            try {
                FileInfo file = new FileInfo(LogFileName);
                using (Stream stream = file.Open(FileMode.OpenOrCreate)) {
                    using (StreamWriter writer = new StreamWriter(stream)) {
                        stream.Position = stream.Length;
                        if (useWriteLine)
                            writer.WriteLine(message);
                        else
                            writer.Write(message);
                    }
                }
            }
            catch (Exception e) {
                WriteLine(SR.GetString(SR.ExceptionOccurred, LogFileName, e.ToString()), false);
            }
            catch {
                WriteLine(SR.GetString(SR.ExceptionOccurred, LogFileName, ""), false);
            }
        }


        // Given a stack trace and start and end frame indexes, construct a
        // callstack that contains method, file and line number information.        
        private string StackTraceToString(StackTrace trace, int startFrameIndex, int endFrameIndex) {
            StringBuilder sb = new StringBuilder(512);
            
            for (int i = startFrameIndex; i <= endFrameIndex; i++) {
                StackFrame frame = trace.GetFrame(i);
                MethodBase method = frame.GetMethod();
                sb.Append("\r\n    at ");
                if (method.ReflectedType != null) {
                    sb.Append(method.ReflectedType.Name);
                }
                else {
                    // This is for global methods and this is what shows up in windbg. 
                    sb.Append("<Module>");
                }
                sb.Append(".");
                sb.Append(method.Name);
                sb.Append("(");
                ParameterInfo[] parameters = method.GetParameters();
                for (int j = 0; j < parameters.Length; j++) {
                    ParameterInfo parameter = parameters[j];
                    if (j > 0)
                        sb.Append(", ");
                    sb.Append(parameter.ParameterType.Name);
                    sb.Append(" ");
                    sb.Append(parameter.Name);
                }
                sb.Append(")  ");
                sb.Append(frame.GetFileName());
                int line = frame.GetFileLineNumber();
                if (line > 0) {
                    sb.Append("(");
                    sb.Append(line.ToString(CultureInfo.InvariantCulture));
                    sb.Append(")");
                }
            }
            sb.Append("\r\n");

            return sb.ToString();
        }

        /// <devdoc>
        ///    <para>
        ///       Writes the output to the OutputDebugString
        ///       API and
        ///       to System.Diagnostics.Debugger.Log.
        ///    </para>
        /// </devdoc>
        public override void Write(string message) {
            Write(message, true);
        }
         
        private void Write(string message, bool useLogFile) {    
            if (NeedIndent) WriteIndent();

            // really huge messages mess up both VS and dbmon, so we chop it up into 
            // reasonable chunks if it's too big
            if (message == null || message.Length <= internalWriteSize) {
                internalWrite(message);
            }
            else {
                int offset;
                for (offset = 0; offset < message.Length - internalWriteSize; offset += internalWriteSize) {
                    internalWrite(message.Substring(offset, internalWriteSize));
                }
                internalWrite(message.Substring(offset));
            }

            if (useLogFile && LogFileName.Length != 0)
                WriteToLogFile(message, false);
        }

        void internalWrite(string message) {
            if (Debugger.IsLogging()) {
                Debugger.Log(0, null, message);
            } else {
                if (message == null) 
                    SafeNativeMethods.OutputDebugString(String.Empty);            
                else
                    SafeNativeMethods.OutputDebugString(message); 
            }
        }

        /// <devdoc>
        ///    <para>
        ///       Writes the output to the OutputDebugString
        ///       API and to System.Diagnostics.Debugger.Log
        ///       followed by a line terminator. The default line terminator is a carriage return
        ///       followed by a line feed (\r\n).
        ///    </para>
        /// </devdoc>
        public override void WriteLine(string message) {
            WriteLine(message, true);
        }
        
        private void WriteLine(string message, bool useLogFile) {
            if (NeedIndent) WriteIndent();
            // I do the concat here to make sure it goes as one call to the output.
            // we would save a stringbuilder operation by calling Write twice.
            Write(message + "\r\n", useLogFile); 
            NeedIndent = true;
        }

        /// <devdoc>
        ///     It returns true if the current permission set allows an assert dialog to be displayed.
        /// </devdoc>
        private static bool UiPermission {
            get {
                bool uiPermission = false;

                try {
                    new UIPermission(UIPermissionWindow.SafeSubWindows).Demand();
                    uiPermission = true;
                }
                catch {
                }
                return uiPermission;
            }
        }
    }    
}
