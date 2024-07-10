using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using log4net;
using log4net.Config;
using log4net.Appender;
using log4net.Layout;
using log4net.Repository.Hierarchy;
using log4net.Core;

namespace SkyrimPluginTextEditor
{
    public class Logger
    {
        public enum LogLevel {
            None,
            Debug, 
            Info, 
            Warning, 
            Error,
            Fatal,
            Max
        }
        private static readonly Lazy<Logger> _instance = new Lazy<Logger>(() => new Logger());
        public ILog Logging = null;
        public static Logger Instance { get { return _instance.Value; } }
        public static ILog Log { get { return Instance.Logging; } }
        private Logger()
        {
            if (File.Exists(@"SkyrimPluginEditor.log"))
            {
                System.IO.File.Delete(@"SkyrimPluginEditor.log0");
                System.IO.File.Move(@"SkyrimPluginEditor.log", @"SkyrimPluginEditor.log0");
            }

            var rollingAppender = new RollingFileAppender();
            rollingAppender.Name = "RollingFile";
            rollingAppender.AppendToFile = true;
            rollingAppender.DatePattern = "-yyyy-MM-dd";
            rollingAppender.File = @"SkyrimPluginEditor.log";
            rollingAppender.RollingStyle = RollingFileAppender.RollingMode.Date;
            rollingAppender.Layout = new PatternLayout("%d [%t] %-5p - %m%n");

            var traceAppender = new TraceAppender();

            var repository = LogManager.GetRepository();
            repository.Configured = true;
            var hierarchy = (Hierarchy)repository;
            hierarchy.Root.AddAppender(rollingAppender);
            hierarchy.Root.Level = GetLogLevel(Config.GetSingleton.GetLogLevel());
            rollingAppender.ActivateOptions();
            Logging = LogManager.GetLogger(this.GetType());
        }
        public log4net.Core.Level GetLogLevel(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.None:
                    {
                        return log4net.Core.Level.Off;
                    }
                case LogLevel.Debug:
                    {
                        return log4net.Core.Level.Debug;
                    }
                case LogLevel.Info:
                    {
                        return log4net.Core.Level.Info;
                    }
                case LogLevel.Warning:
                    {
                        return log4net.Core.Level.Warn;
                    }
                case LogLevel.Error:
                    {
                        return log4net.Core.Level.Error;
                    }
                case LogLevel.Fatal:
                    {
                        return log4net.Core.Level.Fatal;
                    }
            }
            return log4net.Core.Level.All;
        }
        public void SetLogLevel(LogLevel logLevel)
        {
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).Root.Level = GetLogLevel(logLevel);
            ((log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository()).RaiseConfigurationChanged(EventArgs.Empty);
        }
    }
}
