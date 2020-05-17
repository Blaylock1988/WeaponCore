﻿using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using Sandbox.ModAPI;
using VRage.Collections;

namespace WeaponCore.Support
{
    public static class Log
    {
        private static MyConcurrentPool<LogInstance> _logPool = new MyConcurrentPool<LogInstance>(128);
        private static ConcurrentDictionary<string, LogInstance> _instances = new ConcurrentDictionary<string, LogInstance>();
        private static ConcurrentQueue<string[]> _threadedLineQueue = new ConcurrentQueue<string[]>();
        private static string _defaultInstance;

        public class LogInstance
        {
            internal TextWriter TextWriter = null;
            internal Session Session;
            internal void Clean()
            {
                TextWriter = null;
                Session = null;
            }
        }

        public static void Init(string name, Session session, bool defaultInstance = true)
        {
            try
            {
                var filename = name + ".log";
                if (_instances.ContainsKey(name)) return;
                RenameFileInLocalStorage(filename, name + $"-{DateTime.Now:MM-dd-yy_HH-mm-ss}.log", typeof(LogInstance));

                if (defaultInstance) _defaultInstance = name;
                var instance = _logPool.Get();

                instance.Session = session;
                _instances[name] = instance;

                instance.TextWriter = MyAPIGateway.Utilities.WriteFileInLocalStorage(filename, typeof(LogInstance));
                Line($"Logging Started", name);
            }
            catch (Exception e)
            {
                MyAPIGateway.Utilities.ShowNotification(e.Message, 5000);
            }
        }

        public static void RenameFileInLocalStorage(string oldName, string newName, Type anyObjectInYourMod)
        {
            if (!MyAPIGateway.Utilities.FileExistsInLocalStorage(oldName, anyObjectInYourMod))
                return;

            if (MyAPIGateway.Utilities.FileExistsInLocalStorage(newName, anyObjectInYourMod))
                return;

            using (var read = MyAPIGateway.Utilities.ReadFileInLocalStorage(oldName, anyObjectInYourMod))
            {
                using (var write = MyAPIGateway.Utilities.WriteFileInLocalStorage(newName, anyObjectInYourMod))
                {
                    write.Write(read.ReadToEnd());
                    write.Flush();
                    write.Dispose();
                }
            }

            MyAPIGateway.Utilities.DeleteFileInLocalStorage(oldName, anyObjectInYourMod);
        }

        public static void NetLogger(Session session, string message)
        {
            var test = Encoding.UTF8.GetBytes(message);
            foreach (var a in session.ConnectedAuthors)
            {
                MyModAPIHelper.MyMultiplayer.Static.SendMessageTo(Session.AuthorPacketId, test, a.Value, true);
            }
        }

        public static void Line(string text, string instanceName = null)
        {
            try
            {
                var name  = instanceName ?? _defaultInstance;
                var instance = _instances[name];
                if (instance.TextWriter != null)
                {
                    var message = $"{DateTime.Now:MM-dd-yy_HH-mm-ss-fff} - " + text;
                    instance.TextWriter.WriteLine(message);
                    instance.TextWriter.Flush();
                    var set = instance.Session.AuthorSettings;
                    var netEnabled = instance.Session.AuthLogging && name == _defaultInstance && set[0] >= 0 || name == "perf" && set[1] >= 0 || name == "stats" && set[2] >= 0;
                    if (netEnabled)
                        NetLogger(instance.Session, "[R-LOG] " + message);
                }
            }
            catch (Exception e)
            {
            }
        }

        public static void LineShortDate(string text, string instanceName = null)
        {
            try
            {
                var name = instanceName ?? _defaultInstance;
                var instance = _instances[name];
                if (instance.TextWriter != null)
                {
                    var message = $"{DateTime.Now:HH-mm-ss-fff} - " + text;
                    instance.TextWriter.WriteLine(message);
                    instance.TextWriter.Flush();

                    var set = instance.Session.AuthorSettings;
                    var netEnabled = instance.Session.AuthLogging && name == _defaultInstance && set[0] >= 0 || name == "perf" && set[1] >= 0 || name == "stats" && set[2] >= 0;
                    if (netEnabled)
                        NetLogger(instance.Session, "[R-LOG] " + message);
                }
            }
            catch (Exception e)
            {
            }
        }

        public static void NetLog(string text, Session session, int logLevel)
        {
            var set = session.AuthorSettings;
            var netEnabled = session.AuthLogging && set[3] >= 0 && logLevel >= set[4];
            if (netEnabled)
                NetLogger(session, "[R-LOG] " + text);
        }

        public static void Chars(string text, string instanceName = null)
        {
            try
            {
                var name = instanceName ?? _defaultInstance;
                var instance = _instances[name];
                if (instance.TextWriter != null)
                {
                    instance.TextWriter.Write(text);
                    instance.TextWriter.Flush();

                    var set = instance.Session.AuthorSettings;
                    var netEnabled = instance.Session.AuthLogging && name == _defaultInstance && set[0] >= 0 || name == "perf" && set[1] >= 0 || name == "stats" && set[2] >= 0;
                    if (netEnabled)
                        NetLogger(instance.Session, "[R-LOG] " + text);
                }
            }
            catch (Exception e)
            {
            }
        }

        public static void CleanLine(string text, string instanceName = null)
        {
            try
            {
                var name = instanceName ?? _defaultInstance;
                var instance = _instances[name];
                if (instance.TextWriter != null)
                {
                    instance.TextWriter.WriteLine(text);
                    instance.TextWriter.Flush();

                    var set = instance.Session.AuthorSettings;
                    var netEnabled = instance.Session.AuthLogging && name == _defaultInstance && set[0] >= 0 || name == "perf" && set[1] >= 0 || name == "stats" && set[2] >= 0;
                    if (netEnabled)
                        NetLogger(instance.Session, "[R-LOG] " + text);
                }
            }
            catch (Exception e)
            {
            }
        }

        public static void ThreadedWrite(string logLine)
        {
            _threadedLineQueue.Enqueue(new string[] { $"Threaded Time:  {DateTime.Now:HH-mm-ss-fff} - ", logLine });
            MyAPIGateway.Utilities.InvokeOnGameThread(WriteLog);
        }

        private static void WriteLog() {
            string[] line;

            var instance = _instances[_defaultInstance];
            if (instance.TextWriter != null)
                Init("debugdevelop.log", null);

            instance = _instances[_defaultInstance];           

            while (_threadedLineQueue.TryDequeue(out line))
            {
                if (instance.TextWriter != null)
                {
                    instance.TextWriter.WriteLine(line[0] + line[1]);
                    instance.TextWriter.Flush();
                }
            }
        }

        public static void Close()
        {
            try
            {
                _threadedLineQueue.Clear();
                foreach (var pair in _instances)
                {
                    pair.Value.TextWriter.Flush();
                    pair.Value.TextWriter.Close();
                    pair.Value.TextWriter.Dispose();
                    pair.Value.Clean();

                    _logPool.Return(pair.Value);

                }
                _instances.Clear();
                _logPool.Clean();
                _logPool = null;
                _instances = null;
                _threadedLineQueue = null;
                _defaultInstance = null;
            }
            catch (Exception e)
            {
            }
        }
    }
}
