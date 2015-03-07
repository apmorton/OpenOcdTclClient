using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace OpenOcdTclClient
{
    public class OpenOcdTclClient
    {
        #region EnumMaps

        private readonly ReadOnlyDictionary<string, TargetEvent> targetEventMap = new ReadOnlyDictionary<string, TargetEvent>(new Dictionary<string, TargetEvent>()
        {
            { "gdb-halt",              TargetEvent.GdbHalt },
            { "halted",                TargetEvent.Halted },
            { "resumed",               TargetEvent.Resumed },
            { "resume-start",          TargetEvent.ResumeStart },
            { "resume-end",            TargetEvent.ResumeEnd },
            { "gdb-start",             TargetEvent.GdbStart },
            { "gdb-end",               TargetEvent.GdbEnd },
            { "reset-start",           TargetEvent.ResetStart },
            { "reset-assert-pre",      TargetEvent.ResetAssertPre },
            { "reset-assert",          TargetEvent.ResetAssert },
            { "reset-assert-post",     TargetEvent.ResetAssertPost },
            { "reset-deassert-pre",    TargetEvent.ResetDeassertPre },
            { "reset-deassert-post",   TargetEvent.ResetDeassertPost },
            { "reset-halt-pre",        TargetEvent.ResetHaltPre },
            { "reset-halt-post",       TargetEvent.ResetHaltPost },
            { "reset-wait-pre",        TargetEvent.ResetWaitPre },
            { "reset-wait-post",       TargetEvent.ResetWaitPost },
            { "reset-init",            TargetEvent.ResetInit },
            { "reset-end",             TargetEvent.ResetEnd },
            { "examine-start",         TargetEvent.ExamineStart },
            { "examine-end",           TargetEvent.ExamineEnd },
            { "debug-halted",          TargetEvent.DebugHalted },
            { "debug-resumed",         TargetEvent.DebugResumed },
            { "gdb-attach",            TargetEvent.GdbAttach },
            { "gdb-detach",            TargetEvent.GdbDetach },
            { "gdb-flash-write-start", TargetEvent.GdbFlashWriteStart },
            { "gdb-flash-write-end",   TargetEvent.GdbFlashWriteEnd },
            { "gdb-flash-erase-start", TargetEvent.GdbFlashEraseStart },
            { "gdb-flash-erase-end",   TargetEvent.GdbFlashEraseEnd },
            { "trace-config",          TargetEvent.TraceConfig }
        });

        private readonly ReadOnlyDictionary<string, TargetState> targetStateMap = new ReadOnlyDictionary<string, TargetState>(new Dictionary<string, TargetState>()
        {
            { "unknown",       TargetState.Unknown },
            { "running",       TargetState.Running },
            { "halted",        TargetState.Halted },
            { "reset",         TargetState.Reset },
            { "debug-running", TargetState.DebugRunning },
        });

        private readonly ReadOnlyDictionary<string, TargetResetMode> targetResetModeMap = new ReadOnlyDictionary<string, TargetResetMode>(new Dictionary<string, TargetResetMode>()
        {
            { "unknown", TargetResetMode.Unknown },
            { "run",     TargetResetMode.Run },
            { "halt",    TargetResetMode.Halt },
            { "init",    TargetResetMode.Init },
        });

        private bool MapTargetEnum<T>(ReadOnlyDictionary<string, T> map, string match, out T val)
        {
            foreach (KeyValuePair<string, T> mapping in map)
            {
                if (String.Compare(mapping.Key, match, true) == 0)
                {
                    val = mapping.Value;
                    return true;
                }
            }

            val = default(T);
            return false;
        }

        #endregion

        #region Properties

        public readonly string Hostname;
        public readonly int Port;
        public bool Connected { get; private set; }

        public bool Notifications { get; set; }
        public bool Trace { get; set; }

        #endregion

        #region Commands

        public uint ReadMemory(uint address)
        {
            uint ret = 0;

            var result = DoCommand(String.Format("ocd_mdw 0x{0:X}", address));
            if (result == null) return ret;

            var addr = result.Split(':')[0].Trim();
            var val = result.Split(':')[1].Trim();
            ret = Convert.ToUInt32(val, 16);

            return ret;
        }

        #endregion

        #region Public Functions

        public OpenOcdTclClient(ISynchronizeInvoke context, string hostname = "localhost", int port = 6666)
        {
            Hostname = hostname;
            Port = port;
            Connected = false;
            this.context = context;
        }

        public void Start()
        {
            if (!started)
            {
                thread = new Thread(this.ThreadMain);
                thread.IsBackground = true;
                thread.Start();
                started = true;
            }
        }

        public void Stop()
        {
            if (started)
            {
                thread.Abort();
                thread = null;
                started = false;
            }
        }

        #endregion

        #region Events

        #region Target Event

        public class TargetEventArgs : EventArgs
        {
            public TargetEvent EventType;

            public TargetEventArgs(TargetEvent e)
            {
                EventType = e;
            }
        }

        public delegate void TargetEventHandler(object sender, TargetEventArgs args);
        public TargetEventHandler TargetEventRaised;
        public void OnTargetEventRaised(TargetEvent e)
        {
            if (TargetEventRaised != null)
            {
                Invoke(() => TargetEventRaised(this, new TargetEventArgs(e)));
            }
        }

        #endregion

        #region Target State

        public class TargetStateArgs : EventArgs
        {
            public TargetState State;

            public TargetStateArgs(TargetState e)
            {
                State = e;
            }
        }

        public delegate void TargetStateHandler(object sender, TargetStateArgs args);
        public TargetStateHandler TargetStateChanged;
        public void OnTargetStateChanged(TargetState e)
        {
            if (TargetStateChanged != null)
            {
                Invoke(() => TargetStateChanged(this, new TargetStateArgs(e)));
            }
        }

        #endregion

        #region Target Reset

        public class TargetResetArgs : EventArgs
        {
            public TargetResetMode ResetMode;

            public TargetResetArgs(TargetResetMode e)
            {
                ResetMode = e;
            }
        }

        public delegate void TargetResetHandler(object sender, TargetResetArgs args);
        public TargetResetHandler TargetReset;
        public void OnTargetReset(TargetResetMode e)
        {
            if (TargetReset != null)
            {
                Invoke(() => TargetReset(this, new TargetResetArgs(e)));
            }
        }

        #endregion

        #region Target Trace

        public class TargetTraceArgs : EventArgs
        {
            public byte[] Data;

            public TargetTraceArgs(byte[] e)
            {
                Data = e;
            }
        }

        public delegate void TargetTraceHandler(object sender, TargetTraceArgs args);
        public TargetTraceHandler TargetTrace;
        public void OnTargetTrace(byte[] e)
        {
            if (TargetTrace != null)
            {
                Invoke(() => TargetTrace(this, new TargetTraceArgs(e)));
            }
        }

        #endregion

        #region Connection

        public EventHandler ConnectionChanged;
        public void OnConnectionChanged()
        {
            if (ConnectionChanged != null)
            {
                Invoke(() => ConnectionChanged(this, new EventArgs()));
            }
        }

        #endregion

        #endregion

        #region Internal Implementation

        private class Command
        {
            public readonly string Send;
            public string Result;
            public readonly ManualResetEvent Completed;

            public Command(string command)
            {
                Send = command;
                Completed = new ManualResetEvent(false);
            }
        }

        private Thread thread;
        private Socket socket = null;
        private readonly ISynchronizeInvoke context;
        private readonly ConcurrentQueue<Command> commands = new ConcurrentQueue<Command>();
        private bool started = false;

        private void ThreadMain()
        {
            while (true)
            {
                // dispose non connected sockets
                if (socket != null && (!socket.Connected || Connected == false))
                {
                    socket.Dispose();
                    socket = null;
                    Connected = false;
                    OnConnectionChanged();
                }

                // try reconnection
                if (socket == null)
                {
                    socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                    try
                    {
                        // connect to server
                        socket.Connect(Hostname, Port);

                        // turn on notifications
                        if (Notifications) SendCommand("tcl_notifications on");

                        // turn on trace output
                        if (Trace) SendCommand("tcl_trace on");

                        Connected = true;
                        OnConnectionChanged();
                    }
                    catch (SocketException)
                    {
                        // connection failed, delay retry
                        Thread.Sleep(100);
                        continue;
                    }
                }

                try
                {
                    // main connection loop
                    while (true)
                    {
                        Command command;

                        // check connected
                        if ((socket.Poll(1000, SelectMode.SelectRead) && (socket.Available == 0)) || !socket.Connected)
                        {
                            Connected = false;
                            break;
                        }

                        // handle commands
                        while (commands.TryDequeue(out command))
                        {
                            command.Result = SendCommand(command.Send);
                            command.Completed.Set();
                        }

                        // handle poll
                        var eventText = ReceiveResponse(true);
                        if (eventText == null) continue;

                        // handle async output
                        HandleAsync(eventText);
                    }
                }
                catch (SocketException)
                {
                    // socket failed, next loop will dispose and reconnect
                    Connected = false;
                    continue;
                }
            }
        }

        private bool HandleAsync(string response)
        {
            if (response.StartsWith("type target_trace data "))
            {
                HandleTrace(response);
                return true;
            }
            else if (response.StartsWith("type target_event event "))
            {
                HandleEvent(response);
                return true;
            }
            else if (response.StartsWith("type target_state state "))
            {
                HandleState(response);
                return true;
            }
            else if (response.StartsWith("type target_reset mode "))
            {
                HandleReset(response);
                return true;
            }

            return false;
        }

        private void HandleTrace(string response)
        {
            // remove event prefix
            response = response.Replace("type target_trace data ", "");

            // convert to byte array
            var data = Enumerable.Range(0, response.Length).Where(x => x % 2 == 0)
                .Select(x => Convert.ToByte(response.Substring(x, 2), 16)).ToArray();

            // raise trace event
            OnTargetTrace(data);
        }

        private void HandleEvent(string response)
        {
            TargetEvent val;

            // remove event prefix
            response = response.Replace("type target_event event ", "");

            // map event enum and raise event on success
            if (MapTargetEnum(targetEventMap, response, out val))
                OnTargetEventRaised(val);
        }

        private void HandleState(string response)
        {
            TargetState val;

            // remove event prefix
            response = response.Replace("type target_state state ", "");

            // map state enum and raise event on success
            if (MapTargetEnum(targetStateMap, response, out val))
                OnTargetStateChanged(val);
        }

        private void HandleReset(string response)
        {
            TargetResetMode val;

            // remove event prefix
            response = response.Replace("type target_reset mode ", "");

            // map reset mode enum and raise event on success
            if (MapTargetEnum(targetResetModeMap, response, out val))
                OnTargetReset(val);
        }

        private string SendCommand(string command)
        {
            // encode the command string and add the terminator
            var buffer = Encoding.UTF8.GetBytes(command + "\x1a");

            // send the buffer
            socket.Send(buffer);

            // receive the response, and handle timing issues with events
            string resp;
            do
            {
                resp = ReceiveResponse();
            } while (HandleAsync(resp));

            return resp;
        }

        private string DoCommand(string command)
        {
            var cmd = new Command(command);
            commands.Enqueue(cmd);
            cmd.Completed.WaitOne();
            return cmd.Result;
        }

        private string ReceiveResponse(bool onlyIfAvailable = false)
        {
            var buffer = new byte[4096];
            int offset = 0;

            // bail if we aren't supposed to block and nothing is available
            if (onlyIfAvailable && socket.Available == 0) return null;

            // read until the buffer is full or we have a terminator
            while (offset < buffer.Length)
            {
                offset += socket.Receive(buffer, offset, buffer.Length - offset, SocketFlags.None);
                if (buffer.Contains((byte)0x1a)) break;
            }

            // get the index of the terminator
            var end = Array.IndexOf<byte>(buffer, 0x1a);

            // decode the bytes
            return Encoding.UTF8.GetString(buffer, 0, end).Trim();
        }

        private void Invoke(Action action)
        {
            context.BeginInvoke((Delegate)action, null);
        }

        #endregion
    }
}
